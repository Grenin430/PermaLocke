using Color = PermaLocke.App.Views.PixelColour;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>When each thing happens as a caught Pokémon's card goes into the album, in seconds (§190).</summary>
public static class CatchTimeline
{
    /// <summary>The album has slid in from the right, closed.</summary>
    public const double AlbumIn = 0.45;

    public const double BallDrop = 0.1;
    public const double BallLand = 0.4;
    public const double WobbleStart = 0.45;
    public const double WobbleEnd = 0.85;

    /// <summary>The ball opens and the card starts coming out.</summary>
    public const double Open = 0.9;

    /// <summary>The card is out, face up and at its biggest.</summary>
    public const double Shown = 1.5;

    /// <summary>The album's cover swings open, in <see cref="Swing"/> seconds.</summary>
    public const double AlbumOpen = 1.65;

    public const double Swing = 0.3;

    /// <summary>The card leaves for its pocket.</summary>
    public const double Fly = 2.35;

    /// <summary>The card is in its pocket.</summary>
    public const double Inserted = 3.0;

    public const double AlbumClose = 3.15;

    /// <summary>The album slides out to the right, in <see cref="Slide"/> seconds.</summary>
    public const double AlbumOut = 3.5;

    public const double Slide = 0.45;

    /// <summary>Nothing left on screen.</summary>
    public const double Length = AlbumOut + Slide;
}

/// <summary>
/// A wild Pokémon just caught, as its card going into the album over the game (§190): a small album slides in from the
/// right, a Poké Ball drops where the trainer stands, wobbles and opens, the card comes out turning, shows itself, flies
/// into a pocket, and the album closes and goes.
/// </summary>
/// <remarks>
/// <para>
/// Drawn over the 3DS top screen and in its own pixels: the album and the ball are drawn cell by cell at the size of a
/// pixel of the game, so they look like part of it, and the card is the album's own (<see cref="TcgCardArt"/>) turned in
/// 3D by <see cref="HandScene"/>, without its table.
/// </para>
/// <para>
/// Pure, like the card in the hand: a moment makes a frame, premultiplied BGRA, transparent where the game shows
/// through. The window that shows it decides nothing.
/// </para>
/// </remarks>
public sealed class CatchScene
{
    public const int ScreenWidth = 400;
    public const int ScreenHeight = 240;

    /// <summary>Where the ball lands, in pixels of the game: where the trainer stands, in the middle of the field.</summary>
    private const double BallX = 200;
    private const double BallY = 150;

    /// <summary>Where the card shows itself, left of the album.</summary>
    private const double ShowX = 160;
    private const double ShowY = 118;

    /// <summary>The album at rest: its spine, in pixels of the game.</summary>
    private const double SpineX = 334;
    private const double AlbumTop = 82;

    /// <summary>Pixels of the game per cell of the album: two, so it reads as an album at a glance.</summary>
    private const double AlbumScale = 2;

    /// <summary>Pixels of the game per cell of the ball.</summary>
    private const double BallScale = 2;

    // El álbum: 60×38 píxeles del juego; el lomo en la columna 28, la hoja de la derecha de 32 a 58.
    private const int AlbumWidth = 60;
    private const int AlbumHeight = 38;
    private const int Spine = 28;
    private const int PageLeft = 32;
    private const int PageWidth = 27;

    /// <summary>The pocket the card goes into: the middle one of the right-hand page.</summary>
    private const int TargetColumn = 1;
    private const int TargetRow = 1;

    private static readonly Color Outline = Rgb(0x0B, 0x09, 0x10);
    private static readonly Color Leather = Rgb(0x3A, 0x26, 0x66);
    private static readonly Color LeatherLight = Rgb(0x5C, 0x44, 0x9C);
    private static readonly Color LeatherDark = Rgb(0x1A, 0x10, 0x30);
    private static readonly Color Stitch = Rgb(0x9A, 0x7C, 0xD8);
    private static readonly Color Gold = Rgb(0xD9, 0xA4, 0x41);
    private static readonly Color GoldHi = Rgb(0xFF, 0xF3, 0xC4);
    private static readonly Color GoldDark = Rgb(0x8E, 0x62, 0x1E);
    private static readonly Color Page = Rgb(0x1C, 0x15, 0x33);
    private static readonly Color PocketFill = Rgb(0x2A, 0x21, 0x4A);
    private static readonly Color PocketEdge = Rgb(0x6A, 0x5A, 0xA8);
    private static readonly Color White = Rgb(0xFA, 0xF8, 0xFF);
    private static readonly Color BallRed = Rgb(0xE8, 0x3A, 0x34);
    private static readonly Color BallRedLight = Rgb(0xFF, 0x7A, 0x6A);
    private static readonly Color BallRedDark = Rgb(0xA0, 0x20, 0x24);
    private static readonly Color BallWhite = Rgb(0xF0, 0xEC, 0xF8);
    private static readonly Color BallShade = Rgb(0xB8, 0xB2, 0xC8);
    private static readonly Color Band = Rgb(0x24, 0x20, 0x2C);

