using Color = PermaLocke.App.Views.PixelColour;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>One page of the album: its pockets in reading order, a card or an empty one each.</summary>
/// <param name="Caption">What is printed at the top of the page, «CAJA 3 · CAMPO»; empty on the right page.</param>
/// <param name="Number">Its page number within the box, printed in its top corner; 0 for none.</param>
public sealed record AlbumPage(IReadOnlyList<TcgCard?> Pockets, string Caption = "", int Number = 0)
{
    public static AlbumPage Empty(int pockets) => new(new TcgCard?[pockets]);
}

/// <summary>An index tab on the edge of the album, one per box: what it says and whether it is the one open.</summary>
public sealed record AlbumTab(string Label, bool IsParty);

/// <summary>The two pages open in front of the player, and which spread of the album they are.</summary>
/// <param name="Index">Position of the spread in the whole album, so a change knows which way to turn.</param>
/// <param name="Tabs">The index tabs down the edge, in order; empty for none.</param>
/// <param name="ActiveTab">The tab of the box open now, pulled out further than the rest.</param>
public sealed record AlbumSpread(AlbumPage Left, AlbumPage Right, TcgLayout Layout, int Index,
    IReadOnlyList<AlbumTab>? Tabs = null, int ActiveTab = -1)
{
    public IReadOnlyList<AlbumTab> TabList => Tabs ?? [];
}

/// <summary>A page turning: the spread it leaves, which way it goes, and how far it has got, from 0 to 1.</summary>
public sealed record AlbumTurn(AlbumSpread From, bool Forward, double Progress);

/// <summary>
/// The album open on the desk (§186, redesigned in §187), cell by cell: bound in violet leather with its stitching and
/// gold corner guards, two pages of plastic pockets under a desk lamp, the rings of the spine, an index tab per box on
/// its edge, and the page that turns in perspective when the player goes on.
/// </summary>
/// <remarks>
/// <para>
/// Pure, like the other scenes: it is given the spread, the moment, the pocket or the tab under the mouse, and paints.
/// Pages and cards that do not move are drawn once and kept; each frame copies them and adds what moves — the finishes
/// of the cards, the embers, a glint running over the plastic now and then, the card lifted under the mouse, the page
/// turning.
/// </para>
/// <para>
/// The cover and its tabs take room. <paramref name="dressed"/> false leaves them out, for a window where the album
/// would otherwise have to drop to one screen pixel per cell: the pages are what must fit.
/// </para>
/// <para>
/// The lamp is baked into a map of light once for the size of the scene: a little brighter at the top left, darker
/// towards the far corners, in dithered steps. It falls on everything, cards included, as a real lamp does.
/// </para>
/// </remarks>
public sealed class AlbumScene
{
    /// <summary>How long a page takes to turn.</summary>
    public const double TurnLength = 0.62;

    private const int Margin = 1;
    private const int SideMargin = 6;
    private const int TopMargin = 8;
    private const int BottomMargin = 3;
    private const int Gap = 2;
    private const int Spine = 12;
    private const int Cover = 4;
    private const int TabRoom = 12;

    private static readonly Color PageColour = Rgb(0x17, 0x11, 0x2B);
    private static readonly Color PageTexture = Rgb(0x1C, 0x15, 0x33);
    private static readonly Color PageEdgeLight = Rgb(0x34, 0x28, 0x58);
    private static readonly Color PageEdgeDark = Rgb(0x0A, 0x07, 0x14);
    private static readonly Color PocketColour = Rgb(0x21, 0x19, 0x3D);
    private static readonly Color PocketEdge = Rgb(0x33, 0x29, 0x5A);
    private static readonly Color PocketLip = Rgb(0x4A, 0x3C, 0x7C);
    private static readonly Color Emblem = Rgb(0x2A, 0x21, 0x4A);
    private static readonly Color Caption = Rgb(0x9A, 0x8A, 0xC8);
    private static readonly Color CaptionDim = Rgb(0x5C, 0x4E, 0x88);
    private static readonly Color SpineColour = Rgb(0x0C, 0x09, 0x16);
    private static readonly Color SpineLight = Rgb(0x1E, 0x17, 0x34);
    private static readonly Color Leather = Rgb(0x2C, 0x1D, 0x4C);
    private static readonly Color LeatherGrain = Rgb(0x35, 0x24, 0x5A);
    private static readonly Color LeatherLight = Rgb(0x4E, 0x3A, 0x84);
    private static readonly Color LeatherDark = Rgb(0x12, 0x0B, 0x22);
    private static readonly Color Stitch = Rgb(0x9A, 0x7C, 0xD8);
    private static readonly Color GoldHi = Rgb(0xFF, 0xF3, 0xC4);
    private static readonly Color Gold = Rgb(0xD9, 0xA4, 0x41);
    private static readonly Color GoldDark = Rgb(0x8E, 0x62, 0x1E);
    private static readonly Color ChromeHi = Rgb(0xF2, 0xEE, 0xFA);
    private static readonly Color ChromeLight = Rgb(0xC4, 0xBE, 0xD8);
    private static readonly Color ChromeMid = Rgb(0x8E, 0x88, 0xA6);
    private static readonly Color ChromeDark = Rgb(0x4C, 0x46, 0x64);
    private static readonly Color Hole = Rgb(0x06, 0x04, 0x0C);
    private static readonly Color Accent = Rgb(0xB0, 0x7B, 0xF0);
    private static readonly Color AccentLight = Rgb(0xD2, 0xAD, 0xFF);
    private static readonly Color Ink = Rgb(0x1C, 0x16, 0x26);
    private static readonly Color Black = Rgb(0x06, 0x05, 0x0B);
    private static readonly Color White = Rgb(0xFF, 0xFF, 0xFF);

