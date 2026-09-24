using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>What the machine is loaded with: the banner chosen under it.</summary>
/// <param name="Name">What the marquee says.</param>
/// <param name="Skin">Tier index the machine is painted in: the one the banner mostly hands out, as its bar says.</param>
/// <param name="Odds">Chance of each tier on this banner, by tier index. The dome holds balls in these proportions.</param>
/// <param name="Price">What the coin plaque says, already written.</param>
/// <param name="Deluxe">The dearest banner: gold trim and more bulbs.</param>
public sealed record CapsuleBanner(string Name, int Skin, IReadOnlyList<double> Odds, string Price, bool Deluxe);

/// <summary>One pull as the machine plays it. Everything in it was decided and written before the crank turns.</summary>
/// <param name="Steps">
/// Tiers the ball shows, from the one it drops as to the one it opens as. It drops as the cheapest ball the banner has,
/// so the first ball says nothing, and climbs at most twice.
/// </param>
/// <param name="Pokemon">The icon of what came out, from the player's cartridge; null draws only the light.</param>
public sealed record CapsuleRoll(IReadOnlyList<int> Steps, RoomSprite? Pokemon, bool Shiny, bool Legendary, int Seed);

/// <summary>One pull on the shelf of what has come out: its icon and its tier, for the colour of its stand.</summary>
public sealed record CapsuleShelfItem(RoomSprite? Sprite, int Tier);

/// <summary>Everything one frame of the machine shows.</summary>
/// <param name="LoadedFor">Seconds since the banner was chosen: the dome fills up during the first half second.</param>
/// <param name="Roll">The pull being played, or null while the machine waits.</param>
/// <param name="RollTime">Seconds since the coin dropped.</param>
/// <param name="Shelf">What has come out, newest first; the shelf shows the first eight.</param>
/// <param name="Departed">
/// The pull that has just been put away: its ball flies from the mat to the shelf, and the newest figure appears
/// when it lands. Null when nothing is flying.
/// </param>
/// <param name="DepartTime">Seconds since it started flying.</param>
/// <param name="ShelfNewestAge">Seconds since the newest figure was put on the shelf: it drops into place during the first third of a second.</param>
public sealed record CapsuleSceneState(
    CapsuleBanner Banner,
    double LoadedFor,
    CapsuleRoll? Roll,
    double RollTime,
    IReadOnlyList<CapsuleShelfItem> Shelf,
    CapsuleRoll? Departed = null,
    double DepartTime = 0,
    double ShelfNewestAge = 99);

/// <summary>
/// When each thing happens in a pull, in seconds from the moment the coin drops.
/// </summary>
/// <remarks>
/// The same for every tier on purpose. The old reel took longer the rarer the result, which told the player what was
/// coming before it arrived; here only what the ball does differs, never how long the machine takes to get there.
/// </remarks>
public static class CapsuleTimeline
{
    public const double CrankStart = 0.45;
    public const double CrankTurn = 0.42;
    public const int CrankTurns = 4;
    public const double ChurnStart = 0.8;
    public const double ChurnEnd = 2.7;
    public const double Exit = 2.9;
    public const double FirstLanding = 3.55;
    public const double Settled = 4.8;
    public const double WobbleStart = 4.9;
    public const double WobbleLength = 1.15;
    public const double WobbleTilt = 0.7;
    public const int Wobbles = 3;
    public const double Open = 8.9;
    public const double Emerge = 9.15;

    /// <summary>The Pokémon is out and in colour: this is when the screen can say what it is.</summary>
    public const double Revealed = 9.95;

    public static double WobbleEnd => WobbleStart + (Wobbles * WobbleLength);

    /// <summary>The moment at the end of a wobble when the ball may turn into a better one.</summary>
    public static double UpgradeAt(int wobble) => WobbleStart + (wobble * WobbleLength) + WobbleTilt + 0.02;

    /// <summary>
    /// Which wobbles the ball climbs on. Drawn from the pull's own seed and never from the tier, so where it climbs
    /// says nothing about how far it will go.
    /// </summary>
    public static int[] UpgradeWobbles(int climbs, int seed)
    {
        var pick = Math.Abs(seed % 3);

        return climbs switch
        {
            <= 0 => [],
            1 => [pick == 0 ? 1 : 2],
            _ => pick switch { 0 => [0, 2], 1 => [1, 2], _ => [0, 1] },
        };
    }

    /// <summary>How many climbs have happened by <paramref name="t"/>.</summary>
    public static int StepAt(double t, int climbs, int seed) =>
        UpgradeWobbles(climbs, seed).Count(w => t >= UpgradeAt(w));
}

/// <summary>
/// The gacha as a capsule machine, drawn cell by cell: a dome of Poké Balls, a crank, a chute, and the ball that comes
/// out, wobbles three times like a capture and opens.
/// </summary>
/// <remarks>
/// <para>
/// Chosen by the player on 2026-09-22 to replace the reel (§171). The dome is not decoration: it holds the balls of the
/// banner in its real proportions, one ball per tier — Poké, Super, Ultra, Gloria and Master — so a glance at the glass
/// says what the banner hands out. The machine is painted in the colour of the tier the banner mostly gives, as the bar
/// under its card already is.
/// </para>
/// <para>
/// The ball drops as the cheapest ball the banner holds and climbs at the end of a wobble, which is the old tier tease
/// told with something every player already reads: a Master Ball needs no legend. Nothing on screen decides anything —
/// the pull is written before the coin falls.
/// </para>
/// <para>
/// Drawn like the rest of the pixel art (§116, §120, §169): whole cells, flat colours, ordered dithering instead of
/// gradients, no blur. The balls are computed rather than typed in, so a tilted ball is still clean pixels at any angle.
/// </para>
/// </remarks>
public sealed class CapsuleMachineScene : PixelScene
{
    public const int DesignWidth = 350;
    public const int DesignRows = 166;

    private const int FloorTop = 118;
    private const double DomeX = 215.0;
    private const double DomeY = 36.0;
    private const double DomeR = 31.0;
    private const int CollarTop = 60;
    private const int BodyTop = 67;
    private const int BodyBottom = 127;
    private const int BodyLeft = 183;
    private const int BodyRight = 247;
    private const int ChuteLeft = 203;
    private const int ChuteRight = 227;
    private const int ChuteTop = 114;
    private const int ChuteBottom = 125;
    private const double CrankX = 215.0;
    private const double CrankY = 100.5;
    private const double RestX = 262.0;

    /// <summary>The machine is drawn around column 215 and shifted this far left; the ball lands on the right, over bare wall.</summary>
    private const int Shift = -64;