    /// <summary>The tiers' own colours, as the gacha paints them: the little cards in the pockets.</summary>
    private static readonly Color[] Tiers =
    [
        Rgb(0x7C, 0x85, 0x93), Rgb(0x5F, 0xA8, 0x6F), Rgb(0x4E, 0x8B, 0xC9), Rgb(0xD7, 0x5F, 0xA8), Rgb(0xD9, 0xA4, 0x41)
    ];

    private readonly HandScene _hand;
    private readonly CellCanvas _album = new(AlbumWidth, AlbumHeight);
    private readonly CellCanvas _front = new(PageWidth + 1, AlbumHeight);
    private readonly CellCanvas _inside = new(PageWidth + 1, AlbumHeight);
    private readonly CellCanvas _page = new(PageWidth + 1, AlbumHeight);
    private readonly CellCanvas _ball = new(16, 22);

    /// <param name="width">The top screen's width on the monitor, in pixels.</param>
    /// <param name="height">Its height.</param>
    /// <param name="pixel">Monitor pixels per pixel of the game.</param>
    public CatchScene(int width, int height, double pixel)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixel = Math.Max(0.5, pixel);
        Cell = Math.Max(1, (int)Math.Round(Pixel));
        Pixels = new byte[Width * Height * 4];
        _hand = new HandScene(Width, Height);
        Peak = Math.Max(1, (int)Math.Floor(Height * 0.62 / TcgCardArt.FullHeight));
        DrawCover();
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Monitor pixels per pixel of the game.</summary>
    public double Pixel { get; }

    /// <summary>The same, whole: the size of one cell of the album and the ball.</summary>
    public int Cell { get; }

    /// <summary>Monitor pixels per cell of the card at its biggest: whole, so its pixels stay square.</summary>
    public int Peak { get; }

    /// <summary>The frame, premultiplied BGRA: transparent where the game shows through.</summary>
    public byte[] Pixels { get; }

    /// <summary>Where the card goes, in monitor pixels: the middle of its pocket with the album at rest.</summary>
    public (double X, double Y) Pocket => Screen(
        SpineX + ((PocketLeft(PageLeft, TargetColumn) - Spine + 3.5) * AlbumScale), AlbumTop + ((PocketTop(TargetRow) + 5) * AlbumScale));

    /// <summary>Draws the moment <paramref name="t"/> seconds after the capture was counted.</summary>
    public void Render(TcgRender front, TcgRender back, TcgCard card, double t)
    {
        Array.Clear(Pixels);
        if (t < 0 || t >= CatchTimeline.Length)
        {
            return;
        }

        var tier = Tiers[Math.Clamp(card.Rarity, 0, Tiers.Length - 1)];

        // El álbum: entra cerrado, se abre, recibe la carta, se cierra y se va.
        var inserted = t >= CatchTimeline.Inserted;
        DrawAlbum(AlbumAngle(t), inserted, tier, t - CatchTimeline.Inserted, card.Seed);
        var (slide, bump) = AlbumOffset(t);
        Blit(_album, SpineX - (Spine * AlbumScale) + slide, AlbumTop - bump, AlbumScale);

        // La ball: cae donde está el entrenador, se menea, se abre y se va.
        if (t is >= CatchTimeline.BallDrop and < CatchTimeline.Shown)
        {
            DrawBallFrame(t);
        }

        // La carta, de la ball a su funda.
        if (t >= CatchTimeline.Open && !inserted)
        {
            _hand.Render(front, back, CardPose(t), t, card.Seed, t - CatchTimeline.Shown, surroundings: false);
            Over(_hand.Pixels, _hand.Dirty);
        }
        else
        {
            _hand.Clear();
        }

        if (inserted && t < CatchTimeline.Inserted + 0.35)
        {
            // Destellos alrededor de la funda llena.
            var (px, py) = Pocket;
            var since = t - CatchTimeline.Inserted;
            for (var i = 0; i < 6; i++)
            {
                var angle = (i / 6.0 * Math.PI * 2) + 0.4;
                var r = (6 + (since * 40)) * Pixel;
                Star(px + (Math.Cos(angle) * r), py - (bump * Pixel) + (Math.Sin(angle) * r * 0.8), since < 0.18 ? 2 : 1,
                    since < 0.15 ? White : GoldHi);
            }
        }
    }

