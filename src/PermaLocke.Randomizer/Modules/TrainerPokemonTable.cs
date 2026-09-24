namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// Byte-level view of one trainer's party inside <c>trpoke</c> (<c>a/1/0/7</c>): a flat run of
/// fixed-size entries, one per Pokémon, one subfile per trainer.
/// <para>
/// Both trainer GARCs are uncompressed, so parties are patched in place and the containers are
/// never repacked.
/// </para>
/// </summary>
public static class TrainerPokemonTable
{
    /// <summary>Bytes per Pokémon. Fixed in gen 7, unlike the variable entries of gen 6.</summary>
    public const int EntrySize = 0x20;

    /// <summary>
    /// The six EVs, in the order PS, Ataque, Defensa, At. Esp., Def. Esp., Velocidad.
    /// </summary>
    /// <remarks>
    /// Nothing here writes them, and there is a <see cref="GetEvs"/> and no setter on purpose: the
    /// enemy's training is the cartridge's, and the verification pass reads these to say so with a
    /// number instead of with a promise. The layout is pk3DS's <c>TrainerPoke7</c>, where 0x00 is
    /// gender and ability, 0x01 the nature, 0x02-0x07 the EVs and 0x08 the packed IVs.
    /// </remarks>
    private const int EvOffset = 0x02;

    /// <summary>Stats a Pokémon has, which is how many EV bytes each entry carries.</summary>
    public const int EvCount = 6;

    private const int LevelOffset = 0x0E;
    private const int SpeciesOffset = 0x10;
    private const int FormOffset = 0x12;
    private const int ItemOffset = 0x14;
    private const int MovesOffset = 0x18;
    private const int MoveCount = 4;

    public static ReadOnlySpan<byte> GetEvs(byte[] party, int index) =>
        party.AsSpan((index * EntrySize) + EvOffset, EvCount);

    /// <summary>
    /// Writes the six EVs, in the same order as <see cref="GetEvs"/>.
    /// </summary>
    /// <remarks>
    /// Added for the difficulty module, which deals a spread again for the species the trainer module
    /// put there (§122). The order was measured before this was written rather than taken from the
    /// layout comment: among the cartridge's 252s, byte 1 sits on species averaging 103 base Attack,
    /// byte 3 on 101 Special Attack and byte 5 on 90 Speed.
    /// </remarks>
    public static void SetEvs(byte[] party, int index, ReadOnlySpan<byte> evs) =>
        evs[..EvCount].CopyTo(party.AsSpan((index * EntrySize) + EvOffset, EvCount));

    /// <summary>The packed IV field: six five-bit values from bit 0, and two flag bits above them.</summary>
    private const int IvOffset = 0x08;

    private const int IvBits = 5;

    /// <summary>One of the six IVs, by its position in the packed field.</summary>
    public static int GetIv(byte[] party, int index, int stat) =>
        (int)((BitConverter.ToUInt32(party, (index * EntrySize) + IvOffset) >> (stat * IvBits)) & 0x1F);

    /// <summary>
    /// Writes one IV and nothing else: the two bits above the sixth IV are flags (pk3DS reads bit 30
    /// as shiny) and are carried through untouched.
    /// </summary>
    public static void SetIv(byte[] party, int index, int stat, int value)
    {
        var at = (index * EntrySize) + IvOffset;
        var packed = BitConverter.ToUInt32(party, at);
        var mask = 0x1Fu << (stat * IvBits);
        packed = (packed & ~mask) | ((uint)Math.Clamp(value, 0, 31) << (stat * IvBits));
        BitConverter.GetBytes(packed).CopyTo(party, at);
    }

    public static void SetItem(byte[] party, int index, int item) =>
        BitConverter.GetBytes((ushort)item).CopyTo(party, (index * EntrySize) + ItemOffset);

    /// <summary>
    /// How many Pokémon the party holds. One subfile in the cartridge is six bytes long and
    /// therefore holds none; truncating rather than throwing is deliberate.
    /// </summary>
    public static int Count(byte[] party) => party.Length / EntrySize;

    public static int GetSpecies(byte[] party, int index) =>
        BitConverter.ToUInt16(party, (index * EntrySize) + SpeciesOffset);

    public static int GetLevel(byte[] party, int index) => party[(index * EntrySize) + LevelOffset];

    /// <summary>
    /// Sets a trainer's level, clamped to what the game can hold.
    /// </summary>
    /// <remarks>
    /// The randomization of species deliberately leaves levels alone, because the cap table is
    /// built from them. This exists for the <b>role</b>, which is the one thing allowed to move
    /// them: the competition's base edit raises every trainer by a percentage, and a harder role
    /// raises them further while the player's own cap stays put. One byte, patched in place.
    /// </remarks>
    public static void SetLevel(byte[] party, int index, int level) =>
        party[(index * EntrySize) + LevelOffset] = (byte)Math.Clamp(level, 1, 100);

    public static int GetForm(byte[] party, int index) => party[(index * EntrySize) + FormOffset];

    public static int GetItem(byte[] party, int index) =>
        BitConverter.ToUInt16(party, (index * EntrySize) + ItemOffset);

    /// <summary>The four move slots, zeros included.</summary>
    public static int[] GetMoves(byte[] party, int index)
    {
        var at = (index * EntrySize) + MovesOffset;
        return [.. Enumerable.Range(0, MoveCount).Select(move => (int)BitConverter.ToUInt16(party, at + (move * 2)))];
    }

    /// <summary>True when the entry names its own moves instead of leaving them to the game.</summary>
    public static bool HasExplicitMoves(byte[] party, int index)
    {
        var at = (index * EntrySize) + MovesOffset;
        for (var move = 0; move < MoveCount; move++)
        {
            if (BitConverter.ToUInt16(party, at + (move * 2)) != 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Writes the species and clears the form, exactly as the gift and static tables do: a form
    /// index valid for the old species need not exist in the new one.
    /// <para>
    /// The level is deliberately left alone. It is what the level cap table is built from, and
    /// moving it would silently change the competition's caps.
    /// </para>
    /// </summary>
    public static void SetSpecies(byte[] party, int index, int species)
    {
        SetSpecies(party, index, species, form: 0);
    }

    /// <summary>
    /// Writes the species and a form on purpose.
    /// </summary>
    /// <remarks>
    /// For the one case where the form carries meaning: a mega is a <b>form</b> of its species, not
    /// a species of its own, so form 1 -- or 2 for the X/Y pairs -- is a Pokemon that walks into
    /// the battle already mega evolved. Everywhere else the form is cleared, because an index valid
    /// for the old species need not exist in the new one.
    /// </remarks>
    public static void SetSpecies(byte[] party, int index, int species, int form)
    {
        var at = index * EntrySize;
        BitConverter.GetBytes((ushort)species).CopyTo(party, at + SpeciesOffset);
        party[at + FormOffset] = (byte)form;
    }

    /// <summary>
    /// Blanks the four move slots so the game builds the moveset from the species' own learnset.
    /// <para>
    /// Safe by evidence, not by assumption: 518 of the cartridge's 1.139 trainer Pokémon already
    /// ship with all four slots at zero, so the game plainly handles that state.
    /// </para>
    /// </summary>
    public static void ClearMoves(byte[] party, int index)
    {
        var at = (index * EntrySize) + MovesOffset;
        Array.Clear(party, at, MoveCount * 2);
    }
}
