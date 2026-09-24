using System.Windows.Media;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>One type of what arrives, as the cabin's screen shows it: its name and the colour the games give it.</summary>
public sealed record TradeType(string Name, Color Colour);

/// <summary>One wonder trade as the cabin plays it. Everything in it was decided and written into the save before it exists.</summary>
/// <param name="Given">The icon of what is handed over; null leaves the pad empty.</param>
/// <param name="GivenBall">The ball it lives in, when it is one of the five the scene draws itself; null uses the icon.</param>
/// <param name="GivenBallIcon">The cartridge's own icon of its ball, for every other ball.</param>
/// <param name="Received">The icon of what arrives; null draws only its light.</param>
/// <param name="GivenTotal">Base stat total of what was handed over: the screen counts from it to the new one.</param>
/// <param name="Difference">Percent over what was handed over, rounded as the result card says it.</param>
public sealed record TradeShow(
    RoomSprite? Given,
    CapsuleBall? GivenBall,
    RoomSprite? GivenBallIcon,
    RoomSprite? Received,
    int Generation,
    IReadOnlyList<TradeType> Types,
    int GivenTotal,
    int Total,
    int Difference,
    bool Shiny,
    bool Legendary,
    int Seed);

/// <summary>
/// When each thing happens in a wonder trade, in seconds from the moment it was confirmed.
/// </summary>
/// <remarks>
/// The same for every trade, whatever comes back: a cabin that took longer for a legendary would say so before the
/// screen does.
/// </remarks>
public static class TradeTimeline
{
    /// <summary>The ball pops out on the pad beside the Pokémon, open.</summary>
    public const double BallOut = 0.3;

    public const double BeamStart = 0.42;
    public const double ShrinkStart = 0.62;

    /// <summary>The Pokémon is in and the ball snaps shut.</summary>
    public const double Closed = 0.92;

    public const double HopStart = 1.2;
    public const double AtHatch = 1.8;

    /// <summary>Sucked up the left tube.</summary>
    public const double Up = 1.95;

    /// <summary>Out through the ceiling: the search starts.</summary>
    public const double Gone = 2.4;

    public const double Connected = 3.85;

    /// <summary>The other ball comes down the right tube.</summary>
    public const double Down = 4.25;

    public const double InStation = 4.7;
    public const double Out = 4.85;
    public const double Dropped = 5.3;
    public const double RollEnd = 6.35;
    public const double Settled = 6.8;

    // Lo que dice la pantalla, en el orden que eligió el jugador en el §33: generación, tipos y total.
    public const double Generation = 7.0;
    public const double Types = 7.8;
    public const double SecondType = 8.05;
    public const double Total = 8.8;
    public const double TotalLanded = 9.5;

    public const double Open = 10.3;
    public const double Emerge = 10.55;

    /// <summary>The Pokémon is out and in colour: this is when the screen can say what it is.</summary>
    public const double Revealed = 11.35;
}

/// <summary>
/// The wonder trade as a cabin in the same arcade room as the capsule machine, drawn cell by cell: the Pokémon goes into
/// its ball on the left pad, the ball shoots up a glass tube into the ceiling, the screen searches the world, and another
/// ball comes down the other tube, rolls to the right pad and opens.
/// </summary>
/// <remarks>
/// <para>
/// Asked for on 2026-09-23 after the capsule machine (§171), with the same brief: pixel art, as detailed as it gets
/// (§174). It replaces the link-cable scene of §121, whose three reveals were WPF boxes on top of the picture; here they
/// come out on the cabin's own screen, in its own pixels, and stay there beside the result.
/// </para>
/// <para>
/// Nothing on screen decides anything: the trade is written into the save before the first frame. The ball the given
/// Pokémon goes into is its own — the five the scene draws, or the cartridge's icon for any other — and what arrives
/// comes in a Poké Ball because that is the ball PermaLocke writes.
/// </para>
/// </remarks>
public sealed class TradeMachineScene : PixelScene
{
    public const int DesignWidth = 430;
    public const int DesignRows = 166;

    private const int FloorTop = 118;
    private const int Centre = 215;

    private const int MarqueeLeft = 150;
    private const int MarqueeTop = 12;
    private const int MarqueeWidth = 130;
    private const int MarqueeHeight = 26;
    private const int BodyLeft = 156;
    private const int BodyRight = 274;
    private const int BodyTop = 38;
    private const int BodyBottom = 124;
    private const int BezelLeft = 160;
    private const int BezelTop = 41;
    private const int BezelWidth = 110;
    private const int BezelHeight = 65;
    private const int ScreenLeft = 164;
    private const int ScreenTop = 44;
    private const int ScreenWidth = 102;
    private const int ScreenHeight = 58;

    /// <summary>Columns the two tubes are centred on: what goes out climbs the left one, what comes in drops down the right.</summary>
    private const int InTube = 140;

    private const int OutTube = 290;
    private const int TubeBottom = 99;
    private const int StationTop = 100;
    private const int StationBottom = 130;
    private const double HatchY = 116.5;
    private const double HatchR = 7.0;

    private const double SendX = 58.0;
    private const double ReceiveX = 372.0;
    private const double PadFloor = 156.0;
    private const double PadBallX = SendX + 36;
    private const double PadBallR = 10.0;
    private const double TubeR = 5.3;
    private const double HeroR = 13.0;

    // --------------------------------------------------------------------------------------------- palette
    private static readonly Color[] Skin = Tones(Rgb(0x35, 0x5E, 0xBA));
    private static readonly Color[] Trim = [ChromeHi, ChromeLight, ChromeMid, ChromeDark];
    private static readonly Color LedOff = Rgb(0x14, 0x26, 0x36);
    private static readonly Color LedDim = Rgb(0x1E, 0x4E, 0x5E);
    private static readonly Color LedOn = Rgb(0x8C, 0xF6, 0xFF);
    private static readonly Color LedGlow = Rgb(0x34, 0x9C, 0xB8);
    private static readonly Color LedGreen = Rgb(0x8E, 0xFF, 0x9C);
    private static readonly Color LedGreenGlow = Rgb(0x2E, 0x9A, 0x50);
    private static readonly Color ScreenBack = Rgb(0x05, 0x12, 0x16);
    private static readonly Color ScreenLine = Rgb(0x03, 0x0C, 0x0F);
    private static readonly Color Phosphor = Rgb(0x96, 0xFA, 0xE8);
    private static readonly Color PhosphorMid = Rgb(0x44, 0xB2, 0xA4);
    private static readonly Color PhosphorDim = Rgb(0x16, 0x46, 0x44);
    private static readonly Color Better = Rgb(0x92, 0xF2, 0x7E);
    private static readonly Color Worse = Rgb(0xFF, 0x74, 0x62);
    private static readonly Color RecallRed = Rgb(0xFF, 0x48, 0x3C);
    private static readonly Color RecallLight = Rgb(0xFF, 0xB4, 0xA4);
    private static readonly Color RecallDeep = Rgb(0xD8, 0x26, 0x26);
    private static readonly Color Amber = Rgb(0xFF, 0xA8, 0x2E);
    private static readonly Color Cable = Rgb(0x2C, 0x26, 0x40);
    private static readonly Color CableHi = Rgb(0x52, 0x4A, 0x70);
    private static readonly Color CableDark = Rgb(0x10, 0x0C, 0x18);
    private static readonly Color HoleDark = Rgb(0x05, 0x04, 0x09);

    /// <summary>The arrows on the two lamps: what goes up the left tube and what comes down the right one.</summary>
    private static readonly string[] ArrowUp = ["...#...", "..###..", ".#####.", "#######", "..###..", "..###..", "..###.."];
    private static readonly string[] DoorArrow = ["..#..", ".###.", "#####", ".###.", ".###."];

    private enum Where
    {
        Hidden,
        Floor,
        Hatch,
        Tube,
    }

    /// <summary>Where a ball is and how it looks at one moment.</summary>
    private readonly record struct BallPose(
        Where Where, double X, double Y, double R, double Angle, double Squash = 1, double Open = 0, double Flash = 0,
        double Ground = 0)
    {
        public static BallPose Hidden => new(Where.Hidden, 0, 0, 0, 0);
    }

    private readonly (double X, double Y)[] _sendCable;
    private readonly (double X, double Y)[] _receiveCable;

    public TradeMachineScene(int width, int height = DesignRows)
        : base(width, height, DesignWidth, DesignRows)
    {
        _sendCable = Curve((SendX + 33, 154), (112, 152), (InTube - 12, StationBottom - 2));
        _receiveCable = Curve((OutTube + 12, StationBottom - 2), (324, 152), (ReceiveX - 32, 154));
        PaintBackground();
    }

    /// <summary>
    /// The ball a Pokémon lives in, when it is one of the five the scene draws: Master (1), Ultra (2), Super (3),
    /// Poké (4) and Gloria (16). Any other ball is drawn from the cartridge's own icon instead of passing for one of these.
    /// </summary>
    public static CapsuleBall? BallFor(int ballId) => ballId switch
    {
        1 => CapsuleBall.Master,
        2 => CapsuleBall.Ultra,
        3 => CapsuleBall.Great,
        4 => CapsuleBall.Poke,
        16 => CapsuleBall.Cherish,
        _ => null,
    };

    /// <summary>How far the rays reach for what arrived: more for a bigger base stat total.</summary>
    public static int RayStrength(int total) => total switch
    {
        < 350 => 0,
        < 420 => 1,
        < 500 => 2,
        < 580 => 3,
        _ => 4,
    };

