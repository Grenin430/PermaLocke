using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Looks for where the game keeps the player's map and position while playing, using the last save as the
/// ground truth. Only reads.
/// </summary>
/// <remarks>
/// <para>
/// The save's <c>Situation</c> block holds the map the player saved on (a 16-bit number at +0) and the
/// position as three floats at +8, +0xC and +0x10. Twelve bytes of exact floats cannot turn up by chance, so
/// right after saving — before the player moves — one search for them says where the game keeps them.
/// </para>
/// <para>
/// Shaped by the two freezes (§114, §114 ter): <b>one</b> search per run, over the two megabytes where the
/// bag and the party live and that are known to be mapped, and the watch only reads a few dozen bytes per
/// candidate.
/// </para>
/// </remarks>
public static class SituationProbe
{
    private const uint SearchStart = 0x33000000;
    private const uint SearchSize = 0x00200000;

    /// <summary>From the map field to the end of the three position floats.</summary>
    private const int RecordLength = 0x14;

    public static int Run(string mode, string[] rest)
    {
        return mode switch
        {
            "buscar" => Search(),
            "amplio" => SearchWide(),
            "firma" => SearchSignature(),
            "cargada" => SearchLoaded(),
            "vigilar" => Watch(rest),
            _ => ShowSaved()
        };
    }

    private static (SAV7USUM Save, byte[] Bytes, int Offset, string Path)? LoadSave(AzaharRpcClient? client)
    {
        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client ?? new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return null;
        }

        var bytes = File.ReadAllBytes(path);

        if (!SaveUtil.TryGetSaveFile(bytes.ToArray(), out var loaded) || loaded is not SAV7USUM saved)
        {
            Console.WriteLine("PKHeX no reconoce la partida como Ultra Luna.");
            return null;
        }