    /// <summary>Where the chute is once the machine has been shifted.</summary>
    private const double ChuteX = 215.0 + Shift;
    private const double RestFloor = 142.0;
    private const double HeroR = 13.0;
    private const double DomeBallR = 5.3;

    // --------------------------------------------------------------------------------------------- palette
    private static readonly Color ChuteInside = Rgb(0x05, 0x04, 0x09);
    private static readonly Color ChuteShade = Rgb(0x12, 0x0E, 0x1C);
    private static readonly Color Coin = Rgb(0xFF, 0xD2, 0x4A);
    private static readonly Color CoinDark = Rgb(0xB0, 0x7A, 0x1C);

    /// <summary>The tiers' own colours, the same as the portals, the bars and the strip of past rolls.</summary>
    private static readonly Color[] TierColours =
    [
        Rgb(0x7C, 0x85, 0x93), Rgb(0x5F, 0xA8, 0x6F), Rgb(0x4E, 0x8B, 0xC9), Rgb(0xD7, 0x5F, 0xA8), Rgb(0xD9, 0xA4, 0x41)
    ];

    private CapsuleBanner? _layoutFor;
    private List<DomeBall> _dome = [];

    public CapsuleMachineScene(int width, int height = DesignRows)
        : base(width, height, DesignWidth, DesignRows)
    {
        PaintBackground();
    }

    /// <summary>A bare canvas for one ball, with nothing painted behind it.</summary>
    private CapsuleMachineScene(int size)
        : base(size)
    {
    }

    /// <summary>
    /// The ball of a tier on its own, <paramref name="cells"/> across and one cell per pixel, for the portals over the
    /// machine: the same drawing as the ball on the mat, so the ladder reads the same in both places.
    /// </summary>
    public static BitmapSource BallIcon(int tier, int cells)
    {
        var canvas = new CapsuleMachineScene(cells + 2);
        var centre = (cells + 2) / 2.0;
        canvas.DrawBall(centre, centre, cells / 2.0, -0.35, (CapsuleBall)Math.Clamp(tier, 0, 4), BallLook.Plain);
        canvas.Present();
        canvas.Bitmap.Freeze();
        return canvas.Bitmap;
    }

    /// <summary>How long the ball takes to fly from the mat to the shelf when a pull is put away.</summary>
    public const double DepartLength = 0.7;

    /// <summary>
    /// Draws one frame.
    /// </summary>
    /// <param name="clock">Seconds since the screen opened, for what moves on its own: bulbs, glints.</param>
    /// <param name="toScreen">False to leave the bitmap alone, for pictures drawn off screen.</param>
    public void Render(CapsuleSceneState state, double clock, bool toScreen = true)
    {
        var banner = state.Banner;
        var loadedFor = state.LoadedFor;
        var roll = state.Roll;
        var t = state.RollTime;

        BeginFrame();

        DrawNeonSign(clock);
        DrawShelf(state.Shelf, state.ShelfNewestAge);

        if (!ReferenceEquals(_layoutFor, banner))
        {
            _dome = LayOutDome(banner);
            _layoutFor = banner;
        }

        var playing = roll is not null;
        var step = (int)(clock * 6);

        // La máquina se dibuja desplazada a la izquierda; el golpe de la manivela la baja una celda en cada clac.
        Dx = Shift;
        Dy = playing && Clack(t) ? 1 : 0;

        DrawMachine(banner, roll, t, step);
        DrawDome(banner, roll, t, loadedFor, clock);
        DrawLid(Skin(banner.Skin), Trim(banner), banner.Deluxe, step);
        Dx = 0;
        Dy = 0;

        if (roll is not null)
        {
            DrawPull(banner, roll, t, clock);
        }
        else if (state.Departed is { } gone && state.DepartTime < DepartLength)
        {
            DrawDeparture(gone, state.DepartTime);
        }

        if (toScreen)
        {
            Present();
        }
    }

    // ============================================================================================= background

    private void PaintBackground()
    {
        PaintRoom(FloorTop, DesignRows, 215, 215 + Shift);

        // La sombra de la máquina en el suelo, dura y tramada en el borde.
        for (var y = 128; y < 140; y++)
        {
            for (var x = 160 + Shift; x < 270 + Shift; x++)
            {
                var d = Math.Pow((x + 0.5 - 215 - Shift) / 50.0, 2) + Math.Pow((y + 0.5 - 133.5) / 5.5, 2);
                if (d < 1 && (d < 0.7 || Bayer[y & 3, x & 3] < 8)) Put(Back, x, y, Shadow);
            }
        }

        // La alfombrilla redonda donde cae la ball: un aro violeta sobre las baldosas.
        for (var y = (int)RestFloor - 10; y < (int)RestFloor + 10; y++)
        {
            for (var x = (int)RestX - 34; x < (int)RestX + 34; x++)
            {
                var d = Math.Sqrt(Math.Pow((x + 0.5 - RestX) / 32.0, 2) + Math.Pow((y + 0.5 - (RestFloor - 1)) / 7.0, 2));
                if (d > 1) continue;

                var colour = d > 0.9 ? Outline
                    : d > 0.78 ? (y < RestFloor - 2 ? NeonMid : NeonGlow)
                    : d > 0.7 ? Outline
                    : ((x / 3) + (y / 2)) % 2 == 0 ? Rgb(0x2C, 0x20, 0x4A) : Rgb(0x25, 0x1B, 0x3F);
                Put(Back, x, y, colour);
            }
        }
    }

    // ============================================================================================= the machine

    private void DrawMachine(CapsuleBanner banner, CapsuleRoll? roll, double t, int step)
    {
        var skin = Skin(banner.Skin);
        var trim = Trim(banner);

        DrawBody(skin);
        DrawCollar(trim);
        DrawMarquee(banner, trim, step, roll is not null && t < CapsuleTimeline.Settled);
        DrawCoinPlaque(banner, trim);
        DrawCrank(skin, trim, roll is null ? 0 : CrankAngle(t));
        DrawEmblem(banner);
        DrawChute(skin, trim, roll is null ? 0 : FlapOpen(t));
        DrawBase(skin);

        if (roll is not null && t < CapsuleTimeline.CrankStart + 0.05)
        {
            DrawCoin(t);
        }
    }

    /// <summary>Five tones of the machine's paint from the tier's colour: highlight, light, base, shade, deep.</summary>
    private static Color[] Skin(int tier)
    {
        var basis = TierColours[Math.Clamp(tier, 0, TierColours.Length - 1)];
        return
        [
            Lerp(basis, White, 0.45), Lerp(basis, White, 0.18), basis, Lerp(basis, Void, 0.32), Lerp(basis, Void, 0.58)
        ];
    }

