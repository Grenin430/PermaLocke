using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Rewriting the evolutions a solo player cannot reach.
/// </summary>
/// <remarks>
/// The byte layout and the method numbers are anchored against the cartridge with
/// <c>RomTool evo-dump</c>, and the whole thing was verified end to end on a generated mod: 30
/// trade entries and 9 move-taught ones converted, and methods 5, 6, 7 and 21 gone from the file.
/// What is tested here is each decision on its own, because a run that converts thirty entries
/// tells you thirty things went somewhere, not that each went to the right place.
/// </remarks>
public sealed class ImpossibleEvolutionFixerTests
{
    private const int EntrySize = 8;

    private static readonly Dictionary<int, int> NoPartners = [];

    /// <summary>One species' block: eight slots, of which only the first few are used.</summary>
    private static byte[] Block(params (int Method, int Argument, int Target, int Level)[] entries)
    {
        var block = new byte[8 * EntrySize];

        for (var i = 0; i < entries.Length; i++)
        {
            var (method, argument, target, level) = entries[i];
            var at = i * EntrySize;

            BitConverter.GetBytes((ushort)method).CopyTo(block, at);
            BitConverter.GetBytes((ushort)argument).CopyTo(block, at + 2);
            BitConverter.GetBytes((ushort)target).CopyTo(block, at + 4);
            block[at + 6] = unchecked((byte)-1);
            block[at + 7] = (byte)level;
        }

        return block;
    }

    private static (int Method, int Argument, int Target, int Level) Slot(byte[] block, int index)
    {
        var at = index * EntrySize;

        return (BitConverter.ToUInt16(block, at), BitConverter.ToUInt16(block, at + 2),
            BitConverter.ToUInt16(block, at + 4), block[at + 7]);
    }

    private static ImpossibleEvolutionFixer Fixer(bool learnsets = true, int level = 37) =>
        new(new RandomizerOptions { RandomizeLearnsets = learnsets, TradeEvolutionLevel = level });

    /// <summary>Kadabra: a plain trade becomes a level.</summary>
    [Fact]
    public void A_plain_trade_becomes_a_level()
    {
        var block = Block((5, 0, 65, 0));

        var result = Fixer().FixSpecies(block, 64, NoPartners);

        Assert.Equal(1, result.Trades);
        Assert.Equal((4, 0, 65, 37), Slot(block, 0));
    }

    /// <summary>The level is configuration, not a constant hidden in the code.</summary>
    [Fact]
    public void The_level_comes_from_the_options()
    {
        var block = Block((5, 0, 65, 0));

        Fixer(level: 25).FixSpecies(block, 64, NoPartners);

        Assert.Equal(25, Slot(block, 0).Level);
    }

    /// <summary>
    /// Onix: the item is kept, because the cartridge already asked for it and it still makes sense.
    /// </summary>
    [Fact]
    public void A_trade_holding_an_item_keeps_the_item()
    {
        var block = Block((6, 233, 208, 0));

        Fixer().FixSpecies(block, 95, NoPartners);

        Assert.Equal((19, 233, 208, 0), Slot(block, 0));
    }

    /// <summary>
    /// Slowpoke is the exception: a stone, so it does not share a trigger with Slowbro.
    /// </summary>
    [Fact]
    public void Slowking_gets_a_water_stone_instead()
    {
        var block = Block((4, 0, 80, 37), (6, 221, 199, 37));

        Fixer().FixSpecies(block, 79, NoPartners);

        Assert.Equal((8, 84, 199, 0), Slot(block, 1));

        // Y lo que ya se podía alcanzar se queda como estaba.
        Assert.Equal((4, 0, 80, 37), Slot(block, 0));
    }

    /// <summary>The King's Rock on anything else stays a King's Rock.</summary>
    [Fact]
    public void The_exception_is_only_for_Slowking()
    {
        var block = Block((6, 221, 186, 0));

        Fixer().FixSpecies(block, 61, NoPartners);

        Assert.Equal((19, 221, 186, 0), Slot(block, 0));
    }

    /// <summary>Karrablast and Shelmet: each needs the other in the party.</summary>
    [Fact]
    public void The_two_that_swap_ask_for_each_other()
    {
        var partners = new Dictionary<int, int> { [588] = 616, [616] = 588 };

        var karrablast = Block((7, 0, 589, 0));
        var shelmet = Block((7, 0, 617, 0));

        Fixer().FixSpecies(karrablast, 588, partners);
        Fixer().FixSpecies(shelmet, 616, partners);

        Assert.Equal((22, 616, 589, 0), Slot(karrablast, 0));
        Assert.Equal((22, 588, 617, 0), Slot(shelmet, 0));
    }

