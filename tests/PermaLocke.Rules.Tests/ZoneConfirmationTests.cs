using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;
using static PermaLocke.Rules.Tests.RuleTestContext;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// Saying which zone a capture spent, which is what the map's clicks do.
/// </summary>
/// <remarks>
/// The first test here is the one that was missing for the whole project. The first-encounter rule
/// had code, configuration and tests of its own and had still never fired once on the real run,
/// because the watcher files every automatic capture as <see cref="EncounterType.Unknown"/> and
/// Unknown is not in the list of types that spend a zone. Every existing test registered a
/// <see cref="EncounterType.Wild"/> capture by hand, so none of them ever went near the path the
/// application actually takes.
/// </remarks>
public sealed class ZoneConfirmationTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private static (EncounterService Service, FakeEvents Events, FakePokemon Pokemon) Build()
    {
        var events = new FakeEvents();
        var pokemon = new FakePokemon();

        return (new EncounterService(
            Engine(), pokemon, events, RulesConfiguration.Default,
            new NullEvolutionLineProvider(), new LevelCapTable([]), new RunContext(), new StepClock()),
            events, pokemon);
    }

    private static RegisterCaptureRequest Unknown(int species = 731, string name = "Pikipek",
        string location = "Ruta 1") =>
        new(species, name, location, EncounterType.Unknown, Level: 5);

    /// <summary>
    /// The measured defect: two automatic captures in the same zone, and nothing said.
    /// </summary>
    [Fact]
    public async Task Two_automatic_captures_in_one_zone_go_through_because_neither_spends_it()
    {
        var (service, _, pokemon) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi",
            source: EventSource.AutoDetect);
        var second = await service.RegisterAsync(RunId, Unknown(19, "Rattata"), "javi",
            source: EventSource.AutoDetect);

        Assert.True(first.Registered);
        Assert.True(second.Registered);

        // Registrar no es arbitrar, así que esto es correcto -- y por sí solo deja la regla muda.
        Assert.All(await pokemon.GetAllAsync(RunId), entry => Assert.False(entry.ConsumedZoneEncounter));
    }

    /// <summary>
    /// And once the player says so, the zone is spent and a <b>stated</b> capture there is refused.
    /// </summary>
    /// <remarks>
    /// The one refused is the stated capture — the dialog, where somebody looked at the encounter
    /// and said it was wild. An automatic one still registers, because the rule exits early for a
    /// type that spends no zone, and that is deliberate: a watcher that could be blocked from
    /// recording a Pokémon would lose that Pokémon's death, which is the hole §68 closed.
    /// </remarks>
    [Fact]
    public async Task Confirming_spends_the_zone_and_a_stated_capture_there_is_refused()
    {
        var (service, _, pokemon) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        var confirmed = await service.ConfirmZoneAsync(RunId, first.Pokemon!.Id, "Ruta 1", "javi");

        Assert.True(confirmed.Confirmed);
        Assert.True(confirmed.Pokemon!.ConsumedZoneEncounter);

        // Confirmar la zona ES decir que fue salvaje: es lo único que gasta un encuentro.
        Assert.Equal(EncounterType.Wild, confirmed.Pokemon.EncounterType);

        var stated = await service.RegisterAsync(RunId,
            new RegisterCaptureRequest(19, "Rattata", "Ruta 1", EncounterType.Wild, Level: 5), "javi");

        Assert.False(stated.Registered);
        Assert.True(stated.Evaluation.IsBlocked);
        Assert.Single(await pokemon.GetAllAsync(RunId));
    }

    /// <summary>
    /// An automatic capture in a spent zone still registers, and lands on the map as pending.
    /// </summary>
    /// <remarks>
    /// Deliberate, and the reason this is a map rather than a block: the alternative is a Pokémon
    /// the run never hears about, whose death is therefore never counted.
    /// </remarks>
    [Fact]
    public async Task An_automatic_capture_in_a_spent_zone_is_still_recorded()
    {
        var (service, _, pokemon) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        await service.ConfirmZoneAsync(RunId, first.Pokemon!.Id, "Ruta 1", "javi");

        var second = await service.RegisterAsync(RunId, Unknown(19, "Rattata"), "javi",
            source: EventSource.AutoDetect);

        Assert.True(second.Registered);
        Assert.False(second.Pokemon!.ConsumedZoneEncounter);
        Assert.Equal(2, (await pokemon.GetAllAsync(RunId)).Count);
    }

    /// <summary>The map refuses to spend a zone twice, which is the rule doing its job.</summary>
    [Fact]
    public async Task A_zone_already_spent_by_somebody_else_is_refused()
    {
        var (service, _, _) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        var second = await service.RegisterAsync(RunId, Unknown(19, "Rattata", "Ruta 2"), "javi",
            source: EventSource.AutoDetect);

        await service.ConfirmZoneAsync(RunId, first.Pokemon!.Id, "Ruta 1", "javi");
        var clash = await service.ConfirmZoneAsync(RunId, second.Pokemon!.Id, "Ruta 1", "javi");

        Assert.False(clash.Confirmed);
        Assert.Contains("Pikipek", clash.Reason);
    }

    /// <summary>Every confirmation leaves an event: it is a claim by the player, not a deduction.</summary>
    [Fact]
    public async Task Confirming_and_undoing_both_leave_an_event()
    {
        var (service, events, _) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        await service.ConfirmZoneAsync(RunId, first.Pokemon!.Id, "Ruta 1", "javi");
        await service.ClearZoneAsync(RunId, first.Pokemon.Id, "javi");

        var confirmed = Assert.Single(events.All, e => e.Type == GameEventType.ZoneConfirmed);
        Assert.Equal(EventSource.Player, confirmed.Source);
        Assert.Equal("ruta-1", confirmed.LocationId);
        Assert.Equal("Unknown", confirmed.Data["tipoAnterior"]);

        Assert.Single(events.All, e => e.Type == GameEventType.ZoneCleared);
    }

    /// <summary>Undoing frees the zone for somebody else, which is the point of undoing.</summary>
    [Fact]
    public async Task Undoing_frees_the_zone()
    {
        var (service, _, _) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        var second = await service.RegisterAsync(RunId, Unknown(19, "Rattata", "Ruta 2"), "javi",
            source: EventSource.AutoDetect);

        await service.ConfirmZoneAsync(RunId, first.Pokemon!.Id, "Ruta 1", "javi");
        await service.ClearZoneAsync(RunId, first.Pokemon.Id, "javi");

        var again = await service.ConfirmZoneAsync(RunId, second.Pokemon!.Id, "Ruta 1", "javi");

        Assert.True(again.Confirmed);
    }

    /// <summary>
    /// A type somebody actually looked at is not overwritten. Confirming says which zone was
    /// spent; it does not know better than the person who said the encounter was a gift.
    /// </summary>
    [Fact]
    public async Task A_stated_encounter_type_survives_confirmation()
    {
        var (service, _, _) = Build();

        var gift = await service.RegisterAsync(RunId,
            new RegisterCaptureRequest(722, "Rowlet", "Pueblo Lilii", EncounterType.Gift, Level: 5),
            "javi");

        var confirmed = await service.ConfirmZoneAsync(RunId, gift.Pokemon!.Id, "Pueblo Lilii", "javi");

        Assert.True(confirmed.Confirmed);
        Assert.Equal(EncounterType.Gift, confirmed.Pokemon!.EncounterType);
    }

    /// <summary>
    /// A map cell's name has to produce exactly the id the run already holds.
    /// </summary>
    /// <remarks>
    /// These four ids are taken from the real run's database. The map is drawn from PKHeX's
    /// location list for this reason: the cartridge's own zone table says "Ciudad Hauoli" where
    /// PKHeX says "Ciudad Hauoli (Zona Comercial)", and a map built from the short names would
    /// have produced <c>ciudad-hauoli</c> for a capture recorded as
    /// <c>ciudad-hauoli-zona-comercial</c> — a click that silently matches nothing.
    /// </remarks>
    [Theory]
    [InlineData("Ruta 1", "ruta-1")]
    [InlineData("Ruta 1 (Escuela Entrenadores)", "ruta-1-escuela-entrenadores")]
    [InlineData("Ruta 1 (Afueras de Hauoli)", "ruta-1-afueras-de-hauoli")]
    [InlineData("Ciudad Hauoli (Zona Comercial)", "ciudad-hauoli-zona-comercial")]
    public void A_map_cell_produces_the_id_the_run_already_holds(string cell, string id) =>
        Assert.Equal(id, EncounterService.NormaliseLocationId(cell));

    /// <summary>Undoing something that was never confirmed says so instead of writing an event.</summary>
    [Fact]
    public async Task Undoing_what_was_never_confirmed_writes_nothing()
    {
        var (service, events, _) = Build();

        var first = await service.RegisterAsync(RunId, Unknown(), "javi", source: EventSource.AutoDetect);
        var result = await service.ClearZoneAsync(RunId, first.Pokemon!.Id, "javi");

        Assert.False(result.Confirmed);
        Assert.DoesNotContain(events.All, e => e.Type == GameEventType.ZoneCleared);
    }
}
