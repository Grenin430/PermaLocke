using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The wonder trade's write: one Pokémon out, another into the same slot.
/// </summary>
/// <remarks>
/// What is guarded is that the party and the boxes stay separate stores. The party index is the
/// sentinel -1, so a swap that forgot to check it would land on <b>box zero, slot N</b> and destroy
/// a Pokémon nobody chose — and the trade would report success, because something would indeed have
/// been written. Against a save built here: no emulator, no ROM, nobody's partida.
/// </remarks>
public sealed class SaveBoxSwapTests
{
    private static PK7 Pokemon(SAV7USUM save, ushort species, byte level)
    {
        var pokemon = new PK7
        {
            Species = species,
            CurrentLevel = level,
            OriginalTrainerName = save.OT,
            TID16 = save.TID16,
            SID16 = save.SID16,
            Language = (int)LanguageID.Spanish,
            Version = save.Version,
        };

        pokemon.RefreshChecksum();
        return pokemon;
    }

    /// <summary>An offer that hands over <paramref name="given"/> and returns species 25.</summary>
    private static WonderTradeOffer Offer(int given) => new(
        GivenSpecies: given, GivenName: "El que se va", GivenBaseStatTotal: 300,
        Species: 25, Name: "Pikachu", BaseStatTotal: 320, Generation: 1,
        Types: new TypePair(13, "Eléctrico", 13, "Eléctrico"), Legendary: false, Level: 12, IsShiny: false,
        Ivs: [31, 0, 31, 0, 31, 0], Nature: 0, NatureName: "Fuerte",
        AbilityId: 9, Ability: "Electricidad Estática",
        MinBaseStatTotal: 276, MaxBaseStatTotal: 360, Seed: 1, Number: 1);

    /// <summary>
    /// The one that matters: a party index reaches the party, and box zero is not touched.
    /// </summary>
    [Fact]
    public void A_party_member_is_swapped_in_the_party_and_box_zero_is_untouched()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Pokemon(save, 133, 20), 0);

        var bystander = Pokemon(save, 1, 5);
        save.SetBoxSlotAtIndex(bystander, box: 0, slot: 0);

        SaveBoxSwap.ApplyTo(save, Offer(given: 133), BoxedPokemon.PartyBox, 0);

        Assert.Equal(25, save.GetPartySlotAtIndex(0)!.Species);
        Assert.Equal(1, save.GetBoxSlotAtIndex(0, 0)!.Species);
    }

    [Fact]
    public void A_boxed_pokemon_is_swapped_in_its_box()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Pokemon(save, 133, 20), box: 3, slot: 7);

        SaveBoxSwap.ApplyTo(save, Offer(given: 133), 3, 7);

        Assert.Equal(25, save.GetBoxSlotAtIndex(3, 7)!.Species);
    }

    /// <summary>Reading has to follow the same fork, or the guard would check the wrong slot.</summary>
    [Fact]
    public void Reading_a_party_index_reads_the_party()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Pokemon(save, 133, 20), 0);
        save.SetBoxSlotAtIndex(Pokemon(save, 1, 5), box: 0, slot: 0);

        Assert.Equal(133, SaveBoxSwap.Read(save, BoxedPokemon.PartyBox, 0)!.Species);
        Assert.Equal(1, SaveBoxSwap.Read(save, 0, 0)!.Species);
    }

    /// <summary>Past the end of the party there is nobody, rather than whatever the bytes hold.</summary>
    [Fact]
    public void An_empty_party_slot_reads_as_nobody()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Pokemon(save, 133, 20), 0);

        Assert.Null(SaveBoxSwap.Read(save, BoxedPokemon.PartyBox, 5));
        Assert.Null(SaveBoxSwap.Read(save, BoxedPokemon.PartyBox, -1));
    }

    /// <summary>The one that arrives keeps the level the offer promised.</summary>
    [Fact]
    public void What_arrives_is_what_the_offer_said()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Pokemon(save, 133, 20), 0);

        var received = SaveBoxSwap.ApplyTo(save, Offer(given: 133), BoxedPokemon.PartyBox, 0);

        Assert.Equal(25, received.Species);
        Assert.Equal(12, received.CurrentLevel);
        Assert.NotEqual(0u, received.PID);
    }
}
