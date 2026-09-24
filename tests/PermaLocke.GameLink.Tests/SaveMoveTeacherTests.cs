using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Writing a remembered move into a save (§142), against a save built here: no emulator, no ROM, no partida.
/// </summary>
public class SaveMoveTeacherTests
{
    private const int Thunderbolt = (int)Move.Thunderbolt;
    private const int QuickAttack = (int)Move.QuickAttack;
    private const int Surf = (int)Move.Surf;

    /// <summary>The move lands in its slot with the PP it was given, and the replaced move's PP Ups go with it.</summary>
    [Fact]
    public void The_move_lands_in_its_slot_with_its_PP()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        pikachu.Move2_PPUps = 3;
        pikachu.RefreshChecksum();
        save.SetBoxSlotAtIndex(pikachu, box: 2, slot: 4);

        var result = Teacher().ApplyTo(save, Change(2, 4, pikachu.PID, moveSlot: 1, replaced: QuickAttack, Surf, 15));

        Assert.True(result.Delivered);

        var written = (PK7)save.GetBoxSlotAtIndex(2, 4)!;
        Assert.Equal(Thunderbolt, written.Move1);
        Assert.Equal(Surf, written.Move2);
        Assert.Equal(15, written.Move2_PP);
        Assert.Equal(0, written.Move2_PPUps);
        Assert.True(written.ChecksumValid);
    }

    /// <summary>The party is its own store; the -1 sentinel must reach it and not box zero.</summary>
    [Fact]
    public void A_party_member_is_written_in_the_party()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetPartySlotAtIndex(pikachu, 0);

        var result = Teacher().ApplyTo(save,
            Change(BoxedPokemon.PartyBox, 0, pikachu.PID, moveSlot: 2, replaced: 0, Surf, 15));

        Assert.True(result.Delivered);
        Assert.Equal(Surf, ((PK7)save.GetPartySlotAtIndex(0)).Move3);
        Assert.Null(save.GetBoxSlotAtIndex(0, 0) is { Species: > 0 } ? "algo" : null);
    }

    /// <summary>By PID and by nothing else (§96): another Pokémon in the slot is left alone.</summary>
    [Fact]
    public void Another_pokemon_in_the_slot_is_left_alone()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, 0, 0);

        var result = Teacher().ApplyTo(save, Change(0, 0, pikachu.PID + 1, 1, QuickAttack, Surf, 15));

        Assert.Equal(DeliveryOutcome.SlotChanged, result.Outcome);
        Assert.Equal(QuickAttack, ((PK7)save.GetBoxSlotAtIndex(0, 0)!).Move2);
    }

    /// <summary>
    /// A move changed in the game since the screen read the partida is not forgotten on its behalf: the player
    /// never chose to lose it.
    /// </summary>
    [Fact]
    public void A_move_that_changed_since_it_was_read_is_not_overwritten()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, 0, 0);

        var result = Teacher().ApplyTo(save, Change(0, 0, pikachu.PID, 1, replaced: Thunderbolt, Surf, 15));

        Assert.Equal(DeliveryOutcome.SlotChanged, result.Outcome);
        Assert.Equal(QuickAttack, ((PK7)save.GetBoxSlotAtIndex(0, 0)!).Move2);
    }

    /// <summary>A move it already knows in another slot would leave it knowing the same move twice.</summary>
    [Fact]
    public void A_move_it_already_knows_is_refused()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, 0, 0);

        var result = Teacher().ApplyTo(save, Change(0, 0, pikachu.PID, 1, QuickAttack, Thunderbolt, 15));

        Assert.False(result.Delivered);
    }

    /// <summary>Zero PP is a move the game will not let the player pick; it is never written.</summary>
    [Fact]
    public void A_move_without_PP_is_refused()
    {
        var save = new SAV7USUM();
        var pikachu = Pikachu(save);
        save.SetBoxSlotAtIndex(pikachu, 0, 0);

        var result = Teacher().ApplyTo(save, Change(0, 0, pikachu.PID, 1, QuickAttack, Surf, 0));

        Assert.False(result.Delivered);
        Assert.Equal(QuickAttack, ((PK7)save.GetBoxSlotAtIndex(0, 0)!).Move2);
    }

    private static MoveChange Change(int box, int slot, uint pid, int moveSlot, int replaced, int move, int pp) =>
        new(box, slot, pid, "Pikachu", moveSlot, replaced, move, pp);

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
            Move1 = (ushort)Move.Thunderbolt,
            Move2 = (ushort)Move.QuickAttack
        };

        pokemon.HealPP();
        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private static SaveMoveTeacher Teacher() =>
        new(save: null!, Path.GetTempPath(), NullLogger<SaveMoveTeacher>.Instance);
}
