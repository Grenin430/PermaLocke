using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Ties a Pokémon the run granted to the one that now exists in the player's game.
/// </summary>
/// <remarks>
/// <para>
/// The gacha and the wonder trade decide <em>what</em> the player gets, and the delivery decides
/// <em>which</em> Pokémon that turns into: the PID is generated when the entity is built, so it
/// does not exist while the roll is being recorded. This runs afterwards, once the save has been
/// written and read back, and stores the identity on the run's own record.
/// </para>
/// <para>
/// It matters more than it looks. <c>GameWatcher</c> matches the live party against the run by
/// PID and by nothing else, so an entry without one is invisible to it: it can faint in front of
/// the app and no death is recorded, and worse, it is announced as an unregistered Pokémon every
/// time the player puts it in the party.
/// </para>
/// </remarks>
public sealed class PokemonIdentityService(IPokemonRepository pokemon, IEventStore events, IClock clock)
{
    /// <summary>
    /// Records the identity the game gave a delivered Pokémon, and where it landed.
    /// </summary>
    /// <returns>The updated entry, or the one it was given when there was nothing to record.</returns>
    public async Task<PokemonEntry> RememberDeliveryAsync(Run run, PokemonEntry entry, uint pid,
        int box, int slot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(entry);

        // Cero no es un PID, es "no me lo han dicho". Guardarlo emparejaría entre sí a todos los
        // que no lo tienen, que es peor que no tener ninguno.
        if (pid == 0 || entry.Pid == pid)
        {
            return entry;
        }

        var updated = entry with { Pid = pid };
        await pokemon.SaveAsync(updated, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonDelivered,
            Source = EventSource.System,
            Actor = run.PlayerName,
            // Caja 0 es el equipo: las cajas se cuentan desde el 1.
            Description = $"{entry.Nickname ?? entry.SpeciesName} está en la partida: "
                          + (box == 0 ? $"en el equipo, hueco {slot}." : $"caja {box}, hueco {slot}."),
            PokemonId = entry.Id,
            Data = new Dictionary<string, string>
            {
                ["pid"] = pid.ToString("X8"),
                ["caja"] = box.ToString(),
                ["hueco"] = slot.ToString(),
                ["origen"] = entry.Origin.ToString()
            }
        }, ct).ConfigureAwait(false);

        return updated;
    }
}