    /// <summary>The tabs' colours, one after another like a set of coloured dividers.</summary>
    private static readonly Color[] TabColours =
    [
        Rgb(0xE8, 0x8A, 0x9A), Rgb(0xF0, 0xB0, 0x6A), Rgb(0xEC, 0xD8, 0x6A), Rgb(0x8E, 0xD0, 0x86),
        Rgb(0x6E, 0xC8, 0xC4), Rgb(0x7E, 0xA6, 0xEC), Rgb(0xB0, 0x8E, 0xEC), Rgb(0xE0, 0x90, 0xD4)
    ];

    private readonly Dictionary<TcgCard, TcgRender> _cards = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AlbumPage, CellCanvas> _pages = new(ReferenceEqualityComparer.Instance);
    private readonly float[] _light;
    private CellCanvas? _cover;

    public AlbumScene(TcgLayout layout, bool dressed = true)
    {
        Layout = layout;
        Dressed = dressed;
        Columns = layout == TcgLayout.Full ? 3 : 4;
        (CardWidth, CardHeight) = TcgCardArt.SizeOf(layout);
        PocketWidth = CardWidth + (Margin * 2);
        PocketHeight = CardHeight + (Margin * 2);
        PageWidth = (SideMargin * 2) + (Columns * PocketWidth) + ((Columns - 1) * Gap);
        PageHeight = TopMargin + BottomMargin + (Columns * PocketHeight) + ((Columns - 1) * Gap);

        var border = dressed ? Cover : 0;
        PageTop = border;
        LeftPageX = border;
        RightPageX = LeftPageX + PageWidth + Spine;
        Width = RightPageX + PageWidth + border + (dressed ? TabRoom : 0);
        Height = PageHeight + (border * 2);
        Canvas = new CellCanvas(Width, Height);
        _light = LightMap(Width, Height);
    }

    public TcgLayout Layout { get; }

    /// <summary>With the leather cover and the tabs.</summary>
    public bool Dressed { get; }

    /// <summary>Pockets across and down a page: 3 or 4.</summary>
    public int Columns { get; }

    public int PocketsPerPage => Columns * Columns;

    public int CardWidth { get; }

    public int CardHeight { get; }

    public int PocketWidth { get; }

    public int PocketHeight { get; }

    public int PageWidth { get; }

    public int PageHeight { get; }

    /// <summary>Where the pages start, inside the cover.</summary>
    public int PageTop { get; }

    public int LeftPageX { get; }

