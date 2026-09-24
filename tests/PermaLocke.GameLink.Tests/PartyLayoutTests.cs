using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Telling the party's real structures apart from views of the same one.
/// </summary>
/// <remarks>
/// The sweep starts a candidate wherever a Pokémon header appears, so a party of five reports five
/// layouts: one per member, each a view of the same block starting further in. Writes then go to
/// <c>SlotAddress(slot)</c> of every layout, which sends a correction aimed at slot 4 into slot 5
/// of the real party — a different Pokémon.
/// </remarks>
public class PartyLayoutTests
{
    /// <summary>
    /// The eight addresses a real run reported, which is what showed the problem: two structures
    /// and six extra views of them.
    /// </summary>
    private static readonly PartyLayout[] AsFound =
    [
        new(0x330128E4, 0x104, "Grenin430"),
        new(0x330129E8, 0x104, "Grenin430"),
        new(0x33012AEC, 0x104, "Grenin430"),
        new(0x33012BF0, 0x104, "Grenin430"),
        new(0x33F7FA44, 0x1E4, "Grenin430"),
        new(0x33F7FC28, 0x1E4, "Grenin430"),
        new(0x33F7FE0C, 0x1E4, "Grenin430"),
        new(0x33F7FFF0, 0x1E4, "Grenin430"),
    ];

    [Fact]
    public void Eight_candidates_are_really_two_structures()
    {
        var distinct = PartyLayoutLocator.Distinct(AsFound);

        Assert.Equal(2, distinct.Count);
        Assert.Contains(distinct, l => l.Address == 0x330128E4 && l.Stride == 0x104);
        Assert.Contains(distinct, l => l.Address == 0x33F7FA44 && l.Stride == 0x1E4);
    }

    /// <summary>The one kept is the first slot, so <c>SlotAddress</c> means what it says.</summary>
    [Fact]
    public void The_survivor_is_the_start_of_the_block()
    {
        var party = PartyLayoutLocator.Distinct(AsFound).Single(l => l.Stride == 0x104);

        Assert.Equal(0x330128E4u, party.Address);
        Assert.Equal(0x330128E4u, party.SlotAddress(0));
        Assert.Equal(0x33012CF4u, party.SlotAddress(4));
    }

    /// <summary>
    /// Two structures that happen to share a stride but sit far apart are two structures. Anything
    /// past six slots cannot be a view of the same party.
    /// </summary>
    [Fact]
    public void Blocks_further_apart_than_a_party_are_kept_apart()
    {
        var layouts = PartyLayoutLocator.Distinct([
            new PartyLayout(0x1000, 0x104, "A"),
            new PartyLayout((uint)(0x1000 + (0x104 * 6)), 0x104, "A"),
        ]);

        Assert.Equal(2, layouts.Count);
    }

    /// <summary>An address that is not a whole number of slots away is its own block.</summary>
    [Fact]
    public void An_address_off_the_grid_is_not_a_view_of_the_same_block()
    {
        var layouts = PartyLayoutLocator.Distinct([
            new PartyLayout(0x1000, 0x104, "A"),
            new PartyLayout(0x1050, 0x104, "A"),
        ]);

        Assert.Equal(2, layouts.Count);
    }

    [Fact]
    public void Nothing_in_means_nothing_out()
    {
        Assert.Empty(PartyLayoutLocator.Distinct([]));
    }

    private static PartyLayout Authoritative(uint address) =>
        new(address, PartyLayoutLocator.AuthoritativeStride, "Grenin");

    private static PartyLayout Mirror(uint address) =>
        new(address, PartyLayoutLocator.CopyStride, "Grenin");

    /// <summary>
    /// The authoritative structure wins even when the mirror reads more of the party.
    /// </summary>
    /// <remarks>
    /// This is the case that cost a death. The mirror's HP lags — the game writes it and never
    /// reads it (§98, §99) — so on a poll where the authoritative structure had one slot the
    /// reader would not accept, the whole read fell back to the mirror and a Pokémon that had just
    /// fainted still showed its old HP. The watcher decides on <c>CurrentHp == 0</c> and saw
    /// nothing; the player healed it.
    /// </remarks>
    [Fact]
    public void The_authoritative_structure_beats_a_mirror_that_reads_more()
    {
        var chosen = PartyLayoutLocator.Preferred(
        [
            (Mirror(0x2000), 6),
            (Authoritative(0x1000), 5)
        ]);

        Assert.Equal(0x1000u, chosen!.Address);
    }

    /// <summary>Among structures of the same kind, the one that reads most of the party wins.</summary>
    [Fact]
    public void Between_two_of_the_same_kind_the_fuller_read_wins()
    {
        var chosen = PartyLayoutLocator.Preferred(
        [
            (Authoritative(0x1000), 4),
            (Authoritative(0x3000), 6)
        ]);

        Assert.Equal(0x3000u, chosen!.Address);
    }

    /// <summary>
    /// A mirror is still better than nothing: it is only rejected when there is a truthful one.
    /// </summary>
    [Fact]
    public void A_mirror_is_used_when_it_is_all_there_is()
    {
        var chosen = PartyLayoutLocator.Preferred([(Mirror(0x2000), 6)]);

        Assert.Equal(0x2000u, chosen!.Address);
    }

    /// <summary>A structure that read nobody is not a candidate, whatever its kind.</summary>
    [Fact]
    public void A_structure_that_read_nobody_is_not_a_candidate()
    {
        var chosen = PartyLayoutLocator.Preferred(
        [
            (Authoritative(0x1000), 0),
            (Mirror(0x2000), 3)
        ]);

        Assert.Equal(0x2000u, chosen!.Address);
        Assert.Null(PartyLayoutLocator.Preferred([(Authoritative(0x1000), 0)]));
    }

    /// <summary>
    /// Connecting through last session's addresses and landing on the mirror means the structure
    /// the game reads has moved: the list has to be located again.
    /// </summary>
    /// <remarks>
    /// §135: an eight-day-old list kept being accepted because the mirror, which never moves, read
    /// fine. The party was read from the lagging mirror and the fallen were «put down» at addresses
    /// where they no longer were.
    /// </remarks>
    [Fact]
    public void Landing_on_the_mirror_from_remembered_addresses_asks_for_a_sweep()
    {
        var chosen = PartyLayoutLocator.Preferred(
        [
            (Authoritative(0x1000), 0),
            (Mirror(0x2000), 6)
        ]);

        Assert.True(PartyLayoutLocator.NeedsLocatingAgain(chosen!));
        Assert.False(PartyLayoutLocator.NeedsLocatingAgain(Authoritative(0x1000)));
    }
}
