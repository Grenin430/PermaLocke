using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>How the panel feels, which tints its case and the light on it.</summary>
public enum CapMood
{
    /// <summary>Walking about: a warm, quiet light.</summary>
    Calm,

    /// <summary>A battle is going on: a cool light along the rim.</summary>
    Battle,

    /// <summary>In battle and someone is at a quarter of their HP or less: the rim pulses red.</summary>
    Danger,

    /// <summary>One has just fallen: a grey-violet veil and a lower flame, for twenty seconds.</summary>
    Mourning,

    /// <summary>The party has reached the cap: a gold sheen runs along the top of the case.</summary>
    Cap
}

/// <summary>
/// What the panel remembers between frames so that what happens is seen happening (2026-10-10, events 2026-10-11): the HP bars that go down and up,
/// and for each thing that happened to a Pokémon (a hit, a heal, a revive, a level, a fall, a hatching, a status in or out, an item, a move) the moment
/// it was shown. What happened comes as <see cref="CapEvent"/> in the data (<see cref="CapEventFeed"/> decides it, once); when the data carry none
/// (the lab, the tests) it works them out itself with a feed of its own. Pure: it is told the data and the time, never reads a clock, and everything it
/// answers is a function of the two, so a frame is the same wherever and whenever it is asked for.
/// </summary>
/// <remarks>
/// <para>
/// It follows each Pokémon by PID (the rule of the whole app, §96): a swap of two in the party moves their histories with them, and a Pokémon without
/// a PID (the rehearsal) is followed by its place. The first time it sees a party it takes it as it is and announces nothing: a panel that opens does
/// not flash six level-ups.
/// </para>
/// <para>
/// To see it again from a moment (the lab, the tests) build a new one and tell it the steps up to there: it has no other state.
/// </para>
/// </remarks>
public sealed class CapAnim
{
    /// <summary>How long a change dissolves from the old drawing into the new one, in seconds.</summary>
    public const double DissolveSeconds = 1.0;

    private const double Never = -1e9;
    private const double MourningSeconds = 20;

    /// <summary>The share of life at or below which the one on the field puts the sea in danger.</summary>
    public const double DangerShare = 0.30;

    /// <summary>How many hits on one Pokémon are kept to write their numbers one above the other.</summary>
    public const int HitMemory = 3;

    private sealed class Track
    {
        public uint Pid;
        public int Level;
        public int Hp;
        public int MaxHp;
        public bool Egg;
        public CapStatus Status;

        public double HpAt = Never;
        public double HpFrom;
        public double HpTo;
        public double TrailFrom;
        public double LevelAt = Never;
        public double HealAt = Never;
        public double HitAt = Never;
        public double FellAt = Never;
        public int GravesAtFall;
        public double HatchAt = Never;
        public double StatusAt = Never;

        public int HealAmount;
        public double ReviveAt = Never;
        public double CureAt = Never;
        public CapStatus CuredStatus;
        public double ItemAt = Never;
        public int ItemKind;
        public double MoveAt = Never;
        public int MoveType = -1;

        /// <summary>The latest hits, the newest first: when and how much.</summary>
        public readonly (double At, int Amount)[] Hits = new (double, int)[HitMemory];

        public int HitCount;
    }

    private Track?[] _tracks = new Track?[CapWorld.Creatures];
    private bool _seen;
    private readonly CapEventFeed _feed = new();
    private long _lastSeq;

    private int _cap;
    private int _previousCap;
    private double _capAt = Never;
    private int _graves;
    private double _headerAt = Never;
    private int _highest;
    private int _previousHighest;
    private double _highestAt = Never;
    private string _trial = string.Empty;
    private bool _inBattle;
    private bool _danger;
    private double _lastFallAt = Never;

    private CapMood _mood = CapMood.Calm;
    private CapMood _previousMood = CapMood.Calm;
    private double _moodAt = Never;

    /// <summary>Forgets everything, as if the panel had just opened.</summary>
    public void Reset()
    {
        _tracks = new Track?[CapWorld.Creatures];
        _seen = false;
        _cap = 0;
        _capAt = _headerAt = _highestAt = _lastFallAt = Never;
        _mood = _previousMood = CapMood.Calm;
        _moodAt = Never;
        _phaseBase = 0;
        _tempoFrom = _tempoTo = 1;
        _tempoAt = 0;
        _feed.Rebaseline();
        _lastSeq = 0;
    }

