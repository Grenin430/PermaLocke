using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Domain;

/// <summary>One stretch of the emulator being open while a run was loaded.</summary>
/// <param name="Start">When the emulator process started, by its own clock — not when PermaLocke noticed.</param>
/// <param name="End">The last moment it was seen running.</param>
public sealed record PlaySession(DateTimeOffset Start, DateTimeOffset End)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan Length => End > Start ? End - Start : TimeSpan.Zero;
}

/// <summary>How long a run has been played, kept per run next to its <c>run.json</c>.</summary>
/// <remarks>
/// Not an event. Playtime moves no point, no Pokémon and no rule (rule 4), and a heartbeat every half
/// minute written into a hash chain would bury the history under noise. It lives in the run's own
/// folder, so starting again takes it with the run (§125).
/// </remarks>
public interface IPlaytimeStore
{
    Task<IReadOnlyList<PlaySession>> LoadAsync(Guid runId, CancellationToken ct = default);

    Task SaveAsync(Guid runId, IReadOnlyList<PlaySession> sessions, CancellationToken ct = default);
}

/// <summary>Adding up and saying play sessions.</summary>
public static class Playtime
{
    /// <summary>
    /// The sessions with this one recorded: the session with the same start is extended, a new start
    /// adds one. Keyed on the process's start time, so PermaLocke closing and opening again while the
    /// game keeps running goes on with the same session instead of starting a second.
    /// </summary>
    public static IReadOnlyList<PlaySession> Record(IReadOnlyList<PlaySession> sessions, DateTimeOffset start,
        DateTimeOffset seen)
    {
        var list = sessions.ToList();
        var at = list.FindIndex(s => s.Start == start);

        if (at >= 0)
        {
            list[at] = list[at] with { End = seen > list[at].End ? seen : list[at].End };
        }
        else
        {
            list.Add(new PlaySession(start, seen));
        }

        return list;
    }

    public static TimeSpan Total(IEnumerable<PlaySession> sessions) =>
        sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Length);

    public static DateTimeOffset? LastPlayed(IEnumerable<PlaySession> sessions) =>
        sessions.Select(s => (DateTimeOffset?)s.End).DefaultIfEmpty(null).Max();

    /// <summary>"12 h 05 min", "35 min", or "menos de un minuto".</summary>
    public static string Say(TimeSpan span) => span switch
    {
        { TotalMinutes: < 1 } => "menos de un minuto",
        { TotalHours: < 1 } => $"{(int)span.TotalMinutes} min",
        _ => $"{(int)span.TotalHours} h {span.Minutes:00} min"
    };

    /// <summary>"01:24:07", for a session that is running.</summary>
    public static string Clock(TimeSpan span) => $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

    /// <summary>"hoy", "ayer", "hace 3 días", or "nunca".</summary>
    public static string Ago(DateTimeOffset? when, DateTimeOffset now)
    {
        if (when is not { } at)
        {
            return "nunca";
        }

        var days = (now.LocalDateTime.Date - at.LocalDateTime.Date).Days;

        return days switch
        {
            <= 0 => "hoy",
            1 => "ayer",
            < 30 => $"hace {days} días",
            _ => at.LocalDateTime.ToString("dd/MM/yyyy")
        };
    }
}
