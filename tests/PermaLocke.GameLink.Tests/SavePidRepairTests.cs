using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Handing a personality value to the Pokémon that were delivered without one.
/// </summary>
/// <remarks>
/// Against a save built here and kept in memory, the same limit the name repair has: PKHeX does
/// not recognise a blank save written back to disk, so the file round trip is proved by
/// <c>Probe --pids --probar</c> on a copy of a real partida instead.
/// </remarks>
public sealed class SavePidRepairTests
{
    private static PK7 Zeroed(SAV7USUM game, int species, bool shiny = false)
    {
        var pokemon = PokemonBuilder.Build(
            new NewPokemon(species, 5, 0, 0, [31, 30, 29, 28, 27, 26], shiny), game);

        // Así salían: el constructor no ponía PID, y un PK7 nace a cero.
        pokemon.PID = 0;
        pokemon.RefreshChecksum();

        return pokemon;
    }

    private static SAV7USUM Save()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        game.SetBoxSlotAtIndex(Zeroed(game, 25), 0, 0);
        game.SetBoxSlotAtIndex(Zeroed(game, 1), 0, 1);

        var already = PokemonBuilder.Build(
            new NewPokemon(4, 5, 0, 0, [0, 1, 2, 3, 4, 5], false), game);
        already.PID = 0xDEADBEEF;
        already.RefreshChecksum();
        game.SetBoxSlotAtIndex(already, 0, 2);

        return game;
    }

    [Fact]
    public void Everything_at_zero_gets_a_pid_of_its_own()
    {
        var game = Save();

        var (total, given) = SavePidRepair.Apply(game);

        Assert.Equal(3, total);
        Assert.Equal(2, given.Count);

        var first = (PK7)game.GetBoxSlotAtIndex(0, 0);
        var second = (PK7)game.GetBoxSlotAtIndex(0, 1);

        Assert.NotEqual(0u, first.PID);
        Assert.NotEqual(0u, second.PID);
        Assert.NotEqual(first.PID, second.PID);
        Assert.True(first.ChecksumValid);
        Assert.True(second.ChecksumValid);
    }

    /// <summary>A PID the Pokémon already had is not PermaLocke's to reroll.</summary>
    [Fact]
    public void One_that_already_had_a_pid_is_left_alone()
    {
        var game = Save();

        SavePidRepair.Apply(game);

        Assert.Equal(0xDEADBEEFu, ((PK7)game.GetBoxSlotAtIndex(0, 2)).PID);
    }

    /// <summary>
    /// The shininess is a property of the Pokémon, and a repair that changes it is a repair that
    /// broke something.
    /// </summary>
    [Fact]
    public void Shininess_survives_the_new_pid()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        var shiny = Zeroed(game, 25, shiny: true);
        // Un PID de cero casi nunca es shiny, así que se marca aparte para poder comprobar que se
        // conserva lo que el Pokémon dice ser.
        var wasShiny = shiny.IsShiny;
        game.SetBoxSlotAtIndex(shiny, 0, 0);

        var plain = Zeroed(game, 1);
        game.SetBoxSlotAtIndex(plain, 0, 1);

        SavePidRepair.Apply(game);

        Assert.Equal(wasShiny, ((PK7)game.GetBoxSlotAtIndex(0, 0)).IsShiny);
        Assert.False(((PK7)game.GetBoxSlotAtIndex(0, 1)).IsShiny);
    }

    /// <summary>Running it twice must find nothing to do the second time.</summary>
    [Fact]
    public void It_is_idempotent()
    {
        var game = Save();

        SavePidRepair.Apply(game);
        var (_, again) = SavePidRepair.Apply(game);

        Assert.Empty(again);
    }

    /// <summary>
    /// The party is a different store, and a Pokémon in it is exactly the one whose identity
    /// matters most: it is the only place the watcher ever looks.
    /// </summary>
    [Fact]
    public void The_party_is_covered_too()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };
        game.SetPartySlotAtIndex(Zeroed(game, 25), 0);

        var (total, given) = SavePidRepair.Apply(game);

        Assert.Equal(1, total);
        Assert.Contains("equipo", Assert.Single(given).Where);
        Assert.NotEqual(0u, ((PK7)game.GetPartySlotAtIndex(0)).PID);
    }

    /// <summary>
    /// What the builder does now, so nothing new is ever delivered with the fault this repairs.
    /// </summary>
    [Fact]
    public void The_builder_no_longer_hands_out_zeros()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        var pokemon = Enumerable.Range(0, 20)
            .Select(_ => PokemonBuilder.Build(
                new NewPokemon(25, 5, 0, 0, [31, 31, 31, 31, 31, 31], false), game))
            .ToList();

        Assert.All(pokemon, p => Assert.NotEqual(0u, p.PID));
        Assert.All(pokemon, p => Assert.False(p.IsShiny));
        Assert.Equal(pokemon.Count, pokemon.Select(p => p.PID).Distinct().Count());
    }

    [Fact]
    public void The_builder_still_makes_a_shiny_when_it_is_asked_to()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        var shiny = PokemonBuilder.Build(
            new NewPokemon(25, 5, 0, 0, [31, 31, 31, 31, 31, 31], true), game);

        Assert.True(shiny.IsShiny);
        Assert.NotEqual(0u, shiny.PID);
    }
}
