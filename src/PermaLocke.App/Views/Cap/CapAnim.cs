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
/// What the panel remembers between frames so that what changes is seen changing (2026-10-10): the HP that drop and rise, the level
/// that goes up, the one that falls, the egg that hatches, the cap that changes. Pure: it is told the data and the time, never reads a
/// clock, and everything it answers is a function of the two, so a frame is the same wherever and whenever it is asked for.
/// </summary>
/// <remarks>
/// <para>
/// It follows each Pokémon by PID (the rule of the whole app, §96): a swap of two in the party moves their histories with them, and
/// a Pokémon without a PID (the rehearsal) is followed by its place. The first time it sees a party it takes it as it is and
/// announces nothing: a panel that opens does not flash six level-ups.
/// </para>
/// <para>
/// To see it again from a moment (the lab, the tests) build a new one and tell it the steps up to there: it has no other state.
/// </para>
/// </remarks>
public sealed class CapAnim
{
    /// <summary>How long a change dissolves from the old drawing into the new one, in seconds.</summary>
    public const double DissolveSeconds = 0.45;

    private const double Never = -1e9;
    private const double MourningSeconds = 20;

    private sealed class Track
    {
        public uint Pid;
        public int Level;
        public int Hp;
        public int MaxHp;
        public bool Egg;
        public CapStatus Status;
        public string Name = string.Empty;
        public string? Item;
        public int Types;

        public double HpAt = Never;
        public double HpFrom;
        public double HpTo;
        public double TrailFrom;
        public double LevelAt = Never;
        public double HealAt = Never;
        public double HitAt = Never;
        public double FellAt = Never;
        public double HatchAt = Never;
        public double ChangedAt = Never;
        public double StatusAt = Never;
    }

    private Track?[] _tracks = new Track?[CapComposer.PartySlots];
    private bool _seen;

