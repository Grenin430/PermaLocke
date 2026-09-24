using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>What the reminder has for one Pokémon.</summary>
/// <param name="Known">Its four move slots as they are, zero for an empty one.</param>
/// <param name="Learnset">False when its species' learnset could not be read: the list is then incomplete, and says so.</param>
/// <param name="FirstKnownRecorded">
/// False when PermaLocke never wrote down what it knew on arrival — anything registered before that was recorded.
/// </param>
public sealed record MoveReminderOptions(
    IReadOnlyList<int> Known,
    IReadOnlyList<RememberableMove> Moves,
    int Level,
    bool Learnset,
    bool FirstKnownRecorded);

/// <summary>
/// The move reminder: what a Pokémon can be reminded of, and teaching it one.
/// </summary>
/// <remarks>
/// <para>
/// Why it lives in the application and not in the game: Ultra Moon's own reminder is the lady in the Pokémon
/// Center at the foot of the league, a whole game away, and <b>moving her is not something PermaLocke can do
/// reliably</b> — people and their scripts are in overworld files nobody here has identified, and a script written
/// wrong hangs the game. What the lady does, on the other hand, is simple to state, and stating it is all this needs.
/// </para>
/// <para>
/// The rule is <see cref="MoveReminder"/>. This adds the three things that make it safe to act on. The list is
/// <b>recomputed here</b> from the world's tables, never taken from the screen, so the only moves that can be
/// written are the ones the rule allows. A fallen Pokémon is refused, as the wonder trade refuses one. And the
/// order is the shop's: <b>write first, record after</b>, so the log never claims a move the save refused.
/// </para>
/// <para>
/// It costs nothing, like training EVs: it is an edit to a Pokémon the player already owns, and a price would be a
/// rule the competition never agreed. Nothing stops adding one later through <c>Data/</c>.
/// </para>
/// </remarks>
public sealed class MoveReminderService(IMoveCatalog catalog, IMoveTeacher teacher, IEventStore events,
    IPokemonRepository pokemon, IClock clock)
{
    /// <summary>A Pokémon has four move slots.</summary>
    public const int MoveSlots = 4;

    public string Source => catalog.Source;

    public MoveSheet? Describe(int move) => catalog.Describe(move);

    /// <summary>True when a move could be written right now, and why not when it cannot.</summary>
    public bool CanTeachNow(out string reason) => teacher.CanTeachNow(out reason);

    /// <summary>Everything the reminder offers this Pokémon today.</summary>
    public async Task<MoveReminderOptions> OptionsAsync(Guid runId, BoxedPokemon target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var known = Known(target);
        var learnset = catalog.LevelUp(target.Species, target.Form);
        var (arrival, recorded) = await FirstKnownAsync(runId, target.Pid, ct).ConfigureAwait(false);

        // Los «para volver a aprender» que el propio juego guarda en cada Pokémon cuentan como de cuando lo
        // conseguiste: es la misma idea, puesta por el juego.
        var firstKnown = arrival.Concat(target.RelearnMoveIds ?? []).Where(move => catalog.Describe(move) is not null);

        // Un prohibido no se ofrece por ningún camino, tampoco como «lo que sabía al llegar» (§162).
        var moves = MoveReminder.Options(learnset ?? [], target.LevelForStats, known, firstKnown)
            .Where(option => catalog.Describe(option.Move) is { PP: > 0 } && !catalog.IsBanned(option.Move))
            .ToList();

        return new MoveReminderOptions(known, moves, target.LevelForStats, learnset is not null, recorded);
    }

    /// <summary>
    /// Teaches <paramref name="move"/> in <paramref name="moveSlot"/>, over whatever is there.
    /// </summary>
    /// <remarks>
    /// An empty slot is filled from the front: a Pokémon's moves have no holes in the game, and a move written
    /// into the fourth slot behind an empty third is a layout the game never makes.
    /// </remarks>
    public async Task<DeliveryResult> TeachAsync(Run run, BoxedPokemon target, int move, int moveSlot,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);

        // Lo mismo que los EV (§97): una entrada que no cuadra con su firma no se escribe nunca.
        if (!target.IsIntact)
        {
            return Refused($"{target.DisplayName} está dañado en la partida y no se toca.");
        }

        if (target.IsEgg)
        {
            return Refused("Un huevo no aprende movimientos.");
        }

        if (moveSlot is < 0 or >= MoveSlots)
        {
            return Refused("Ese hueco de movimiento no existe.");
        }

        if (await IsFallenAsync(run.Id, target.Pid, ct).ConfigureAwait(false))
        {
            return Refused($"{target.DisplayName} está caído. Un caído no aprende nada.");
        }

        var options = await OptionsAsync(run.Id, target, ct).ConfigureAwait(false);

        if (options.Moves.FirstOrDefault(option => option.Move == move) is not { } chosen)
        {
            return Refused($"{target.DisplayName} no puede recordar ese movimiento.");
        }

        if (catalog.Describe(move) is not { PP: > 0 } sheet)
        {
            return Refused("Ese movimiento no existe en el juego que estás jugando.");
        }

        var slot = options.Known[moveSlot] == 0 ? FirstEmpty(options.Known) : moveSlot;
        var replaced = options.Known[slot];

        var change = new MoveChange(target.Box, target.Slot, target.Pid, target.DisplayName, slot, replaced, move,
            sheet.PP);

        var result = await teacher.ApplyAsync(change, ct).ConfigureAwait(false);

        if (!result.Delivered)
        {
            return result;
        }

        await RecordAsync(run, target, chosen, sheet, change, ct).ConfigureAwait(false);
        return result;
    }

    private static DeliveryResult Refused(string why) => new(DeliveryOutcome.Failed, why);

    private static IReadOnlyList<int> Known(BoxedPokemon target)
    {
        var known = new int[MoveSlots];
        var ids = target.MoveIds ?? [];

        for (var index = 0; index < MoveSlots && index < ids.Count; index++)
        {
            known[index] = ids[index];
        }

        return known;
    }

    private static int FirstEmpty(IReadOnlyList<int> known)
    {
        for (var index = 0; index < known.Count; index++)
        {
            if (known[index] == 0)
            {
                return index;
            }
        }

        return known.Count - 1;
    }

    private async Task<bool> IsFallenAsync(Guid runId, uint pid, CancellationToken ct)
    {
        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Any(p => p.Pid == pid && p.Status == PokemonStatus.Dead);
    }

    /// <summary>
    /// The moves it knew when PermaLocke registered it, from the event that registered it.
    /// </summary>
    /// <remarks>
    /// Read from the event and not kept anywhere else: the event is what the run says happened, and a second copy
    /// would be one more thing that could disagree with it. Anything registered before the events carried the
    /// moves simply has none, and <c>recorded</c> says so instead of pretending the list is complete.
    /// </remarks>
    private async Task<(IReadOnlyList<int> Moves, bool Recorded)> FirstKnownAsync(Guid runId, uint pid,
        CancellationToken ct)
    {
        var entries = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        var origins = entries.Where(p => p.Pid == pid && p.OriginEventId is not null)
            .Select(p => p.OriginEventId!.Value)
            .ToHashSet();

        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);

        foreach (var gameEvent in history)
        {
            var mine = origins.Contains(gameEvent.Id)
                       || (gameEvent.Data.TryGetValue(PidKey, out var text) && text == pid.ToString("X8"));

            if (mine && gameEvent.Data.TryGetValue(FirstMovesKey, out var list))
            {
                return (MoveReminder.ParseList(list), true);
            }
        }

        return ([], false);
    }

    /// <summary>The key under which an arrival event keeps the moves the Pokémon came with.</summary>
    public const string FirstMovesKey = "movimientos";

    /// <summary>The key under which an event names the Pokémon it is about.</summary>
    private const string PidKey = "pid";

    private Task RecordAsync(Run run, BoxedPokemon target, RememberableMove chosen, MoveSheet sheet, MoveChange change,
        CancellationToken ct)
    {
        var replacedName = change.Replaced == 0
            ? null
            : catalog.Describe(change.Replaced)?.Name ?? $"#{change.Replaced}";

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.MoveRemembered,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = replacedName is null
                ? $"{target.DisplayName} recuerda {sheet.Name}."
                : $"{target.DisplayName} olvida {replacedName} y recuerda {sheet.Name}.",
            Data = new Dictionary<string, string>
            {
                ["donde"] = change.Where,
                ["especie"] = target.Species.ToString(),
                ["forma"] = target.Form.ToString(),
                ["especieNombre"] = target.SpeciesName,
                [PidKey] = target.Pid.ToString("X8"),
                ["movimiento"] = sheet.Id.ToString(),
                ["movimientoNombre"] = sheet.Name,
                ["hueco"] = (change.MoveSlot + 1).ToString(),
                ["olvida"] = change.Replaced.ToString(),
                ["origen"] = chosen.From switch
                {
                    RememberedFrom.Evolution => "evolución",
                    RememberedFrom.Level => $"nivel {chosen.Level}",
                    _ => "de cuando lo conseguiste"
                },
                ["nivel"] = target.LevelForStats.ToString(),
                ["fuente"] = catalog.Source
            }
        }, ct);
    }
}
