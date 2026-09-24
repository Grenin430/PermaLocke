using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The ability the gacha and the wonder trade deal, shared so the two cannot disagree (§136).
/// </summary>
public sealed class AbilityDrawTests
{
    private static List<string> Names(int count, params int[] holes) =>
        [.. Enumerable.Range(0, count).Select(id => id == 0 || holes.Contains(id) ? "-" : $"Habilidad {id}")];

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(233, true)]
    [InlineData(293, true)]   // General Supremo: la que antes se daba la vuelta a la 37
    [InlineData(301, false)]  // hueco del mod
    [InlineData(278, false)]  // Cambio Heroico, excluida
    [InlineData(319, true)]
    [InlineData(320, false)]  // fuera de la lista
    public void What_can_be_dealt(int id, bool dealable)
    {
        var names = Names(320, 301, 302, 303, 304, 317, 318);

        Assert.Equal(dealable, AbilityDraw.IsDealable(names, id, [278]));
    }

    /// <summary>The old lists marked their holes with an em dash; those are holes too.</summary>
    [Fact]
    public void An_em_dash_is_a_hole()
    {
        List<string> names = ["-", "Levitación", "—"];

        Assert.False(AbilityDraw.IsDealable(names, 2, []));
        Assert.True(AbilityDraw.IsDealable(names, 1, []));
    }

    /// <summary>
    /// Where the old rule and the new one agree, the roll is the same number: only rolls that used
    /// to land above 233 or on a hole move.
    /// </summary>
    [Fact]
    public void A_roll_the_old_rule_accepted_comes_out_the_same()
    {
        var names = Names(234);

        for (var seed = 0UL; seed < 300; seed++)
        {
            var old = new SeededRandomSource(seed).Next(1, names.Count);
            var now = AbilityDraw.Roll(new SeededRandomSource(seed), names, []);

            Assert.Equal(old, now);
        }
    }

    [Fact]
    public void A_list_with_nothing_to_deal_gives_no_ability()
    {
        Assert.Equal(0, AbilityDraw.Roll(new SeededRandomSource(1), ["-", "-", "-"], []));
        Assert.Equal(0, AbilityDraw.Roll(new SeededRandomSource(1), ["-"], []));
    }
}
