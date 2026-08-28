using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Writing a mega into a trainer's party.
/// </summary>
/// <remarks>
/// A mega is a <b>form</b> of its species, so what makes a boss arrive already mega evolved is one
/// byte at 0x12. The rest of the randomizer clears that byte on purpose — a form valid for the old
/// species need not exist in the new one — so the two overloads have to stay clearly different, and
/// a mistake here would show the wrong Pokémon without failing anywhere.
/// </remarks>
public sealed class MegaFormWriteTests
{
    private static byte[] Party(int slots) => new byte[slots * TrainerPokemonTable.EntrySize];

    [Fact]
    public void Writing_a_species_with_a_form_keeps_the_form()
    {
        var party = Party(3);

        TrainerPokemonTable.SetSpecies(party, 1, species: 6, form: 2);

        Assert.Equal(6, TrainerPokemonTable.GetSpecies(party, 1));
        Assert.Equal(2, TrainerPokemonTable.GetForm(party, 1));
    }

    /// <summary>The plain overload still clears it, which is what every other module wants.</summary>
    [Fact]
    public void Writing_a_species_without_a_form_clears_it()
    {
        var party = Party(2);
        TrainerPokemonTable.SetSpecies(party, 0, species: 6, form: 2);

        TrainerPokemonTable.SetSpecies(party, 0, species: 25);

        Assert.Equal(25, TrainerPokemonTable.GetSpecies(party, 0));
        Assert.Equal(0, TrainerPokemonTable.GetForm(party, 0));
    }

    /// <summary>One slot's form must not bleed into its neighbours.</summary>
    [Fact]
    public void Only_the_slot_written_changes()
    {
        var party = Party(4);

        TrainerPokemonTable.SetSpecies(party, 2, species: 150, form: 1);

        foreach (var other in (int[])[0, 1, 3])
        {
            Assert.Equal(0, TrainerPokemonTable.GetSpecies(party, other));
            Assert.Equal(0, TrainerPokemonTable.GetForm(party, other));
        }
    }
}
