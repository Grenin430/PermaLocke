using System.Text.Json;
using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// No legendary in the grass, and the rest of the game as it was.
/// </summary>
/// <remarks>
/// <c>bannedSpecies</c> was written before the gen 8-9 expansion and stops at 807. The audit of the
/// installed world on 2026-09-21 found Meltan in 105 grass slots, Kubfu in 60, Terapagos in 41 and
/// Calyrex in 38. The player asked for every legendary out of the wild and nothing else changed, so
/// the new ones went into a list that narrows only the wild pool.
/// </remarks>
public sealed class WildBannedSpeciesTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Every species <c>Data/species.json</c> flags as legendary is kept out of the wild by one list or
    /// the other. The flag comes from PKHeX, so a mod that adds a legendary shows up here once the
    /// species file is regenerated, instead of turning up in a patch of grass.
    /// </summary>
    [Fact]
    public void Every_legendary_is_kept_out_of_the_wild()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));
        var outOfTheWild = options.BannedSpecies.Concat(options.WildBannedSpecies).ToHashSet();

        using var stream = File.OpenRead(Path.Combine(Root(), "Data", "species.json"));
        using var document = JsonDocument.Parse(stream);

        var legendaries = document.RootElement.GetProperty("species").EnumerateArray()
            .Where(species => species.GetProperty("legendary").GetBoolean())
            .Select(species => species.GetProperty("id").GetInt32())
            .ToList();

        Assert.NotEmpty(legendaries);

        var loose = legendaries.Where(id => !outOfTheWild.Contains(id)).ToList();
        Assert.True(loose.Count == 0, $"legendarios que pueden salir en la hierba: {string.Join(", ", loose)}");
    }

    private static int[] Totals(int count)
    {
        var totals = new int[count + 1];
        for (var i = 1; i <= count; i++)
        {
            totals[i] = 500;
        }
        return totals;
    }

    /// <summary>The wild pool never hands out a wild-banned species; the shared pool still does.</summary>
    [Fact]
    public void Only_the_wild_pool_loses_them()
    {
        var options = new RandomizerOptions { MaxSpecies = 10, WildBannedSpecies = [4, 5], SimilarStrength = false };
        var shared = new SpeciesPool(Totals(10), options);
        var wild = WildEncounterRandomizer.PoolFor(shared, options);
        var random = new SeededRandomSource(7);

        var fromShared = new HashSet<int>();

        for (var i = 0; i < 5_000; i++)
        {
            Assert.DoesNotContain(wild.Pick(random, 1), (int[])[4, 5]);
            fromShared.Add(shared.Pick(random, 1));
        }

        Assert.Contains(4, fromShared);
        Assert.Contains(5, fromShared);
    }

    /// <summary>With no list, the wild pool is the shared one, so older worlds regenerate identically.</summary>
    [Fact]
    public void Without_the_list_nothing_changes()
    {
        var options = new RandomizerOptions { MaxSpecies = 10 };
        var shared = new SpeciesPool(Totals(10), options);

        Assert.Same(shared, WildEncounterRandomizer.PoolFor(shared, options));
    }
}
