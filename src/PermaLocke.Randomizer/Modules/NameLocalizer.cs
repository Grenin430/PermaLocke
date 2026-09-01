namespace PermaLocke.Randomizer.Modules;

/// <summary>What extending one list of names did.</summary>
/// <param name="Kept">Entries the game already had, left exactly as they were.</param>
/// <param name="Translated">Entries added with an official name.</param>
/// <param name="Borrowed">Entries added with the mod's own text, for want of an official name.</param>
public sealed record LocalizedNames(string[] Lines, int Kept, int Translated, int Borrowed)
{
    public int Added => Translated + Borrowed;
}

/// <summary>
/// Puts the names a mod only ships in English into the language the player is using.
/// </summary>
/// <remarks>
/// <para>
/// This is not a translation. The Spanish names of species, moves, abilities and items are
/// <b>official</b> and PKHeX carries them, so the job is copying them into the right slot. What
/// this class does is decide which slots, and it deliberately does not know where the names come
/// from: the caller passes them in, which keeps the randomizer free of a PKHeX dependency and makes
/// the merge testable without either library.
/// </para>
/// <para>
/// <b>Existing entries are never touched</b>, and that is the important decision. It came out of
/// the anchor measurement: the cartridge's own Spanish names and PKHeX's agree on 805 of 808
/// species — enough to prove the indices line up — but only 659 of 729 moves and 738 of 960 items.
/// The differences are not errors. The cartridge abbreviates to fit its own boxes ("Picotazo Ven",
/// "Protec. Especial") and uses the translations of its generation ("Golpe" where the modern name
/// is "Saña"). Overwriting with the newer list would rename half the game underneath a player who
/// knows it, and could push text out of the UI. So the merge only ever appends.
/// </para>
/// <para>
/// Where no official name exists for a new entry, the mod's English is copied rather than leaving a
/// blank. A blank is not neutral here: these lists are indexed by id, so a missing entry is either
/// an empty label on screen or a read past the end of the array. An English name is at least true.
/// </para>
/// </remarks>
public static class NameLocalizer
{
    /// <summary>
    /// Extends <paramref name="current"/> to the length <paramref name="english"/> has.
    /// </summary>
    /// <param name="current">The language's list as the cartridge ships it.</param>
    /// <param name="english">The mod's list, which is the one that says how long the answer is.</param>
    /// <param name="official">
    /// The official names of that language, indexed the same way. May be shorter than
    /// <paramref name="english"/>, and then the rest is borrowed from it.
    /// </param>
    public static LocalizedNames Extend(string[] current, string[] english,
        IReadOnlyList<string> official)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(english);
        ArgumentNullException.ThrowIfNull(official);

        // Un mod que ACORTA una lista no es un caso que este codigo entienda, y rellenar hasta la
        // longitud vieja inventaria entradas. Se queda como esta y se dice.
        if (english.Length <= current.Length)
        {
            return new LocalizedNames([.. current], current.Length, 0, 0);
        }

        var lines = new string[english.Length];
        Array.Copy(current, lines, current.Length);

        var translated = 0;
        var borrowed = 0;

        for (var i = current.Length; i < english.Length; i++)
        {
            var name = i < official.Count ? official[i] : string.Empty;

            if (name.Length > 0)
            {
                lines[i] = name;
                translated++;
            }
            else
            {
                lines[i] = english[i];
                borrowed++;
            }
        }

        return new LocalizedNames(lines, current.Length, translated, borrowed);
    }

    /// <summary>
    /// How well the official list lines up with what the game already has, over the range both
    /// cover.
    /// </summary>
    /// <remarks>
    /// The gate for the whole operation. These lists are addressed <b>by index</b>, so if the
    /// official one were offset by even one, every new Pokémon would be given its neighbour's name
    /// and nothing would fail to say so. Agreement over the existing range is what proves the
    /// alignment; it is not expected to be total, because of the abbreviations described above.
    /// </remarks>
    /// <returns>How many of the compared entries are identical, and how many were compared.</returns>
    public static (int Same, int Compared) Alignment(string[] current, IReadOnlyList<string> official)
    {
        var compared = Math.Min(current.Length, official.Count);
        var same = 0;

        for (var i = 0; i < compared; i++)
        {
            if (current[i] == official[i])
            {
                same++;
            }
        }

        return (same, compared);
    }
}
