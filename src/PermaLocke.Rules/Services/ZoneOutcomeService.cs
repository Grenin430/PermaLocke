using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>What a zone is marked as on the map, and who marked it.</summary>
/// <param name="Outcome">How the zone's first encounter ended.</param>
/// <param name="ByPlayer">
/// Clicked on the map by the player, from the time the map was marked by hand. Nothing writes these any more
/// (§118); the ones already in a run's history stay, and say so.
/// </param>
/// <param name="Species">The wild Pokémon of that first encounter, when PermaLocke saw it.</param>
public sealed record ZoneMark(ZoneOutcome Outcome, bool ByPlayer, string? Species, DateTimeOffset At);

/// <summary>
/// What happened at each zone's single encounter, as the map shows it.
/// </summary>
/// <remarks>
/// <para>
/// Since §118 the map is marked by PermaLocke and only by PermaLocke: it reads the zone from the game, sees the
/// first wild battle of a route start and marks how it ended. It used to be a board the player clicked, and with
/// people the player barely knows in the competition, a board anyone can click is not a record. The first-encounter
/// rule reads it too — a route marked here counts as spent (§117).
/// </para>
/// <para>
/// The marks live in the run's own event chain rather than in a file beside it. Not ceremony: the chain is already
/// per-run, already backed up, already travels with the run, and reading the current state is the same projection
/// every other counter in this application does.
/// </para>
/// <para>
/// A zone reads as <b>the last thing said about it</b>, with one exception: what PermaLocke detected is never
/// overwritten or cleared by a mark from the player. Nothing in the application writes those any more, but old
/// runs carry them, and so does any event written by an older build.
/// </para>
/// </remarks>
public sealed class ZoneOutcomeService(IEventStore events, IClock clock)
{
    /// <summary>The stored name of each outcome. Written out so a rename cannot silently reinterpret history.</summary>
    private static readonly Dictionary<ZoneOutcome, string> Names = new()
    {
        [ZoneOutcome.Free] = "libre",
        [ZoneOutcome.Caught] = "atrapado",
        [ZoneOutcome.Died] = "muerto",
        [ZoneOutcome.Fled] = "huida"
    };