        // El bloque 1 es Situation: medido, M en +0 y las tres coordenadas en +8, +0xC y +0x10.
        return (saved, bytes, saved.AllBlocks[1].Offset, path);
    }

    private static int ShowSaved()
    {
        if (LoadSave(null) is not { } loaded)
        {
            return 1;
        }

        Console.WriteLine($"Partida: {loaded.Path}");
        Console.WriteLine($"Guardada: {File.GetLastWriteTime(loaded.Path):yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
        Print("GUARDADO", loaded.Bytes.AsSpan(loaded.Offset, RecordLength));
        Console.WriteLine($"LastZoneID = {loaded.Save.Situation.LastZoneID}");
        return 0;
    }

    private static int Search()
    {
        var client = Attach();

        if (client is null || LoadSave(client) is not { } loaded)
        {
            return 1;
        }

        var pattern = loaded.Bytes.AsSpan(loaded.Offset + 8, 12).ToArray();
        var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();

        Console.WriteLine($"Guardada: {File.GetLastWriteTime(loaded.Path):yyyy-MM-dd HH:mm:ss}");
        Print("GUARDADO", loaded.Bytes.AsSpan(loaded.Offset, RecordLength));
        Console.WriteLine();
        Console.WriteLine($"Una búsqueda de {BitConverter.ToString(pattern)} en 0x{SearchStart:X8} + {SearchSize / 0x100000} MB...");

        var watch = Stopwatch.StartNew();
        var hits = client.SearchMemory(SearchStart, SearchSize, pattern, mask, stride: 4);
        Console.WriteLine($"{hits.Count} coincidencias en {watch.ElapsedMilliseconds} ms");
        Console.WriteLine();

        foreach (var hit in hits)
        {
            var record = hit - 8;

            if (client.TryReadMemory(record, RecordLength, out var bytes))
            {
                Print($"0x{record:X8}", bytes);
            }
        }

        if (hits.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Para seguirlas mientras andas:");
            Console.WriteLine("  --situacion vigilar " + string.Join(' ', hits.Select(hit => $"0x{hit - 8:X8}")));
        }

        return 0;
    }

    /// <summary>
    /// One search for the saved X alone across the 64 MB of linear heap the bag locator already reads every
    /// session, printing what sits around each hit.
    /// </summary>
    /// <remarks>
    /// The first search found only copies that change on saving: the live position is elsewhere, and maybe
    /// not laid out as the save lays it out. So four bytes instead of twelve, and the other two coordinates are
    /// looked for near each hit instead of being assumed at +4 and +8.
    /// </remarks>
    private static int SearchWide()
    {
        const uint Start = 0x30000000, Size = 0x04000000;

        var client = Attach();

        if (client is null || LoadSave(client) is not { } loaded)
        {
            return 1;
        }

        var x = loaded.Bytes.AsSpan(loaded.Offset + 8, 4).ToArray();
        var y = BitConverter.ToSingle(loaded.Bytes, loaded.Offset + 0xC);
        var z = BitConverter.ToSingle(loaded.Bytes, loaded.Offset + 0x10);

        Console.WriteLine($"Guardada: {File.GetLastWriteTime(loaded.Path):yyyy-MM-dd HH:mm:ss}");
        Print("GUARDADO", loaded.Bytes.AsSpan(loaded.Offset, RecordLength));
        Console.WriteLine();
        Console.WriteLine($"Una búsqueda de X ({BitConverter.ToString(x)}) en 0x{Start:X8} + {Size / 0x100000} MB...");

        var watch = Stopwatch.StartNew();
        var hits = client.SearchMemory(Start, Size, x, [0xFF, 0xFF, 0xFF, 0xFF], stride: 4);
        Console.WriteLine($"{hits.Count} coincidencias en {watch.ElapsedMilliseconds} ms{(hits.Count >= 255 ? " (TOPE: hay más)" : string.Empty)}");
        Console.WriteLine();

        foreach (var hit in hits)
        {
            if (!client.TryReadMemory(hit - 0x20, 0x40, out var around))
            {
                continue;
            }

            // Dónde, cerca de X, están también Y y Z, sin suponer el orden del registro.
            var near = new List<string>();

            for (var at = 0; at + 4 <= around.Length; at += 4)
            {
                var value = BitConverter.ToSingle(around, at);

                if (Math.Abs(value - y) < 2)
                {
                    near.Add($"Y@{at - 0x20:+0;-0}");
                }

                if (Math.Abs(value - z) < 2)
                {
                    near.Add($"Z@{at - 0x20:+0;-0}");
                }
            }

            Console.WriteLine($"0x{hit:X8}  {string.Join(' ', near)}");
            Console.WriteLine($"    {BitConverter.ToString(around, 0, 0x20)}");
            Console.WriteLine($"    {BitConverter.ToString(around, 0x20, 0x20)}");
        }

        return 0;
    }

    /// <summary>
    /// Finds the live position record by its shape alone, without knowing where the player is.
    /// </summary>
    /// <remarks>
    /// Measured on 2026-09-13: the live record has the world and the map as two 16-bit numbers right before X,
    /// then X, Y, Z, an identity quaternion (0, 0, 0, 1) and <c>FFFFFFFF</c>. The search is for the last twenty
    /// bytes of that; each hit is then printed with its world and map so they can be checked against zonedata.
    /// At most four pages, so a pattern commoner than expected cannot turn into a burst (§114 ter).
    /// </remarks>
    private static int SearchSignature()
    {
        const uint Start = 0x30000000, End = 0x34000000;
        byte[] pattern = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x00, 0x80, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF];
        var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();

        var client = Attach();

        if (client is null)
        {
            return 1;
        }

        var hits = new List<uint>();
        var from = Start;
        var watch = Stopwatch.StartNew();

        for (var page = 0; page < 4 && from < End; page++)
        {
            var found = client.SearchMemory(from, End - from, pattern, mask, stride: 4);
            hits.AddRange(found);

            if (found.Count < 255)
            {
                break;
            }

            from = found[^1] + 4;
            Thread.Sleep(200);
        }

        Console.WriteLine($"{hits.Count} coincidencias de la firma en {watch.ElapsedMilliseconds} ms");

        var plausible = 0;

        foreach (var hit in hits)
        {
            var x = hit - 0x0C;

            if (!client.TryReadMemory(x - 4, 0x10, out var bytes))
            {
                continue;
            }

            var world = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            var map = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
            var px = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(4));
            var py = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(8));
            var pz = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(12));

            if (map >= 380 || world >= 300 || !float.IsFinite(px) || !float.IsFinite(py) || !float.IsFinite(pz)
                || (px == 0 && py == 0 && pz == 0) || Math.Abs(px) > 100000 || Math.Abs(pz) > 100000)
            {
                continue;
            }

            plausible++;
            Console.WriteLine($"  X en 0x{x:X8}  mundo {world,3}  mapa {map,3}  X={px,10:F2}  Y={py,9:F2}  Z={pz,10:F2}");
        }

        Console.WriteLine($"{plausible} con forma de registro de posición");
        return 0;
    }

    /// <summary>
    /// Right after loading, before moving: the live records hold exactly the saved world, map and position, laid
    /// out as world, map, X, Y, Z. Sixteen exact bytes from the save; one search.
    /// </summary>
    private static int SearchLoaded()
    {
        const uint Start = 0x30000000, Size = 0x04000000;

        var client = Attach();

        if (client is null || LoadSave(client) is not { } loaded)
        {
            return 1;
        }

        var pattern = new byte[16];
        loaded.Bytes.AsSpan(loaded.Offset, 4).CopyTo(pattern);
        loaded.Bytes.AsSpan(loaded.Offset + 8, 12).CopyTo(pattern.AsSpan(4));

        Print("GUARDADO", loaded.Bytes.AsSpan(loaded.Offset, RecordLength));
        Console.WriteLine($"Una búsqueda de {BitConverter.ToString(pattern)} en 0x{Start:X8} + {Size / 0x100000} MB...");

        var watch = Stopwatch.StartNew();
        var hits = client.SearchMemory(Start, Size, pattern, Enumerable.Repeat((byte)0xFF, 16).ToArray(), stride: 4);
        Console.WriteLine($"{hits.Count} coincidencias en {watch.ElapsedMilliseconds} ms");

        foreach (var hit in hits)
        {
            if (client.TryReadMemory(hit, 0x24, out var bytes))
            {
                var q = new[] { 0x10, 0x14, 0x18, 0x1C }.Select(at => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at))).ToArray();
                var norm = Math.Sqrt(q.Sum(v => (double)v * v));
                Console.WriteLine($"  X en 0x{hit + 4:X8}  rot=({string.Join("; ", q.Select(v => v.ToString("F3")))}) |q|={norm:F3}  "
                                  + $"+0x20={BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x20)):X8}");
            }
        }

        return 0;
    }

    private static int Watch(string[] rest)
    {
        var client = Attach();

        if (client is null)
        {
            return 1;
        }

        var addresses = rest
            .Where(arg => arg.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            .Select(arg => Convert.ToUInt32(arg[2..], 16))
            .ToArray();

        var seconds = rest.FirstOrDefault(arg => int.TryParse(arg, out _)) is { } text ? int.Parse(text) : 300;

        if (addresses.Length == 0)
        {
            Console.WriteLine("Dime qué direcciones vigilar: --situacion vigilar 0x33xxxxxx ...");
            return 1;
        }

        Console.WriteLine($"Vigilando {addresses.Length} direcciones durante {seconds} s. Anda, cambia de zona, entra en edificios.");
        var last = new string?[addresses.Length];
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            for (var i = 0; i < addresses.Length; i++)
            {
                if (!client.TryReadMemory(addresses[i], RecordLength, out var bytes))
                {
                    continue;
                }

                var line = Describe(bytes);

                if (line != last[i])
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.f}  0x{addresses[i]:X8}  {line}");
                    last[i] = line;
                }
            }

            Thread.Sleep(400);
        }

        return 0;
    }

    private static AzaharRpcClient? Attach()
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
            return client;
        }
        catch (Exception ex)
        {
            Console.WriteLine("No se puede enganchar al juego: " + ex.Message);
            return null;
        }
    }

    private static void Print(string label, ReadOnlySpan<byte> record) =>
        Console.WriteLine($"{label,-12} {Describe(record)}");

    private static string Describe(ReadOnlySpan<byte> record) =>
        $"M={BinaryPrimitives.ReadUInt16LittleEndian(record),5}  +2={BinaryPrimitives.ReadUInt16LittleEndian(record[2..]),5}  "
        + $"+4={BinaryPrimitives.ReadUInt32LittleEndian(record[4..]):X8}  "
        + $"X={BinaryPrimitives.ReadSingleLittleEndian(record[8..]),10:F2}  "
        + $"Y={BinaryPrimitives.ReadSingleLittleEndian(record[0xC..]),9:F2}  "
        + $"Z={BinaryPrimitives.ReadSingleLittleEndian(record[0x10..]),10:F2}";
}
