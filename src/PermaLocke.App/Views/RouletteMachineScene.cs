using System.Windows.Media;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>One of the six wedges as the wheel shows it once it is turned over.</summary>
/// <param name="Label">The short name, in capitals: it may break into two lines.</param>
/// <param name="Figure">The number it shows big: «+200», «×3», «−1».</param>
/// <param name="Good">Green when it pays, red when it costs.</param>
/// <param name="Icon">Its picture from the cartridge, or null when the face has none.</param>
/// <param name="FaceId">Which of the sixteen it is, so the prize board can light the same one.</param>
public sealed record RouletteWedge(string Label, string Figure, bool Good, RoomSprite? Icon, string FaceId);

/// <summary>One of the sixteen faces on the prize board.</summary>
public sealed record RouletteBoardItem(string Id, string Label, string Figure, bool Good);

/// <summary>One spin as the wheel plays it. Everything in it was decided, written and recorded before it exists.</summary>
/// <param name="StartAngle">Where the wheel was left by the last spin, so it does not jump.</param>
/// <param name="OwedBefore">Spins owed before this one: the chip that goes in is one of them.</param>
public sealed record RouletteShow(
    IReadOnlyList<RouletteWedge> Wedges,
    int WinningIndex,
    WheelEnding Ending,
    double StartAngle,
    int Seed,
    int OwedBefore);

/// <summary>Everything one frame of the wheel shows.</summary>
/// <param name="Board">The sixteen faces, in the catalogue's order.</param>
/// <param name="Show">The spin being played, or null while the wheel waits with question marks.</param>
/// <param name="T">Seconds since the spin was confirmed.</param>
/// <param name="Owed">Spins owed, for the counter and the pile of chips, while no spin is playing.</param>
public sealed record RouletteSceneState(IReadOnlyList<RouletteBoardItem> Board, RouletteShow? Show, double T, int Owed);

/// <summary>
/// When each thing happens in a spin, in seconds from the moment it was confirmed, and where the wheel is at any
/// moment.
/// </summary>
/// <remarks>
/// <para>
/// The same for every spin, good or bad. The braking profile varies, and it is drawn from its own stream
/// (<see cref="WheelEnding.For"/>), never from the face.
/// </para>
/// <para>
/// The angle is a pure function of the time so the wheel can be drawn from any moment — leaving the screen mid-spin
/// and coming back finds it exactly where it should be — and so the rules the player asked for can be tested: one
/// sweep that never goes back past the winner (§86), and the winner under the pointer once it stops.
/// </para>
/// </remarks>
public static class RouletteTimeline
{
    public const int Wedges = 6;

    /// <summary>The chip lands in the slot.</summary>
    public const double ChipIn = 0.7;

    public const double LeverDown = 0.8;
    public const double LeverUp = 1.35;

    /// <summary>The first question mark turns over.</summary>
    public const double FirstReveal = 1.5;

    /// <summary>Between two wedges turning over. Long enough to read each one (§84).</summary>
    public const double RevealGap = 1.5;

    /// <summary>The wheel is pulled back a little before it is let go.</summary>
    public const double WindUp = 0.35;

    public const double WindUpDegrees = 10;

    public const double SpinTime = 12;
    public const double SettleTime = 0.42;
    public const double RockTime = 0.3;
    public const double RockDegrees = 2.5;
    public const int Turns = 11;

    public static double RevealAt(int index) => FirstReveal + (index * RevealGap);

    public static double SpinStart => RevealAt(Wedges - 1) + RevealGap + WindUp;

    /// <summary>The wheel stops on the winner.</summary>
    public static double Stopped => SpinStart + SpinTime;

    /// <summary>It has rocked back into place: the card can say what it was.</summary>
    public static double Landed => Stopped + RockTime;

    /// <summary>Where the wheel rests for wedge <paramref name="index"/> to sit under the pointer at twelve.</summary>
    public static double RestingAngle(int index) => 360 - ((index * 60) + 30);

    public static double Wrap(double angle) => ((angle % 360) + 360) % 360;

    /// <summary>Which wedge is under the pointer at a given rotation.</summary>
    public static int Under(double angle) => (int)Math.Floor(Wrap(-angle) / 60) % Wedges;

    /// <summary>
    /// The wheel's rotation at <paramref name="t"/>, clockwise, in degrees.
    /// </summary>
    /// <remarks>
    /// Still until the wind-up, a little pull back, then one sweep with the profile's brake onto the winner — or a few
    /// degrees past it when the profile settles back, never more than <see cref="WheelEnding.MaxBounce"/> — and a
    /// rock of <see cref="RockDegrees"/> when it lands, a twenty-fourth of a wedge.
    /// </remarks>
    public static double AngleAt(double startAngle, int winner, WheelEnding ending, double t)
    {
        var from = Wrap(startAngle);
        var windFrom = SpinStart - WindUp;

        if (t <= windFrom)
        {
            return from;
        }

        if (t < SpinStart)
        {
            var p = (t - windFrom) / WindUp;
            return from - (WindUpDegrees * (1 - Math.Cos(p * Math.PI)) / 2);
        }

        var resting = from + (360 * (Turns + ending.ExtraTurns)) + Wrap(RestingAngle(winner) - from);
        var target = resting + ending.Bounce;
        var sweep = ending.Bounce > 0 ? SpinTime - SettleTime : SpinTime;
        var start = from - WindUpDegrees;
        var s = t - SpinStart;

        if (s < sweep)
        {
            var p = s / sweep;
            return start + ((target - start) * (1 - Math.Pow(1 - p, ending.Power)));
        }

        if (ending.Bounce > 0 && s < SpinTime)
        {
            var p = (s - sweep) / SettleTime;
            return target + ((resting - target) * Math.Sin(p * Math.PI / 2));
        }

        var rock = s - SpinTime;
        return rock < RockTime ? resting + (RockDegrees * Math.Sin(rock / RockTime * Math.PI)) : resting;
    }
}

/// <summary>
/// The LUDÓPATA wheel as a wheel of fortune in the arcade room, drawn cell by cell: a gold rim of bulbs, pegs that
/// knock the pointer, six wedges that turn over one by one, a prize board, a counter of what is owed and a pile of
/// chips that pays for each spin.
/// </summary>
/// <remarks>
/// <para>
/// Asked for on 2026-09-23, after the capsule machine (§171) and the trade cabin (§174), in the same pixel art and
/// the same room (§175). What the wheel means does not change: green pays and red costs (§84), the six faces are
/// turned over before it moves, it stops in one sweep on the winner and does not change its mind (§86), and the
/// winner stays lit while the other five go dark.
/// </para>
/// <para>
/// Nothing on screen decides anything. By the first frame the spin has been computed from the run seed, written into
/// the save and recorded; the wheel shows a result that already exists.
/// </para>
/// </remarks>
public sealed class RouletteMachineScene : PixelScene
{
    public const int DesignWidth = 350;
    public const int DesignRows = 176;

