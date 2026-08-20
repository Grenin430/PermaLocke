using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The species → icon table.
/// </summary>
/// <remarks>
/// It was built by looking at the container icon by icon, so what needs guarding is that nobody
/// edits the adjustment lists without redoing that work. These tests pin the checks that would
/// catch it: the ordered block has to add up exactly, and a handful of icons verified by eye
/// have to keep landing where they were seen.
/// </remarks>
public class PokemonIconIndexTests
{
    [Fact]
    public void The_table_stops_where_the_container_stops_following_the_dex()
    {
        Assert.Equal(649, PokemonIconIndex.LastKnownSpecies);
        Assert.Equal(866, PokemonIconIndex.OrderedBlockIcons);
        Assert.Equal(0, PokemonIconIndex.EggIcon);
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
    public void Icons_verified_by_eye_stay_where_they_were_seen(int species, int icon)
    {
        var table = BuildWithRealCartridgeCounts();
        Assert.Equal(icon, table[species]);
    }

    [Fact]
    public void Every_species_up_to_the_cut_has_an_icon_and_no_more()
    {
        var table = BuildWithRealCartridgeCounts();

        Assert.Equal(PokemonIconIndex.LastKnownSpecies, table.Count);
        Assert.All(table.Values, icon => Assert.InRange(icon, 1, PokemonIconIndex.OrderedBlockIcons));
        Assert.DoesNotContain(650, table.Keys);
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
        PokemonIconIndex.Build(species => CartridgeFormCounts.Value[species]);

    /// <summary>
    /// The ninety species of 1-649 that declare more than one form, exactly as pk3DS reads them
    /// out of Ultra Moon. Dumped from the cartridge once; everything else declares one.
    /// </summary>
    private static readonly (int Species, int Forms)[] ManyFormed =
    [
        (3, 2), (6, 3), (9, 2), (15, 2), (18, 2), (19, 2), (20, 3), (25, 8), (26, 2), (27, 2),
        (28, 2), (37, 2), (38, 2), (50, 2), (51, 2), (52, 2), (53, 2), (65, 2), (74, 2), (75, 2),
        (76, 2), (80, 2), (88, 2), (89, 2), (94, 2), (103, 2), (105, 3), (115, 2), (127, 2),
        (130, 2), (142, 2), (150, 3), (181, 2), (201, 28), (208, 2), (212, 2), (214, 2), (229, 2),
        (248, 2), (254, 2), (257, 2), (260, 2), (282, 2), (302, 2), (303, 2), (306, 2), (308, 2),
        (310, 2), (319, 2), (323, 2), (334, 2), (351, 4), (354, 2), (359, 2), (362, 2), (373, 2),
        (376, 2), (380, 2), (381, 2), (382, 2), (383, 2), (384, 2), (386, 4), (412, 3), (413, 3),
        (414, 3), (421, 2), (422, 2), (423, 2), (428, 2), (445, 2), (448, 2), (460, 2), (475, 2),
        (479, 6), (487, 2), (492, 2), (493, 18), (531, 2), (550, 2), (555, 2), (585, 4), (586, 4),
        (641, 2), (642, 2), (645, 2), (646, 3), (647, 2), (648, 2), (649, 5),
    ];

    private static readonly Lazy<int[]> CartridgeFormCounts = new(() =>
    {
        var counts = new int[650];
        Array.Fill(counts, 1);
        foreach (var (species, forms) in ManyFormed)
        {
            counts[species] = forms;
        }
        return counts;
    });
}
