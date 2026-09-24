using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Rewrites the EVs of a Pokémon the player already owns, and leaves a record of it.
/// </summary>
/// <remarks>
/// <para>
/// The order is the same one the shop settled on and for the same reason: <b>the write happens
/// first and the event is appended afterwards</b>. Recording first would leave the log claiming a
/// training that the save may have refused, and a log that says things that did not happen is
/// worse than one that is a moment behind.
/// </para>
/// <para>
/// Nothing here costs points. Training is an edit the player makes to their own Pokémon, not a
/// purchase, so inventing a price would be inventing a rule the competition never agreed.
/// </para>
/// </remarks>
public sealed class EvTrainingService(IEvTrainer trainer, IEventStore events, IClock clock)
{
    private static readonly string[] StatNames =
        ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    /// <summary>True when EVs could be written right now, and why not when they cannot.</summary>
    public bool CanTrainNow(out string reason) => trainer.CanTrainNow(out reason);

    /// <summary>
    /// Writes <paramref name="wanted"/> over whatever <paramref name="target"/> currently holds.
    /// </summary>
    /// <remarks>
    /// A spread equal to the one already stored is refused before anything is opened. Writing it
    /// would rewrite the whole save and append an event saying nothing changed, which is noise in
    /// a log whose whole job is to be believable.
    /// </remarks>
    public async Task<DeliveryResult> TrainAsync(Run run, BoxedPokemon target, EvSpread wanted,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wanted);

        // Una entrada que no cuadra con su firma no se escribe nunca (§97): PKHeX la devolvería con
        // una firma válida y lo que quedaría en la partida no es un Pokémon reparado, es basura que el
        // juego ya no dibuja como Huevo Malo.
        if (!target.IsIntact)
        {
            return new DeliveryResult(DeliveryOutcome.Failed,
                $"{target.DisplayName} está dañado en la partida y no se toca.");
        }

        var before = EvSpread.Of(target.Evs);

        // El tope de 510 se comprueba aquí y no se recorta al escribirlo, porque recortarlo
        // decidiría por el jugador de qué estadística quitar. La pantalla deja pasarse mientras
        // se reparte; lo que no deja es guardarlo.
        if (!wanted.IsLegal)
        {
            return new DeliveryResult(DeliveryOutcome.Failed,
                $"Te pasas por {wanted.Over} EV. El máximo es {EvSpread.TotalMax}.");
        }

        if (before.Equals(wanted))
        {
            return new DeliveryResult(DeliveryOutcome.Failed,
                $"Los EV de {target.DisplayName} ya son esos.");
        }

        var change = new EvChange(target.Box, target.Slot, target.Pid, target.DisplayName, wanted.Values);
        var result = await trainer.ApplyAsync(change, ct).ConfigureAwait(false);

        if (!result.Delivered)
        {
            return result;
        }

        await RecordAsync(run, target, before, wanted, change, ct).ConfigureAwait(false);
        return result;
    }

    private Task RecordAsync(Run run, BoxedPokemon target, EvSpread before, EvSpread after,
        EvChange change, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.EvsTrained,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"EV de {target.DisplayName}: {before} → {after} ({Moved(before, after)}).",
            Data = new Dictionary<string, string>
            {
                ["donde"] = change.Where,
                ["especie"] = target.Species.ToString(),
                ["especieNombre"] = target.SpeciesName,
                ["pid"] = target.Pid.ToString("X8"),
                ["antes"] = before.ToString(),
                ["despues"] = after.ToString(),
                ["antesTotal"] = before.Total.ToString(),
                ["despuesTotal"] = after.Total.ToString()
            }
        }, ct);

    /// <summary>Which stats actually moved, so the log reads as a change and not as a dump.</summary>
    private static string Moved(EvSpread before, EvSpread after)
    {
        var moved = Enumerable.Range(0, EvSpread.StatCount)
            .Where(index => before[index] != after[index])
            .Select(index => $"{StatNames[index]} {before[index]}→{after[index]}")
            .ToList();

        return moved.Count == 0 ? "sin cambios" : string.Join(", ", moved);
    }
}
