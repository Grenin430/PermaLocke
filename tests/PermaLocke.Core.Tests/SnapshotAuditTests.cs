using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Checking a published snapshot against the history published beside it (§123). Each test is one
/// thing a player could do to the files, and what the check has to say about it.
/// </summary>
public sealed class SnapshotAuditTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    /// <summary>A chain sealed exactly as the event store seals it.</summary>
    private static List<GameEvent> Chain(params int[] deltas)
    {
        var events = new List<GameEvent>();
        var previous = string.Empty;

        for (var i = 0; i < deltas.Length; i++)
        {
            var sealedEvent = EventHasher.Seal(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = RunId,
                Timestamp = new DateTimeOffset(2026, 9, 14, 12, i, 0, TimeSpan.Zero),
                Type = GameEventType.PointsEarned,
                Source = EventSource.System,
                Actor = "Grenin",
                Description = $"evento {i}",
                PointsDelta = deltas[i],
                Data = new Dictionary<string, string> { ["n"] = i.ToString() }
            }, previous);

            events.Add(sealedEvent);
            previous = sealedEvent.Hash;
        }

        return events;
    }

    private static RunSnapshot SnapshotOf(IReadOnlyList<GameEvent> events) => new()
    {
        RunId = RunId,
        PlayerName = "Grenin",
        RunName = "PRUEBA",
        Points = events.Sum(e => e.PointsDelta),
        EventCount = events.Count,
        ChainHead = events.Count > 0 ? events[^1].Hash : string.Empty
    };

    private static RunHistory HistoryOf(IReadOnlyList<GameEvent> events) => new() { RunId = RunId, Events = events };

    [Fact]
    public void An_untouched_run_checks_out()
    {
        var events = Chain(100, -25, 50);

        var audit = SnapshotAudit.Check(SnapshotOf(events), HistoryOf(events));

        Assert.Equal(AuditVerdict.Consistent, audit.Verdict);
    }

    [Fact]
    public void Points_edited_in_the_snapshot_do_not_match()
    {
        var events = Chain(100, -25);

        var audit = SnapshotAudit.Check(SnapshotOf(events) with { Points = 900 }, HistoryOf(events));

        Assert.Equal(AuditVerdict.DoesNotMatch, audit.Verdict);
        Assert.Contains("900", audit.Detail);
    }

    /// <summary>Removing the −25 of a death from the history breaks the chain after it.</summary>
    [Fact]
    public void A_death_deleted_from_the_history_breaks_the_chain()
    {
        var events = Chain(100, -25, 50);
        var withoutDeath = new List<GameEvent> { events[0], events[2] };

        var audit = SnapshotAudit.Check(SnapshotOf(withoutDeath), HistoryOf(withoutDeath));

        Assert.Equal(AuditVerdict.ChainBroken, audit.Verdict);
    }

    [Fact]
    public void An_event_edited_in_place_breaks_the_chain()
    {
        var events = Chain(100, -25, 50);
        events[1] = events[1] with { PointsDelta = 0 };

        var audit = SnapshotAudit.Check(SnapshotOf(events), HistoryOf(events));

        Assert.Equal(AuditVerdict.ChainBroken, audit.Verdict);
    }

    [Fact]
    public void Without_history_there_is_nothing_to_check()
    {
        var events = Chain(100);

        Assert.Equal(AuditVerdict.NoHistory, SnapshotAudit.Check(SnapshotOf(events), null).Verdict);
    }

    [Fact]
    public void A_history_of_another_run_does_not_match()
    {
        var events = Chain(100);

        var audit = SnapshotAudit.Check(SnapshotOf(events) with { RunId = Guid.NewGuid() }, HistoryOf(events));

        Assert.Equal(AuditVerdict.DoesNotMatch, audit.Verdict);
    }

    /// <summary>
    /// The case a chain alone cannot catch: an older copy is perfectly consistent. What gives it away
    /// is having seen the run further along before.
    /// </summary>
    [Fact]
    public void An_older_copy_is_caught_by_what_was_seen_before()
    {
        var events = Chain(100, -25, 50, -25);
        var seen = new SeenMark(4, events[3].Hash);
        var restored = events.Take(2).ToList();

        var audit = SnapshotAudit.Check(SnapshotOf(restored), HistoryOf(restored), seen);

        Assert.Equal(AuditVerdict.Rewound, audit.Verdict);
        Assert.Equal(AuditVerdict.Consistent, SnapshotAudit.Check(SnapshotOf(restored), HistoryOf(restored)).Verdict);
    }

    /// <summary>Restoring a copy and playing on past where it had been: the part already seen is different.</summary>
    [Fact]
    public void A_rewritten_past_is_caught_even_after_growing_again()
    {
        var original = Chain(100, -25, 50);
        var seen = new SeenMark(3, original[2].Hash);
        var replayed = Chain(100, 30, 50, 10);

        var audit = SnapshotAudit.Check(SnapshotOf(replayed), HistoryOf(replayed), seen);

        Assert.Equal(AuditVerdict.Rewound, audit.Verdict);
    }

    [Fact]
    public void Playing_on_honestly_checks_out_against_what_was_seen()
    {
        var events = Chain(100, -25, 50, 10);
        var seen = new SeenMark(2, events[1].Hash);

        Assert.Equal(AuditVerdict.Consistent, SnapshotAudit.Check(SnapshotOf(events), HistoryOf(events), seen).Verdict);
    }

    /// <summary>The mark moves forward on good snapshots and never onto a bad one.</summary>
    [Fact]
    public void The_seen_mark_only_moves_forward_and_never_past_a_bad_snapshot()
    {
        var events = Chain(100, -25, 50);
        var good = SnapshotOf(events);
        var seen = new SeenMark(2, events[1].Hash);

        var advanced = SnapshotAudit.Advance(seen, good, new AuditResult(AuditVerdict.Consistent, ""));
        var kept = SnapshotAudit.Advance(seen, good, new AuditResult(AuditVerdict.Rewound, ""));
        var notBack = SnapshotAudit.Advance(advanced, SnapshotOf(events.Take(1).ToList()),
            new AuditResult(AuditVerdict.Consistent, ""));

        Assert.Equal(new SeenMark(3, events[2].Hash), advanced);
        Assert.Equal(seen, kept);
        Assert.Equal(advanced, notBack);
    }

    /// <summary>
    /// The tournament server keeps the JSON the application sends (camelCase, event types as text); read back the same
    /// way, an honest run still checks out.
    /// </summary>
    [Fact]
    public void A_run_read_back_from_the_server_still_checks_out()
    {
        var events = Chain(100, -25, 50);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<RunSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(SnapshotOf(events), options), options)!;
        var history = System.Text.Json.JsonSerializer.Deserialize<RunHistory>(
            System.Text.Json.JsonSerializer.Serialize(HistoryOf(events), options), options);

        Assert.Equal(AuditVerdict.Consistent, SnapshotAudit.Check(snapshot, history).Verdict);
    }

    [Fact]
    public void The_upload_log_shows_a_restored_and_a_rewritten_run()
    {
        var log = new List<SeenMark> { new(3, "a"), new(5, "b"), new(4, "c"), new(6, "d"), new(6, "e"), new(7, "f") };

        Assert.Equal([2, 4], SnapshotAudit.Rewinds(log));
    }
}
