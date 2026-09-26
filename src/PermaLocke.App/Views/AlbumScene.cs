using System.Windows.Media;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>One page of the album: its pockets in reading order, a card or an empty one each.</summary>
public sealed record AlbumPage(IReadOnlyList<TcgCard?> Pockets)
{
    public static AlbumPage Empty(int pockets) => new(new TcgCard?[pockets]);
}

/// <summary>The two pages open in front of the player, and which spread of the album they are.</summary>
/// <param name="Index">Position of the spread in the whole album, so a change knows which way to turn.</param>
public sealed record AlbumSpread(AlbumPage Left, AlbumPage Right, TcgLayout Layout, int Index);

/// <summary>
/// The album open on the table (§186), cell by cell: two binder pages with their plastic pockets, the rings of the
/// spine, the cards in the pockets, and the page that turns when the player goes on.
/// </summary>
/// <remarks>
/// <para>
/// Pure, like the other scenes: it is given the spread, the moment and the pocket under the mouse, and paints. The
/// pages and the cards that do not move are drawn once and kept; each frame copies them and adds what moves — the
/// foil of a shiny, the embers of a fallen card, the page turning.
/// </para>
/// <para>
/// A page is 3×3 pockets of the full card or 4×4 of the small one, the two sizes the player asked to try. The pocket of
/// a slot is the slot's place in the box, so the album keeps the PC's order and its gaps.
/// </para>
/// </remarks>
public sealed class AlbumScene
{
    /// <summary>How long a page takes to turn.</summary>
    public const double TurnLength = 0.42;

    private const int Margin = 2;
    private const int SideMargin = 6;
    private const int TopMargin = 5;
    private const int Gap = 2;
    private const int Spine = 12;

    private static readonly Color PageColour = Rgb(0x17, 0x11, 0x2B);
    private static readonly Color PageTexture = Rgb(0x1C, 0x15, 0x33);
    private static readonly Color PageEdgeLight = Rgb(0x34, 0x28, 0x58);
    private static readonly Color PageEdgeDark = Rgb(0x0A, 0x07, 0x14);
    private static readonly Color PocketColour = Rgb(0x21, 0x19, 0x3D);
    private static readonly Color PocketEdge = Rgb(0x33, 0x29, 0x5A);
    private static readonly Color PocketLip = Rgb(0x4A, 0x3C, 0x7C);
    private static readonly Color SpineColour = Rgb(0x0C, 0x09, 0x16);
    private static readonly Color SpineLight = Rgb(0x1E, 0x17, 0x34);
    private static readonly Color ChromeHi = Rgb(0xF2, 0xEE, 0xFA);
    private static readonly Color ChromeLight = Rgb(0xC4, 0xBE, 0xD8);
    private static readonly Color ChromeMid = Rgb(0x8E, 0x88, 0xA6);
    private static readonly Color ChromeDark = Rgb(0x4C, 0x46, 0x64);
    private static readonly Color Hole = Rgb(0x06, 0x04, 0x0C);
    private static readonly Color Accent = Rgb(0xB0, 0x7B, 0xF0);
    private static readonly Color AccentLight = Rgb(0xD2, 0xAD, 0xFF);
    private static readonly Color Black = Rgb(0x06, 0x05, 0x0B);
    private static readonly Color White = Rgb(0xFF, 0xFF, 0xFF);

    private readonly Dictionary<TcgCard, TcgRender> _cards = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AlbumPage, CellCanvas> _pages = new(ReferenceEqualityComparer.Instance);

    public AlbumScene(TcgLayout layout)
    {
        Layout = layout;
        Columns = layout == TcgLayout.Full ? 3 : 4;
        (CardWidth, CardHeight) = TcgCardArt.SizeOf(layout);
        PocketWidth = CardWidth + (Margin * 2);
        PocketHeight = CardHeight + (Margin * 2);
        PageWidth = (SideMargin * 2) + (Columns * PocketWidth) + ((Columns - 1) * Gap);
        PageHeight = (TopMargin * 2) + (Columns * PocketHeight) + ((Columns - 1) * Gap);
        Width = (PageWidth * 2) + Spine;
        Height = PageHeight;
        Canvas = new CellCanvas(Width, Height);
    }

    public TcgLayout Layout { get; }

    /// <summary>Pockets across and down a page: 3 or 4.</summary>
    public int Columns { get; }

    public int PocketsPerPage => Columns * Columns;

    public int CardWidth { get; }

    public int CardHeight { get; }

    public int PocketWidth { get; }

    public int PocketHeight { get; }

    public int PageWidth { get; }

