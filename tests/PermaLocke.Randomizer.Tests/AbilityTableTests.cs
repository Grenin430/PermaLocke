using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Which abilities a randomized species may be given, and that the draw did not move.
/// </summary>
/// <remarks>
/// §136: the ceiling came from pk3DS's constant of 233 and capped the expansion mod whatever the
/// configuration said; now it comes from the game's own names, holes and banned ones out.
/// </remarks>
public sealed class AbilityTableTests
{
    /// <summary>Names shaped like the mod's: real names, «-» holes, and one past the end.</summary>
    private static string[] Names(int count, params int[] holes) =>
        [.. Enumerable.Range(0, count).Select(id => id == 0 || holes.Contains(id) ? "-" : $"Habilidad {id}")];

    [Fact]
    public void A_hole_is_never_dealt()
    {
        var pool = AbilityTable.Assignable(Names(320, 301, 302, 303, 304, 317, 318), 319, []);

        Assert.DoesNotContain(301, pool);
        Assert.DoesNotContain(318, pool);
        Assert.Contains(300, pool);
        Assert.Contains(319, pool);
        Assert.Equal(319 - 6, pool.Count);
    }

    [Fact]
    public void A_banned_ability_is_never_dealt()
    {
        var pool = AbilityTable.Assignable(Names(320), 319, [278, 279]);

        Assert.DoesNotContain(278, pool);
        Assert.DoesNotContain(279, pool);
        Assert.Contains(281, pool);
    }

    [Fact]
    public void The_ceiling_still_holds()
    {
        var pool = AbilityTable.Assignable(Names(320), 233, []);

        Assert.Equal(233, pool.Count);
        Assert.Equal(233, pool[^1]);
    }

    /// <summary>
    /// A world with nothing banned and every name present comes out ability for ability as before.
    /// </summary>
    /// <remarks>
    /// The old draw was <c>Next(1, max + 1)</c>; the new one is an index into the pool. With the pool
    /// exactly 1..max they are the same number, and that is what keeps a vanilla world, or one
    /// generated before this change, identical when it is generated again.
    /// </remarks>
    [Fact]
    public void A_full_pool_draws_exactly_what_the_old_range_did()
    {
        var pool = AbilityTable.Assignable(Names(234), 233, []);
        var fromPool = new SeededRandomSource(20260918);
        var fromRange = new SeededRandomSource(20260918);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(fromRange.Next(1, 234), pool[fromPool.Next(pool.Count)]);
        }
    }

    /// <summary>The configuration shipped with the game: the form abilities stay out.</summary>
    [Fact]
    public void The_configured_list_keeps_the_form_abilities_out()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(FindRoot(), "Data", "randomizer.json"));

        Assert.Equal(0, options.MaxAbility);
        Assert.Equal(0, options.MaxMove);

        // Cambio Heroico, Comandar, Cara de Hielo, Mutapetito, Tragamisil y las tres Tera.
        foreach (var ability in (int[])[241, 248, 258, 278, 279, 307, 308, 309])
        {
            Assert.Contains(ability, options.BannedAbilities);
        }
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encuentra la raíz.");
    }
}
