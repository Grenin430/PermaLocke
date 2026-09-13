using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Finds where the game keeps the party's HP <b>during a battle</b>, in three steps taken while the
/// battle waits at its move menu.
/// </summary>
/// <remarks>
/// <para>
/// A death is only seen when a battle ends, because the party structure the game reads outside
/// battle (§99, <c>0x1E4 + 0x158</c>) is not touched until then. The battle keeps its own copy, and
/// that is the one a real-time death needs.
/// </para>
/// <para>
/// <b>The first version froze the emulator and cost the player unsaved progress</b>, and the design
/// below exists because of that. It searched every four seconds across the whole heap range, of which
/// only the first 4 MB exist: the emulator logs an error for every missing page, filled its log to
/// the 100 MB cap in minutes, and stops emulating while it searches. So now:
/// </para>
/// <list type="bullet">
/// <item><c>buscar</c> searches <b>once</b>, only in memory measured to exist (the unmapped pages were
/// read off that same log), while the battle sits at its menu waiting for the player.</item>
/// <item><c>filtrar</c>, after a hit, reads each candidate <b>once</b> and keeps what dropped with its
/// maximum intact.</item>
/// <item><c>vigilar</c> records only the few survivors, and refuses to start with many.</item>
/// </list>
/// <para>
/// Read-only throughout: nothing is written to the game.
/// </para>
/// </remarks>
public static class BattleHpProbe
{
    /// <summary>
    /// Memory that exists. The heap ends at 0x08425000 — every page from there to 0x0A000000 came back
    /// unmapped in the emulator's log — and the linear heap is mapped whole.
    /// </summary>
    private static readonly MemoryRegion[] Mapped =
    [
        new(0x08000000, 0x08400000, "monton"),
        new(0x30000000, 0x34000000, "linear")
    ];

    private const int MaxWatched = 40;

    private static string Folder => Path.Combine(new PermaLocke.Infrastructure.AppPaths().Root, "Logs");

    private static string CandidatesFile => Path.Combine(Folder, "combate-candidatos.txt");

    public static int Run(string step, int seconds)
    {
        // Solo mira la pantalla: ni se engancha al emulador.
        if (step == "ver-barra")
        {
            return BarProbe.See(seconds);
        }

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

        return step switch
        {
            "buscar" => Search(client),
            "filtrar" => Filter(client),
            "vigilar" => Watch(client, seconds),
            "estructuras" => Structures(client),
            "detector" => Detector(client, seconds),
            "barra" => BarProbe.Run(client, seconds),
            "indicador" => GaugeProbe.Run(client, seconds),
            _ => Usage()
        };
    }

    private static int Usage()
    {
        Console.WriteLine("Uso, con el combate esperando en el menú de ataques:");
        Console.WriteLine("  Probe --combate buscar             una búsqueda con los PS de ahora");
        Console.WriteLine("  Probe --combate filtrar            tras un golpe: se queda con lo que ha bajado");
        Console.WriteLine("  Probe --combate vigilar [segundos] graba los supervivientes");
        return 1;
    }

    private static int Search(AzaharRpcClient client)
    {
        var party = ReadParty(client);

        if (party.Count == 0)
        {
            Console.WriteLine("No se ha podido leer el equipo: no se busca nada.");
            return 1;
        }

        Console.WriteLine("Equipo según la estructura 0x1E4: " + Describe(party));

        var clock = Stopwatch.StartNew();
        var candidates = new Dictionary<uint, Candidate>();
        var searches = 0;

        foreach (var member in party.Where(member => member.CurrentHp > 0 && member.MaxHp > 0))
        {
            foreach (var gap in new[] { 0, 2, 4, 6 })
            {
                foreach (var hpFirst in new[] { true, false })
                {
                    var (pattern, mask) = Pattern(member.CurrentHp, member.MaxHp, gap, hpFirst);

                    foreach (var hit in SearchAll(client, pattern, mask, ref searches))
                    {
                        var hp = hpFirst ? hit : hit + 2u + (uint)gap;
                        var max = hpFirst ? hit + 2u + (uint)gap : hit;

                        candidates.TryAdd(hp, new Candidate(hp, max, member.Slot, (ushort)member.CurrentHp,
                            (ushort)member.MaxHp, hpFirst ? $"PS,+{gap},MAX" : $"MAX,+{gap},PS"));
                    }
                }
            }
        }

        Directory.CreateDirectory(Folder);
        File.WriteAllLines(CandidatesFile, candidates.Values.Select(candidate => candidate.ToLine()));

        Console.WriteLine($"{searches} búsquedas en {clock.Elapsed.TotalSeconds:F1} s: {candidates.Count} candidatos.");

        foreach (var group in candidates.Values.GroupBy(candidate => candidate.Slot))
        {
            Console.WriteLine($"  hueco {group.Key}: {group.Count()}");
        }

        Console.WriteLine();
        Console.WriteLine("Ahora recibe un golpe, vuelve al menú y ejecuta:  Probe --combate filtrar");
        return 0;
    }

