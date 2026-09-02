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
    /// <summary>
    /// Starters are entries 0-2 and the eleven fossils are entries 3-13.
    /// <para>
    /// Measured, not assumed: read straight out of the vanilla cartridge, entries 0, 1 and 2 hold
    /// 722, 725 and 728 — Rowlet, Litten and Popplio, in the order the game offers them. The check
    /// is <c>RomTool iniciales &lt;carpeta&gt;</c> against an unpatched <c>a/1/5/9</c>.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Writes the species <b>and</b> a form, for the one case where the form is the point.
    /// </summary>
    /// <remarks>
    /// A mega is a form of its species and not a species of its own, so replacing a boss with «a
    /// random mega» is species plus form and nothing else. Kept apart from the ordinary setter
    /// because that one clears the form on purpose, and it must go on doing so: a form index that
    /// was valid for the old species is not necessarily valid for the new one.
    /// </remarks>
    public static void SetSpecies(byte[] payload, EncounterEntryLayout layout, int index,
        int species, int form)
    {
        var at = index * layout.Stride;
        BitConverter.GetBytes((ushort)species).CopyTo(payload, at + layout.SpeciesOffset);
        payload[at + layout.FormOffset] = (byte)form;
    }

    /// <summary>
    /// Byte that says what kind of encounter an entry is. Two means a <b>Totem</b>.
    /// </summary>
    /// <remarks>
    /// Found by diffing the known Totems against their own SOS allies: the eight trial bosses all
    /// carry 2 here and every ally carries 0. Confirmed by a second, independent mark — the three
    /// bytes at 0x21 read <c>FF-99-19</c> on a Totem and zero on everything else, which is the aura
    /// that boosts its stats.
    /// <para>
    /// Enumerating by this beats any list of species: there are <b>fourteen</b> Totems, not the
    /// eight of a single playthrough, because each version has its own for three of the trials and
    /// two more exist as level 60 rematches. A hand-written list had already missed six of them.
    /// </para>
    /// </remarks>
    public const int KindOffset = 0x07;

    /// <summary>Value of <see cref="KindOffset"/> that marks a Totem.</summary>
    public const int TotemKind = 2;

    /// <summary>First of the three aura bytes, the second and independent mark of a Totem.</summary>
    public const int AuraOffset = 0x21;

    /// <summary>
    /// True when this entry is a Totem, by both marks at once.
    /// </summary>
    /// <remarks>
    /// Both, because they disagree on exactly one entry: Tapu Koko carries the kind byte of a Totem
    /// and none of the aura. Whatever that means, it is not a Totem, and asking for both keeps it
    /// out without anyone having to name it.
    /// </remarks>
    public static bool IsTotem(byte[] payload, EncounterEntryLayout layout, int index)
    {
        if (layout.LevelOffset is null)
        {
            return false;
        }

        var at = index * layout.Stride;
        return payload[at + KindOffset] == TotemKind
               && payload[at + AuraOffset] == 0xFF
               && payload[at + AuraOffset + 1] == 0x99;
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