    /// <summary>Tells it what the panel says now, at a moment: it notices what changed since the last time.</summary>
    public void Update(CapPlaqueData data, double now)
    {
        _graves = data.Graves?.Count ?? 0;

        // What happened: from the data when the app sent it, from its own feed when it did not.
        var events = data.Events ?? _feed.Observe(data, now, now).Events!;

        var old = _tracks;
        var next = new Track?[CapWorld.Creatures];

        for (var i = 0; i < data.Party.Count && i < next.Length; i++)
        {
            var m = data.Party[i];
            var before = Find(old, m.Pid, i);
            var track = before ?? new Track { Pid = m.Pid };
            if (before is not null && _seen) NoticeLife(track, m, now);
            else Take(track, m);

            Remember(track, m);
            next[i] = track;
        }

        _tracks = next;

        // The first time it sees the party it takes it as it is: events from before it opened are not shown.
        if (_seen) Apply(events);
        else if (events.Count > 0) _lastSeq = events.Max(e => e.Seq);

        var wasSeen = _seen;
        _seen = true;

        var highest = data.Highest ?? 0;
        if (wasSeen && data.Cap != _cap && _cap > 0)
        {
            _previousCap = _cap;
            _capAt = now;
            _headerAt = now;
        }

        if (wasSeen && data.Trial != _trial) _headerAt = now;
        if (wasSeen && highest != _highest)
        {
            _previousHighest = _highest;
            _highestAt = now;
        }

        _cap = data.Cap;
        _highest = highest;
        _trial = data.Trial;
        _inBattle = data.InBattle;

        var alive = data.Party.Where(m => !m.Egg && !m.Fallen).ToList();
        // The sea is in danger when the Pokémon on the field is: its bar on the screen says how much life it has. When the bar cannot be seen the sea is not
        // in danger: the lowest of the party stood in for it and kept the sea red after a swap to a healthy one (seen in the game, 2026-10-11).
        _danger = data.InBattle && data.ActiveShare is { } active && active <= DangerShare;
        var capped = alive.Count > 0 && highest >= data.Cap && data.Cap > 0;

        var mood = _danger ? CapMood.Danger
            : data.InBattle ? CapMood.Battle
            : capped ? CapMood.Cap
            : CapMood.Calm;
        if (mood != _mood)
        {
            _previousMood = _mood;
            _mood = mood;
            _moodAt = now;
        }

        // The clock of the sea changes speed with the mood, smoothly: the phase is the integral of the tempo, so nothing jumps.
        var effective = mood;
        var target = CapSeaPalette.Of(effective).Tempo;
        if (Math.Abs(target - _tempoTo) > 1e-6)
        {
            _phaseBase = Phase(now);
            _tempoFrom = TempoAt(now);
            _tempoTo = target;
            _tempoAt = now;
        }
    }

    private double _phaseBase;
    private double _tempoFrom = 1;
    private double _tempoTo = 1;
    private double _tempoAt;
    private const double TempoSeconds = 1.2;

    private double TempoAt(double t)
    {
        var k = Math.Clamp((t - _tempoAt) / TempoSeconds, 0, 1);
        return _tempoFrom + ((_tempoTo - _tempoFrom) * k);
    }

    /// <summary>The clock everything that drifts goes by (the snow, the waves, the creatures' sway): seconds, but faster in a battle, slower in mourning.</summary>
    public double Phase(double t)
    {
        var dt = Math.Max(0, t - _tempoAt);
        if (dt < TempoSeconds) return _phaseBase + (_tempoFrom * dt) + ((_tempoTo - _tempoFrom) * dt * dt / (2 * TempoSeconds));
        return _phaseBase + (_tempoFrom * TempoSeconds) + ((_tempoTo - _tempoFrom) * TempoSeconds / 2) + (_tempoTo * (dt - TempoSeconds));
    }

    private static Track? Find(Track?[] tracks, uint pid, int index)
    {
        if (pid != 0)
        {
            return tracks.FirstOrDefault(t => t is not null && t.Pid == pid);
        }

        return index < tracks.Length && tracks[index] is { Pid: 0 } at ? at : null;
    }

    private static void Take(Track track, CapPlaqueMember m)
    {
        track.HpFrom = track.HpTo = m.Hp;
        track.TrailFrom = m.Hp;
    }

