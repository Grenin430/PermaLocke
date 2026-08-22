using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Shows the four copies of the area field the game keeps, and why they are or are not believed.
/// </summary>
/// <remarks>
/// The zone is read at fixed distances from the bag block, which is the one structure that can be
/// located on its own. A reading is only believed when the four copies agree and none of them sits
/// in blank memory, so "PermaLocke no sabe dónde está el jugador" can mean several very different
/// things — the copies disagreeing, one of them being blank, or the distances no longer holding.
/// This says which.
/// </remarks>
public static class ZoneProbe
{
    public static int Run()
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var bag = new BagService(client,
            new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
                NullLogger<AzaharGameWriter>.Instance),
            Path.Combine(Path.GetTempPath(), "permalocke-probe", "retirados.txt"),
            Path.Combine(Path.GetTempPath(), "permalocke-probe", "mochila.txt"),
            NullLogger<BagService>.Instance);

        if (bag.Locate() is not { } block)
        {
            Console.WriteLine("No se ha encontrado la mochila, que es el ancla de la zona.");
            return 1;
        }

        Console.WriteLine($"Mochila en 0x{block.BaseAddress:X8}");
        Console.WriteLine("(medida en su dia en 0x33011934; si no coincide, las distancias no valen)");
        Console.WriteLine();

        var readings = new ZoneReading[ZoneLocator.CopyOffsets.Count];

        for (var copy = 0; copy < ZoneLocator.CopyOffsets.Count; copy++)
        {
            var handleAt = block.BaseAddress + ZoneLocator.CopyOffsets[copy] - 0x10;

            if (!client.TryReadMemory(handleAt, 0x14, out var data))
            {
                Console.WriteLine($"  copia {copy}: 0x{handleAt + 0x10:X8}  ILEGIBLE");
                continue;
            }

            readings[copy] = new ZoneReading(BitConverter.ToUInt32(data, 0), BitConverter.ToUInt16(data, 0x10));

            var live = readings[copy].LooksLive ? "" : "   <-- asa a cero: memoria en blanco";

            Console.WriteLine($"  copia {copy}: 0x{handleAt + 0x10:X8}  "
                              + $"asa {readings[copy].Anchor:X8}  zona {readings[copy].Area,3}{live}");
        }

        Console.WriteLine();

        if (ZoneLocator.TryResolve(readings, out var area))
        {
            var name = GameInfo.GetStrings("es").GetLocationName(false, (ushort)area, 7, 7, GameVersion.UM);
            Console.WriteLine($"Las cuatro coinciden: zona {area}"
                              + (string.IsNullOrWhiteSpace(name) ? "" : $"  (por id de mapa seria «{name}»)"));
            return 0;
        }

        // Por qué no, que es lo que hay que saber para arreglarlo.
        var areas = readings.Select(r => r.Area).Distinct().ToList();
        var blank = readings.Count(r => !r.LooksLive);

        Console.WriteLine("NO se cree la lectura:");

        if (blank > 0)
        {
            Console.WriteLine($"  - {blank} de {readings.Length} copias tienen el asa a cero.");
        }

        if (areas.Count > 1)
        {
            Console.WriteLine($"  - las copias no dicen lo mismo: {string.Join(", ", areas)}");
        }

        if (areas.Count == 1 && areas[0] >= ZoneLocator.UltraSunMoonAreaCount)
        {
            Console.WriteLine($"  - dicen {areas[0]}, y encdata solo tiene "
                              + $"{ZoneLocator.UltraSunMoonAreaCount} zonas.");
        }

        return 1;
    }
}
