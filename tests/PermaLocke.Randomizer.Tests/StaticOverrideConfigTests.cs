namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The static encounter rules as shipped, and the one property that makes adding one safe.
/// </summary>
/// <remarks>
/// <para>
/// The rules in <c>Data/randomizer.json</c> decide what the story bosses are, and a new rule gets
/// added to a world somebody is already playing. Added the ordinary way it runs before the draw, on
/// the draw's own stream, and moves the whole table: measured on the player's installed world, the
/// two Necrozma fusions changed <b>249 of 252 statics and all 7 trades</b> — Totems and legendaries
/// still ahead of him — to fix two. With <see cref="StaticOverride.IndependentDraw"/> the same two
/// rules changed <b>2 of 252 and nothing else</b>, byte for byte. That measurement needs the
/// cartridge and lives in <c>docs/ARCHITECTURE.md</c>; what can be pinned without one is here.
/// </para>
/// <para>
/// And the file itself: while this was being done, a quote inside one of its comments made the
/// whole configuration unreadable, and the only thing that said so was the randomizer throwing.
/// </para>
/// </remarks>
public sealed class StaticOverrideConfigTests
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

    private static RandomizerOptions Shipped() =>
        RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));

    /// <summary>Every Necrozma that is a boss fight is a mega, and the catchable ones are not.</summary>
    /// <remarks>
    /// Necrozma is four encounters and only the form tells them apart: 1 and 2 are the fusions with
    /// Solgaleo and Lunala, 3 is Ultra Necrozma, 0 the one you catch. The fusion with Lunala went out
    /// as an ordinary Swampert, which in the one fight of the story that is built around it read as
    /// a mistake.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_boss_Necrozma_is_a_mega(int form)
    {
        var rule = Assert.Single(Shipped().StaticOverrides, r => r.Species == 800 && r.Form == form);

        Assert.Equal(StaticOverrideRule.Mega, rule.Rule);
    }

    /// <summary>
    /// The rules added on top of an installed world draw on their own, and the old ones do not.
    /// </summary>
    /// <remarks>
    /// Both halves matter. The new ones have to be independent or installing them re-rolls the
    /// world. The old ones have to stay as they were, because they are part of how the worlds already
    /// being played were generated — switching them over would re-roll those just the same.
    /// </remarks>
    [Fact]
    public void The_fusions_draw_on_their_own_and_the_older_rules_do_not()
    {
        var rules = Shipped().StaticOverrides;

        Assert.All(rules.Where(r => r.Species == 800 && r.Form is 1 or 2),
            r => Assert.True(r.IndependentDraw, r.Note));

        Assert.False(Assert.Single(rules, r => r.Species == 800 && r.Form == 3).IndependentDraw);
        Assert.False(Assert.Single(rules, r => r.Species == 793).IndependentDraw);
        Assert.False(Assert.Single(rules, r => r.Species == 800 && r.Form == 0).IndependentDraw);
        Assert.False(Assert.Single(rules, r => r.Species == 792).IndependentDraw);
    }

    /// <summary>A rule that does not say is an ordinary one, so older files read the same.</summary>
    [Fact]
    public void A_rule_that_does_not_mention_it_is_not_independent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"permalocke-override-{Guid.NewGuid():N}.json");

        File.WriteAllText(path, """
            { "staticOverrides": [ { "species": 800, "form": 3, "rule": "Mega" } ] }
            """);

        try
        {
            var rule = Assert.Single(RandomizerOptionsLoader.Load(path).StaticOverrides);
            Assert.False(rule.IndependentDraw);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