    /// <summary>The bar follows the life: from where it is shown now to the new value, in a time that grows with how much it moves.</summary>
    private static void NoticeLife(Track track, CapPlaqueMember m, double now)
    {
        if (m.Hp == track.Hp || m.Egg || track.Egg) return;

        // What the bar shows right now is where a new move starts from, so changes in a row do not jump.
        var shown = ShownHp(track, now);
        track.HpFrom = shown;
        track.HpTo = m.Hp;
        track.HpAt = now;
        if (m.Hp < track.Hp) track.TrailFrom = Math.Max(track.TrailFrom, shown);
    }

    /// <summary>Puts what happened on the tracks of the Pokémon it happened to, each with the moment it is shown.</summary>
    private void Apply(IReadOnlyList<CapEvent> events)
    {
        foreach (var e in events.Where(e => e.Seq > _lastSeq).OrderBy(e => e.Seq))
        {
            _lastSeq = e.Seq;
            var track = e.Pid != 0 ? _tracks.FirstOrDefault(t => t is not null && t.Pid == e.Pid) : e.Slot >= 0 && e.Slot < _tracks.Length ? _tracks[e.Slot] : null;
            if (track is null) continue;

            switch (e.Kind)
            {
                case CapEventKind.Hit:
                    track.HitAt = e.At;
                    for (var k = HitMemory - 1; k > 0; k--) track.Hits[k] = track.Hits[k - 1];
                    track.Hits[0] = (e.At, e.Amount);
                    track.HitCount = Math.Min(HitMemory, track.HitCount + 1);
                    break;
                case CapEventKind.Fell:
                    track.FellAt = e.At;
                    track.GravesAtFall = _graves;
                    _lastFallAt = e.At;
                    break;
                case CapEventKind.Heal:
                    track.HealAt = e.At;
                    track.HealAmount = e.Amount;
                    break;
                case CapEventKind.Revive:
                    track.HealAt = e.At;
                    track.ReviveAt = e.At;
                    track.HealAmount = e.Amount;
                    break;
                case CapEventKind.Level:
                    track.LevelAt = e.At;
                    break;
                case CapEventKind.Hatch:
                    track.HatchAt = e.At;
                    break;
                case CapEventKind.StatusIn:
                    track.StatusAt = e.At;
                    break;
                case CapEventKind.StatusOut:
                    track.CureAt = e.At;
                    track.CuredStatus = e.Status;
                    break;
                case CapEventKind.ItemChanged:
                    track.ItemAt = e.At;
                    track.ItemKind = e.Extra;
                    break;
                case CapEventKind.MoveLearned:
                    track.MoveAt = e.At;
                    track.MoveType = e.Extra;
                    break;
            }
        }
    }

    private static void Remember(Track track, CapPlaqueMember m)
    {
        track.Pid = m.Pid;
        track.Level = m.Level;
        track.Hp = m.Hp;
        track.MaxHp = m.MaxHp;
        track.Egg = m.Egg;
        track.Status = m.Status;
    }

    // ---------------------------------------------------------------- what a niche shows

    private Track? At(int index) => index >= 0 && index < _tracks.Length ? _tracks[index] : null;

    private static double Ease(double k) => k * k * (3 - (2 * Math.Clamp(k, 0, 1)));

    /// <summary>How long the bar takes in its current move: the table's (<see cref="HpSyncTable.BarSeconds"/>), which is the game's own once it is measured.</summary>
    private static double BarSeconds(Track track)
    {
        var share = track.MaxHp > 0 ? Math.Abs(track.HpTo - track.HpFrom) / track.MaxHp : 0;
        return HpSyncTable.BarSeconds(share, track.HpTo < track.HpFrom);
    }

    private static double ShownHp(Track track, double now)
    {
        var k = Math.Clamp((now - track.HpAt) / BarSeconds(track), 0, 1);
        return track.HpFrom + ((track.HpTo - track.HpFrom) * Ease(k));
    }

    /// <summary>How long the bar of a niche takes in its current move, in seconds.</summary>
    public double BarSeconds(int index) => At(index) is { } track ? BarSeconds(track) : 0;

    /// <summary>The HP bar to show for a niche at a moment, 0 to 1: the real one moving to its new value. Null when it knows nothing of the niche.</summary>
    public double? HpFraction(int index, double t)
    {
        if (At(index) is not { MaxHp: > 0 } track) return null;
        return Math.Clamp(ShownHp(track, t) / track.MaxHp, 0, 1);
    }

