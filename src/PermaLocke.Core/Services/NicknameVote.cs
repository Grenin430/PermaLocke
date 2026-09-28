namespace PermaLocke.Core.Services;

/// <summary>One player's part in a nickname vote: the name they proposed and the one they voted for, either may be missing.</summary>
public sealed record NicknameBallot(string Voter, string? Proposal, string? Vote, DateTimeOffset At);

/// <summary>
/// The nicknames the others vote for a capture (2026-09-28, players' list item 9): first everyone proposes a name, then
/// everyone votes among the proposals.
/// </summary>
public static class NicknameVote
{
    /// <summary>The proposals to vote on, each spelled as first written, in the order they came.</summary>
    public static IReadOnlyList<string> Options(IEnumerable<NicknameBallot> ballots) =>
    [
        .. ballots.OrderBy(b => b.At)
            .Select(b => b.Proposal?.Trim() ?? string.Empty)
            .Where(Valid)
            .DistinctBy(p => p.ToUpperInvariant())
    ];

    /// <summary>
    /// The winner: the option with most votes; with no votes at all, the most proposed. A tie goes to the one proposed
    /// first. Null when nobody proposed anything valid.
    /// </summary>
    public static string? Winner(IEnumerable<NicknameBallot> ballots)
    {
        var all = ballots.ToList();
        var options = Options(all);
        if (options.Count == 0) return null;

        var votes = all.Select(b => b.Vote?.Trim()).Where(v => v is not null && Valid(v)).ToList();
        var counted = votes.Count > 0 ? votes : all.Select(b => b.Proposal?.Trim()).Where(p => p is not null && Valid(p)).ToList();

        return options
            .Select((option, order) => (option, order,
                count: counted.Count(c => string.Equals(c, option, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(t => t.count)
            .ThenBy(t => t.order)
            .First().option;
    }

    private static bool Valid(string? name) => name is { Length: > 0 } && RenameService.Problem(name) is null;
}
