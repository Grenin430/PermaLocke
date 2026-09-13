using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The stat formula, which decides what number ends up written into somebody's Pokémon.
/// </summary>
/// <remarks>
/// Every case here is worked out by hand in its own comment rather than copied from a run, because
/// a table of expected values lifted from the thing being tested proves only that it still does
/// what it did. The formula itself is the series': for PS,
/// <c>(2·base + IV + EV/4)·nivel/100 + nivel + 10</c>, and for the rest the same core plus five,
/// times the nature.
/// </remarks>
public sealed class StatCalculatorTests
{
    private static byte[] Bases(params int[] values) => [.. values.Select(v => (byte)v)];

    private static int[] Six(int value) => [.. Enumerable.Repeat(value, 6)];

    /// <summary>Naturaleza 0 is Fuerte, which raises nothing and lowers nothing.</summary>
    private const int Neutral = 0;

    /// <summary>
    /// PS at level 50 with perfect IVs and no effort: (2·100 + 31 + 0)·50/100 + 50 + 10 = 175.
    /// </summary>
    [Fact]
    public void Hp_is_the_core_plus_the_level_plus_ten()
    {
        var stats = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100),
            Six(31), Six(0), level: 50, Neutral);

        Assert.Equal(175, stats[0]);
    }

    /// <summary>
    /// The others are the core plus five: (2·100 + 31)·50/100 = 115, and 115 + 5 = 120.
    /// </summary>
    [Fact]
    public void The_other_five_are_the_core_plus_five()
    {
        var stats = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100),
            Six(31), Six(0), level: 50, Neutral);

        Assert.Equal([175, 120, 120, 120, 120, 120], stats);
    }

    /// <summary>
    /// Effort counts in fours and truncates, so three points are worth nothing at all.
    /// </summary>
    /// <remarks>
    /// It is the game's rounding and not ours, and it is why the screen can look broken to somebody
    /// who spread a handful of points around: 252 and 255 also give exactly the same stat.
    /// </remarks>
    [Fact]
    public void Effort_below_four_does_nothing()
    {
        var none = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), Six(31), Six(0), 50, Neutral);
        var three = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), Six(31), Six(3), 50, Neutral);
        var four = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), Six(31), Six(4), 50, Neutral);

        Assert.Equal(none, three);
        Assert.NotEqual(none, four);
    }

    /// <summary>252 and 255 are the same stat: 63 either way once divided by four.</summary>
    [Fact]
    public void Two_hundred_and_fifty_two_is_as_good_as_two_hundred_and_fifty_five()
    {
        var capped = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), Six(31), Six(252), 50, Neutral);
        var over = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), Six(31), Six(255), 50, Neutral);

        Assert.Equal(capped, over);
    }

    /// <summary>
    /// A nature moves one stat up a tenth and another down a tenth, and never PS.
    /// </summary>
    /// <remarks>
    /// Naturaleza 1 is Huraña: raises Ataque, lowers Defensa. The index counts from Ataque, so a
    /// mapping that forgot to shift it would move Defensa and At. Esp. instead — and the totals
    /// would still look plausible, which is why this checks all six rather than the two.
    /// </remarks>
    [Fact]
    public void A_nature_raises_one_and_lowers_another()
    {
        var stats = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100),
            Six(31), Six(0), level: 50, nature: 1);

        Assert.Equal(175, stats[0]);            // los PS no los toca ninguna naturaleza
        Assert.Equal(132, stats[1]);            // 120 x 1,1
        Assert.Equal(108, stats[2]);            // 120 x 0,9
        Assert.Equal([120, 120, 120], stats[3..]);
    }

    /// <summary>
    /// A nature whose two halves fall where the two orders <b>disagree</b>.
    /// </summary>
    /// <remarks>
    /// This is the test that was missing. The nature's numbering runs over the game's internal
    /// order — Ataque, Defensa, <b>Velocidad</b>, At. Esp., Def. Esp. — and the one above uses
    /// Huraña, which raises Ataque and lowers Defensa: those two sit in the same slot in both
    /// orders, so it passed while the mapping was wrong. Naturaleza 20 is Modesta in the internal
    /// numbering: raises index 4, which is <b>Def. Esp.</b>, and lowers index 0, Ataque. Read with
    /// the screen's order instead it would move Velocidad, and that is exactly the eleven wrong
    /// numbers the real partida showed.
    /// </remarks>
    [Fact]
    public void A_nature_uses_the_games_order_and_not_the_screens()
    {
        var stats = StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100),
            Six(31), Six(0), level: 50, nature: 20);

        Assert.Equal(175, stats[0]);            // PS, intocables
        Assert.Equal(108, stats[1]);            // Ataque  120 x 0,9
        Assert.Equal(120, stats[2]);            // Defensa, sin tocar
        Assert.Equal(120, stats[3]);            // At. Esp., sin tocar
        Assert.Equal(132, stats[4]);            // Def. Esp.  120 x 1,1
        Assert.Equal(120, stats[5]);            // Velocidad, sin tocar: aqui fallaba
    }

    /// <summary>The five natures whose two halves coincide do nothing at all.</summary>
    [Fact]
    public void A_neutral_nature_changes_nothing()
    {
        var plain = StatCalculator.Compute(Bases(90, 90, 90, 90, 90, 90), Six(20), Six(0), 40, Neutral);

        foreach (var nature in (int[])[0, 6, 12, 18, 24])
        {
            Assert.Equal(plain,
                StatCalculator.Compute(Bases(90, 90, 90, 90, 90, 90), Six(20), Six(0), 40, nature));
        }
    }

    /// <summary>The twenty-five natures are the only ones there are; anything else is left alone.</summary>
    [Fact]
    public void An_impossible_nature_multiplies_by_one()
    {
        Assert.Equal(1.0, StatCalculator.NatureFactor(25, 1));
        Assert.Equal(1.0, StatCalculator.NatureFactor(-1, 1));

        // Y los PS nunca, ni siquiera con una naturaleza que suba el Ataque.
        Assert.Equal(1.0, StatCalculator.NatureFactor(1, 0));
    }

    /// <summary>
    /// Shedinja always has one PS, whatever the formula works out.
    /// </summary>
    /// <remarks>
    /// The game forces it by species, so a world with <c>shuffleBaseStats</c> can hand Shedinja a
    /// base PS of anything at all and it still has one. Without the special case, training it would
    /// write a Shedinja with seventy PS into the partida.
    /// </remarks>
    [Fact]
    public void Shedinja_always_has_one_hp()
    {
        var normal = StatCalculator.Compute(Bases(120, 90, 45, 30, 30, 40),
            Six(31), Six(252), 60, Neutral, isShedinja: false);

        var shedinja = StatCalculator.Compute(Bases(120, 90, 45, 30, 30, 40),
            Six(31), Six(252), 60, Neutral, isShedinja: true);

        Assert.True(normal[0] > 1);
        Assert.Equal(1, shedinja[0]);

        // Y lo demas no lo toca.
        Assert.Equal(normal[1..], shedinja[1..]);
    }

    /// <summary>
    /// A level 1 Pokémon out of the gacha has to come out with sane numbers.
    /// </summary>
    /// <remarks>
    /// At level 1 the core is <c>(2·base + IV)/100</c> truncated, so it is zero up to base 49 and
    /// one from base 50 — which is why 49 gives five and 65 gives six. Worth writing down: the
    /// first version of this test expected five across the board and the code was right.
    /// </remarks>
    [Fact]
    public void Level_one_still_works()
    {
        var stats = StatCalculator.Compute(Bases(45, 49, 49, 65, 65, 45), Six(0), Six(0), 1, Neutral);

        Assert.Equal(11, stats[0]);                     // (90/100=0) + 1 + 10
        Assert.Equal([5, 5, 6, 6, 5], stats[1..]);      // 98/100=0 contra 130/100=1
    }

    /// <summary>
    /// A table that is not six long is a mistake worth throwing over, not clamping.
    /// </summary>
    /// <remarks>
    /// Anything shorter would silently stop computing halfway and leave the rest of the stats at
    /// zero, which written into a partida is a Pokémon with no Velocidad.
    /// </remarks>
    [Fact]
    public void A_table_of_the_wrong_size_is_refused()
    {
        Assert.Throws<ArgumentException>(() =>
            StatCalculator.Compute(Bases(100, 100, 100), Six(31), Six(0), 50, Neutral));

        Assert.Throws<ArgumentException>(() =>
            StatCalculator.Compute(Bases(100, 100, 100, 100, 100, 100), [31, 31], Six(0), 50, Neutral));
    }
}