    // ================================================================================================== THE CARD

    private HandPose CardPose(double t)
    {
        var (ballX, ballY) = Screen(BallX, BallY - 6);
        var (showX, showY) = Screen(ShowX, ShowY);

        if (t < CatchTimeline.Shown)
        {
            // Sale de la ball dando vuelta y media, creciendo con un pequeño rebote, y acaba de cara.
            var p = (t - CatchTimeline.Open) / (CatchTimeline.Shown - CatchTimeline.Open);
            var move = 1 - Math.Pow(1 - p, 3);
            var grow = BackOut(p, 1.4);
            return new HandPose((1 - move) * Math.PI * 3, 0, 0, Math.Max(0.3, Peak * (0.1 + (0.9 * grow))),
                ballX + ((showX - ballX) * move), ballY + ((showY - ballY) * move), 0.8);
        }

        if (t < CatchTimeline.Fly)
        {
            // Se deja ver, meciéndose.
            var s = t - CatchTimeline.Shown;
            return new HandPose(Math.Sin(s * 4) * 0.14, Math.Cos(s * 3) * 0.06, 0, Peak, showX, showY - (Math.Sin(s * 3) * Pixel), 0.8);
        }

        // A su funda en arco, encogiendo hasta caber.
        var q = Math.Clamp((t - CatchTimeline.Fly) / (CatchTimeline.Inserted - CatchTimeline.Fly), 0, 1);
        var eased = q * q * (3 - (2 * q));
        var (pocketX, pocketY) = Pocket;
        var pocketScale = 10 * AlbumScale * Pixel / TcgCardArt.FullHeight;
        return new HandPose(-0.3 * Math.Sin(q * Math.PI), 0, Math.Sin(q * Math.PI) * 0.35,
            Peak + ((pocketScale - Peak) * Math.Pow(eased, 0.7)),
            showX + ((pocketX - showX) * eased),
            showY + ((pocketY - showY) * eased) - (Math.Sin(q * Math.PI) * Height * 0.14), 0.8);
    }

    private static double BackOut(double t, double overshoot)
    {
        var u = Math.Clamp(t, 0, 1) - 1;
        return 1 + (u * u * (((overshoot + 1) * u) + overshoot));
    }

    // ================================================================================================== THE ALBUM

    /// <summary>How far the cover has swung round its spine: 0 closed, π open.</summary>
    private static double AlbumAngle(double t)
    {
        if (t < CatchTimeline.AlbumOpen) return 0;
        if (t < CatchTimeline.AlbumOpen + CatchTimeline.Swing) return Ease((t - CatchTimeline.AlbumOpen) / CatchTimeline.Swing) * Math.PI;
        if (t < CatchTimeline.AlbumClose) return Math.PI;
        if (t < CatchTimeline.AlbumClose + CatchTimeline.Swing) return (1 - Ease((t - CatchTimeline.AlbumClose) / CatchTimeline.Swing)) * Math.PI;
        return 0;
    }

    /// <summary>How far right of its place the album is, and how far up it hops, in pixels of the game.</summary>
    private static (double Slide, double Bump) AlbumOffset(double t)
    {
        const double away = 130;
        double slide;
        if (t < CatchTimeline.AlbumIn) slide = away * Math.Pow(1 - (t / CatchTimeline.AlbumIn), 3);
        else if (t < CatchTimeline.AlbumOut) slide = 0;
        else slide = away * Math.Pow((t - CatchTimeline.AlbumOut) / CatchTimeline.Slide, 2);

        // A saltos de un píxel, como un sprite: al llegar, al recibir la carta y al cerrarse.
        var since = t - CatchTimeline.Inserted;
        var bump = since is >= 0 and < 0.12 ? 2 : since is >= 0.12 and < 0.2 ? 1 : 0;
        if (t is >= CatchTimeline.AlbumIn and < CatchTimeline.AlbumIn + 0.08) bump = 1;
        return (Math.Round(slide), bump);
    }

    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static int PocketLeft(int pageLeft, int column) => pageLeft + 1 + (column * 8);

    private static int PocketTop(int row) => 3 + (row * 11);

