using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>Whose the loaded run is, from this machine's point of view.</summary>
public enum RunOwnership
{
    /// <summary>No run is loaded.</summary>
    NoRun,

    /// <summary>It already belonged to this player.</summary>
    Mine,

    /// <summary>It had no owner and was just linked to this player, with its event.</summary>
    Linked,

    /// <summary>It belongs to another player's profile. It is never taken over.</summary>
    Foreign,
}

/// <summary>
/// This machine's player: creating the profile, renaming it, and tying the loaded run to it.
/// </summary>
/// <remarks>
/// <para>
/// The profile is not run state — renaming a player moves no point and no Pokémon — so changing it
/// writes no event. Tying a run to a player <em>is</em> run state, because from then on that run is
/// published under that player, so linking writes <see cref="GameEventType.PlayerLinked"/> (rule 4).
/// </para>
/// <para>
/// A run that already has an owner is never reassigned. The case is not hypothetical: a run folder
/// copied from a friend's PC would otherwise be published as this player's.
/// </para>
/// </remarks>
public sealed class PlayerProfileService(IPlayerProfileStore store, IRunRepository runs, IEventStore events,
    IRunContext context, IClock clock)
{
    /// <summary>The name nobody typed: used only when there is nothing better to go on.</summary>
    public const string FallbackName = "Jugador";

    /// <summary>
    /// One link or profile creation at a time. Measured on the real run: the application's start-up and the
    /// COMPETICIÓN screen both linked the same unowned run in the same second, and the history got two
    /// <see cref="GameEventType.PlayerLinked"/> (§125). The chain cannot lose the second one, so it stays.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task<PlayerProfile?> CurrentAsync(CancellationToken ct = default) => store.LoadAsync(ct);

    /// <summary>
    /// Returns this machine's profile, creating it the first time with the name given.
    /// </summary>
    /// <remarks>
    /// The name offered is the one the player already typed into their run, so nobody who has been
    /// playing for weeks is asked who they are: they already said.
    /// </remarks>
    public async Task<PlayerProfile> EnsureAsync(string? suggestedName, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await EnsureLockedAsync(suggestedName, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PlayerProfile> EnsureLockedAsync(string? suggestedName, CancellationToken ct)
    {
        if (await store.LoadAsync(ct).ConfigureAwait(false) is { } existing)
        {
            return existing;
        }

        var profile = new PlayerProfile
        {
            Id = Guid.NewGuid(),
            Name = CleanName(suggestedName) ?? FallbackName,
            CreatedAt = clock.Now
        };

        await store.SaveAsync(profile, ct).ConfigureAwait(false);
        return profile;
    }

    /// <summary>Changes the name everybody reads. The id, which everything keys on, stays.</summary>
    /// <returns>The renamed profile, or null when the name is empty once cleaned.</returns>
    public async Task<PlayerProfile?> RenameAsync(string name, CancellationToken ct = default)
    {
        if (CleanName(name) is not { } clean)
        {
            return null;
        }

        var profile = await EnsureAsync(clean, ct).ConfigureAwait(false);
        var renamed = profile with { Name = clean };

        await store.SaveAsync(renamed, ct).ConfigureAwait(false);
        return renamed;
    }

    /// <summary>
    /// Ties the loaded run to this player if it has no owner yet, and says whose it is.
    /// </summary>
    public async Task<RunOwnership> LinkCurrentRunAsync(CancellationToken ct = default)
    {
        if (context.Current is not { } loaded)
        {
            return RunOwnership.NoRun;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await LinkLockedAsync(loaded, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RunOwnership> LinkLockedAsync(Run loaded, CancellationToken ct)
    {
        // Leída otra vez dentro del cerrojo: la run que traía quien llamó puede ser la de antes de que otro la
        // vinculara, y decidir con esa es justo como salió el evento duplicado.
        var run = await runs.GetAsync(loaded.Id, ct).ConfigureAwait(false) ?? loaded;
        var profile = await EnsureLockedAsync(run.PlayerName, ct).ConfigureAwait(false);

        if (run.PlayerId == profile.Id)
        {
            if (context.Current?.PlayerId != profile.Id && context.Current?.Id == run.Id)
            {
                context.SetCurrent(run);
            }

            return RunOwnership.Mine;
        }

        if (run.PlayerId is not null)
        {
            return RunOwnership.Foreign;
        }

        var linked = run with { PlayerId = profile.Id };
        await runs.SaveAsync(linked, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PlayerLinked,
            Source = EventSource.System,
            Actor = profile.Name,
            Description = $"Run «{run.Name}» vinculada al jugador {profile.Name} ({profile.ShortId}).",
            Data = new Dictionary<string, string>
            {
                ["playerId"] = profile.Id.ToString("N"),
                ["playerName"] = profile.Name
            }
        }, ct).ConfigureAwait(false);

        context.SetCurrent(linked);
        return RunOwnership.Linked;
    }

    /// <summary>
    /// A name trimmed, with control characters removed and cut to <see cref="PlayerProfile.MaxNameLength"/>.
    /// Null when nothing is left.
    /// </summary>
    public static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var kept = new string([.. name.Where(c => !char.IsControl(c))]).Trim();

        if (kept.Length > PlayerProfile.MaxNameLength)
        {
            kept = kept[..PlayerProfile.MaxNameLength].TrimEnd();
        }

        return kept.Length == 0 ? null : kept;
    }
}
