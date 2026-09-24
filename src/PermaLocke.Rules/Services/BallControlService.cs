using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>
/// Carries out what <see cref="EncounterPolicy"/> decides: takes the Poké Balls away or gives them back, and writes
/// down every step.
/// </summary>
/// <remarks>
/// <para>
/// It decides nothing and it reads nothing of the game; the watcher in the application does that and calls in.
/// What this owns is the part that has to be right every time: which items, what the run says is spent, and the
/// events. Every withholding and every return is a <see cref="GameEventType.BallsWithheld"/> or
/// <see cref="GameEventType.BallsReturned"/> with the reason, and the first encounter of a route is a
/// <see cref="GameEventType.ZoneEncounterSpent"/> — the player has the right to know why their bag emptied.
/// </para>
/// <para>
/// A bag that cannot be read is never written: an empty answer from <see cref="IItemWithholder.CarriedAll"/> means
/// «unknown», and taking «unknown» for «nothing to take» would be harmless, but taking it for «something» would
/// not.
/// </para>
/// </remarks>
public sealed class BallControlService(
    IItemWithholder bag,
    IEventStore events,
    IPokemonRepository pokemon,
    ZoneOutcomeService outcomes,
    IClock clock)
{
    /// <summary>Item ids of the balls, in cartridge numbering, from <c>Data/rules.json</c>.</summary>
    /// <remarks>Empty by default on purpose, so a missing configuration turns the rule off instead of guessing.</remarks>
    public IReadOnlyList<int> BallItemIds { get; init; } = [];

    public bool Enabled { get; init; }

    /// <summary>Whether a shiny encounter spends the route, from the shiny clause in <c>Data/rules.json</c>.</summary>
    public bool ShinyConsumesEncounter { get; init; }

    public bool IsActive => Enabled && BallItemIds.Count > 0;

    /// <summary>
    /// The routes whose encounter is used: by a capture that spent it, by a first wild battle PermaLocke saw, or by
    /// a mark on the map.
    /// </summary>
    /// <remarks>
    /// The map is marked by PermaLocke alone since §118, but runs from before carry marks the player clicked, and
    /// those still count. A «libre» among them does not free a route PermaLocke saw spent: the spent event is read
    /// here on its own, and the map's projection does not let a manual mark undo a detected one either.
    /// </remarks>
    public async Task<IReadOnlySet<string>> SpentZonesAsync(Guid runId, CancellationToken ct = default)
    {
        // Cada señal con su hora, y se aplican en orden. Una corrección (§67: la cadena no se edita, se le añade)
        // tiene que ganarle al gasto que corrige, y un encuentro posterior a la corrección tiene que volver a
        // gastar la ruta. Contando cada fuente en su propio bucle, «gastada» dependía del orden de los bucles.
        var signals = new List<(DateTimeOffset When, string Zone, bool Spent)>();

        foreach (var entry in await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false))
        {
            if (entry is { ConsumedZoneEncounter: true, LocationId: { } id })
            {
                signals.Add((entry.ObtainedAt, id, true));
            }
        }

        foreach (var gameEvent in await events.GetAllAsync(runId, ct).ConfigureAwait(false))
        {
            if (gameEvent is { Type: GameEventType.ZoneEncounterSpent, LocationId: { } spentId })
            {
                signals.Add((gameEvent.Timestamp, spentId, true));
            }
            else if (gameEvent is { Type: GameEventType.ZoneCleared, LocationId: { } freedId })
            {
                signals.Add((gameEvent.Timestamp, freedId, false));
            }
        }

        var spent = new HashSet<string>(StringComparer.Ordinal);

        foreach (var signal in signals.OrderBy(signal => signal.When))
        {
            if (signal.Spent)
            {
                spent.Add(signal.Zone);
            }
            else
            {
                spent.Remove(signal.Zone);
            }
        }

        // Una zona marcada en el MAPA está gastada por definición, y las liberadas ya no salen marcadas.
        foreach (var zone in (await outcomes.GetAsync(runId, ct).ConfigureAwait(false)).Keys)
        {
            spent.Add(zone);
        }

        return spent;
    }

    /// <summary>
    /// Brings the bag in line with the decision.
    /// </summary>
    /// <returns>How many kinds of ball were taken or given back; zero when nothing changed or the bag is unreadable.</returns>
    public async Task<int> ApplyAsync(Run run, EncounterDecision decision, FieldZone? zone, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(decision);

        if (!IsActive)
        {
            return 0;
        }

        var changed = new Dictionary<string, string>();

        if (decision.Action == BallAction.Withhold)
        {
            var carried = bag.CarriedAll(BallItemIds);

            foreach (var (item, count) in carried)
            {
                if (count > 0 && bag.Withhold(run.Id, item))
                {
                    changed[item.ToString()] = count.ToString();
                }
            }

            if (changed.Count > 0)
            {
                await RecordAsync(run, GameEventType.BallsWithheld, $"Poké Balls retiradas. {decision.Reason}",
                    zone, changed, ct).ConfigureAwait(false);
            }
        }
        else
        {
            foreach (var item in BallItemIds)
            {
                var owed = bag.Owed(run.Id, item);

                if (owed > 0 && bag.GiveBack(run.Id, item))
                {
                    changed[item.ToString()] = owed.ToString();
                }
            }

            if (changed.Count > 0)
            {
                await RecordAsync(run, GameEventType.BallsReturned, $"Poké Balls devueltas. {decision.Reason}",
                    zone, changed, ct).ConfigureAwait(false);
            }
        }

        return changed.Count;
    }

    /// <summary>Records that a route's single encounter was used by the first wild battle in it.</summary>
    public Task SpendZoneAsync(Run run, FieldZone zone, int species, string speciesName, string reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(zone);

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.ZoneEncounterSpent,
            Source = EventSource.AutoDetect,
            Actor = run.PlayerName,
            Description = species > 0
                ? $"Primer encuentro en {zone.LocationName}: {speciesName}."
                : $"Primer encuentro en {zone.LocationName}.",
            Reason = reason,
            LocationId = zone.LocationId,
            Data = new Dictionary<string, string>
            {
                ["zona"] = zone.LocationName,
                ["mapa"] = zone.Map.ToString(),
                ["especie"] = species > 0 ? species.ToString() : string.Empty
            }
        }, ct);
    }

    /// <summary>Runs known to have had a Poké Ball, so the history is read once per run and not every battle.</summary>
    private readonly HashSet<Guid> _hadBalls = [];
    private readonly SemaphoreSlim _firstBallGate = new(1, 1);

    /// <summary>Raised once, after the run's first observed ball has been saved to its history.</summary>
    public event EventHandler<Run>? FirstBallDetected;

    /// <summary>
    /// Whether this run has had a Poké Ball yet. Until it has, a wild battle spends no route and marks no map (§149).
    /// </summary>
    /// <remarks>
    /// The story walks the player through tall grass before anyone hands them a ball, and a battle nothing could be
    /// caught in is not the route's encounter. The first ball seen in the bag — carried, or owed back by this run —
    /// is written down as <see cref="GameEventType.FirstPokeBallSeen"/>, and from then on it always counts, even with
    /// every ball thrown. A bag that cannot be read answers true: the rule then behaves as it always did, rather than
    /// letting routes go free on a failed read.
    /// </remarks>
    public async Task<bool> HasHadBallsAsync(Run run, CancellationToken ct = default)
    {
        bool hadBalls, newlySeen;
        // Both monitoring loops can see the same first ball. Save and announce it only once.
        await _firstBallGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            (hadBalls, newlySeen) = await DetectFirstBallAsync(run, ct).ConfigureAwait(false);
        }
        finally
        {
            _firstBallGate.Release();
        }

        if (newlySeen) FirstBallDetected?.Invoke(this, run);
        return hadBalls;
    }
    private async Task<(bool HadBalls, bool NewlySeen)> DetectFirstBallAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (_hadBalls.Contains(run.Id))
        {
            return (true, false);
        }

        if ((await events.GetAllAsync(run.Id, ct).ConfigureAwait(false))
            .Any(e => e.Type == GameEventType.FirstPokeBallSeen))
        {
            _hadBalls.Add(run.Id);
            return (true, false);
        }

        var carried = bag.CarriedAll(BallItemIds);

        if (carried.Count == 0)
        {
            return (true, false);
        }

        if (!carried.Values.Any(count => count > 0) && !BallItemIds.Any(id => bag.Owed(run.Id, id) > 0))
        {
            return (false, false);
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.FirstPokeBallSeen,
            Source = EventSource.AutoDetect,
            Actor = run.PlayerName,
            Description = "Primera Poké Ball en la mochila: desde ahora cada ruta cuenta su primer encuentro.",
            Data = carried.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString())
        }, ct).ConfigureAwait(false);

        _hadBalls.Add(run.Id);
        return (true, true);
    }

    /// <summary>Marks on the map how the route's first encounter ended. The map has no other way to be marked (§118).</summary>
    public Task RecordOutcomeAsync(Run run, FieldZone zone, ZoneOutcome outcome, string? speciesName, string how,
        CancellationToken ct = default) =>
        outcomes.SetAsync(run.Id, zone.LocationId, zone.LocationName, outcome, run.PlayerName, EventSource.AutoDetect,
            speciesName, how, ct);

    private Task RecordAsync(Run run, GameEventType type, string description, FieldZone? zone,
        IReadOnlyDictionary<string, string> items, CancellationToken ct)
    {
        // Se copia en vez de añadir sobre el diccionario del llamador: al mutarlo, el recuento
        // de objetos afectados pasó a incluir "mapa" y "zona" y el log decía tres donde había uno.
        var data = new Dictionary<string, string>(items);

        if (zone is not null)
        {
            data["zona"] = zone.LocationName;
            data["mapa"] = zone.Map.ToString();
        }

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
            LocationId = zone?.LocationId,
            Data = data
        }, ct);
    }
}
