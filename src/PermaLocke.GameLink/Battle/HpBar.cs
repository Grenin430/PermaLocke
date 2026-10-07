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
    /// <item><b>Empty with no colour seen at all</b> counts after <see cref="EmptiesWithoutColour"/> readings: the
    /// bar had already reached zero when the fall arrived, which is what poison and entry hazards look like (§165).
    /// This watch only ever runs on a fall both tables already agree on, so a box at zero is that fall.</item>
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

        /// <summary>Empty readings in a row that count as a fall when no colour has been seen at all.</summary>
        public const int EmptiesWithoutColour = 3;

        /// <summary>
        /// How long to wait for a box that has not appeared at all before giving up on seeing the bar.
        /// </summary>
        /// <remarks>
        /// In the fifty falls measured up to 2026-09-22 the bar was seen at zero within 1889 ms at the worst, because
        /// the fall is given when the second table reaches zero, which is the bar itself finishing. A box that has not
        /// shown by now is one that has already gone — which is what a death by poison or by an entry hazard looks
        /// like — and there is nothing left to wait for (§165). It was 2500 ms until 1.0.12: a long attack animation hides the box
        /// for longer than that, and the ceremony then came over the game before the Pokémon had fainted (§234).
        /// </remarks>
        public const double NoBoxLimit = 4_000;

        private double? _lastColour;
        private double _lastColourAt;
        private bool _emptyRightAfterColour;
        private int _empties;
        private bool _sawBox;
        private HpBarState _previous = HpBarState.Hidden;
        private double? _hiddenSince;
        private bool _draining;
        private int _hiddenReads;

        /// <summary>Whether the bar has been seen with colour at any point.</summary>
        public bool SawColour => _lastColour is not null;

        /// <summary>Whether the box has been on screen at all, with colour or empty.</summary>
        public bool SawBox => _sawBox;

        /// <summary>Whether to stop waiting because the box has never been on screen. See <see cref="NoBoxLimit"/>.</summary>
        public bool GiveUpWithoutBox(double ms) => !_sawBox && ms >= NoBoxLimit;

        /// <summary>
        /// How long a box may stay hidden after the bar was last seen in red (30 % or less) before it is taken as the fall. When a
        /// Pokémon faints the game takes the box away and does not always show it empty: two deaths of 2026-10-06 and 10-07 left the
        /// watch waiting its six seconds with the Pokémon long gone. An attack animation hides the box for less than this (§234).
        /// </summary>
        public const double HiddenAfterLowLimit = 3_500;

        /// <summary>Whether to stop waiting: the box went away with the bar nearly empty and has not come back.</summary>
        public bool GiveUpHiddenAfterLow(double ms) =>
            _lastColour is <= LowFill && _hiddenSince is { } since && ms - since >= HiddenAfterLowLimit;

        /// <returns>Whether this reading completes a fall to zero.</returns>
        public bool Observe(HpBarReading reading, double ms)
        {
            var previous = _previous;
            _previous = reading.State;

            switch (reading.State)
            {
                case HpBarState.Filled:
                    // Bajando: menos color que la lectura anterior.
                    _hiddenReads = 0;
                    _draining = _lastColour is { } before && reading.Fill < before;
                    _lastColour = reading.Fill;
                    _lastColourAt = ms;
                    _empties = 0;
                    _sawBox = true;
                    _hiddenSince = null;
                    return false;

                case HpBarState.Hidden:
                    _hiddenSince ??= ms;
                    _hiddenReads++;
                    _empties = 0;
                    _emptyRightAfterColour = false;

                    // La barra bajaba casi a cero y la caja se esconde al instante: el juego se la lleva sin enseñarla
                    // vacía (a doble velocidad dura menos que una lectura). Es la caída (§234).
                    // Dos lecturas ocultas seguidas, no una: una captura fallida o una ventana delante dan una sola.
                    return _draining && _hiddenReads >= 2 && _lastColour is <= 0.15 && ms - _lastColourAt <= 150;
            }

            _sawBox = true;
            _hiddenSince = null;
            _hiddenReads = 0;

            if (previous != HpBarState.Empty)
            {
                _empties = 0;
                _emptyRightAfterColour = previous == HpBarState.Filled;
            }

            _empties++;

            if (_lastColour is not { } colour)
            {
                // La caja está a la vista y vacía sin haber visto color: la barra ya había llegado a cero antes de
                // que las tablas dieran la caída, que es lo que pasa con el veneno y con las trampas de entrada
                // (§165). Tres lecturas seguidas, para no fiarlo a un fotograma suelto.
                return _empties >= EmptiesWithoutColour;
            }

            if (colour <= LowFill && ms - _lastColourAt <= MaxGap)
            {
                return true;
            }

            return _emptyRightAfterColour && _empties >= 2;
        }
    }

    /// <summary>The bar's three colours, measured on frames of a real death.</summary>
    private static readonly (int R, int G, int B)[] Fills = [(148, 254, 48), (253, 201, 43), (251, 0, 20)];

    /// <summary>
    /// How far a cell may be from one of those colours and still be the bar: enough for the emulator's scaling,
    /// which blurs the edges of the bar but not its middle.
    /// </summary>
    private const int Tolerance = 60;

    /// <summary>
    /// A cell of colour: one of the bar's own three, and not any bright warm thing that happens to be there.
    /// </summary>
    /// <remarks>
    /// Until 2026-09-22 this asked only for a saturated warm colour, and the orange floor of the Iki Town ring —
    /// (170,110,65) along the bar's rows, measured on a killcam — passed it. Two deaths that day, by poison and by
    /// Stealth Rock, are only given by the tables once the game has already shown the faint and taken the box away:
    /// the watcher then read the floor as a bar at 97 % of colour and waited its whole six seconds (§165).
    /// </remarks>
    private static bool IsFill(byte r, byte g, byte b)
    {
        foreach (var (fr, fg, fb) in Fills)
        {
            if (Math.Abs(r - fr) <= Tolerance && Math.Abs(g - fg) <= Tolerance && Math.Abs(b - fb) <= Tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrack(byte r, byte g, byte b) =>
        Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14 && r is > 40 and < 100;
}

/// <summary>One reading of the bar: what it shows and how much of it has colour, from 0 to 1.</summary>
public readonly record struct HpBarReading(HpBarState State, double Fill);
