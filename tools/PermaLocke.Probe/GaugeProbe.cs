using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Looks for the HP bar's own animated value — the number the battle box counts down — in one battle
/// with two hits, without the player having to do anything between them but attack.
/// </summary>
/// <remarks>
/// <para>
/// Measured with screen and memory on one clock: the calculation table changes when the move is
/// chosen, the second table when «X used Y» appears — <b>before</b> the attack animation — and the bar
/// drains after that animation, whose length depends on the move. Neither table is the bar, and no
/// fixed delay can stand in for it. The bar must hold its own value, and pairs of whole numbers beside
/// the maximum did not find it, so this tries the shapes left: <b>floats</b>, and whole numbers further
/// from the maximum.
/// </para>
/// <list type="number">
/// <item>At the move menu: one search per shape, excluding the battle tables themselves.</item>
/// <item>First hit, seen in the calculation table: eight seconds later — the bar long finished — each
/// candidate is read once, and what now holds the new HP stays.</item>
/// <item>Second hit: the survivors are read as fast as the link allows, and the one that passes through
/// values in between is the bar.</item>
/// </list>
/// <para>
/// Only reads. Searches only in the linear heap, which exists end to end, and only while the battle
/// waits at its menu.
/// </para>
/// </remarks>
public static class GaugeProbe
{
    private const uint Start = 0x30000000, End = 0x34000000;

    public static int Run(AzaharRpcClient client, int seconds)
    {
        var tables = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance).Read(DateTimeOffset.Now);

        if (tables.Count < 2)
        {
            Console.WriteLine("No hay combate: entra en uno y quédate en el menú de ataques.");
            return 1;
        }

        var players = tables[0].Blocks.Where(block => block.IsPlayers && block.CurrentHp > 0).ToList();
        var clock = Stopwatch.StartNew();
        var candidates = new List<Candidate>();
        var searches = 0;

        foreach (var member in players)
        {
            foreach (var shape in Shapes(member.CurrentHp, member.MaxHp))
            {
                foreach (var hit in SearchAll(client, shape.Pattern, shape.Mask, ref searches))
                {
                    var at = hit + shape.ValueOffset;

                    if (!InsideTables(tables, at))
                    {
                        candidates.Add(new Candidate(at, member.BattleId, shape.Name, shape.IsFloat));
                    }
                }
            }
        }

        candidates = [.. candidates.DistinctBy(candidate => (candidate.Address, candidate.IsFloat))];

        Console.WriteLine($"{searches} búsquedas en {clock.Elapsed.TotalSeconds:F1} s: {candidates.Count} candidatos "
                          + $"({string.Join(", ", candidates.GroupBy(c => c.Shape).Select(g => $"{g.Key} {g.Count()}"))}).");

        // 1. El primer golpe, visto en la tabla del cálculo.
        Console.WriteLine("Ataca: esperando el primer golpe...");

        if (WaitForHit(client, tables, players, seconds) is not { } first)
        {
            Console.WriteLine("No ha llegado ningún golpe.");
            return 1;
        }

        Console.WriteLine($"Golpe a la posición {first.Id}: {first.Before} -> {first.After}. Espero 8 s a que acabe la barra...");
        Thread.Sleep(8000);

        var survivors = candidates
            .Where(candidate => candidate.Id == first.Id)
            .Where(candidate => ReadValue(client, candidate) is { } value && Math.Abs(value - first.After) < 0.01)
            .ToList();

        Console.WriteLine($"{survivors.Count} candidatos de esa posición tienen ahora {first.After}:");

        foreach (var survivor in survivors.Take(30))
        {
            Console.WriteLine($"  0x{survivor.Address:X8} {survivor.Shape}");
        }

        if (survivors.Count == 0 || survivors.Count > 30)
        {
            Console.WriteLine(survivors.Count == 0
                ? "Ninguno: la barra no guarda los PS de ninguna de estas formas."
                : "Demasiados para vigilarlos rápido.");
            return 0;
        }

        // 2. El segundo golpe, leído lo más rápido posible.
        Console.WriteLine("Vuelve a atacar: esperando el segundo golpe...");

        if (WaitForHit(client, tables, players, seconds) is not { } second)
        {
            Console.WriteLine("No ha llegado el segundo golpe.");
            return 1;
        }

        var hitClock = Stopwatch.StartNew();
        var last = survivors.ToDictionary(s => s.Address, s => ReadValue(client, s));
        var lastA = ReadHp(client, tables[0], second.Id);
        var lastB = ReadHp(client, tables[1], second.Id);

        Console.WriteLine($"   0 ms  golpe en la tabla del cálculo: {second.Before} -> {second.After}");

