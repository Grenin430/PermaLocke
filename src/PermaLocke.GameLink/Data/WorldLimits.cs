namespace PermaLocke.GameLink.Data;

/// <summary>
/// How big the world being played is, for the sanity filters that separate a real Pokémon from
/// heap rubbish.
/// </summary>
/// <remarks>
/// <para>
/// The live readers reject anything whose species is out of range. That check is <b>secondary</b> —
/// the decisive one is <c>PK7.ChecksumValid</c> (§56) — but it is not decoration either, and on the
/// cartridge a species above 807 cannot exist at all.
/// </para>
/// <para>
/// It became a setting because of the gen 8-9 expansion, which raises the ceiling to 1025. Simply
/// widening the constant would have been the §55 mistake in miniature: loosening a guard so that a
/// world which <em>might</em> be installed keeps working, at the cost of the guard being looser for
/// everyone who has not installed it. So the ceiling is declared, and the default is the cartridge.
/// </para>
/// <para>
/// It is deliberately a process-wide value set once at startup rather than an injected service:
/// two of the three readers are static helpers on the memory-scanning path, and threading a
/// dependency through them would have touched the party locator, the layout scanner and the live
/// reader for a single integer. The contract is that <see cref="MaxSpecies"/> is set before the
/// game link starts and not touched again.
/// </para>
/// </remarks>
public static class WorldLimits
{
    /// <summary>What Ultra Sun and Ultra Moon ship with.</summary>
    public const int CartridgeMaxSpecies = 807;

    /// <summary>The highest species id that can legitimately exist in the installed world.</summary>
    /// <remarks>
    /// A value below the cartridge's is refused rather than clamped: it would mean somebody has
    /// mixed up "the cap the player chose for the randomizer" with "what the game can hold", and
    /// under that confusion the live readers would start discarding perfectly real Pokémon as
    /// rubbish — silently, which is the failure this project keeps having to design against.
    /// </remarks>
    public static int MaxSpecies
    {
        get;
        set => field = value >= CartridgeMaxSpecies
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                $"El mundo no puede tener menos de {CartridgeMaxSpecies} especies: eso es lo que trae "
                + "el cartucho. Este número es lo que el JUEGO admite, no el tope que el jugador le "
                + "haya puesto al randomizador.");
    } = CartridgeMaxSpecies;

    /// <summary>True when the species could exist in this world at all.</summary>
    public static bool IsKnownSpecies(int species) => species > 0 && species <= MaxSpecies;

    /// <summary>
    /// Experience growth rate per species, straight out of the installed game's own table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Empty by default, which means «ask PKHeX». It is filled in from the installed world's
    /// personal file at startup, and it matters because a level is not stored: it is
    /// <b>computed</b> from experience, and the curve to compute it with is a property of the
    /// species. PKHeX's gen 7 table stops at 807, so for anything the expansion mod adds it falls
    /// back to Medium Fast — and that is not an error message, it is a wrong number.
    /// </para>
    /// <para>
    /// Measured on the real run, and it is exactly two levels: a Dragapult with 53593 experience.
    /// That is 1.25 x 35³ to the unit, level 35 on the <b>Slow</b> curve, and the game's own
    /// Stat_Level field said 35. PermaLocke read 37. With a cap of 34 it corrected a Pokémon by
    /// three levels that was over by one, and wrote back the experience for «34» on the wrong
    /// curve, which in the game is level 31.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<byte> GrowthRates { get; set; } = [];

    /// <summary>
    /// The growth curve for a species, or null when this world has not published its table.
    /// </summary>
    /// <remarks>
    /// Null and not a default: guessing Medium Fast here is precisely the bug. A caller that gets
    /// null has to fall back to PKHeX and knows it is doing so.
    /// </remarks>
    public static byte? GrowthOf(int species) =>
        species > 0 && species < GrowthRates.Count ? GrowthRates[species] : null;
}
