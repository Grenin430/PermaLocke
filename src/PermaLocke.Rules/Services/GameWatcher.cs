using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Rules.Services;

/// <param name="NewMembers">In the game's party but not yet registered in the run.</param>
/// <param name="Fainted">Registered as alive, but at zero HP in the game.</param>
public sealed record WatcherFindings(
    IReadOnlyList<LivePartyMember> NewMembers,
    IReadOnlyList<PokemonEntry> Fainted)
{
    public bool IsEmpty => NewMembers.Count == 0 && Fainted.Count == 0;

    public static WatcherFindings None { get; } = new([], []);
}

/// <summary>
/// Compares what the game shows against what the run has recorded, and reports the difference.
/// </summary>
/// <remarks>
/// Matching is by PID, the personality value the game assigns on generation: it survives
/// nicknames, level ups and evolutions, which a species match would not.
///
/// The watcher only reports. Registering a capture needs a zone, and the zone is not readable
/// from memory yet, so that decision stays with the player. Deaths need no such input and are
/// recorded automatically.
/// </remarks>
public sealed class GameWatcher(IPokemonRepository pokemon, IEventStore events, IClock clock,
    PenaltyService penalties)
{
    /// <summary>
    /// Whether the party had someone standing last time we looked.
    /// </summary>
    /// <remarks>
    /// A wipe is an <b>edge</b>, not a state: the party stays at zero HP for as long as the player
    /// takes to reach a Pokémon Centre, and the watcher looks once a second. Charging on the
    /// state would charge dozens of times for one wipe.
    ///
    /// Where it starts is read from the run's history, per run (§161): true, unless the last wipe is
    /// newer than the last death, in which case the party is still down from it and opening
    /// PermaLocke again must not charge it twice. The cost of reading the edge: a wipe that happens
    /// with the app closed is not charged as a wipe. The deaths that make it up are, because those
    /// are recorded from the run, not from a flag in memory.
    /// </remarks>
    private bool _partyWasStanding = true;

    /// <summary>How many looks in a row have found nobody standing.</summary>
    private int _nobodyStandingPolls;

    /// <summary>Consecutive looks needed before a wipe is charged. See <see cref="CheckWipeAsync"/>.</summary>
    private const int PollsBeforeCallingItAWipe = 3;

    // Una muerte se registra con UNA lectura a cero, y a proposito. Hubo un dia en que se exigieron
    // tres seguidas, por un diagnostico equivocado: se creyo que una muerte de Okidogi era una
    // lectura tomada con la partida a medio cargar, y no lo era -la aplicacion llevaba ocho minutos
    // conectada y el jugador confirmo que lo mataron en un combate-. Peor: justo despues de esa
    // muerte real, una copia atrasada de la memoria leyo a Okidogi a 191, y con tres lecturas
    // seguidas esa copia habria reiniciado la cuenta y la muerte de verdad podria no registrarse
    // nunca. Una lectura atrasada que ENSEÑA VIVO a un muerto esta medida; una que enseñe muerto a
    // un vivo, no. Ver docs/ARCHITECTURE.md §111.
    public async Task<WatcherFindings> InspectAsync(Guid runId, GameSnapshot snapshot,
        CancellationToken ct = default)
    {
        if (!snapshot.Connected)
        {
            return WatcherFindings.None;
        }

        var registered = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        // The same PID can appear twice if a Pokémon was registered more than once. Grouping
        // instead of keying directly keeps that from throwing; the earliest entry wins.
        var byPid = registered
            .Where(p => p.Pid is not null)
            .GroupBy(p => p.Pid!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(p => p.ObtainedAt).First());

        var newMembers = snapshot.Party
            .Where(member => member.Pid != 0 && !byPid.ContainsKey(member.Pid))
            .ToList();

        var fainted = snapshot.Party
            .Where(member => member.IsFainted)
            .Select(member => byPid.TryGetValue(member.Pid, out var entry) ? entry : null)
            .OfType<PokemonEntry>()
            .Where(entry => entry.Status == PokemonStatus.Alive)
            .ToList();

        return new WatcherFindings(newMembers, fainted);
    }

    /// <summary>
    /// Records a death: the Pokémon is marked dead and an auditable event is written. Fainting
    /// is death in a Nuzlocke, so no confirmation is asked for; the event carries the evidence.
    /// </summary>
    /// <param name="source">
    /// Who decided it. <see cref="EventSource.AutoDetect"/> when the watcher saw the Pokémon at
    /// zero HP, <see cref="EventSource.Player"/> when a person marked it by hand.
    /// </param>
    /// <summary>
    /// The PID of everything this run counts as fallen.
    /// </summary>
    /// <remarks>
    /// By PID and not by "it looks like the marker": a Shedinja called MUERTO is what a death is
    /// turned <b>into</b>, and the run has held a real one that was never a death at all (§59).
    /// The history is the authority on who died; the game only says where they are now.
    /// </remarks>
    public async Task<IReadOnlySet<uint>> FallenPidsAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);

        return all.Where(entry => entry.Status == PokemonStatus.Dead && entry.Pid is not null)
            .Select(entry => entry.Pid!.Value)
            .ToHashSet();
    }

    /// <param name="detection">
    /// How it was known, written into the event. It matters because the two are not equally
    /// trustworthy and the historial has to say which one this was.
    /// </param>
    /// <returns>What the death was charged, so whoever tells the player says the real number.</returns>
    /// <param name="details">
    /// What else was seen at the moment, added to the event as it was: the rivals of the battle, for the
    /// cemetery. Never a replacement for the fields this method writes itself.
    /// </param>
    public async Task<PenaltyResult> RecordDeathAsync(PokemonEntry entry, string actor,
        EventSource source = EventSource.AutoDetect, string detection = "memoria del juego",
        CancellationToken ct = default, IReadOnlyDictionary<string, string>? details = null)
    {
        var charged = await penalties.ChargeDeathAsync(entry.RunId, actor, entry, ct).ConfigureAwait(false);

        var data = new Dictionary<string, string>
        {
            ["especie"] = entry.Species.ToString(),
            ["nivel"] = entry.Level.ToString(),
            ["deteccion"] = detection
        };

        foreach (var (key, value) in details ?? new Dictionary<string, string>())
        {
            data.TryAdd(key, value);
        }

        var died = await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = entry.RunId,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonDied,
            Source = source,
            Actor = actor,
            Description = source == EventSource.Player
                ? $"{entry.Nickname ?? entry.SpeciesName} marcado como caído a mano."
                : $"{entry.Nickname ?? entry.SpeciesName} ha caído a 0 PS.",
            PokemonId = entry.Id,
            LocationId = entry.LocationId,
            Data = data
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(entry with
        {
            Status = PokemonStatus.Dead,
            DiedAt = died.Timestamp
        }, ct).ConfigureAwait(false);

        return charged;
    }

    /// <summary>
    /// Undoes a death that should not have been recorded: alive again, penalty paid back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An addition to the chain and not an edit of it, like <see cref="GameEventType.ZoneCleared"/>:
    /// the <see cref="GameEventType.PokemonDied"/> stays, and a
    /// <see cref="GameEventType.DeathRevoked"/> carrying its id says it was wrong, who said so and
    /// why. The refund is exactly what that death was charged, read from its own penalty event, and
    /// not the price in the configuration today — a role or a rule changed since would otherwise
    /// pay back a different amount than was taken.
    /// </para>
    /// <para>
    /// It refuses rather than guesses: a Pokémon that is not dead, a death with nothing recorded
    /// behind it, or one already revoked. And it is a player's decision by construction — the
    /// source is <see cref="EventSource.Player"/> — because the only thing that knows a death was
    /// false is somebody who was there.
    /// </para>
    /// </remarks>
    /// <returns>Null when done; otherwise why nothing was done.</returns>
    public async Task<string?> RevokeDeathAsync(PokemonEntry entry, string actor, string reason,
        EventSource source = EventSource.Player,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var name = entry.Nickname ?? entry.SpeciesName;

        if (entry.Status != PokemonStatus.Dead)
        {
            return $"{name} no está muerto.";
        }

        var history = await events.GetAllAsync(entry.RunId, ct).ConfigureAwait(false);

        var death = history.LastOrDefault(e => e.Type == GameEventType.PokemonDied && e.PokemonId == entry.Id);

        if (death is null)
        {
            return $"No se encuentra la muerte de {name}.";
        }

        var deathId = death.Id.ToString();

        if (history.Any(e => e.Type == GameEventType.DeathRevoked
                             && e.Data.TryGetValue("muerte", out var revoked) && revoked == deathId))
        {
            return $"La muerte de {name} ya está revocada.";
        }

        // La penalizacion se escribe justo antes que la muerte, asi que es la ultima de «muerte»
        // de este Pokemon que no va detras de ella.
        var penalty = history.LastOrDefault(e => e.Type == GameEventType.PointsPenalty
                                                 && e.PokemonId == entry.Id
                                                 && e.Reason == "muerte"
                                                 && e.Timestamp <= death.Timestamp);

        var refund = penalty is null ? 0 : Math.Max(0, -penalty.PointsDelta);
        var why = string.IsNullOrWhiteSpace(reason) ? "sin motivo anotado" : reason.Trim();

        var data = new Dictionary<string, string>
        {
            ["muerte"] = deathId,
            ["especie"] = entry.Species.ToString(),
            ["devuelto"] = refund.ToString()
        };

        if (penalty is not null)
        {
            data["penalizacion"] = penalty.Id.ToString();
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = entry.RunId,
            Timestamp = clock.Now,
            Type = GameEventType.DeathRevoked,
            Source = source,
            Actor = actor,
            Description = refund > 0
                ? $"Muerte de {name} revocada: vuelve a estar vivo y se devuelven {refund} puntos."
                : $"Muerte de {name} revocada: vuelve a estar vivo.",
            PokemonId = entry.Id,
            PointsDelta = refund,
            Reason = why,
            Data = data
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(entry with
        {
            Status = PokemonStatus.Alive,
            DiedAt = null
        }, ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    /// Charges for a wipe the moment the party goes from having someone standing to having nobody.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="RecordDeathAsync"/>: the individual deaths are
    /// charged one by one as they are detected, and the wipe is charged <b>on top</b>, once, for
    /// the party falling as a whole.
    /// </remarks>
    public async Task<PenaltyResult?> CheckWipeAsync(Guid runId, string actor, GameSnapshot snapshot,
        CancellationToken ct = default)
    {
        if (!snapshot.Connected || snapshot.Party.Count == 0)
        {
            // Sin lectura fiable no se decide nada: un equipo vacío es "no se sabe", no "han
            // caído todos".
            return null;
        }

        // Al cambiar de run -o al abrir PermaLocke- no se sabe cómo estaba el equipo. Si desde el último equipo caído no
        // ha muerto nadie, sigue siendo aquel desastre: si no, cerrar y abrir la app tras perder volvía a cobrarlo.
        if (_stateRun != runId)
        {
            _stateRun = runId;
            _nobodyStandingPolls = 0;
            _partyWasStanding = !await StillDownFromLastWipeAsync(runId, ct).ConfigureAwait(false);
        }

        // En pie es con PS y vivo EN LA RUN (§161). Tras un equipo caído el juego lleva al Centro Pokémon y cura a todos;
        // PermaLocke devuelve al suelo a los caídos (§99 bis), y ese ir y volver se leía como otro equipo caído: −100
        // cada vez que te curaban. El historial dice quién ha muerto; los PS solo dicen dónde está ahora.
        var fallenInRun = await FallenPidsAsync(runId, ct).ConfigureAwait(false);
        var standing = snapshot.Party.Any(member => !member.IsFainted && !fallenInRun.Contains(member.Pid));

        if (standing)
        {
            _partyWasStanding = true;
            _nobodyStandingPolls = 0;
            return null;
        }

        if (!_partyWasStanding)
        {
            // Ya estaba caído la vez anterior: es el mismo desastre, no uno nuevo.
            return null;
        }

        // Y no se cobra a la primera. «Nadie en pie» también es lo que parece una lectura
        // INCOMPLETA en la que lo poco que se leyó estaba caído, y eso ocurre: el proveedor
        // prefiere la estructura autoritativa aunque lea menos huecos que el espejo, que es lo
        // correcto para decidir muertes y deja lecturas cortas de vez en cuando. Un equipo caído
        // de verdad no es un instante -- te manda al Centro Pokémon y sigue caído hasta que curas
        // --, así que exigirlo tres vueltas seguidas cuesta tres segundos y quita el falso -100.
        if (++_nobodyStandingPolls < PollsBeforeCallingItAWipe)
        {
            return null;
        }

        _partyWasStanding = false;
        _nobodyStandingPolls = 0;

        var fallen = snapshot.Party
            .Select(member => string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname)
            .ToList();

        return await penalties.ChargeWipeAsync(runId, actor, fallen, ct).ConfigureAwait(false);
    }

    /// <summary>The run the wipe state belongs to; another run starts it again.</summary>
    private Guid? _stateRun;

    /// <summary>
    /// The run's last wipe is newer than its last death: nobody has fallen since, so the party is still down from it.
    /// </summary>
    /// <remarks>
    /// The deaths of a wipe are recorded before the wipe itself (<c>GameLinkMonitor</c> checks them first), so a wipe
    /// always comes after the deaths that made it. A later death means somebody was standing again, and a new wipe can
    /// happen; none means this is the same one.
    /// </remarks>
    private async Task<bool> StillDownFromLastWipeAsync(Guid runId, CancellationToken ct)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        var lastWipe = history.Where(e => e.Type == GameEventType.TeamWiped).Select(e => (DateTimeOffset?)e.Timestamp).Max();
        var lastDeath = history.Where(e => e.Type == GameEventType.PokemonDied).Select(e => (DateTimeOffset?)e.Timestamp).Max();

        return lastWipe is { } wipe && (lastDeath is not { } death || wipe >= death);
    }
}
