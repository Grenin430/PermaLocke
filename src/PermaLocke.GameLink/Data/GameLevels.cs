using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// The level of a Pokémon as <b>the game being played</b> computes it.
/// </summary>
/// <remarks>
/// <para>
/// A level is not a stored field. It is derived from experience, and the curve to derive it with
/// is a property of the species. PKHeX derives it from its own gen 7 table, which stops at 807, so
/// for every species the expansion mod adds it falls back to Medium Fast. That is not a blank or an
/// error: it is a confident wrong number.
/// </para>
/// <para>
/// Measured on the real run, and it cost exactly two levels. A Dragapult with <b>53593</b>
/// experience: that is 1.25 × 35³ to the unit, level 35 on the <b>Slow</b> curve, and the game's own
/// Stat_Level field said 35. PermaLocke read 37. With a cap of 34 it kept correcting a Pokémon that
/// was over by one as though it were over by three — and wrote back «the experience for 34» on the
/// Medium Fast curve, which on the Slow curve is level <b>31</b>. A cap that takes three levels
/// instead of one is not a cap.
/// </para>
/// <para>
/// So: when the installed world has published its own table, that wins. When it has not — the plain
/// cartridge, or a table that could not be read — PKHeX is right and is used. There is no third
/// branch that guesses.
/// </para>
/// </remarks>
public static class GameLevels
{
    /// <summary>What level this Pokémon actually is in the game it came from.</summary>
    public static int Of(PKM pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        return WorldLimits.GrowthOf(pokemon.Species) is { } growth
            ? Experience.GetLevel(pokemon.EXP, growth)
            : pokemon.CurrentLevel;
    }

    /// <summary>Sets the Pokémon to a level, on the curve its own game uses.</summary>
    /// <remarks>
    /// Both fields, because a party Pokémon carries the level twice — as experience and as
    /// <c>Stat_Level</c> — and the game reads them in different places (§53). Setting only one
    /// leaves the two disagreeing, which is how a corrected Pokémon can still show its old level.
    /// </remarks>
    public static void Set(PKM pokemon, int level)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        if (WorldLimits.GrowthOf(pokemon.Species) is { } growth)
        {
            pokemon.EXP = Experience.GetEXP((byte)level, growth);
            pokemon.Stat_Level = (byte)level;
            return;
        }

        pokemon.CurrentLevel = (byte)level;
    }
}