    private const int FloorTop = 128;
    private const double WX = 215.0;
    private const double WY = 86.0;
    private const double ROuter = 72.0;
    private const double RGold = 64.0;
    private const double RLip = 62.0;
    private const double RHub = 15.0;
    private const double RFace = 11.0;
    private const double RBulbs = 67.5;
    private const double RPegs = 59.5;
    private const double RLabel = 37.0;
    private const int Bulbs = 20;
    private const double PivotX = 215.0;
    private const double PivotY = 7.0;
    private const double SlotX = 264.0;
    private const double SlotY = 153.0;
    private const int WheelShift = -40;
    private const int RightShift = -80;

    // --------------------------------------------------------------------------------------------- palette
    private static readonly Color WedgeGood = Rgb(0x3E, 0x8F, 0x57);
    private static readonly Color WedgeGoodAlt = Rgb(0x2E, 0x73, 0x45);
    private static readonly Color WedgeBad = Rgb(0xB8, 0x43, 0x3A);
    private static readonly Color WedgeBadAlt = Rgb(0x93, 0x33, 0x2C);
    private static readonly Color WedgeHidden = Rgb(0x2A, 0x22, 0x46);
    private static readonly Color WedgeHiddenAlt = Rgb(0x22, 0x1B, 0x3A);
    private static readonly Color Lip = Rgb(0x13, 0x0F, 0x1F);
    private static readonly Color PointerLight = Rgb(0xFF, 0x7A, 0x5E);
    private static readonly Color PointerRed = Rgb(0xD9, 0x3B, 0x2B);
    private static readonly Color PointerDark = Rgb(0x8C, 0x1E, 0x1A);
    private static readonly Color GoodGlow = Rgb(0x7C, 0xF0, 0x96);
    private static readonly Color BadGlow = Rgb(0xFF, 0x6A, 0x55);
    private static readonly Color Led = Rgb(0xFF, 0x52, 0x34);
    private static readonly Color LedDim = Rgb(0x3A, 0x12, 0x0E);
    private static readonly Color BoardText = Rgb(0x6E, 0x66, 0x8A);
    private static readonly Color Felt = Rgb(0x3A, 0x1E, 0x5C);
    private static readonly Color FeltLight = Rgb(0x4E, 0x2C, 0x78);
    private static readonly Color ChipViolet = Rgb(0x8A, 0x4C, 0xE0);
    private static readonly Color ChipVioletDark = Rgb(0x52, 0x28, 0x94);
    private static readonly Color ChipWhite = Rgb(0xF0, 0xEC, 0xFA);
    private static readonly Color[] Cabinet = Tones(Rgb(0x5A, 0x34, 0x94));
    private static readonly Color[] Confetti =
    [
        Rgb(0x7C, 0xF0, 0x96), Rgb(0xFF, 0xDC, 0x7A), Rgb(0xFA, 0xF8, 0xFF), Rgb(0xB0, 0x7B, 0xF0), Rgb(0x6C, 0xC8, 0xFF)
    ];

    public RouletteMachineScene(int width, int height = DesignRows)
        : base(width, height, DesignWidth, DesignRows)
    {
        PaintBackground();
    }

    /// <summary>
    /// Draws one frame.
    /// </summary>
    /// <param name="clock">Seconds since the screen opened, for what moves on its own: bulbs, neon.</param>
    /// <param name="toScreen">False to leave the bitmap alone, for pictures drawn off screen.</param>
    public void Render(RouletteSceneState state, double clock, bool toScreen = true)
    {
        var show = state.Show;
        var t = show is null ? -1 : state.T;
        var angle = show is null ? 0 : RouletteTimeline.AngleAt(show.StartAngle, show.WinningIndex, show.Ending, t);
        var before = show is null ? 0 : RouletteTimeline.AngleAt(show.StartAngle, show.WinningIndex, show.Ending, t - (1 / 30.0));
        var speed = Math.Abs(angle - before);
        var owed = show is not null && t < RouletteTimeline.Landed + 1
            ? Math.Max(0, show.OwedBefore - (t >= RouletteTimeline.ChipIn ? 1 : 0))
            : state.Owed;
        var pile = show is not null && t < RouletteTimeline.Landed + 1 ? Math.Max(0, show.OwedBefore - 1) : state.Owed;

        var (tint, strength) = Tint(show, t, angle, speed);

        BeginFrame();

        // Diseñada a 430 columnas y apretada a 350 para que quepa en GRANDE: la lista se queda, la rueda entra 40 y
        // el marcador y las fichas 80.
        var origin = Ox;
        DrawBoard(state.Board, show, t, clock);
        Ox = origin + RightShift;
        DrawScoreboard(owed, show, t, clock);
        DrawNeon(clock);
        DrawChips(pile);
        Ox = origin + WheelShift;
        DrawHalo(tint, strength);
        DrawWheel(show, t, angle, clock, tint, strength, speed);
        DrawPointer(angle);
        DrawLever(show, t);
        DrawSlotGlow(show, t);

        if (show is not null)
        {
            DrawChipFlight(show, t);
            DrawLanding(show, t);
        }

        Ox = origin;

        if (toScreen)
        {
            Present();
        }
    }

    // ============================================================================================= background

    private static Color[] Tones(Color basis) =>
        [Lerp(basis, White, 0.45), Lerp(basis, White, 0.18), basis, Lerp(basis, Void, 0.32), Lerp(basis, Void, 0.58)];

    private void PaintBackground()
    {
        Ox += WheelShift;
        PaintRoom(FloorTop, DesignRows, WX, WX);

        // La sombra dura de la rueda en la pared, un poco abajo a la derecha: la rueda está colgada, no pintada.
        for (var y = (int)(WY - ROuter); y <= (int)(WY + ROuter + 8); y++)
        {
            for (var x = (int)(WX - ROuter); x <= (int)(WX + ROuter + 8); x++)
            {
                var d = Math.Sqrt(Math.Pow(x + 0.5 - WX - 5, 2) + Math.Pow(y + 0.5 - WY - 6, 2));
                if (d <= ROuter && y < FloorTop - 4 && (d < ROuter - 2 || Bayer[y & 3, x & 3] < 8)) Put(Back, x, y, Shadow);
            }
        }

        // La sombra del puesto en el suelo.
        for (var y = 160; y < DesignRows; y++)
        {
            for (var x = 150; x < 282; x++)
            {
                var d = Math.Pow((x + 0.5 - WX) / 64.0, 2) + Math.Pow((y + 0.5 - 172.0) / 5.0, 2);
                if (d < 1 && (d < 0.7 || Bayer[y & 3, x & 3] < 8)) Put(Back, x, y, Shadow);
            }
        }

        // La mesita de las fichas y su sombra.
        Ox += RightShift - WheelShift;
        for (var y = 150; y < 166; y++)
        {
            for (var x = 342; x < 404; x++)
            {
                var d = Math.Pow((x + 0.5 - 373) / 28.0, 2) + Math.Pow((y + 0.5 - 162.0) / 4.0, 2);
                if (d < 1 && (d < 0.7 || Bayer[y & 3, x & 3] < 8)) Put(Back, x, y, Shadow);
            }
        }
        Ox -= RightShift;
    }

    // ============================================================================================= the wall