    private static int Filter(AzaharRpcClient client)
    {
        if (!File.Exists(CandidatesFile))
        {
            Console.WriteLine("No hay candidatos: primero  Probe --combate buscar");
            return 1;
        }

        var candidates = File.ReadAllLines(CandidatesFile).Select(Candidate.FromLine).ToList();
        var party = ReadParty(client);

        Console.WriteLine("Equipo según la estructura 0x1E4: " + Describe(party));

        var survivors = new List<Candidate>();
        var unchanged = 0;
        var broken = 0;

        // Una lectura por ventana de 1 KB, una sola vez: nada de sondeo continuo.
        foreach (var window in candidates.GroupBy(candidate => Math.Min(candidate.Address, candidate.MaxAddress) & ~0x3FFu))
        {
            if (!client.TryReadMemory(window.Key, 0x400 + 16, out var data))
            {
                broken += window.Count();
                continue;
            }

            foreach (var candidate in window)
            {
                var value = Read(data, window.Key, candidate.Address);
                var max = Read(data, window.Key, candidate.MaxAddress);

                if (max != candidate.Max || value > candidate.Max)
                {
                    broken++;
                }
                else if (value == candidate.Value)
                {
                    unchanged++;
                }
                else
                {
                    Console.WriteLine($"  0x{candidate.Address:X8} hueco {candidate.Slot} {candidate.Shape,-12} "
                                      + $"{candidate.Value} -> {value} (máx {max})");
                    survivors.Add(candidate with { Value = value });
                }
            }

            Thread.Sleep(1);
        }

        Console.WriteLine();
        Console.WriteLine($"{survivors.Count} han cambiado, {unchanged} siguen igual, {broken} ya no parecen PS.");

        File.WriteAllLines(CandidatesFile, survivors.Select(candidate => candidate.ToLine()));

        foreach (var candidate in survivors.Take(6))
        {
            Dump(client, candidate.Address);
        }

        Console.WriteLine(survivors.Count switch
        {
            0 => "No queda ninguno: el combate no guarda los PS junto a su máximo. Hay que cambiar de forma de buscar.",
            > MaxWatched => "Aún son muchos: otro golpe y  Probe --combate filtrar  otra vez.",
            _ => "Pocos ya:  Probe --combate vigilar 180  y juega el resto del combate."
        });

        return 0;
    }