    /// <summary>The metal: chrome, or gold on the dearest banner.</summary>
    private static Color[] Trim(CapsuleBanner banner) =>
        banner.Deluxe ? [GoldHi, GoldLight, Gold, GoldDark] : [ChromeHi, ChromeLight, ChromeMid, ChromeDark];

    private void DrawLid(Color[] skin, Color[] trim, bool deluxe, int step)
    {
        // La tapa: un casquete de la pintura de la máquina sobre la cúpula, con su aro y su pomo.
        const double lidY = 11.5;
        for (var y = 2; y <= 12; y++)
        {
            for (var x = 194; x < 236; x++)
            {
                var nx = (x + 0.5 - DomeX) / 20.5;
                var ny = (y + 0.5 - lidY) / 9.0;
                var d = (nx * nx) + (ny * ny);
                if (d > 1) continue;

                var colour = d > 0.84 ? Outline
                    : nx < -0.6 ? skin[1]
                    : nx > 0.62 ? skin[4]
                    : nx > 0.4 ? skin[3]
                    : ny < -0.55 && nx < -0.05 ? skin[0]
                    : skin[2];
                if (colour == skin[2] && nx < -0.25 && Bayer[y & 3, x & 3] < 8) colour = skin[1];
                Put(Canvas, x, y, colour);
            }
        }

        Rect(195, 11, 40, 1, trim[1]);
        Rect(195, 12, 40, 1, trim[2]);
        Rect(229, 11, 6, 2, trim[3]);
        Rect(194, 13, 42, 1, Outline);

        // Pomo.
        Rect(212, 0, 6, 3, skin[2]);
        Rect(212, 0, 2, 2, skin[1]);
        Rect(216, 0, 2, 3, skin[3]);
        Rect(211, 3, 8, 1, Outline);

        if (deluxe)
        {
            // La cara lleva una fila de bombillas en la tapa.
            for (var i = 0; i < 6; i++)
            {
                var bx = 200 + (i * 6);
                var on = (i + step) % 2 == 0;
                Put(Canvas, bx, 8, on ? BulbOn : BulbOff);
                Put(Canvas, bx + 1, 8, on ? BulbWarm : BulbOff);
                Put(Canvas, bx, 9, on ? BulbWarm : BulbOff);
            }
        }
    }

    private void DrawBody(Color[] skin)
    {
        for (var y = BodyTop; y < BodyBottom; y++)
        {
            for (var x = BodyLeft; x < BodyRight; x++)
            {
                var cornerBottom = y >= BodyBottom - 2 && (x < BodyLeft + 2 || x >= BodyRight - 2) && !(y == BodyBottom - 2 && (x == BodyLeft + 1 || x == BodyRight - 2));
                if (cornerBottom) continue;

                var edge = x == BodyLeft || x == BodyRight - 1 || y == BodyBottom - 1
                           || (y == BodyBottom - 2 && (x == BodyLeft + 1 || x == BodyRight - 2));
                var colour = edge ? Outline
                    : x <= BodyLeft + 1 ? skin[0]
                    : x <= BodyLeft + 3 ? skin[1]
                    : x >= BodyRight - 3 ? skin[4]
                    : x >= BodyRight - 6 ? skin[3]
                    : skin[2];

                // Brillo vertical ancho a la izquierda, tramado: la luz viene de arriba a la izquierda.
                if (colour == skin[2] && x <= BodyLeft + 7 && Bayer[y & 3, x & 3] < 8) colour = skin[1];
                if (colour == skin[2] && x >= BodyRight - 10 && Bayer[y & 3, x & 3] < 6) colour = skin[3];
                Put(Canvas, x, y, colour);
            }
        }

        // Sombra de la cúpula y el aro sobre el cuerpo.
        Rect(BodyLeft + 1, BodyTop, BodyRight - BodyLeft - 2, 1, skin[4]);
        for (var x = BodyLeft + 1; x < BodyRight - 1; x++)
        {
            if (Bayer[1, x & 3] < 8) Put(Canvas, x, BodyTop + 1, skin[3]);
        }

        // Juntas de los paneles.
        Rect(BodyLeft + 1, 87, BodyRight - BodyLeft - 2, 1, skin[3]);
        Rect(BodyLeft + 2, 88, BodyRight - BodyLeft - 4, 1, skin[1]);
        Rect(BodyLeft + 1, 113, BodyRight - BodyLeft - 2, 1, skin[3]);

        // Remaches en las cuatro esquinas del panel central.
        foreach (var (rx, ry) in new[] { (186, 90), (243, 90), (186, 110), (243, 110) })
        {
            Put(Canvas, rx, ry, skin[0]);
            Put(Canvas, rx + 1, ry + 1, skin[4]);
        }
    }

    private void DrawCollar(Color[] trim)
    {
        for (var y = CollarTop; y < BodyTop; y++)
        {
            for (var x = 179; x < 251; x++)
            {
                var cut = (y == CollarTop || y == BodyTop - 1) && (x == 179 || x == 250);
                if (cut) continue;

                var edge = y == CollarTop || y == BodyTop - 1 || x == 179 || x == 250;
                var colour = edge ? Outline
                    : y == CollarTop + 1 ? trim[1]
                    : y == CollarTop + 2 ? trim[2]
                    : y >= BodyTop - 3 ? trim[3]
                    : trim[2];

                if (!edge && x > 238 && y > CollarTop + 1) colour = trim[3];
                if (!edge && x < 185 && y > CollarTop + 1 && y < BodyTop - 3) colour = trim[1];
                Put(Canvas, x, y, colour);
            }
        }

        // Un destello duro en el aro.
        Rect(188, CollarTop + 1, 8, 1, trim[0]);
        Rect(190, CollarTop + 2, 3, 1, trim[1]);
    }

