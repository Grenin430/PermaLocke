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

    /// <summary>A straight family comes out as one rung per stage.</summary>
    [Fact]
    public void A_line_is_its_stages_in_order()
    {
        var line = Family(3, (1, 2), (2, 3)).Lines().Single();

        Assert.Equal(3, line.Count);
        Assert.Equal([1], line[0]);
        Assert.Equal([2], line[1]);
        Assert.Equal([3], line[2]);
    }

    /// <summary>
    /// A branch puts its alternatives on the same rung, which is what the gacha picks between.
    /// </summary>
    /// <remarks>
    /// Wurmple's shape: one base, two second stages, two third ones. Written as
    /// <c>[[1], [2, 3], [4, 5]]</c>, which is the whole reason the stages are lists.
    /// </remarks>
    [Fact]
    public void A_branching_family_keeps_its_alternatives_on_one_rung()
    {
        var line = Family(5, (1, 2), (1, 3), (2, 4), (3, 5)).Lines().Single();

        Assert.Equal(3, line.Count);
        Assert.Equal([1], line[0]);
        Assert.Equal([2, 3], line[1]);
        Assert.Equal([4, 5], line[2]);
    }

    /// <summary>
    /// Branches of different lengths keep the family as deep as its deepest one.
    /// </summary>
    /// <remarks>
    /// The one that stops early simply has nothing on the last rung, so a roll that asks for the
    /// third stage of this family can only land on the branch that has one.
    /// </remarks>
    [Fact]
    public void An_uneven_branch_leaves_the_short_side_behind()
    {
        var line = Family(4, (1, 2), (1, 3), (2, 4)).Lines().Single();

        Assert.Equal(3, line.Count);
        Assert.Equal([2, 3], line[1]);
        Assert.Equal([4], line[2]);
    }

    /// <summary>
    /// A species two rungs could reach belongs to the first, not to both.
    /// </summary>
    /// <remarks>
    /// Otherwise the same family would hand it out from two different stages, and «which stage did
    /// I get» would stop being a straight answer. The vanilla cartridge has no such family; a
    /// randomized evolution table can build one.
    /// </remarks>
    [Fact]
    public void A_species_two_rungs_could_reach_sits_on_the_first()
    {
        var line = Family(3, (1, 2), (1, 3), (2, 3)).Lines().Single();

        Assert.Equal(2, line.Count);
        Assert.Equal([2, 3], line[1]);
    }

    /// <summary>A loop below the base ends the family instead of spinning forever.</summary>
    [Fact]
    public void A_loop_ends_the_line_instead_of_running_forever()
    {
        var line = Family(3, (1, 2), (2, 3), (3, 2)).Lines().Single();

        Assert.Equal(3, line.Count);
        Assert.Equal([3], line[2]);
    }

    /// <summary>
    /// A family that is nothing but a loop has no base, so it produces no line at all.
    /// </summary>
    /// <remarks>
    /// Said out loud because of what it costs: species in no family cannot come out of the gacha,
    /// and they would go missing without a word. The cartridge has no such loop and
    /// <c>randomizeEvolutions</c> is false, so this cannot happen today — which is exactly why
    /// <c>RomTool species</c> counts the species left out of every family and refuses to be
    /// silent about it, instead of this being trusted to stay true.
    /// </remarks>
    [Fact]
    public void A_family_that_is_only_a_loop_produces_nothing()
    {
        Assert.Empty(Family(3, (1, 2), (2, 3), (3, 1)).Lines());
    }

    /// <summary>Every species belongs to exactly one family, and none is left out.</summary>
    [Fact]
    public void The_families_between_them_hold_every_species_once()
    {
        var lines = Family(6, (1, 2), (2, 3), (4, 5)).Lines();

        var all = lines.SelectMany(line => line.SelectMany(stage => stage)).ToList();

        Assert.Equal(6, all.Count);
        Assert.Equal(6, all.Distinct().Count());
        Assert.Contains(6, all);
    }
}