    /// <summary>The front of the cover, drawn once: leather, stitching, gold corners and a Poké Ball in gold.</summary>
    private void DrawCover()
    {
        var c = _front;
        c.Rect(0, 0, c.Width, c.Height, Outline);
        c.Rect(0, 1, c.Width - 1, c.Height - 2, Leather);

        for (var y = 2; y < c.Height - 2; y++)
        {
            for (var x = 0; x < c.Width - 2; x++)
            {
                if (Hash(x, y, 11) < 0.12) c.Put(x, y, LeatherLight);
                else if (Hash(x, y, 12) < 0.1) c.Put(x, y, LeatherDark);
            }
        }

        c.Rect(0, 1, c.Width - 1, 1, LeatherLight);
        c.Rect(c.Width - 2, 1, 1, c.Height - 2, LeatherDark);

        // Pespunte a dos píxeles del borde.
        for (var x = 2; x < c.Width - 3; x += 2)
        {
            c.Put(x, 3, Stitch);
            c.Put(x, c.Height - 4, Stitch);
        }

        for (var y = 3; y < c.Height - 3; y += 2)
        {
            c.Put(c.Width - 4, y, Stitch);
        }

        // Cantoneras doradas en las esquinas de fuera.
        foreach (var (x, y, dx, dy) in new[] { (c.Width - 2, 1, -1, 1), (c.Width - 2, c.Height - 2, -1, -1) })
        {
            for (var i = 0; i < 4; i++)
            {
                c.Put(x + (dx * i), y, i == 0 ? GoldHi : Gold);
                c.Put(x, y + (dy * i), Gold);
            }

            c.Put(x + dx, y + dy, GoldDark);
        }

        // La Poké Ball del centro, en oro, de 11×11.
        const int cx = 13;
        const int cy = 18;
        for (var y = -6; y <= 6; y++)
        {
            for (var x = -6; x <= 6; x++)
            {
                var d = Math.Sqrt((x * x) + (y * y));
                if (d > 5.6) continue;

                var colour = d > 4.6 ? GoldDark
                    : Math.Abs(y) <= 0 && Math.Abs(x) > 1 ? GoldDark
                    : d < 1.6 ? (d < 0.8 ? GoldHi : GoldDark)
                    : y < 0 ? (x + y < -3 ? GoldHi : Gold) : Mix(Gold, Leather, 0.45);
                c.Put(cx + x, cy + y, colour);
            }
        }

        // La cara de dentro de la tapa es la hoja de la izquierda, con sus fundas.
        DrawPage(_inside, 1, filled: null, Tiers[0], 7);
    }

