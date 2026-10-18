using PermaLocke.GameLink;
using PermaLocke.GameLink.Field;
using PermaLocke.GameLink.Rpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace PermaLocke.Probe;

/// <summary>
/// <c>--repelente</c> (2026-10-16): where the running game keeps the Repel. With the Repelente Infinito on, the Repel's item (114) and its one
/// step sit 0x1A bytes apart in <c>Field::EventWork</c> (+0xA3E and +0xA58); this looks for that pair round the save data in memory and says
/// where each is from the save data's start. Only reads.
/// </summary>
internal static class RepelProbe
{
    public static int Run()
    {
        using var client = new AzaharRpcClient();
        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client, AppContext.BaseDirectory);
        var live = new LiveSave(client, new SavedGameCache(save));
        if (live.Base() is not { } start)
        {
            Console.WriteLine("No llego a la partida en memoria.");
            return 1;
        }

        Console.WriteLine($"Partida en memoria desde 0x{start:X8}; EventWork según la app en +0x015DC.");
        const uint Chunk = 0x1000;
        var from = Math.Max(0x30000000u, start - 0x200000);
        var to = Math.Min(0x34000000u, start + 0x200000);
        var hits = 0;
        for (var at = from; at < to; at += Chunk)
        {
            if (!client.TryReadMemory(at, (int)Chunk + 0x20, out var part)) continue;
            for (var i = 0; i + 0x1C <= part.Length && i < Chunk; i += 2)
            {
                if (BitConverter.ToUInt16(part, i) != 114) continue;
                var steps = BitConverter.ToUInt16(part, i + 0x1A);
                if (steps is 0 or > 3) continue;
                var address = at + (uint)i;
                Console.WriteLine($"  0x{address:X8} (partida +0x{address - start:X}): objeto 114, pasos {steps}; EventWork estaría en +0x{address - 0xA3E - start:X}");
                if (++hits > 40) return 0;
            }
        }

        Console.WriteLine(hits == 0 ? "Ninguna pareja. ¿Está el Repelente Infinito encendido?" : $"{hits} sitios.");
        return 0;
    }
}
