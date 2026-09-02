using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Where a species' line ends, which is what «fully evolved» has to mean.
/// </summary>
/// <remarks>
/// The competition asks that from the sixth trial on every trainer carries final evolutions. The
/// hard part is not the rule, it is that a family does not always have one ending: Eevee has eight
/// and Wurmple has two at different depths. Picking at random there would make the same seed give a
/// different mod every time, and a randomizer that cannot be recomputed cannot be audited.
/// </remarks>
public sealed class FinalEvolutionTests
{
    /// <summary>Builds a table from «species N evolves into these».</summary>
    private static EvolutionTable Table(params (int From, int[] Into)[] lines)
    {
        var size = lines.SelectMany(l => l.Into.Append(l.From)).Max() + 1;
        var into = new IReadOnlyList<int>[size];

        for (var i = 0; i < size; i++)
        {
            into[i] = [];
        }

        foreach (var line in lines)
        {
            into[line.From] = line.Into;
        }

        return EvolutionTable.FromTargets(into);
    }

    [Fact]
    public void A_three_stage_line_ends_at_its_third()
    {
        var table = Table((1, [2]), (2, [3]));

        Assert.Equal(3, table.FinalOf(1));
        Assert.Equal(3, table.FinalOf(2));
        Assert.Equal(3, table.FinalOf(3));
    }

    /// <summary>Something that never evolves is already final, and comes back untouched.</summary>
    [Fact]
    public void Something_that_does_not_evolve_is_its_own_final_form()
    {
        var table = Table((1, [2]));

        Assert.Equal(2, table.FinalOf(2));
        Assert.Equal(132, table.FinalOf(132));
        Assert.Equal(0, table.FinalOf(0));
    }

    /// <summary>
    /// Branches of different depth: the deepest wins, because that is what fully evolved means.
    /// </summary>
    [Fact]
    public void The_deepest_branch_wins()
    {
        // 1 evoluciona a 2 (que sigue a 3) o a 9, que se queda ahí.
        var table = Table((1, [2, 9]), (2, [3]));

        Assert.Equal(3, table.FinalOf(1));
    }

    /// <summary>
    /// Branches of equal depth: the lowest id, always, so the same seed gives the same mod.
    /// </summary>
    [Fact]
    public void A_tie_is_broken_the_same_way_every_time()
    {
        var table = Table((133, [196, 197, 134]));

        Assert.Equal(134, table.FinalOf(133));
        Assert.Equal(134, table.FinalOf(133));
    }

    /// <summary>
    /// A randomized evolution table can contain a loop. Without a guard this recurses for ever.
    /// </summary>
    [Fact]
    public void A_loop_does_not_hang()
    {
        var table = Table((1, [2]), (2, [3]), (3, [1]));

        // Lo que importa es que conteste, y que conteste algo de la familia.
        Assert.Contains(table.FinalOf(1), (int[])[1, 2, 3]);
    }
}
