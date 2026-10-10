namespace PermaLocke.GameLink.Battle;

/// <summary>Where the player's HP bar is in the picture of the emulator, in 3DS pixels, for the layout the player chose.</summary>
/// <param name="PictureWidth">The width of the whole 3DS picture in its layout, in native pixels.</param>
/// <param name="PictureHeight">Its height.</param>
/// <param name="TopX">Where the top screen (400×240, the one that carries the player's HP box) starts in that picture.</param>
/// <param name="TopY">Where it starts down the picture.</param>
public readonly record struct HpBarPlace(int PictureWidth, int PictureHeight, int TopX, int TopY);

/// <summary>
/// The place of the bar for each layout of the screens (2026-10-11), not only the default: the stacked layout, either way up; a single screen when it is
/// the top one; side by side, either way round. A layout this does not compute (a large screen, a hybrid, a turned picture, a single screen that is the
/// bottom one) gives nothing: the bar is not looked for, and whoever asked uses the delay it computes instead.
/// </summary>
public static class HpBarGeometry
{
    public static HpBarPlace? Of(AzaharScreenLayout layout)
    {
        if (layout.Upright) return null;
        return layout.Option switch
        {
            0 => new HpBarPlace(400, 480, 0, layout.Swapped ? 240 : 0),
            1 => layout.Swapped ? null : new HpBarPlace(400, 240, 0, 0),
            3 => new HpBarPlace(720, 240, layout.Swapped ? 320 : 0, 0),
            _ => null
        };
    }
}

/// <summary>What the bar did, as the tracker saw it.</summary>
public enum BarSignalKind
{
    None,

    /// <summary>The bar began to move: the game is showing the change now.</summary>
    Started,

    /// <summary>The bar stopped: how long it moved and where it ended.</summary>
    Ended
}

/// <param name="At">The moment of the reading that saw it, in the ticks the readings were given in.</param>
/// <param name="Fill">The bar's fill when it started or stopped, 0 to 1.</param>
/// <param name="Rising">The bar went up (a heal) and not down.</param>
/// <param name="Duration">For <see cref="BarSignalKind.Ended"/>: ticks between the start and the stop.</param>
public readonly record struct BarSignal(BarSignalKind Kind, long At, double Fill, bool Rising, long Duration);

/// <summary>
/// Decides from the readings of the HP bar when the game starts to show a change and when it stops (2026-10-11). Memory knows the change before the
/// bar moves, by the length of a message and an attack animation, so memory cannot say when; the bar can. Pure: readings in, signals out.
/// </summary>
/// <remarks>
/// It is armed when memory says the life changed and takes the first reading with colour as the bar's starting fill; the bar has started when it
/// has moved by <see cref="Step"/> (a bit more than a pixel of the 85 it has) and has stopped when it has not moved for <see cref="SettleReadings"/>
/// readings. A hidden bar (an attack animation, a message box over it) is not a reading. A bar that is empty after a hidden stretch counts for nothing,
/// as in the death watch: measured at the move menu, the box hid with the bar full and came back as seven seconds of grey.
/// </remarks>
public sealed class BarTracker
{
    /// <summary>How much the fill has to move to count as moving.</summary>
    public const double Step = 0.02;

    /// <summary>How many readings in a row without moving mean it has stopped.</summary>
    public const int SettleReadings = 4;

    private enum Phase
    {
        Idle,
        Waiting,
        Moving
    }

    private Phase _phase;
    private double? _baseline;
    private bool _hiddenSinceBaseline;
    private double _last;
    private int _still;
    private long _startedAt;
    private bool _rising;

    public bool Armed => _phase != Phase.Idle;

    /// <summary>Starts waiting for the bar to move; the bar's own fill, read from now, is where it starts from.</summary>
    public void Arm()
    {
        if (_phase == Phase.Moving) return;
        _phase = Phase.Waiting;
        _baseline = null;
        _hiddenSinceBaseline = false;
    }

    public void Disarm() => _phase = Phase.Idle;

    public BarSignal Observe(HpBarReading reading, long at)
    {
        if (_phase == Phase.Idle) return default;

        if (reading.State == HpBarState.Hidden)
        {
            if (_baseline is not null) _hiddenSinceBaseline = true;
            return default;
        }

        if (_phase == Phase.Waiting)
        {
            if (_baseline is null)
            {
                _baseline = reading.Fill;
                _hiddenSinceBaseline = false;
                return default;
            }

            // Grey after a stretch with the box hidden is the box coming back at zero, not a bar that moved.
            if (reading.State == HpBarState.Empty && _hiddenSinceBaseline) return default;

            var delta = reading.Fill - _baseline.Value;
            if (Math.Abs(delta) < Step) return default;

            _phase = Phase.Moving;
            _rising = delta > 0;
            _startedAt = at;
            _last = reading.Fill;
            _still = 0;
            return new BarSignal(BarSignalKind.Started, at, reading.Fill, _rising, 0);
        }

        if (Math.Abs(reading.Fill - _last) < 0.006)
        {
            if (++_still >= SettleReadings)
            {
                _phase = Phase.Idle;
                return new BarSignal(BarSignalKind.Ended, at, reading.Fill, _rising, at - _startedAt);
            }
        }
        else
        {
            _last = reading.Fill;
            _still = 0;
        }

        return default;
    }
}