    public int PageHeight { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame after the last <see cref="Render"/>.</summary>
    public CellCanvas Canvas { get; }

    /// <summary>Left edge of the right page.</summary>
    private int RightPageX => PageWidth + Spine;

    /// <summary>
    /// The pocket at a cell of the scene: 0 to <see cref="PocketsPerPage"/> − 1 on the left page, the rest on the right,
    /// or −1 between pockets, on the spine or outside.
    /// </summary>
    public int PocketAt(int x, int y)
    {
        var page = x < PageWidth ? 0 : x >= RightPageX ? 1 : -1;
        if (page < 0 || y < 0 || y >= Height)
        {
            return -1;
        }

        var px = x - (page == 0 ? 0 : RightPageX) - SideMargin;
        var py = y - TopMargin;
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

    /// <summary>
    /// Paints the spread at <paramref name="seconds"/>. With <paramref name="turn"/> the page turning is drawn over
    /// the pages it leaves and the ones it uncovers.
    /// </summary>
    /// <param name="hover">The pocket under the mouse: its card lifts a cell and the pocket lights up.</param>
    public void Render(AlbumSpread spread, double seconds, int hover = -1, AlbumTurn? turn = null)
    {
        Canvas.Clear();

        if (turn is { } turning && turning.Progress < 1)
        {
            RenderTurn(spread, turning, seconds);
            return;
        }

        DrawPage(spread.Left, 0, seconds, hover < PocketsPerPage ? hover : -1);
        DrawPage(spread.Right, RightPageX, seconds, hover >= PocketsPerPage ? hover - PocketsPerPage : -1);
        DrawSpine();
    }

    /// <summary>Forgets the drawings kept for pages and cards that are no longer in the album.</summary>
    public void Forget()
    {
        _cards.Clear();
        _pages.Clear();
    }

    // ====================================================================================================== TURN

    private void RenderTurn(AlbumSpread to, AlbumTurn turn, double seconds)
    {
        var from = turn.From;
        var p = Ease(turn.Progress);

        // Debajo: la página que no se mueve y la que va apareciendo.
        var stillLeft = turn.Forward ? from.Left : to.Left;
        var stillRight = turn.Forward ? to.Right : from.Right;
        DrawPage(stillLeft, 0, seconds, -1);
        DrawPage(stillRight, RightPageX, seconds, -1);
        DrawSpine();

        // La hoja que gira: hasta la mitad se ve su cara de antes estrechándose hacia el lomo, y después su otra cara
        // abriéndose al otro lado.
        var firstHalf = p < 0.5;
        var share = firstHalf ? 1 - (p * 2) : (p - 0.5) * 2;
        var width = Math.Max(1, (int)Math.Round(PageWidth * share));

        AlbumPage face;
        bool onRight;
        if (turn.Forward)
        {
            face = firstHalf ? from.Right : to.Left;
            onRight = firstHalf;
        }
        else
        {
            face = firstHalf ? from.Left : to.Right;
            onRight = !firstHalf;
        }

        var picture = StaticPage(face);
        var left = onRight ? RightPageX : PageWidth - width;

        // La hoja se levanta: su sombra cae sobre la página de debajo.
        var shadowWidth = Math.Min(6, PageWidth - width);
        for (var s = 0; s < shadowWidth; s++)
        {
            var sx = onRight ? left + width + s : left - 1 - s;
            for (var y = 0; y < Height; y++)
            {
                if (Dither(sx, y, 0.6 - (s * 0.1)))
                {
                    Canvas.Put(sx, y, Mix(Canvas.At(sx, y), Black, 0.5));
                }
            }
        }

        Canvas.StampColumns(picture, left, 0, width);

        // Más oscura cuanto más de canto: la luz le da de lado.
        var shade = 0.55 * (1 - share);
        for (var y = 0; y < Height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (Dither(x, y, shade))
                {
                    Canvas.Put(x, y, Mix(Canvas.At(x, y), Black, 0.45));
                }
            }
        }

        // El canto de la hoja, un filo claro del lado que se levanta.
        var edge = onRight ? left + width - 1 : left;
        for (var y = 0; y < Height; y++)
        {
            Canvas.Put(edge, y, PageEdgeLight);
        }
    }

    /// <summary>Slow at the start and the end, like a page lifted, turned and laid down.</summary>
    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    // ====================================================================================================== PAGES

    /// <param name="hover">The pocket of this page under the mouse, or -1.</param>
    private void DrawPage(AlbumPage page, int left, double seconds, int hover)
    {
        Canvas.Stamp(StaticPage(page), left, 0);

        for (var i = 0; i < page.Pockets.Count && i < PocketsPerPage; i++)
        {
            if (page.Pockets[i] is not { } card)
            {
                continue;
            }

            var (px, py) = PocketOrigin(i);
            var cx = left + px + Margin;
            var cy = py + Margin;
            if (i == hover)
            {
                // Levantada una celda de su funda, con el borde de la funda encendido.
                Outline(left + px - 1, py - 1, PocketWidth + 2, PocketHeight + 2, Accent);
                RedrawPocket(left + px, py);
                var render = CardRender(card);
                Canvas.Stamp(render.Canvas, cx, cy - 2);
                if (card.IsLive)
                {
                    TcgCardArt.Animate(render, Canvas, cx, cy - 2, seconds, card.Seed);
                }

                Sheen(left + px, py, AccentLight);
                continue;
            }

            if (card.IsLive)
            {
                // La funda entera otra vez, para que el brillo del plástico caiga una sola vez sobre lo que se mueve.
                RedrawPocket(left + px, py);
                var render = CardRender(card);
                Canvas.Stamp(render.Canvas, cx, cy);
                TcgCardArt.Animate(render, Canvas, cx, cy, seconds, card.Seed);
                Sheen(left + px, py, White);
            }
        }
    }

    /// <summary>A page with its pockets and its still cards, drawn once.</summary>
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
                    : (x * 3 + y) % 7 == 0 && Dither(x, y, 0.5) ? PageTexture : PageColour;
                canvas.Put(x, y, colour);
            }
        }

        for (var i = 0; i < PocketsPerPage; i++)
        {
            var (px, py) = PocketOrigin(i);
            Pocket(canvas, px, py);

            if (i < page.Pockets.Count && page.Pockets[i] is { } card)
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

    /// <summary>An empty plastic pocket: a slightly lighter well with an edge and the lip of its opening.</summary>
    private void Pocket(CellCanvas canvas, int x, int y)
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
    }

    /// <summary>Clears a pocket back to empty on the frame, for a card lifted out of it.</summary>
    private void RedrawPocket(int x, int y)
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
    }

    /// <summary>
    /// The shine of the plastic over what is in the pocket: a light diagonal band near the top-left corner and a thinner
    /// one after it, at a half tone so the card reads through.
    /// </summary>
    private void PlasticShine(CellCanvas canvas, int x, int y, Color light, double amount)
    {
        for (var yy = 1; yy < PocketHeight - 1; yy++)
        {
            for (var xx = 1; xx < PocketWidth - 1; xx++)
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

    /// <summary>The plastic's shine again on the frame, over a card redrawn this frame.</summary>
    private void Sheen(int x, int y, Color light) => PlasticShine(Canvas, x, y, light, 0.16);

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
        var left = PageWidth;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Spine; x++)
            {
                Canvas.Put(left + x, y, x is 0 or Spine - 1 ? SpineLight : SpineColour);
            }
        }

        foreach (var share in new[] { 0.17, 0.5, 0.83 })
        {
            var cy = (int)(Height * share);
            Ring(left + (Spine / 2), cy);
        }
    }

    private void Ring(int cx, int cy)
    {
        // Los agujeros de las dos páginas, junto al lomo.
        foreach (var hx in new[] { cx - (Spine / 2) - 4, cx + (Spine / 2) + 3 })
        {
            for (var yy = -1; yy <= 1; yy++)
            {
                for (var xx = 0; xx < 2; xx++)
                {
                    Canvas.Put(hx + xx, cy + yy, Hole);
                }
            }
        }

        // La anilla: un óvalo de metal que entra por un agujero y sale por el otro.
        const double rx = 9.5;
        const double ry = 3.2;
        for (var yy = -4; yy <= 4; yy++)
        {
            for (var xx = -10; xx <= 10; xx++)
            {
                var d = Math.Sqrt(Math.Pow(xx / rx, 2) + Math.Pow(yy / ry, 2));
                if (d < 0.72 || d > 1.12)
                {
                    continue;
                }

                // Por la parte de abajo, la anilla pasa por detrás de la página.
                if (yy > 0 && Math.Abs(xx) > Spine / 2)
                {
                    continue;
                }

                var colour = yy < -1 ? ChromeHi : yy < 1 ? ChromeLight : yy < 3 ? ChromeMid : ChromeDark;
                Canvas.Put(cx + xx, cy + yy, colour);
            }
        }
    }
}

/// <summary>A page turning: the spread it leaves, which way it goes, and how far it has got, from 0 to 1.</summary>
public sealed record AlbumTurn(AlbumSpread From, bool Forward, double Progress);
