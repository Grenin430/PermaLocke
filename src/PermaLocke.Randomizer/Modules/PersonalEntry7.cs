namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Byte-level view of one entry of the personal table (<c>a/0/1/7</c>): base stats, types and
/// abilities of a species or of one of its forms.
/// <para>
/// The GARC holds 977 subfiles: 976 individual entries and, as the last one, the whole table
/// concatenated. pk3DS reads that packed copy. PermaLocke writes <em>both</em>, so the two can
/// never disagree about what a species is.
/// </para>
/// </summary>
public static class PersonalEntry7
{
    /// <summary>Bytes per entry in Sun/Moon and Ultra Sun/Moon.</summary>
    public const int Size = 0x54;

    /// <summary>Base stats, in the order the cartridge stores them.</summary>
    public static readonly int[] StatOffsets = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05];

    /// <summary>Experience curve: 0 medium fast, 1 erratic, 2 fluctuating, 3 medium slow,
    /// 4 fast, 5 slow. Anchored on the real cartridge -- ver InstalledWorld.</summary>
    private const int GrowthOffset = 0x15;

    private const int Type1Offset = 0x06;
    private const int Type2Offset = 0x07;

    /// <summary>Ability 1, ability 2 and the hidden ability.</summary>
    public static readonly int[] AbilityOffsets = [0x18, 0x19, 0x1A];

    public static int GetStat(byte[] entry, int at, int stat) => entry[at + StatOffsets[stat]];

    public static void SetStat(byte[] entry, int at, int stat, int value) =>
        entry[at + StatOffsets[stat]] = (byte)Math.Clamp(value, 1, 255);

    public static int BaseStatTotal(byte[] entry, int at) =>
        StatOffsets.Sum(offset => entry[at + offset]);

    public static (int First, int Second) GetTypes(byte[] entry, int at) =>
        (entry[at + Type1Offset], entry[at + Type2Offset]);

    public static void SetTypes(byte[] entry, int at, int first, int second)
    {
        entry[at + Type1Offset] = (byte)first;
        entry[at + Type2Offset] = (byte)second;
    }

    /// <summary>A species with one type stores the same value in both slots.</summary>
    public static bool IsMonoType(byte[] entry, int at)
    {
        var (first, second) = GetTypes(entry, at);
        return first == second;
    }

    public static int GetAbility(byte[] entry, int at, int slot) => entry[at + AbilityOffsets[slot]];

    public static void SetAbility(byte[] entry, int at, int slot, int ability) =>
        entry[at + AbilityOffsets[slot]] = (byte)ability;

    /// <summary>
    /// Rearranges the six base stats without changing their total, so a species keeps the league
    /// it was in. That matters: the encounter and trainer modules match replacements by base
    /// stat total, and a randomized total would make those comparisons meaningless.
    /// </summary>
    public static void ShuffleStats(byte[] entry, int at, Func<int, int> nextBelow)
    {
        var stats = StatOffsets.Select(offset => entry[at + offset]).ToArray();
        for (var i = stats.Length - 1; i > 0; i--)
        {
            var j = nextBelow(i + 1);
            (stats[i], stats[j]) = (stats[j], stats[i]);
        }
        for (var i = 0; i < stats.Length; i++)
        {
            entry[at + StatOffsets[i]] = stats[i];
        }
    }

    /// <summary>Where a species' alternate forms begin, as an entry index. Zero means it has none.</summary>
    public const int FormStatsIndexOffset = 0x1C;

    public static int GetFormStatsIndex(byte[] entry, int at) =>
        BitConverter.ToUInt16(entry, at + FormStatsIndexOffset);

    /// <summary>
    /// How many species the table describes, read from the table itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is laid out as every species first and every alternate form after them, so the
    /// <b>lowest form index any species points at</b> is the first entry that is not a species —
    /// and one less than that is the last species. Nothing else in the file states the count.
    /// </para>
    /// <para>
    /// It is derived rather than declared because pk3DS answers this question with a <b>constant</b>
    /// (<c>MaxSpeciesID_7_USUM = 807</c>) that does not look at the loaded files at all. On the
    /// cartridge that constant is right; on a mod that adds Pokémon it is silently wrong, and
    /// wrong in the §68 direction — everything past 807 would simply never be picked, with nothing
    /// failing to show for it.
    /// </para>
    /// <para>
    /// Verified against both worlds: the cartridge's first form index is 808, giving 807, which is
    /// the known answer; the gen 8-9 expansion's is 1026, giving 1025, which is the known last
    /// species of the ninth generation. A derivation that reproduces two independently known
    /// numbers is a measurement and not a guess.
    /// </para>
    /// </remarks>
    /// <param name="packed">The concatenated table, which is the GARC's last subfile.</param>
    /// <summary>Experience curve, one byte per species, indexed by species id.</summary>
    /// <remarks>
    /// A level is not stored anywhere: it is computed from experience, and which curve to use is a
    /// property of the species. PKHeX's gen 7 table stops at 807, so for everything the expansion
    /// mod adds it silently falls back to Medium Fast — which is not a missing value, it is a wrong
    /// level. Index 0 is padding so the array can be indexed by species id directly.
    /// </remarks>
    public static byte[] GrowthRates(byte[] packed, int speciesCount)
    {
        var rates = new byte[speciesCount + 1];

        for (var species = 1; species <= speciesCount; species++)
        {
            rates[species] = packed[(species * Size) + GrowthOffset];
        }

        return rates;
    }

    public static int SpeciesCount(byte[] packed)
    {
        var rows = packed.Length / Size;
        var first = 0;

        for (var row = 0; row < rows; row++)
        {
            var index = GetFormStatsIndex(packed, row * Size);

            if (index > 0 && (first == 0 || index < first))
            {
                first = index;
            }
        }

        // A table where nothing declares a form is not a table this code understands, and guessing
        // the row count instead would hand back a number that mixes species with forms.
        return first > 0
            ? first - 1
            : throw new InvalidDataException(
                "La tabla de datos de especie no declara ninguna forma, así que no se puede deducir "
                + "cuántas especies tiene. Ver PersonalEntry7.SpeciesCount.");
    }
}