        while (hitClock.Elapsed.TotalSeconds < 10)
        {
            var ms = hitClock.Elapsed.TotalMilliseconds;

            foreach (var survivor in survivors)
            {
                var value = ReadValue(client, survivor);

                if (value != last[survivor.Address])
                {
                    Console.WriteLine($"{ms,6:F0} ms  0x{survivor.Address:X8} {survivor.Shape,-14} {last[survivor.Address]} -> {value}");
                    last[survivor.Address] = value;
                }
            }

            var a = ReadHp(client, tables[0], second.Id);
            var b = ReadHp(client, tables[1], second.Id);

            if (a != lastA || b != lastB)
            {
                Console.WriteLine($"{ms,6:F0} ms  tablas: 0x{tables[0].Origin:X8}={a}  0x{tables[1].Origin:X8}={b}");
                lastA = a;
                lastB = b;
            }
        }

        return 0;
    }

    private sealed record Candidate(uint Address, int Id, string Shape, bool IsFloat);

    private sealed record Shape(string Name, byte[] Pattern, byte[] Mask, uint ValueOffset, bool IsFloat);

    private sealed record Hit(int Id, int Before, int After);

    /// <summary>
    /// Floats beside the maximum (gaps of 0 to 12, both orders), the float alone, and whole numbers
    /// 8 to 32 bytes from the maximum — the shapes the first search did not try.
    /// </summary>
    private static IEnumerable<Shape> Shapes(int hp, int max)
    {
        var hpFloat = BitConverter.GetBytes((float)hp);
        var maxFloat = BitConverter.GetBytes((float)max);

        for (var gap = 0; gap <= 12; gap += 4)
        {
            yield return Pair($"f PS,+{gap},MAX", hpFloat, maxFloat, gap, valueFirst: true, isFloat: true);
            yield return Pair($"f MAX,+{gap},PS", maxFloat, hpFloat, gap, valueFirst: false, isFloat: true);
        }

        var hpWord = BitConverter.GetBytes((ushort)hp);
        var maxWord = BitConverter.GetBytes((ushort)max);

        for (var gap = 8; gap <= 32; gap += 2)
        {
            yield return Pair($"PS,+{gap},MAX", hpWord, maxWord, gap, valueFirst: true, isFloat: false);
            yield return Pair($"MAX,+{gap},PS", maxWord, hpWord, gap, valueFirst: false, isFloat: false);
        }
    }

    private static Shape Pair(string name, byte[] first, byte[] second, int gap, bool valueFirst, bool isFloat)
    {
        var pattern = new byte[first.Length + gap + second.Length];
        var mask = new byte[pattern.Length];

        first.CopyTo(pattern, 0);
        second.CopyTo(pattern, first.Length + gap);

        for (var i = 0; i < first.Length; i++)
        {
            mask[i] = 0xFF;
        }

        for (var i = 0; i < second.Length; i++)
        {
            mask[first.Length + gap + i] = 0xFF;
        }

        return new Shape(name, pattern, mask, valueFirst ? 0u : (uint)(first.Length + gap), isFloat);
    }

    private static List<uint> SearchAll(AzaharRpcClient client, byte[] pattern, byte[] mask, ref int searches)
    {
        var all = new List<uint>();
        var from = Start;

        for (var page = 0; page < 10 && from < End; page++)
        {
            var hits = client.SearchMemory(from, End - from, pattern, mask, stride: 2);
            searches++;
            all.AddRange(hits);

            if (hits.Count < 255)
            {
                break;
            }

            from = hits[^1] + 2;
        }

        return all;
    }

    private static bool InsideTables(IReadOnlyList<BattleTable> tables, uint address) =>
        tables.Any(table => address >= table.Origin - BattleLayout.HeaderSize
                            && address < table.Origin + (uint)(24 * BattleLayout.Stride));

    private static Hit? WaitForHit(AzaharRpcClient client, IReadOnlyList<BattleTable> tables,
        IReadOnlyList<BattleBlock> players, int seconds)
    {
        var before = players.ToDictionary(p => p.BattleId, p => ReadHp(client, tables[1], p.BattleId));
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            foreach (var player in players)
            {
                var now = ReadHp(client, tables[1], player.BattleId);

                if (now >= 0 && now != before[player.BattleId])
                {
                    return new Hit(player.BattleId, before[player.BattleId], now);
                }
            }

            Thread.Sleep(20);
        }

        return null;
    }

    private static int ReadHp(AzaharRpcClient client, BattleTable table, int id) =>
        client.TryReadMemory(table.Origin + (uint)(id * BattleLayout.Stride) + 0x30, 2, out var bytes)
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : -1;

    private static double? ReadValue(AzaharRpcClient client, Candidate candidate)
    {
        if (!client.TryReadMemory(candidate.Address, candidate.IsFloat ? 4 : 2, out var bytes))
        {
            return null;
        }

        return candidate.IsFloat ? BitConverter.ToSingle(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }
}
