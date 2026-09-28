using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Read-only look at two things of the save the players' list asked about (2026-09-28): the Roto Loto flags and the
/// berry pile block. With a file argument it writes the berry block there, so two dumps (before and after picking a
/// pile) can be compared byte by byte.
/// </summary>
public static class FieldStateProbe
{
    public static int Run(string? dump, string? folder = null)
    {
        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(), folder ?? AppContext.BaseDirectory);

        if (save.Find() is not { } path || !SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine("No se encuentra o no se lee la partida de Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida: {path}");
        Console.WriteLine($"Guardada por última vez: {File.GetLastWriteTime(path):dd/MM HH:mm:ss}  Tiempo jugado: {game.PlayedHours}:{game.PlayedMinutes:00}:{game.PlayedSeconds:00}");
        Console.WriteLine($"Rotombola 1: {game.FieldMenu.RotomLoto1}  Rotombola 2: {game.FieldMenu.RotomLoto2}  Afecto Rotom: {game.FieldMenu.RotomAffection}");

        foreach (var pouch in game.Inventory.Pouches)
        {
            Console.WriteLine($"bolsillo {pouch.Type}: acepta 1579 = {PermaLocke.GameLink.Data.BagLayout.UltraSunMoon.PocketFor(1579)?.Type}, lleva {string.Join(",", pouch.Items.Where(i => i.Index is >= 1579 and <= 1589 && i.Count > 0).Select(i => $"{i.Index}x{i.Count}"))}");
        }

        var blocks = game.AllBlocks;
        for (var i = 0; i < blocks.Count; i++)
        {
            Console.WriteLine($"bloque {i,2}: 0x{blocks[i].Offset:X6} +0x{blocks[i].Length:X5}");
        }

        if (dump is not null)
        {
            File.WriteAllLines(dump, blocks.Select((b, i) => $"{i} {Convert.ToHexString(game.Data.Slice(b.Offset, b.Length))}"));
            Console.WriteLine($"Bloques escritos en {dump}");
        }

        return 0;
    }

    /// <summary>Byte offsets that differ between two dumps, per block.</summary>
    public static int Diff(string a, string b)
    {
        var left = File.ReadAllLines(a);
        var right = File.ReadAllLines(b);
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var x = Convert.FromHexString(left[i].Split(' ')[1]);
            var y = Convert.FromHexString(right[i].Split(' ')[1]);
            var diffs = Enumerable.Range(0, Math.Min(x.Length, y.Length)).Where(o => x[o] != y[o]).ToList();
            if (diffs.Count is > 0 and < 200)
            {
                Console.WriteLine($"bloque {i}: {string.Join(" ", diffs.Select(o => $"{o:X}:{x[o]:X2}>{y[o]:X2}"))}");
            }
            else if (diffs.Count > 0)
            {
                Console.WriteLine($"bloque {i}: {diffs.Count} bytes distintos");
            }
        }

        return 0;
    }
}
