using System.Globalization;
using System.Text;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <param name="Force">Registers even when a rule blocks. Recorded as a violation.</param>
public sealed record RegisterCaptureRequest(
    int Species,
    string SpeciesName,
    string LocationName,
    EncounterType EncounterType,
    bool IsShiny = false,
    int Level = 0,
    string? Nickname = null,
    bool Force = false,
    uint? Pid = null);

/// <param name="Registered">False when a rule blocked the capture and it was not forced.</param>
public sealed record RegisterCaptureResult(
    bool Registered,
    RuleEvaluation Evaluation,
    PokemonEntry? Pokemon);

/// <summary>
/// Turns an attempted capture into rule verdicts, a stored Pokémon and audit events.
/// </summary>
/// <remarks>
/// Lives in the rules assembly because it is the only layer that legitimately knows both the
/// domain and the rule engine; Core must not depend on Rules. Both applications consume it,
/// so no capture logic exists in any view model.
/// </remarks>
public sealed class EncounterService(
    IRuleEngine engine,
    IPokemonRepository pokemon,
    IEventStore events,
    RulesConfiguration configuration,
    IEvolutionLineProvider evolutionLines,
    LevelCapTable caps,
    IRunContext runContext,
    IClock clock)
{
    /// <summary>Evaluates without writing anything, for the preview shown before confirming.</summary>
    public async Task<RuleEvaluation> PreviewAsync(Guid runId, RegisterCaptureRequest request,
        CancellationToken ct = default)
    {
        var context = await BuildContextAsync(runId, ct).ConfigureAwait(false);
        return engine.Evaluate(ToAction(request), context);
    }

    /// <param name="source">
    /// Who decided. The player pressing REGISTRAR and the watcher noticing a new party member are
    /// both legitimate, but they are not the same claim: one person looked at the encounter and
    /// said what it was, the other only saw a Pokémon appear. The history has to be able to tell
    /// them apart afterwards.
    /// </param>
    public async Task<RegisterCaptureResult> RegisterAsync(Guid runId, RegisterCaptureRequest request,
        string actor, CancellationToken ct = default, EventSource source = EventSource.Player)
    {
        var context = await BuildContextAsync(runId, ct).ConfigureAwait(false);
        var action = ToAction(request);
        var evaluation = engine.Evaluate(action, context);

        var locationId = NormaliseLocationId(request.LocationName);

        if (evaluation.IsBlocked && !request.Force)
        {
            if (evaluation.ShouldAudit)
            {
                await AppendViolationAsync(runId, actor, evaluation, request, locationId, ct)
                    .ConfigureAwait(false);
            }

            return new RegisterCaptureResult(false, evaluation, null);
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Species = request.Species,
            SpeciesName = request.SpeciesName,
            Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim(),
            Level = request.Level,
            IsShiny = request.IsShiny,
            Status = PokemonStatus.Alive,
            Origin = OriginFor(request.EncounterType),
            EncounterType = request.EncounterType,
            LocationId = locationId,
            ObtainedAt = clock.Now,
            Pid = request.Pid,
            ObtainedByRuleException = evaluation.IsException,
            ConsumedZoneEncounter = ConsumesZone(request, evaluation, context)
        };

        var caught = await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonCaught,
            Source = source,
            Actor = actor,
            Description = $"{request.SpeciesName} capturado en {request.LocationName}."
                          + (request.IsShiny ? " ¡Shiny!" : string.Empty),
            LocationId = locationId,
            Data = Describe(request, entry)
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(entry with { OriginEventId = caught.Id }, ct).ConfigureAwait(false);

        if (evaluation.IsException)
        {
            var exception = evaluation.Results.First(r => r.Outcome == RuleOutcome.AllowedWithException);
            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                Timestamp = clock.Now,
                Type = GameEventType.RuleException,
                Source = source,
                Actor = actor,
                Description = $"{exception.Title}: {exception.Message}",
                PokemonId = entry.Id,
                LocationId = locationId,
                Data = new Dictionary<string, string>
                {
                    ["regla"] = exception.RuleId,
                    ["reglasLevantadas"] = string.Join(", ",
                        evaluation.Results.Where(r => r.Overridden).Select(r => r.RuleId))
                }
            }, ct).ConfigureAwait(false);
        }

        if (evaluation.IsBlocked && request.Force)
        {
            await AppendViolationAsync(runId, actor, evaluation, request, locationId, ct, entry.Id)
                .ConfigureAwait(false);
        }

        return new RegisterCaptureResult(true, evaluation, entry);
    }


    /// <param name="Reason">Why not, when the map refuses the click.</param>
    public sealed record ConfirmZoneResult(bool Confirmed, string? Reason, PokemonEntry? Pokemon);

    /// <summary>
    /// Records that a capture spent a zone's one encounter, which is what the map's clicks do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the missing half of the first-encounter rule. The seventh generation stores no field
    /// saying an encounter was wild — measured on the real save, where a gift and a wild capture are
    /// identical down to the ball — so the watcher registers every automatic capture as
    /// <see cref="EncounterType.Unknown"/>, which spends nothing. Without somebody saying which zone
    /// was spent, <c>UsedZones</c> is empty forever and the rule never fires: fourteen Pokémon in
    /// the real run, zero zones spent, two captures in Ruta 1 and not a word.
    /// </para>
    /// <para>
    /// It refuses when the zone is already spent by somebody else, and <b>that refusal is the rule
    /// finally doing its job</b>. It does not force, because overriding a rule is the player's call
    /// and belongs to the capture dialog, which records it as a violation.
    /// </para>
    /// </remarks>
    public async Task<ConfirmZoneResult> ConfirmZoneAsync(Guid runId, Guid pokemonId,
        string locationName, string actor, CancellationToken ct = default)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        var entry = all.FirstOrDefault(p => p.Id == pokemonId);

        if (entry is null)
        {
            return new ConfirmZoneResult(false, "Ese Pokémon no está en la run.", null);
        }

        var locationId = NormaliseLocationId(locationName);
        var owner = all.FirstOrDefault(p =>
            p.Id != pokemonId && p.ConsumedZoneEncounter && p.LocationId == locationId);

        if (owner is not null)
        {
            return new ConfirmZoneResult(false,
                $"{locationName} ya la gastó {owner.SpeciesName}.", null);
        }

        var updated = entry with
        {
            LocationId = locationId,
            ConsumedZoneEncounter = true,

            // Confirmar la zona ES decir que fue un encuentro salvaje: es lo único que gasta el
            // encuentro de una zona. Lo que ya venía con un tipo dicho no se toca, porque eso lo
            // afirmó alguien mirando la captura y esto no sabe más que él.
            EncounterType = entry.EncounterType == EncounterType.Unknown
                ? EncounterType.Wild
                : entry.EncounterType
        };

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneConfirmed,
            Source = EventSource.Player,
            Actor = actor,
            Description = $"{entry.SpeciesName} gastó el encuentro de {locationName}.",
            PokemonId = entry.Id,
            LocationId = locationId,
            Data = new Dictionary<string, string>
            {
                ["zona"] = locationName,
                ["zonaAnterior"] = entry.LocationId ?? "(ninguna)",
                ["tipoAnterior"] = entry.EncounterType.ToString()
            }
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(updated, ct).ConfigureAwait(false);
        return new ConfirmZoneResult(true, null, updated);
    }

    /// <summary>Takes back a confirmation, freeing the zone. For a mis-click on the map.</summary>
    /// <remarks>
    /// Undoing by editing the database would leave a chain whose hashes still line up around a hole,
    /// so it is its own event instead: the zone was spent, and then it was not.
    /// </remarks>
    public async Task<ConfirmZoneResult> ClearZoneAsync(Guid runId, Guid pokemonId, string actor,
        CancellationToken ct = default)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        var entry = all.FirstOrDefault(p => p.Id == pokemonId);

        if (entry is null)
        {
            return new ConfirmZoneResult(false, "Ese Pokémon no está en la run.", null);
        }

        if (!entry.ConsumedZoneEncounter)
        {
            return new ConfirmZoneResult(false, $"{entry.SpeciesName} no tenía ninguna zona gastada.", null);
        }

        var updated = entry with { ConsumedZoneEncounter = false };

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneCleared,
            Source = EventSource.Player,
            Actor = actor,
            Description = $"{entry.SpeciesName} deja libre {entry.LocationId}.",
            PokemonId = entry.Id,
            LocationId = entry.LocationId,
            Data = new Dictionary<string, string> { ["zona"] = entry.LocationId ?? "(ninguna)" }
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(updated, ct).ConfigureAwait(false);
        return new ConfirmZoneResult(true, null, updated);
    }
    /// <summary>Zones already used, so the UI can offer them and rules can consult them.</summary>
    public async Task<IReadOnlyList<string>> GetKnownLocationsAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        return [.. all.Where(p => p.LocationId is not null).Select(p => p.LocationId!).Distinct()];
    }

    /// <summary>
    /// Zones whose single encounter the run has already spent, keyed the same way the rules
    /// key them. What the ball rule consults to decide whether to confiscate.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetSpentZonesAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);

        return all
            .Where(p => p is { ConsumedZoneEncounter: true, LocationId: not null })
            .Select(p => p.LocationId!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<RuleContext> BuildContextAsync(Guid runId, CancellationToken ct)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);

        var usedZones = all
            .Where(p => p is { ConsumedZoneEncounter: true, LocationId: not null })
            .GroupBy(p => p.LocationId!)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var first = group.OrderBy(p => p.ObtainedAt).First();
                    return new ZoneEncounter(first.LocationId!, first.LocationId!, first.Species,
                        first.SpeciesName, first.ObtainedAt);
                });

        return new RuleContext
        {
            Pokemon = all,
            UsedZones = usedZones,
            Configuration = configuration,
            EvolutionLines = evolutionLines,

            // The cap of the stage the player is about to face, from Data/levelcaps.json.
            LevelCap = runContext.Current is { } run ? caps.CapFor(run.ClearedStages) : null
        };
    }

    private AttemptCapture ToAction(RegisterCaptureRequest request) => new(
        request.Species,
        request.SpeciesName,
        NormaliseLocationId(request.LocationName),
        request.LocationName.Trim(),
        request.EncounterType,
        request.IsShiny,
        request.Level);

    private bool ConsumesZone(RegisterCaptureRequest request, RuleEvaluation evaluation, RuleContext context)
    {
        if (!context.ConsumesZone(request.EncounterType))
        {
            return false;
        }

        // A shiny allowed by the clause only burns the zone if the run says it should.
        if (evaluation.IsException
            && evaluation.Results.Any(r => r.RuleId == RuleIds.ShinyClause
                                           && r.Outcome == RuleOutcome.AllowedWithException))
        {
            return configuration.For(RuleIds.ShinyClause).ConsumesEncounter;
        }

        return true;
    }

    private Task AppendViolationAsync(Guid runId, string actor, RuleEvaluation evaluation,
        RegisterCaptureRequest request, string locationId, CancellationToken ct, Guid? pokemonId = null)
    {
        var blocked = evaluation.Results.First(r => r.Outcome == RuleOutcome.Blocked);

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.RuleViolation,
            Source = EventSource.Player,
            Actor = actor,
            Description = request.Force
                ? $"Registro forzado pese a «{blocked.Title}»: {blocked.Message}"
                : $"Intento bloqueado por «{blocked.Title}»: {blocked.Message}",
            PokemonId = pokemonId,
            LocationId = locationId,
            Data = new Dictionary<string, string>
            {
                ["regla"] = blocked.RuleId,
                ["forzado"] = request.Force ? "sí" : "no",
                ["pokemon"] = request.SpeciesName
            }
        }, ct);
    }

    private static Dictionary<string, string> Describe(RegisterCaptureRequest request, PokemonEntry entry) => new()
    {
        ["especie"] = request.Species.ToString(),
        ["tipoEncuentro"] = request.EncounterType.ToString(),
        ["shiny"] = request.IsShiny ? "sí" : "no",
        ["nivel"] = request.Level.ToString(),
        ["consumeZona"] = entry.ConsumedZoneEncounter ? "sí" : "no"
    };

    private static PokemonOrigin OriginFor(EncounterType type) => type switch
    {
        EncounterType.Starter => PokemonOrigin.Starter,
        EncounterType.Gift => PokemonOrigin.Gift,
        EncounterType.Trade => PokemonOrigin.WonderTrade,
        _ => PokemonOrigin.Capture
    };

    /// <summary>
    /// "Ruta 1", "ruta 1" and "RUTA  1" must be the same zone, or the first-encounter rule
    /// could be bypassed with a typo.
    /// </summary>
    public static string NormaliseLocationId(string locationName)
    {
        var decomposed = locationName.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
