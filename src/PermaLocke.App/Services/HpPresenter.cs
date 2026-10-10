namespace PermaLocke.App.Services;

/// <summary>When the panel shows a change of life (CONFIGURACIÓN, «Vida del panel»).</summary>
public enum HpSyncMode
{
    /// <summary>With the game: the change is shown when the game's own bar starts to move, or after a delay worked out from the damage when the bar cannot be seen.</summary>
    Synchronized = 0,

    /// <summary>As soon as it is read: before the game shows it, so it gives the damage away.</summary>
    Instant = 1,

    /// <summary>Three seconds after it is read, as the panel always did.</summary>
    Delayed = 2
}

/// <summary>
/// Every number the synchronisation with the game rests on and that has not been measured on the real game yet, in one place: change them here and
/// nowhere else. They are PROVISIONAL. The procedure that gives the real ones is in docs/MEDICIONES-CAP.md (§1, the bar of the game), and what it
/// writes is one line for each constant.
/// </summary>
public static class HpSyncTable
{
    /// <summary>«Retrasado»: how long after reading a change it is shown. The 3 s the panel used before, kept as it was.</summary>
    public const double DelayedSeconds = 3.0;

    /// <summary>
    /// PROVISIONAL. When the bar cannot be seen (the window is covered, the layout is not computed, a double battle): seconds from the change in memory
    /// to the game's bar starting to move. Memory changes when the move is chosen or when «X used Y» appears, and the bar moves after the attack's
    /// animation; 1.6 s plus 0.8 s for each whole bar of damage is a guess between the two measurements of §114 ter, not a measurement.
    /// </summary>
    public const double CalculatedBaseSeconds = 1.6, CalculatedPerShareSeconds = 0.8;

    /// <summary>The longest the panel holds a change back waiting for the bar of the game: after this it is shown whatever the bar did.</summary>
    public const double GiveUpSeconds = 6.0;

    /// <summary>PROVISIONAL. Seconds the game's bar takes to go down, or up, by a share of the maximum HP: what the panel's own bar takes too, so the two end together.</summary>
    public static double BarSeconds(double share, bool drop)
    {
        share = Math.Clamp(share, 0, 1);
        return drop ? 0.45 + (1.5 * share) : 0.7 + (1.0 * share);
    }

    /// <summary>How long to wait for a change of that size when the bar cannot say.</summary>
    public static double CalculatedDelay(double share) => CalculatedBaseSeconds + (CalculatedPerShareSeconds * Math.Clamp(share, 0, 1));
}

/// <summary>
/// What life to show for each Pokémon, and when (2026-10-11): memory knows a change the instant it happens, the game shows it much later (a message, an
/// attack animation, a bar that drains), and what the panel shows has to follow the game and not memory, or it gives the damage away. Pure: told what was
/// read and when, and when the game's bar moved, it answers what to show at a moment. Time is in seconds on any one clock.
/// </summary>
/// <remarks>
/// Only a change inside a battle is held back: out of a battle the player is looking at the party menu and the game shows it as it happens. A change of
/// the maximum (a level, a swap) is not held either. In «Sincronizado» each time the game's bar starts to move it releases the oldest change that was
/// waiting, one for each bar, which is how the game shows them.
/// </remarks>
public sealed class HpPresenter
{
    private sealed class Held(int hp, double readAt, double due)
    {
        public int Hp { get; } = hp;

        public double ReadAt { get; } = readAt;

        public double Due { get; set; } = due;
    }

    private sealed class Track(int hp, int max)
    {
        public int Real = hp;
        public int Max = max;
        public int Shown = hp;
        public readonly List<Held> Waiting = [];
    }

    private readonly Dictionary<uint, Track> _tracks = [];

    public HpPresenter(HpSyncMode mode = HpSyncMode.Synchronized) => Mode = mode;

    public HpSyncMode Mode { get; set; }

    /// <summary>Whether some change is held back, so the game's bar is worth looking at.</summary>
    public bool HasWaiting => _tracks.Values.Any(t => t.Waiting.Count > 0);

    /// <summary>When the next held change is due on its own, or null when none is held.</summary>
    public double? NextDue => _tracks.Values.SelectMany(t => t.Waiting).Select(w => (double?)w.Due).Min();

    /// <summary>Forgets everything: the next reading of each Pokémon is taken as it is, nothing is announced.</summary>
    public void Reset() => _tracks.Clear();

    /// <summary>Tells the life really read for a Pokémon at a moment.</summary>
    /// <returns>True when this reading holds a change back that the game's bar will have to release: the bar is worth arming.</returns>
    public bool Observe(uint pid, int hp, int max, double now, bool inBattle)
    {
        if (!_tracks.TryGetValue(pid, out var track))
        {
            _tracks[pid] = new Track(hp, max);
            return false;
        }

        if (hp == track.Real && max == track.Max) return false;

        var maximumMoved = max != track.Max;
        var before = track.Real;
        track.Real = hp;
        track.Max = max;

        if (Mode == HpSyncMode.Instant || !inBattle || maximumMoved)
        {
            // Out of a battle, or a level: it is shown at once, and whatever was held for this Pokémon goes with it.
            track.Shown = hp;
            track.Waiting.Clear();
            return false;
        }

        var share = max > 0 ? Math.Abs(hp - before) / (double)max : 0;
        track.Waiting.Add(new Held(hp, now, now + (Mode == HpSyncMode.Delayed ? HpSyncTable.DelayedSeconds : HpSyncTable.CalculatedDelay(share))));
        return Mode == HpSyncMode.Synchronized;
    }

    /// <summary>The battle is over: what is held back is shown now, the game has shown it all.</summary>
    public void Flush()
    {
        foreach (var track in _tracks.Values)
        {
            track.Shown = track.Real;
            track.Waiting.Clear();
        }
    }

    /// <summary>The bar of the game started to move at <paramref name="now"/>: the oldest change read before it is shown now.</summary>
    public void BarStarted(double now)
    {
        if (Mode != HpSyncMode.Synchronized) return;
        Held? oldest = null;
        foreach (var held in _tracks.Values.SelectMany(t => t.Waiting))
        {
            if (held.ReadAt <= now && (oldest is null || held.ReadAt < oldest.ReadAt)) oldest = held;
        }

        if (oldest is not null) oldest.Due = Math.Min(oldest.Due, now);
    }

    /// <summary>The life to show for a Pokémon at a moment; what was read last when nothing is known of it.</summary>
    public int Shown(uint pid, double now)
    {
        if (!_tracks.TryGetValue(pid, out var track)) return 0;

        // In the order they were read: each whose time has come becomes what is shown, and the changes behind a held one wait for it.
        while (track.Waiting.Count > 0 && track.Waiting[0].Due <= now)
        {
            track.Shown = track.Waiting[0].Hp;
            track.Waiting.RemoveAt(0);
        }

        return track.Shown;
    }

    /// <summary>Whether the panel has a Pokémon of that identity.</summary>
    public bool Knows(uint pid) => _tracks.ContainsKey(pid);

    /// <summary>Forgets the Pokémon that are not in the party any more.</summary>
    public void Keep(IReadOnlyCollection<uint> pids)
    {
        foreach (var gone in _tracks.Keys.Where(pid => !pids.Contains(pid)).ToList()) _tracks.Remove(gone);
    }
}