    public int RightPageX { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame after the last <see cref="Render"/>.</summary>
    public CellCanvas Canvas { get; }

    /// <summary>The scene's width with the cover and the tabs, before building one: to know whether it fits.</summary>
    public static (int Width, int Height) SizeOf(TcgLayout layout, bool dressed)
    {
        var columns = layout == TcgLayout.Full ? 3 : 4;
        var (cardWidth, cardHeight) = TcgCardArt.SizeOf(layout);
        var pageWidth = (SideMargin * 2) + (columns * (cardWidth + (Margin * 2))) + ((columns - 1) * Gap);
        var pageHeight = TopMargin + BottomMargin + (columns * (cardHeight + (Margin * 2))) + ((columns - 1) * Gap);
        var border = dressed ? Cover : 0;
        return ((pageWidth * 2) + Spine + (border * 2) + (dressed ? TabRoom : 0), pageHeight + (border * 2));
    }

    // ====================================================================================================== HIT

    /// <summary>
    /// The pocket at a cell of the scene: 0 to <see cref="PocketsPerPage"/> − 1 on the left page, the rest on the right,
    /// or −1 between pockets, on the spine, the cover or outside.
    /// </summary>
    public int PocketAt(int x, int y)
    {
        var page = x >= LeftPageX && x < LeftPageX + PageWidth ? 0 : x >= RightPageX && x < RightPageX + PageWidth ? 1 : -1;
        if (page < 0 || y < PageTop || y >= PageTop + PageHeight)
        {
            return -1;
        }

        var px = x - (page == 0 ? LeftPageX : RightPageX) - SideMargin;
        var py = y - PageTop - TopMargin;
        if (px < 0 || py < 0)
        {
            return -1;
        }

        var column = px / (PocketWidth + Gap);
        var row = py / (PocketHeight + Gap);
        if (column >= Columns || row >= Columns || px % (PocketWidth + Gap) >= PocketWidth || py % (PocketHeight + Gap) >= PocketHeight)
        {
            return -1;
        }

        return (page * PocketsPerPage) + (row * Columns) + column;
    }

    /// <summary>The top-left cell of a pocket on its own page.</summary>
    public (int X, int Y) PocketOrigin(int pocket)
    {
        var index = pocket % PocketsPerPage;
        return (SideMargin + ((index % Columns) * (PocketWidth + Gap)), TopMargin + ((index / Columns) * (PocketHeight + Gap)));
    }

    /// <summary>The top-left cell of a pocket in the scene, for where a card comes out from.</summary>
    public (int X, int Y) PocketInScene(int pocket)
    {
        var (px, py) = PocketOrigin(pocket);
        return ((pocket < PocketsPerPage ? LeftPageX : RightPageX) + px, PageTop + py);
    }

    /// <summary>The tab at a cell of the scene, or −1.</summary>
    public int TabAt(int x, int y, int tabs)
    {
        if (!Dressed || tabs <= 0)
        {
            return -1;
        }

        for (var i = 0; i < tabs; i++)
        {
            var (tx, ty, tw, th) = TabRect(i, tabs, active: true);
            if (x >= tx && x < tx + tw && y >= ty && y < ty + th)
            {
                return i;
            }
        }

        return -1;
    }

    private (int X, int Y, int W, int H) TabRect(int index, int count, bool active)
    {
        var room = Height - 16;
        var height = Math.Clamp((room / Math.Max(1, count)) - 1, 9, 24);
        var top = 8 + (index * (height + 1));
        var coverRight = RightPageX + PageWidth + Cover;
        var width = active ? TabRoom : TabRoom - 3;
        return (coverRight - 3, top, width + 3, height);
    }

    // ====================================================================================================== RENDER

    /// <summary>
    /// Paints the spread at <paramref name="seconds"/>. With <paramref name="turn"/> the page turning is drawn over
    /// the pages it leaves and the ones it uncovers.
    /// </summary>
    /// <param name="hover">The pocket under the mouse: its card lifts off the page and the pocket lights up.</param>
    /// <param name="hoverTab">The tab under the mouse, which lights up.</param>
    public void Render(AlbumSpread spread, double seconds, int hover = -1, AlbumTurn? turn = null, int hoverTab = -1)
    {
        Canvas.Clear();

        if (Dressed)
        {
            Canvas.Stamp(CoverCanvas(), 0, 0);
            Tabs(spread, hoverTab);
        }

        if (turn is { } turning && turning.Progress < 1)
        {
            RenderTurn(spread, turning, seconds);
        }
        else
        {
            DrawPage(spread.Left, LeftPageX, seconds, hover < PocketsPerPage ? hover : -1);
            DrawPage(spread.Right, RightPageX, seconds, hover >= PocketsPerPage ? hover - PocketsPerPage : -1);
            DrawSpine();
            Glint(seconds);
            if (Dressed)
            {
                CornerGuards();
            }

            // La luz de la lámpara cae sobre todo; la carta levantada va después, por encima, más cerca de ella.
            ApplyLight();
            if (hover >= 0)
            {
                Lifted(spread, hover, seconds);
            }

            return;
        }

        if (Dressed)
        {
            CornerGuards();
        }

        ApplyLight();
    }

    /// <summary>Forgets the drawings kept for pages and cards that are no longer in the album.</summary>
    public void Forget()
    {
        _cards.Clear();
        _pages.Clear();
    }

    // ====================================================================================================== COVER

    /// <summary>The leather cover, stitched, with gold guards on its corners, drawn once.</summary>
    private CellCanvas CoverCanvas()
    {
        if (_cover is { } kept)
        {
            return kept;
        }

        var width = RightPageX + PageWidth + Cover;
        var cover = new CellCanvas(width, Height);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var edge = x == 0 || y == 0 || x == width - 1 || y == Height - 1;
                var corner = (x == 0 || x == width - 1) && (y == 0 || y == Height - 1);
                if (corner)
                {
                    continue;
                }

                Color colour;
                if (edge)
                {
                    colour = x == 0 || y == 0 ? LeatherLight : LeatherDark;
                }
                else
                {
                    // Grano de piel: un ruido suave a tramado, sin repetirse a la vista.
                    var grain = Hash(x / 2, y / 2, 91) < 0.35 && Dither(x, y, 0.5);
                    colour = grain ? LeatherGrain : Leather;
                }

                cover.Put(x, y, colour);
            }
        }

