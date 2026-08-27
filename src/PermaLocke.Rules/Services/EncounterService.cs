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
