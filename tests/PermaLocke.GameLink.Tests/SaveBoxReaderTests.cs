using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Reading the PC out of a save file.
/// </summary>
/// <remarks>
/// Against a save built here, not against the player's own: it needs no emulator, no ROM and no
/// personal data. It goes through PKHeX and through the real box walk; what it cannot cover is
/// opening a file, because PKHeX does not recognise a blank save written back to disk, and the
/// only save it would recognise is the player's own.
/// </remarks>
public class SaveBoxReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"permalocke-sav-{Guid.NewGuid()}.main");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_empty_pc_reads_as_the_party_plus_thirty_two_empty_boxes()
    {
        var snapshot = ReadBack(new SAV7USUM());

        Assert.True(snapshot.Available);
        Assert.Null(snapshot.Problem);
        Assert.Equal(33, snapshot.Boxes.Count);
        Assert.Equal(30, snapshot.SlotsPerBox);
        Assert.Equal(0, snapshot.Total);
    }

    /// <summary>
    /// The party comes first and is marked as such. It is another store of the save with six holes
    /// instead of thirty, and anything that writes has to pick the right one, so it cannot pass as
    /// a thirty-third box.
    /// </summary>
    [Fact]
    public void The_party_leads_the_list_and_says_it_is_the_party()
    {
        var snapshot = ReadBack(new SAV7USUM());

        var party = snapshot.Boxes[0];
        Assert.True(party.IsParty);
        Assert.Equal(0, party.Number);
        Assert.Equal(6, party.Slots);
        Assert.Same(party, snapshot.Party);

        Assert.All(snapshot.Boxes.Skip(1), box => Assert.False(box.IsParty));
        Assert.All(snapshot.Boxes.Skip(1), box => Assert.Equal(30, box.Slots));
    }

    /// <summary>
    /// A Pok�mon in the party is found there, carries the sentinel box, and does not turn up in
    /// the PC as well.
    /// </summary>
    [Fact]
    public void A_party_member_reads_from_the_party()
    {
        var save = new SAV7USUM();
        save.SetPartySlotAtIndex(Pikachu(save), 2);

        var snapshot = ReadBack(save);

        var found = Assert.Single(snapshot.Party!.Pokemon);
        Assert.Equal(BoxedPokemon.PartyBox, found.Box);
        Assert.True(found.IsInParty);
        Assert.Equal(2, found.Slot);
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(0, snapshot.Stored);
    }

    /// <summary>
    /// The box and slot have to survive the round trip: they are what the screen draws the grid
    /// with, and an off-by-one would put every Pokémon in the wrong hole.
    /// </summary>
    [Fact]
    public void A_pokemon_comes_back_from_the_box_and_slot_it_was_put_in()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Pikachu(save), box: 4, slot: 17);

        var snapshot = ReadBack(save);

        Assert.Equal(1, snapshot.Total);
        var found = Assert.Single(Box(snapshot, 5).Pokemon);
        Assert.Equal(4, found.Box);
        Assert.Equal(17, found.Slot);
        Assert.Equal((int)Species.Pikachu, found.Species);
        Assert.Equal(50, found.Level);
    }

    /// <summary>
    /// A boxed Pokémon carries no battle stats: the game works them out when it comes out. The
    /// reader has to do the same, or the detail panel would show six zeros.
    /// </summary>
    [Fact]
    public void Stats_are_worked_out_even_though_the_box_does_not_store_them()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Pikachu(save), box: 0, slot: 0);

        var found = Box(ReadBack(save), 1).Pokemon[0];

        Assert.All(found.Stats, stat => Assert.True(stat > 0, "una estadística salió a cero"));
        Assert.Equal(6, found.Ivs.Count);
        Assert.Equal(6, found.Evs.Count);
        Assert.Equal(31 * 2, found.IvTotal);
    }

    /// <summary>Ids resolved to text, so the screen needs no Pokédex of its own.</summary>
    [Fact]
    public void Everything_arrives_as_text_the_screen_can_show()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Pikachu(save), box: 0, slot: 0);

        var found = Box(ReadBack(save), 1).Pokemon[0];

        Assert.False(string.IsNullOrWhiteSpace(found.SpeciesName));
        Assert.False(string.IsNullOrWhiteSpace(found.NatureName));
        Assert.False(string.IsNullOrWhiteSpace(found.BallName));
        Assert.NotEmpty(found.Moves);
        Assert.DoesNotContain("?", found.SpeciesName);
    }

    /// <summary>Without a nickname the player's name for it is the species, not an empty label.</summary>
    [Fact]
    public void An_unnamed_pokemon_shows_its_species()
    {
        var save = new SAV7USUM();
        save.SetBoxSlotAtIndex(Pikachu(save), box: 0, slot: 0);

        var found = Box(ReadBack(save), 1).Pokemon[0];

        Assert.Equal(string.Empty, found.Nickname);
        Assert.Equal(found.SpeciesName, found.DisplayName);
    }

    [Fact]
    public void A_file_that_is_not_a_save_is_reported_and_not_guessed_at()
    {
        File.WriteAllText(_path, "esto no es una partida");

        var snapshot = Reader().ReadFrom(_path);

        Assert.False(snapshot.Available);
        Assert.NotNull(snapshot.Problem);
        Assert.Empty(snapshot.Boxes);
    }

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
            IV_HP = 31,
            IV_ATK = 31,
            Move1 = (ushort)Move.Thunderbolt,
        };

        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();
        return pokemon;
    }

    /// <summary>By number and not by position: the party sits at the head of the list.</summary>
    private static BoxContents Box(BoxSnapshot snapshot, int number) =>
        snapshot.Boxes.Single(box => box.Number == number);

    /// <summary>
    /// The form name POKE PASTE writes after the species, for Galar too: the gen 7 form table has no
    /// Galarian Meowth, so it has to be asked in the gen 9 context (§140).
    /// </summary>
    [Fact]
    public void A_regional_form_reads_with_the_name_showdown_gives_it()
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 1, SID16 = 2 };
        game.SetBoxSlotAtIndex(new PK7 { Species = 52, Form = 2, CurrentLevel = 10 }, 0, 0, PokemonBuilder.InPlace);
        game.SetBoxSlotAtIndex(new PK7 { Species = 37, Form = 1, CurrentLevel = 10 }, 0, 1, PokemonBuilder.InPlace);
        game.SetBoxSlotAtIndex(new PK7 { Species = 37, CurrentLevel = 10 }, 0, 2, PokemonBuilder.InPlace);

        var english = new SaveBoxReader(save: null!, new PkhexLocationLookup(), "en", NullLogger<SaveBoxReader>.Instance);
        var box = english.ReadFrom(game).Boxes[1].Pokemon;

        Assert.Equal("Galar", box[0].FormName);
        Assert.Equal("Alola", box[1].FormName);
        Assert.Equal(string.Empty, box[2].FormName);
    }

    private static BoxSnapshot ReadBack(SAV7USUM save) => Reader().ReadFrom(save);

    private static SaveBoxReader Reader() =>
        new(save: null!, new PkhexLocationLookup(), "es", NullLogger<SaveBoxReader>.Instance);
}
