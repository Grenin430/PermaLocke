using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

public class TrainerPokemonTableTests
{
    private static byte[] MakeParty(int count)
    {
        var party = new byte[TrainerPokemonTable.EntrySize * count];
        for (var i = 0; i < party.Length; i++)
        {
            party[i] = (byte)(i * 11); // filler, so a stray write is visible
        }
        return party;
    }

    [Fact]
    public void A_party_holds_one_entry_per_pokemon()
    {
        Assert.Equal(0x20, TrainerPokemonTable.EntrySize);
        Assert.Equal(6, TrainerPokemonTable.Count(new byte[0x20 * 6]));
    }

    /// <summary>
    /// One trpoke subfile in the cartridge is six bytes long. It must read as an empty party
    /// rather than throw, or the randomizer dies on trainer number whatever-it-is.
    /// </summary>
    [Fact]
    public void A_subfile_too_short_for_one_entry_is_an_empty_party()
    {
        Assert.Equal(0, TrainerPokemonTable.Count(new byte[6]));
    }

    [Fact]
    public void Species_survives_a_round_trip_and_clears_the_form()
    {
        var party = MakeParty(2);
        TrainerPokemonTable.SetSpecies(party, 1, 448);

        Assert.Equal(448, TrainerPokemonTable.GetSpecies(party, 1));
        Assert.Equal(0, TrainerPokemonTable.GetForm(party, 1));
    }

    /// <summary>
    /// The level is what the competition's caps are read from. Changing a species must never
    /// move it.
    /// </summary>
    [Fact]
    public void Replacing_a_species_leaves_the_level_alone()
    {
        var party = MakeParty(1);
        var level = TrainerPokemonTable.GetLevel(party, 0);

        TrainerPokemonTable.SetSpecies(party, 0, 129);

        Assert.Equal(level, TrainerPokemonTable.GetLevel(party, 0));
    }

    [Fact]
    public void Replacing_a_species_leaves_the_held_item_alone()
    {
        var party = MakeParty(1);
        var item = TrainerPokemonTable.GetItem(party, 0);

        TrainerPokemonTable.SetSpecies(party, 0, 129);

        Assert.Equal(item, TrainerPokemonTable.GetItem(party, 0));
    }

    [Fact]
    public void Explicit_moves_are_detected_and_can_be_handed_back_to_the_game()
    {
        var party = new byte[TrainerPokemonTable.EntrySize];
        Assert.False(TrainerPokemonTable.HasExplicitMoves(party, 0));

        BitConverter.GetBytes((ushort)85).CopyTo(party, 0x18 + 4); // third move slot
        Assert.True(TrainerPokemonTable.HasExplicitMoves(party, 0));

        TrainerPokemonTable.ClearMoves(party, 0);
        Assert.False(TrainerPokemonTable.HasExplicitMoves(party, 0));
    }

    [Fact]
    public void Clearing_moves_touches_only_the_four_move_slots()
    {
        var party = MakeParty(2);
        var before = (byte[])party.Clone();

        TrainerPokemonTable.ClearMoves(party, 0);

        for (var i = 0; i < party.Length; i++)
        {
            if (i >= 0x18 && i < 0x20)
            {
                continue; // the four move slots of entry 0
            }
            Assert.True(party[i] == before[i], $"el byte 0x{i:X} cambió y no es un movimiento");
        }
    }

    [Fact]
    public void Writing_one_entry_leaves_its_neighbours_alone()
    {
        var party = MakeParty(3);
        var before = (byte[])party.Clone();

        TrainerPokemonTable.SetSpecies(party, 1, 700);

        for (var i = 0; i < party.Length; i++)
        {
            var inEntry1 = i is >= 0x20 and < 0x40;
            var isSpeciesOrForm = i is 0x30 or 0x31 or 0x32;
            if (inEntry1 && isSpeciesOrForm)
            {
                continue;
            }
            Assert.True(party[i] == before[i], $"el byte 0x{i:X} cambió y no debía");
        }
    }
}
