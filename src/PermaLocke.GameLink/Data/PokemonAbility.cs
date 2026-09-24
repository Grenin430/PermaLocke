using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// The ability of a Pokémon as the game being played stores it: one byte, plus a ninth bit.
/// </summary>
/// <remarks>
/// <para>
/// PKHeX reads a gen 7 ability as the byte at 0x14, which is all the cartridge ever needed. The
/// expansion mod goes up to 319 and keeps the ninth bit in <b>bit 4 of 0x15</b>, the byte whose low
/// three bits are the ability slot (1, 2 or 4). Disassembled from the block the mod adds to
/// <c>code.bin</c> (§134): on save the game stores the low byte at 0x14 and sets or clears 0x10 at
/// 0x15, and on load it reads them back the same way. It is the same trick the mod plays on the
/// species table (§132), one structure further along. On the plain cartridge that bit is always
/// zero, so reading it changes nothing there.
/// </para>
/// <para>
/// Reading only the byte does not fail, it names another ability: Cambio Heroico (278) reads as
/// 22, Imán. Writing only the byte is worse, because the old ninth bit stays behind — an old
/// ability given to a Pokémon that had a new one would come out as that ability plus 256.
/// </para>
/// </remarks>
public static class PokemonAbility
{
    /// <summary>The highest ability one byte and one bit can hold.</summary>
    public const int MaxStorable = 0x1FF;

    private const int LowByte = 0x14;
    private const int HighBits = 0x15;
    private const byte NinthBit = 0x10;

    /// <summary>The whole ability id, ninth bit included.</summary>
    public static int Of(PK7 pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        return pokemon.Data[LowByte] | ((pokemon.Data[HighBits] & NinthBit) != 0 ? 0x100 : 0);
    }

    /// <summary>
    /// Writes the whole ability id, and clears the ninth bit when the new one does not need it.
    /// </summary>
    /// <remarks>Leaves the ability slot in the low bits of 0x15 as it was.</remarks>
    public static void Set(PK7 pokemon, int ability)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentOutOfRangeException.ThrowIfNegative(ability);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ability, MaxStorable);

        pokemon.Data[LowByte] = (byte)(ability & 0xFF);
        pokemon.Data[HighBits] = ability > 0xFF
            ? (byte)(pokemon.Data[HighBits] | NinthBit)
            : (byte)(pokemon.Data[HighBits] & ~NinthBit);
    }
}
