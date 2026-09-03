using pk3DS.Core.CTR;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The generated world against the one it came from: same counts, different TMs.
/// </summary>
/// <remarks>
/// The whole promise of shuffling instead of drawing is that nobody ends up able to learn almost
/// nothing. That promise is only worth anything if it is checked on the file that ships, so this
/// compares the mod PermaLocke wrote against the base layer it read, species by species.
/// </remarks>
public sealed class MachineCompatibilityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? ".";
    }

    private static byte[]? Packed(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var garc = new GARC.LazyGARC(File.ReadAllBytes(path));
        return garc[garc.FileCount - 1];
    }

    [Fact]
    public void Everyone_learns_as_many_as_before_and_hardly_anyone_the_same_ones()
    {
        var root = Root();
        var generated = Directory.Exists(Path.Combine(root, "Randomized"))
            ? Directory.EnumerateDirectories(Path.Combine(root, "Randomized"), "seed-*")
                .Select(d => Path.Combine(d, "romfs", "a", "0", "1", "7"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (generated is null
            || Packed(Path.Combine(root, "Expansion", "romfs", "a", "0", "1", "7")) is not { } before
            || Packed(generated) is not { } after)
        {
            return;
        }

        var rows = Math.Min(before.Length, after.Length) / PersonalEntry7.Size;
        var moved = 0;
        var couldMove = 0;

        for (var species = 1; species < rows; species++)
        {
            var was = MachineFlags.Read(before, species);
            var now = MachineFlags.Read(after, species);

            // Lo que no puede cambiar: CUANTAS. Un Pokemon que aprendia nueve aprende nueve.
            Assert.Equal(was.Count(on => on), now.Count(on => on));

            // Solo cuentan las que PUEDEN cambiar. Con 0 o con las 100 encendidas, cualquier
            // permutacion da el mismo resultado, y de las 1330 filas muchas son formas sin ninguna.
            var count = was.Count(on => on);

            if (count is > 0 and < MachineFlags.Count)
            {
                couldMove++;

                if (!was.SequenceEqual(now))
                {
                    moved++;
                }
            }
        }

        Assert.True(couldMove > 500, $"solo {couldMove} especies podian cambiar: algo va mal en la lectura");
        Assert.True(moved > couldMove * 95 / 100,
            $"solo {moved} de {couldMove} especies que podian cambiar lo hicieron");
    }
}
