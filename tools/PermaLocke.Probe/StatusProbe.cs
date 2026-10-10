using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Looks for a reliable status byte in the battle blocks (2026-10-11): <c>--combate estado</c>. The party's own copy keeps the status of the last battle
/// until the battle ends, so the panel shows no status in a battle (§99); if the block the battle holds for each Pokémon carries it, the panel could.
/// Nothing is assumed about where: whole blocks are taken (0x320 bytes each, both tables) in the situations below, and the bytes that changed for the
/// Pokémon that got a status and did not change for the ones that did not are listed, with the value they took. The status byte of PK7 is the code the
/// party uses: bits 0-2 the turns of sleep, 0x08 poison, 0x10 burn, 0x20 freeze, 0x40 paralysis, 0x80 bad poison. Only reads. The procedure:
///   1. In a battle, with nobody with a status:        Probe --combate estado capturar sano
///   2. After one of yours gets one (poison, burn...): Probe --combate estado capturar con-veneno
///   3. Optionally, a different status on another one:  Probe --combate estado capturar con-quemadura
///   4. At the end of the battle, in the world:         Probe --combate estado capturar despues  (the party copy has been updated by then)
///   5.                                                  Probe --combate estado analizar sano con-veneno [despues]
/// It says which offsets are candidates and which of them agree with the status the party shows after the battle. A candidate that agrees for
/// every status tried, in both tables, is a field; one that does not is noise. Until it is verified, the panel keeps showing the status only out of battle.
/// </summary>
public static class StatusProbe
{
    private static string Folder => Path.Combine(Path.GetTempPath(), "permalocke-estado");

    private sealed record Capture(string Name, List<(int Table, int Id, int Species, uint Pid, byte[] Bytes)> Blocks, List<(int Slot, int Species, uint Pid, int Status, int Hp)> Party);

    public static int Run(AzaharRpcClient client, string[] args)
    {
        Directory.CreateDirectory(Folder);
        if (args.Length >= 2 && args[0] == "capturar") return Take(client, args[1]);
        if (args.Length >= 3 && args[0] == "analizar") return Analyse(args[1], args[2], args.Length >= 4 ? args[3] : null);

        Console.WriteLine("Uso: Probe --combate estado capturar <nombre>   (en combate, o al acabar para «despues»)");
        Console.WriteLine("     Probe --combate estado analizar <sano> <con-estado> [<despues>]");
        return 1;
    }

    private static int Take(AzaharRpcClient client, string name)
    {
        var party = BattleHpProbe.ReadParty(client);
        var reader = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance);
        reader.Read(DateTimeOffset.Now);
        var tables = BattleFaintTracker.InProgress(reader.Read(DateTimeOffset.Now));
        var capture = new Capture(name, [], [.. party.Select(m => (m.Slot, m.Species, m.Pid, m.Status, m.CurrentHp))]);

        for (var t = 0; t < tables.Count; t++)
        {
            foreach (var block in tables[t].Blocks.Where(b => b.IsPlayers))
            {
                // Whose it is: the Pokémon behind the block (a PK7 with a valid checksum), by its PID, as everywhere else in the app.
                var pid = reader.ReadPokemon(block)?.PID ?? 0;
                if (client.TryReadMemory(block.Address, BattleLayout.BlockSize, out var bytes)) capture.Blocks.Add((t, block.BattleId, block.Species, pid, bytes));
            }
        }

        var text = new StringBuilder($"nombre {name}\n");
        foreach (var m in capture.Party) text.Append(string.Create(CultureInfo.InvariantCulture, $"equipo {m.Slot} {m.Species} {m.Pid:X8} {m.Status} {m.Hp}\n"));
        foreach (var b in capture.Blocks) text.Append(string.Create(CultureInfo.InvariantCulture, $"bloque {b.Table} {b.Id} {b.Species} {b.Pid:X8} {Convert.ToHexString(b.Bytes)}\n"));
        File.WriteAllText(Path.Combine(Folder, name + ".txt"), text.ToString());

