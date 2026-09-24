using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The numbers the EV training screen promises: they have to come from the installed world's table
/// or not come at all.
/// </summary>
/// <remarks>
/// <see cref="WorldLimits"/> is global state, so every case that sets it leaves it empty again; the
/// assembly runs its classes one at a time for the same reason (see Parallelism.cs).
/// </remarks>
[Collection("WorldLimits")]
public sealed class WorldStatForecastTests : IDisposable
{
    private const int Species = 25;

    /// <summary>Miedosa: raises Velocidad and lowers Ataque.</summary>
    /// <remarks>
    /// 10 / 5 = 2 raised and 10 % 5 = 0 lowered, over the nature's own order Ataque, Defensa,
    /// VELOCIDAD, At. Esp., Def. Esp. -- so it is the case that catches Velocidad being read as the
    /// third stat of the summary screen, which is Defensa.
    /// </remarks>
    private const int Timid = 10;

    private readonly WorldStatForecast _forecast = new();

    public void Dispose()
    {
        WorldLimits.BaseStats = [];
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();
    }

    private static void WorldSays(int species, params byte[] bases)
    {
        var table = new byte[(species + 1) * 6];
        bases.CopyTo(table, species * 6);
        WorldLimits.BaseStats = table;
    }

    private static int[] Six(int value) => [.. Enumerable.Repeat(value, 6)];

    /// <summary>
    /// A party member's stats are worked out at the level stored next to them, which is the one the EV
    /// writer uses. Base 100, perfect IVs, no effort: 175 PS at 50, and (200 + 31)·45/100 + 45 + 10 = 158 at 45.
    /// </summary>
    [Fact]
    public void A_party_member_is_projected_at_the_level_its_stats_are_stored_at()
    {
        WorldSays(Species, 100, 100, 100, 100, 100, 100);

        var drifted = Pokemon(level: 50) with { StatLevel = 45 };

        Assert.Equal(158, _forecast.With(drifted, Six(0))![0]);
    }

    private static BoxedPokemon Pokemon(int species = Species, int level = 50, int nature = 0, int form = 0,
        bool isEgg = false) => new(
        Box: BoxedPokemon.PartyBox, Slot: 0, Species: species, Form: form, SpeciesName: "Prueba", Nickname: "",
        Level: level, IsShiny: false, IsEgg: isEgg, GenderMark: "", NatureName: "", AbilityName: "",
        HeldItemName: "", BallName: "", TrainerName: "", MetLocationName: "", MetLevel: 1, Moves: [],
        Stats: Six(0), Ivs: Six(31), Evs: Six(0), Friendship: 0, Pid: 1, Nature: nature);

    [Fact]
    public void Without_the_worlds_table_there_is_no_number()
    {
        WorldLimits.BaseStats = [];

        Assert.Null(_forecast.With(Pokemon(), Six(0)));
    }

    /// <summary>
    /// Base 100 everywhere at level 50 with perfect IVs: 175 PS and 120 in the rest with no effort;
    /// 252 in Velocidad makes the core (200 + 31 + 63)·50/100 = 147, so 152.
    /// </summary>
    [Fact]
    public void It_is_the_same_formula_the_writer_uses()
    {
        WorldSays(Species, 100, 100, 100, 100, 100, 100);

        Assert.Equal([175, 120, 120, 120, 120, 120], _forecast.With(Pokemon(), Six(0)));
        Assert.Equal([175, 120, 120, 120, 120, 152], _forecast.With(Pokemon(), [0, 0, 0, 0, 0, 252]));
    }

    /// <summary>
    /// The table is this world's, not the series': a shuffled Pikachu with base 5 in Velocidad
    /// comes out at (10 + 31)·50/100 + 5 = 25, which PKHeX's table would never say.
    /// </summary>
    [Fact]
    public void The_base_stats_are_the_installed_worlds()
    {
        WorldSays(Species, 35, 55, 40, 50, 50, 5);

        Assert.Equal(25, _forecast.With(Pokemon(), Six(0))![5]);
    }

    [Fact]
    public void An_egg_has_no_stats_to_promise()
    {
        WorldSays(Species, 100, 100, 100, 100, 100, 100);

        Assert.Null(_forecast.With(Pokemon(isEgg: true), Six(0)));
    }

    /// <summary>
    /// A form with no row of its own is built like its species, which is the game's rule. The ones with their
    /// own row are covered in RegionalFormStatsTests.
    /// </summary>
    [Fact]
    public void A_form_without_a_row_of_its_own_is_built_like_its_species()
    {
        WorldSays(Species, 100, 100, 100, 100, 100, 100);

        Assert.Equal(_forecast.With(Pokemon(form: 0), Six(0)), _forecast.With(Pokemon(form: 1), Six(0)));
    }

    [Fact]
    public void An_unknown_nature_is_not_read_as_a_neutral_one()
    {
        WorldSays(Species, 100, 100, 100, 100, 100, 100);

        Assert.Null(_forecast.With(Pokemon(nature: -1), Six(0)));
    }

    /// <summary>Summary screen order: PS, Ataque, Defensa, At. Esp., Def. Esp., Velocidad.</summary>
    [Fact]
    public void The_nature_marks_the_stats_of_the_summary_screen()
    {
        var timid = Pokemon(nature: Timid);

        Assert.Equal(0, _forecast.NatureEffect(timid, 0));
        Assert.Equal(-1, _forecast.NatureEffect(timid, 1));
        Assert.Equal(0, _forecast.NatureEffect(timid, 2));
        Assert.Equal(0, _forecast.NatureEffect(timid, 3));
        Assert.Equal(0, _forecast.NatureEffect(timid, 4));
        Assert.Equal(1, _forecast.NatureEffect(timid, 5));
    }

    [Fact]
    public void A_neutral_or_unknown_nature_marks_nothing()
    {
        for (var stat = 0; stat < 6; stat++)
        {
            Assert.Equal(0, _forecast.NatureEffect(Pokemon(nature: 0), stat));
            Assert.Equal(0, _forecast.NatureEffect(Pokemon(nature: -1), stat));
        }
    }
}
