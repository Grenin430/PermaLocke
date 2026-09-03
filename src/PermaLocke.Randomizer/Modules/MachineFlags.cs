namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Which TMs a species can be taught: one bit each, inside its personal entry.
/// </summary>
/// <remarks>
/// <para>
/// Measured, not remembered. Mew's entry reads <c>FF FF FF FF FF FF FF FF FF FF FF FF 0F</c> from
/// <c>0x28</c> — twelve full bytes and four more bits, which is <b>exactly one hundred</b>, the
/// number of TMs the game has. Ditto's is all zeroes there, and Ditto learns no TM at all.
/// </para>
/// <para>
/// The four spare bits at the top of the last byte are zero in every species, and that is what
/// pins the length: a window one byte longer would still look plausible on Mew alone.
/// </para>
/// <para>
/// The same entry carries the tutors' bits right after, at <c>0x3C</c>: eight full bytes and three
/// more, <b>sixty-seven</b> — the same number as the tutor list found in <c>code.bin</c>. Two
/// independent structures agreeing on a count is worth more than either of them alone.
/// </para>
/// </remarks>
public static class MachineFlags
{
    public const int Offset = 0x28;

    /// <summary>One hundred TMs, so thirteen bytes with four bits spare.</summary>
    public const int Count = 100;

    public const int Length = 13;

    /// <summary>Where the tutors' own bits start, and how many. Not written, only documented.</summary>
    public const int TutorOffset = 0x3C;

    public const int TutorCount = 67;

    /// <summary>Reads a species' hundred flags out of the packed table.</summary>
    public static bool[] Read(ReadOnlySpan<byte> packed, int species)
    {
        var at = (species * PersonalEntry7.Size) + Offset;
        var flags = new bool[Count];

        for (var i = 0; i < Count; i++)
        {
            flags[i] = (packed[at + (i / 8)] & (1 << (i % 8))) != 0;
        }

        return flags;
    }

    /// <summary>
    /// Writes them back, leaving the four spare bits of the last byte exactly as they were.
    /// </summary>
    /// <remarks>
    /// Cleared rather than preserved would look identical on this cartridge, where they are already
    /// zero — and would quietly corrupt a world where they are not. Nothing is known about them, so
    /// nothing is done to them.
    /// </remarks>
    public static void Write(Span<byte> packed, int species, IReadOnlyList<bool> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (flags.Count != Count)
        {
            throw new ArgumentException($"Son {Count} MT y se han dado {flags.Count}.", nameof(flags));
        }

        var at = (species * PersonalEntry7.Size) + Offset;

        for (var i = 0; i < Count; i++)
        {
            var mask = (byte)(1 << (i % 8));

            if (flags[i])
            {
                packed[at + (i / 8)] |= mask;
            }
            else
            {
                packed[at + (i / 8)] &= (byte)~mask;
            }
        }
    }
}