    private void DrawMarquee(CapsuleBanner banner, Color[] trim, int step, bool racing)
    {
        const int left = 190;
        const int top = 70;
        const int width = 50;
        const int height = 15;

        Rect(left, top, width, height, Plate);
        Border(left - 1, top - 1, width + 2, height + 2, Outline);
        Border(left, top, width, height, trim[2]);
        Rect(left + 1, top + 1, width - 2, 1, PlateLight);

        // Bombillas alrededor del cartel. En reposo persiguen despacio; mientras la máquina trabaja, deprisa.
        var bulbs = new List<(int X, int Y)>();
        for (var x = left + 2; x < left + width - 1; x += 4) bulbs.Add((x, top));
        for (var y = top + 4; y < top + height - 1; y += 4) bulbs.Add((left + width - 1, y));
        for (var x = left + width - 3; x > left; x -= 4) bulbs.Add((x, top + height - 1));
        for (var y = top + height - 5; y > top; y -= 4) bulbs.Add((left, y));

        var phase = racing ? step * 2 : step / 2;
        for (var i = 0; i < bulbs.Count; i++)
        {
            var on = (i + phase) % 3 == 0;
            var (bx, by) = bulbs[i];
            Put(Canvas, bx, by, on ? BulbOn : BulbOff);
            if (on) Put(Canvas, bx + (bx == left || bx == left + width - 1 ? 0 : 1), by + (bx == left || bx == left + width - 1 ? 1 : 0), BulbWarm);
        }

        var text = banner.Name.ToUpperInvariant();
        var textWidth = (text.Length * 6) - 1;
        var tx = left + ((width - textWidth) / 2);
        var ty = top + 4;
        BigText(text, tx + 1, ty + 1, Outline);
        BigText(text, tx, ty, banner.Deluxe ? GoldLight : BulbOn);
    }

    private void DrawCoinPlaque(CapsuleBanner banner, Color[] trim)
    {
        Frame(187, 90, 10, 15, trim[1], trim[3]);
        Rect(189, 92, 6, 11, trim[2]);
        Rect(191, 93, 2, 9, ChuteInside);
        Put(Canvas, 191, 93, Outline);
        Put(Canvas, 192, 101, trim[0]);

        // El precio debajo, en dorado: lo que cuesta una tirada de este banner.
        var price = banner.Price;
        var width = (price.Length * 4) - 1;
        var px = 192 - (width / 2);
        SmallText(price, px + 1, 108, Outline);
        SmallText(price, px, 107, GoldLight);
    }

    private void DrawCrank(Color[] skin, Color[] trim, double angle)
    {
        Frame(202, 89, 27, 23, trim[1], trim[3]);
        Rect(204, 91, 23, 19, trim[2]);
        foreach (var (rx, ry) in new[] { (204, 91), (226, 91), (204, 109), (226, 109) }) Put(Canvas, rx, ry, trim[0]);

        // El disco.
        Disc(CrankX, CrankY, 8.6, (nx, ny, d) =>
            d > 0.88 ? Outline
            : (nx + ny) < -0.9 ? trim[0]
            : (nx + ny) < -0.2 ? trim[1]
            : (nx + ny) > 0.9 ? trim[3]
            : trim[2]);
        Disc(CrankX, CrankY, 5.2, (nx, ny, d) => d > 0.8 ? trim[3] : ((nx + ny) < -0.3 ? trim[1] : trim[2]));

        // La manivela: una barra que atraviesa el disco con un pomo en cada punta.
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        for (var y = 86; y < 116; y++)
        {
            for (var x = 200; x < 231; x++)
            {
                var dx = x + 0.5 - CrankX;
                var dy = y + 0.5 - CrankY;
                var u = (dx * cos) + (dy * sin);
                var v = (-dx * sin) + (dy * cos);

                if (Math.Abs(u) <= 11.5 && Math.Abs(v) <= 2.2)
                {
                    var colour = Math.Abs(v) > 1.5 || Math.Abs(u) > 10.8 ? Outline : v < -0.4 ? skin[1] : v > 0.6 ? skin[3] : skin[2];
                    Put(Canvas, x, y, colour);
                }

                foreach (var end in new[] { -10.5, 10.5 })
                {
                    var eu = u - end;
                    var r = Math.Sqrt((eu * eu) + (v * v));
                    if (r <= 3.3) Put(Canvas, x, y, r > 2.5 ? Outline : (eu + v) < -0.8 ? skin[0] : skin[2]);
                }
            }
        }

        Disc(CrankX, CrankY, 2.2, (_, _, d) => d > 0.7 ? Outline : trim[0]);
    }

    /// <summary>A sticker of the ball the banner mostly hands out, beside the crank.</summary>
    private void DrawEmblem(CapsuleBanner banner)
    {
        Disc(238.0, 97.0, 6.6, (_, _, d) => d > 0.86 ? Outline : White);
        DrawBall(238.0, 97.0, 5.2, -0.35, Dominant(banner), BallLook.Plain);
    }

    private void DrawChute(Color[] skin, Color[] trim, double open)
    {
        for (var y = ChuteTop; y < ChuteBottom; y++)
        {
            for (var x = ChuteLeft; x < ChuteRight; x++)
            {
                var corner = y < ChuteTop + 2 && (x < ChuteLeft + 2 || x >= ChuteRight - 2) && !(y == ChuteTop + 1 && (x == ChuteLeft + 1 || x == ChuteRight - 2));
                if (corner) continue;
                Put(Canvas, x, y, y < ChuteTop + 3 ? ChuteShade : ChuteInside);
            }
        }

        Border(ChuteLeft - 1, ChuteTop - 1, ChuteRight - ChuteLeft + 2, ChuteBottom - ChuteTop + 1, Outline);
        Rect(ChuteLeft - 1, ChuteTop - 2, ChuteRight - ChuteLeft + 2, 1, skin[3]);

        // La trampilla: plástico tintado que se abre hacia fuera cuando sale la ball.
        var height = (int)Math.Round((ChuteBottom - ChuteTop - 1) * (1 - (0.8 * Math.Clamp(open, 0, 1))));
        for (var y = ChuteTop; y < ChuteTop + height; y++)
        {
            for (var x = ChuteLeft + 1; x < ChuteRight - 1; x++)
            {
                if ((x + y) % 2 == 0) Put(Canvas, x, y, Lerp(GlassLight, skin[1], 0.25));
            }

            Put(Canvas, ChuteLeft + 3, y, GlassShine);
            Put(Canvas, ChuteLeft + 4, y, GlassRim);
        }

        if (height > 0) Rect(ChuteLeft + 1, ChuteTop + height - 1, ChuteRight - ChuteLeft - 2, 1, GlassRim);

        // La bandeja.
        Rect(ChuteLeft - 3, ChuteBottom, ChuteRight - ChuteLeft + 6, 1, trim[0]);
        Rect(ChuteLeft - 3, ChuteBottom + 1, ChuteRight - ChuteLeft + 6, 1, trim[2]);
        Border(ChuteLeft - 4, ChuteBottom - 1, ChuteRight - ChuteLeft + 8, 4, Outline);
    }

