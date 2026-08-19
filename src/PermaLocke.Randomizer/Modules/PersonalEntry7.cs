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
}
