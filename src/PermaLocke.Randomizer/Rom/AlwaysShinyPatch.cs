namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// A test switch that makes every Pokémon the game generates shiny, as an IPS patch Azahar applies at boot.
/// </summary>
/// <remarks>
/// <para>
/// For trying the shiny clause of the first-encounter rule (§117) without waiting for a 1 in 4096. Not a feature of
/// the competition, and it never goes into a generated world: it is a separate file, <c>exefs/code.ips</c> in the
/// installed mod, put there and taken away by hand with <c>Probe --shiny-siempre</c>.
/// </para>
/// <para>
/// The site is pk3DS's own «everything shiny» switch for generation 7 (<c>pk3DS.WinForms/Subforms/ShinyRate.cs</c>,
/// GPLv3 like PermaLocke): after the pattern below — <c>EOR r2,r2,r0; AND r3,r1,#2; ORRS r2,r2,r3</c> and the first
/// three bytes of a branch — the fourth byte is the branch condition. <c>0A</c> is BEQ, the cartridge's; <c>EA</c> is
/// an unconditional B, so the shiny path is always taken. Measured in the player's installed <c>code.bin</c> on
/// 2026-09-14: exactly one match, the byte at <c>0x2205CF</c> holds <c>0A</c>, and pk3DS's PID reroll routine sits
/// unpatched right after it at <c>0x220668</c>, which is what places this as the same routine in Ultra Moon.
/// </para>
/// <para>
/// Azahar reads <c>load/mods/&lt;title&gt;/exefs/code.ips</c> and applies it to the code after loading the mod's own
/// <c>code.bin</c> (<c>NCCHContainer::ApplyCodePatch</c>, called from <c>AppLoader_NCCH::LoadExec</c>), so neither
/// that file nor the ROM is touched, and removing the patch is deleting one file. It is read at boot only.
/// </para>
/// </remarks>
public static class AlwaysShinyPatch
{
    /// <summary>What precedes the branch condition byte. From pk3DS.</summary>
    public static readonly byte[] Pattern =
        [0x00, 0x20, 0x22, 0xE0, 0x02, 0x30, 0x21, 0xE2, 0x03, 0x20, 0x92, 0xE1, 0x1C, 0x00, 0x00];

    public const byte Original = 0x0A;
    public const byte Always = 0xEA;

    /// <summary>Where the condition byte is, or null unless the pattern appears exactly once.</summary>
    /// <remarks>
    /// Exactly once, because a patch that picks the first of two matches changes a branch nobody has looked at.
    /// </remarks>
    public static int? Find(ReadOnlySpan<byte> code)
    {
        var first = code.IndexOf(Pattern);

        if (first < 0 || first + Pattern.Length >= code.Length)
        {
            return null;
        }

        return code[(first + 1)..].IndexOf(Pattern) >= 0 ? null : first + Pattern.Length;
    }

    /// <summary>An IPS file with one record: <see cref="Always"/> at <paramref name="offset"/>.</summary>
    public static byte[] Ips(int offset)
    {
        if (offset is < 0 or > 0xFFFFFF || offset == 0x454F46)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Un parche IPS no puede apuntar ahí.");
        }

        return
        [
            (byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H',
            (byte)(offset >> 16), (byte)(offset >> 8), (byte)offset,
            0x00, 0x01,
            Always,
            (byte)'E', (byte)'O', (byte)'F'
        ];
    }
}
