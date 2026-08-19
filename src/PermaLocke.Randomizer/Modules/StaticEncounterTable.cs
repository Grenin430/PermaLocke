namespace PermaLocke.Randomizer.Modules;

/// <param name="Subfile">Index inside the encounterstatic GARC.</param>
/// <param name="Stride">Bytes per entry.</param>
/// <param name="SpeciesOffset">Offset of the u16 species within an entry.</param>
/// <param name="FormOffset">Offset of the form byte within an entry.</param>
/// <param name="Name">What to call this table in a report shown to the player.</param>
public sealed record EncounterEntryLayout(int Subfile, int Stride, int SpeciesOffset, int FormOffset, string Name);

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

    public static readonly EncounterEntryLayout Statics = new(1, 0x38, 0x00, 0x02, "estáticos");

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
}
