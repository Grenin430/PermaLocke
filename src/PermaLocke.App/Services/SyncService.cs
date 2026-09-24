using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <param name="Profile">This machine's player, or null before one exists.</param>
/// <param name="Ownership">Whose the loaded run is.</param>
public sealed record PlayerStatus(PlayerProfile? Profile, RunOwnership Ownership);

/// <summary>
/// The summary of the loaded run and the history it comes from, for <see cref="TournamentUpload"/>. The shared folder
/// it used to publish to is gone (2026-09-24): the tournament server replaced it.
/// </summary>
public sealed class SyncService(
    IRunContext runContext,
    IEventStore events,
    IPokemonRepository pokemon,
    ProgressService progress,
    AchievementService achievements,
    IRunRoles roles,
    PlayerProfileService profiles,
    IBoxReader boxes,
    ILogger<SyncService> logger)
{
    /// <summary>This machine's player and whose the loaded run is, linking an unowned run on the way.</summary>
    public async Task<PlayerStatus> PlayerAsync(CancellationToken ct = default)
    {
        var ownership = await profiles.LinkCurrentRunAsync(ct);
        var profile = ownership == RunOwnership.NoRun
            ? await profiles.CurrentAsync(ct)
            : await profiles.EnsureAsync(runContext.Current?.PlayerName, ct);

        return new PlayerStatus(profile, ownership);
    }

    /// <summary>
    /// Builds the summary of the loaded run and the history it comes from. Reads only; publishing is
    /// a separate step.
    /// </summary>
    /// <remarks>
    /// Both out of <b>one</b> read of the chain, and the points summed from it rather than asked of
    /// the points service a moment later: an event landing in between would otherwise publish a
    /// snapshot that does not match its own history, and the audit would call an honest run wrong.
    /// </remarks>
    public async Task<(RunSnapshot Snapshot, RunHistory History)?> BuildAsync(PlayerProfile profile,
        CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return null;
        }

        var team = await pokemon.GetAllAsync(run.Id, ct);
        var history = await events.GetAllAsync(run.Id, ct);
        var unlocked = (await achievements.GetProgressAsync(run.Id, ct)).Count(a => a.Unlocked);

        var snapshot = new RunSnapshot
        {
            RunId = run.Id,
            PlayerName = profile.Name,
            PlayerId = profile.Id,
            RunName = run.Name,
            RoleName = roles.Of(run.Id)?.Name ?? run.RoleId,
            SeedLabel = run.SeedLabel,
            Points = history.Sum(e => e.PointsDelta),
            Registered = team.Count,
            Alive = team.Count(p => p.Status == PokemonStatus.Alive),
            Dead = team.Count(p => p.Status == PokemonStatus.Dead),
            Traded = team.Count(p => p.Status == PokemonStatus.Traded),
            StagesCleared = await progress.ClearedAsync(run, ct),
            LevelCap = await progress.CurrentCapAsync(run, ct),
            AchievementsUnlocked = unlocked,
            AchievementsTotal = achievements.All.Count,
            EventCount = history.Count,
            ChainHead = history.Count > 0 ? history[^1].Hash : string.Empty,
            BattleReady = BattleReadyFrom(history),
            WorldSpecies = WorldSpeciesFrom(history),
            AvatarSpecies = await AvatarAsync(ct),
            RunCreatedAt = run.CreatedAt,
            PublishedAt = DateTimeOffset.Now
        };

        return (snapshot, new RunHistory
        {
            RunId = run.Id,
            PlayerId = profile.Id,
            ExportedAt = snapshot.PublishedAt,
            Events = history
        });
    }

    /// <summary>
    /// Whether this world can link-battle, worked out from the last randomization that actually
    /// ran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// From the <b>event</b> and never from <c>randomizer.json</c>. The file says how the next
    /// generation would be configured; the event says how the world being played was made. Somebody
    /// who edits the file and does not regenerate has two different answers, and the one that
    /// decides whether they can battle is the second. Same principle as the <c>gratis</c> mark and
    /// the roulette credit (§63, §64): what is charged comes from what the event said happened.
    /// </para>
    /// <para>
    /// Null when the run was randomized before this was recorded. That is "not known", not "no".
    /// </para>
    /// </remarks>
    private static bool? BattleReadyFrom(IReadOnlyList<GameEvent> history)
    {
        var last = history.LastOrDefault(e => e.Type == GameEventType.RomRandomized);

        if (last is null
            || !last.Data.TryGetValue("shuffleBaseStats", out var stats)
            || !last.Data.TryGetValue("randomizeAbilities", out var abilities)
            || !bool.TryParse(stats, out var shuffled)
            || !bool.TryParse(abilities, out var randomAbilities))
        {
            return null;
        }

        // Compatible es que NINGUNA de las dos toque los datos de combate. Que dos mundos con las
        // mismas opciones a true y la misma seed tambien valdrian es cierto y no se modela: nadie
        // juega asi, y una regla que casi nunca se cumple es una regla que confunde.
        return !shuffled && !randomAbilities;
    }

    /// <summary>The species leading the saved party, for the friends list. Decoration: null if the save will not read.</summary>
    private async Task<int?> AvatarAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await boxes.ReadAsync(ct);
            return snapshot.Boxes.FirstOrDefault(b => b.IsParty)?.Pokemon
                .FirstOrDefault(p => !p.IsEgg && p.Species > 0)?.Species;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Sin avatar: no se ha podido leer el equipo");
            return null;
        }
    }

    /// <summary>How many species the last generated world had, from its event. Null when not recorded.</summary>
    private static int? WorldSpeciesFrom(IReadOnlyList<GameEvent> history) =>
        history.LastOrDefault(e => e.Type == GameEventType.RomRandomized) is { } last
        && last.Data.TryGetValue("maxSpecies", out var raw)
        && int.TryParse(raw, out var species)
        && species > 0
            ? species
            : null;
}
