using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Reading the three starters back out of a mod.
/// </summary>
/// <remarks>
/// What is guarded here is the empty answer. The panel this feeds exists so nobody has to pick a
/// starter blind and reload a save when it disappoints, and three names that turn out to belong to
/// a mod the player is not running would send them back to that save for a Pokemon they never had.
/// So: no mod, no list.
/// <para>
/// That entries 0-2 really are the starters is anchored against the cartridge instead, in
/// <see cref="StaticEncounterTable.Gifts"/>: vanilla holds 722, 725 and 728 there.
/// </para>
/// </remarks>
public sealed class StarterReaderTests
{
    [Fact]
    public void A_folder_that_is_not_a_mod_yields_nothing()
    {
        var empty = Directory.CreateTempSubdirectory("permalocke-starters");

        try
        {
            Assert.Empty(StarterReader.Read(empty.FullName));
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_folder_that_does_not_exist_yields_nothing()
    {
        Assert.Empty(StarterReader.Read(
            Path.Combine(Path.GetTempPath(), $"permalocke-no-such-{Guid.NewGuid():N}")));
    }
}
