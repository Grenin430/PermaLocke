using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The species → icon table.
/// </summary>
/// <remarks>
/// It was built by looking at the container icon by icon, so what needs guarding is that nobody
/// edits the adjustment lists without redoing that work. These tests pin the checks that would
/// catch it: both blocks have to add up exactly, and a handful of icons verified by eye
/// have to keep landing where they were seen.
/// </remarks>
public class PokemonIconIndexTests
{
    [Fact]
    public void The_container_is_two_blocks_and_the_table_knows_where_each_one_ends()
    {
        Assert.Equal(649, PokemonIconIndex.OrderedBlockSpecies);
        Assert.Equal(866, PokemonIconIndex.OrderedBlockIcons);
        Assert.Equal(0, PokemonIconIndex.EggIcon);
        Assert.Equal(807, PokemonIconIndex.LastSpecies);
        Assert.Equal(1154, PokemonIconIndex.ContainerIcons);
    }

    /// <summary>
    /// Real numbers, checked against the decoded icons: Bulbasaur opens the container, Charmander
    /// sits after Mega Venusaur, Pikachu takes ten slots and Raichu's ordinary form is the second
    /// of its pair because the Alolan one comes first.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]      // Bulbasaur
    [InlineData(4, 5)]      // Charmander, after Venusaur's mega
    [InlineData(19, 26)]    // Rattata: the Alolan icon is 25
    [InlineData(25, 34)]    // Pikachu
    [InlineData(26, 45)]    // Raichu: the Alolan icon is 44
    [InlineData(51, 81)]    // Dugtrio: two Alolan icons before the plain one
    [InlineData(89, 131)]   // Muk: same
    [InlineData(103, 148)]  // Exeggutor
    [InlineData(120, 168)]  // Staryu
    [InlineData(150, 201)]  // Mewtwo
    [InlineData(201, 259)]  // Unown
    [InlineData(493, 664)]  // Arceus, whose eighteen forms share one icon
    [InlineData(649, 862)]  // Genesect, the last one in the ordered block
    [InlineData(650, 929)]  // Chespin, first of the second block
    [InlineData(676, 872)]  // Furfrou untrimmed, sixth of its eleven icons
    [InlineData(777, 1110)] // Togedemaru, the one that had been read as Jangmo-o
    [InlineData(782, 1024)] // Jangmo-o
    [InlineData(671, 920)]  // Florges
    [InlineData(700, 987)]  // Sylveon
    [InlineData(778, 1028)] // Mimikyu
    public void Icons_verified_by_eye_stay_where_they_were_seen(int species, int icon)
    {
        var table = BuildWithRealCartridgeCounts();
        Assert.Equal(icon, table[species]);
    }

    [Fact]
    public void The_ordered_block_covers_every_species_up_to_the_seam()
    {
        var table = BuildWithRealCartridgeCounts();
        var ordered = table.Where(pair => pair.Key <= PokemonIconIndex.OrderedBlockSpecies).ToList();

        Assert.Equal(PokemonIconIndex.OrderedBlockSpecies, ordered.Count);
        Assert.All(ordered, pair => Assert.InRange(pair.Value, 1, PokemonIconIndex.OrderedBlockIcons));
    }

    /// <summary>
    /// The second block was identified icon by icon, so the guard is arithmetic: all 158 species
    /// present, each pointing at its own picture, and every picture inside the block. An entry
    /// landing in the ordered block would be a typo aiming at somebody else's Pokemon, and
    /// nothing at runtime would notice.
    /// </summary>
    [Fact]
    public void The_second_block_covers_the_rest_and_uses_up_its_icons_exactly()
    {
        var table = BuildWithRealCartridgeCounts();
        var second = table.Where(pair => pair.Key > PokemonIconIndex.OrderedBlockSpecies).ToList();

        var expected = PokemonIconIndex.LastSpecies - PokemonIconIndex.OrderedBlockSpecies;
        Assert.Equal(expected, second.Count);
        Assert.Equal(expected, second.Select(pair => pair.Value).Distinct().Count());
        Assert.All(second, pair =>
            Assert.InRange(pair.Key, PokemonIconIndex.OrderedBlockSpecies + 1, PokemonIconIndex.LastSpecies));
        Assert.All(second, pair =>
            Assert.InRange(pair.Value, PokemonIconIndex.OrderedBlockIcons + 1, PokemonIconIndex.ContainerIcons - 1));
    }

    /// <summary>Every species the cartridge draws now has an icon: there is no gap left.</summary>
    [Fact]
    public void No_species_is_left_without_a_picture()
    {
        var table = BuildWithRealCartridgeCounts();

        Assert.Equal(PokemonIconIndex.LastSpecies, table.Count);
        for (var species = 1; species <= PokemonIconIndex.LastSpecies; species++)
        {
            Assert.True(table.ContainsKey(species), $"la especie {species} se ha quedado sin icono");
        }
    }

    /// <summary>Two species never share an icon: that would mean the table drifted.</summary>
    [Fact]
    public void No_two_species_point_at_the_same_icon()
    {
        var table = BuildWithRealCartridgeCounts();
        Assert.Equal(table.Count, table.Values.Distinct().Count());
    }

    /// <summary>
    /// Builds the table with the cartridge's own form counts, kept here so the suite does not
    /// need a 3,7 GB ROM to run.
    /// </summary>
    private static IReadOnlyDictionary<int, int> BuildWithRealCartridgeCounts() =>
        PokemonIconIndex.Build(CartridgeFormCounts.Of);

}
