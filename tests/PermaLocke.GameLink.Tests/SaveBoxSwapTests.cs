using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>The album's one-for-one wonder trade (2026-10-07; before, two cards for one), on a save built here: no emulator, no partida.</summary>
public class SaveBoxSwapTests
{
    private static PK7 Mon(SAV7USUM save, Species species, uint pid, int level)
    {
        var pokemon = new PK7
        {
            Species = (ushort)species, CurrentLevel = (byte)level, OriginalTrainerName = "GRENIN",
            Language = save.Language, Version = save.Version, PID = pid, Move1 = (ushort)Move.Tackle
        };
        pokemon.ClearNickname();
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private static WonderTradeOffer Offer(int givenSpecies, int level) => new(
        givenSpecies, "Rattata", 253, (int)Species.Lucario, "Lucario", 525, 4, new TypePair(1, "Lucha", 8, "Acero"),
        false, level, false, [31, 31, 31, 31, 31, 31], 3, "Firme", 1, "Impasible", 230, 270, 7, 0);

    [Fact]
    public void From_a_box_the_new_one_takes_the_slot_of_the_one_that_goes()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Mon(save, Species.Rattata, 0x11111111, 12), 0, 0);
        save.SetBoxSlotAtIndex(Mon(save, Species.Pidgey, 0x22222222, 15), 2, 5);

        var received = SaveBoxSwap.ApplyTo(save, Offer((int)Species.Rattata, 12), 0, 0);

        Assert.Equal((ushort)Species.Lucario, save.GetBoxSlotAtIndex(0, 0).Species);
        Assert.Equal(received.PID, save.GetBoxSlotAtIndex(0, 0).PID);
        Assert.NotEqual(0x11111111u, save.GetBoxSlotAtIndex(0, 0).PID);

        // Lo demás no se toca.
        Assert.Equal((ushort)Species.Pidgey, save.GetBoxSlotAtIndex(2, 5).Species);
    }

    [Fact]
    public void From_the_party_the_new_one_takes_its_place_and_the_party_keeps_its_size()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Mon(save, Species.Pidgey, 0x22222222, 15), 0);
        save.SetPartySlotAtIndex(Mon(save, Species.Rattata, 0x11111111, 12), 1);
        save.SetPartySlotAtIndex(Mon(save, Species.Caterpie, 0x33333333, 9), 2);

        SaveBoxSwap.ApplyTo(save, Offer((int)Species.Rattata, 12), BoxedPokemon.PartyBox, 1);

        Assert.Equal(3, save.PartyCount);
        Assert.Equal((ushort)Species.Pidgey, save.GetPartySlotAtIndex(0).Species);
        Assert.Equal((ushort)Species.Lucario, save.GetPartySlotAtIndex(1).Species);
        Assert.Equal((ushort)Species.Caterpie, save.GetPartySlotAtIndex(2).Species);
        Assert.DoesNotContain(save.PartyData, p => p.PID == 0x11111111);
    }
}