    private void DrawBase(Color[] skin)
    {
        Rect(179, BodyBottom, 72, 6, skin[4]);
        Rect(180, BodyBottom, 70, 1, skin[3]);
        Border(178, BodyBottom - 1, 74, 8, Outline);
        Rect(181, BodyBottom + 7, 6, 2, Outline);
        Rect(243, BodyBottom + 7, 6, 2, Outline);
    }

    private void DrawCoin(double t)
    {
        // Cae desde arriba hasta la ranura, girando: el ancho va y viene.
        var p = Math.Clamp(t / CapsuleTimeline.CrankStart, 0, 1);
        var y = 74 + (int)Math.Round(p * p * 19);
        var w = (int)Math.Round(Math.Abs(Math.Cos(t * 18)) * 2.5);
        for (var dy = 0; dy < 5; dy++)
        {
            for (var dx = -w; dx <= w; dx++)
            {
                var edge = Math.Abs(dx) == w || dy is 0 or 4;
                Put(Canvas, 192 + dx, y + dy, edge ? CoinDark : Coin);
            }
        }
    }

    private static double CrankAngle(double t)
    {
        var angle = 0.0;
        for (var i = 0; i < CapsuleTimeline.CrankTurns; i++)
        {
            var start = CapsuleTimeline.CrankStart + (i * CapsuleTimeline.CrankTurn);
            var p = Math.Clamp((t - start) / (CapsuleTimeline.CrankTurn * 0.64), 0, 1);
            angle += Math.PI / 2 * (p < 0.5 ? 2 * p * p : 1 - (Math.Pow(-2 * p + 2, 2) / 2));
        }

        return angle;
    }

    /// <summary>True for the instant after each quarter turn, when the mechanism clicks and the machine jolts.</summary>
    private static bool Clack(double t)
    {
        for (var i = 0; i < CapsuleTimeline.CrankTurns; i++)
        {
            var at = CapsuleTimeline.CrankStart + (i * CapsuleTimeline.CrankTurn) + (CapsuleTimeline.CrankTurn * 0.64);
            if (t >= at && t < at + 0.07) return true;
        }

        return false;
    }

    private static double FlapOpen(double t)
    {
        var s = t - CapsuleTimeline.Exit;
        if (s < 0) return 0;
        if (s < 0.2) return s / 0.2;
        var after = s - 0.2;
        return Math.Max(0, Math.Cos(after * 11) * Math.Exp(-after * 4.5));
    }

    // ============================================================================================= the dome

    private sealed record DomeBall(double X, double Y, int Tier, double Angle, double Phase, int Row);

    /// <summary>
    /// The pile of balls in the dome, in the banner's proportions and in the same places every time it is chosen.
    /// </summary>
    private static List<DomeBall> LayOutDome(CapsuleBanner banner)
    {
        var slots = new List<(double X, double Y, int Row)>();
        var d = DomeBallR * 2;
        var y = CollarTop - DomeBallR + 2.2;

        // Filas al tresbolillo y un poco solapadas, como un montón de verdad; llega a un poco más de media cúpula.
        for (var row = 0; y > DomeY - 9; row++)
        {
            var dy = y - DomeY;
            var half = Math.Sqrt(Math.Max(0, Math.Pow(DomeR - DomeBallR - 1.2, 2) - (dy * dy)));
            var offset = row % 2 == 0 ? 0 : d * 0.48;
            var count = (int)Math.Floor(((half * 2) - offset) / (d * 0.96)) + 1;
            var start = DomeX - (((count - 1) * d * 0.96) / 2) + (row % 2 == 0 ? 0 : 0.6);

            for (var i = 0; i < count; i++)
            {
                slots.Add((start + (i * d * 0.96), y, row));
            }

            y -= d * 0.78;
        }

        var counts = DomeMix(banner.Odds, slots.Count);
        var tiers = new List<int>();
        for (var i = 0; i < counts.Length; i++) tiers.AddRange(Enumerable.Repeat(i, counts[i]));

        var random = new Random(StableHash(banner.Name));
        var shuffled = tiers.OrderBy(_ => random.Next()).ToList();

        return slots.Select((s, i) => new DomeBall(
            s.X + ((random.NextDouble() - 0.5) * 1.2),
            s.Y + ((random.NextDouble() - 0.5) * 0.8),
            shuffled[i],
            (random.NextDouble() - 0.5) * 1.6,
            random.NextDouble() * Math.PI * 2,
            s.Row)).ToList();
    }

    /// <summary>
    /// How many balls of each tier go in a dome of <paramref name="slots"/>, in the banner's odds.
    /// </summary>
    /// <remarks>
    /// By largest remainder, so they add up to exactly the balls that fit and a tier the banner does not give gets
    /// none: the glass never shows a ball that cannot come out. A banner with no odds at all fills with the first tier
    /// rather than leaving the dome empty.
    /// </remarks>
    public static int[] DomeMix(IReadOnlyList<double> odds, int slots)
    {
        var clean = odds.Select(o => Math.Max(0, o)).ToArray();
        var total = clean.Sum();
        var counts = new int[Math.Max(1, clean.Length)];

        if (total <= 0)
        {
            counts[0] = slots;
            return counts;
        }

        var exact = clean.Select(o => o / total * slots).ToArray();
        for (var i = 0; i < clean.Length; i++) counts[i] = (int)Math.Floor(exact[i]);

        foreach (var i in Enumerable.Range(0, clean.Length)
                     .Where(i => clean[i] > 0)
                     .OrderByDescending(i => exact[i] - counts[i])
                     .Take(slots - counts.Sum()))
        {
            counts[i]++;
        }

        return counts;
    }

    /// <summary>Inside the dome's glass and above the collar: where a ball of the pile can be seen.</summary>
    private static bool InsideDome(int x, int y)
    {
        var dx = x + 0.5 - DomeX;
        var dy = y + 0.5 - DomeY;
        return (dx * dx) + (dy * dy) <= (DomeR - 1.6) * (DomeR - 1.6) && y < CollarTop;
    }

