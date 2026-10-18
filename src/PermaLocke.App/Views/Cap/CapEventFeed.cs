namespace PermaLocke.App.Views;

/// <summary>
/// Turns the party the panel shows, reading after reading, into what happened to each Pokémon (2026-10-11): a list of <see cref="CapEvent"/> with the
/// moment each was shown, that stays in the data for a few seconds. It is the only place that decides what is a hit, a heal, a revive, a level, a cure;
/// the animation never compares two readings itself. Pure and with no clock of its own: it is told the moment.
/// </summary>
/// <remarks>
/// <para>
/// What it will not call an event, because it did not happen to the Pokémon: the first time it sees one (a panel that opens does not flash six
/// level-ups), anything while the maximum life moved (a level, an evolution, a swap: the game adds the same to the life, and that is not a heal), a
/// status that appears or goes in the reading where a battle starts or ends (the status is only known out of battle, so what changes there is what is
/// known), and an egg. A Pokémon is followed by its PID; one without a PID (the rehearsal) by its place.
/// </para>
/// <para>
/// A hit that takes the life to zero is a hit and a fall; a heal of a Pokémon that was at zero is a revive and not a heal. Several events on one
/// Pokémon, or on several, come out in the order they were seen and keep that order.
/// </para>
/// </remarks>
public sealed class CapEventFeed
{
    /// <summary>How many seconds an event stays in the data after the moment it was shown.</summary>
    public const double LifeSeconds = 6;

    private sealed record Seen(int Level, int Hp, int MaxHp, bool Egg, CapStatus Status, string? Item, IReadOnlyList<string?>? Moves, bool InBattle);

    private readonly Dictionary<(uint Pid, int Slot), Seen> _seen = [];
    private readonly List<CapEvent> _events = [];
    private long _next = 1;

    /// <summary>
    /// Whether life reaching zero is a fall. The lab and the tests say yes; the app says no (singleplayer, 2026-10-13) and calls <see cref="Fall"/> when
    /// the run records the death: before the first Poké Ball a Pokémon at zero is only knocked out, and the record waits for the game to show the faint.
    /// </summary>
    public bool FallsFromLife { get; set; } = true;

    /// <summary>A death the run has recorded, as a fall of the Pokémon with that PID. Returns the events of the last few seconds.</summary>
    public IReadOnlyList<CapEvent> Fall(uint pid, double at, int death = -1)
    {
        // The death drawn for it, when the app chose one (2026-10-15), goes as the event's amount, plus one: 0 is none.
        _events.Add(new CapEvent(_next++, CapEventKind.Fell, pid, -1, at, death + 1, CapStatus.None, 0));
        return [.. _events];
    }

    /// <summary>Forgets what it has seen: the next reading of each Pokémon is taken as it is, as when the reading started again (the game came back).</summary>
    public void Rebaseline() => _seen.Clear();

    private static (uint, int) KeyOf(CapPlaqueMember m, int slot) => m.Pid != 0 ? (m.Pid, 0) : (0u, slot);

    /// <summary>
    /// Takes the party as it is now at a moment and returns the data with the events of the last few seconds in it. <paramref name="at"/> is when the
    /// news came (the reading, the bar), which may be a little before now.
    /// </summary>
    public CapPlaqueData Observe(CapPlaqueData data, double at, double now)
    {
        var present = new HashSet<(uint, int)>();
        for (var slot = 0; slot < data.Party.Count; slot++)
        {
            var member = data.Party[slot];
            var key = KeyOf(member, slot);
            present.Add(key);
            var state = new Seen(member.Level, member.Hp, member.MaxHp, member.Egg, member.Status, member.Item,
                member.Moves?.Select(move => move.Name).ToList(), data.InBattle);

            if (_seen.TryGetValue(key, out var before)) Compare(before, state, member, slot, at);
            _seen[key] = state;
        }

        // A Pokémon that left the party is forgotten, so that it comes back as a first sight and not as a change.
        foreach (var gone in _seen.Keys.Where(key => !present.Contains(key)).ToList()) _seen.Remove(gone);

        _events.RemoveAll(e => now - e.At > LifeSeconds);
        return data with { Events = [.. _events] };
    }

    private void Add(CapEventKind kind, CapPlaqueMember member, int slot, double at, int amount = 0, CapStatus status = CapStatus.None, int extra = 0) =>
        _events.Add(new CapEvent(_next++, kind, member.Pid, slot, at, amount, status, extra));

    private void Compare(Seen before, Seen now, CapPlaqueMember member, int slot, double at)
    {
        if (before.Egg && !now.Egg) Add(CapEventKind.Hatch, member, slot, at);
        if (before.Egg || now.Egg) return;

        if (now.Level > before.Level) Add(CapEventKind.Level, member, slot, at);

        // Life, only while the maximum stood still: when it moves, the life moves with it and nothing was hit or healed.
        if (now.MaxHp == before.MaxHp && now.Hp != before.Hp)
        {
            if (now.Hp < before.Hp)
            {
                Add(CapEventKind.Hit, member, slot, at, before.Hp - now.Hp);
                if (now.Hp <= 0 && FallsFromLife) Add(CapEventKind.Fell, member, slot, at);
            }
            else if (before.Hp <= 0) Add(CapEventKind.Revive, member, slot, at, now.Hp - before.Hp);
            else Add(CapEventKind.Heal, member, slot, at, now.Hp - before.Hp);
        }

        // The status is known only out of battle, so only between two readings out of it does a change mean something.
        if (!before.InBattle && !now.InBattle && before.Status != now.Status)
        {
            if (before.Status != CapStatus.None) Add(CapEventKind.StatusOut, member, slot, at, 0, before.Status);
            if (now.Status != CapStatus.None) Add(CapEventKind.StatusIn, member, slot, at, 0, now.Status);
        }

        if (before.Item != now.Item) Add(CapEventKind.ItemChanged, member, slot, at, 0, CapStatus.None, before.Item is null ? 0 : now.Item is null ? 1 : 2);

        // A move that is in the set now and was not before. Only when both sets are known; the PP running down changes no name.
        if (before.Moves is { Count: > 0 } oldMoves && now.Moves is { Count: > 0 } newMoves)
        {
            for (var k = 0; k < newMoves.Count; k++)
            {
                if (newMoves[k] is { Length: > 0 } name && !oldMoves.Contains(name))
                {
                    Add(CapEventKind.MoveLearned, member, slot, at, 0, CapStatus.None, member.Moves![k].Type);
                    break;
                }
            }
        }
    }
}