        Console.WriteLine($"{capture.Blocks.Count} bloques de combate ({tables.Count} tablas) y {capture.Party.Count} del equipo guardados en {Path.Combine(Folder, name + ".txt")}");
        foreach (var m in capture.Party) Console.WriteLine($"  equipo {m.Slot}: especie {m.Species}, PS {m.Hp}, estado del equipo 0x{m.Status:X2}");
        if (capture.Blocks.Count == 0) Console.WriteLine("  (no hay combate: así vale para «despues», con el equipo ya actualizado)");
        return 0;
    }

    private static Capture Load(string name)
    {
        var path = Path.Combine(Folder, name + ".txt");
        if (!File.Exists(path)) throw new FileNotFoundException("No existe " + path);
        var capture = new Capture(name, [], []);
        foreach (var line in File.ReadAllLines(path))
        {
            var f = line.Split(' ');
            if (f[0] == "equipo") capture.Party.Add((int.Parse(f[1]), int.Parse(f[2]), Convert.ToUInt32(f[3], 16), int.Parse(f[4]), int.Parse(f[5])));
            else if (f[0] == "bloque") capture.Blocks.Add((int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]), Convert.ToUInt32(f[4], 16), Convert.FromHexString(f[5])));
        }

        return capture;
    }

    private static readonly int[] StatusValues = [1, 2, 3, 4, 5, 6, 7, 0x08, 0x10, 0x20, 0x40, 0x80];

    private static int Analyse(string healthyName, string ailingName, string? afterName)
    {
        var healthy = Load(healthyName);
        var ailing = Load(ailingName);
        var after = afterName is null ? null : Load(afterName);

        // Whose status changed: the PIDs that have a status in the last capture of the party (after the battle if there is one) and none in the healthy one.
        var reference = after ?? ailing;
        var affected = reference.Party.Where(m => m.Status != 0 && healthy.Party.FirstOrDefault(h => h.Pid == m.Pid).Status == 0).Select(m => (m.Pid, m.Status)).ToList();
        if (affected.Count == 0)
        {
            Console.WriteLine("En " + reference.Name + " ningún Pokémon del equipo tiene estado que no tuviera en " + healthy.Name + ". Sin «despues», el equipo no se actualiza hasta el final del combate:");
            Console.WriteLine("captura «despues» al acabar, o dime a mano de quién es el estado (no se inventa).");
            return 1;
        }

        Console.WriteLine("Con estado al acabar: " + string.Join(", ", affected.Select(a => $"PID {a.Pid:X8} = 0x{a.Status:X2}")));

        var candidates = new Dictionary<(int Table, int Offset), List<string>>();
        foreach (var (pid, status) in affected)
        {
            foreach (var table in new[] { 0, 1 })
            {
                var a = healthy.Blocks.FirstOrDefault(b => b.Table == table && b.Pid == pid);
                var b = ailing.Blocks.FirstOrDefault(x => x.Table == table && x.Pid == pid);
                if (a.Bytes is null || b.Bytes is null) continue;

                for (var o = 0; o < BattleLayout.BlockSize; o++)
                {
                    if (a.Bytes[o] == b.Bytes[o]) continue;

                    // A control: the same offset in the Pokémon that did NOT get a status must not have moved the same way.
                    var control = healthy.Blocks.Where(h => h.Table == table && h.Pid != pid)
                        .Select(h => (h, ailing.Blocks.FirstOrDefault(x => x.Table == table && x.Pid == h.Pid)))
                        .Where(pair => pair.Item2.Bytes is not null)
                        .Count(pair => pair.h.Bytes[o] != pair.Item2.Bytes[o]);
                    var plausible = StatusValues.Contains(b.Bytes[o]) || (b.Bytes[o] & status) != 0;
                    var note = $"PID {pid:X8}: 0x{a.Bytes[o]:X2} → 0x{b.Bytes[o]:X2}, cambió en {control} de los otros{(plausible ? ", valor de estado" : string.Empty)}{(b.Bytes[o] == status ? ", IGUAL al estado del equipo" : string.Empty)}";
                    if (!candidates.TryGetValue((table, o), out var list)) candidates[(table, o)] = list = [];
                    list.Add(note);
                }
            }
        }

        var strong = candidates.Where(c => c.Value.Any(n => n.Contains("IGUAL al estado")) && c.Value.All(n => n.Contains("cambió en 0 de los otros"))).ToList();
        Console.WriteLine($"{candidates.Count} offsets cambiaron para el Pokémon con estado; {strong.Count} cambiaron a EXACTAMENTE su estado sin que cambiara ninguno de los demás:");
        foreach (var ((table, offset), notes) in strong.OrderBy(c => c.Key.Table).ThenBy(c => c.Key.Offset))
        {
            Console.WriteLine($"  tabla {table} +0x{offset:X3}: {string.Join(" | ", notes)}");
        }

        if (strong.Count == 0)
        {
            Console.WriteLine("Ninguno cumple. Con otro estado (quemadura, parálisis) y otro Pokémon, repite: un byte que sale una vez puede ser casualidad.");
            Console.WriteLine("Candidatos con valor de estado (para mirar a mano, NO fiables):");
            foreach (var ((table, offset), notes) in candidates.Where(c => c.Value.Any(n => n.Contains("valor de estado"))).OrderBy(c => c.Key.Table).ThenBy(c => c.Key.Offset).Take(20))
            {
                Console.WriteLine($"  tabla {table} +0x{offset:X3}: {string.Join(" | ", notes)}");
            }
        }
        else
        {
            Console.WriteLine("Para darlo por bueno hace falta el MISMO offset con dos estados distintos (veneno y quemadura, por ejemplo) y en otro combate.");
            Console.WriteLine("Cuando lo tengas: dime tabla y offset; se añade a BattleHotReader con su prueba y el panel enseña el estado en combate.");
        }

        return 0;
    }
}
