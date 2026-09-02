using PermaLocke.GameLink.Data;
using PKHeX.Core;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// La lápida: en qué convierte la run a un Pokémon caído.
/// </summary>
public sealed class DeathMarkTests
{
    private static PK7 Living() => new()
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

    [Fact]
    public void Marcar_lo_convierte_en_Shedinja_MUERTO_de_nivel_1()
    {
        var pokemon = Living();

        DeathMark.Apply(pokemon);

        Assert.Equal(DeathMark.Species, pokemon.Species);
        Assert.Equal(DeathMark.Nickname, pokemon.Nickname);
        Assert.True(pokemon.IsNicknamed);
        Assert.Equal(1, pokemon.CurrentLevel);
        Assert.Equal(0, pokemon.Move1);
        Assert.Equal(0, pokemon.Move4);
        Assert.True(pokemon.ChecksumValid);
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
        var pid = pokemon.PID;
        var ec = pokemon.EncryptionConstant;

        DeathMark.Apply(pokemon);

        Assert.Equal(pid, pokemon.PID);
        Assert.Equal(ec, pokemon.EncryptionConstant);
    }

    [Fact]
    public void Un_marcado_se_reconoce_y_uno_vivo_no()
    {
        var pokemon = Living();

        Assert.False(DeathMark.IsMarked(pokemon));

        DeathMark.Apply(pokemon);

        Assert.True(DeathMark.IsMarked(pokemon));
    }

    /// <summary>
    /// Un Shedinja de verdad, capturado y con su nombre, no cuenta como lápida.
    /// </summary>
    /// <remarks>
    /// Hacen falta las dos cosas. Con solo la especie, cualquiera que capture un Shedinja lo vería
    /// tratado como muerto; con solo el mote, un Pokémon apodado MUERTO por gusto también.
    /// </remarks>
    [Fact]
    public void Un_Shedinja_normal_no_es_una_lapida()
    {
        var real = new PK7 { Species = DeathMark.Species, Nickname = "Ninja", CurrentLevel = 20 };

        Assert.False(DeathMark.IsMarked(real));
    }

    /// <summary>Volver a marcar no cambia nada: la operación se puede repetir sin miedo.</summary>
    [Fact]
    public void Marcar_dos_veces_deja_lo_mismo()
    {
        var pokemon = Living();

        DeathMark.Apply(pokemon);
        var first = pokemon.Data.ToArray();

        DeathMark.Apply(pokemon);

        Assert.Equal(first, pokemon.Data.ToArray());
    }
}