    /// <summary>
    /// Draws one frame.
    /// </summary>
    /// <param name="show">The trade being played, or null for the cabin waiting.</param>
    /// <param name="t">Seconds since the trade was confirmed.</param>
    /// <param name="clock">Seconds since the screen opened, for what moves on its own: bulbs, neon.</param>
    /// <param name="toScreen">False to leave the bitmap alone, for pictures drawn off screen.</param>
    public void Render(TradeShow? show, double t, double clock, bool toScreen = true)
    {
        if (show is null) t = -1;

        BeginFrame();

        DrawNeonSign(clock);
        DrawCable(_sendCable);
        DrawCable(_receiveCable);
        DrawPad(SendX);
        DrawPad(ReceiveX);

        DrawTube(InTube);
        DrawTube(OutTube);
        DrawStation(InTube, Door(t, TradeTimeline.HopStart + 0.3, TradeTimeline.Up + 0.08), t is >= TradeTimeline.AtHatch and < TradeTimeline.Gone);
        DrawStation(OutTube, Door(t, TradeTimeline.InStation - 0.05, TradeTimeline.Out + 0.35), t is >= TradeTimeline.InStation and < TradeTimeline.Dropped);
        DrawMachine();

        var given = show is null ? BallPose.Hidden : GivenAt(t);
        var received = show is null ? BallPose.Hidden : ReceivedAt(t, show);

        if (show is not null)
        {
            DrawTubeTraffic(show, given, received, t);
        }

        DrawTubeFront(InTube);
        DrawTubeFront(OutTube);

        if (show is not null)
        {
            DrawSend(show, given, t);

            if (received.Where == Where.Floor)
            {
                var height = received.Ground - (received.Y + received.R);
                Ellipse(received.X, received.Ground - 0.5, Math.Max(3, (received.R * 1.5) - (height * 0.25)), 1.8, Shadow, dither: height > 4);
            }
        }

        var dim = show is null ? 0 : Math.Clamp((t - TradeTimeline.Open) / 0.4, 0, 1) * 0.58;
        if (dim > 0) Darken(dim);

        DrawLights(show, t, clock);

        if (show is not null)
        {
            DrawArrival(show, received, t);
        }

        if (toScreen)
        {
            Present();
        }
    }

    // ============================================================================================= background

    private void PaintBackground()
    {
        PaintRoom(FloorTop, DesignRows, Centre, Centre);

        // Las sombras de la cabina y de las dos estaciones en el suelo, duras y tramadas en el borde.
        for (var y = 126; y < 138; y++)
        {
            for (var x = 108; x < 324; x++)
            {
                var d = Math.Pow((x + 0.5 - Centre) / 104.0, 2) + Math.Pow((y + 0.5 - 131.0) / 5.0, 2);
                if (d < 1 && (d < 0.7 || Bayer[y & 3, x & 3] < 8)) Put(Back, x, y, Shadow);
            }
        }
    }

    // ============================================================================================= the room's props

    /// <summary>
    /// A neon trade sign on the left wall, over the pad where the Pokémon waits: two arrows chasing each other round a
    /// small globe. It flickers now and then, as neon does, and it is what the result card covers at the end.
    /// </summary>
    private void DrawNeonSign(double clock)
    {
        const double cx = 58.0;
        const double cy = 42.0;
        const double r = 17.0;

        var cycle = clock % 7.7;
        var off = cycle is > 5.1 and < 5.2 or > 5.3 and < 5.36;
        var core = off ? NeonGlow : Neon;
        var tube = off ? NeonHalo : NeonMid;

        bool OnArc(double angle) => angle is > 200 and < 338 || angle is > 20 and < 158;

        for (var y = (int)(cy - r - 5); y <= (int)(cy + r + 5); y++)
        {
            for (var x = (int)(cx - r - 5); x <= (int)(cx + r + 5); x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;

                // El globo de dentro: su aro, el ecuador y un meridiano.
                var globe = Math.Abs(d - 7.0) < 0.7
                            || (Math.Abs(dy) < 0.6 && d < 7)
                            || (Math.Abs(Math.Sqrt((dx * dx / 9.0) + (dy * dy / 49.0)) - 1) < 0.12 && d < 7.2);
                var arc = Math.Abs(d - r) < 0.75 && OnArc(angle);

                if (arc || globe) Put(Canvas, x, y, dy > 0 ? tube : core);
                else if (!off && ((Math.Abs(d - r) < 3 && OnArc(angle)) || Math.Abs(d - 7.0) < 2.4) && Bayer[y & 3, x & 3] < 5)
                {
                    Put(Canvas, x, y, NeonGlow);
                }
            }
        }

        // Las dos puntas de flecha, en el sentido del giro.
        foreach (var (at, core2) in new[] { (338.0, tube), (158.0, core) })
        {
            var a = at * Math.PI / 180;
            var tipX = cx + (Math.Cos(a) * r);
            var tipY = cy + (Math.Sin(a) * r);
            var along = (X: -Math.Sin(a), Y: Math.Cos(a));
            for (var i = 1; i <= 5; i++)
            {
                foreach (var side in new[] { -1.0, 1.0 })
                {
                    var bx = tipX - (along.X * i) + (side * along.Y * i * 0.9);
                    var by = tipY - (along.Y * i) - (side * along.X * i * 0.9);
                    Put(Canvas, (int)Math.Floor(bx), (int)Math.Floor(by), off ? NeonGlow : core2);
                }
            }
        }
    }

    private static (double X, double Y)[] Curve((double X, double Y) a, (double X, double Y) control, (double X, double Y) b)
    {
        var points = new (double X, double Y)[90];
        for (var i = 0; i < points.Length; i++)
        {
            var p = i / (double)(points.Length - 1);
            var u = 1 - p;
            points[i] = ((u * u * a.X) + (2 * u * p * control.X) + (p * p * b.X), (u * u * a.Y) + (2 * u * p * control.Y) + (p * p * b.Y));
        }

        return points;
    }

    /// <summary>A link cable on the floor between a pad and its station: the nod to the trades of the first games (§121).</summary>
    private void DrawCable((double X, double Y)[] points)
    {
        foreach (var (px, py) in points)
        {
            var x = (int)Math.Floor(px);
            var y = (int)Math.Floor(py);
            Put(Canvas, x, y - 1, Outline);
            Put(Canvas, x, y, (x & 1) == 0 ? CableHi : Cable);
            Put(Canvas, x, y + 1, CableDark);
            Put(Canvas, x, y + 2, Outline);
        }

        // Las clavijas de las dos puntas.
        foreach (var (px, py) in new[] { points[0], points[^1] })
        {
            var x = (int)Math.Floor(px) - 2;
            var y = (int)Math.Floor(py) - 1;
            Rect(x, y, 5, 3, ChromeMid);
            Rect(x, y, 5, 1, ChromeLight);
            Border(x - 1, y - 1, 7, 5, Outline);
        }
    }

    /// <summary>A round platform on the floor: a metal rim, a ring that lights, and a plate in the middle.</summary>
    private void DrawPad(double cx)
    {
        const double rx = 32;
        const double ry = 7;
        var cy = PadFloor - 1;

        // El canto de la plataforma, debajo del óvalo.
        for (var x = (int)(cx - rx); x <= (int)(cx + rx); x++)
        {
            var dx = (x + 0.5 - cx) / rx;
            if (Math.Abs(dx) >= 1) continue;
            var bottom = (int)Math.Floor(cy + (ry * Math.Sqrt(1 - (dx * dx))));
            Put(Canvas, x, bottom + 1, dx < -0.4 ? ChromeMid : ChromeDark);
            Put(Canvas, x, bottom + 2, ChromeDeep);
            Put(Canvas, x, bottom + 3, Outline);
        }

        for (var y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
        {
            for (var x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                var dx = (x + 0.5 - cx) / rx;
                var dy = (y + 0.5 - cy) / ry;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 1) continue;

                var colour = d > 0.93 ? Outline
                    : d > 0.8 ? (dy < 0 ? (dx < 0 ? ChromeHi : ChromeLight) : ChromeMid)
                    : d > 0.66 ? LedOff
                    : d > 0.6 ? ChromeDeep
                    : ((x / 3) + (y / 2)) % 2 == 0 ? PlateLight : Plate;
                if (colour == Plate && dx < -0.2 && dy < 0 && Bayer[y & 3, x & 3] < 6) colour = PlateLight;
                Put(Canvas, x, y, colour);
            }
        }
    }

    /// <summary>The lit ring of a pad, drawn over it after the room dims.</summary>
    private void DrawPadRing(double cx, Color ring, double strength)
    {
        const double rx = 32;
        const double ry = 7;
        var cy = PadFloor - 1;
        var dimmed = Lerp(ring, Void, 0.5);

        for (var y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
        {
            for (var x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                var dx = (x + 0.5 - cx) / rx;
                var dy = (y + 0.5 - cy) / ry;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d is <= 0.66 or > 0.8) continue;

                var lit = Bayer[y & 3, x & 3] < strength * 16;
                Put(Canvas, x, y, lit ? (d < 0.73 ? ring : Lerp(ring, White, 0.35)) : dimmed);
            }
        }
    }

    // ============================================================================================= tubes and stations

    private bool InsideTube(int c, int x, int y) => x >= c - 6 && x <= c + 5 && y <= TubeBottom && y >= -Oy + 4;

