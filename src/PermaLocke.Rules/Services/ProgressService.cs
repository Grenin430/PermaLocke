using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Rules.Services;

/// <summary>
/// Tracks how far the player has got, which is what decides the level cap in force.
/// </summary>
/// <remarks>
/// <para>
/// The cap used to move only when somebody pressed a button, because detecting a cleared trial
/// meant event flags nobody had located. That is no longer true: the twelve trials count
/// themselves off the Z-Crystal that lands in the bag, so the stage the run is on can be worked
/// out instead of remembered. Which achievement clears which stage is configuration, in
/// <c>Data/levelcaps.json</c>.
/// </para>
/// <para>
/// The manual count is kept and the two are combined with <c>Math.Max</c>. Detection can only ever
/// <em>raise</em> the cap: if it lags or a save cannot be read, the run falls back to what was
/// pressed, and a cap never drops under a team that is already legal.
/// </para>
/// </remarks>
public sealed class ProgressService(
    IRunRepository runs,
    IEventStore events,
    IRunContext context,
    LevelCapTable caps,
    AchievementService achievements,
    IClock clock)
{
    /// <summary>
    /// How long a detection is trusted before asking again.
    /// </summary>
    /// <remarks>
    /// Working out the stage means reading and parsing the save file, and the game link asks for
    /// the cap every three seconds. Twenty is short enough that a trial cleared now takes effect
    /// almost at once, and long enough not to re-parse half a megabyte on every tick.
    /// </remarks>
    private static readonly TimeSpan DetectionLife = TimeSpan.FromSeconds(20);

    private (Guid Run, DateTimeOffset At, int Cleared)? _detected;

    public LevelCapStage? CurrentStage(Run run) => caps.Current(run.ClearedStages);

    public int? CurrentCap(Run run) => caps.CapFor(run.ClearedStages);

    /// <summary>Stages cleared, counting what the achievements already know.</summary>
    public async Task<int> ClearedAsync(Run run, CancellationToken ct = default) =>
        Math.Max(run.ClearedStages, await DetectedAsync(run, ct).ConfigureAwait(false));

    /// <summary>The stage the run is about to face, taking detection into account.</summary>
    public async Task<LevelCapStage?> CurrentStageAsync(Run run, CancellationToken ct = default) =>
        caps.Current(await ClearedAsync(run, ct).ConfigureAwait(false));

    /// <summary>The cap in force, taking detection into account.</summary>
    public async Task<int?> CurrentCapAsync(Run run, CancellationToken ct = default) =>
        caps.CapFor(await ClearedAsync(run, ct).ConfigureAwait(false));

    /// <summary>
    /// The highest stage whose achievement is already unlocked.
    /// </summary>
    /// <remarks>
    /// The highest rather than the count: if detection sees the third trial but not the second,
    /// the player is plainly past the third, and counting would leave the cap a stage behind.
    /// </remarks>
    private async Task<int> DetectedAsync(Run run, CancellationToken ct)
    {
        if (_detected is { } cached && cached.Run == run.Id && clock.Now - cached.At < DetectionLife)
        {
            return cached.Cleared;
        }

        try
        {
            var progress = await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false);
            var unlocked = progress
                .Where(p => p.Unlocked)
                .Select(p => p.Achievement.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var cleared = caps.Stages
                .Where(stage => stage.Achievement is { } id && unlocked.Contains(id))
                .Select(stage => stage.Order)
                .DefaultIfEmpty(0)
                .Max();

            _detected = (run.Id, clock.Now, cleared);
            return cleared;
        }
        catch (Exception)
        {
            // Sin detección se juega con lo que haya pulsado el jugador. Nunca al revés.
            return 0;
        }
    }

    /// <param name="delta">+1 after clearing a milestone, -1 to undo a mistake.</param>
    public async Task<Run> AdvanceAsync(Run run, int delta, string actor, CancellationToken ct = default)
    {
        var cleared = Math.Clamp(run.ClearedStages + delta, 0, caps.Stages.Count);
        var updated = run with { ClearedStages = cleared };

        await runs.SaveAsync(updated, ct).ConfigureAwait(false);

        var stage = caps.Current(cleared);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.LevelCapEnforced,
            Source = EventSource.Player,
            Actor = actor,
            Description = delta > 0
                ? $"Etapa superada. Cap de nivel: {stage?.Level.ToString() ?? "sin definir"} ({stage?.Name})."
                : $"Etapa revertida. Cap de nivel: {stage?.Level.ToString() ?? "sin definir"} ({stage?.Name}).",
            Data = new Dictionary<string, string>
            {
                ["etapasSuperadas"] = cleared.ToString(),
                ["cap"] = stage?.Level.ToString() ?? string.Empty,
                ["etapa"] = stage?.Name ?? string.Empty
            }
        }, ct).ConfigureAwait(false);

        context.SetCurrent(updated);
        return updated;
    }
}