    /// <summary>
    /// Every battle Pokémon block, found by its shape instead of by one Pokémon's HP.
    /// </summary>
    /// <remarks>
    /// Measured on the first hit (Ferrocuello, 176 → 174): the battle copy is an 800-byte heap block
    /// — header <c>44 55 · · 20 03 00 00</c>, the «DU» of a used block and its size — whose data starts
    /// with <c>E7 FF FF FF 20 00 00 00</c>, and holds a pointer at +0x20, the species at +0x2C, the
    /// maximum HP at +0x2E and the current HP at +0x30. Two identical copies were there. Searching for
    /// that header finds the blocks of every Pokémon in the battle, the rival's included, and their
    /// HP addresses replace the candidates so <c>vigilar</c> records all of them.
    /// </remarks>
    private static int Structures(AzaharRpcClient client)
    {
        byte[] pattern = [0x44, 0x55, 0, 0, 0x20, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xE7, 0xFF, 0xFF, 0xFF, 0x20, 0, 0, 0];
        byte[] mask = [0xFF, 0xFF, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        var searches = 0;
        var hits = SearchAll(client, pattern, mask, ref searches);
        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var found = new List<Candidate>();

        Console.WriteLine($"{hits.Count} bloques con esa cabecera:");

        foreach (var hit in hits)
        {
            var data = hit + 16;

            if (!client.TryReadMemory(data, 0x40, out var bytes))
            {
                continue;
            }

            var pointer = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x20));
            var species = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x2C));
            var max = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x2E));
            var hp = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x30));
            var name = species > 0 && species < names.Length ? names[species] : $"#{species}";

            Console.WriteLine($"  0x{data:X8}  puntero 0x{pointer:X8}  {name,-14} PS {hp}/{max}   "
                              + string.Join(' ', bytes.Skip(0x20).Take(0x20).Select(b => b.ToString("X2"))));

            if (species > 0 && max > 0 && hp <= max)
            {
                found.Add(new Candidate(data + 0x30, data + 0x2E, -1, hp, max, name));
            }
        }

        File.WriteAllLines(CandidatesFile, found.Select(candidate => candidate.ToLine()));
        Console.WriteLine($"{found.Count} con especie y PS coherentes, guardados para  vigilar.");
        return 0;
    }

    /// <summary>
    /// Runs the application's own battle detector against the game, only writing down what it sees.
    /// </summary>
    /// <remarks>
    /// The same <see cref="PermaLocke.GameLink.Battle.BattleTableReader"/> and
    /// <see cref="PermaLocke.GameLink.Battle.BattleFaintTracker"/> the monitor uses, at the same pace,
    /// with nothing recorded and nothing shown. Beating a wild Pokémon proves the whole path — the
    /// battle is found without help, the opponent's fall is seen the moment it happens, and the
    /// tables are let go when the battle ends — without anybody in the run having to die.
    /// </remarks>
    private static int Detector(AzaharRpcClient client, int seconds)
    {
        var logPath = Path.Combine(Folder, $"detector-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };

        void Say(string line)
        {
            var stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";
            Console.WriteLine(stamped);
            log.WriteLine(stamped);
        }

        var reader = new PermaLocke.GameLink.Battle.BattleTableReader(client,
            new ConsoleLogger<PermaLocke.GameLink.Battle.BattleTableReader>(Say));
        var tracker = new PermaLocke.GameLink.Battle.BattleFaintTracker();
        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var deadline = DateTime.Now.AddSeconds(seconds);
        var lastLine = string.Empty;
        var reads = 0;
        var clock = Stopwatch.StartNew();

        Say($"Detector de combate {seconds} s. Solo anota. Registro: {logPath}");

        while (DateTime.Now < deadline)
        {
            var tables = reader.Read(DateTimeOffset.Now);
            reads++;

            foreach (var faint in tracker.Observe(tables))
            {
                Say($"CAÍDO {(faint.IsPlayers ? "DEL JUGADOR" : "rival")}: {names[faint.Species]} (posición {faint.BattleId})");
            }

            var line = tracker.InBattle && tables.Count > 0
                ? string.Join("  |  ", tables.Select(table => string.Join(" ", table.Blocks.Select(block =>
                    $"{names[block.Species]} {block.CurrentHp}/{block.MaxHp}"))))
                : "fuera de combate";

            if (line != lastLine)
            {
                Say(line);
                lastLine = line;
            }

            Thread.Sleep(tracker.InBattle ? 250 : 1000);
        }

        Say($"Fin: {reads} vueltas en {clock.Elapsed.TotalSeconds:F0} s.");
        return 0;
    }

    private sealed class ConsoleLogger<T>(Action<string> say) : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            say("[lector] " + formatter(state, exception));
    }

    private static int Watch(AzaharRpcClient client, int seconds)
    {
        if (!File.Exists(CandidatesFile))
        {
            Console.WriteLine("No hay candidatos: primero  Probe --combate buscar");
            return 1;
        }

        var candidates = File.ReadAllLines(CandidatesFile).Select(Candidate.FromLine).ToList();

        if (candidates.Count > MaxWatched)
        {
            Console.WriteLine($"{candidates.Count} candidatos son demasiados para vigilar sin cargar al emulador. Filtra más.");
            return 1;
        }

        var logPath = Path.Combine(Folder, $"combate-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };

        void Say(string line)
        {
            var stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";
            Console.WriteLine(stamped);
            log.WriteLine(stamped);
        }

        Say($"Vigilando {candidates.Count} direcciones {seconds} s, cinco veces por segundo. Registro: {logPath}");

        var last = candidates.ToDictionary(candidate => candidate.Address, candidate => candidate.Value);
        var deadline = DateTime.Now.AddSeconds(seconds);

        while (DateTime.Now < deadline)
        {
            foreach (var candidate in candidates)
            {
                if (!client.TryReadMemory(candidate.Address, 2, out var bytes))
                {
                    continue;
                }

                var value = BinaryPrimitives.ReadUInt16LittleEndian(bytes);

                if (value != last[candidate.Address])
                {
                    Say($"0x{candidate.Address:X8} {candidate.Shape,-14} {last[candidate.Address],5} -> {value,5}");
                    last[candidate.Address] = value;
                }
            }

            Thread.Sleep(200);
        }

        Say("Fin.");
        return 0;
    }

    /// <summary>
    /// The party from the structure the game reads, starting from the addresses the application
    /// already found, so the probe does not sweep memory by itself.
    /// </summary>
    private static IReadOnlyList<LivePartyMember> ReadParty(AzaharRpcClient client)
    {
        var known = Path.Combine(new PermaLocke.Infrastructure.AppPaths().SaveBackups, "equipo.txt");
        var copy = Path.Combine(Path.GetTempPath(), "permalocke-probe-equipo.txt");

        // Una copia: el fichero es de la aplicación, que puede estar abierta, y el proveedor lo reescribe.
        if (File.Exists(known))
        {
            File.Copy(known, copy, overwrite: true);
        }

        var provider = new AzaharGameStateProvider(client, new PkhexSpeciesLookup("es"), new PkhexLocationLookup("es"),
            copy, NullLogger<AzaharGameStateProvider>.Instance);

        var snapshot = provider.ReadAsync().GetAwaiter().GetResult();

        return snapshot.Connected ? snapshot.Party : [];
    }

    private static string Describe(IReadOnlyList<LivePartyMember> party) =>
        string.Join("  ", party.Select(member => $"[{member.Slot}] {member.SpeciesName} {member.CurrentHp}/{member.MaxHp}"));

    private static (byte[] Pattern, byte[] Mask) Pattern(int hp, int max, int gap, bool hpFirst)
    {
        var pattern = new byte[4 + gap];
        var mask = new byte[4 + gap];
        var (first, second) = hpFirst ? (hp, max) : (max, hp);

        BinaryPrimitives.WriteUInt16LittleEndian(pattern, (ushort)first);
        BinaryPrimitives.WriteUInt16LittleEndian(pattern.AsSpan(2 + gap), (ushort)second);

        mask[0] = mask[1] = 0xFF;
        mask[2 + gap] = mask[3 + gap] = 0xFF;

        return (pattern, mask);
    }

    /// <summary>All hits in the mapped regions, paging past the fork's 255 per call.</summary>
    private static List<uint> SearchAll(AzaharRpcClient client, byte[] pattern, byte[] mask, ref int searches)
    {
        const int Cap = 255;
        const int MaxPages = 20;

        var all = new List<uint>();

        foreach (var region in Mapped)
        {
            var start = region.Start;

            for (var page = 0; page < MaxPages && start < region.End; page++)
            {
                var hits = client.SearchMemory(start, region.End - start, pattern, mask, stride: 2);
                searches++;
                all.AddRange(hits);

                if (hits.Count < Cap)
                {
                    break;
                }

                start = hits[^1] + 2;
            }
        }

        return all;
    }

    private static ushort Read(byte[] data, uint windowStart, uint address) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)(address - windowStart)));

    private static void Dump(AzaharRpcClient client, uint address)
    {
        var start = (address & ~0xFu) - 0x60;

        if (!client.TryReadMemory(start, 0xD0, out var bytes))
        {
            return;
        }

        Console.WriteLine($"  alrededor de 0x{address:X8}:");

        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            Console.WriteLine($"    {start + offset:X8}  {string.Join(' ', bytes.Skip(offset).Take(16).Select(b => b.ToString("X2")))}");
        }
    }

    private sealed record Candidate(uint Address, uint MaxAddress, int Slot, ushort Value, ushort Max, string Shape)
    {
        public string ToLine() => $"{Address:X8};{MaxAddress:X8};{Slot};{Value};{Max};{Shape}";

        public static Candidate FromLine(string line)
        {
            var parts = line.Split(';');

            return new Candidate(Convert.ToUInt32(parts[0], 16), Convert.ToUInt32(parts[1], 16), int.Parse(parts[2]),
                ushort.Parse(parts[3]), ushort.Parse(parts[4]), parts[5]);
        }
    }
}
