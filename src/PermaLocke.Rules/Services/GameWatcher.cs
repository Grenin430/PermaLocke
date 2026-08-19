using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

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
public sealed class GameWatcher(IPokemonRepository pokemon, IEventStore events, IClock clock)
{
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
    public async Task RecordDeathAsync(PokemonEntry entry, string actor, CancellationToken ct = default)
    {
        var died = await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = entry.RunId,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonDied,
            Source = EventSource.AutoDetect,
            Actor = actor,
            Description = $"{entry.Nickname ?? entry.SpeciesName} ha caído a 0 PS.",
            PokemonId = entry.Id,
            LocationId = entry.LocationId,
            Data = new Dictionary<string, string>
            {
                ["especie"] = entry.Species.ToString(),
                ["nivel"] = entry.Level.ToString(),
                ["deteccion"] = "memoria del juego"
            }
        }, ct).ConfigureAwait(false);

        await pokemon.SaveAsync(entry with
        {
            Status = PokemonStatus.Dead,
            DiedAt = died.Timestamp
        }, ct).ConfigureAwait(false);
    }
}
