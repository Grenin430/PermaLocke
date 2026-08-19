using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>
/// Tracks how far the player has got, which is what decides the level cap in force.
/// </summary>
/// <remarks>
/// Advancing is deliberately an explicit act with an event behind it. Detecting a cleared trial
/// from memory would need flags nobody has located yet, and guessing would silently move the
/// cap — the one thing that must never happen quietly in this project.
/// </remarks>
public sealed class ProgressService(
    IRunRepository runs,
    IEventStore events,
    IRunContext context,
    LevelCapTable caps,
    IClock clock)
{
    public LevelCapStage? CurrentStage(Run run) => caps.Current(run.ClearedStages);

    public int? CurrentCap(Run run) => caps.CapFor(run.ClearedStages);

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
