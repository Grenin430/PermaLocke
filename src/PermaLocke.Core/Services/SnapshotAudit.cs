using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

public enum AuditVerdict
{
    /// <summary>No history was published with the snapshot, so there is nothing to check it against.</summary>
    NoHistory,

    /// <summary>The chain verifies and the snapshot's numbers come out of it.</summary>
    Consistent,

    /// <summary>The published history itself does not verify.</summary>
    ChainBroken,

    /// <summary>The history verifies but the snapshot says something it does not.</summary>
    DoesNotMatch,

    /// <summary>
    /// This machine had already seen more of this run, or a different version of what it saw: a copy
    /// was restored or the history was rewritten.
    /// </summary>
    Rewound,
}

public sealed record AuditResult(AuditVerdict Verdict, string Detail);

/// <summary>How far this machine has seen a run: how many events, and the hash of the last one.</summary>
/// <remarks>
/// A chain that verifies can still be an old copy. The only thing that tells a restored run from an
/// honest one is having seen it further along before, so each application keeps the furthest point
/// it has seen of every run and checks every new snapshot against it (§123).
/// </remarks>
public sealed record SeenMark(int EventCount, string ChainHead);

/// <summary>
/// Checks a published snapshot against the history published beside it.
/// </summary>
/// <remarks>
/// <para>
/// Only what the events can answer is checked: the event count, the last hash and the points, which
/// are the sum of every event's delta and nothing else (<c>PointsService.GetBalanceAsync</c>). The
/// Pokémon counts come from another table and are not in the chain, so they are not claimed.
/// </para>
/// <para>
/// «Consistent» means the snapshot was not edited and the history was not tampered with in the ways a
/// chain catches. It does not mean nobody cheated: a whole history rebuilt with tools verifies too.
/// The wording on screen says exactly that (§123).
/// </para>
/// </remarks>
public static class SnapshotAudit
{
    /// <param name="seen">The furthest this machine had seen this run before, or null the first time.</param>
    public static AuditResult Check(RunSnapshot snapshot, RunHistory? history, SeenMark? seen = null)
    {
        if (seen is not null && seen.EventCount > 0)
        {
            if (snapshot.EventCount < seen.EventCount)
            {
                return new AuditResult(AuditVerdict.Rewound,
                    "Ha vuelto a una copia anterior de su run.");
            }

            var sameLength = snapshot.EventCount == seen.EventCount
                             && !string.Equals(snapshot.ChainHead, seen.ChainHead, StringComparison.Ordinal);

            var rewrittenPrefix = history is not null
                                  && history.Events.Count >= seen.EventCount
                                  && !string.Equals(history.Events[seen.EventCount - 1].Hash, seen.ChainHead,
                                      StringComparison.Ordinal);

            if (sameLength || rewrittenPrefix)
            {
                return new AuditResult(AuditVerdict.Rewound,
                    "Su historial se ha cambiado.");
            }
        }

        if (history is null)
        {
            return new AuditResult(AuditVerdict.NoHistory,
                "Sus números no se pueden comprobar.");
        }

        if (history.RunId != snapshot.RunId)
        {
            return new AuditResult(AuditVerdict.DoesNotMatch,
                "Su historial es de otra run.");
        }

        if (history.Events.Any(e => e.RunId != snapshot.RunId))
        {
            return new AuditResult(AuditVerdict.ChainBroken,
                "Su historial es de otra run.");
        }

        var chain = EventChain.Verify(history.Events);

        if (!chain.IsValid)
        {
            return new AuditResult(AuditVerdict.ChainBroken,
                "Su historial está dañado.");
        }

        if (history.Events.Count != snapshot.EventCount)
        {
            return new AuditResult(AuditVerdict.DoesNotMatch,
                "Sus números no cuadran.");
        }

        var head = history.Events.Count > 0 ? history.Events[^1].Hash : string.Empty;

        if (!string.Equals(head, snapshot.ChainHead, StringComparison.Ordinal))
        {
            return new AuditResult(AuditVerdict.DoesNotMatch,
                "Sus números no cuadran.");
        }

        var points = history.Events.Sum(e => e.PointsDelta);

        if (points != snapshot.Points)
        {
            return new AuditResult(AuditVerdict.DoesNotMatch,
                $"Dice tener {snapshot.Points} puntos y le salen {points}.");
        }

        return new AuditResult(AuditVerdict.Consistent,
            "Sus números cuadran.");
    }

    /// <summary>
    /// The mark to remember after checking: moves forward only, and never past a snapshot that did
    /// not check out, so a restored copy keeps being flagged instead of becoming the new normal.
    /// </summary>
    public static SeenMark? Advance(SeenMark? seen, RunSnapshot snapshot, AuditResult audit)
    {
        if (audit.Verdict is not (AuditVerdict.Consistent or AuditVerdict.NoHistory))
        {
            return seen;
        }

        return seen is null || snapshot.EventCount > seen.EventCount
            ? new SeenMark(snapshot.EventCount, snapshot.ChainHead)
            : seen;
    }

    /// <summary>
    /// The positions in a run's upload log, in arrival order, where it went backwards: fewer events than the upload
    /// before (restored from a copy), or the same number with another chain head (history rewritten).
    /// </summary>
    public static IEnumerable<int> Rewinds(IReadOnlyList<SeenMark> uploads)
    {
        for (var i = 1; i < uploads.Count; i++)
        {
            var (before, now) = (uploads[i - 1], uploads[i]);

            if (now.EventCount < before.EventCount
                || (now.EventCount == before.EventCount && now.ChainHead != before.ChainHead))
            {
                yield return i;
            }
        }
    }
}
