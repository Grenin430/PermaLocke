using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The two ceilings on effort values, which belong to the game and not to PermaLocke.
/// </summary>
/// <remarks>
/// Worth its own file because the whole editor leans on them: the screen lets the player type
/// anything, and this is what turns "anything" into something the cartridge will accept.
/// </remarks>
public class EvSpreadTests
{
    [Fact]
    public void An_empty_spread_has_the_whole_budget_left()
    {
        Assert.Equal(0, EvSpread.Empty.Total);
        Assert.Equal(510, EvSpread.Empty.Remaining);
        Assert.All(EvSpread.Empty.Values, value => Assert.Equal(0, value));
    }

    /// <summary>252 is the most one stat takes, whatever the player types.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(252, 252)]
    [InlineData(253, 252)]
    [InlineData(999, 252)]
    [InlineData(-40, 0)]
    public void One_stat_never_goes_past_252(int wanted, int expected)
    {
        Assert.Equal(expected, EvSpread.Empty.With(0, wanted)[0]);
    }

    /// <summary>
    /// The six share 510, so the third full stat only gets what the first two left behind.
    /// </summary>
    [Fact]
    public void The_six_share_one_budget_of_510()
    {
        var spread = EvSpread.Empty
            .With(0, 252)
            .With(1, 252)
            .With(2, 252);

        Assert.Equal(252, spread[0]);
        Assert.Equal(252, spread[1]);
        Assert.Equal(6, spread[2]);
        Assert.Equal(510, spread.Total);
        Assert.Equal(0, spread.Remaining);
    }

    /// <summary>
    /// Raising a stat that already holds EVs must not count its own share twice, or a stat sitting
    /// at 252 could never be re-set to 252.
    /// </summary>
    [Fact]
    public void Raising_a_stat_does_not_charge_it_for_what_it_already_holds()
    {
        var spread = EvSpread.Empty.With(0, 252).With(1, 200);

        var again = spread.With(1, 252);

        Assert.Equal(252, again[1]);
        Assert.Equal(504, again.Total);
    }

    [Fact]
    public void The_ceiling_says_how_far_a_stat_could_go()
    {
        var spread = EvSpread.Empty.With(0, 252).With(1, 200);

        // A la 0 le caben sus 252; a la 2 solo le queda lo que sobra de los 510.
        Assert.Equal(252, spread.CeilingFor(0));
        Assert.Equal(252, spread.CeilingFor(1));
        Assert.Equal(58, spread.CeilingFor(2));
    }

    /// <summary>
    /// A save edited elsewhere can arrive over the total. Reading it must not throw, and must not
    /// pass the illegal spread along either: it comes back clamped, so saving it fixes the Pokémon.
    /// </summary>
    [Fact]
    public void An_illegal_spread_read_from_a_save_comes_back_clamped()
    {
        var spread = EvSpread.Of([255, 255, 255, 255, 255, 255]);

        Assert.Equal(252, spread[0]);
        Assert.Equal(252, spread[1]);
        Assert.Equal(6, spread[2]);
        Assert.Equal(0, spread[3]);
        Assert.Equal(510, spread.Total);
    }

    [Fact]
    public void A_short_or_missing_list_reads_as_zeroes_rather_than_throwing()
    {
        var spread = EvSpread.Of([4, 8]);

        Assert.Equal(4, spread[0]);
        Assert.Equal(8, spread[1]);
        Assert.Equal(0, spread[5]);
        Assert.Equal(12, spread.Total);
    }

    /// <summary>Two spreads with the same six numbers are the same spread: the editor asks this.</summary>
    /// <remarks>
    /// With headroom to spare on purpose. A spread already at 510 cannot take another point
    /// anywhere, so an edit on top of it would come back clamped to the same six numbers — which
    /// is right, and would make this test prove nothing.
    /// </remarks>
    [Fact]
    public void Equality_is_by_value_so_an_untouched_edit_can_be_recognised()
    {
        var saved = EvSpread.Of([4, 8, 0, 252, 0, 100]);
        var edited = EvSpread.Of([4, 8, 0, 252, 0, 100]);

        Assert.Equal(saved, edited);
        Assert.NotEqual(saved, edited.With(2, 4));
    }

    /// <summary>A spread with the budget spent takes nothing more, and says so by not changing.</summary>
    [Fact]
    public void A_full_spread_cannot_take_another_point()
    {
        var full = EvSpread.Of([4, 8, 0, 252, 0, 246]);

        Assert.Equal(510, full.Total);
        Assert.Equal(0, full.CeilingFor(2));
        Assert.Equal(full, full.With(2, 4));
    }

    [Fact]
    public void Editing_never_mutates_the_spread_it_came_from()
    {
        var original = EvSpread.Of([4, 0, 0, 0, 0, 0]);

        original.With(0, 252);

        Assert.Equal(4, original[0]);
    }

    [Fact]
    public void A_stat_that_does_not_exist_is_a_mistake_and_not_a_silent_no_op()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvSpread.Empty.With(6, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => EvSpread.Empty.With(-1, 4));
    }
}