    /// <summary>
    /// A pneumatic glass tube from a station up through the ceiling. The glass lets the wall show through, darkened, and
    /// has chrome rings along it; the LEDs beside it are drawn with the lights.
    /// </summary>
    private void DrawTube(int c)
    {
        var top = -Oy;

        for (var y = top; y <= TubeBottom; y++)
        {
            Put(Canvas, c - 8, y, Outline);
            Put(Canvas, c + 7, y, Outline);
            Put(Canvas, c - 7, y, Lerp(GlassRim, GlassShine, 0.35));
            Put(Canvas, c + 6, y, GlassRim);

            // Cristal: la pared se ve a través, teñida de azul, más clara a la izquierda y más honda a la derecha.
            for (var x = c - 6; x <= c + 5; x++)
            {
                var glass = x >= c + 3 ? GlassDeep : x <= c - 3 ? GlassRim : GlassLight;
                Put(Canvas, x, y, Lerp(At(Back, x, y), glass, x >= c + 3 ? 0.55 : 0.4));
            }

            if ((y & 3) == 0) Put(Canvas, c + 4, y, Lerp(GlassRim, GlassShine, 0.2));

            // El carril de los LED, pegado por fuera a cada tubo.
            var rail = c < Centre ? c - 12 : c + 8;
            Put(Canvas, rail, y, Outline);
            Put(Canvas, rail + 1, y, ChromeDeep);
            Put(Canvas, rail + 2, y, ChromeDeep);
            Put(Canvas, rail + 3, y, Outline);
        }

        foreach (var ring in TubeRings())
        {
            Rect(c - 9, ring - 1, 18, 1, Outline);
            Rect(c - 9, ring, 18, 1, ChromeLight);
            Rect(c - 9, ring + 1, 18, 1, ChromeMid);
            Rect(c - 9, ring + 2, 18, 1, ChromeDark);
            Rect(c - 9, ring + 3, 18, 1, Outline);
            Rect(c - 7, ring, 3, 1, ChromeHi);
            Put(Canvas, c - 10, ring + 1, Outline);
            Put(Canvas, c + 9, ring + 1, Outline);
        }

        // La brida del techo, donde el tubo se mete.
        Rect(c - 11, top, 22, 1, ChromeDark);
        Rect(c - 11, top + 1, 22, 1, ChromeMid);
        Rect(c - 11, top + 2, 22, 1, ChromeLight);
        Rect(c - 11, top + 3, 22, 1, Outline);
    }

    private IEnumerable<int> TubeRings()
    {
        for (var ring = TubeBottom - 14; ring > -Oy + 8; ring -= 36) yield return ring;
    }

    /// <summary>The glass in front of whatever travels inside: a thin shine down the left, over the ball too.</summary>
    private void DrawTubeFront(int c)
    {
        var rings = TubeRings().ToHashSet();
        for (var y = -Oy + 4; y <= TubeBottom; y++)
        {
            if (rings.Contains(y) || rings.Contains(y - 1) || rings.Contains(y - 2) || rings.Contains(y + 1)) continue;
            Put(Canvas, c - 5, y, Lerp(At(Canvas, c - 5, y), GlassShine, 0.7));
            Put(Canvas, c - 4, y, Lerp(At(Canvas, c - 4, y), GlassShine, 0.25));
            if (((y + c) & 7) == 0) Put(Canvas, c - 3, y, Lerp(At(Canvas, c - 3, y), GlassShine, 0.35));
        }
    }

    /// <summary>How open a hatch door is: it slides open before the ball and shut after it.</summary>
    private static double Door(double t, double opens, double closes)
    {
        if (t < opens - 0.12 || t > closes + 0.12) return 0;
        if (t < opens) return (t - (opens - 0.12)) / 0.12;
        if (t > closes) return 1 - ((t - closes) / 0.12);
        return 1;
    }

    /// <summary>
    /// The station at the foot of a tube: a box on the floor with a round hatch, whose door slides aside for the ball,
    /// and a light inside while it pulls or pushes.
    /// </summary>
    private void DrawStation(int c, double door, bool active)
    {
        var left = c - 16;
        var right = c + 15;

        for (var y = StationTop; y < StationBottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var edge = x == left || x == right || y == StationTop || y == StationBottom - 1;
                var colour = edge ? Outline
                    : y == StationTop + 1 ? Skin[0]
                    : x <= left + 2 ? Skin[1]
                    : x >= right - 3 ? Skin[4]
                    : x >= right - 6 ? Skin[3]
                    : y >= StationBottom - 4 ? Skin[4]
                    : Skin[2];
                if (colour == Skin[2] && x <= left + 6 && Bayer[y & 3, x & 3] < 8) colour = Skin[1];
                Put(Canvas, x, y, colour);
            }
        }

        // El collarín donde entra el tubo.
        Rect(c - 10, StationTop - 3, 20, 1, Outline);
        Rect(c - 10, StationTop - 2, 20, 1, ChromeLight);
        Rect(c - 10, StationTop - 1, 20, 1, ChromeMid);
        Rect(c - 10, StationTop, 20, 1, ChromeDark);
        Put(Canvas, c - 11, StationTop - 1, Outline);
        Put(Canvas, c + 10, StationTop - 1, Outline);

        foreach (var (rx, ry) in new[] { (left + 2, StationTop + 3), (right - 3, StationTop + 3), (left + 2, StationBottom - 5), (right - 3, StationBottom - 5) })
        {
            Put(Canvas, rx, ry, Skin[0]);
            Put(Canvas, rx + 1, ry + 1, Skin[4]);
        }

        // La trampilla: aro de cromo, agujero y puerta corredera.
        var inside = active ? Lerp(HoleDark, LedGlow, 0.35) : HoleDark;
        Disc(c, HatchY, HatchR + 2, (nx, ny, d) =>
        {
            if (d > 0.9) return Outline;
            if (d > 0.78) return (nx + ny) < -0.3 ? ChromeHi : (nx + ny) > 0.5 ? ChromeDark : ChromeLight;
            return ChromeMid;
        });

