using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>
/// What happened at each zone's single encounter, as the player marks it on the map.
/// </summary>
/// <remarks>
/// <para>
/// It is a tracker, not a referee. Clicking a zone says «aquí atrapé», «aquí se me murió el
/// primero» or «el primer encuentro huyó», and nothing else in PermaLocke reads it: no rule fires,
/// no points move. That is deliberate — the player asked for a board to look at, and a board that
/// quietly arbitrated would be worse than one that does not.
/// </para>
/// <para>
/// The marks live in the run's own event chain rather than in a file beside it. Not ceremony: the
/// chain is already per-run, already backed up, already travels with the run, and reading the
/// current state is the same projection every other counter in this application does. A separate
/// file would have needed all of that building again.
/// </para>
/// <para>
/// The state of a zone is <b>the last thing said about it</b>. Marks are not edits to a row: each
/// click is its own event, so the history keeps the whole sequence — which is what lets a mis-click
/// be undone by clicking again rather than by rewriting anything.
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
        CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        var outcomes = new Dictionary<string, ZoneOutcome>(StringComparer.Ordinal);

        foreach (var gameEvent in all
            .Where(e => e.Type == GameEventType.ZoneOutcomeSet && e.LocationId is not null)
            .OrderBy(e => e.Timestamp))
        {
            var outcome = Parse(gameEvent.Data.GetValueOrDefault("resultado"));

            if (outcome == ZoneOutcome.Free)
            {
                outcomes.Remove(gameEvent.LocationId!);
                continue;
            }

            outcomes[gameEvent.LocationId!] = outcome;
        }

        return outcomes;
    }

    /// <summary>Marks a zone, or clears it with <see cref="ZoneOutcome.Free"/>.</summary>
    public async Task SetAsync(Guid runId, string locationId, string locationName,
        ZoneOutcome outcome, string actor, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneOutcomeSet,
            Source = EventSource.Player,
            Actor = actor,
            Description = Describe(locationName, outcome),
            LocationId = locationId,
            Data = new Dictionary<string, string>
            {
                ["zona"] = locationName,
                ["resultado"] = Names[outcome]
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The next state in the cycle the map walks through on repeated clicks.</summary>
    public static ZoneOutcome Next(ZoneOutcome current) => current switch
    {
        ZoneOutcome.Free => ZoneOutcome.Caught,
        ZoneOutcome.Caught => ZoneOutcome.Died,
        ZoneOutcome.Died => ZoneOutcome.Fled,
        _ => ZoneOutcome.Free
    };

    /// <summary>What the card under a marker says.</summary>
    public static string Label(ZoneOutcome outcome) => outcome switch
    {
        ZoneOutcome.Caught => "Atrapado en esta zona",
        ZoneOutcome.Died => "Primer pokémon matado",
        ZoneOutcome.Fled => "Primer encuentro: huida",
        _ => "Sin marcar"
    };

    private static string Describe(string zone, ZoneOutcome outcome) => outcome == ZoneOutcome.Free
        ? $"{zone} vuelve a estar sin marcar."
        : $"{zone}: {Label(outcome).ToLowerInvariant()}.";

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
