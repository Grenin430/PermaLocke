using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// A regional form is recomputed from its own base stats, not from its species' (§131).
/// </summary>
/// <remarks>
/// The world here is Raichu and Alolan Raichu with their series base stats, which differ in five of six: an
/// Alolan Raichu worked out from the Raichu row comes out with the wrong Ataque, Defensa, At. Esp. and Def. Esp.
/// <see cref="WorldLimits"/> is global, so it is emptied after every case.
/// </remarks>
[Collection("WorldLimits")]
public sealed class RegionalFormStatsTests : IDisposable
{
    private const int Raichu = 26;

    /// <summary>Firme: raises Ataque and lowers At. Esp.</summary>
    private const int Adamant = 3;

    private static readonly byte[] RaichuBases = [60, 90, 55, 90, 80, 110];
    private static readonly byte[] AlolanBases = [60, 85, 50, 95, 85, 110];

    private static readonly int[] Ivs = [31, 20, 15, 10, 5, 0];
    private static readonly int[] Evs = [0, 252, 0, 0, 4, 252];

    public RegionalFormStatsTests()
    {
        var table = new byte[(Raichu + 1) * 6];
        RaichuBases.CopyTo(table, Raichu * 6);

        WorldLimits.BaseStats = table;
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]> { [(Raichu, 1)] = AlolanBases };
    }

    public void Dispose()
    {
        WorldLimits.BaseStats = [];
        WorldLimits.FormBaseStats = new Dictionary<(int Species, int Form), byte[]>();
    }

    /// <summary>Raising the HP maximum never raises the current HP: Ultra Moon calls that save corrupted (2026-09-27).</summary>
    [Fact]
    public void More_max_hp_leaves_the_current_hp_where_it_was()
    {
        var raichu = new PK7 { Species = Raichu, CurrentLevel = 50, Stat_Level = 50, Nature = (Nature)Adamant, Stat_HPMax = 100, Stat_HPCurrent = 80 };

        Assert.True(StatCalculator.Restat(raichu));

        Assert.True(raichu.Stat_HPMax > 100);
        Assert.Equal(80, raichu.Stat_HPCurrent);
    }

    [Fact]
    public void The_world_answers_by_species_and_form()
    {
        Assert.Equal(RaichuBases, WorldLimits.BaseStatsOf(Raichu, 0));
        Assert.Equal(AlolanBases, WorldLimits.BaseStatsOf(Raichu, 1));

        // Una forma sin fila propia está hecha como su especie: la regla del juego, no un hueco.
        Assert.Equal(RaichuBases, WorldLimits.BaseStatsOf(Raichu, 2));
    }

    /// <summary>The bug: the writer used to read the species row whatever the form.</summary>
    [Fact]
    public void The_ev_writer_recomputes_an_alolan_raichu_from_its_own_row()
    {
        var save = new SAV7USUM();
        var raichu = Party(save, form: 1);

        var result = Writer().ApplyTo(save, new EvChange(BoxedPokemon.PartyBox, 0, raichu.PID, "Raichu", Evs));

        Assert.True(result.Delivered);

        var written = (PK7)save.GetPartySlotAtIndex(0);
        int[] stats = [written.Stat_HPMax, written.Stat_ATK, written.Stat_DEF,
            written.Stat_SPA, written.Stat_SPD, written.Stat_SPE];

        Assert.Equal(StatCalculator.Compute(AlolanBases, Ivs, Evs, 50, Adamant), stats);
        Assert.NotEqual(StatCalculator.Compute(RaichuBases, Ivs, Evs, 50, Adamant), stats);
    }

    [Fact]
    public void The_normal_form_still_reads_the_species_row()
    {
        var save = new SAV7USUM();
        var raichu = Party(save, form: 0);

        Writer().ApplyTo(save, new EvChange(BoxedPokemon.PartyBox, 0, raichu.PID, "Raichu", Evs));

        var written = (PK7)save.GetPartySlotAtIndex(0);

        Assert.Equal(StatCalculator.Compute(RaichuBases, Ivs, Evs, 50, Adamant)[1], written.Stat_ATK);
    }

    /// <summary>What the training screen promises is what the writer puts on disk, form included.</summary>
    [Fact]
    public void The_forecast_projects_an_alolan_raichu_from_its_own_row()
    {
        var pokemon = new BoxedPokemon(
            Box: BoxedPokemon.PartyBox, Slot: 0, Species: Raichu, Form: 1, SpeciesName: "Raichu", Nickname: "",
            Level: 50, IsShiny: false, IsEgg: false, GenderMark: "", NatureName: "", AbilityName: "",
            HeldItemName: "", BallName: "", TrainerName: "", MetLocationName: "", MetLevel: 1, Moves: [],
            Stats: new int[6], Ivs: Ivs, Evs: new int[6], Friendship: 0, Pid: 1, Nature: Adamant);

        Assert.Equal(StatCalculator.Compute(AlolanBases, Ivs, Evs, 50, Adamant), new WorldStatForecast().With(pokemon, Evs));
    }

    private static PK7 Party(SAV7USUM save, byte form)
    {
        var pokemon = new PK7
        {
            Species = Raichu,
            Form = form,
            CurrentLevel = 50,
            Nature = (Nature)Adamant,
            Language = save.Language,
            Version = save.Version,
            OriginalTrainerName = "GRENIN",
            PID = 0x0BADF00D,
            IV_HP = Ivs[0],
            IV_ATK = Ivs[1],
            IV_DEF = Ivs[2],
            IV_SPA = Ivs[3],
            IV_SPD = Ivs[4],
            IV_SPE = Ivs[5],
        };

        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();
        save.SetPartySlotAtIndex(pokemon, 0);
        return pokemon;
    }

    private static SaveEvTrainer Writer() =>
        new(save: null!, Path.GetTempPath(), NullLogger<SaveEvTrainer>.Instance);
}