    /// <summary>
    /// With no partner worked out, the entry is left exactly as it was.
    /// </summary>
    /// <remarks>
    /// Guessing a partner would produce an evolution that asks for the wrong Pokémon and never
    /// fires, which is the same as before but harder to notice. Untouched, the verify step counts
    /// it and the report says so out loud.
    /// </remarks>
    [Fact]
    public void With_no_partner_the_entry_is_left_alone()
    {
        var block = Block((7, 0, 589, 0));

        var result = Fixer().FixSpecies(block, 588, NoPartners);

        Assert.Equal(0, result.Trades);
        Assert.Equal((7, 0, 589, 0), Slot(block, 0));
    }

    /// <summary>Lickitung: the move it waited for is replaced by a level.</summary>
    [Fact]
    public void A_move_taught_evolution_becomes_a_level()
    {
        var block = Block((21, 205, 463, 0));

        var result = Fixer().FixSpecies(block, 108, NoPartners);

        Assert.Equal(1, result.Moves);
        Assert.Equal(0, result.Trades);
        Assert.Equal((4, 0, 463, 33), Slot(block, 0));
    }

    /// <summary>
    /// The expansion's move-taught evolutions, at the level each pre-evolution learns the move in the
    /// unrandomized base layer. They were missing and stayed impossible in the installed world.
    /// </summary>
    [Theory]
    [InlineData(57, 889, 979, 35)]    // Primeape -> Annihilape
    [InlineData(203, 888, 981, 32)]   // Girafarig -> Farigiraf
    [InlineData(234, 828, 899, 21)]   // Stantler -> Wyrdeer
    [InlineData(852, 269, 853, 35)]   // Clobbopus -> Grapploct
    [InlineData(1100, 839, 904, 28)]  // Qwilfish de Hisui -> Overqwil
    [InlineData(1011, 913, 1019, 45)] // Dipplin -> Hydrapple: lo aprende a nivel 1, 45 como Piloswine y Poipole
    public void The_expansion_move_evolutions_become_levels(int species, int move, int target, int level)
    {
        var block = Block((21, move, target, 0));

        var result = Fixer().FixSpecies(block, species, NoPartners);

        Assert.Equal(1, result.Moves);
        Assert.Equal((4, 0, target, level), Slot(block, 0));
    }

    /// <summary>
    /// Without randomized learnsets it is left alone: the move is still learnable, so the
    /// evolution still works, and changing it would be a change nobody asked for.
    /// </summary>
    [Fact]
    public void A_move_taught_evolution_survives_unrandomized_learnsets()
    {
        var block = Block((21, 205, 463, 0));

        var result = Fixer(learnsets: false).FixSpecies(block, 108, NoPartners);

        Assert.Equal(0, result.Moves);
        Assert.Equal((21, 205, 463, 0), Slot(block, 0));
    }

    /// <summary>
    /// A move-taught entry pointing somewhere the table does not expect is left alone.
    /// </summary>
    /// <remarks>
    /// Which is why the table is keyed by species <em>and</em> target. If the evolution lines are
    /// randomized, Lickitung may no longer point at Lickilicky, and handing that new evolution
    /// Lickilicky's level 33 would be inventing a number for a pair nobody measured.
    /// </remarks>
    [Fact]
    public void A_move_taught_evolution_that_moved_is_left_alone()
    {
        var block = Block((21, 205, 999, 0));

        var result = Fixer().FixSpecies(block, 108, NoPartners);

        Assert.Equal(0, result.Moves);
        Assert.Equal((21, 205, 999, 0), Slot(block, 0));
    }

    /// <summary>Everything already reachable alone is untouched, empty slots included.</summary>
    [Fact]
    public void What_already_works_is_not_touched()
    {
        var block = Block((4, 0, 2, 16), (8, 83, 26, 0), (1, 0, 169, 0));
        var before = block.ToArray();

        var result = Fixer().FixSpecies(block, 1, NoPartners);

        Assert.Equal(0, result.Trades);
        Assert.Equal(0, result.Moves);
        Assert.Equal(before, block);
    }
}