    /// <summary>
    /// The lighter stretch that stays behind a drop and then catches up (the ghost), as the fraction the bar had: it holds where the bar was until the real
    /// one has got down to its place, and then chases it. Null when there is none.
    /// </summary>
    public double? TrailFraction(int index, double t)
    {
        if (At(index) is not { MaxHp: > 0 } track || track.HitAt <= Never) return null;

        var since = t - track.HitAt;
        var duration = BarSeconds(track);
        if (since < 0 || since > duration + 1.2) return null;

        var from = track.TrailFrom / track.MaxHp;
        var to = ShownHp(track, t) / track.MaxHp;
        if (since < duration + 0.1) return Math.Max(to, Math.Clamp(from, 0, 1));
        var chase = Math.Clamp((since - duration - 0.1) / 1.0, 0, 1);
        return Math.Max(to, Math.Clamp(from + ((to - from) * Ease(chase)), 0, 1));
    }

    private static double Pulse(double at, double t, double length) => t < at || at <= Never ? 0 : Math.Clamp(1 - ((t - at) / length), 0, 1);

    /// <summary>A red wash after a hit: 1 at the moment, 0 after a third of a second.</summary>
    public double HitPulse(int index, double t) => At(index) is { } track ? Pulse(track.HitAt, t, 0.35) : 0;

    /// <summary>A green pulse after a heal: 1 at the moment, 0 after 0.9 s.</summary>
    public double HealPulse(int index, double t) => At(index) is { } track ? Pulse(track.HealAt, t, 0.9) : 0;

    /// <summary>A gold flash on the level after a level-up: 1 at the moment, 0 after 1.1 s.</summary>
    public double LevelPulse(int index, double t) => At(index) is { } track ? Pulse(track.LevelAt, t, 1.1) : 0;

    /// <summary>A white flash when an egg hatches: 1 at the moment, 0 after 1.2 s.</summary>
    public double HatchPulse(int index, double t) => At(index) is { } track ? Pulse(track.HatchAt, t, 1.2) : 0;

    /// <summary>How long ago a Pokémon fell, in seconds, or null if it has not in the time the panel has been open.</summary>
    public double? SinceFall(int index, double t) => At(index) is { } track && track.FellAt > Never && t >= track.FellAt ? t - track.FellAt : null;

    /// <summary>How many graves the run had when this Pokémon fell: the fossil it becomes is the next one, at that place.</summary>
    public int GravesAtFall(int index) => At(index)?.GravesAtFall ?? 0;

    /// <summary>How long ago the latest fall was, in seconds, or null when none has happened in the time the panel has been open.</summary>
    public double? LastFallSince(double t) => _lastFallAt > Never && t >= _lastFallAt ? t - _lastFallAt : null;

    /// <summary>How long ago a Pokémon was hit, in seconds, or null.</summary>
    public double? SinceHit(int index, double t) => At(index) is { } track && track.HitAt > Never && t >= track.HitAt ? t - track.HitAt : null;

    /// <summary>How long ago a Pokémon got a status it did not have, or null.</summary>
    public double? SinceStatus(int index, double t) => At(index) is { } track && track.StatusAt > Never && t >= track.StatusAt ? t - track.StatusAt : null;

    /// <summary>The latest hits on a Pokémon, the newest first, with how long ago each was and how much it took: so that the numbers stack and none hides another.</summary>
    public int Hits(int index, double t, Span<(double Since, int Amount)> into)
    {
        if (At(index) is not { } track) return 0;
        var n = 0;
        for (var k = 0; k < track.HitCount && n < into.Length; k++)
        {
            if (t < track.Hits[k].At) continue;
            into[n++] = (t - track.Hits[k].At, track.Hits[k].Amount);
        }

        return n;
    }

    /// <summary>The size of the last hit as a share of the maximum life, 0 to 1 (0 when unknown).</summary>
    public double HitShare(int index) => At(index) is { MaxHp: > 0, HitCount: > 0 } track ? Math.Clamp(track.Hits[0].Amount / (double)track.MaxHp, 0, 1) : 0;

    public double HealShare(int index) => At(index) is { MaxHp: > 0 } track ? Math.Clamp(track.HealAmount / (double)track.MaxHp, 0, 1) : 0;

    public int HealAmount(int index) => At(index)?.HealAmount ?? 0;

    /// <summary>How long ago a Pokémon got up from zero life, or null.</summary>
    public double? SinceRevive(int index, double t) => At(index) is { } track && track.ReviveAt > Never && t >= track.ReviveAt ? t - track.ReviveAt : null;

