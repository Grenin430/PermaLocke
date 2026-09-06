using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PKHeX.Core;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// La lápida: qué le hace la run a un Pokémon caído.
/// </summary>
/// <remarks>
/// Era convertirlo en un Shedinja llamado MUERTO y ya no. El jugador pidió quitarlo, y de paso es
/// mejor marca: un Shedinja destruye al Pokémon y no se puede deshacer, mientras que quedarse sin
/// PS es lo que el propio juego llama debilitado. Verificado contra la pantalla en el §98 bis —
/// 55 PS escritos en el fichero salieron a 55 al cargar.
/// </remarks>
public sealed class DeathMarkTests
{
    private static PK7 Living(int hp = 87)
    {
        var pokemon = new PK7
        {
            Species = 570,
            CurrentLevel = 32,
            Nickname = "Zorrito",
            IsNicknamed = true,
            PID = 0xABCD1234,
            EncryptionConstant = 0x11223344,
            Move1 = 10, Move2 = 33, Move3 = 45, Move4 = 55,
            Move1_PP = 35, Move2_PP = 35, Move3_PP = 40, Move4_PP = 30,
        };

        pokemon.Stat_HPMax = 104;
        pokemon.Stat_HPCurrent = hp;

        return pokemon;
    }

    [Fact]
    public void Marcar_le_quita_los_PS_y_lo_deja_siendo_el()
    {
        var pokemon = Living();

        DeathMark.Apply(pokemon);

        Assert.Equal(0, pokemon.Stat_HPCurrent);
        Assert.True(DeathMark.IsMarked(pokemon));
        Assert.True(pokemon.ChecksumValid);

        // Y sigue siendo él, que es toda la diferencia con el Shedinja.
        Assert.Equal(570, pokemon.Species);
        Assert.Equal("Zorrito", pokemon.Nickname);
        Assert.Equal(32, pokemon.CurrentLevel);
        Assert.Equal(10, pokemon.Move1);
        Assert.Equal(55, pokemon.Move4);
        Assert.Equal(104, pokemon.Stat_HPMax);
    }

    /// <summary>
    /// El estado se va con los PS, porque uno en el suelo no está además envenenado: dejarlo sería
    /// dejar la partida en un estado que el juego no escribe nunca.
    /// </summary>
    [Fact]
    public void El_estado_se_va_con_los_PS()
    {
        var pokemon = Living();
        pokemon.Status_Condition = 8;

        DeathMark.Apply(pokemon);

        Assert.Equal(0, pokemon.Status_Condition);
    }

    /// <summary>
    /// Lo que NO se toca, y es la razón de que esto funcione.
    /// </summary>
    /// <remarks>
    /// El PID es lo único por lo que la run empareja (§56). Cambiarlo convertiría la lápida en un
    /// huérfano que nadie puede atar a ningún registro, y de paso haría que el vigilante lo viera
    /// como un Pokémon nuevo sin registrar.
    /// </remarks>
    [Fact]
    public void El_PID_y_la_constante_de_encriptacion_no_se_tocan()
    {
        var pokemon = Living();

        DeathMark.Apply(pokemon);

        Assert.Equal(0xABCD1234u, pokemon.PID);
        Assert.Equal(0x11223344u, pokemon.EncryptionConstant);
    }

    [Fact]
    public void Marcar_a_uno_ya_caido_no_hace_nada()
    {
        var pokemon = Living(hp: 0);

        Assert.True(DeathMark.IsMarked(pokemon));

        DeathMark.Apply(pokemon);

        Assert.Equal(0, pokemon.Stat_HPCurrent);
        Assert.Equal(570, pokemon.Species);
    }

    /// <summary>
    /// La limitación, dicha en una prueba y no solo en un comentario: en una caja esto no vale.
    /// </summary>
    /// <remarks>
    /// Un Pokémon guardado no lleva estadísticas de combate (§32), así que escribirle un cero no
    /// cambia nada que el juego lea y saldría de la caja entero. Quien escriba tiene que preguntar,
    /// porque una marca que aparenta funcionar es peor que no tener ninguna.
    /// </remarks>
    [Fact]
    public void La_marca_solo_significa_algo_en_el_equipo()
    {
        Assert.True(DeathMark.WorksIn(BoxedPokemon.PartyBox));

        Assert.False(DeathMark.WorksIn(0));
        Assert.False(DeathMark.WorksIn(7));
        Assert.False(DeathMark.WorksIn(31));
    }
}