    private void DrawDome(CapsuleBanner banner, CapsuleRoll? roll, double t, double loadedFor, double clock)
    {
        var top = (int)Math.Floor(DomeY - DomeR);
        var left = (int)Math.Floor(DomeX - DomeR);
        var right = (int)Math.Ceiling(DomeX + DomeR);

        // El cristal por dentro: más claro arriba a la izquierda, tramado.
        for (var y = top; y < CollarTop + 1; y++)
        {
            for (var x = left; x < right; x++)
            {
                var nx = (x + 0.5 - DomeX) / DomeR;
                var ny = (y + 0.5 - DomeY) / DomeR;
                var d = Math.Sqrt((nx * nx) + (ny * ny));
                if (d > 1) continue;

                var light = -(nx * 0.6) - (ny * 0.8) + (Bayer[y & 3, x & 3] / 16.0 * 0.5);
                Put(Canvas, x, y, light > 0.75 ? GlassLight : light > 0.05 ? Glass : GlassDeep);
            }
        }

        // Las balls, de arriba abajo, así que las de abajo tapan a las de arriba como en un montón.
        var taken = roll is null ? -1 : TakenBall(roll);
        foreach (var (ball, index) in _dome.Select((b, i) => (b, i)).OrderBy(p => p.b.Y))
        {
            if (index == taken && t >= CapsuleTimeline.ChurnEnd + 0.15) continue;

            var (bx, by) = (ball.X, ball.Y);

            // Recién elegido el banner, las balls caen dentro de la cúpula una tras otra.
            var delay = (ball.Row * 0.06) + ((index % 5) * 0.03);
            var fall = Math.Clamp((loadedFor - delay) / 0.28, 0, 1);
            if (fall <= 0) continue;
            if (fall < 1) by -= (1 - (fall * fall)) * (ball.Y - 8);

            if (roll is not null)
            {
                // Mientras se revuelven: cada una en su círculo y dando saltitos, más fuerte en medio del giro.
                var churn = Envelope(t, CapsuleTimeline.ChurnStart, CapsuleTimeline.ChurnEnd);
                bx += Math.Sin((t * 13) + ball.Phase) * 2.6 * churn;
                by -= Math.Abs(Math.Sin((t * 9) + (ball.Phase * 1.7))) * 4.5 * churn;
                if (Clack(t)) by -= 1;

                // La elegida baja por el agujero del fondo antes de salir por la trampilla.
                if (index == taken && t >= CapsuleTimeline.ChurnEnd - 0.05)
                {
                    var sink = Math.Clamp((t - (CapsuleTimeline.ChurnEnd - 0.05)) / 0.2, 0, 1);
                    by += sink * 7;
                }
            }

            DrawBall(Math.Round(bx * 2) / 2, Math.Round(by * 2) / 2, DomeBallR, ball.Angle, (CapsuleBall)ball.Tier,
                BallLook.Plain, InsideDome, Glass);
        }

        // Reflejos del cristal, encima de todo lo de dentro.
        var sweep = (clock % 7.0) / 0.7;
        for (var y = top; y < CollarTop + 1; y++)
        {
            for (var x = left; x < right; x++)
            {
                var dx = x + 0.5 - DomeX;
                var dy = y + 0.5 - DomeY;
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > DomeR) continue;

                var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

                if (r > DomeR - 1) Put(Canvas, x, y, Outline);
                else if (r > DomeR - 2) Put(Canvas, x, y, angle is > -170 and < -60 ? GlassShine : angle is > 20 and < 150 ? GlassDeep : GlassRim);
                else if (r > DomeR - 6 && r < DomeR - 3.4 && angle is > -158 and < -112) Put(Canvas, x, y, GlassShine);
                else if (r > DomeR - 5 && r < DomeR - 4 && angle is > -100 and < -88) Put(Canvas, x, y, GlassShine);
                else if (r > DomeR - 4 && r < DomeR - 3 && angle is > -15 and < 35 && (x + y) % 2 == 0) Put(Canvas, x, y, GlassRim);

                // Un brillo que cruza la cúpula cada siete segundos.
                if (sweep < 1)
                {
                    var band = ((dx + dy) / (DomeR * 2)) + 1 - (sweep * 2.4);
                    if (Math.Abs(band) < 0.018 && r < DomeR - 2) Put(Canvas, x, y, GlassShine);
                    else if (Math.Abs(band) < 0.045 && r < DomeR - 2 && (x + y) % 2 == 0) Put(Canvas, x, y, GlassRim);
                }
            }
        }

