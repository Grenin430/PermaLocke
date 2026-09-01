namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// How many forms each species of Ultra Moon declares, so the icon table can be built in a unit
/// test without a 3,7 GB ROM on disk.
/// </summary>
/// <remarks>
/// Dumped from the cartridge once, exactly as pk3DS reads it. Shared by every test that builds the
/// table: two copies of this list would drift, and a drifted copy would make one of the two suites
/// pass for the wrong reason.
/// </remarks>
internal static class CartridgeFormCounts
{
    /// <summary>The ninety species of 1-649 that declare more than one form. The rest declare one.</summary>
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

    private static readonly Lazy<int[]> Counts = new(() =>
    {
        var counts = new int[650];
        Array.Fill(counts, 1);

        foreach (var (species, forms) in ManyFormed)
        {
            counts[species] = forms;
        }

        return counts;
    });

    public static int Of(int species) => Counts.Value[species];
}