        // El pespunte, a dos celdas del borde: tres puntadas y dos de hueco.
        for (var x = 2; x < width - 2; x++)
        {
            if (x % 5 < 3)
            {
                cover.Put(x, 2, Stitch);
                cover.Put(x, Height - 3, Mix(Stitch, Black, 0.3));
            }
        }

        for (var y = 2; y < Height - 2; y++)
        {
            if (y % 5 < 3)
            {
                cover.Put(2, y, Stitch);
                cover.Put(width - 3, y, Mix(Stitch, Black, 0.3));
            }
        }

        _cover = cover;
        return cover;
    }

    /// <summary>
    /// The gold guards on the four corners of the cover, drawn over the page corners they protect, with their rivets.
    /// </summary>
    private void CornerGuards()
    {
        var width = RightPageX + PageWidth + Cover;
        foreach (var (cx, cy, fx, fy) in new[] { (0, 0, 1, 1), (width - 1, 0, -1, 1), (0, Height - 1, 1, -1), (width - 1, Height - 1, -1, -1) })
        {
            for (var i = 0; i < 10; i++)
            {
                for (var j = 0; j < 10 - i; j++)
                {
                    var x = cx + (fx * i);
                    var y = cy + (fy * j);
                    if (i == 0 && j == 0)
                    {
                        continue;
                    }

                    var rim = i + j >= 8;
                    var shine = i + j < 4;
                    Canvas.Put(x, y, rim ? GoldDark : shine ? GoldHi : Gold);
                }
            }

            // El remache.
            Canvas.Put(cx + (fx * 3), cy + (fy * 3), GoldDark);
            Canvas.Put(cx + (fx * 3) + fx, cy + (fy * 3), GoldHi);
        }
    }

    /// <summary>The index tabs down the right edge: coloured, numbered, the open box's pulled out and lit.</summary>
    private void Tabs(AlbumSpread spread, int hoverTab)
    {
        var tabs = spread.TabList;
        for (var i = 0; i < tabs.Count; i++)
        {
            var active = i == spread.ActiveTab;
            var (x, y, w, h) = TabRect(i, tabs.Count, active || i == hoverTab);
            var colour = tabs[i].IsParty ? Rgb(0xF0, 0xE6, 0xFF) : TabColours[i % TabColours.Length];
            var fill = active ? colour : Mix(colour, Black, i == hoverTab ? 0.15 : 0.35);

            for (var yy = 0; yy < h; yy++)
            {
                for (var xx = 0; xx < w; xx++)
                {
                    var rightEdge = xx == w - 1;
                    var roundCorner = rightEdge && (yy == 0 || yy == h - 1);
                    if (roundCorner)
                    {
                        continue;
                    }

                    var border = rightEdge || yy == 0 || yy == h - 1;
                    Canvas.Put(x + xx, y + yy, border ? Mix(fill, Black, 0.45) : yy == 1 ? Mix(fill, White, 0.35) : fill);
                }
            }

            var label = tabs[i].Label;
            var textX = x + 3 + Math.Max(0, (w - 4 - TcgCardArt.SmallWidth(label)) / 2);
            if (h >= 7)
            {
                SmallText(label, textX, y + ((h - 5) / 2), active ? Ink : Mix(fill, Black, 0.6));
            }

            if (active)
            {
                // Sombra de la pestaña sobre la tapa.
                for (var yy = 1; yy < h; yy++)
                {
                    Canvas.Put(x + w, y + yy, Black);
                }
            }
        }
    }

    // ====================================================================================================== PAGES

    private void DrawPage(AlbumPage page, int left, double seconds, int hover)
    {
        Canvas.Stamp(StaticPage(page), left, PageTop);

        for (var i = 0; i < page.Pockets.Count && i < PocketsPerPage; i++)
        {
            if (page.Pockets[i] is not { } card || i == hover || !card.IsLive)
            {
                continue;
            }

            var (px, py) = PocketOrigin(i);
            var x = left + px;
            var y = PageTop + py;

            // La funda entera otra vez, para que el brillo del plástico caiga una sola vez sobre lo que se mueve.
            RedrawPocket(x, y);
            var render = CardRender(card);
            Canvas.Stamp(render.Canvas, x + Margin, y + Margin);
            TcgCardArt.Animate(render, Canvas, x + Margin, y + Margin, seconds, card.Seed);
            PlasticShine(Canvas, x, y, White, 0.16);
        }

        if (hover >= 0 && hover < page.Pockets.Count && page.Pockets[hover] is not null)
        {
            // La funda de la carta levantada, vacía y encendida: la carta va luego, por encima.
            var (px, py) = PocketOrigin(hover);
            RedrawPocket(left + px, PageTop + py, empty: true);
        }
    }

    /// <summary>The card under the mouse, lifted off its pocket with its shadow on the page and its pocket outlined.</summary>
    private void Lifted(AlbumSpread spread, int hover, double seconds)
    {
        var page = hover < PocketsPerPage ? spread.Left : spread.Right;
        var index = hover % PocketsPerPage;
        if (index >= page.Pockets.Count || page.Pockets[index] is not { } card)
        {
            return;
        }

        var (x, y) = PocketInScene(hover);
        var cx = x + Margin;
        var cy = y + Margin - 4;
        var render = CardRender(card);

        // El borde de la funda late entre dos violetas.
        var pulse = (int)(seconds * 2.5) % 2 == 0 ? Accent : AccentLight;
        Outline(x - 1, y - 1, PocketWidth + 2, PocketHeight + 2, pulse);

        // La sombra de la carta en la página, desplazada y a medio tono.
        for (var yy = 0; yy < render.Canvas.Height; yy++)
        {
            for (var xx = 0; xx < render.Canvas.Width; xx++)
            {
                var sx = cx + xx + 2;
                var sy = cy + yy + 5;
                if (render.Canvas.IsSet(xx, yy) && Dither(sx, sy, 0.6))
                {
                    Canvas.Put(sx, sy, Mix(Canvas.At(sx, sy), Black, 0.55));
                }
            }
        }

        Canvas.Stamp(render.Canvas, cx, cy);
        TcgCardArt.Animate(render, Canvas, cx, cy, seconds, card.Seed);
    }

    /// <summary>A page with its header, its pockets and its still cards, drawn once.</summary>
    private CellCanvas StaticPage(AlbumPage page)
    {
        if (_pages.TryGetValue(page, out var kept))
        {
            return kept;
        }

        var canvas = new CellCanvas(PageWidth, PageHeight);

        for (var y = 0; y < PageHeight; y++)
        {
            for (var x = 0; x < PageWidth; x++)
            {
                var border = x == 0 || y == 0 || x == PageWidth - 1 || y == PageHeight - 1;
                var colour = border
                    ? (x == 0 || y == 0 ? PageEdgeLight : PageEdgeDark)
                    : ((x * 3) + y) % 7 == 0 && Dither(x, y, 0.5) ? PageTexture : PageColour;
                canvas.Put(x, y, colour);
            }
        }

        // La cabecera impresa en la hoja: de qué caja es y el número de página.
        if (page.Caption.Length > 0)
        {
            SmallText(canvas, TcgCardArt.SmallTrim(page.Caption, PageWidth - 30), SideMargin, 2, Caption);
        }

        if (page.Number > 0)
        {
            var number = page.Number.ToString();
            SmallText(canvas, number, PageWidth - SideMargin - TcgCardArt.SmallWidth(number), 2, CaptionDim);
        }

        for (var i = 0; i < PocketsPerPage; i++)
        {
            var (px, py) = PocketOrigin(i);
            var card = i < page.Pockets.Count ? page.Pockets[i] : null;
            Pocket(canvas, px, py, empty: card is null);

            if (card is not null)
            {
                canvas.Stamp(CardRender(card).Canvas, px + Margin, py + Margin);
            }

            PlasticShine(canvas, px, py, White, 0.16);
        }

        _pages[page] = canvas;
        return canvas;
    }

    private TcgRender CardRender(TcgCard card)
    {
        if (!_cards.TryGetValue(card, out var render))
        {
            render = TcgCardArt.Render(card, Layout);
            _cards[card] = render;
        }

        return render;
    }

    /// <summary>
    /// A plastic pocket: a slightly lighter well with an edge and the lip of its opening; an empty one shows the faint
    /// outline of a Poké Ball printed on the page behind, like a collector's binder.
    /// </summary>
    private void Pocket(CellCanvas canvas, int x, int y, bool empty)
    {
        for (var yy = 0; yy < PocketHeight; yy++)
        {
            for (var xx = 0; xx < PocketWidth; xx++)
            {
                var edge = xx == 0 || yy == 0 || xx == PocketWidth - 1 || yy == PocketHeight - 1;
                var lip = yy == 2 && xx > 1 && xx < PocketWidth - 2;
                canvas.Put(x + xx, y + yy, edge ? PocketEdge : lip ? PocketLip : PocketColour);
            }
        }

        if (empty)
        {
            BallEmblem(canvas, x + (PocketWidth / 2), y + (PocketHeight / 2), Math.Min(PocketWidth, PocketHeight) / 5);
        }
    }

    private static void BallEmblem(CellCanvas canvas, int cx, int cy, int radius)
    {
        for (var yy = -radius - 1; yy <= radius + 1; yy++)
        {
            for (var xx = -radius - 1; xx <= radius + 1; xx++)
            {
                var r = Math.Sqrt((xx * xx) + (yy * yy));
                var ring = Math.Abs(r - radius) < 0.6;
                var band = Math.Abs(yy) == 0 && r < radius;
                var button = Math.Abs(r - 2) < 0.6;
                if (ring || (band && r > 2.5) || button)
                {
                    canvas.Put(cx + xx, cy + yy, Emblem);
                }
            }
        }
    }

    /// <summary>Clears a pocket back to empty on the frame, for a card redrawn or lifted out of it.</summary>
    private void RedrawPocket(int x, int y, bool empty = false)
    {
        for (var yy = 0; yy < PocketHeight; yy++)
        {
            for (var xx = 0; xx < PocketWidth; xx++)
            {
                var edge = xx == 0 || yy == 0 || xx == PocketWidth - 1 || yy == PocketHeight - 1;
                var lip = yy == 2 && xx > 1 && xx < PocketWidth - 2;
                Canvas.Put(x + xx, y + yy, edge ? PocketEdge : lip ? PocketLip : PocketColour);
            }
        }

        if (empty)
        {
            BallEmblem(Canvas, x + (PocketWidth / 2), y + (PocketHeight / 2), Math.Min(PocketWidth, PocketHeight) / 5);
            PlasticShine(Canvas, x, y, White, 0.16);
        }
    }

    /// <summary>
    /// The shine of the plastic over what is in the pocket: a light diagonal band near the top-left corner and a thinner
    /// one after it, at a half tone so the card reads through.
    /// </summary>
    private static void PlasticShine(CellCanvas canvas, int x, int y, Color light, double amount)
    {
        for (var yy = 1; yy < canvas.Height - y && yy < 60; yy++)
        {
            for (var xx = 1; xx < 40; xx++)
            {
                var d = xx + yy;
                var band = (d > 14 && d < 22) || (d > 25 && d < 28);
                if (band && Dither(x + xx, y + yy, 0.5) && canvas.IsSet(x + xx, y + yy))
                {
                    canvas.Put(x + xx, y + yy, Mix(canvas.At(x + xx, y + yy), light, amount));
                }
            }
        }
    }

    /// <summary>
    /// Now and then a glint runs over the plastic of both pages, as when the album catches the lamp: a thin diagonal
    /// light, once every seven seconds.
    /// </summary>
    private void Glint(double seconds)
    {
        var t = seconds % 7.0;
        if (t > 1.3)
        {
            return;
        }

        var span = Width + Height;
        var centre = (t / 1.3 * (span + 40)) - 20;

        for (var y = PageTop; y < PageTop + PageHeight; y++)
        {
            for (var x = LeftPageX; x < RightPageX + PageWidth; x++)
            {
                var d = Math.Abs(x + (y * 0.6) - centre);
                if (d < 3 && Dither(x, y, 0.6 - (d * 0.15)))
                {
                    Canvas.Put(x, y, Mix(Canvas.At(x, y), White, 0.22));
                }
            }
        }
    }

    private void Outline(int x, int y, int width, int height, Color colour)
    {
        for (var xx = x; xx < x + width; xx++)
        {
            Canvas.Put(xx, y, colour);
            Canvas.Put(xx, y + height - 1, colour);
        }

        for (var yy = y; yy < y + height; yy++)
        {
            Canvas.Put(x, yy, colour);
            Canvas.Put(x + width - 1, yy, colour);
        }
    }

    // ====================================================================================================== SPINE

    /// <summary>The spine between the pages, with three metal rings through the punched holes of both.</summary>
    private void DrawSpine()
    {
        var left = LeftPageX + PageWidth;
        for (var y = PageTop; y < PageTop + PageHeight; y++)
        {
            for (var x = 0; x < Spine; x++)
            {
                // Hondo en el centro, con la luz en los bordes de las páginas.
                var depth = Math.Abs(x - (Spine / 2.0) + 0.5) / (Spine / 2.0);
                Canvas.Put(left + x, y, x is 0 or Spine - 1 ? SpineLight : Dither(x, y, depth) ? SpineLight : SpineColour);
            }
        }

        foreach (var share in new[] { 0.17, 0.5, 0.83 })
        {
            Ring(left + (Spine / 2), PageTop + (int)(PageHeight * share));
        }
    }

    private void Ring(int cx, int cy)
    {
        // Los agujeros de las dos páginas, junto al lomo.
        foreach (var hx in new[] { cx - (Spine / 2) - 4, cx + (Spine / 2) + 2 })
        {
            for (var yy = -2; yy <= 2; yy++)
            {
                for (var xx = 0; xx < 3; xx++)
                {
                    if (Math.Abs(yy) == 2 && xx != 1) continue;
                    Canvas.Put(hx + xx, cy + yy, Hole);
                }
            }
        }

        // La anilla: un óvalo de metal grueso que entra por un agujero y sale por el otro.
        const double rx = 10.5;
        const double ry = 4.2;
        for (var yy = -6; yy <= 6; yy++)
        {
            for (var xx = -12; xx <= 12; xx++)
            {
                var d = Math.Sqrt(Math.Pow(xx / rx, 2) + Math.Pow(yy / ry, 2));
                if (d < 0.62 || d > 1.16)
                {
                    continue;
                }

                // Por la parte de abajo, la anilla pasa por detrás de las páginas.
                if (yy > 0 && Math.Abs(xx) > Spine / 2)
                {
                    continue;
                }

                var colour = yy < -2 ? ChromeHi : yy < 0 ? ChromeLight : yy < 3 ? ChromeMid : ChromeDark;
                if (d > 1.05 || d < 0.7)
                {
                    colour = ChromeDark;
                }

                Canvas.Put(cx + xx, cy + yy, colour);
            }
        }
    }

    // ====================================================================================================== TURN

    /// <summary>
    /// The page turning over its spine in perspective: it lifts, narrows to its edge and opens on the other side, taller
    /// at its free edge where it is nearer, shaded as it turns away from the lamp, casting its shadow on the page it
    /// uncovers.
    /// </summary>
    private void RenderTurn(AlbumSpread to, AlbumTurn turn, double seconds)
    {
        var from = turn.From;
        var p = Ease(turn.Progress);

        // Debajo: la página que no se mueve y la que va apareciendo.
        var stillLeft = turn.Forward ? from.Left : to.Left;
        var stillRight = turn.Forward ? to.Right : from.Right;
        DrawPage(stillLeft, LeftPageX, seconds, -1);
        DrawPage(stillRight, RightPageX, seconds, -1);
        DrawSpine();

        var angle = p * Math.PI;
        var (sin, cos) = Math.SinCos(angle);
        var front = turn.Forward ? StaticPage(from.Right) : StaticPage(from.Left);
        var back = turn.Forward ? StaticPage(to.Left) : StaticPage(to.Right);

        // El eje: el borde de la página que gira junto al lomo.
        var axis = turn.Forward ? RightPageX : LeftPageX + PageWidth;
        var direction = turn.Forward ? 1 : -1;
        var focal = PageWidth * 2.4;
        var centreY = PageTop + (PageHeight / 2.0);

        // Hasta dónde llega el borde libre y cuánto crece por estar más cerca.
        var reach = focal * PageWidth * cos / (focal - (PageWidth * sin));
        var grow = focal / (focal - (PageWidth * sin));
        var x0 = axis + (int)Math.Floor(Math.Min(0, reach * direction));
        var x1 = axis + (int)Math.Ceiling(Math.Max(0, reach * direction));
        var y0 = (int)Math.Floor(centreY - (PageHeight / 2.0 * grow)) - 1;
        var y1 = (int)Math.Ceiling(centreY + (PageHeight / 2.0 * grow)) + 1;

        // La sombra que la hoja levantada echa sobre la página que destapa, más larga cuanto más alta va.
        var shadow = (int)(sin * 14);
        for (var s = 0; s < shadow; s++)
        {
            var sx = cos >= 0 ? axis + (direction * ((int)Math.Abs(reach) + s)) : axis - (direction * ((int)Math.Abs(reach) + s));
            for (var y = PageTop; y < PageTop + PageHeight; y++)
            {
                if (Dither(sx, y, 0.55 * (1 - ((double)s / shadow))))
                {
                    Canvas.Put(sx, y, Mix(Canvas.At(sx, y), Black, 0.5));
                }
            }
        }

        var facingFront = cos >= 0;
        var face = facingFront ? front : back;
        var shade = 0.5 * (1 - Math.Abs(cos));

        for (var y = Math.Max(0, y0); y < Math.Min(Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(Width, x1); x++)
            {
                // Inversa de la proyección: de la celda de la pantalla a la de la hoja.
                var dx = (x + 0.5 - axis) * direction;
                var denominator = (focal * cos) + (dx * sin);
                if (Math.Abs(denominator) < 1e-6)
                {
                    continue;
                }

                var along = dx * focal / denominator;
                if (along < 0 || along >= PageWidth)
                {
                    continue;
                }

                var depth = (focal - (along * sin)) / focal;
                var down = ((y + 0.5 - centreY) * depth) + (PageHeight / 2.0);
                if (down < 0 || down >= PageHeight)
                {
                    continue;
                }

                // Columna de la hoja desde el lomo: por delante es la que era; por detrás, la de la página nueva.
                var column = (int)along;
                var sourceX = turn.Forward
                    ? (facingFront ? column : PageWidth - 1 - column)
                    : (facingFront ? PageWidth - 1 - column : column);

                var colour = face.At(sourceX, (int)down);
                if (colour.A == 0)
                {
                    continue;
                }

                if (Dither(x, y, shade))
                {
                    colour = Mix(colour, Black, 0.45);
                }

                Canvas.Put(x, y, colour);
            }
        }

        // El canto de la hoja, un filo claro en el borde libre.
        var edgeX = axis + (int)Math.Round(reach * direction);
        for (var y = Math.Max(0, y0 + 1); y < Math.Min(Height, y1 - 1); y++)
        {
            if (edgeX >= 0 && edgeX < Width && Canvas.At(edgeX, y).A > 0)
            {
                Canvas.Put(edgeX, y, PageEdgeLight);
            }
        }
    }

    /// <summary>Slow at the start and the end, like a page lifted, turned and laid down.</summary>
    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    // ====================================================================================================== LIGHT

    /// <summary>
    /// The desk lamp over the album, as a factor per cell: brighter near the top left, darker towards the far corners,
    /// in dithered steps so it is pixel art and not a gradient.
    /// </summary>
    private static float[] LightMap(int width, int height)
    {
        var map = new float[width * height];
        var lx = width * 0.3;
        var ly = -height * 0.1;
        var reach = Math.Sqrt((width * width) + (height * height));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var d = Math.Sqrt(Math.Pow(x - lx, 2) + Math.Pow((y - ly) * 1.3, 2)) / reach;
                float factor;
                if (d < 0.32) factor = Dither(x, y, 1 - (d / 0.32)) ? 1.07f : 1f;
                else if (d < 0.62) factor = 1f;
                else if (d < 0.82) factor = Dither(x, y, (d - 0.62) / 0.2) ? 0.88f : 1f;
                else factor = Dither(x, y, Math.Min(1, (d - 0.82) / 0.2)) ? 0.8f : 0.88f;
                map[(y * width) + x] = factor;
            }
        }

        return map;
    }

    private void ApplyLight()
    {
        var pixels = Canvas.Bgra;
        for (var i = 0; i < _light.Length; i++)
        {
            var factor = _light[i];
            if (factor == 1f)
            {
                continue;
            }

            var at = i * 4;
            if (pixels[at + 3] == 0)
            {
                continue;
            }

            pixels[at] = (byte)Math.Min(255, pixels[at] * factor);
            pixels[at + 1] = (byte)Math.Min(255, pixels[at + 1] * factor);
            pixels[at + 2] = (byte)Math.Min(255, pixels[at + 2] * factor);
        }
    }

    // ====================================================================================================== TEXT

    private void SmallText(string text, int x, int y, Color colour) => SmallText(Canvas, text, x, y, colour);

    /// <summary>The small font in capitals, for the page headers and the tabs: the cards' own, wide N included.</summary>
    private static void SmallText(CellCanvas canvas, string text, int x, int y, Color colour) =>
        TcgCardArt.SmallText(canvas, text, x, y, colour);
}