    /// <summary>A page of pockets, most of them already holding a card; <paramref name="filled"/> is the target's card.</summary>
    private static void DrawPage(CellCanvas c, int left, Color? filled, Color _, uint seed)
    {
        c.Clear();
        c.Rect(0, 0, c.Width, c.Height, Outline);
        c.Rect(left == 1 ? 1 : 0, 1, c.Width - 1, c.Height - 2, Page);

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var x = left + 1 + (column * 8);
                var y = PocketTop(row);
                c.Rect(x, y, 7, 10, PocketFill);
                c.Rect(x, y, 7, 1, PocketEdge);

                var target = filled is not null || (left != 1 && column == TargetColumn && row == TargetRow);
                if (left != 1 && column == TargetColumn && row == TargetRow)
                {
                    if (filled is { } card) MiniCard(c, x, y, card);
                    continue;
                }

                // Las demás fundas, llenas casi todas: se ve que es una colección.
                if (!target || left == 1)
                {
                    if (Hash(column, row + (left * 3), seed) < 0.78)
                    {
                        MiniCard(c, x, y, Tiers[(int)(Hash(row, column + left, seed + 5) * 3.99)]);
                    }
                }
            }
        }
    }

    /// <summary>A little card in a pocket: a white frame, its colour, a lighter strip where its picture is.</summary>
    private static void MiniCard(CellCanvas c, int x, int y, Color colour)
    {
        c.Rect(x + 1, y + 1, 5, 8, White);
        c.Rect(x + 2, y + 2, 3, 6, colour);
        c.Rect(x + 2, y + 3, 3, 2, Mix(colour, White, 0.5));
    }

    private void DrawAlbum(double angle, bool inserted, Color tier, double sinceInsert, uint seed)
    {
        _album.Clear();

        // La hoja de la derecha, con la funda vacía hasta que entra la carta.
        if (angle > 0.001 || inserted)
        {
            DrawPage(_page, 0, inserted ? tier : null, tier, seed | 1);

            // La funda recién llena brilla un momento.
            if (inserted && sinceInsert < 0.3)
            {
                var x = 1 + (TargetColumn * 8);
                var y = PocketTop(TargetRow);
                if (Dither(0, 0, 1 - (sinceInsert / 0.3))) _page.Rect(x + 1, y + 1, 5, 8, GoldHi);
            }

            _album.Stamp(_page, PageLeft, 0);
        }

        // El lomo, con sus anillas.
        _album.Rect(Spine - 1, 0, 6, AlbumHeight, Outline);
        _album.Rect(Spine, 1, 4, AlbumHeight - 2, LeatherDark);
        _album.Rect(Spine + 1, 1, 1, AlbumHeight - 2, Leather);
        foreach (var y in new[] { 7, 18, 29 })
        {
            _album.Rect(Spine - 1, y, 6, 2, Gold);
            _album.Put(Spine, y, GoldHi);
            _album.Rect(Spine - 1, y + 2, 6, 1, GoldDark);
        }

        // La tapa girando sobre el lomo: por delante mientras va hacia arriba, y la hoja de dentro al caer al otro lado.
        var width = (int)Math.Round(PageWidth * Math.Abs(Math.Cos(angle)));
        if (width <= 0)
        {
            return;
        }

        if (angle < Math.PI / 2)
        {
            _album.StampColumns(_front, PageLeft, 0, width + 1);
        }
        else
        {
            _album.StampColumns(_inside, Spine - width, 0, width + 1);
        }
    }

    // ================================================================================================== THE BALL

    private void DrawBallFrame(double t)
    {
        var c = _ball;
        c.Clear();

        // Cae desde arriba y bota dos veces.
        double fall;
        if (t < CatchTimeline.BallLand)
        {
            var p = (t - CatchTimeline.BallDrop) / (CatchTimeline.BallLand - CatchTimeline.BallDrop);
            fall = -(1 - (p * p)) * 60;
        }
        else
        {
            var s = t - CatchTimeline.BallLand;
            fall = s < 0.12 ? -Math.Sin(s / 0.12 * Math.PI) * 5 : s < 0.2 ? -Math.Sin((s - 0.12) / 0.08 * Math.PI) * 2 : 0;
        }

        // Dos meneos, como al capturar.
        var tilt = 0.0;
        if (t is >= CatchTimeline.WobbleStart and < CatchTimeline.WobbleEnd)
        {
            var w = (t - CatchTimeline.WobbleStart) / (CatchTimeline.WobbleEnd - CatchTimeline.WobbleStart);
            tilt = Math.Sin(w * Math.PI * 2) * 0.45;
        }

        var open = Math.Clamp((t - CatchTimeline.Open) / 0.12, 0, 1);

        // Ya abierta, se hace pequeña y se va.
        var radius = 5.6;
        if (t > CatchTimeline.Open + 0.35)
        {
            radius *= Math.Max(0, 1 - ((t - (CatchTimeline.Open + 0.35)) / 0.25));
        }

        if (radius > 1)
        {
            Ball(c, 8, 15, radius, tilt, open);
        }

        Blit(c, BallX - (8 * BallScale), BallY - (15 * BallScale) + Math.Round(fall), BallScale);

        // El fogonazo al abrirse: rayos blancos desde la ball.
        var since = t - CatchTimeline.Open;
        if (since is >= 0 and < 0.3)
        {
            var (bx, by) = Screen(BallX, BallY - 2);
            var reach = (4 + (since * 60)) * Pixel;
            for (var i = 0; i < 8; i++)
            {
                var angle = (i / 8.0 * Math.PI * 2) + 0.2;
                for (var d = 3 * Pixel; d < reach; d += Cell)
                {
                    var fade = 1 - (d / reach);
                    if (!Dither((int)(d / Cell), i, fade * (1 - (since / 0.3)) + 0.2)) continue;
                    Block(bx + (Math.Cos(angle) * d), by + (Math.Sin(angle) * d * 0.8), White);
                }
            }
        }
    }

    /// <summary>A Poké Ball of <paramref name="r"/> pixels, tilted, with its top lifted as it opens and light inside.</summary>
    private static void Ball(CellCanvas c, double cx, double cy, double r, double tilt, double open)
    {
        var (sin, cos) = Math.SinCos(tilt);
        var lift = open * 5;

        for (var y = 0; y < c.Height; y++)
        {
            for (var x = 0; x < c.Width; x++)
            {
                foreach (var top in open > 0 ? new[] { true, false } : [false])
                {
                    var dx = x + 0.5 - cx;
                    var dy = y + 0.5 - cy + (top ? lift : 0);
                    var d = Math.Sqrt((dx * dx) + (dy * dy)) / r;
                    if (d > 1) continue;

                    var u = (dx * cos) + (dy * sin);
                    var v = ((-dx * sin) + (dy * cos)) / r;

                    if (open > 0)
                    {
                        if (top && v > 0) continue;
                        if (!top && v < 0)
                        {
                            // El hueco de la ball abierta: luz.
                            if (v > -0.35) c.Put(x, y, White);
                            continue;
                        }
                    }

                    var colour = d > 0.84 ? Outline
                        : Math.Abs(v) < 0.14 ? Band
                        : Math.Sqrt((u * u) + (v * r * v * r)) < 1.7 && open <= 0 ? (Math.Sqrt((u * u) + (v * r * v * r)) < 1.0 ? White : Band)
                        : v < 0 ? (u + (v * r) < -3.2 ? BallRedLight : u > 2.5 ? BallRedDark : BallRed)
                        : (u > 2 ? BallShade : BallWhite);
                    c.Put(x, y, colour);
                }
            }
        }
    }

    // ================================================================================================== PIXELS

    /// <summary>Monitor pixels for a point in pixels of the game.</summary>
    private (double X, double Y) Screen(double x, double y) => (x * Pixel, y * Pixel);

    /// <summary>A canvas at <paramref name="scale"/> pixels of the game per cell, each cell a whole block of the monitor.</summary>
    private void Blit(CellCanvas canvas, double left, double top, double scale)
    {
        var sx = (int)Math.Round(left * Pixel);
        var sy = (int)Math.Round(top * Pixel);
        var block = Math.Max(1, (int)Math.Round(Pixel * scale));

        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                var at = ((y * canvas.Width) + x) * 4;
                if (canvas.Bgra[at + 3] == 0) continue;
                Fill(sx + (x * block), sy + (y * block), canvas.Bgra[at], canvas.Bgra[at + 1], canvas.Bgra[at + 2], block);
            }
        }
    }

    private void Block(double x, double y, Color colour) =>
        Fill((int)Math.Round(x - (Cell / 2.0)), (int)Math.Round(y - (Cell / 2.0)), colour.B, colour.G, colour.R, Cell);

    private void Fill(int left, int top, byte b, byte g, byte r, int size)
    {
        for (var y = Math.Max(0, top); y < Math.Min(Height, top + size); y++)
        {
            for (var x = Math.Max(0, left); x < Math.Min(Width, left + size); x++)
            {
                var at = ((y * Width) + x) * 4;
                Pixels[at] = b;
                Pixels[at + 1] = g;
                Pixels[at + 2] = r;
                Pixels[at + 3] = 255;
            }
        }
    }

    /// <summary>A four-point star of blocks.</summary>
    private void Star(double x, double y, int size, Color colour)
    {
        Block(x, y, colour);
        for (var i = 1; i <= size; i++)
        {
            Block(x + (i * Cell), y, colour);
            Block(x - (i * Cell), y, colour);
            Block(x, y + (i * Cell), colour);
            Block(x, y - (i * Cell), colour);
        }
    }

    /// <summary>Lays premultiplied pixels of the same size over the frame, inside a rectangle.</summary>
    private void Over(byte[] source, (int X, int Y, int Width, int Height) rect)
    {
        var x0 = Math.Max(0, rect.X);
        var y0 = Math.Max(0, rect.Y);
        var x1 = Math.Min(Width, rect.X + rect.Width);
        var y1 = Math.Min(Height, rect.Y + rect.Height);

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var at = ((y * Width) + x) * 4;
                var a = source[at + 3];
                if (a == 0) continue;

                if (a == 255)
                {
                    Pixels[at] = source[at];
                    Pixels[at + 1] = source[at + 1];
                    Pixels[at + 2] = source[at + 2];
                    Pixels[at + 3] = 255;
                    continue;
                }

                var keep = 255 - a;
                Pixels[at] = (byte)(source[at] + ((Pixels[at] * keep) / 255));
                Pixels[at + 1] = (byte)(source[at + 1] + ((Pixels[at + 1] * keep) / 255));
                Pixels[at + 2] = (byte)(source[at + 2] + ((Pixels[at + 2] * keep) / 255));
                Pixels[at + 3] = (byte)(a + ((Pixels[at + 3] * keep) / 255));
            }
        }
    }
}
