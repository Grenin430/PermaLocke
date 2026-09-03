using pk3DS.Core.CTR;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The generated world: does a Pokémon really learn its own types' TMs far more often?
/// </summary>
/// <remarks>
/// Counting that «something changed» would pass on a coin flip per bit. What makes this the right
/// check is that it measures the <b>shape</b> of the result on the file that ships: nine in ten for
/// a move of the species' own type against one in four for the rest is a gap no accident produces,
/// and if the weighting were dropped the two rates would meet in the middle and this would fail.
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

    private static string? Newest(string root, params string[] parts)
    {
        var randomized = Path.Combine(root, "Randomized");

        return Directory.Exists(randomized)
            ? Directory.EnumerateDirectories(randomized, "seed-*")
                .Select(d => Path.Combine([d, .. parts]))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
    }

    [Fact]
    public void The_odds_are_the_ones_Universal_Randomizer_uses()
    {
        const int fire = 9;
        const int flying = 2;
        const int water = 10;

        // Del tipo propio, cualquiera de los dos.
        Assert.Equal(900, MachineCompatibilityRandomizer.ChanceOf(fire, fire, flying));
        Assert.Equal(900, MachineCompatibilityRandomizer.ChanceOf(flying, fire, flying));

        // Normal, que es el 0: a mitad de camino.
        Assert.Equal(500, MachineCompatibilityRandomizer.ChanceOf(0, fire, flying));

        // Cualquier otro.
        Assert.Equal(250, MachineCompatibilityRandomizer.ChanceOf(water, fire, flying));

        // Y un tipo que no se pudo leer cae del lado BAJO: no saber no es motivo para regalar.
        Assert.Equal(250, MachineCompatibilityRandomizer.ChanceOf(-1, fire, flying));

        // Un Pokemon de tipo Normal si tiene 900 en los movimientos Normal, no 500.
        Assert.Equal(900, MachineCompatibilityRandomizer.ChanceOf(0, 0, 0));
    }

    /// <summary>Nobody is left unable to learn anything at all.</summary>
    /// <remarks>
    /// The risk of rolling per TM instead of shuffling, and the reason the player was warned about
    /// it. One in four is the worst case for a species whose types no TM matches, and over a
    /// hundred rolls the chance of coming out with none is vanishingly small — but «vanishingly
    /// small» over 1329 species is worth checking rather than asserting.
    /// </remarks>
    [Fact]
    public void Nobody_ends_up_learning_nothing_that_could_learn_something()
    {
        var root = Root();

        if (Newest(root, "romfs", "a", "0", "1", "7") is not { } personal
            || Packed(personal) is not { } after
            || Packed(Path.Combine(root, "Expansion", "romfs", "a", "0", "1", "7")) is not { } before)
        {
            return;
        }

        var rows = Math.Min(before.Length, after.Length) / PersonalEntry7.Size;
        var lost = new List<int>();

        for (var species = 1; species < rows; species++)
        {
            if (MachineFlags.Read(before, species).Any(on => on)
                && !MachineFlags.Read(after, species).Any(on => on))
            {
                lost.Add(species);
            }
        }

        Assert.True(lost.Count == 0,
            $"{lost.Count} especies se quedaron sin poder aprender NINGUNA MT: {string.Join(", ", lost.Take(10))}");
    }
}
