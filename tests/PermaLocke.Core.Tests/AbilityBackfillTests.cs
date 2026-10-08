using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>What ability a Pokémon was given, worked out from the history for those dealt before the ledger (§237).</summary>
public sealed class AbilityBackfillTests
{
    private static readonly string[] Names = ["", "Hedor", "Llovizna", "Gran Encanto", "Espada Indómita", "Repetida", "Repetida"];

    private static PokemonEntry Entry(Guid id, uint? pid) => new()
    {
        Id = id, RunId = Guid.NewGuid(), Species = 214, SpeciesName = "Heracross", Origin = PokemonOrigin.Gacha,
        EncounterType = EncounterType.Special, Pid = pid
    };

    private static GameEvent Dealt(GameEventType type, Guid? pokemon, string? ability, int minutes = 0) => new()
    {
        Id = Guid.NewGuid(), RunId = Guid.NewGuid(), Timestamp = new DateTimeOffset(2026, 10, 8, 0, minutes, 0, TimeSpan.Zero),
        Type = type, Source = EventSource.Player, Actor = "x", Description = "x", PokemonId = pokemon,
        Data = ability is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["habilidad"] = ability }
    };

    [Fact]
    public void The_gacha_and_the_wonder_trade_say_which_ability_they_gave_and_only_a_known_unique_name_counts()
    {
        Guid heracross = Guid.NewGuid(), lost = Guid.NewGuid(), twice = Guid.NewGuid(), noPid = Guid.NewGuid(), other = Guid.NewGuid();
        var entries = new[] { Entry(heracross, 0xC6AA4BB0), Entry(lost, 0x11), Entry(twice, 0x22), Entry(noPid, null), Entry(other, 0x33) };

        var found = AbilityBackfill.Intended(
        [
            Dealt(GameEventType.GachaRoll, heracross, "Espada Indómita"),
            Dealt(GameEventType.GachaRoll, lost, "Nombre que el mundo no tiene"),    // desconocida: se deja
            Dealt(GameEventType.WonderTrade, twice, "Repetida"),                    // dos con el mismo nombre: no se adivina
            Dealt(GameEventType.GachaRoll, noPid, "Hedor"),                         // sin PID no hay a quién devolverla
            Dealt(GameEventType.PokemonCaught, other, "Llovizna"),                  // una captura no reparte habilidades
            Dealt(GameEventType.GachaRoll, null, "Hedor")
        ], entries, Names);

        Assert.Equal(new Dictionary<uint, int> { [0xC6AA4BB0] = 4 }, found);
    }

    [Fact]
    public void The_latest_deal_of_a_pokemon_wins()
    {
        var id = Guid.NewGuid();

        var found = AbilityBackfill.Intended(
        [
            Dealt(GameEventType.WonderTrade, id, "Llovizna", minutes: 30),
            Dealt(GameEventType.GachaRoll, id, "Hedor", minutes: 5)
        ], [Entry(id, 0xAB)], Names);

        Assert.Equal(2, found[0xAB]);
    }

    [Fact]
    public void A_run_whose_roulette_changed_abilities_is_not_guessed_at_all()
    {
        var id = Guid.NewGuid();
        var spun = Dealt(GameEventType.RouletteSpun, null, null, minutes: 40) with
        {
            Data = new Dictionary<string, string> { ["efecto"] = "HabilidadMala" }
        };

        // La ruleta no dice a quién: la habilidad del gacha podría ya no ser la vigente.
        Assert.Empty(AbilityBackfill.Intended([Dealt(GameEventType.GachaRoll, id, "Hedor"), spun], [Entry(id, 0xAB)], Names));

        // Una vuelta de la ruleta que no toca habilidades no impide deducirlas.
        var points = Dealt(GameEventType.RouletteSpun, null, null, minutes: 40) with
        {
            Data = new Dictionary<string, string> { ["efecto"] = "Puntos" }
        };
        Assert.Single(AbilityBackfill.Intended([Dealt(GameEventType.GachaRoll, id, "Hedor"), points], [Entry(id, 0xAB)], Names));
    }
}