        var edgeX = c - HatchR + (2 * HatchR * door);
        for (var y = (int)Math.Floor(HatchY - HatchR); y <= (int)Math.Ceiling(HatchY + HatchR); y++)
        {
            for (var x = (int)Math.Floor(c - HatchR); x <= (int)Math.Ceiling(c + HatchR); x++)
            {
                var dx = (x + 0.5 - c) / HatchR;
                var dy = (y + 0.5 - HatchY) / HatchR;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 1) continue;

                if (x + 0.5 < edgeX)
                {
                    // El agujero, con su borde de sombra arriba: se ve hondo.
                    Put(Canvas, x, y, dy < -0.7 ? Outline : inside);
                    continue;
                }

                // La puerta, con una raya en medio y la flecha del sentido en que va la ball.
                var colour = Math.Abs(dy) < 0.12 ? ChromeDark : dy < -0.6 ? ChromeLight : ChromeMid;
                var ax = x - (c - 2);
                var ay = y - (int)Math.Floor(HatchY - 2);
                if (ax is >= 0 and < 5 && ay is >= 0 and < 5 && DoorArrow[c < Centre ? ay : 4 - ay][ax] == '#') colour = ChromeDark;
                if (Math.Abs(x + 0.5 - edgeX) < 1 && door > 0) colour = Outline;
                Put(Canvas, x, y, colour);
            }
        }
    }

    /// <summary>What travels inside the glass: the ball going up the left tube and the one coming down the right.</summary>
    private void DrawTubeTraffic(TradeShow show, BallPose given, BallPose received, double t)
    {
        if (given.Where == Where.Tube)
        {
            var speed = Math.Clamp((t - TradeTimeline.Up) / (TradeTimeline.Gone - TradeTimeline.Up), 0, 1);
            DrawTubeGlow(InTube, given.Y);
            DrawStreaks(InTube, given, direction: 1, speed);
            DrawGivenBall(show, given, (x, y) => InsideTube(InTube, x, y), Glass);
        }

        if (received.Where == Where.Tube)
        {
            var q = Math.Clamp((t - TradeTimeline.Down) / (TradeTimeline.InStation - TradeTimeline.Down), 0, 1);
            DrawTubeGlow(OutTube, received.Y);
            DrawStreaks(OutTube, received, direction: -1, 1 - q);
            DrawBall(received.X, received.Y, received.R, received.Angle, CapsuleBall.Poke, BallLook.Plain,
                (x, y) => InsideTube(OutTube, x, y), Glass);
        }
    }

    /// <summary>The glass lighting up around a ball as it passes, tramado so it fades out above and below.</summary>
    private void DrawTubeGlow(int c, double y)
    {
        for (var yy = (int)(y - 18); yy <= (int)(y + 18); yy++)
        {
            for (var x = c - 6; x <= c + 5; x++)
            {
                if (!InsideTube(c, x, yy)) continue;
                var d = Math.Abs(yy + 0.5 - y) / 18;
                if (Bayer[yy & 3, x & 3] < (1 - d) * 10) Put(Canvas, x, yy, Lerp(At(Canvas, x, yy), LedGlow, 0.45));
            }
        }
    }

    /// <summary>Lines of air left behind a ball going fast through a tube.</summary>
    /// <param name="direction">1 when the streaks trail below (the ball climbs), -1 above (it drops).</param>
    private void DrawStreaks(int c, BallPose ball, int direction, double speed)
    {
        if (speed < 0.2) return;

        var length = (int)(4 + (speed * 12));
        foreach (var (offset, extra) in new[] { (-3, 0), (0, 3), (3, 1) })
        {
            for (var j = 1; j <= length + extra; j++)
            {
                var x = c + offset;
                var y = (int)Math.Floor(ball.Y + (direction * (ball.R + j)));
                if (!InsideTube(c, x, y)) continue;
                var fade = j / (double)(length + extra);
                if (Bayer[y & 3, x & 3] < (1 - fade) * 16) Put(Canvas, x, y, fade < 0.4 ? White : GlassShine);
            }
        }
    }

    // ============================================================================================= the cabin

    private static Color[] Tones(Color basis) =>
        [Lerp(basis, White, 0.45), Lerp(basis, White, 0.18), basis, Lerp(basis, Void, 0.32), Lerp(basis, Void, 0.58)];

    private void DrawMachine()
    {
        DrawBody();
        DrawBrackets();
        DrawMarqueePlate();
        DrawBeaconHousing();
        DrawBezel();
        DrawLowerPanel();
        DrawBase();
    }

    private void DrawBody()
    {
        for (var y = BodyTop; y < BodyBottom; y++)
        {
            for (var x = BodyLeft; x < BodyRight; x++)
            {
                var edge = x == BodyLeft || x == BodyRight - 1;
                var colour = edge ? Outline
                    : x <= BodyLeft + 1 ? Skin[0]
                    : x <= BodyLeft + 3 ? Skin[1]
                    : x >= BodyRight - 3 ? Skin[4]
                    : x >= BodyRight - 6 ? Skin[3]
                    : Skin[2];

                // Brillo vertical a la izquierda, tramado: la luz viene de arriba a la izquierda, como en la otra máquina.
                if (colour == Skin[2] && x <= BodyLeft + 8 && Bayer[y & 3, x & 3] < 8) colour = Skin[1];
                if (colour == Skin[2] && x >= BodyRight - 11 && Bayer[y & 3, x & 3] < 6) colour = Skin[3];
                Put(Canvas, x, y, colour);
            }
        }

        // La sombra del cartel sobre el cuerpo.
        Rect(BodyLeft + 1, BodyTop, BodyRight - BodyLeft - 2, 1, Skin[4]);
        for (var x = BodyLeft + 1; x < BodyRight - 1; x++)
        {
            if (Bayer[1, x & 3] < 8) Put(Canvas, x, BodyTop + 1, Skin[3]);
        }
    }

    /// <summary>The clamps that hold each tube to the cabin, at the height of two of its rings.</summary>
    private void DrawBrackets()
    {
        foreach (var ring in TubeRings().Where(r => r is > BodyTop + 6 and < BodyBottom - 4))
        {
            foreach (var (from, to) in new[] { (InTube + 8, BodyLeft), (BodyRight, OutTube - 8) })
            {
                Rect(from, ring - 1, to - from, 1, Outline);
                Rect(from, ring, to - from, 1, ChromeLight);
                Rect(from, ring + 1, to - from, 1, ChromeMid);
                Rect(from, ring + 2, to - from, 1, ChromeDark);
                Rect(from, ring + 3, to - from, 1, Outline);
            }
        }
    }

    private void DrawMarqueePlate()
    {
        const int left = MarqueeLeft;
        const int top = MarqueeTop;
        const int width = MarqueeWidth;
        const int height = MarqueeHeight;

        Rect(left, top, width, height, Plate);
        Rect(left + 1, top + 1, width - 2, 2, Trim[1]);
        Rect(left + 1, top + height - 3, width - 2, 2, Trim[3]);
        Rect(left + 1, top + 1, 2, height - 2, Trim[1]);
        Rect(left + width - 3, top + 1, 2, height - 2, Trim[3]);
        Rect(left + 3, top + 3, width - 6, 1, PlateLight);
        Rect(left + 8, top + 1, 14, 1, Trim[0]);
        Border(left, top, width, height, Outline);

        // Las dos patas que lo apoyan en el cuerpo.
        foreach (var x in new[] { BodyLeft + 6, BodyRight - 12 })
        {
            Rect(x, top + height, 6, BodyTop - top - height, Trim[2]);
            Put(Canvas, x - 1, top + height, Outline);
            Put(Canvas, x + 6, top + height, Outline);
        }
    }

    private void DrawBeaconHousing()
    {
        Rect(Centre - 7, MarqueeTop - 4, 14, 1, Outline);
        Rect(Centre - 7, MarqueeTop - 3, 14, 1, Trim[1]);
        Rect(Centre - 7, MarqueeTop - 2, 14, 1, Trim[2]);
        Rect(Centre - 7, MarqueeTop - 1, 14, 1, Trim[3]);
        Put(Canvas, Centre - 8, MarqueeTop - 2, Outline);
        Put(Canvas, Centre + 7, MarqueeTop - 2, Outline);
    }

    private void DrawBezel()
    {
        Rect(BezelLeft, BezelTop, BezelWidth, BezelHeight, Trim[2]);
        Rect(BezelLeft + 1, BezelTop + 1, BezelWidth - 2, 1, Trim[0]);
        Rect(BezelLeft + 1, BezelTop + 1, 1, BezelHeight - 2, Trim[1]);
        Rect(BezelLeft + 1, BezelTop + BezelHeight - 2, BezelWidth - 2, 1, Trim[3]);
        Rect(BezelLeft + BezelWidth - 2, BezelTop + 1, 1, BezelHeight - 2, Trim[3]);
        Border(BezelLeft - 1, BezelTop - 1, BezelWidth + 2, BezelHeight + 2, Outline);

        foreach (var (sx, sy) in new[]
                 {
                     (BezelLeft + 2, BezelTop + 2), (BezelLeft + BezelWidth - 3, BezelTop + 2),
                     (BezelLeft + 2, BezelTop + BezelHeight - 3), (BezelLeft + BezelWidth - 3, BezelTop + BezelHeight - 3)
                 })
        {
            Put(Canvas, sx, sy, Trim[3]);
        }

        // La pantalla apagada: líneas de barrido y una sombra en el borde de arriba.
        for (var y = ScreenTop; y < ScreenTop + ScreenHeight; y++)
        {
            for (var x = ScreenLeft; x < ScreenLeft + ScreenWidth; x++)
            {
                var corner = (x == ScreenLeft || x == ScreenLeft + ScreenWidth - 1) && (y == ScreenTop || y == ScreenTop + ScreenHeight - 1);
                Put(Canvas, x, y, corner ? Trim[3] : (y & 1) == 0 ? ScreenBack : ScreenLine);
            }
        }

        Rect(ScreenLeft + 1, ScreenTop, ScreenWidth - 2, 1, Outline);
        Border(ScreenLeft - 1, ScreenTop - 1, ScreenWidth + 2, ScreenHeight + 2, Outline);
    }

    private void DrawLowerPanel()
    {
        // Dos rejillas de altavoz.
        foreach (var gx in new[] { 165, 244 })
        {
            for (var i = 0; i < 6; i++)
            {
                var y = 110 + (i * 2);
                Rect(gx + 1, y, 20, 1, Skin[4]);
                Rect(gx + 1, y + 1, 20, 1, Skin[1]);
            }

            Border(gx - 1, 108, 24, 15, Outline);
        }

        // Las casillas de los dos pilotos, a los lados del emblema.
        foreach (var lx in new[] { 193, 228 })
        {
            Rect(lx, 109, 11, 11, Plate);
            Frame(lx, 109, 11, 11, Trim[1], Trim[3]);
        }

        Disc(Centre, 114.5, 6.8, (_, _, d) => d > 0.86 ? Outline : White);
        DrawBall(Centre, 114.5, 5.3, -0.35, CapsuleBall.Poke, BallLook.Plain);
    }

    private void DrawBase()
    {
        const int left = BodyLeft - 4;
        const int right = BodyRight + 4;
        Rect(left, BodyBottom, right - left, 1, Trim[1]);
        Rect(left, BodyBottom + 1, right - left, 1, Trim[3]);
        Rect(left, BodyBottom + 2, right - left, 3, Skin[4]);
        Rect(left, BodyBottom + 5, right - left, 1, Outline);
        Rect(left, BodyBottom, 1, 6, Outline);
        Rect(right - 1, BodyBottom, 1, 6, Outline);
        Rect(left + 6, BodyBottom, 20, 1, Trim[0]);
    }

    // ============================================================================================= the lights

    /// <summary>
    /// Everything that gives light, drawn after the room dims so it keeps shining: bulbs, the beacon, the LEDs along the
    /// tubes, the two lamps, the screen, the lit rings of the pads and the pulses along the cables.
    /// </summary>
    private void DrawLights(TradeShow? show, double t, double clock)
    {
        var playing = show is not null;

        DrawMarqueeLights(t, clock, playing);
        DrawBeacon(t, playing);
        DrawLeds(InTube, t, clock, playing);
        DrawLeds(OutTube, t, clock, playing);
        DrawLamp(193, up: true, playing && t is >= TradeTimeline.Closed and < TradeTimeline.Gone && ((int)(t * 8) % 2 == 0));
        DrawLamp(228, up: false, playing && t is >= TradeTimeline.Down - 0.1 and < TradeTimeline.Dropped && ((int)(t * 8) % 2 == 0));
        DrawScreen(show, t, clock);
        DrawScreenGlare();

        // El anillo de la plataforma de envío se enciende con el Pokémon encima y se apaga cuando se va.
        var send = !playing ? 0.35 + (0.2 * Math.Sin(clock * 2.4))
            : t < TradeTimeline.Closed ? 0.7 + (0.3 * Math.Sin(t * 9))
            : Math.Max(0.15, 0.7 - ((t - TradeTimeline.Closed) * 1.4));
        DrawPadRing(SendX, LedGlow, send);

        // El de recepción espera tenue y toma el color del tipo al abrirse la ball.
        if (show is not null && t >= TradeTimeline.Open)
        {
            var colour = show.Types.Count > 0 ? show.Types[0].Colour : LedGlow;
            DrawPadRing(ReceiveX, colour, 0.75 + (0.25 * Math.Sin((t - TradeTimeline.Open) * 5)));
        }
        else
        {
            var waiting = playing && t is >= TradeTimeline.Out and < TradeTimeline.Settled ? 0.8 : 0.25;
            DrawPadRing(ReceiveX, LedGlow, waiting);
        }

        if (playing)
        {
            DrawCablePulses(_sendCable, t, TradeTimeline.Closed, TradeTimeline.Up);
            DrawCablePulses(_receiveCable, t, TradeTimeline.Out, TradeTimeline.Settled);
        }
    }

    private void DrawCablePulses((double X, double Y)[] points, double t, double from, double to)
    {
        if (t < from || t > to) return;

        for (var k = 0; k < 3; k++)
        {
            var p = (((t - from) * 1.8) - (k * 0.33)) % 1.0;
            if (p < 0) continue;
            var (px, py) = points[(int)(p * (points.Length - 1))];
            var x = (int)Math.Floor(px);
            var y = (int)Math.Floor(py);
            Put(Canvas, x, y, White);
            Put(Canvas, x + 1, y, LedOn);
            Put(Canvas, x - 1, y, LedOn);
            Put(Canvas, x, y + 1, LedGlow);
        }
    }

    private void DrawMarqueeLights(double t, double clock, bool playing)
    {
        const int left = MarqueeLeft;
        const int top = MarqueeTop;
        const int width = MarqueeWidth;
        const int height = MarqueeHeight;

        var bulbs = new List<(int X, int Y)>();
        for (var x = left + 4; x < left + width - 4; x += 5) bulbs.Add((x, top + 1));
        for (var y = top + 6; y < top + height - 5; y += 5) bulbs.Add((left + width - 3, y));
        for (var x = left + width - 6; x > left + 2; x -= 5) bulbs.Add((x, top + height - 3));
        for (var y = top + height - 8; y > top + 2; y -= 5) bulbs.Add((left + 1, y));

        var celebrating = playing && t is >= TradeTimeline.Revealed and < TradeTimeline.Revealed + 1.3;
        var racing = playing && t is >= TradeTimeline.HopStart and < TradeTimeline.Settled;
        var phase = racing ? (int)(t * 14) : (int)(clock * 3);

        for (var i = 0; i < bulbs.Count; i++)
        {
            var on = celebrating ? ((int)((t - TradeTimeline.Revealed) * 7) % 2 == 0) : (i + phase) % 3 == 0;
            var (bx, by) = bulbs[i];
            Rect(bx, by, 2, 2, on ? BulbWarm : BulbOff);
            if (on) Put(Canvas, bx, by, BulbOn);
        }

        // El texto con relleno de dos tonos, de bombilla arriba y ámbar abajo, y su sombra dura.
        var flash = playing && t is >= TradeTimeline.Connected and < TradeTimeline.Connected + 0.25;
        foreach (var (line, y) in new[] { ("INTERCAMBIO", top + 5), ("PRODIGIOSO", top + 14) })
        {
            var x = Centre - (BigWidth(line) / 2);
            BigText(line, x + 1, y + 1, Outline);
            BigText(line, x, y, flash || celebrating ? White : BulbOn);

            if (!flash && !celebrating)
            {
                for (var yy = y + 4; yy < y + 7; yy++)
                {
                    for (var xx = x; xx < x + BigWidth(line); xx++)
                    {
                        if (At(Canvas, xx, yy) == BulbOn) Put(Canvas, xx, yy, BulbWarm);
                    }
                }
            }
        }
    }

    /// <summary>A beacon on top of the cabin: dark while it waits, turning amber while it searches, green when it finds.</summary>
    private void DrawBeacon(double t, bool playing)
    {
        const double cx = Centre;
        const double cy = MarqueeTop - 5;
        const double r = 5.0;

        var searching = playing && t is >= TradeTimeline.Gone and < TradeTimeline.Connected;
        var found = playing && t is >= TradeTimeline.Connected and < TradeTimeline.Connected + 0.6;
        var basis = found ? LedGreen : Amber;
        var band = cx + (Math.Sin(t * 9) * r);

        if (searching || found)
        {
            // Su luz en la pared, cuando el haz mira hacia delante.
            var facing = found || Math.Cos(t * 9) > 0.2;
            if (facing)
            {
                for (var y = (int)(cy - 12); y <= (int)(cy + 6); y++)
                {
                    for (var x = (int)(cx - 14); x <= (int)(cx + 14); x++)
                    {
                        var d = Math.Sqrt(Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2));
                        if (d is > 6 and < 13 && Bayer[y & 3, x & 3] < (13 - d) / 7 * 5) Put(Canvas, x, y, Lerp(basis, Void, 0.45));
                    }
                }
            }
        }

        for (var y = (int)(cy - r); y <= (int)cy + 1; y++)
        {
            for (var x = (int)(cx - r); x <= (int)(cx + r); x++)
            {
                var dx = (x + 0.5 - cx) / r;
                var dy = (y + 0.5 - cy) / r;
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > 1 && y < cy) continue;
                if (Math.Abs(dx) > 1) continue;

                Color colour;
                if (d > 0.84 && y < cy) colour = Outline;
                else if (!searching && !found) colour = dx < -0.3 && dy < -0.3 ? Lerp(Amber, Void, 0.35) : Lerp(Amber, Void, 0.68);
                else if (found) colour = dx < -0.3 && dy < -0.3 ? White : basis;
                else colour = Math.Abs(x + 0.5 - band) < 1.6 ? BulbOn : basis;
                Put(Canvas, x, y, colour);
            }
        }
    }

    /// <summary>
    /// The LEDs along a tube: chasing up the left while a ball leaves, round both while it searches, green when it finds,
    /// chasing down the right while the other one comes.
    /// </summary>
    private void DrawLeds(int c, double t, double clock, bool playing)
    {
        var x = c < Centre ? c - 11 : c + 9;
        var leds = new List<int>();
        for (var y = TubeBottom - 3; y > -Oy + 6; y -= 6) leds.Add(y);

        var isIn = c < Centre;
        for (var i = 0; i < leds.Count; i++)
        {
            Color colour;

            if (!playing)
            {
                colour = (i + (int)(clock * 2)) % 9 == 0 ? LedDim : LedOff;
            }
            else if (t is >= TradeTimeline.Connected and < TradeTimeline.Connected + 0.45)
            {
                colour = (int)((t - TradeTimeline.Connected) / 0.075) % 2 == 0 ? LedGreen : LedGreenGlow;
            }
            else if (isIn && t is >= TradeTimeline.Closed and < TradeTimeline.Gone + 0.15)
            {
                colour = ((i - (int)(t * 26)) % 4 + 4) % 4 == 0 ? LedOn : LedDim;
            }
            else if (!isIn && t is >= TradeTimeline.Down - 0.1 and < TradeTimeline.InStation + 0.1)
            {
                colour = ((i + (int)(t * 26)) % 4 + 4) % 4 == 0 ? LedOn : LedDim;
            }
            else if (t is >= TradeTimeline.Gone and < TradeTimeline.Connected)
            {
                var phase = (int)(t * 12);
                colour = (isIn ? ((i - phase) % 5 + 5) % 5 : ((i + phase) % 5 + 5) % 5) == 0 ? LedOn : LedOff;
            }
            else
            {
                colour = t >= TradeTimeline.Settled ? LedDim : LedOff;
            }

            Rect(x, leds[i], 2, 2, colour);
            if (colour == LedOn || colour == LedGreen) Put(Canvas, x, leds[i], White);
        }
    }

    private void DrawLamp(int left, bool up, bool lit)
    {
        var colour = lit ? LedOn : Lerp(LedOff, Plate, 0.3);
        for (var row = 0; row < 7; row++)
        {
            var line = ArrowUp[up ? row : 6 - row];
            for (var col = 0; col < 7; col++)
            {
                if (line[col] == '#') Put(Canvas, left + 2 + col, 111 + row, colour);
            }
        }

        if (lit) Put(Canvas, left + 5, up ? 111 : 117, White);
    }

    // ============================================================================================= the screen

    private const int ScreenMid = ScreenLeft + (ScreenWidth / 2);

    private void PutScreen(int x, int y, Color colour)
    {
        if (x >= ScreenLeft && x < ScreenLeft + ScreenWidth && y > ScreenTop && y < ScreenTop + ScreenHeight - 1) Put(Canvas, x, y, colour);
    }

    private void CentreText(string text, int top, Color colour, int scale = 1) =>
        BigText(text, ScreenMid - (BigWidth(text, scale) / 2), top, colour, scale);

    /// <summary>
    /// What the cabin's screen says: ready, sending, the world turning while it searches, found, receiving, and then the
    /// three things the player is told before the Pokémon comes out — its generation, its types and its total, counted up
    /// from the one handed over.
    /// </summary>
    private void DrawScreen(TradeShow? show, double t, double clock)
    {
        if (show is null)
        {
            CentreText("LISTO", ScreenTop + 25, Phosphor);
            if ((int)(clock * 2) % 2 == 0) Rect(ScreenMid + 17, ScreenTop + 25, 5, 7, PhosphorMid);
            return;
        }

        if (t < TradeTimeline.Up - 0.15)
        {
            if (show.Given is { } sprite)
            {
                DrawPhosphorSprite(sprite, ScreenMid, ScreenTop + 36);
            }

            CentreText("ENVÍO", ScreenTop + 42, Phosphor);
            return;
        }

        if (t < TradeTimeline.Gone)
        {
            CentreText("ENVIANDO", ScreenTop + 18, Phosphor);
            DrawProgress(ScreenTop + 32, (t - (TradeTimeline.Up - 0.15)) / (TradeTimeline.Gone - TradeTimeline.Up + 0.15));
            return;
        }

        if (t < TradeTimeline.Connected)
        {
            DrawGlobe(ScreenMid, ScreenTop + 21.5, 15, t * 1.7, t, show.Seed, ping: null);
            const string word = "BUSCANDO";
            var left = ScreenMid - (BigWidth(word + "...") / 2);
            BigText(word, left, ScreenTop + 42, Phosphor);
            var dots = (int)((t - TradeTimeline.Gone) * 4) % 4;
            for (var i = 0; i < dots; i++) BigText(".", left + BigWidth(word) + 1 + (i * 6), ScreenTop + 42, Phosphor);
            return;
        }

        if (t < TradeTimeline.Down)
        {
            var since = t - TradeTimeline.Connected;
            if (since < 0.08)
            {
                Rect(ScreenLeft + 1, ScreenTop + 1, ScreenWidth - 2, ScreenHeight - 2, Phosphor);
                return;
            }

            DrawGlobe(ScreenMid, ScreenTop + 21.5, 15, TradeTimeline.Connected * 1.7, t, show.Seed, ping: since);
            CentreText("¡CONECTADO!", ScreenTop + 42, (int)(since * 8) % 2 == 0 ? White : Phosphor);
            return;
        }

        if (t < TradeTimeline.Out)
        {
            CentreText("RECIBIENDO", ScreenTop + 18, Phosphor);
            DrawProgress(ScreenTop + 32, (t - TradeTimeline.Down) / (TradeTimeline.Out - TradeTimeline.Down));
            return;
        }

        if (t < TradeTimeline.Generation)
        {
            CentreText("RECIBIDO", ScreenTop + 25, Phosphor);
            return;
        }

        DrawReveals(show, t);
    }

    /// <summary>The glass of the screen: two faint diagonal reflections in the top left corner, over whatever it shows.</summary>
    private void DrawScreenGlare()
    {
        for (var y = ScreenTop + 1; y < ScreenTop + 26; y++)
        {
            for (var x = ScreenLeft; x < ScreenLeft + 34; x++)
            {
                var k = (x - ScreenLeft) + (y - ScreenTop);
                var band = k is >= 8 and < 14 ? 0.13 : k is >= 17 and < 19 ? 0.09 : 0;
                if (band > 0 && k < 30) Put(Canvas, x, y, Lerp(At(Canvas, x, y), GlassShine, band));
            }
        }
    }

    /// <summary>A progress bar in blocks, filling left to right.</summary>
    private void DrawProgress(int top, double p)
    {
        const int width = 78;
        var left = ScreenMid - (width / 2);
        Border(left, top, width, 9, PhosphorMid);
        var filled = (int)Math.Round(Math.Clamp(p, 0, 1) * 15);
        for (var i = 0; i < 15; i++)
        {
            Rect(left + 2 + (i * 5), top + 2, 4, 5, i < filled ? Phosphor : PhosphorDim);
        }
    }

    /// <summary>A Pokémon's icon on the screen, in the phosphor's three tones by how bright each of its pixels is.</summary>
    private void DrawPhosphorSprite(RoomSprite sprite, int cx, int feet)
    {
        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var left = cx - ((maxX - minX + 1) / 2);
        var top = feet - (maxY - minY + 1);
        for (var sy = minY; sy <= maxY; sy++)
        {
            for (var sx = minX; sx <= maxX; sx++)
            {
                if (!sprite.Solid(sx, sy)) continue;
                var c = sprite.At(sx, sy);
                var light = (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);
                PutScreen(left + sx - minX, top + sy - minY, light > 150 ? Phosphor : light > 70 ? PhosphorMid : PhosphorDim);
            }
        }
    }

    /// <summary>
    /// A wireframe globe turning on the screen, with pings where the search looks, and one big ping at its middle once
    /// someone is found.
    /// </summary>
    /// <param name="ping">Seconds since the connection, or null while it still searches.</param>
    private void DrawGlobe(double cx, double cy, double r, double spin, double t, int seed, double? ping)
    {
        const double step = Math.PI / 6;

        for (var y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
        {
            for (var x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                var nx = (x + 0.5 - cx) / r;
                var ny = (y + 0.5 - cy) / r;
                var d2 = (nx * nx) + (ny * ny);
                if (d2 > 1) continue;

                var d = Math.Sqrt(d2);
                if (d > 0.9)
                {
                    PutScreen(x, y, (nx + ny) < -0.4 ? Phosphor : PhosphorMid);
                    continue;
                }

                var nz = Math.Sqrt(1 - d2);
                var lat = Math.Asin(-ny);
                var lon = Math.Atan2(nx, nz) + spin;
                var dl = Math.Abs((lat / step) - Math.Round(lat / step)) * step * r;
                var dm = Math.Abs((lon / step) - Math.Round(lon / step)) * step * r * Math.Cos(lat);

                if (dl < 0.5 || dm < 0.45) PutScreen(x, y, (nx + ny) < -0.2 ? Phosphor : PhosphorMid);
                else if (Bayer[y & 3, x & 3] < 2) PutScreen(x, y, PhosphorDim);
            }
        }

        if (ping is { } since)
        {
            PutScreen((int)cx, (int)cy, White);
            var radius = since * 60;
            ScreenCircle(cx, cy, radius, since < 0.2 ? White : Phosphor);
            ScreenCircle(cx, cy, radius * 0.6, PhosphorMid);
            return;
        }

        // Las señales de la búsqueda: puntos del globo que se encienden y abren un anillo.
        var random = new Random(seed);
        for (var i = 0; i < 9; i++)
        {
            var lat = (random.NextDouble() - 0.5) * 2.2;
            var lon = random.NextDouble() * Math.PI * 2;
            var at = TradeTimeline.Gone + (i * 0.16) + (random.NextDouble() * 0.1);
            var s = t - at;
            if (s is < 0 or > 0.55) continue;

            var turned = lon + spin;
            if (Math.Cos(turned) < 0.15) continue;

            var px = cx + (r * Math.Cos(lat) * Math.Sin(turned));
            var py = cy - (r * Math.Sin(lat));
            PutScreen((int)px, (int)py, White);
            ScreenCircle(px, py, 1 + (s * 8), s < 0.25 ? Phosphor : PhosphorMid);
        }
    }

    private void ScreenCircle(double cx, double cy, double radius, Color colour)
    {
        var steps = Math.Max(8, (int)(radius * 7));
        for (var i = 0; i < steps; i++)
        {
            var a = i / (double)steps * Math.PI * 2;
            PutScreen((int)Math.Floor(cx + (Math.Cos(a) * radius)), (int)Math.Floor(cy + (Math.Sin(a) * radius)), colour);
        }
    }

    /// <summary>How a line of the screen looks at <paramref name="s"/> seconds from its stamp: a white block, then itself.</summary>
    private static bool Stamping(double s) => s is >= 0 and < 0.06;

    private void DrawReveals(TradeShow show, double t)
    {
        // La generación.
        var generation = $"GENERACIÓN {show.Generation}";
        var sGen = t - TradeTimeline.Generation;
        if (Stamping(sGen)) Rect(ScreenMid - (BigWidth(generation) / 2) - 2, ScreenTop + 3, BigWidth(generation) + 4, 11, White);
        else CentreText(generation, ScreenTop + 5, Phosphor);

        // Los tipos, en placas de su color, uno debajo del otro.
        var types = show.Types;
        for (var i = 0; i < types.Count && i < 2; i++)
        {
            var s = t - (i == 0 ? TradeTimeline.Types : TradeTimeline.SecondType);
            if (s < 0) continue;

            var top = types.Count == 1 ? ScreenTop + 21 : ScreenTop + 15 + (i * 13);
            DrawBadge(types[i], top, Stamping(s));
        }

        // El total, contado desde el del que se entregó, y la diferencia cuando para.
        var sTotal = t - TradeTimeline.Total;
        if (sTotal < 0) return;

        var land = TradeTimeline.TotalLanded - TradeTimeline.Total;
        var p = Math.Clamp(sTotal / land, 0, 1);
        var eased = 1 - Math.Pow(1 - p, 3);
        var value = p >= 1 ? show.Total : (int)Math.Round(show.GivenTotal + ((show.Total - show.GivenTotal) * eased));
        var number = value.ToString();
        var diff = show.Difference >= 0 ? $"+{show.Difference}%" : $"{show.Difference}%";
        var landed = t >= TradeTimeline.TotalLanded;

        var width = BigWidth("TOTAL") + 4 + BigWidth(number, 2) + 4 + BigWidth(diff);
        var left = ScreenMid - (width / 2);
        const int bottom = ScreenTop + ScreenHeight - 3;

        BigText("TOTAL", left, bottom - 7, PhosphorMid);
        var numberColour = landed && t < TradeTimeline.TotalLanded + 0.12 ? White : landed ? White : Phosphor;
        BigText(number, left + BigWidth("TOTAL") + 4, bottom - 14, numberColour, 2);

        if (landed)
        {
            var sDiff = t - TradeTimeline.TotalLanded;
            var diffLeft = left + BigWidth("TOTAL") + 4 + BigWidth(number, 2) + 4;
            if (Stamping(sDiff)) Rect(diffLeft - 1, bottom - 8, BigWidth(diff) + 2, 9, White);
            else BigText(diff, diffLeft, bottom - 7, show.Difference > 0 ? Better : show.Difference < 0 ? Worse : Phosphor);
        }
    }

    /// <summary>A type's plate: its colour, lighter on top and darker below, and its name in white with a hard shadow.</summary>
    private void DrawBadge(TradeType type, int top, bool stamping)
    {
        var name = type.Name.ToUpperInvariant();
        var width = BigWidth(name) + 8;
        var left = ScreenMid - (width / 2);
        var colour = type.Colour;

        if (stamping)
        {
            Rect(left, top, width, 11, White);
            return;
        }

        Rect(left, top, width, 11, colour);
        Rect(left, top, width, 1, Lerp(colour, White, 0.4));
        Rect(left, top + 10, width, 1, Lerp(colour, Void, 0.4));
        Border(left - 1, top - 1, width + 2, 13, Outline);
        BigText(name, left + 5, top + 3, Lerp(colour, Void, 0.6));
        BigText(name, left + 4, top + 2, White);
    }

    // ============================================================================================= what goes

    /// <summary>
    /// The Pokémon handed over on its pad, its ball, the red beam that calls it back, and the ball's way to the hatch.
    /// </summary>
    private void DrawSend(TradeShow show, BallPose ball, double t)
    {
        var mouth = (X: PadBallX - 4.0, Y: PadFloor - 1 - (PadBallR * 1.6));
        (double X, double Y)? centre = null;

        if (show.Given is { } sprite && t < TradeTimeline.Closed - 0.02)
        {
            var (_, minY, _, maxY) = Bounds(sprite);
            var height = (maxY - minY + 1) * 2;
            var feet = PadFloor - 2;
            centre = (SendX, feet - (height / 2.0));

            if (t < TradeTimeline.ShrinkStart)
            {
                var hop = t < TradeTimeline.BeamStart ? HopAt(t + 0.6) : 0;
                var red = Math.Clamp((t - TradeTimeline.BeamStart - 0.04) / 0.12, 0, 1);
                DrawPokemon(sprite, SendX, feet - hop, 2, 1 - red, RecallRed);
            }
            else
            {
                var q = (t - TradeTimeline.ShrinkStart) / (TradeTimeline.Closed - 0.02 - TradeTimeline.ShrinkStart);
                var e = q * q;
                var scale = (2 * (1 - e)) + (0.12 * e);
                centre = (SendX + ((mouth.X - SendX) * e), centre.Value.Y + ((mouth.Y - centre.Value.Y) * e));
                DrawSpriteScaled(sprite, centre.Value.X, centre.Value.Y, scale, (int)(t * 30) % 2 == 0 ? RecallRed : RecallDeep);
            }
        }

        if (centre is { } target && t >= TradeTimeline.BeamStart)
        {
            DrawRecallBeam(mouth.X, mouth.Y, target.X, target.Y, t);
        }

        if (ball.Where == Where.Floor)
        {
            var height = ball.Ground - (ball.Y + ball.R);
            Ellipse(ball.X, ball.Ground - 0.5, Math.Max(3, (ball.R * 1.4) - (height * 0.25)), 1.6, Shadow, dither: height > 4);
            DrawGivenBall(show, ball, null, null);

            if (t is >= TradeTimeline.Closed and < TradeTimeline.Closed + 0.4)
            {
                DrawSparkRing(ball.X, ball.Y, ball.R, t - TradeTimeline.Closed, LedGlow);
            }
        }
        else if (ball.Where == Where.Hatch)
        {
            DrawGivenBall(show, ball, (x, y) => Math.Pow(x + 0.5 - InTube, 2) + Math.Pow(y + 0.5 - HatchY, 2) < (HatchR - 0.5) * (HatchR - 0.5), null);
        }

        // La bocanada de aire al tragársela la trampilla.
        if (t is >= TradeTimeline.Up - 0.05 and < TradeTimeline.Up + 0.3)
        {
            DrawPuff(InTube, HatchY, t - (TradeTimeline.Up - 0.05), show.Seed);
        }
    }

    private void DrawPuff(double x, double y, double s, int seed)
    {
        var random = new Random(seed ^ 0x5A5A);
        for (var i = 0; i < 10; i++)
        {
            var a = random.NextDouble() * Math.PI * 2;
            var speed = 14 + (random.NextDouble() * 14);
            var px = x + (Math.Cos(a) * (HatchR + (speed * s)));
            var py = y + (Math.Sin(a) * (HatchR + (speed * s)) * 0.7);
            if ((i + (int)(s * 30)) % 3 == 0) continue;
            Put(Canvas, (int)Math.Floor(px), (int)Math.Floor(py), s < 0.12 ? White : GlassShine);
        }
    }

    /// <summary>The red beam of a Poké Ball calling its Pokémon back: a jagged line with a white core.</summary>
    private void DrawRecallBeam(double x0, double y0, double x1, double y1, double t)
    {
        var length = Math.Sqrt(Math.Pow(x1 - x0, 2) + Math.Pow(y1 - y0, 2));
        if (length < 1) return;

        var nx = -(y1 - y0) / length;
        var ny = (x1 - x0) / length;
        var steps = (int)(length * 1.6);
        for (var i = 0; i <= steps; i++)
        {
            var p = i / (double)steps;
            var wobble = Math.Sin((p * 17) + (t * 70)) * 1.3 * Math.Sin(p * Math.PI);
            var x = x0 + ((x1 - x0) * p) + (nx * wobble);
            var y = y0 + ((y1 - y0) * p) + (ny * wobble);
            var cx = (int)Math.Floor(x);
            var cy = (int)Math.Floor(y);
            Put(Canvas, cx - 1, cy, RecallRed);
            Put(Canvas, cx + 1, cy, RecallRed);
            Put(Canvas, cx, cy - 1, RecallRed);
            Put(Canvas, cx, cy + 1, RecallRed);
        }

        for (var i = 0; i <= steps; i++)
        {
            var p = i / (double)steps;
            var wobble = Math.Sin((p * 17) + (t * 70)) * 1.3 * Math.Sin(p * Math.PI);
            Put(Canvas, (int)Math.Floor(x0 + ((x1 - x0) * p) + (nx * wobble)), (int)Math.Floor(y0 + ((y1 - y0) * p) + (ny * wobble)),
                (int)(t * 40) % 2 == 0 ? White : RecallLight);
        }
    }

    private void DrawGivenBall(TradeShow show, BallPose pose, Func<int, int, bool>? clip, Color? tint)
    {
        if (show.GivenBall is { } kind)
        {
            DrawBall(pose.X, pose.Y, pose.R, pose.Angle, kind, new BallLook(pose.Flash, pose.Open, pose.Squash, false), clip, tint);
        }
        else if (show.GivenBallIcon is { } icon)
        {
            var (minX, _, maxX, _) = Bounds(icon);
            DrawSpriteScaled(icon, pose.X, pose.Y, pose.R * 2 / (maxX - minX + 1));
        }
        else
        {
            DrawBall(pose.X, pose.Y, pose.R, pose.Angle, CapsuleBall.Poke, new BallLook(pose.Flash, pose.Open, pose.Squash, false), clip, tint);
        }
    }

    /// <summary>Where the given ball is: on the pad, hopping to the hatch, going in, and up the tube.</summary>
    private BallPose GivenAt(double t)
    {
        if (t < TradeTimeline.BallOut) return BallPose.Hidden;

        if (t < TradeTimeline.HopStart)
        {
            var pop = Math.Clamp((t - TradeTimeline.BallOut) / 0.09, 0, 1);
            var r = PadBallR * Math.Min(1, 0.35 + (pop * 0.75));
            var open = t < TradeTimeline.Closed
                ? Math.Clamp((t - TradeTimeline.BallOut - 0.06) / 0.06, 0, 1)
                : 1 - Math.Clamp((t - TradeTimeline.Closed) / 0.05, 0, 1);
            var flash = t >= TradeTimeline.Closed ? Math.Max(0, 1 - ((t - TradeTimeline.Closed) / 0.14)) : 0;
            var angle = t < TradeTimeline.Closed ? -0.45 : -0.45 * Math.Max(0, 1 - ((t - TradeTimeline.Closed) / 0.1));

            // Un meneo al cerrarse, como al capturar.
            var w = (t - (TradeTimeline.Closed + 0.1)) / 0.22;
            if (w is >= 0 and < 1)
            {
                var tilt = Math.Sin(w * Math.PI * 2) * 0.28 * (1 - w);
                return new BallPose(Where.Floor, PadBallX + (Math.Sin(tilt) * r), PadFloor - 1 - (Math.Cos(tilt) * r), r, tilt,
                    Ground: PadFloor - 1);
            }

            return new BallPose(Where.Floor, PadBallX, PadFloor - 1 - r, r, angle, Open: open, Flash: flash, Ground: PadFloor - 1);
        }

        if (t < TradeTimeline.AtHatch)
        {
            // Dos botes: al suelo, alejándose (encoge), y de ahí dentro de la trampilla.
            const double midX = 118.0;
            const double midGround = 138.0;
            const double midR = 7.4;
            var split = TradeTimeline.HopStart + 0.32;

            if (t < split)
            {
                var q = (t - TradeTimeline.HopStart) / (split - TradeTimeline.HopStart);
                var x = PadBallX + ((midX - PadBallX) * q);
                var ground = (PadFloor - 1) + ((midGround - (PadFloor - 1)) * q);
                var r = PadBallR + ((midR - PadBallR) * q);
                return new BallPose(Where.Floor, x, ground - r - (Math.Sin(q * Math.PI) * 13), r, (x - PadBallX) / 8,
                    Squash: q < 0.08 ? 0.86 : 1, Ground: ground);
            }

            var p = (t - split) / (TradeTimeline.AtHatch - split);
            var hx = midX + ((InTube - midX) * p);
            var hy = (midGround - midR) + ((HatchY - (midGround - midR)) * p) - (Math.Sin(p * Math.PI) * 14);
            var hr = midR + ((TubeR - midR) * p);
            return new BallPose(Where.Floor, hx, hy, hr, (hx - PadBallX) / 8, Squash: p < 0.08 ? 0.86 : 1,
                Ground: midGround + ((StationBottom - midGround) * p));
        }

        if (t < TradeTimeline.Up)
        {
            var q = (t - TradeTimeline.AtHatch) / (TradeTimeline.Up - TradeTimeline.AtHatch);
            return new BallPose(Where.Hatch, InTube, HatchY, TubeR - (q * 2.8), (InTube - PadBallX) / 8);
        }

        if (t < TradeTimeline.Gone)
        {
            var q = (t - TradeTimeline.Up) / (TradeTimeline.Gone - TradeTimeline.Up);
            var start = TubeBottom + 5.0;
            var end = -Oy - 10.0;
            var y = start + ((end - start) * q * q);
            return new BallPose(Where.Tube, InTube, y, TubeR, y * 0.3);
        }

        return BallPose.Hidden;
    }

    // ============================================================================================= what comes

    /// <summary>Where the received ball is: down the tube, out of the hatch, bouncing to the pad, and still.</summary>
    private BallPose ReceivedAt(double t, TradeShow show)
    {
        if (t < TradeTimeline.Down) return BallPose.Hidden;

        if (t < TradeTimeline.InStation)
        {
            var q = (t - TradeTimeline.Down) / (TradeTimeline.InStation - TradeTimeline.Down);
            var start = -Oy - 10.0;
            var end = TubeBottom + 6.0;
            var y = start + ((end - start) * (1 - ((1 - q) * (1 - q))));
            return new BallPose(Where.Tube, OutTube, y, TubeR, y * 0.35);
        }

        if (t < TradeTimeline.Out) return BallPose.Hidden;

        const double dropX = OutTube + 9.0;
        const double dropGround = StationBottom + 1.0;
        const double dropR = 6.2;

        if (t < TradeTimeline.Out + 0.2)
        {
            var q = (t - TradeTimeline.Out) / 0.2;
            var x = OutTube + (3 * q);
            var r = 2 + (3.3 * q);
            return new BallPose(q < 0.45 ? Where.Hatch : Where.Floor, x, HatchY + (1.5 * q), r, (x - ReceiveX) / r, Ground: dropGround);
        }

        if (t < TradeTimeline.Dropped)
        {
            var q = (t - (TradeTimeline.Out + 0.2)) / (TradeTimeline.Dropped - (TradeTimeline.Out + 0.2));
            var x = OutTube + 3 + ((dropX - OutTube - 3) * q);
            var r = 5.3 + ((dropR - 5.3) * q);
            var from = HatchY + 1.5;
            var y = from + (((dropGround - r) - from) * q * q);
            return new BallPose(Where.Floor, x, y, r, (x - ReceiveX) / r, Squash: q > 0.94 ? 0.82 : 1, Ground: dropGround);
        }

        const double far = ReceiveX + 9;

        if (t < TradeTimeline.RollEnd)
        {
            // Rueda hacia la plataforma, acercándose (crece), con dos botes cada vez más bajos y pasándose un poco.
            var q = (t - TradeTimeline.Dropped) / (TradeTimeline.RollEnd - TradeTimeline.Dropped);
            var ease = 1 - ((1 - q) * (1 - q));
            var x = dropX + ((far - dropX) * ease);
            var ground = dropGround + (((PadFloor - 1) - dropGround) * ease);
            var r = dropR + ((HeroR - dropR) * ease);
            var lift = 0.0;
            var squash = 1.0;

            if (q < 0.4)
            {
                var p = q / 0.4;
                lift = Math.Sin(p * Math.PI) * 11;
                squash = p < 0.06 ? 0.82 : 1;
            }
            else if (q < 0.66)
            {
                var p = (q - 0.4) / 0.26;
                lift = Math.Sin(p * Math.PI) * 4.5;
                squash = p < 0.1 ? 0.88 : 1;
            }
            else if (q < 0.7)
            {
                squash = 0.93;
            }

            return new BallPose(Where.Floor, x, ground - r - lift, r, (x - ReceiveX) / r, Squash: squash, Ground: ground);
        }

        if (t < TradeTimeline.Settled)
        {
            var p = (t - TradeTimeline.RollEnd) / (TradeTimeline.Settled - TradeTimeline.RollEnd);
            var back = 1 - Math.Pow(1 - p, 3);
            var x = far + ((ReceiveX - far) * back);
            return new BallPose(Where.Floor, x, PadFloor - 1 - HeroR, HeroR, (x - ReceiveX) / HeroR, Ground: PadFloor - 1);
        }

        // Quieta en la plataforma; da un respingo cada vez que la pantalla dice algo.
        var stamps = new List<double> { TradeTimeline.Generation, TradeTimeline.Types, TradeTimeline.TotalLanded };
        if (show.Types.Count > 1) stamps.Add(TradeTimeline.SecondType);

        var k = 0;
        foreach (var at in stamps.Order())
        {
            var w = (t - at) / 0.32;
            if (w is >= 0 and < 1)
            {
                var tilt = Math.Sin(w * Math.PI * 2) * 0.2 * (1 - w) * (k % 2 == 0 ? 1 : -1);
                return new BallPose(Where.Floor, ReceiveX + (Math.Sin(tilt) * HeroR), PadFloor - 1 - (Math.Cos(tilt) * HeroR), HeroR, tilt,
                    Ground: PadFloor - 1);
            }

            k++;
        }

        return new BallPose(Where.Floor, ReceiveX, PadFloor - 1 - HeroR, HeroR, 0, Ground: PadFloor - 1);
    }

    /// <summary>The received ball, and once it opens, the light, the rays in its type's colour and the Pokémon.</summary>
    private void DrawArrival(TradeShow show, BallPose ball, double t)
    {
        if (ball.Where == Where.Hatch)
        {
            DrawBall(ball.X, ball.Y, ball.R, ball.Angle, CapsuleBall.Poke, BallLook.Plain,
                (x, y) => Math.Pow(x + 0.5 - OutTube, 2) + Math.Pow(y + 0.5 - HatchY, 2) < (HatchR - 0.5) * (HatchR - 0.5));
            return;
        }

        if (ball.Where != Where.Floor) return;

        var colour = show.Types.Count > 0 ? show.Types[0].Colour : LedGlow;
        var (x, y, r) = (ball.X, ball.Y, ball.R);

        var open = Math.Clamp((t - TradeTimeline.Open) / 0.25, 0, 1);
        var closing = Math.Clamp((t - (TradeTimeline.Revealed - 0.1)) / 0.25, 0, 1);
        open *= 1 - closing;

        if (t >= TradeTimeline.Open)
        {
            DrawRays(x, y - r - 26, t - TradeTimeline.Open - 0.2, colour, RayStrength(show.Total), show.Legendary);
            DrawBeam(x, y, t - TradeTimeline.Open, colour);
        }

        var blink = t is >= TradeTimeline.TotalLanded + 0.3 and < TradeTimeline.Open && ((int)((t - TradeTimeline.TotalLanded) / 0.1) % 2 == 0);

        // Abierta, la tapa va detrás del Pokémon; cerrada, la ball queda delante de sus pies.
        var behind = open > 0;
        if (behind) DrawBall(x, y, r, ball.Angle, CapsuleBall.Poke, new BallLook(0, open, ball.Squash, false));

        if (t >= TradeTimeline.Emerge && show.Received is { } sprite)
        {
            var scale = t < TradeTimeline.Emerge + 0.2 ? 1 : 2;
            var colourIn = Math.Clamp((t - (TradeTimeline.Emerge + 0.45)) / 0.12, 0, 1);
            var hop = t > TradeTimeline.Revealed + 0.4 ? HopAt(t - TradeTimeline.Revealed - 0.4) : 0;
            DrawPokemon(sprite, x, y - r + 1 - hop, scale, colourIn);
        }

        if (!behind) DrawBall(x, y, r, ball.Angle, CapsuleBall.Poke, new BallLook(0, 0, ball.Squash, blink));

        if (t >= TradeTimeline.Emerge + 0.2)
        {
            DrawParticles(x, y - r, t - (TradeTimeline.Emerge + 0.2), colour, show.Seed, show.Legendary);
        }

        if (show.Shiny && t >= TradeTimeline.Revealed)
        {
            DrawShinySparkles(x, y - r - 30, t - TradeTimeline.Revealed, show.Seed);
        }
    }
}
