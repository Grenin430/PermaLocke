using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Putting the name back on the Pokémon that were delivered without one.
/// </summary>
/// <remarks>
/// Against a save built here and kept in memory. It goes through the real box and party walk and
/// the real naming; what it cannot cover is the file round trip, because PKHeX does not recognise
/// a blank save written back to disk, and the only save it would recognise is the player's own —
/// the same limit the box reader has.
/// </remarks>
public sealed class SaveNameRepairTests
{
    private static SAV7USUM Save()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        var nameless = PokemonBuilder.Build(new NewPokemon(25, 5, 0, 0, [31, 31, 31, 31, 31, 31], false), game);
        nameless.Nickname = string.Empty;
        nameless.RefreshChecksum();
        game.SetBoxSlotAtIndex(nameless, 0, 0);

        var named = PokemonBuilder.Build(new NewPokemon(1, 5, 0, 0, [0, 0, 0, 0, 0, 0], false), game);
        named.Nickname = "Verdecito";
        named.IsNicknamed = true;
        named.RefreshChecksum();
        game.SetBoxSlotAtIndex(named, 0, 1);

        return game;
    }

    [Fact]
    public void It_finds_the_nameless_one_and_names_it()
    {
        var game = Save();

        var (total, named) = SaveNameRepair.Apply(game);

        Assert.Equal(2, total);
        Assert.Contains("Pikachu", Assert.Single(named));
        Assert.Contains("caja 1, hueco 1", named[0]);

        var repaired = (PK7)game.GetBoxSlotAtIndex(0, 0);
        Assert.Equal("Pikachu", repaired.Nickname);
        Assert.False(repaired.IsNicknamed);
        Assert.True(repaired.ChecksumValid);
    }

    /// <summary>A name the player chose is not PermaLocke's to touch.</summary>
    [Fact]
    public void A_nickname_the_player_gave_is_left_alone()
    {
        var game = Save();

        SaveNameRepair.Apply(game);

        var untouched = (PK7)game.GetBoxSlotAtIndex(0, 1);
        Assert.Equal("Verdecito", untouched.Nickname);
        Assert.True(untouched.IsNicknamed);
    }

    /// <summary>Running it twice must find nothing the second time.</summary>
    [Fact]
    public void A_second_pass_has_nothing_to_do()
    {
        var game = Save();
        SaveNameRepair.Apply(game);

        var (total, named) = SaveNameRepair.Apply(game);

        Assert.Equal(2, total);
        Assert.Empty(named);
    }

    /// <summary>
    /// Renaming is not acquiring. PKHeX bumps captures, Poké Balls used and wild battles when a
    /// Pokémon is put into a box, and on the real run that turned a rename of 150 into 150
    /// captures, 150 balls and 150 wild battles that never happened.
    /// </summary>
    [Fact]
    public void Repairing_does_not_touch_the_trainer_card_counters()
    {
        var game = Save();
        int[] watched = [3, 4, 6, 42, 21];
        var before = watched.Select(game.Records.GetRecord).ToArray();

        SaveNameRepair.Apply(game);

        Assert.Equal(before, watched.Select(game.Records.GetRecord).ToArray());
    }

    /// <summary>Nor the Pokédex: these were already in the box before anyone renamed them.</summary>
    [Fact]
    public void Repairing_does_not_register_anything_in_the_pokedex()
    {
        var game = Save();
        var before = Enumerable.Range(1, 807).Count(sp => game.GetSeen((ushort)sp));

        SaveNameRepair.Apply(game);

        Assert.Equal(before, Enumerable.Range(1, 807).Count(sp => game.GetSeen((ushort)sp)));
    }

    /// <summary>The party is walked too: the level 100 in the team was nameless as well.</summary>
    [Fact]
    public void The_party_is_repaired_as_well_as_the_boxes()
    {
        var game = Save();
        var partner = PokemonBuilder.Build(new NewPokemon(717, 100, 0, 0, [31, 31, 31, 31, 31, 31], false), game);
        partner.Nickname = string.Empty;
        partner.RefreshChecksum();
        game.SetPartySlotAtIndex(partner, 0);

        var (total, named) = SaveNameRepair.Apply(game);

        Assert.Equal(3, total);
        Assert.Equal(2, named.Count);
        Assert.Contains(named, line => line.Contains("equipo, puesto 1") && line.Contains("Yveltal"));
        Assert.Equal("Yveltal", ((PK7)game.GetPartySlotAtIndex(0)).Nickname);
    }
}
