using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>What the ball rule did, or why it did nothing.</summary>
public enum BallControlOutcome
{
    /// <summary>The zone still has its encounter and the player keeps their balls.</summary>
    ZoneAvailable,

    /// <summary>The zone was spent, so the balls were taken away.</summary>
    Withheld,

    /// <summary>The zone changed to one that is free, so the balls came back.</summary>
    Returned,

    /// <summary>Nothing changed since the last check.</summary>
    Unchanged,

    /// <summary>The current zone could not be established. Nothing was touched.</summary>
    ZoneUnknown,

    /// <summary>The area covers more than one location, so it cannot be judged.</summary>
    ZoneAmbiguous,

    /// <summary>The rule is turned off in the configuration.</summary>
    Disabled
}

/// <param name="LocationName">The zone the decision was taken on, when there was one.</param>
public sealed record BallControlResult(
    BallControlOutcome Outcome, int? Area, string? LocationName, int ItemsAffected)
{
    public bool Acted => Outcome is BallControlOutcome.Withheld or BallControlOutcome.Returned;
}

/// <summary>
/// Takes the player's Poké Balls away while the zone they are standing in has already spent
/// its encounter, and gives them back when they leave.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule the run always wanted (§14): not auditing a capture after the fact, but
/// making it impossible. It needs three things that were each a separate problem — knowing the
/// zone, translating it to the one the run records, and writing the bag — and it does none of
/// them itself: it decides <em>when</em>, and the ports do the rest.
/// </para>
/// <para>
/// Every path that is not certain does nothing at all. An unknown zone, an area covering two
/// locations, a bag that cannot be found: all of them leave the player alone. Taking someone's
/// balls away in the wrong zone is worse than not enforcing the rule.
/// </para>
/// </remarks>
public sealed class BallControlService(
    IZoneProvider zones,
    IItemWithholder bag,
    ZoneTable table,
    IEventStore events,
    IClock clock)
{
    /// <summary>Item ids the rule takes away, in cartridge numbering.</summary>
    /// <remarks>
    /// Configuration, not code: it arrives from <c>Data/rules.json</c>. The default is empty on
    /// purpose, so a missing configuration disables the rule instead of guessing which items to
    /// confiscate.
    /// </remarks>
    public IReadOnlyList<int> BallItemIds { get; init; } = [];

    public bool Enabled { get; init; }

    /// <summary>
    /// How many consecutive unreadable ticks before the balls are handed back. A zone change
    /// makes the copies disagree for an instant, and that alone should not trigger a return.
    /// </summary>
    private const int UnreadableTicksBeforeRelease = 2;

    private string? _lastLocationId;
    private int _unreadableTicks;

    /// <summary>
    /// Brings the bag in line with where the player is. Safe to call on every monitor tick:
    /// it only writes when the answer changes.
    /// </summary>
    /// <remarks>
    /// Withholding and returning are deliberately not symmetric. Items are taken away only
    /// while PermaLocke is sure the player stands in a spent zone, and given back as soon as it
    /// stops being sure. That asymmetry came from a real run: leaving Route 1 crosses area 0,
    /// which covers Route 1 and the Melemele Sea at once, and the first version simply did
    /// nothing there — so the player was left with no Poké Balls and no explanation. Doubt now
    /// always resolves in the player's favour, and the capture is still policed by the rule
    /// that blocks its registration.
    /// </remarks>
    public async Task<BallControlResult> ApplyAsync(Run run, IReadOnlySet<string> usedZones,
        CancellationToken ct = default)
    {
        if (!Enabled || BallItemIds.Count == 0)
        {
            return new BallControlResult(BallControlOutcome.Disabled, null, null, 0);
        }

        if (zones.CurrentArea() is not { } area)
        {
            // Ilegible puede ser el instante de un cambio de mapa, así que se espera un poco.
            return await UncertainAsync(run, null, null, BallControlOutcome.ZoneUnknown,
                ++_unreadableTicks >= UnreadableTicksBeforeRelease, ct).ConfigureAwait(false);
        }

        _unreadableTicks = 0;

        if (table.LocationNameFor(area) is not { } locationName)
        {
            // Un área que cubre dos localizaciones es un estado estable, no una transición:
            // no se va a aclarar esperando, así que se devuelve ya.
            var ambiguous = table.IsAmbiguous(area);

            return await UncertainAsync(run, area, null,
                ambiguous ? BallControlOutcome.ZoneAmbiguous : BallControlOutcome.ZoneUnknown,
                true, ct).ConfigureAwait(false);
        }

        var locationId = EncounterService.NormaliseLocationId(locationName);
        var spent = usedZones.Contains(locationId);
        var unchanged = _lastLocationId == locationId;
        _lastLocationId = locationId;

        var affected = spent
            ? await WithholdAsync(run, area, locationName, ct).ConfigureAwait(false)
            : await GiveBackAsync(run, area, locationName, ct).ConfigureAwait(false);

        if (affected == 0)
        {
            return new BallControlResult(
                unchanged ? BallControlOutcome.Unchanged
                    : spent ? BallControlOutcome.Withheld : BallControlOutcome.ZoneAvailable,
                area, locationName, 0);
        }

        return new BallControlResult(
            spent ? BallControlOutcome.Withheld : BallControlOutcome.Returned,
            area, locationName, affected);
    }

    /// <summary>
    /// Handles the cases where the zone cannot be judged: gives back anything withheld, since
    /// there is no longer any evidence the player is standing where it was taken.
    /// </summary>
    private async Task<BallControlResult> UncertainAsync(Run run, int? area, string? locationName,
        BallControlOutcome outcome, bool release, CancellationToken ct)
    {
        _lastLocationId = null;

        if (!release)
        {
            return new BallControlResult(outcome, area, locationName, 0);
        }

        var given = await GiveBackAsync(run, area ?? -1, locationName ?? "una zona sin identificar", ct)
            .ConfigureAwait(false);

        return given > 0
            ? new BallControlResult(BallControlOutcome.Returned, area, locationName, given)
            : new BallControlResult(outcome, area, locationName, 0);
    }
    private async Task<int> WithholdAsync(Run run, int area, string locationName, CancellationToken ct)
    {
        var taken = new Dictionary<string, string>();

        foreach (var itemId in BallItemIds)
        {
            var carried = bag.Carried(itemId);

            if (carried > 0 && bag.Withhold(itemId))
            {
                taken[itemId.ToString()] = carried.ToString();
            }
        }

        if (taken.Count == 0)
        {
            return 0;
        }

        await RecordAsync(run, GameEventType.BallsWithheld,
            $"Poké Balls retiradas en {locationName}: la zona ya gastó su encuentro.",
            area, locationName, taken, ct).ConfigureAwait(false);

        return taken.Count;
    }

    private async Task<int> GiveBackAsync(Run run, int area, string locationName, CancellationToken ct)
    {
        var returned = new Dictionary<string, string>();

        foreach (var itemId in BallItemIds)
        {
            var owed = bag.Owed(itemId);

            if (owed > 0 && bag.GiveBack(itemId))
            {
                returned[itemId.ToString()] = owed.ToString();
            }
        }

        if (returned.Count == 0)
        {
            return 0;
        }

        await RecordAsync(run, GameEventType.BallsReturned,
            $"Poké Balls devueltas en {locationName}.", area, locationName, returned, ct)
            .ConfigureAwait(false);

        return returned.Count;
    }

    private Task RecordAsync(Run run, GameEventType type, string description, int area,
        string locationName, IReadOnlyDictionary<string, string> items, CancellationToken ct)
    {
        // Se copia en vez de añadir sobre el diccionario del llamador: al mutarlo, el recuento
        // de objetos afectados pasó a incluir "area" y "zona" y el log decía tres donde había
        // uno. Un contador que miente es exactamente lo que este proyecto no admite.
        var data = new Dictionary<string, string>(items)
        {
            ["area"] = area.ToString(),
            ["zona"] = locationName
        };

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = type,
            Source = EventSource.AutoDetect,
            Actor = run.PlayerName,
            Description = description,
            Reason = "Regla de primer encuentro por zona",
            Data = data
        }, ct);
    }
}
