namespace PermaLocke.GameLink.Battle;

/// <summary>What the player's HP bar shows on the battle screen.</summary>
public enum HpBarState
{
    /// <summary>The box is not on screen: an attack animation, a message box over it, another window.</summary>
    Hidden,

    /// <summary>The bar has colour left in it.</summary>
    Filled,

    /// <summary>The box is there and the bar is all empty track: it has reached zero.</summary>
    Empty
}

/// <summary>
/// Reads the player's HP bar from the pixels of the battle screen.
/// </summary>
/// <remarks>
/// <para>
/// Why the screen and not memory: the ceremony has to start when the bar reaches zero, and memory has no
/// value for that moment. Measured with screen and memory on one clock (§114 ter), both battle tables
/// change before the bar moves — one when the move is chosen, the other when «X used Y» appears — and the
/// bar drains after an attack animation whose length depends on the move. The bar's own animated value
/// was searched for and the search froze the game. Looking at the bar costs the emulator nothing.
/// </para>
/// <para>
/// Geometry and colours measured on frames of the player's recording of a real death, Leavanny at 12 HP:
/// on the 400×240 top screen the bar runs from x 5 to 89 along y 218-220. A filled cell is a saturated
/// green, yellow or red — (148,254,48), (253,201,43), (251,0,20) — and the empty track is a neutral grey,
/// (67,67,64). Counted along the row, colour plus grey was 85 of 85 with the box on screen, the colour
/// alone shrank with the HP (85, 53, 36, 18 for 12, 6, 4 and 2 of 12), and with a message box over it both
/// were zero. That is what tells an empty bar from a hidden one.
/// </para>
/// </remarks>
public static class HpBar
{
    public const int Left = 5;
    public const int Right = 89;
    public const int FirstRow = 218;
    public const int LastRow = 220;

    /// <summary>Samples per row: one per native pixel of the bar.</summary>
    public static int Samples => Right - Left + 1;

    /// <summary>
    /// Classifies one row of the bar from its sampled pixels, in blue-green-red-alpha order.
    /// </summary>
    public static HpBarState Classify(ReadOnlySpan<byte> bgra) => Measure(bgra).State;

    /// <summary>
    /// Classifies one row and says how much of it has colour, from 0 to 1.
    /// </summary>
    public static HpBarReading Measure(ReadOnlySpan<byte> bgra)
    {
        var count = bgra.Length / 4;
        var filled = 0;
        var track = 0;

        for (var i = 0; i < count; i++)
        {
            var b = bgra[i * 4];
            var g = bgra[(i * 4) + 1];
            var r = bgra[(i * 4) + 2];

            if (IsFill(r, g, b))
            {
                filled++;
            }
            else if (IsTrack(r, g, b))
            {
                track++;
            }
        }

        // Con la caja en pantalla toda la fila es relleno o hueco; se deja margen para el borde y la
        // compresión. Menos que eso es que no se está viendo la barra.
        if (count == 0 || filled + track < count * 0.85)
        {
            return new HpBarReading(HpBarState.Hidden, 0);
        }

        return new HpBarReading(filled == 0 ? HpBarState.Empty : HpBarState.Filled, filled / (double)count);
    }

    /// <summary>The three rows together: the state most of them agree on, hidden when they do not.</summary>
    public static HpBarState Combine(IReadOnlyList<HpBarState> rows) =>
        Combine([.. rows.Select(state => new HpBarReading(state, 0))]).State;

    /// <summary>The rows together, with the colour of the rows that agree.</summary>
    public static HpBarReading Combine(IReadOnlyList<HpBarReading> rows)
    {
        var winner = rows.GroupBy(row => row.State).OrderByDescending(group => group.Count()).First();

        return winner.Count() * 2 > rows.Count
            ? new HpBarReading(winner.Key, winner.Average(row => row.Fill))
            : new HpBarReading(HpBarState.Hidden, 0);
    }

    /// <summary>
    /// Decides, reading after reading, the moment the bar reaches zero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bar that reaches zero drains in sight: colour, less colour, red, none. Two ways of seeing that
    /// count, and one that must never:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Empty right after low colour</b> — red, at most <see cref="LowFill"/> of the bar — counts at
    /// once, even across a hidden reading of up to <see cref="MaxGap"/>. The first version asked for two
    /// empty readings with nothing in between, and a real death ended in its six-second fallback: the
    /// player's box does not stay on screen long at zero, and reading the window was slower than
    /// measured.</item>
    /// <item><b>Empty right after any colour</b>, held for two readings, counts too.</item>
    /// <item><b>Empty after a full bar and a hidden stretch never counts.</b> Measured at the move menu with
    /// nobody hit: the box hid with the bar full and came back as seven seconds of grey.</item>
    /// </list>
    /// </remarks>
    public sealed class ZeroWatch
    {
        /// <summary>Red territory: how little colour the bar must have shown before an empty reading.</summary>
        public const double LowFill = 0.30;

        /// <summary>The longest hidden stretch allowed between that low colour and the empty reading.</summary>
        public const double MaxGap = 600;

        private double? _lastColour;
        private double _lastColourAt;
        private bool _emptyRightAfterColour;
        private int _empties;
        private HpBarState _previous = HpBarState.Hidden;

        /// <summary>Whether the bar has been seen with colour at any point.</summary>
        public bool SawColour => _lastColour is not null;

        /// <returns>Whether this reading completes a fall to zero.</returns>
        public bool Observe(HpBarReading reading, double ms)
        {
            var previous = _previous;
            _previous = reading.State;

            switch (reading.State)
            {
                case HpBarState.Filled:
                    _lastColour = reading.Fill;
                    _lastColourAt = ms;
                    _empties = 0;
                    return false;

                case HpBarState.Hidden:
                    _empties = 0;
                    _emptyRightAfterColour = false;
                    return false;
            }

            if (_lastColour is not { } colour)
            {
                return false;
            }

            if (previous != HpBarState.Empty)
            {
                _empties = 0;
                _emptyRightAfterColour = previous == HpBarState.Filled;
            }

            _empties++;

            if (colour <= LowFill && ms - _lastColourAt <= MaxGap)
            {
                return true;
            }

            return _emptyRightAfterColour && _empties >= 2;
        }
    }

    private static bool IsFill(byte r, byte g, byte b)
    {
        var high = Math.Max(r, g);
        return high > 150 && b < 110 && high - b > 80;
    }

    private static bool IsTrack(byte r, byte g, byte b) =>
        Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14 && r is > 40 and < 100;
}

/// <summary>One reading of the bar: what it shows and how much of it has colour, from 0 to 1.</summary>
public readonly record struct HpBarReading(HpBarState State, double Fill);
