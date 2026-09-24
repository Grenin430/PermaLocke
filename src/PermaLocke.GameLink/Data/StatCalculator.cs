namespace PermaLocke.GameLink.Data;

/// <summary>
/// Works out the six battle stats a party Pokémon should be showing.
/// </summary>
/// <remarks>
/// <para>
/// It exists because writing EVs is only half of giving a Pokémon EVs. A party member <b>stores</b>
/// its stats — unlike one in a box, which has none and is worked out on the way out — so changing
/// the effort values on disk moves a number the game never looks at again until something makes it
/// recalculate. <see cref="SaveEvTrainer"/> used to leave them alone and say the stat would «catch
/// up when the game next recalculates», which is true of a Pokémon gaining EVs in battle and false
/// here: with a level cap in force the Pokémon does not level up, so it never recalculates and the
/// training simply never shows.
/// </para>
/// <para>
/// The base stats have to be the installed world's. That is not a detail — this run is played with
/// <c>shuffleBaseStats</c> on, so PKHeX's table is wrong for every species and not just for the ones
/// the expansion added. When the world has not published its table there is no answer, and the
/// caller must leave the stat alone rather than write a plausible one.
/// </para>
/// </remarks>
public static class StatCalculator
{
    /// <summary>Stats a Pokémon has.</summary>
    public const int Count = 6;

    /// <summary>
    /// Where the nature's own numbering puts each stat of the summary screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A nature is <c>increased = nature / 5</c> and <c>decreased = nature % 5</c> over the game's
    /// internal order — <b>Ataque, Defensa, VELOCIDAD, At. Esp., Def. Esp.</b> — which is not the
    /// screen's. Velocidad is third there and fifth here, so a mapping that just subtracts one from
    /// the screen's index moves the wrong two stats.
    /// </para>
    /// <para>
    /// It went in wrong first time and the tests passed, because the nature they used raises Ataque
    /// and lowers Defensa and those two sit in the same place in both orders. What caught it was
    /// checking the calculator against the six Pokémon of the real partida, whose stats the game
    /// itself worked out: <b>25 of 36</b>, and the eleven that failed were all At. Esp., Def. Esp.
    /// or Velocidad, on the five Pokémon whose nature is not neutral.
    /// </para>
    /// </remarks>
    private static readonly int[] NatureIndex = [0, 1, 3, 4, 2];

    /// <summary>
    /// What a nature does to one stat of the summary screen. PS are never touched.
    /// </summary>
    /// <remarks>
    /// Natures are not randomized, so unlike the base stats this is a fact about the series and can
    /// live in code. The five natures whose two halves coincide do nothing at all.
    /// </remarks>
    public static double NatureFactor(int nature, int stat)
    {
        if (nature is < 0 or > 24 || stat is < 1 or >= Count)
        {
            return 1.0;
        }

        var up = nature / 5;
        var down = nature % 5;

        if (up == down)
        {
            return 1.0;
        }

        var index = NatureIndex[stat - 1];

        return index == up ? 1.1 : index == down ? 0.9 : 1.0;
    }

    /// <summary>
    /// The six stats, in the summary screen's order: PS, Ataque, Defensa, At. Esp., Def. Esp., Velocidad.
    /// </summary>
    /// <param name="baseStats">The installed world's, in that same order. Six of them.</param>
    /// <exception cref="ArgumentException">If any of the three tables is not six long.</exception>
    public static int[] Compute(IReadOnlyList<byte> baseStats, IReadOnlyList<int> ivs,
        IReadOnlyList<int> evs, int level, int nature)
    {
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(ivs);
        ArgumentNullException.ThrowIfNull(evs);

        if (baseStats.Count != Count || ivs.Count != Count || evs.Count != Count)
        {
            throw new ArgumentException(
                $"Hacen falta {Count} bases, {Count} IV y {Count} EV; llegaron "
                + $"{baseStats.Count}, {ivs.Count} y {evs.Count}.");
        }

        var stats = new int[Count];

        for (var stat = 0; stat < Count; stat++)
        {
            // Los EV cuentan de cuatro en cuatro y se truncan, asi que 3 EV no valen nada y 255 vale
            // lo mismo que 252. No es un redondeo nuestro: es como lo hace el juego.
            var core = ((2 * baseStats[stat]) + ivs[stat] + (evs[stat] / 4)) * level / 100;

            stats[stat] = stat == 0
                ? core + level + 10
                : (int)((core + 5) * NatureFactor(nature, stat));
        }

        return stats;
    }

    /// <summary>
    /// Shedinja is the exception the formula does not cover: its PS are always one.
    /// </summary>
    /// <remarks>
    /// Its base PS is 1 and the formula would still give it level + 11. The game special-cases it,
    /// so anything that writes stats has to as well or it hands out a Shedinja with 70 PS.
    /// </remarks>
    public static int[] Compute(IReadOnlyList<byte> baseStats, IReadOnlyList<int> ivs,
        IReadOnlyList<int> evs, int level, int nature, bool isShedinja)
    {
        var stats = Compute(baseStats, ivs, evs, level, nature);

        if (isShedinja)
        {
            stats[0] = 1;
        }

        return stats;
    }

    /// <summary>Shedinja, whose PS the game forces to one whatever the formula says.</summary>
    private const int Shedinja = 292;

    /// <summary>
    /// Puts a party member's stored battle stats back in step with its level and effort values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Moved here from <see cref="SaveEvTrainer"/> on 2026-09-21 so the level cap uses the very same code: a Pokémon
    /// brought down from 15 to 14 has to lose its level-15 stats too, or twenty rare candies and a cap make a level 14
    /// with level 34 stats.
    /// </para>
    /// <para>
    /// Returns false, and touches nothing, when the installed world has not published its base stats. That is the
    /// §51 rule kept rather than dropped: with <c>shuffleBaseStats</c> on, a stat worked out from PKHeX's table is not an
    /// approximation but a wrong number written into somebody's Pokémon — 168 PS came back as 151 the day that was
    /// tried. The world's own table is readable now (<see cref="WorldLimits.BaseStats"/>), so the honest answer is
    /// usually available instead of never.
    /// </para>
    /// <para>
    /// The current PS are the delicate part. They follow the maximum by the same amount, which is what the game does on
    /// a level up — but <b>a Pokémon at zero stays at zero</b>. In this project zero PS is what a death IS (§98), so
    /// healing one here would quietly undo a death through a screen that has nothing to do with dying.
    /// </para>
    /// </remarks>
    public static bool Restat(PKHeX.Core.PK7 pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        // Por especie Y forma: un Raichu de Alola no está hecho como un Raichu, y con solo la especie se
        // le escribían las estadísticas del normal (§131).
        if (WorldLimits.BaseStatsOf(pokemon.Species, pokemon.Form) is not { } bases)
        {
            return false;
        }

        var stats = Compute(
            bases,
            [pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE],
            [pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF, pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE],
            pokemon.Stat_Level,
            (int)pokemon.Nature,
            pokemon.Species == Shedinja);

        var gained = stats[0] - pokemon.Stat_HPMax;

        pokemon.Stat_HPMax = stats[0];
        pokemon.Stat_ATK = stats[1];
        pokemon.Stat_DEF = stats[2];
        pokemon.Stat_SPA = stats[3];
        pokemon.Stat_SPD = stats[4];
        pokemon.Stat_SPE = stats[5];

        if (pokemon.Stat_HPCurrent > 0)
        {
            pokemon.Stat_HPCurrent = Math.Clamp(pokemon.Stat_HPCurrent + gained, 1, stats[0]);
        }

        return true;
    }
}