    /// <summary>How long ago a Pokémon was healed (revives included), or null.</summary>
    public double? SinceHeal(int index, double t) => At(index) is { } track && track.HealAt > Never && t >= track.HealAt ? t - track.HealAt : null;

    /// <summary>How long ago a status was cured, and which one, or null.</summary>
    public (double Since, CapStatus Status)? SinceCure(int index, double t) =>
        At(index) is { } track && track.CureAt > Never && t >= track.CureAt ? (t - track.CureAt, track.CuredStatus) : null;

    /// <summary>How long ago the held item changed and how (0 got one, 1 lost it, 2 swapped), or null.</summary>
    public (double Since, int Kind)? SinceItem(int index, double t) =>
        At(index) is { } track && track.ItemAt > Never && t >= track.ItemAt ? (t - track.ItemAt, track.ItemKind) : null;

    /// <summary>How long ago a move was learned and its type, or null.</summary>
    public (double Since, int Type)? SinceMove(int index, double t) =>
        At(index) is { } track && track.MoveAt > Never && t >= track.MoveAt ? (t - track.MoveAt, track.MoveType) : null;

    /// <summary>The same for the head of the panel (the cap, the trial, the fuse).</summary>
    public double HeaderDissolve(double t) => t >= _headerAt ? Math.Clamp((t - _headerAt) / DissolveSeconds, 0, 1) : 1;

    // ---------------------------------------------------------------- the fuse and the light

    /// <summary>
    /// How much of the fuse is burnt at a moment, 0 to 1. After a change of cap it burns back to nothing and lights again towards the new
    /// one; after a change of the party's level it moves to the new place.
    /// </summary>
    public double FuseShare(double t, int highest, int cap)
    {
        var target = Math.Clamp(highest / (double)Math.Max(1, cap), 0, 1);
        if (_capAt > Never && t >= _capAt && t - _capAt < 1.6)
        {
            var before = Math.Clamp(highest / (double)Math.Max(1, _previousCap), 0, 1);
            var k = t - _capAt;
            return k < 0.6 ? before * (1 - Ease(k / 0.6)) : target * Ease((k - 0.6) / 1.0);
        }

        if (_highestAt > Never && t >= _highestAt && t - _highestAt < 0.9)
        {
            var before = Math.Clamp(_previousHighest / (double)Math.Max(1, cap), 0, 1);
            return before + ((target - before) * Ease((t - _highestAt) / 0.9));
        }

        return target;
    }

    /// <summary>The cap rose (a trial passed) while the panel was open: how long ago, from which cap to which; null otherwise (2026-10-15).</summary>
    public (double Since, int From, int To)? CapRise(double t) => _capAt > Never && t >= _capAt && _cap > _previousCap ? (t - _capAt, _previousCap, _cap) : null;

    /// <summary>How long ago the cap changed, in seconds, or null when it has not in the time the panel has been open.</summary>
    public double? SinceCap(double t) => _capAt > Never && t >= _capAt ? t - _capAt : null;

    /// <summary>The fuse is closing and opening because the cap changed: 0 when not, 1 at the moment it is out.</summary>
    public double CapPulse(double t) => Pulse(_capAt, t, 1.6);

    /// <summary>The mood now, and the one before it with how far the change has gone (0 just changed, 1 done).</summary>
    public (CapMood Now, CapMood Before, double Blend) Mood(double t)
    {
        // No mourning any more (2026-10-15, asked by the player): a fall no longer turns the sea grey for twenty seconds; its death is the moment.
        return (_mood, _previousMood, Math.Clamp((t - _moodAt) / 0.8, 0, 1));
    }

    /// <summary>Whether something is moving that needs frames at the pace of the screen: a pulse, a trail, a number, a change dissolving, or danger.</summary>
    public bool Busy(double t)
    {
        if (_danger || t - _capAt < 3.4 || t - _highestAt < 1) return true;
        if (t - _headerAt < DissolveSeconds) return true;

        foreach (var track in _tracks)
        {
            if (track is null) continue;
            if (t - track.HitAt < 3.4 || t - track.HealAt < 2.8 || t - track.LevelAt < 1.2 || t - track.HatchAt < 1.3 || t - track.StatusAt < 1.4
                || t - track.CureAt < 2.2 || t - track.ItemAt < 1.6 || t - track.MoveAt < 1.6 || t - track.HpAt < BarSeconds(track) + 1.4
                || t - track.FellAt < 8) return true;
        }

        return false;
    }
}