    /// <summary>
    /// The prize board on the left wall: the sixteen faces, what pays on top and what costs below. A lamp by each one
    /// lights as it turns up on the wheel, and the winner blinks gold.
    /// </summary>
    private void DrawBoard(IReadOnlyList<RouletteBoardItem> board, RouletteShow? show, double t, double clock)
    {
        const int left = 10;
        const int top = 6;
        const int width = 88;
        const int row = 6;

        var good = board.Where(b => b.Good).ToList();
        var bad = board.Where(b => !b.Good).ToList();
        var height = 4 + 8 + (good.Count * row) + 3 + 8 + (bad.Count * row) + 2;

        Rect(left, top, width, height, Plate);
        Rect(left + 1, top + 1, width - 2, 1, PlateLight);
        Frame(left, top, width, height, GoldLight, GoldDark);

        var y = top + 4;
        foreach (var (title, items, colour) in new[] { ("PREMIOS", good, GoodGlow), ("CASTIGOS", bad, BadGlow) })
        {
            SmallText(title, left + ((width - SmallWidth(title)) / 2) + 1, y + 1, Outline);
            SmallText(title, left + ((width - SmallWidth(title)) / 2), y, colour);
            Rect(left + 4, y + 6, width - 8, 1, Lerp(colour, Plate, 0.6));
            y += 8;

            foreach (var item in items)
            {
                var state = Standing(item.Id, show, t);
                var lamp = state switch
                {
                    Stand.Won => ((int)(clock * 4) % 2 == 0 ? GoldHi : Gold),
                    Stand.OnWheel => item.Good ? GoodGlow : BadGlow,
                    _ => Rgb(0x2A, 0x24, 0x3A),
                };

                Rect(left + 4, y + 1, 3, 3, lamp);
                Border(left + 3, y, 5, 5, Outline);

                var text = $"{item.Figure} {item.Label}";
                var textColour = state switch { Stand.Won => GoldHi, Stand.OnWheel => White, _ => BoardText };
                SmallText(text, left + 10, y, textColour);

                if (state == Stand.Won)
                {
                    Border(left + 2, y - 1, width - 4, row + 1, (int)(clock * 4) % 2 == 0 ? GoldLight : GoldDark);
                }

                y += row;
            }

            y += 3;
        }
    }

    private enum Stand
    {
        Off,
        OnWheel,
        Won,
    }

    /// <summary>Whether a face is on the wheel being played, has turned up yet, and won.</summary>
    private static Stand Standing(string id, RouletteShow? show, double t)
    {
        if (show is null) return Stand.Off;

        for (var i = 0; i < show.Wedges.Count; i++)
        {
            if (show.Wedges[i].FaceId != id || t < RouletteTimeline.RevealAt(i)) continue;
            return i == show.WinningIndex && t >= RouletteTimeline.Stopped ? Stand.Won : Stand.OnWheel;
        }

        return Stand.Off;
    }

    /// <summary>The counter of spins owed, in red segments on the right wall, with bulbs round its frame.</summary>
    private void DrawScoreboard(int owed, RouletteShow? show, double t, double clock)
    {
        const int left = 324;
        const int top = 10;
        const int width = 92;
        const int height = 38;

        Rect(left, top, width, height, Void);
        Frame(left, top, width, height, GoldLight, GoldDark);
        Rect(left + 2, top + 2, width - 4, 1, PlateLight);

        var racing = show is not null && t >= 0 && t < RouletteTimeline.Stopped;
        var phase = racing ? (int)(t * 12) : (int)(clock * 2);
        var bulbs = new List<(int X, int Y)>();
        for (var x = left + 4; x < left + width - 3; x += 6) bulbs.Add((x, top - 3));
        for (var x = left + width - 5; x > left + 2; x -= 6) bulbs.Add((x, top + height + 1));
        for (var i = 0; i < bulbs.Count; i++)
        {
            var on = (i + phase) % 3 == 0;
            Rect(bulbs[i].X, bulbs[i].Y, 2, 2, on ? BulbWarm : BulbOff);
            if (on) Put(Canvas, bulbs[i].X, bulbs[i].Y, BulbOn);
        }

        SmallText("DEBES", left + ((width - SmallWidth("DEBES")) / 2), top + 4, GoldLight);

        // Dos cifras de segmentos: las apagadas debajo, como un marcador de verdad.
        var digits = Math.Clamp(owed, 0, 99).ToString("00");
        var dx = left + ((width - BigWidth("88", 2)) / 2);
        BigText("88", dx, top + 11, LedDim, 2);
        var justChanged = show is not null && t is >= RouletteTimeline.ChipIn and < RouletteTimeline.ChipIn + 0.2;
        BigText(digits, dx, top + 11, justChanged ? GoldHi : Led, 2);

        var word = owed == 1 ? "TIRADA" : "TIRADAS";
        SmallText(word, left + ((width - SmallWidth(word)) / 2), top + 28, GoldLight);
    }

    /// <summary>A neon pair of dice and the word LUDÓPATA on the right wall. It flickers now and then, as neon does.</summary>
    private void DrawNeon(double clock)
    {
        var cycle = clock % 8.3;
        var off = cycle is > 6.0 and < 6.1 or > 6.2 and < 6.27;
        var core = off ? NeonGlow : Neon;
        var tube = off ? NeonHalo : NeonMid;

        // Dos dados de neón, uno enseñando un cinco y otro un dos.
        DrawNeonDie(344, 58, 16, [(3, 3), (12, 3), (7.5, 7.5), (3, 12), (12, 12)], core, tube, off);
        DrawNeonDie(368, 54, 16, [(3.5, 3.5), (11.5, 11.5)], core, tube, off);

        const string word = "LUDÓPATA";
        var left = 370 - (BigWidth(word) / 2);
        if (!off)
        {
            foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) BigText(word, left + ox, 82 + oy, NeonGlow);
        }