    /// <summary>What each zone is currently marked as. Zones never marked are simply absent.</summary>
    public async Task<IReadOnlyDictionary<string, ZoneOutcome>> GetAsync(Guid runId,
        CancellationToken ct = default) =>
        (await GetMarksAsync(runId, ct).ConfigureAwait(false))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Outcome, StringComparer.Ordinal);

    /// <summary>Each marked zone with who marked it and against what.</summary>
    public async Task<IReadOnlyDictionary<string, ZoneMark>> GetMarksAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        var marks = new Dictionary<string, ZoneMark>(StringComparer.Ordinal);
        var detected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var gameEvent in all
            .Where(e => e.Type is GameEventType.ZoneOutcomeSet or GameEventType.ZoneCleared && e.LocationId is not null)
            .OrderBy(e => e.Timestamp))
        {
            var zone = gameEvent.LocationId!;

            // Liberar es una corrección explícita, así que sí puede con una marca detectada — es para lo que
            // existe el tipo. Se olvida también que estaba detectada: la zona vuelve a estar como si nadie la
            // hubiera tocado, y lo que la vuelva a marcar manda.
            if (gameEvent.Type == GameEventType.ZoneCleared)
            {
                marks.Remove(zone);
                detected.Remove(zone);
                continue;
            }

            var byPlayer = gameEvent.Source != EventSource.AutoDetect;

            // Lo detectado no lo pisa ni lo borra una marca a mano, venga de cuando el mapa se pinchaba o de
            // una versión vieja de la aplicación.
            if (byPlayer && detected.Contains(zone))
            {
                continue;
            }

            if (!byPlayer)
            {
                detected.Add(zone);
            }

            var outcome = Parse(gameEvent.Data.GetValueOrDefault("resultado"));

            if (outcome == ZoneOutcome.Free)
            {
                marks.Remove(zone);
                continue;
            }

            var species = gameEvent.Data.GetValueOrDefault("especie");
            marks[zone] = new ZoneMark(outcome, byPlayer, string.IsNullOrEmpty(species) ? null : species,
                gameEvent.Timestamp);
        }

        return marks;
    }

    /// <summary>Marks a zone, or clears it with <see cref="ZoneOutcome.Free"/>.</summary>
    /// <param name="source">
    /// Who is saying it. PermaLocke marks as <see cref="EventSource.AutoDetect"/>; there is no longer anything in the
    /// application that marks as the player, and the parameter has no default so a new caller has to say which.
    /// </param>
    /// <param name="speciesName">The wild Pokémon of the encounter, when known.</param>
    /// <param name="how">How the ending was read, in Spanish, for the history.</param>
    public async Task SetAsync(Guid runId, string locationId, string locationName, ZoneOutcome outcome,
        string actor, EventSource source, string? speciesName = null, string? how = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var data = new Dictionary<string, string>
        {
            ["zona"] = locationName,
            ["resultado"] = Names[outcome]
        };

        if (!string.IsNullOrEmpty(speciesName))
        {
            data["especie"] = speciesName;
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneOutcomeSet,
            Source = source,
            Actor = actor,
            Description = Describe(locationName, outcome, speciesName),
            Reason = how,
            LocationId = locationId,
            Data = data
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives a zone back its encounter: the mark comes off the map and the route stops counting as spent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chain is not edited, so this is an addition (§67). What it corrects stays in the history saying what
    /// PermaLocke did, and this says, with a person and a reason, that it was wrong — the same shape as
    /// <see cref="GameEventType.DeathRevoked"/>.
    /// </para>
    /// <para>
    /// It is the only thing allowed to take down a mark that PermaLocke detected, and that is the point: what §118
    /// forbids is a stray click overriding a detection, not a correction. Written on 2026-09-21, when the zone
    /// reader placed the first encounter of Ruta 1 in the route next door, spent it and emptied the player's bag —
    /// and nothing in the application could undo any of it.
    /// </para>
    /// </remarks>
    /// <param name="why">Obligatory: a correction with no reason is indistinguishable from a mistake.</param>
    public Task ClearAsync(Guid runId, string locationId, string locationName, string actor, string why,
        EventSource source = EventSource.Player,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(why);

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneCleared,
            Source = source,
            Actor = actor,
            Description = $"{locationName}: la zona vuelve a estar libre.",
            Reason = why,
            LocationId = locationId,
            Data = new Dictionary<string, string> { ["zona"] = locationName }
        }, ct);
    }

    /// <summary>What the card under a marker says.</summary>
    public static string Label(ZoneOutcome outcome) => outcome switch
    {
        ZoneOutcome.Caught => "Atrapado en esta zona",
        ZoneOutcome.Died => "Primer pokémon matado",
        ZoneOutcome.Fled => "Primer encuentro: huida",
        _ => "Sin marcar"
    };

    private static string Describe(string zone, ZoneOutcome outcome, string? species) => outcome switch
    {
        ZoneOutcome.Free => $"{zone} vuelve a estar sin marcar.",
        _ when string.IsNullOrEmpty(species) => $"{zone}: {Label(outcome).ToLowerInvariant()}.",
        _ => $"{zone}: {Label(outcome).ToLowerInvariant()} ({species})."
    };

    /// <summary>
    /// An outcome nobody recognises reads as <see cref="ZoneOutcome.Free"/>.
    /// </summary>
    /// <remarks>
    /// Which is the harmless direction: an unmarked zone invites a look, while guessing at one of
    /// the other three would put a colour on the map that nobody put there.
    /// </remarks>
    private static ZoneOutcome Parse(string? stored) =>
        Names.FirstOrDefault(pair => string.Equals(pair.Value, stored, StringComparison.Ordinal)).Key;
}
