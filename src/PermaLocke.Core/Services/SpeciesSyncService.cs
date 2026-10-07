using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>A Pokémon as the game has it right now: who it is, by PID, and which species and form it is.</summary>
public sealed record SeenSpecies(uint Pid, int Species, string SpeciesName, int Form);

/// <summary>
/// Keeps the run's species in step with the game's (2026-10-07). The registry wrote a species when it first saw a Pokémon and
/// never looked again, so a Gimmighoul that became Gholdengo, a Ferroseed that became Ferrothorn or a Yamask that became
/// Cofagrigus stayed as the first one, and the CEMENTERIO showed the old name and icon over a killcam of the new one.
/// By PID, like everything the watcher matches; every change leaves an event.
/// </summary>
public sealed class SpeciesSyncService(IPokemonRepository pokemon, IEventStore events, IClock clock)
{
    /// <summary>
    /// Updates every registered Pokémon whose PID is among <paramref name="seen"/> and whose species or form differs.
    /// An entry without a species (a nursery egg that has not hatched) is left to <see cref="NurseryService.HatchedAsync"/>.
    /// Returns how many changed.
    /// </summary>
    public async Task<int> SyncAsync(Run run, IEnumerable<SeenSpecies> seen, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var wanted = seen.Where(s => s.Species > 0).GroupBy(s => s.Pid).ToDictionary(g => g.Key, g => g.First());
        if (wanted.Count == 0) return 0;

        var changed = 0;

        foreach (var entry in (await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false)).ToList())
        {
            if (entry.Pid is not { } pid || entry.Species <= 0 || !wanted.TryGetValue(pid, out var now)) continue;
            if (entry.Species == now.Species && entry.Form == now.Form) continue;

            // A nickname that is just the old species name would keep showing it.
            var nickname = string.Equals(entry.Nickname, entry.SpeciesName, StringComparison.OrdinalIgnoreCase) ? null : entry.Nickname;
            await pokemon.SaveAsync(entry with { Species = now.Species, SpeciesName = now.SpeciesName, Form = now.Form, Nickname = nickname }, ct)
                .ConfigureAwait(false);

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.PokemonEvolved,
                Source = EventSource.AutoDetect,
                Actor = run.PlayerName,
                Description = $"{entry.SpeciesName} es ahora {now.SpeciesName}.",
                PokemonId = entry.Id,
                Data = new Dictionary<string, string>
                {
                    ["pid"] = pid.ToString("X8"),
                    ["antes"] = entry.Species.ToString(),
                    ["despues"] = now.Species.ToString(),
                    ["forma"] = now.Form.ToString()
                }
            }, ct).ConfigureAwait(false);

            changed++;
        }

        return changed;
    }
}