        // Un punto de luz duro.
        Rect(195, 18, 2, 3, White);
        Put(Canvas, 198, 15, White);
    }

    /// <summary>Which ball of the pile goes down the hole: the lowest-middle one of the kind the pull drops as.</summary>
    private int TakenBall(CapsuleRoll roll)
    {
        var tier = roll.Steps.Count > 0 ? roll.Steps[0] : 0;
        var best = -1;
        var bestScore = double.MaxValue;

        for (var i = 0; i < _dome.Count; i++)
        {
            if (_dome[i].Tier != tier) continue;
            var score = Math.Abs(_dome[i].X - DomeX) + ((CollarTop - _dome[i].Y) * 1.5);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    // ============================================================================================= the pull

    private void DrawPull(CapsuleBanner banner, CapsuleRoll roll, double t, double clock)
    {
        if (t < CapsuleTimeline.Exit) return;

        var climbs = Math.Max(0, roll.Steps.Count - 1);
        var shown = roll.Steps.Count == 0 ? 0 : roll.Steps[Math.Min(CapsuleTimeline.StepAt(t, climbs, roll.Seed), roll.Steps.Count - 1)];
        var final = roll.Steps.Count == 0 ? 0 : roll.Steps[^1];
        var tierColour = TierColours[Math.Clamp(final, 0, TierColours.Length - 1)];

        var (x, y, r, angle, squash) = BallPath(t, roll.Seed);

        // Sombra en el suelo, más pequeña cuanto más alta va la ball.
        if (t >= CapsuleTimeline.Exit + 0.25)
        {
            var height = RestFloor - (y + (r * squash));
            var width = Math.Max(3, (r * 1.5) - (height * 0.25));
            Ellipse(x, RestFloor - 0.5, width, 1.8, Shadow, dither: height > 4);
        }

        var dim = Math.Clamp((t - CapsuleTimeline.Open) / 0.4, 0, 1) * 0.62;
        if (dim > 0) Darken(dim);

        var open = Math.Clamp((t - CapsuleTimeline.Open) / 0.25, 0, 1);
        var closing = Math.Clamp((t - (CapsuleTimeline.Revealed - 0.1)) / 0.25, 0, 1);
        open *= 1 - closing;

        // La luz que sale al abrirse, con los rayos detrás de donde va a estar el Pokémon.
        if (t >= CapsuleTimeline.Open)
        {
            DrawRays(x, y - r - 26, t - CapsuleTimeline.Open - 0.2, tierColour, final, roll.Legendary);
            DrawBeam(x, y, t - CapsuleTimeline.Open, tierColour);
        }

        var blink = t is >= CapsuleTimeline.WobbleStart + (CapsuleTimeline.Wobbles * CapsuleTimeline.WobbleLength) and < CapsuleTimeline.Open
            && ((int)((t - CapsuleTimeline.WobbleEnd) / 0.1) % 2 == 0);

        // Abierta, la tapa va detrás del Pokémon; cerrada, la ball queda delante de sus pies.
        var ballBehind = open > 0;
        if (ballBehind) DrawBall(x, y, r, angle, (CapsuleBall)shown, new BallLook(0, open, squash, false));

        // El Pokémon: silueta blanca, luego al doble, y de golpe en su color.
        if (t >= CapsuleTimeline.Emerge && roll.Pokemon is { } sprite)
        {
            var scale = t < CapsuleTimeline.Emerge + 0.2 ? 1 : 2;
            var colour = Math.Clamp((t - (CapsuleTimeline.Emerge + 0.45)) / 0.12, 0, 1);
            var hop = t > CapsuleTimeline.Revealed + 0.4 ? HopAt(t - CapsuleTimeline.Revealed - 0.4) : 0;
            DrawPokemon(sprite, x, y - r + 1 - hop, scale, colour);
        }

        // Cada salto de ball: fogonazo blanco y un aro de chispas.
        var flash = 0.0;
        foreach (var w in CapsuleTimeline.UpgradeWobbles(climbs, roll.Seed))
        {
            var since = t - CapsuleTimeline.UpgradeAt(w);
            if (since is >= -0.08 and < 0.1) flash = 1 - (Math.Abs(since - 0.01) / 0.09);
            if (since is >= 0 and < 0.5) DrawSparkRing(x, y, r, since, TierColours[Math.Clamp(shown, 0, 4)]);
        }

        // Chispa falsa: en un meneo sin salto, a veces brilla y no pasa nada.
        for (var w = 0; w < CapsuleTimeline.Wobbles; w++)
        {
            if (CapsuleTimeline.UpgradeWobbles(climbs, roll.Seed).Contains(w) || Math.Abs((roll.Seed >> (w + 3)) % 5) > 1) continue;
            var since = t - CapsuleTimeline.UpgradeAt(w);
            if (since is >= 0 and < 0.3) Star(x - (r * 0.45), y - (r * 0.55), since < 0.15 ? 2 : 1, White, GoldLight);
        }

        if (!ballBehind) DrawBall(x, y, r, angle, (CapsuleBall)shown, new BallLook(flash, 0, squash, blink));

        if (t >= CapsuleTimeline.Emerge + 0.2)
        {
            DrawParticles(x, y - r, t - (CapsuleTimeline.Emerge + 0.2), tierColour, roll.Seed, roll.Legendary);
        }

        if (roll.Shiny && t >= CapsuleTimeline.Revealed)
        {
            DrawShinySparkles(x, y - r - 30, t - CapsuleTimeline.Revealed, roll.Seed);
        }
    }

    // ============================================================================================= the neon sign

    /// <summary>
    /// A neon Poké Ball and the word GACHA on the right wall, which is where the result card will open: it keeps that
    /// half of the room from being an empty wall while the machine waits, and it flickers now and then, as neon does.
    /// </summary>
    private void DrawNeonSign(double clock)
    {
        const double cx = 320.0;
        const double cy = 44.0;
        const double r = 19.0;

        var cycle = clock % 9.3;
        var off = cycle is > 4.0 and < 4.12 or > 4.22 and < 4.28;
        var core = off ? NeonGlow : Neon;
        var tube = off ? NeonHalo : NeonMid;

        for (var y = (int)(cy - r - 5); y <= (int)(cy + r + 5); y++)
        {
            for (var x = (int)(cx - r - 5); x <= (int)(cx + r + 5); x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var d = Math.Sqrt((dx * dx) + (dy * dy));

                var ring = Math.Abs(d - r);
                var band = Math.Abs(dy) < 1.0 && d < r && d > 6.5;
                var button = Math.Abs(d - 5.0) < 1.0;

                if (ring < 0.75 || band || button) Put(Canvas, x, y, ring < 0.75 && dy > 0 ? tube : core);
                else if (!off && (ring < 3.2 || (Math.Abs(dy) < 3.2 && d < r && d > 6.5) || Math.Abs(d - 5.0) < 2.6)
                         && Bayer[y & 3, x & 3] < 5) Put(Canvas, x, y, NeonGlow);
            }
        }

        const string word = "GACHA";
        var left = (int)cx - (((word.Length * 6) - 1) / 2);
        if (!off)
        {
            foreach (var (ox, oy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) BigText(word, left + ox, 72 + oy, NeonGlow);
        }

        BigText(word, left, 72, core);
    }

    // ============================================================================================= the shelf

    private const int ShelfLeft = 10;
    private const int ShelfRight = 108;
    private static readonly int[] ShelfPlanks = [62, 100];
    private static readonly Color ShelfTop = Rgb(0x5A, 0x48, 0x8A);
    private static readonly Color ShelfFace = Rgb(0x3A, 0x2E, 0x5C);
    private static readonly Color ShelfUnder = Rgb(0x1E, 0x18, 0x32);
    private static readonly Color Label = Rgb(0x6C, 0x64, 0x84);

    /// <summary>Where the figure of the <paramref name="slot"/>-th newest pull stands: its feet, in design cells.</summary>
    private static (double X, int Feet) ShelfSlot(int slot) =>
        (ShelfLeft + 13 + ((slot % 4) * 24.5), ShelfPlanks[Math.Min(1, slot / 4)] - 2);

    /// <summary>
    /// What has come out, as figures on a shelf: the last eight pulls, newest top left, each on a stand in its tier's colour.
    /// </summary>
    /// <remarks>
    /// It replaces the strip under the old reel and says the same thing, from the same history, inside the room instead of
    /// on top of it.
    /// </remarks>
    private void DrawShelf(IReadOnlyList<CapsuleShelfItem> shelf, double newestAge)
    {
        BigText("ÚLTIMAS TIRADAS", ShelfLeft + 5, 24, Outline);
        BigText("ÚLTIMAS TIRADAS", ShelfLeft + 4, 23, Label);

        foreach (var plank in ShelfPlanks)
        {
            Rect(ShelfLeft, plank, ShelfRight - ShelfLeft, 1, ShelfTop);
            Rect(ShelfLeft, plank + 1, ShelfRight - ShelfLeft, 2, ShelfFace);
            Rect(ShelfLeft, plank + 3, ShelfRight - ShelfLeft, 1, ShelfUnder);
            Border(ShelfLeft - 1, plank - 1, ShelfRight - ShelfLeft + 2, 6, Outline);

            // Escuadras.
            foreach (var bx in new[] { ShelfLeft + 6, ShelfRight - 8 })
            {
                for (var i = 0; i < 5; i++) Rect(bx, plank + 4 + i, 3 - (i / 2), 1, i == 0 ? ShelfFace : ShelfUnder);
            }

            // La sombra de la balda en la pared.
            for (var x = ShelfLeft; x < ShelfRight; x++)
            {
                if ((x + plank) % 2 == 0) Put(Canvas, x, plank + 5, Shadow);
            }
        }

        for (var i = 0; i < Math.Min(8, shelf.Count); i++)
        {
            var (cx, feet) = ShelfSlot(i);
            var drop = i == 0 && newestAge is >= 0 and < 0.35 ? (1 - (newestAge / 0.35)) : 0;
            var lift = (int)Math.Round(drop * drop * 14);

            var tier = TierColours[Math.Clamp(shelf[i].Tier, 0, 4)];
            var stand = (int)Math.Round(cx) - 6;
            Rect(stand, feet, 12, 1, Lerp(tier, White, 0.35));
            Rect(stand, feet + 1, 12, 1, Lerp(tier, Void, 0.3));
            Border(stand - 1, feet - 1, 14, 4, Outline);

            if (shelf[i].Sprite is { } sprite)
            {
                DrawPokemon(sprite, cx, feet - 1 - lift, 1, 1);
            }
        }
    }

    /// <summary>The ball of a pull that has had its time, flying from the mat to its place on the shelf.</summary>
    private void DrawDeparture(CapsuleRoll roll, double s)
    {
        var p = Math.Clamp(s / DepartLength, 0, 1);
        var ease = p < 0.5 ? 2 * p * p : 1 - (Math.Pow(-2 * p + 2, 2) / 2);
        var (tx, feet) = ShelfSlot(0);
        var fromY = RestFloor - HeroR;
        var x = RestX + ((tx - RestX) * ease);
        var y = fromY + ((feet - 6 - fromY) * ease) - (Math.Sin(p * Math.PI) * 38);
        var r = HeroR + ((5 - HeroR) * ease);
        var final = roll.Steps.Count == 0 ? 0 : roll.Steps[^1];

        // Una estela de chispas detrás.
        for (var k = 1; k <= 3; k++)
        {
            var q = Math.Clamp(p - (k * 0.06), 0, 1);
            var e = q < 0.5 ? 2 * q * q : 1 - (Math.Pow(-2 * q + 2, 2) / 2);
            Star(RestX + ((tx - RestX) * e), fromY + ((feet - 6 - fromY) * e) - (Math.Sin(q * Math.PI) * 38), k == 1 ? 1 : 0,
                Lerp(TierColours[Math.Clamp(final, 0, 4)], White, 0.5), TierColours[Math.Clamp(final, 0, 4)]);
        }

        DrawBall(x, y, r, p * 9, (CapsuleBall)Math.Clamp(final, 0, 4), BallLook.Plain);
    }

    /// <summary>Where the ball is: out of the chute, bouncing towards the viewer, settling, wobbling.</summary>
    private static (double X, double Y, double R, double Angle, double Squash) BallPath(double t, int seed)
    {
        var s = t - CapsuleTimeline.Exit;

        if (s < 0.25)
        {
            // Asoma por la trampilla.
            var p = s / 0.25;
            return (ChuteX, 117 + (p * 5), 5.5, p * 0.4, 1);
        }

        var rest = RestFloor - HeroR;
        var landX = ChuteX + 14;

        // El giro al rodar es la distancia entre el radio, contado para que en el centro de la alfombrilla quede recta.
        var turns = (landX - RestX) / HeroR;
        var landAngle = turns - (Math.PI * 2 * Math.Floor(turns / (Math.PI * 2)));
        var offset = landAngle - turns;

        if (s < 0.6)
        {
            // Cae de la bandeja al suelo, acercándose: crece.
            var p = (s - 0.25) / 0.35;
            var r = 5.5 + ((HeroR - 5.5) * p);
            var y = 122 + ((rest - 122) * p * p) - (Math.Sin(p * Math.PI) * 5);
            return (ChuteX + (p * 14), y, r, 0.4 + (p * (landAngle - 0.4)), 1);
        }

        // Rueda por el suelo hasta la alfombrilla, dando dos botes cada vez más bajos, y se pasa un poco.
        const double rollEnd = 1.72;
        var settle = CapsuleTimeline.Settled - CapsuleTimeline.Exit;
        var far = RestX + 9;

        if (s < rollEnd)
        {
            var q = (s - 0.6) / (rollEnd - 0.6);
            var x = landX + ((far - landX) * (1 - ((1 - q) * (1 - q))));
            var y = rest;
            var squash = 1.0;

            if (s < 1.0)
            {
                var p = (s - 0.6) / 0.4;
                y = rest - (Math.Sin(p * Math.PI) * 10);
                squash = p < 0.1 ? 0.8 : 1;
            }
            else if (s < 1.28)
            {
                var p = (s - 1.0) / 0.28;
                y = rest - (Math.Sin(p * Math.PI) * 4);
                squash = p < 0.12 ? 0.86 : 1;
            }
            else if (s < 1.34)
            {
                squash = 0.92;
            }

            return (x, y, HeroR, ((x - RestX) / HeroR) + offset, squash);
        }

        if (s < settle)
        {
            // Y vuelve al centro de la alfombrilla.
            var p = (s - rollEnd) / (settle - rollEnd);
            var back = 1 - Math.Pow(1 - p, 3);
            var x = far + ((RestX - far) * back);
            return (x, rest, HeroR, ((x - RestX) / HeroR) + offset, 1);
        }

        // Los tres meneos, girando sobre el punto de apoyo como una captura.
        for (var w = 0; w < CapsuleTimeline.Wobbles; w++)
        {
            var start = CapsuleTimeline.WobbleStart + (w * CapsuleTimeline.WobbleLength);
            var p = (t - start) / CapsuleTimeline.WobbleTilt;
            if (p is >= 0 and < 1)
            {
                var tilt = Math.Sin(p * Math.PI * 2) * 0.42 * (1 - (0.25 * p)) * (w % 2 == 0 ? 1 : -1);
                return (RestX + (Math.Sin(tilt) * HeroR), RestFloor - (Math.Cos(tilt) * HeroR), HeroR, tilt, 1);
            }
        }

        return (RestX, rest, HeroR, 0, 1);
    }

    private static CapsuleBall Dominant(CapsuleBanner banner)
    {
        var best = 0;
        for (var i = 1; i < banner.Odds.Count; i++)
        {
            if (banner.Odds[i] > banner.Odds[best]) best = i;
        }

        return (CapsuleBall)Math.Clamp(best, 0, 4);
    }
}
