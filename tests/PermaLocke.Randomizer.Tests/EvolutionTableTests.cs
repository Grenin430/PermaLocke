using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The shape of an evolution family, which is what decides whether a species may be a starter.
/// </summary>
/// <remarks>
/// The byte reading is anchored against the real cartridge with
/// <c>RomTool evoluciones</c> — 94 species with two evolutions ahead, and thirteen known families
/// checked by hand. What is tested here is the part with judgement in it: depth, branches and the
/// loop guard, none of which the cartridge exercises but a randomized evolution table would.
/// </remarks>
public sealed class EvolutionTableTests
{
    /// <summary>Builds a table from "a evolves into b" pairs, for as many species as needed.</summary>
    private static EvolutionTable Family(int size, params (int From, int Into)[] links)
    {
        var into = new IReadOnlyList<int>[size + 1];

        for (var species = 0; species <= size; species++)
        {
            into[species] = links.Where(l => l.From == species).Select(l => l.Into).ToList();
        }

        return EvolutionTable.FromTargets(into);
    }

    [Fact]
    public void A_three_stage_line_qualifies_only_from_its_first_stage()
    {
        var table = Family(3, (1, 2), (2, 3));

        Assert.True(table.HasTwoEvolutionsAhead(1));
        Assert.False(table.HasTwoEvolutionsAhead(2));
        Assert.False(table.HasTwoEvolutionsAhead(3));

        Assert.Equal(3, table.Stages(1));
        Assert.Equal(2, table.Stages(2));
        Assert.Equal(1, table.Stages(3));
    }

    [Fact]
    public void A_two_stage_line_does_not_qualify()
    {
        var table = Family(2, (1, 2));

        Assert.False(table.HasTwoEvolutionsAhead(1));
        Assert.Equal(2, table.Stages(1));
    }

    [Fact]
    public void Something_that_never_evolves_does_not_qualify()
    {
        var table = Family(1);

        Assert.False(table.HasTwoEvolutionsAhead(1));
        Assert.Equal(1, table.Stages(1));
        Assert.True(table.IsBase(1));
    }

    /// <summary>
    /// Eevee's shape: one base, many branches, still only two stages. Counting branches instead of
    /// depth would have made it a starter.
    /// </summary>
    [Fact]
    public void Many_branches_of_one_step_are_still_two_stages()
    {
        var table = Family(4, (1, 2), (1, 3), (1, 4));

        Assert.False(table.HasTwoEvolutionsAhead(1));
        Assert.Equal(2, table.Stages(1));
    }

    /// <summary>A family is as deep as its deepest branch, not its shortest.</summary>
    [Fact]
    public void The_longest_branch_decides()
    {
        var table = Family(4, (1, 2), (1, 3), (3, 4));

        Assert.True(table.HasTwoEvolutionsAhead(1));
        Assert.Equal(3, table.Stages(1));
    }

    /// <summary>
    /// A loop must not hang. The cartridge has none, but this table is read out of a file a
    /// randomizer writes, and pointing an evolution back at its own line is exactly what that does.
    /// </summary>
    /// <remarks>
    /// The depth of a ring is not a meaningful number, so what is asserted is what matters: it
    /// terminates, and nothing in a ring is a base, so none of it can be offered as a starter.
    /// </remarks>
    [Fact]
    public void A_loop_is_survived_rather_than_recursed_into()
    {
        var table = Family(3, (1, 2), (2, 3), (3, 1));

        Assert.InRange(table.Stages(1), 1, 4);
        Assert.False(table.IsBase(1));
        Assert.False(table.HasTwoEvolutionsAhead(1));
        Assert.False(table.HasTwoEvolutionsAhead(2));
        Assert.False(table.HasTwoEvolutionsAhead(3));
    }

    /// <summary>
    /// The answer must not depend on which species was asked about first.
    /// </summary>
    /// <remarks>
    /// The reason a depth reached by breaking a loop is never memoised. Cached, the table would
    /// answer differently depending on the order of the questions, and a randomizer asks in
    /// whatever order the pool happens to be in.
    /// </remarks>
    [Fact]
    public void The_order_of_the_questions_does_not_change_the_answers()
    {
        var forwards = Family(5, (1, 2), (2, 3), (3, 2), (4, 5));
        var backwards = Family(5, (1, 2), (2, 3), (3, 2), (4, 5));

        var asked = Enumerable.Range(1, 5).Select(forwards.Stages).ToList();

        foreach (var species in Enumerable.Range(1, 5).Reverse())
        {
            _ = backwards.Stages(species);
        }

        Assert.Equal(asked, [.. Enumerable.Range(1, 5).Select(backwards.Stages)]);
    }

    /// <summary>A species that evolves into itself is noise, not another stage.</summary>
    [Fact]
    public void Evolving_into_itself_adds_no_stage()
    {
        var table = Family(1, (1, 1));

        Assert.Equal(1, table.Stages(1));
        Assert.True(table.IsBase(1));
    }

    /// <summary>The middle of a line is not a base, however deep what follows it is.</summary>
    [Fact]
    public void A_middle_stage_is_never_a_base()
    {
        var table = Family(4, (1, 2), (2, 3), (3, 4));

        Assert.False(table.IsBase(2));
        Assert.False(table.HasTwoEvolutionsAhead(2));
    }
}
