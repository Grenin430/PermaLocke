using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Slot">Which of the three the game will offer it as, counting from one.</param>
/// <param name="Species">The national dex number sitting in that gift entry.</param>
public sealed record StarterChoice(int Slot, int Species, int Form);

/// <summary>
/// Reads back the three starters from a mod folder, so they can be shown at the moment the game
/// asks the player to choose.
/// </summary>
/// <remarks>
/// <para>
/// This is a reader and nothing else: it opens the mod's own <c>a/1/5/9</c>, which is the file the
/// emulator loads, and reports what is in it. It does not repeat the roll, and it does not read
/// the randomizer's report — a report is what PermaLocke <em>said</em> it did, and the point here
/// is what the game will actually offer.
/// </para>
/// <para>
/// A randomized run makes choosing a starter a guess: the three eggs look the same, so the usual
/// way round it is to pick one, look at it, and reload a save if it disappoints. Naming them costs
/// the game nothing it was keeping secret on purpose.
/// </para>
/// </remarks>
public static class StarterReader
{
    /// <summary>
    /// The starters of the mod rooted at <paramref name="modRoot"/>, or empty when there is no
    /// such mod. Empty rather than a guess: a wrong list here would send somebody back to a save
    /// for a Pokemon they never had.
    /// </summary>
    public static IReadOnlyList<StarterChoice> Read(string modRoot)
    {
        var path = Path.Combine(modRoot, "romfs",
            GameFiles.EncounterStatic.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            return [];
        }

        var gifts = GarcPatcher.ReadOnly(path, StaticEncounterTable.Gifts.Subfile);
        var layout = StaticEncounterTable.Gifts;
        var available = StaticEncounterTable.Count(gifts, layout);

        var starters = new List<StarterChoice>();

        for (var i = 0; i < Math.Min(StaticEncounterTable.StarterCount, available); i++)
        {
            var species = StaticEncounterTable.GetSpecies(gifts, layout, i);

            if (species <= 0)
            {
                continue;
            }

            starters.Add(new StarterChoice(i + 1, species, StaticEncounterTable.GetForm(gifts, layout, i)));
        }

        return starters;
    }
}
