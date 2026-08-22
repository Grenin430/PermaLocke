namespace PermaLocke.Randomizer.Modules;

/// <param name="Subfile">Index inside the encounterstatic GARC.</param>
/// <param name="Stride">Bytes per entry.</param>
/// <param name="SpeciesOffset">Offset of the u16 species within an entry.</param>
/// <param name="FormOffset">Offset of the form byte within an entry.</param>
/// <param name="Name">What to call this table in a report shown to the player.</param>
/// <param name="LevelOffset">
/// Where the level lives inside an entry, or null when this table does not carry one.
/// <para>
/// Only the statics have it, at 0x03. It was found by looking for a byte that is always between
/// 1 and 100 and varies, and confirmed against things whose level is known without doubt:
/// Solgaleo and Lunala at 60, Totem Gumshoos at 12, Totem Wishiwashi at 20, Totem Salazzle at 22.
/// </para>
/// </param>
public sealed record EncounterEntryLayout(
    int Subfile, int Stride, int SpeciesOffset, int FormOffset, string Name, int? LevelOffset = null);

/// <summary>
/// Byte-level view of the fixed-size tables in <c>a/1/5/9</c>: starters, the eleven fossils,
/// gifts, static encounters, totems and in-game trades.
/// <para>
/// The six subfiles are uncompressed, so these tables are patched truly in place: the GARC
/// container is never repacked, which removes the one risk that broke the first attempt at
/// wild encounters.
/// </para>
/// </summary>
public static class StaticEncounterTable
{
    /// <summary>Starters are entries 0-2 and the eleven fossils are entries 3-13.</summary>
    public static readonly EncounterEntryLayout Gifts = new(0, 0x14, 0x00, 0x02, "regalos");

    public static readonly EncounterEntryLayout Statics = new(1, 0x38, 0x00, 0x02, "estáticos", 0x03);

    /// <summary>
    /// A trade holds two species: what you receive at 0x0 and what you must hand over at 0x2.
    /// Only the first is randomized; changing the requirement would make a trade impossible.
    /// </summary>
    public static readonly EncounterEntryLayout Trades = new(4, 0x34, 0x00, 0x04, "intercambios");

    /// <summary>Entries in the first three gift slots: the starter choice.</summary>
    public const int StarterCount = 3;

    public static int Count(byte[] payload, EncounterEntryLayout layout) => payload.Length / layout.Stride;

    public static int GetSpecies(byte[] payload, EncounterEntryLayout layout, int index) =>
        BitConverter.ToUInt16(payload, (index * layout.Stride) + layout.SpeciesOffset);

    public static int GetForm(byte[] payload, EncounterEntryLayout layout, int index) =>
        payload[(index * layout.Stride) + layout.FormOffset];

    /// <summary>
    /// Writes the species and clears the form. A form index that was valid for the old species
    /// is not necessarily valid for the new one, and form 0 always is.
    /// </summary>
    public static void SetSpecies(byte[] payload, EncounterEntryLayout layout, int index, int species)
    {
        var at = index * layout.Stride;
        BitConverter.GetBytes((ushort)species).CopyTo(payload, at + layout.SpeciesOffset);
        payload[at + layout.FormOffset] = 0;
    }

    /// <summary>The level of an entry, or zero when the table does not carry one.</summary>
    public static int GetLevel(byte[] payload, EncounterEntryLayout layout, int index) =>
        layout.LevelOffset is { } offset ? payload[(index * layout.Stride) + offset] : 0;

    /// <summary>
    /// Sets the level, clamped to what the game can hold. Does nothing where there is no level.
    /// </summary>
    public static void SetLevel(byte[] payload, EncounterEntryLayout layout, int index, int level)
    {
        if (layout.LevelOffset is not { } offset)
        {
            return;
        }

        payload[(index * layout.Stride) + offset] = (byte)Math.Clamp(level, 1, 100);
    }
}
