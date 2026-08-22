using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Writing effort values into a save.
/// </summary>
/// <remarks>
/// Against a save built here, not against the player's own: it needs no emulator, no ROM and no
/// personal data. It goes through PKHeX and through the real edit; what it cannot cover is the
/// round trip through a file, because PKHeX does not recognise a blank save written back to disk.
/// </remarks>
public class SaveEvTrainerTests
{
    [Fact]
    public void The_six_values_land_on_the_pokemon_in_the_box()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, box: 3, slot: 7);

        var result = Trainer().ApplyTo(save, Change(box: 3, slot: 7, pikachu.PID, [252, 0, 0, 252, 4, 0]));

        Assert.True(result.Delivered);

        var written = (PK7)save.GetBoxSlotAtIndex(3, 7)!;
        Assert.Equal(252, written.EV_HP);
        Assert.Equal(0, written.EV_ATK);
        Assert.Equal(252, written.EV_SPA);
        Assert.Equal(4, written.EV_SPD);
    }

    /// <summary>
    /// The party is a different store in the save, so writing to it has to reach the party and not
    /// box zero — which is exactly what a sentinel of -1 would land on if nobody checked.
    /// </summary>
    [Fact]
    public void A_party_member_is_written_to_the_party_and_not_to_a_box()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetPartySlotAtIndex(pikachu, 0);

        var result = Trainer().ApplyTo(save,
            Change(BoxedPokemon.PartyBox, slot: 0, pikachu.PID, [4, 252, 0, 0, 0, 252]));

        Assert.True(result.Delivered);

        var written = (PK7)save.GetPartySlotAtIndex(0);
        Assert.Equal(4, written.EV_HP);
        Assert.Equal(252, written.EV_ATK);
        Assert.Equal(252, written.EV_SPE);

        // Y la caja 0 sigue vacía: el -1 no se ha convertido en un 0 por el camino.
        Assert.Null(save.GetBoxSlotAtIndex(0, 0) is { Species: > 0 } ? "algo" : null);
    }

    /// <summary>
    /// The battle stats a party member carries are the game's, and stay untouched.
    /// </summary>
    /// <remarks>
    /// Recomputing them looked like the tidy thing to do, and is wrong: PKHeX works a stat out from
    /// its own table of base stats, and this competition is played on a ROM with the base stats
    /// shuffled. Measured on a copy of the real partida, a Kommo-o with 168 HP came back with 151.
    /// The stat catches up on its own when the game next recalculates it.
    /// </remarks>
    [Fact]
    public void A_party_members_battle_stats_are_left_exactly_as_the_game_wrote_them()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetPartySlotAtIndex(pikachu, 0);

        var before = (PK7)save.GetPartySlotAtIndex(0);
        int[] stored = [before.Stat_HPMax, before.Stat_ATK, before.Stat_DEF,
            before.Stat_SPA, before.Stat_SPD, before.Stat_SPE];

        Trainer().ApplyTo(save, Change(BoxedPokemon.PartyBox, 0, pikachu.PID, [0, 0, 0, 0, 0, 252]));

        var after = (PK7)save.GetPartySlotAtIndex(0);
        int[] now = [after.Stat_HPMax, after.Stat_ATK, after.Stat_DEF,
            after.Stat_SPA, after.Stat_SPD, after.Stat_SPE];

        Assert.Equal(stored, now);
        Assert.Equal(252, after.EV_SPE);
    }

    /// <summary>
    /// The PID is the identity that survives nicknames, levels and evolutions. If the slot holds a
    /// different Pokémon the partida has moved on, and writing would train the wrong one.
    /// </summary>
    [Fact]
    public void A_slot_holding_a_different_pokemon_is_refused_and_nothing_is_written()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, box: 0, slot: 0);

        var result = Trainer().ApplyTo(save, Change(0, 0, pikachu.PID + 1, [252, 252, 0, 0, 0, 0]));

        Assert.False(result.Delivered);
        Assert.Equal(DeliveryOutcome.SlotChanged, result.Outcome);
        Assert.Equal(0, ((PK7)save.GetBoxSlotAtIndex(0, 0)!).EV_HP);
    }

    [Fact]
    public void An_empty_slot_is_refused_rather_than_filled()
    {
        var save = new SAV7USUM();

        var result = Trainer().ApplyTo(save, Change(0, 0, 0xDEADBEEF, [4, 4, 4, 4, 4, 4]));

        Assert.False(result.Delivered);
        Assert.Equal(DeliveryOutcome.SlotChanged, result.Outcome);
    }

    /// <summary>
    /// §42: PKHeX treats putting a Pokémon into a slot as <em>acquiring</em> it and, left at the
    /// default, bumps captures, Poké Balls used and wild battles. Editing EVs is none of those,
    /// and the run that learned this the hard way gained a hundred and fifty of each.
    /// </summary>
    [Fact]
    public void Training_does_not_touch_the_trainer_card_counters()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, box: 0, slot: 0);

        var captures = save.Records.GetRecord(0);
        var balls = save.Records.GetRecord(1);
        var wild = save.Records.GetRecord(2);

        Trainer().ApplyTo(save, Change(0, 0, pikachu.PID, [252, 0, 0, 0, 0, 252]));

        Assert.Equal(captures, save.Records.GetRecord(0));
        Assert.Equal(balls, save.Records.GetRecord(1));
        Assert.Equal(wild, save.Records.GetRecord(2));
    }

    /// <summary>The message has to say where it happened, in the words the game uses.</summary>
    [Fact]
    public void The_report_says_where_the_pokemon_was()
    {
        Assert.Equal("la caja 5, hueco 12", Change(4, 11, 1, []).Where);
        Assert.Equal("el equipo, puesto 3", Change(BoxedPokemon.PartyBox, 2, 1, []).Where);
    }

    private static EvChange Change(int box, int slot, uint pid, int[] evs) =>
        new(box, slot, pid, "Pikachu", evs);

    private static PK7 Pikachu(SAV7USUM save)
    {
        var pokemon = new PK7
        {
            Species = (ushort)Species.Pikachu,
            CurrentLevel = 50,
            Nature = Nature.Adamant,
            Ability = (int)PKHeX.Core.Ability.Static,
            AbilityNumber = 1,
            Ball = (byte)PKHeX.Core.Ball.Poke,
            OriginalTrainerName = "GRENIN",
            Language = save.Language,
            Version = save.Version,
            PID = 0x1234ABCD,
            IV_HP = 31,
            IV_ATK = 31,
            Move1 = (ushort)Move.Thunderbolt,
        };

        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private static SaveEvTrainer Trainer() =>
        new(save: null!, Path.GetTempPath(), NullLogger<SaveEvTrainer>.Instance);
}