        BigText(word, left, 82, core);
    }

    private void DrawNeonDie(int left, int top, int size, (double X, double Y)[] pips, Color core, Color tube, bool off)
    {
        for (var y = top - 3; y < top + size + 3; y++)
        {
            for (var x = left - 3; x < left + size + 3; x++)
            {
                var inside = x >= left && x < left + size && y >= top && y < top + size;
                var edge = inside && (x == left || x == left + size - 1 || y == top || y == top + size - 1);
                var corner = (x == left || x == left + size - 1) && (y == top || y == top + size - 1);

                if (edge && !corner) Put(Canvas, x, y, y > top + (size / 2) ? tube : core);
                else if (!off && !inside && Bayer[y & 3, x & 3] < 4) Put(Canvas, x, y, NeonGlow);
            }
        }

        foreach (var (px, py) in pips)
        {
            Rect(left + (int)px, top + (int)py, 2, 2, core);
        }
    }

    private const double TableX = 374.0;
    private const double TableY = 144.0;

    /// <summary>Where the <paramref name="index"/>-th chip of the pile sits: two stacks of eight, filled alternately.</summary>
    private static (double X, double Y) ChipAt(int index) =>
        (TableX - 10 + (index % 2 * 20), TableY - 2 - (index / 2 * 3));

    /// <summary>The little table by the wheel with the chips: one per spin owed, in two stacks.</summary>
    private void DrawChips(int count)
    {
        // La mesa: tablero de fieltro violeta con canto dorado, sobre un pie.
        Rect((int)TableX - 3, (int)TableY + 6, 7, 16, ChromeDark);
        Rect((int)TableX - 3, (int)TableY + 6, 2, 16, ChromeMid);
        Rect((int)TableX - 12, (int)TableY + 21, 25, 3, ChromeDark);
        Rect((int)TableX - 12, (int)TableY + 21, 25, 1, ChromeMid);
        Border((int)TableX - 13, (int)TableY + 20, 27, 5, Outline);

        for (var y = (int)(TableY - 9); y <= (int)(TableY + 9); y++)
        {
            for (var x = (int)(TableX - 36); x <= (int)(TableX + 36); x++)
            {
                var d = Math.Pow((x + 0.5 - TableX) / 35.0, 2) + Math.Pow((y + 0.5 - TableY) / 7.5, 2);
                if (d > 1) continue;
                var colour = d > 0.88 ? Outline : d > 0.74 ? (y < TableY ? GoldLight : GoldDark) : ((x + y) % 5 == 0 ? FeltLight : Felt);
                Put(Canvas, x, y, colour);
            }
        }

        Rect((int)TableX - 33, (int)TableY + 7, 67, 1, GoldDark);
        Rect((int)TableX - 31, (int)TableY + 8, 63, 1, Outline);

        var visible = Math.Min(count, 16);
        for (var i = 0; i < visible; i++)
        {
            var (x, y) = ChipAt(i);
            DrawChip(x, y);
        }

        // Más de dieciséis no caben en la mesa: se dice con un número, no se inventan pilas.
        if (count > 16)
        {
            SmallText($"+{count - 16}", (int)TableX + 20, (int)TableY - 30, GoldLight);
        }
    }

    /// <summary>
    /// A casino chip seen from above at an angle: violet with white notches round the edge, a gold middle and its
    /// edge showing below.
    /// </summary>
    /// <param name="squeeze">Less than 1 to draw it turned, for a chip spinning through the air.</param>
    private void DrawChip(double cx, double cy, double squeeze = 1)
    {
        var rx = 7.0 * squeeze;
        for (var y = (int)Math.Floor(cy - 4); y <= (int)Math.Ceiling(cy + 5); y++)
        {
            for (var x = (int)Math.Floor(cx - rx - 1); x <= (int)Math.Ceiling(cx + rx + 1); x++)
            {
                var nx = (x + 0.5 - cx) / Math.Max(1, rx);
                var ny = (y + 0.5 - cy) / 3.2;
                var d = (nx * nx) + (ny * ny);

                // El canto, dos filas por debajo de la cara.
                var nySide = (y + 0.5 - cy - 2) / 3.2;
                var dSide = (nx * nx) + (nySide * nySide);

                if (d <= 1)
                {
                    var notch = ((int)Math.Floor((Math.Atan2(ny, nx) + Math.PI) / (Math.PI / 4))) % 2 == 0;
                    var colour = d > 0.82 ? Outline
                        : d > 0.5 ? (notch ? ChipWhite : ChipViolet)
                        : d > 0.2 ? ChipViolet
                        : (nx + ny) < 0 ? GoldHi : Gold;
                    Put(Canvas, x, y, colour);
                }
                else if (dSide <= 1)
                {
                    var notch = ((int)Math.Floor((nx + 1) * 4)) % 2 == 0;
                    Put(Canvas, x, y, dSide > 0.82 ? Outline : notch ? Lerp(ChipWhite, Void, 0.3) : ChipVioletDark);
                }
            }
        }
    }

    // ============================================================================================= the wheel

    /// <summary>What colour the room takes from the wheel, and how much: the wedge under the pointer once it is slow.</summary>
    private static (Color Colour, double Strength) Tint(RouletteShow? show, double t, double angle, double speed)
    {
        if (show is null || t < RouletteTimeline.SpinStart) return (Void, 0);

        var under = RouletteTimeline.Under(angle);
        var colour = show.Wedges.Count > under && show.Wedges[under].Good ? GoodGlow : BadGlow;

        if (t >= RouletteTimeline.Stopped)
        {
            var since = t - RouletteTimeline.Stopped;
            return (colour, since < 0.3 ? 1 : Math.Max(0.5, 1 - ((since - 0.3) / 1.5)));
        }

        // Cuatro grados por fotograma es una cuña cada cuarto de segundo: más deprisa sería un estroboscopio.
        return speed <= 4 ? (colour, 0.55) : (Void, 0);
    }

    /// <summary>The coloured light round the wheel, tramada so it fades into the wall.</summary>
    private void DrawHalo(Color colour, double strength)
    {
        if (strength <= 0) return;

        var light = Lerp(colour, Void, 0.35);
        for (var y = (int)(WY - ROuter - 16); y <= (int)(WY + ROuter + 16); y++)
        {
            for (var x = (int)(WX - ROuter - 16); x <= (int)(WX + ROuter + 16); x++)
            {
                var r = Math.Sqrt(Math.Pow(x + 0.5 - WX, 2) + Math.Pow(y + 0.5 - WY, 2));
                if (r <= ROuter || r > ROuter + 16) continue;
                var fade = 1 - ((r - ROuter) / 16);
                if (Bayer[y & 3, x & 3] < strength * fade * 11) Put(Canvas, x, y, light);
            }
        }
    }

    /// <summary>
    /// The wheel: rim of gold with its bulbs, the six wedges, the seams, the pegs, the hub, and the faces upright on
    /// top of it all.
    /// </summary>
    private void DrawWheel(RouletteShow? show, double t, double angle, double clock, Color tint, double strength, double speed)
    {
        var stopped = show is not null && t >= RouletteTimeline.Stopped;
        var winner = show?.WinningIndex ?? -1;
        var pulse = stopped && t < RouletteTimeline.Stopped + 1.2 ? (int)((t - RouletteTimeline.Stopped) * 8) % 2 == 0 : stopped;

        DrawStand();

        for (var y = (int)(WY - ROuter - 1); y <= (int)(WY + ROuter + 1); y++)
        {
            for (var x = (int)(WX - ROuter - 1); x <= (int)(WX + ROuter + 1); x++)
            {
                var dx = x + 0.5 - WX;
                var dy = y + 0.5 - WY;
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > ROuter) continue;

                Color colour;

                if (r > ROuter - 1)
                {
                    colour = Outline;
                }
                else if (r >= RGold)
                {
                    colour = GoldAt(dx, dy, r, x, y);
                }
                else if (r >= RLip)
                {
                    colour = Lip;
                }
                else if (r >= RHub)
                {
                    colour = WedgeAt(show, t, angle, dx, dy, r, x, y, winner, stopped, pulse);
                }
                else if (r >= RFace)
                {
                    colour = r > RHub - 1 ? Outline : GoldAt(dx, dy, r * 4.5, x, y);
                }
                else
                {
                    colour = r > RFace - 1 ? GoldDark : BallWhiteFace(dx, dy);
                }

                Put(Canvas, x, y, colour);
            }
        }

        DrawPegs(angle);
        DrawBulbs(show, t, clock, tint, strength, speed);
        DrawHubRivets(angle);
        DrawBall(WX, WY, 8.2, (angle * Math.PI / 180) - 0.35, CapsuleBall.Poke, BallLook.Plain);

        for (var i = 0; i < RouletteTimeline.Wedges; i++)
        {
            var phi = (angle + (i * 60) + 30) * Math.PI / 180;
            var lx = WX + (RLabel * Math.Sin(phi));
            var ly = WY - (RLabel * Math.Cos(phi));
            DrawWedgeFace(show, i, t, lx, ly, stopped && i != winner);
        }

        if (stopped && winner >= 0)
        {
            var phi = (angle + (winner * 60) + 30) * Math.PI / 180;
            DrawWinnerSparkles(WX + (RLabel * Math.Sin(phi)), WY - (RLabel * Math.Cos(phi)), t - RouletteTimeline.Stopped, show!.Seed);
        }
    }

    private static Color BallWhiteFace(double dx, double dy) => (dx + dy) < -4 ? White : Rgb(0xE6, 0xE2, 0xEE);

    /// <summary>Gold lit from the top left: bright there, deep at the bottom right, with a tramada in between.</summary>
    private static Color GoldAt(double dx, double dy, double r, int x, int y)
    {
        var light = -((dx * 0.6) + (dy * 0.8)) / Math.Max(1, r);
        var edge = r >= ROuter - 2 ? 0.15 : r < RGold + 1 ? -0.35 : 0;
        light += edge;
        var jitter = (Bayer[y & 3, x & 3] / 16.0) - 0.5;

        return (light + (jitter * 0.25)) switch
        {
            > 0.55 => GoldHi,
            > 0.15 => GoldLight,
            > -0.35 => Gold,
            _ => GoldDark,
        };
    }

    /// <summary>
    /// The colour of a cell of the wedge disc: its wedge's colour, lit and shaded, or a seam, or the flash of a wedge
    /// turning over.
    /// </summary>
    private static Color WedgeAt(RouletteShow? show, double t, double angle, double dx, double dy, double r, int x, int y,
        int winner, bool stopped, bool pulse)
    {
        var theta = Math.Atan2(dx, -dy) * 180 / Math.PI;
        var local = RouletteTimeline.Wrap(theta - angle);
        var index = Math.Min(RouletteTimeline.Wedges - 1, (int)(local / 60));
        var within = local - (index * 60);
        var seam = Math.Min(within, 60 - within) * Math.PI / 180 * r;

        if (seam < 0.65)
        {
            var isWinnerEdge = stopped && (index == winner || (within > 30 ? (index + 1) % 6 == winner : (index + 5) % 6 == winner));
            return isWinnerEdge && pulse ? GoldHi : isWinnerEdge ? GoldLight : GoldDark;
        }

        var alternate = index % 2 == 1;
        var hidden = alternate ? WedgeHiddenAlt : WedgeHidden;
        var colour = hidden;

        if (show is not null && index < show.Wedges.Count)
        {
            var revealAt = RouletteTimeline.RevealAt(index);
            var wedge = show.Wedges[index];
            var open = wedge.Good ? (alternate ? WedgeGoodAlt : WedgeGood) : (alternate ? WedgeBadAlt : WedgeBad);

            // Se destapa de dentro afuera en un cuarto de segundo, con el frente tramado.
            var reach = (t - revealAt) / 0.25 * RLip;
            if (reach >= r + 3) colour = open;
            else if (reach >= r && Bayer[y & 3, x & 3] < 8) colour = open;

            // El fogonazo de destaparse.
            if (t >= revealAt && t < revealAt + 0.08 && Bayer[y & 3, x & 3] < 9) return White;

            if (stopped && index != winner) colour = Lerp(colour, Void, 0.55);

            // El borde de la ganadora late en oro.
            if (stopped && index == winner && r > RLip - 2.5) return pulse ? GoldHi : GoldLight;
        }

        // Luz fija de arriba a la izquierda, y el borde exterior algo más oscuro: la rueda tiene bulto.
        var light = -((dx * 0.6) + (dy * 0.8)) / RLip;
        var bayer = Bayer[y & 3, x & 3];
        if (r > 55 && bayer < (r - 55) / 7 * 9) colour = Lerp(colour, Void, 0.3);
        if (light > 0.35 && bayer < (light - 0.35) * 22) colour = Lerp(colour, White, 0.1);
        else if (light < -0.45 && bayer < (-light - 0.45) * 22) colour = Lerp(colour, Void, 0.16);
        return colour;
    }

    /// <summary>The A-frame legs behind the wheel and the booth in front with the name and the coin slot.</summary>
    private void DrawStand()
    {
        foreach (var (fromX, toX) in new[] { (192.0, 168.0), (238.0, 262.0) })
        {
            for (var y = 120; y < DesignRows; y++)
            {
                var p = (y - 120) / (double)(DesignRows - 120);
                var cx = fromX + ((toX - fromX) * p);
                for (var k = -3; k <= 3; k++)
                {
                    var colour = Math.Abs(k) == 3 ? Outline : k < -1 ? Cabinet[1] : k > 1 ? Cabinet[4] : Cabinet[3];
                    Put(Canvas, (int)Math.Round(cx) + k, y, colour);
                }
            }
        }

        // El puesto: tablero de cara con remate dorado, el nombre y la ranura de la ficha.
        const int left = 176;
        const int right = 254;
        const int top = 159;
        for (var y = top; y < DesignRows; y++)
        {
            for (var x = left; x < right; x++)
            {
                var edge = x == left || x == right - 1;
                var colour = edge ? Outline
                    : y == top || y == top + 1 ? (y == top ? GoldLight : Gold)
                    : y == top + 2 ? GoldDark
                    : x <= left + 2 ? Cabinet[1]
                    : x >= right - 4 ? Cabinet[4]
                    : Cabinet[2];
                if (colour == Cabinet[2] && x <= left + 8 && Bayer[y & 3, x & 3] < 8) colour = Cabinet[1];
                if (colour == Cabinet[2] && x >= right - 12 && Bayer[y & 3, x & 3] < 6) colour = Cabinet[3];
                Put(Canvas, x, y, colour);
            }
        }

        Rect(left - 1, top - 1, right - left + 2, 1, Outline);

        const string name = "RULETA";
        var nx = (int)WX - (BigWidth(name) / 2);
        Rect(nx - 3, top + 4, BigWidth(name) + 6, 10, Plate);
        Border(nx - 4, top + 3, BigWidth(name) + 8, 12, GoldDark);
        BigText(name, nx + 1, top + 6, Outline);
        BigText(name, nx, top + 5, BulbOn);
    }

    private void DrawPegs(double angle)
    {
        for (var k = 0; k < RouletteTimeline.Wedges; k++)
        {
            var theta = (angle + (k * 60)) * Math.PI / 180;
            var px = WX + (RPegs * Math.Sin(theta));
            var py = WY - (RPegs * Math.Cos(theta));
            Disc(px, py, 2.1, (nx, ny, d) => d > 0.72 ? Outline : (nx + ny) < -0.3 ? ChromeHi : (nx + ny) > 0.4 ? ChromeDark : ChromeLight);
        }
    }

    private void DrawHubRivets(double angle)
    {
        for (var k = 0; k < 8; k++)
        {
            var theta = (angle + 22.5 + (k * 45)) * Math.PI / 180;
            var px = (int)Math.Floor(WX + (13 * Math.Sin(theta)));
            var py = (int)Math.Floor(WY - (13 * Math.Cos(theta)));
            Put(Canvas, px, py, GoldHi);
            Put(Canvas, px + 1, py + 1, GoldDark);
        }
    }

    /// <summary>
    /// The twenty bulbs round the rim. They chase slowly while the wheel waits, blink while the faces turn over, race
    /// while it spins, take the colour of the wedge under the pointer once it is slow, and flash when it lands.
    /// </summary>
    private void DrawBulbs(RouletteShow? show, double t, double clock, Color tint, double strength, double speed)
    {
        for (var i = 0; i < Bulbs; i++)
        {
            var theta = i * 360.0 / Bulbs * Math.PI / 180;
            var bx = WX + (RBulbs * Math.Sin(theta));
            var by = WY - (RBulbs * Math.Cos(theta));

            Color? colour;

            if (show is null || t < 0)
            {
                colour = (i + (int)(clock * 3)) % 5 == 0 ? BulbOn : null;
            }
            else if (t < RouletteTimeline.LeverDown + 0.2)
            {
                colour = (i + (int)(clock * 3)) % 5 == 0 ? BulbOn : null;
            }
            else if (t < RouletteTimeline.FirstReveal)
            {
                // Se despiertan de una en una, en el sentido de las agujas.
                colour = i < (t - (RouletteTimeline.LeverDown + 0.2)) / 0.5 * Bulbs ? BulbOn : null;
            }
            else if (t < RouletteTimeline.SpinStart - RouletteTimeline.WindUp)
            {
                colour = (i + (int)(t * 3)) % 2 == 0 ? BulbOn : BulbWarm;
            }
            else if (t < RouletteTimeline.Stopped)
            {
                colour = strength > 0 ? ((i + (int)(t * 6)) % 2 == 0 ? Lerp(tint, White, 0.4) : tint)
                    : (i + (int)(t * 20)) % 4 == 0 ? BulbOn : null;
                if (speed > 25 && (i + (int)(t * 20)) % 4 != 0) colour = null;
            }
            else
            {
                var since = t - RouletteTimeline.Stopped;
                colour = since < 1.2
                    ? ((int)(since * 7) % 2 == 0 ? White : tint)
                    : (i + (int)(clock * 2)) % 2 == 0 ? Lerp(tint, White, 0.3) : tint;
            }

            DrawBulb(bx, by, colour);
        }
    }

    private void DrawBulb(double cx, double cy, Color? lit)
    {
        var x = (int)Math.Floor(cx);
        var y = (int)Math.Floor(cy);
        var glass = lit ?? BulbOff;
        var rim = lit is null ? GoldDark : Lerp(glass, Void, 0.25);

        Put(Canvas, x, y, lit is null ? Lerp(BulbOff, Void, 0.2) : White);
        Put(Canvas, x - 1, y, glass);
        Put(Canvas, x + 1, y, glass);
        Put(Canvas, x, y - 1, glass);
        Put(Canvas, x, y + 1, rim);
        Put(Canvas, x - 1, y - 1, rim);
        Put(Canvas, x + 1, y - 1, rim);
        Put(Canvas, x - 1, y + 1, Outline);
        Put(Canvas, x + 1, y + 1, Outline);
    }

    /// <summary>
    /// What a wedge shows upright on top of it: a big gold question mark until it turns over, then its picture, its
    /// figure and its name.
    /// </summary>
    private void DrawWedgeFace(RouletteShow? show, int index, double t, double cx, double cy, bool dim)
    {
        var revealAt = RouletteTimeline.RevealAt(index);
        var revealed = show is not null && index < show.Wedges.Count && t >= revealAt + 0.05;

        if (!revealed)
        {
            // La interrogación encoge justo antes de destaparse: dos aumentos, uno, y fuera.
            var scale = show is null || t < revealAt - 0.16 ? 2 : t < revealAt ? 1 : 0;
            if (scale == 0) return;
            DrawQuestion(cx, cy, scale, show is null ? Gold : GoldLight);
            return;
        }

        var wedge = show!.Wedges[index];
        var lines = SplitLabel(wedge.Label.ToUpperInvariant());
        var icon = wedge.Icon;
        var iconHeight = 0;
        if (icon is not null)
        {
            var (_, minY, _, maxY) = Bounds(icon);
            iconHeight = maxY - minY + 1;
        }

        var height = iconHeight + (iconHeight > 0 ? 2 : 0) + 7 + 2 + (lines.Count * 6) - 1;
        var top = (int)Math.Round(cy - (height / 2.0));

        // Al destaparse salta un poco, como la figura de la estantería del gacha.
        var since = t - revealAt;
        var hop = since < 0.25 ? (int)Math.Round(Math.Sin(since / 0.25 * Math.PI) * 3) : 0;
        top -= hop;

        var text = dim ? Lerp(White, Void, 0.5) : White;
        var shadow = Outline;

        if (icon is not null)
        {
            DrawIcon(icon, cx, top + iconHeight, dim ? 0.55 : 0);
            top += iconHeight + 2;
        }

        var figure = wedge.Figure;
        var fx = (int)Math.Round(cx - (BigWidth(figure) / 2.0));
        BigText(figure, fx + 1, top + 1, shadow);
        BigText(figure, fx, top, dim ? text : GoldHi);
        top += 9;

        foreach (var line in lines)
        {
            var lx = (int)Math.Round(cx - (SmallWidth(line) / 2.0));
            SmallText(line, lx + 1, top + 1, shadow);
            SmallText(line, lx, top, text);
            top += 6;
        }

        if (since < 0.5)
        {
            // Chispas al destaparse.
            var colour = wedge.Good ? GoodGlow : BadGlow;
            for (var k = 0; k < 6; k++)
            {
                var a = (k / 6.0 * Math.PI * 2) + 0.4;
                var rr = 10 + (since * 36);
                Star(cx + (Math.Cos(a) * rr), cy + (Math.Sin(a) * rr * 0.9), since < 0.2 ? 2 : 1, White, colour);
            }
        }
    }

    /// <summary>The question mark a wedge shows until it turns over: nine rows tall, gold, with a hard shadow.</summary>
    private static readonly string[] Question =
    [
        ".#####.",
        "##...##",
        "##...##",
        "....##.",
        "...##..",
        "...##..",
        ".......",
        "...##..",
        "...##..",
    ];

    private void DrawQuestion(double cx, double cy, int scale, Color colour)
    {
        var left = (int)Math.Round(cx - (3.5 * scale));
        var top = (int)Math.Round(cy - (4.5 * scale));

        for (var pass = 0; pass < 2; pass++)
        {
            var offset = pass == 0 ? scale : 0;
            for (var gy = 0; gy < Question.Length; gy++)
            {
                for (var gx = 0; gx < 7; gx++)
                {
                    if (Question[gy][gx] != '#') continue;

                    // El primer píxel de cada tramo lleva el brillo: el oro se ve de metal y no de pegatina.
                    var lead = gx == 0 || Question[gy][gx - 1] != '#';
                    var cell = pass == 0 ? GoldDark : lead ? GoldHi : colour;
                    Rect(left + (gx * scale) + offset, top + (gy * scale) + offset, scale, scale, cell);
                }
            }
        }
    }

    /// <summary>A face's picture, darkened pixel by pixel for a wedge that did not win, so nothing around it darkens.</summary>
    private void DrawIcon(RoomSprite icon, double cx, int feet, double darken)
    {
        var (minX, minY, maxX, maxY) = Bounds(icon);
        var left = (int)Math.Round(cx - ((maxX - minX + 1) / 2.0));
        var top = feet - (maxY - minY + 1);

        for (var sy = minY; sy <= maxY; sy++)
        {
            for (var sx = minX; sx <= maxX; sx++)
            {
                if (!icon.Solid(sx, sy)) continue;
                Put(Canvas, left + sx - minX, top + sy - minY, Lerp(icon.At(sx, sy), Void, darken));
            }
        }
    }

    /// <summary>A label in one line when it fits in a wedge, in two broken at the space nearest the middle when not.</summary>
    public static IReadOnlyList<string> SplitLabel(string label)
    {
        if (SmallWidth(label) <= 40 || !label.Contains(' ')) return [label];

        var best = -1;
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] == ' ' && (best < 0 || Math.Abs(i - (label.Length / 2.0)) < Math.Abs(best - (label.Length / 2.0)))) best = i;
        }

        return [label[..best], label[(best + 1)..]];
    }

    /// <summary>
    /// The pointer at twelve: a red flapper hanging from a gold mount, knocked sideways by each peg as it goes past,
    /// the way a wheel of fortune clicks.
    /// </summary>
    private void DrawPointer(double angle)
    {
        // El soporte en la pared.
        Rect((int)PivotX - 9, -2, 18, (int)PivotY + 4, Gold);
        Rect((int)PivotX - 9, -2, 18, 1, GoldLight);
        Rect((int)PivotX - 9, -2, 2, (int)PivotY + 4, GoldLight);
        Rect((int)PivotX + 7, -2, 2, (int)PivotY + 4, GoldDark);
        Border((int)PivotX - 10, -3, 20, (int)PivotY + 6, Outline);
        Put(Canvas, (int)PivotX - 7, 0, GoldHi);
        Put(Canvas, (int)PivotX + 6, 0, GoldDark);

        var deflect = Deflection(angle) * Math.PI / 180;
        var sin = Math.Sin(deflect);
        var cos = Math.Cos(deflect);

        for (var y = (int)PivotY - 3; y <= (int)PivotY + 24; y++)
        {
            for (var x = (int)PivotX - 12; x <= (int)PivotX + 12; x++)
            {
                var dx = x + 0.5 - PivotX;
                var dy = y + 0.5 - PivotY;
                var u = (dx * sin) + (dy * cos);
                var v = (dx * cos) - (dy * sin);
                if (u < -1 || u > 21) continue;

                var half = u < 3 ? 3.5 : u < 11 ? 5.5 : 5.5 * (21 - u) / 10;
                var a = Math.Abs(v);
                if (a > half + 1) continue;

                var colour = a > half ? Outline
                    : v < -half + 1.5 ? PointerLight
                    : v > half - 1.8 ? PointerDark
                    : PointerRed;
                if (u > 19.5) colour = Outline;
                Put(Canvas, x, y, colour);
            }
        }

        Disc(PivotX, PivotY, 3.2, (nx, ny, d) => d > 0.75 ? Outline : (nx + ny) < -0.2 ? GoldHi : Gold);
    }

    /// <summary>
    /// How far the pegs push the pointer at a given rotation, in degrees: a peg coming up from the left bends it
    /// clockwise, and once past it snaps back a little the other way.
    /// </summary>
    public static double Deflection(double angle)
    {
        var best = 0.0;
        for (var k = 0; k < RouletteTimeline.Wedges; k++)
        {
            var delta = RouletteTimeline.Wrap(angle + (k * 60));
            if (delta > 180) delta -= 360;

            var d = delta is >= -10 and < 0 ? (delta + 10) / 10 * 22
                : delta is >= 0 and < 5 ? -8 * (1 - (delta / 5))
                : 0;
            if (Math.Abs(d) > Math.Abs(best)) best = d;
        }

        return best;
    }

    /// <summary>
    /// The coin box on the right of the booth, with the slot the chip goes into, and its lever: pulled down when the
    /// spin starts, and back up.
    /// </summary>
    private void DrawLever(RouletteShow? show, double t)
    {
        const int left = 257;
        const int right = 271;
        const int top = 149;

        // La caja: violeta como el puesto, remate dorado arriba, la ranura y una flecha encendida que la señala.
        for (var y = top; y < DesignRows; y++)
        {
            for (var x = left; x < right; x++)
            {
                var edge = x == left || x == right - 1;
                var colour = edge ? Outline
                    : y == top ? GoldLight
                    : y == top + 1 ? GoldDark
                    : x <= left + 2 ? Cabinet[1]
                    : x >= right - 3 ? Cabinet[4]
                    : Cabinet[2];
                Put(Canvas, x, y, colour);
            }
        }

        Rect(left - 1, top - 1, right - left + 2, 1, Outline);
        Rect((int)SlotX - 4, (int)SlotY - 1, 8, 3, GoldDark);
        Rect((int)SlotX - 3, (int)SlotY, 6, 1, Void);

        var waiting = show is null || t >= RouletteTimeline.Landed;
        var blink = waiting && (int)(t < 0 ? 0 : t * 2) % 2 == 0;
        foreach (var (dx, dy) in new[] { (0, 0), (-1, -1), (1, -1), (-2, -2), (2, -2) })
        {
            Put(Canvas, (int)SlotX + dx, (int)SlotY + 7 + dy, blink ? BulbWarm : BulbOff);
        }

        const double pivotX = 272.0;
        const double pivotY = 162.0;
        var pull = show is null || t < RouletteTimeline.LeverDown ? 0
            : t < RouletteTimeline.LeverDown + 0.2 ? (t - RouletteTimeline.LeverDown) / 0.2
            : t < RouletteTimeline.LeverUp - 0.2 ? 1
            : t < RouletteTimeline.LeverUp ? 1 - ((t - (RouletteTimeline.LeverUp - 0.2)) / 0.2)
            : 0;

        // Arriba el pomo está a treinta celdas; tirada, la palanca baja hacia el jugador y se acorta.
        var knobX = pivotX + 2 + (8 * pull);
        var knobY = pivotY - 32 + (24 * pull);

        const int steps = 40;
        for (var i = 0; i <= steps; i++)
        {
            var p = i / (double)steps;
            var x = (int)Math.Floor(pivotX + ((knobX - pivotX) * p));
            var y = (int)Math.Floor(pivotY + ((knobY - pivotY) * p));
            Put(Canvas, x - 1, y, Outline);
            Put(Canvas, x, y, ChromeLight);
            Put(Canvas, x + 1, y, ChromeDark);
            Put(Canvas, x + 2, y, Outline);
        }

        Disc(pivotX + 0.5, pivotY, 2.6, (nx, ny, d) => d > 0.7 ? Outline : (nx + ny) < 0 ? ChromeHi : ChromeMid);
        Disc(knobX + 0.5, knobY, 4.4, (nx, ny, d) =>
            d > 0.8 ? Outline
            : ((nx + 0.35) * (nx + 0.35)) + ((ny + 0.4) * (ny + 0.4)) < 0.08 ? White
            : (nx + ny) < -0.2 ? PointerLight
            : (nx + ny) > 0.5 ? PointerDark
            : PointerRed);
    }

    /// <summary>The slot glows when the chip goes in.</summary>
    private void DrawSlotGlow(RouletteShow? show, double t)
    {
        if (show is null || t < RouletteTimeline.ChipIn || t > RouletteTimeline.ChipIn + 0.5) return;

        var since = t - RouletteTimeline.ChipIn;
        Rect((int)SlotX - 3, (int)SlotY, 6, 1, since < 0.15 ? White : GoldLight);
        DrawSparkRing(SlotX, SlotY, 1, since, GoldLight);
    }

    /// <summary>
    /// The chip that pays for the spin, flying from the top of the pile into the slot, spinning as it goes and turning
    /// edge-on at the end so it fits.
    /// </summary>
    private void DrawChipFlight(RouletteShow show, double t)
    {
        if (t is < 0 or >= RouletteTimeline.ChipIn) return;

        var (fromX, fromY) = ChipAt(Math.Max(0, show.OwedBefore - 1));
        fromX += RightShift - WheelShift;
        var p = t / RouletteTimeline.ChipIn;
        var x = fromX + ((SlotX - fromX) * p);
        var y = fromY + ((SlotY - 4 - fromY) * p) - (Math.Sin(p * Math.PI) * 30);
        var squeeze = p > 0.8 ? 0.2 : Math.Max(0.2, Math.Abs(Math.Cos(t * 16)));
        DrawChip(x, y, squeeze);
    }

    // ============================================================================================= the landing

    /// <summary>
    /// What happens when it stops: a ring out of the hub, and then confetti and coins for something that pays, or the
    /// room going red for something that costs.
    /// </summary>
    private void DrawLanding(RouletteShow show, double t)
    {
        var since = t - RouletteTimeline.Stopped;
        if (since < 0) return;

        if (since < 0.7)
        {
            var radius = RHub + (since / 0.7 * 110);
            var colour = since < 0.3 ? GoldHi : GoldLight;
            var steps = (int)(radius * 7);
            for (var i = 0; i < steps; i++)
            {
                if (since > 0.35 && i % 2 == 1) continue;
                var a = i / (double)steps * Math.PI * 2;
                Put(Canvas, (int)Math.Floor(WX + (Math.Cos(a) * radius)), (int)Math.Floor(WY + (Math.Sin(a) * radius)), colour);
            }
        }

        var good = show.WinningIndex < show.Wedges.Count && show.Wedges[show.WinningIndex].Good;
        if (good)
        {
            DrawConfetti(since, show.Seed);
            DrawCoins(since, show.Seed);
        }
        else
        {
            DrawAlarm(since);
        }
    }

    private void DrawConfetti(double s, int seed)
    {
        if (s > 3.2) return;

        var random = new Random(seed ^ 0x1F2E);
        for (var i = 0; i < 80; i++)
        {
            var x0 = WX + ((random.NextDouble() - 0.5) * 300);
            var y0 = -Oy - 4 - (random.NextDouble() * 50);
            var fall = 34 + (random.NextDouble() * 34);
            var phase = random.NextDouble() * Math.PI * 2;
            var delay = random.NextDouble() * 0.6;
            var colour = Confetti[i % Confetti.Length];
            var local = s - delay;
            if (local < 0) continue;

            var x = x0 + (Math.Sin((local * 5) + phase) * 6);
            var y = y0 + (fall * local) + (8 * local * local);
            var turn = (int)((local * 10) + i) % 2 == 0;
            Put(Canvas, (int)x, (int)y, colour);
            Put(Canvas, (int)x + (turn ? 1 : 0), (int)y + (turn ? 0 : 1), Lerp(colour, Void, 0.25));
        }
    }

    private void DrawCoins(double s, int seed)
    {
        if (s > 1.8) return;

        var random = new Random(seed ^ 0x6C01);
        for (var i = 0; i < 14; i++)
        {
            var angle = -Math.PI / 2 + ((random.NextDouble() - 0.5) * 2.4);
            var speed = 60 + (random.NextDouble() * 50);
            var x = WX + (Math.Cos(angle) * speed * s);
            var y = (WY - ROuter + 6) + (Math.Sin(angle) * speed * s) + (90 * s * s);
            var wide = Math.Abs(Math.Cos((s * 14) + i)) > 0.4;

            Rect((int)x - (wide ? 2 : 1), (int)y - 2, wide ? 4 : 2, 4, Gold);
            Put(Canvas, (int)x - (wide ? 1 : 0), (int)y - 1, GoldHi);
            Put(Canvas, (int)x + (wide ? 1 : 0), (int)y + 1, GoldDark);
        }
    }

    /// <summary>The ceiling neon blinking red and the edges of the room going dark red for a moment.</summary>
    private void DrawAlarm(double s)
    {
        if (s > 2.0) return;

        var on = (int)(s * 6) % 2 == 0;
        if (on)
        {
            for (var x = -Ox; x < Width - Ox; x++)
            {
                Put(Canvas, x, -Oy + 2, BadGlow);
                Put(Canvas, x, -Oy + 3, Lerp(BadGlow, Void, 0.3));
            }
        }

        var fade = 1 - (s / 2.0);
        for (var y = -Oy; y < DesignRows; y++)
        {
            for (var x = -Ox; x < Width - Ox; x++)
            {
                var edge = Math.Min(Math.Min(x + Ox, Width - Ox - x), Math.Min(y + Oy, DesignRows - y));
                if (edge > 22) continue;
                if (Bayer[y & 3, x & 3] < (1 - (edge / 22.0)) * fade * 9) Put(Canvas, x, y, Lerp(At(Canvas, x, y), BadGlow, 0.35));
            }
        }

    }

    private void DrawWinnerSparkles(double cx, double cy, double s, int seed)
    {
        var round = (int)(s / 1.4);
        var within = s - (round * 1.4);
        var random = new Random(seed ^ (round * 7919));

        for (var i = 0; i < 5; i++)
        {
            var px = cx + ((random.NextDouble() - 0.5) * 44);
            var py = cy + ((random.NextDouble() - 0.5) * 40);
            var local = within - (i * 0.14);
            if (local is < 0 or > 0.45) continue;
            Star(px, py, local < 0.15 ? 1 : local < 0.3 ? 3 : 2, White, GoldLight);
        }
    }
}
