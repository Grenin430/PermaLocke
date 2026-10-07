namespace PermaLocke.Core.Domain;

/// <summary>
/// Where PermaLocke leaves the run's rules for the patched game to read (2026-10-06, rules inside the game).
/// </summary>
/// <remarks>
/// <para>
/// The 252 bytes between the end of <c>code.bin</c>'s BSS (0x6D3F04, from the cartridge's exheader: data 0x667000 +
/// 0x3CD48, BSS 0x301BC) and the end of its last page (0x6D4000). Mapped and writable, never cleared at boot (the start-up
/// loop at 0x100024 stops at 0x6D3F04), and nothing in the mod's <c>code.bin</c> or its <c>.cro</c> points inside it.
/// </para>
/// <para>
/// A fresh game reads zeros there, and every patch treats zero as «no rule»: without PermaLocke the game behaves as the
/// cartridge does.
/// </para>
/// </remarks>
public static class RuleBlock
{
    public const uint Address = 0x006D3F10;

    /// <summary>«PLK1», so a reader can tell the block was written by this version.</summary>
    public const uint Magic = 0x314B4C50;

    /// <summary>The level cap now (1-100); 0 is no cap.</summary>
    public const uint CapOffset = 4;

    public static uint Cap => Address + CapOffset;

    /// <summary>
    /// Why a ball cannot be thrown now (a byte; 0 = it can): one of the battle menu's own reasons, which picks its message.
    /// PermaLocke writes <see cref="SpentZoneReason"/> in a spent zone, and 0 for a wild shiny.
    /// </summary>
    public const uint BallRefusalOffset = 8;

    public static uint BallRefusal => Address + BallRefusalOffset;

    /// <summary>
    /// Levels a SuperCarameloraro has just raised beyond the first (a byte, 0-4), so the move check that follows also
    /// offers the moves of the levels it skipped. The game itself writes it and puts it back to 0 when the check ends;
    /// PermaLocke never touches it.
    /// </summary>
    public const uint SkippedLevelsOffset = 0xC;

    public static uint SkippedLevels => Address + SkippedLevelsOffset;

    /// <summary>The reason whose message (battle text 135, the fused Necrozma's) says the zone is spent.</summary>
    public const byte SpentZoneReason = 8;

    /// <summary>The game's own reason for a trial zone (battle text 63): «¡No podrás atrapar Pokémon hasta haber superado la prueba!».</summary>
    public const byte TrialReason = 5;

    /// <summary>
    /// The encryption constants of the run's fallen in the party, six words (0 = empty slot). The patched game keeps their
    /// HP at zero whatever heals them: it reads the constant from the Pokémon's block, where it is never encrypted.
    /// </summary>
    public const uint FallenOffset = 0x20;

    public const int FallenSlots = 6;

    public static uint Fallen => Address + FallenOffset;

    /// <summary>
    /// The duplicates clause: one bit per species (byte = species / 8, bit = species % 8), set for every species of a
    /// family the run already has. A wild slot whose species is set is rolled again among the slots that are not. Bit 0
    /// stands for empty slots and is set whenever any other is, so an empty slot is never the reroll's pick.
    /// </summary>
    public const uint DupesOffset = 0x40;

    public const int DupesBytes = 0x88;

    /// <summary>Species from this one up are never duplicates for the game (the bitset ends here).</summary>
    public const int DupesSpeciesLimit = DupesBytes * 8;

    public static uint Dupes => Address + DupesOffset;

    /// <summary>
    /// The Incubadora Turbo (2026-10-07): a byte, 1 while it is on. The game writes it, from the bag (<c>Bag.cro</c>), and reads it in the
    /// egg step count; PermaLocke never touches it. A fresh game reads 0, so the cartridge hatches eggs as it always did.
    /// </summary>
    public const uint EggTurboOffset = 0x10;

    public static uint EggTurbo => Address + EggTurboOffset;
}