    private int _cap;
    private int _previousCap;
    private double _capAt = Never;
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
        _tracks = new Track?[CapComposer.PartySlots];
        _seen = false;
        _cap = 0;
        _capAt = _headerAt = _highestAt = _lastFallAt = Never;
        _mood = _previousMood = CapMood.Calm;
        _moodAt = Never;
    }

    /// <summary>Tells it what the panel says now, at a moment: it notices what changed since the last time.</summary>
    public void Update(CapPlaqueData data, double now)
    {
        var old = _tracks;
        var next = new Track?[CapComposer.PartySlots];

        for (var i = 0; i < data.Party.Count && i < next.Length; i++)
        {
            var m = data.Party[i];
            var before = Find(old, m.Pid, i);
            var track = before ?? new Track { Pid = m.Pid };
            if (before is not null && _seen) Notice(track, m, now);
            else Take(track, m);

            Remember(track, m);
            next[i] = track;
        }

        _tracks = next;

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
        _danger = data.InBattle && alive.Any(m => m.MaxHp > 0 && m.Hp * 4 <= m.MaxHp);
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

    private void Notice(Track track, CapPlaqueMember m, double now)
    {
        // What the bar shows right now is where a new move starts from, so changes in a row do not jump.
        var shown = ShownHp(track, now);

        if (m.Level > track.Level && !m.Egg) track.LevelAt = now;
        if (track.Egg && !m.Egg) track.HatchAt = now;

        if (m.Hp != track.Hp && !m.Egg && !track.Egg)
        {
            track.HpFrom = shown;
            track.HpTo = m.Hp;
            track.HpAt = now;
            if (m.Hp < track.Hp)
            {
                track.HitAt = now;
                track.TrailFrom = Math.Max(track.TrailFrom, shown);
                if (m.Hp <= 0 && track.Hp > 0)
                {
                    track.FellAt = now;
                    _lastFallAt = now;
                }
            }
            else if (m.Hp > track.Hp && m.MaxHp >= track.MaxHp)
            {
                track.HealAt = now;
            }
        }

        if (m.Status != track.Status && m.Status != CapStatus.None) track.StatusAt = now;

        if (Visible(track, m)) track.ChangedAt = now;
    }

    /// <summary>Whether something the niche shows in its still part changed: a text, a chip, the icon's state. Experience is a bar, so it is not.</summary>
    private static bool Visible(Track track, CapPlaqueMember m) =>
        track.Level != m.Level || track.Hp != m.Hp || track.MaxHp != m.MaxHp || track.Egg != m.Egg || track.Status != m.Status
        || track.Name != m.Name || track.Item != m.Item || track.Types != (m.Types?.Count ?? 0);

    private static void Remember(Track track, CapPlaqueMember m)
    {
        track.Pid = m.Pid;
        track.Level = m.Level;
        track.Hp = m.Hp;
        track.MaxHp = m.MaxHp;
        track.Egg = m.Egg;
        track.Status = m.Status;
        track.Name = m.Name;
        track.Item = m.Item;
        track.Types = m.Types?.Count ?? 0;
    }

    // ---------------------------------------------------------------- what a niche shows

    private Track? At(int index) => index >= 0 && index < _tracks.Length ? _tracks[index] : null;

    private static double Ease(double k) => k * k * (3 - (2 * Math.Clamp(k, 0, 1)));

    private static double ShownHp(Track track, double now)
    {
        var dur = track.HpTo < track.HpFrom ? 0.30 : 0.55;
        var k = Math.Clamp((now - track.HpAt) / dur, 0, 1);
        return track.HpFrom + ((track.HpTo - track.HpFrom) * Ease(k));
    }

    /// <summary>The HP bar to show for a niche at a moment, 0 to 1: the real one moving to its new value. Null when it knows nothing of the niche.</summary>
    public double? HpFraction(int index, double t)
    {
        if (At(index) is not { MaxHp: > 0 } track) return null;
        return Math.Clamp(ShownHp(track, t) / track.MaxHp, 0, 1);
    }

    /// <summary>
    /// The lighter stretch that stays behind a drop and then catches up (the trail), as the fraction the bar had; null when there is none.
    /// </summary>
    public double? TrailFraction(int index, double t)
    {
        if (At(index) is not { MaxHp: > 0 } track || track.HitAt <= Never) return null;

        var since = t - track.HitAt;
        if (since < 0 || since > 1.2) return null;

        var from = track.TrailFrom / track.MaxHp;
        var to = ShownHp(track, t) / track.MaxHp;
        if (since < 0.35) return Math.Max(to, Math.Clamp(from, 0, 1));
        var k = Math.Clamp((since - 0.35) / 0.7, 0, 1);
        return Math.Max(to, Math.Clamp(from + ((to - from) * k), 0, 1));
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

    /// <summary>How long ago a Pokémon was hit, in seconds, or null.</summary>
    public double? SinceHit(int index, double t) => At(index) is { } track && track.HitAt > Never && t >= track.HitAt ? t - track.HitAt : null;

    /// <summary>How long ago a Pokémon got a status it did not have, or null.</summary>
    public double? SinceStatus(int index, double t) => At(index) is { } track && track.StatusAt > Never && t >= track.StatusAt ? t - track.StatusAt : null;

    /// <summary>How far a niche's change has dissolved from the old drawing into the new one: 0 just changed, 1 done (or nothing changed).</summary>
    public double Dissolve(int index, double t) =>
        At(index) is { } track && t >= track.ChangedAt ? Math.Clamp((t - track.ChangedAt) / DissolveSeconds, 0, 1) : 1;

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

    /// <summary>The fuse is closing and opening because the cap changed: 0 when not, 1 at the moment it is out.</summary>
    public double CapPulse(double t) => Pulse(_capAt, t, 1.6);

    /// <summary>The mood now, and the one before it with how far the change has gone (0 just changed, 1 done).</summary>
    public (CapMood Now, CapMood Before, double Blend) Mood(double t)
    {
        // Mourning is not kept as a mood but worked out here, so it ends by itself twenty seconds after the fall without a new update.
        var mourning = !_danger && _lastFallAt > Never && t >= _lastFallAt && t - _lastFallAt < MourningSeconds;
        if (mourning) return (CapMood.Mourning, _mood, Math.Clamp((t - Math.Max(_moodAt, _lastFallAt)) / 0.8, 0, 1));

        var ended = _lastFallAt + MourningSeconds;
        if (!_danger && _lastFallAt > Never && t >= ended && t - ended < 0.8) return (_mood, CapMood.Mourning, (t - ended) / 0.8);

        return (_mood, _previousMood, Math.Clamp((t - _moodAt) / 0.8, 0, 1));
    }

    /// <summary>Whether something is moving that needs frames more often than the ambient flicker: a pulse, a trail, a change dissolving, or danger.</summary>
    public bool Busy(double t)
    {
        if (_danger || t - _capAt < 1.7 || t - _highestAt < 1) return true;
        if (t - _headerAt < DissolveSeconds) return true;

        foreach (var track in _tracks)
        {
            if (track is null) continue;
            if (t - track.HitAt < 1.3 || t - track.HealAt < 1 || t - track.LevelAt < 1.2 || t - track.HatchAt < 1.3
                || t - track.ChangedAt < DissolveSeconds || t - track.HpAt < 0.7 || t - track.FellAt < 2.2) return true;
        }

        return false;
    }
}
