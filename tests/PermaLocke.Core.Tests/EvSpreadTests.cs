using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The two ceilings on effort values, which belong to the game and not to PermaLocke.
/// </summary>
/// <remarks>
/// They are enforced differently on purpose, and that difference is what most of this file is
/// about: 252 per stat is clamped, 510 in total is only checked. Clamping the total would decide
/// the order somebody has to work in — empty this before filling that — and the whole point of the
/// editor is to let a spread be moved around freely.
/// </remarks>
public class EvSpreadTests
{
    /// <summary>MÁX with two stats full gives the third what is left of 510, not 252.</summary>
    [Fact]
    public void Room_is_what_is_left_of_the_budget()
    {
        var spread = EvSpread.Of([252, 252, 0, 0, 0, 0]);

        Assert.Equal(6, spread.RoomFor(2));
        Assert.Equal(252, EvSpread.Of([0, 0, 0, 0, 0, 0]).RoomFor(0));
        Assert.Equal(252, spread.RoomFor(0));
    }

    [Fact]
    public void An_empty_spread_has_the_whole_budget_left()
    {
        Assert.Equal(0, EvSpread.Empty.Total);
        Assert.Equal(510, EvSpread.Empty.Remaining);
        Assert.True(EvSpread.Empty.IsLegal);
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
    /// The scenario the editor exists for: 252 in HP and 252 in Attack, and now the player wants
    /// Speed instead. Filling Speed first has to work, or they would have to know to empty HP
    /// before touching anything — which nobody would guess.
    /// </summary>
    [Fact]
    public void A_stat_can_be_filled_even_with_no_budget_left()
    {
        var full = EvSpread.Empty.With(0, 252).With(1, 252);

        var overflowing = full.With(5, 252);

        Assert.Equal(252, overflowing[5]);
        Assert.Equal(756, overflowing.Total);
        Assert.False(overflowing.IsLegal);
        Assert.Equal(246, overflowing.Over);

        // Y al quitar de PS vuelve a ser legal, sin haber tenido que hacerlo en ese orden.
        var settled = overflowing.With(0, 0);

        Assert.True(settled.IsLegal);
        Assert.Equal(504, settled.Total);
        Assert.Equal(0, settled.Over);
    }

    [Fact]
    public void Exactly_510_is_legal_and_511_is_not()
    {
        var exact = EvSpread.Of([252, 252, 6, 0, 0, 0]);
        Assert.Equal(510, exact.Total);
        Assert.True(exact.IsLegal);
        Assert.Equal(0, exact.Over);

        var over = exact.With(3, 1);
        Assert.False(over.IsLegal);
        Assert.Equal(1, over.Over);
    }

    /// <summary>
    /// Raising a stat that already holds EVs must not count its own share twice: setting 252 on a
    /// stat that already sits at 252 has to be a no-op, not a refusal.
    /// </summary>
    [Fact]
    public void Setting_a_stat_to_what_it_already_holds_changes_nothing()
    {
        var spread = EvSpread.Empty.With(0, 252).With(1, 200);

        Assert.Equal(spread, spread.With(0, 252));
    }

    /// <summary>
    /// A save edited elsewhere can arrive over the total. Reading it must not throw, and must not
    /// quietly decide which stats to rob either: it comes back as it is, and says it is illegal.
    /// </summary>
    [Fact]
    public void An_illegal_spread_read_from_a_save_is_shown_as_it_is_and_flagged()
    {
        var spread = EvSpread.Of([255, 255, 255, 255, 255, 255]);

        // 255 pasa de 252, y eso sí se recorta: es el tope de la propia estadística.
        Assert.All(spread.Values, value => Assert.Equal(252, value));
        Assert.Equal(1512, spread.Total);
        Assert.False(spread.IsLegal);
        Assert.Equal(1002, spread.Over);
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
    [Fact]
    public void Equality_is_by_value_so_an_untouched_edit_can_be_recognised()
    {
        var saved = EvSpread.Of([4, 8, 0, 252, 0, 246]);
        var edited = EvSpread.Of([4, 8, 0, 252, 0, 246]);

        Assert.Equal(saved, edited);
        Assert.NotEqual(saved, edited.With(2, 4));
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
