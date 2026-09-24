using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Cuántos Pokémon de más lleva un combate importante, por rol (§47).
/// </summary>
/// <remarks>
/// El módulo no tenía ni una prueba, y ha tenido dos fallos: el §47 lo generó con los equipos crecidos y los
/// niveles sin subir, y el §85 descubrió que no aplicaba la regla de «todos evolucionados». Esto fija lo que la
/// competición promete —vanilla + 1, y + 2 en EXPERTO— y, sobre todo, lo que ese «+» significa cuando ya no cabe.
/// </remarks>
public sealed class ExtraPokemonTests
{
    /// <summary>NORMAL, CAGONETA y LUDÓPATA: los tres llevan pokemonExtra 1.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 0)]   // ya va lleno
    public void One_extra_fits_in_every_team_that_is_not_already_full(int team, int expected) =>
        Assert.Equal(expected, ExtraPokemonRandomizer.ExtraFor(team, 1));

    /// <summary>EXPERTO: pokemonExtra 2, y con cinco solo cabe uno.</summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 1)]   // el tope recorta, no el módulo
    [InlineData(6, 0)]
    public void Two_extras_are_cut_short_only_by_the_six_the_engine_allows(int team, int expected) =>
        Assert.Equal(expected, ExtraPokemonRandomizer.ExtraFor(team, 2));

    /// <summary>Nunca se pasa de seis, pida el rol lo que pida.</summary>
    [Theory]
    [InlineData(1, 9)]
    [InlineData(4, 5)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    public void A_team_never_grows_past_six(int team, int wanted) =>
        Assert.True(team + ExtraPokemonRandomizer.ExtraFor(team, wanted) <= ExtraPokemonRandomizer.MaxParty);

    /// <summary>Un rol que no añade nada no añade nada.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    public void No_extra_asked_for_is_no_extra_added(int team) =>
        Assert.Equal(0, ExtraPokemonRandomizer.ExtraFor(team, 0));

    /// <summary>
    /// El reparto medido en el mundo instalado del jugador el 2026-09-21, rol EXPERTO: los 137 combates
    /// importantes con el tamaño que traían del cartucho. Si la regla cambiara, estos números dejarían de salir.
    /// </summary>
    [Fact]
    public void The_measured_world_matches_the_rule()
    {
        (int Team, int Battles)[] medido = [(1, 38), (2, 7), (3, 24), (4, 11), (5, 42), (6, 15)];

        Assert.Equal(137, medido.Sum(m => m.Battles));

        var conDos = medido.Where(m => ExtraPokemonRandomizer.ExtraFor(m.Team, 2) == 2).Sum(m => m.Battles);
        var conUno = medido.Where(m => ExtraPokemonRandomizer.ExtraFor(m.Team, 2) == 1).Sum(m => m.Battles);
        var conNada = medido.Where(m => ExtraPokemonRandomizer.ExtraFor(m.Team, 2) == 0).Sum(m => m.Battles);

        Assert.Equal(80, conDos);
        Assert.Equal(42, conUno);
        Assert.Equal(15, conNada);

        // Y con los otros tres roles, todos menos los quince que ya iban con seis.
        Assert.Equal(122, medido.Where(m => ExtraPokemonRandomizer.ExtraFor(m.Team, 1) == 1).Sum(m => m.Battles));
    }
}
