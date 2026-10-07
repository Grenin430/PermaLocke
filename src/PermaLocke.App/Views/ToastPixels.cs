using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// What the pieces of a notice share: the size of a cell, the colours of each kind, and drawing in cells.
/// </summary>
/// <remarks>
/// <para>
/// The notices are pixel art for the same reason the cemetery and the Alola strip are (§115, §116): what reads as
/// made by hand here is drawn cell by cell, and what reads as generated is blur, glow and soft gradients. So a cell is
/// a whole number of screen pixels — two at 100 %, three at 150 % — every edge is hard, the shadow is a solid block
/// moved down and right, and anything that moves does it in steps.
/// </para>
/// <para>
/// Each piece paints its own small bitmap in cells and draws it scaled with nearest neighbour, which keeps every cell
/// square at any display scaling.
/// </para>
/// </remarks>
internal static class ToastPixels
{
    /// <summary>Outlines; the theme's (2026-10-01).</summary>
    public static Color Ink => Pixel.PixelTheme.Current.Ink;
    public static readonly Color Face = Rgb(0x13, 0x0F, 0x1F);
    public static readonly Color Bevel = Rgb(0x2E, 0x25, 0x4C);
    public static readonly Color Groove = Rgb(0x0B, 0x09, 0x14);
    public static readonly Color Well = Rgb(0x0E, 0x0B, 0x18);
    public static readonly Color WellFloor = Rgb(0x1A, 0x15, 0x2C);
    public static readonly Color Off = Rgb(0x23, 0x1C, 0x3B);

    /// <summary>The colour of each kind: its tab, its countdown and the floor under its sprite.</summary>
    /// <remarks>
    /// The same colours the rest of PermaLocke already means something with: gold is the shiny of the roulette's
    /// rim, red and green are what costs and what pays, violet is PermaLocke's own.
    /// </remarks>
    public static Color AccentOf(ToastKind kind) => kind switch
    {
        ToastKind.Shiny or ToastKind.Gift => Rgb(0xD8, 0xA8, 0x3C),
        ToastKind.BallsTaken => Rgb(0xCF, 0x50, 0x44),
        ToastKind.BallsBack or ToastKind.Reward => Rgb(0x58, 0xA5, 0x6E),
        ToastKind.AllowedCapture => Rgb(0x5B, 0x8A, 0xC4),
        ToastKind.Duplicate => Rgb(0x4F, 0xB8, 0xB0),
        ToastKind.Death or ToastKind.TeamWipe => Rgb(0xB8, 0x43, 0x3A),
        ToastKind.Warning => Rgb(0xD9, 0x77, 0x2F),
        ToastKind.Ghost => Rgb(0x9C, 0xC8, 0xE0),
        ToastKind.Update => Rgb(0xE8, 0xC0, 0x3A),
        _ => Rgb(0xB0, 0x7B, 0xF0)
    };

    /// <summary>Screen pixels per cell: three at 100 %, and still whole pixels at any other scaling.</summary>
    /// <remarks>
    /// Three and not two: at two the outline and the bevel read as a thin border, the sprites came out small over the
    /// game, and the pixel art stopped looking like pixel art. Seen side by side on renders of the real window.
    /// </remarks>
    public static int Cell(Visual visual)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return Math.Max(3, (int)Math.Floor((3 * dpi.DpiScaleX) + 0.5));
    }

    /// <summary>How many device-independent units a number of cells takes.</summary>
    public static double Units(Visual visual, int cells) => cells * Cell(visual) / VisualTreeHelper.GetDpi(visual).DpiScaleX;

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + ((b.R - a.R) * t)),
        (byte)Math.Round(a.G + ((b.G - a.G) * t)),
        (byte)Math.Round(a.B + ((b.B - a.B) * t)));

    /// <summary>A canvas of cells, written straight into a Bgra32 buffer.</summary>
    public sealed class Canvas(int columns, int rows)
    {
        public int Columns { get; } = columns;
        public int Rows { get; } = rows;
        private readonly byte[] _pixels = new byte[Math.Max(1, columns * rows * 4)];
        private readonly bool _shades = Pixel.PixelTheme.Current.Shades is not null;

        /// <summary>A canvas that never redraws in the theme's shades: for the previews of the other looks.</summary>
        public Canvas(int columns, int rows, bool shades) : this(columns, rows)
        {
            _shades = shades && _shades;
        }

        public void Put(int x, int y, Color colour, byte alpha = 255)
        {
            if (x < 0 || y < 0 || x >= Columns || y >= Rows)
            {
                return;
            }

            // El Game Boy lo redibuja todo en sus cuatro verdes (2026-10-01); los demás temas no tocan nada.
            if (_shades) colour = Pixel.PixelTheme.Current.Map(colour);

            var at = ((y * Columns) + x) * 4;
            _pixels[at] = colour.B;
            _pixels[at + 1] = colour.G;
            _pixels[at + 2] = colour.R;
            _pixels[at + 3] = alpha;
        }

        /// <summary>A rectangle with its four corner cells left out, which is how a pixel box gets round corners.</summary>
        public void Box(int left, int top, int width, int height, Color colour, byte alpha = 255, bool notched = true)
        {
            for (var y = top; y < top + height; y++)
            {
                for (var x = left; x < left + width; x++)
                {
                    var corner = (x == left || x == left + width - 1) && (y == top || y == top + height - 1);

                    if (!(notched && corner))
                    {
                        Put(x, y, colour, alpha);
                    }
                }
            }
        }

        /// <summary>A sprite written as text: each character a colour from the key, a dot for nothing.</summary>
        public void Sprite(string[] rows, int left, int top, IReadOnlyDictionary<char, Color> key)
        {
            for (var y = 0; y < rows.Length; y++)
            {
                for (var x = 0; x < rows[y].Length; x++)
                {
                    if (key.TryGetValue(rows[y][x], out var colour))
                    {
                        Put(left + x, top + y, colour);
                    }
                }
            }
        }

        public BitmapSource ToBitmap()
        {
            var bitmap = BitmapSource.Create(Columns, Rows, 96, 96, PixelFormats.Bgra32, null, _pixels, Columns * 4);
            bitmap.Freeze();
            return bitmap;
        }
    }

    /// <summary>A sprite's pixels, read once: the cartridge icons are RGBA5551, so alpha is all or nothing.</summary>
    public sealed record SpritePixels(byte[] Bgra, int Width, int Height)
    {
        private static readonly ConditionalWeakTable<BitmapSource, SpritePixels> Cache = new();

        public static SpritePixels? From(BitmapSource? sprite)
        {
            if (sprite is null)
            {
                return null;
            }

            if (Cache.TryGetValue(sprite, out var cached))
            {
                return cached;
            }

            var converted = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
            var bgra = new byte[converted.PixelWidth * converted.PixelHeight * 4];
            converted.CopyPixels(bgra, converted.PixelWidth * 4, 0);

            var pixels = new SpritePixels(bgra, converted.PixelWidth, converted.PixelHeight);
            Cache.AddOrUpdate(sprite, pixels);
            return pixels;
        }

        public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Bgra[(((y * Width) + x) * 4) + 3] >= 128;

        public Color At(int x, int y)
        {
            var at = ((y * Width) + x) * 4;
            return Color.FromRgb(Bgra[at + 2], Bgra[at + 1], Bgra[at]);
        }
    }

    /// <summary>Draws a canvas over an element's box, every cell the same whole number of screen pixels.</summary>
    public static void Draw(DrawingContext context, Visual visual, Canvas canvas)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        var cell = Cell(visual);

        context.DrawImage(canvas.ToBitmap(),
            new Rect(0, 0, canvas.Columns * cell / dpi.DpiScaleX, canvas.Rows * cell / dpi.DpiScaleY));
    }
}

/// <summary>
/// A notice's box: a one-cell outline, a one-cell bevel lit on top and dark underneath, notched corners and a hard
/// shadow. Or, with <see cref="IsTab"/>, the coloured tab that sits on top of it, open at the bottom so it grows out of
/// the box instead of lying on it.
/// </summary>
public sealed class ToastFrame : FrameworkElement
{
    /// <summary>How far the shadow falls, in cells, down and to the right.</summary>
    private const int Shadow = 2;

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ToastKind), typeof(ToastFrame),
        new FrameworkPropertyMetadata(ToastKind.Info, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsTabProperty = DependencyProperty.Register(
        nameof(IsTab), typeof(bool), typeof(ToastFrame),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public ToastFrame()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public ToastKind Kind
    {
        get => (ToastKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsTab
    {
        get => (bool)GetValue(IsTabProperty);
        set => SetValue(IsTabProperty, value);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Floor(ActualHeight * dpi.DpiScaleY / cell);

        if (columns < 8 || rows < 4)
        {
            return;
        }

        var canvas = new ToastPixels.Canvas(columns, rows);

        if (IsTab)
        {
            PaintTab(canvas);
        }
        else
        {
            PaintBox(canvas);
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }

    private static void PaintBox(ToastPixels.Canvas canvas)
    {
        var width = canvas.Columns - Shadow;
        var height = canvas.Rows - Shadow;

        canvas.Box(Shadow, Shadow, width, height, Colors.Black, alpha: 150);
        canvas.Box(0, 0, width, height, ToastPixels.Ink);
        canvas.Box(1, 1, width - 2, height - 2, ToastPixels.Face, notched: false);

        // El relieve: arriba y a la izquierda la luz, abajo y a la derecha el surco. Un píxel, sin fundido.
        for (var x = 2; x < width - 2; x++)
        {
            canvas.Put(x, 1, ToastPixels.Bevel);
            canvas.Put(x, height - 2, ToastPixels.Groove);
        }

        for (var y = 2; y < height - 2; y++)
        {
            canvas.Put(1, y, ToastPixels.Bevel);
            canvas.Put(width - 2, y, ToastPixels.Groove);
        }
    }

    private void PaintTab(ToastPixels.Canvas canvas)
    {
        var accent = ToastPixels.AccentOf(Kind);
        var light = ToastPixels.Mix(accent, Colors.White, 0.35);
        var dark = ToastPixels.Mix(accent, Colors.Black, 0.35);
        var width = canvas.Columns - Shadow;
        var height = canvas.Rows;

        // La pestaña no lleva borde abajo: se mete dos celdas en la caja y tapa su contorno, así que sale de ella.
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var outline = y == 0 || x == 0 || x == width - 1;
                var corner = y == 0 && (x == 0 || x == width - 1);

                if (corner)
                {
                    continue;
                }

                canvas.Put(x, y, outline ? ToastPixels.Ink : y == 1 ? light : x == width - 2 ? dark : accent);
            }
        }

        // Sombra solo a la derecha, que es por donde asoma.
        for (var y = Shadow; y < height; y++)
        {
            canvas.Put(width, y, Colors.Black, 150);
            canvas.Put(width + 1, y, Colors.Black, 150);
        }
    }
}

/// <summary>
/// The square the sprite stands in: a dark well with a dithered floor in the notice's colour, the sprite from the
/// cartridge one pixel per cell, and the mark of its kind drawn by hand on top.
/// </summary>
/// <remarks>
/// The sprite hops a cell every half second, two frames, which is exactly how the party menu of the games moves its
/// icons — the one animation here that is not invented. A fallen Pokémon does not hop, and is drawn in grey.
/// </remarks>
public sealed class ToastPlate : FrameworkElement
{
    private const int Columns = 36;
    private const int Rows = 34;
    private const int Floor = 7;

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ToastKind), typeof(ToastPlate),
        new FrameworkPropertyMetadata(ToastKind.Info, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
        nameof(Sprite), typeof(BitmapSource), typeof(ToastPlate),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _step = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _frame;

    public ToastPlate()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        _step.Tick += (_, _) =>
        {
            _frame++;
            InvalidateVisual();
        };
        Loaded += (_, _) => _step.Start();
        Unloaded += (_, _) => _step.Stop();
    }

    public ToastKind Kind
    {
        get => (ToastKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public BitmapSource? Sprite
    {
        get => (BitmapSource?)GetValue(SpriteProperty);
        set => SetValue(SpriteProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(ToastPixels.Units(this, Columns), ToastPixels.Units(this, Rows));

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var canvas = new ToastPixels.Canvas(Columns, Rows);
        var accent = ToastPixels.AccentOf(Kind);

        Well(canvas, accent);

        var fallen = Kind is ToastKind.Death or ToastKind.Ghost;
        var hop = !fallen && Kind is not ToastKind.TeamWipe && (_frame / 2) % 2 == 1 ? 1 : 0;

        if (Sprite is { } sprite && Pixels(sprite) is { } pixels)
        {
            var left = Math.Max(1, (Columns - pixels.Width) / 2);
            var top = Math.Max(1, Rows - 3 - Math.Min(pixels.Height, Rows - 3) - hop);
            SpriteInto(canvas, pixels, left, top, grey: fallen);
        }
        else if (Kind is ToastKind.Death or ToastKind.TeamWipe)
        {
            canvas.Sprite(ToastMarks.Grave, (Columns - ToastMarks.Grave[0].Length) / 2, Rows - 3 - ToastMarks.Grave.Length, ToastMarks.Key);
        }
        else if (Kind is ToastKind.Warning or ToastKind.Update)
        {
            canvas.Sprite(ToastMarks.Sign, (Columns - ToastMarks.Sign[0].Length) / 2, Rows - 5 - ToastMarks.Sign.Length - hop, ToastMarks.Key);
        }
        else
        {
            canvas.Sprite(ToastMarks.Ball, (Columns - ToastMarks.Ball[0].Length) / 2, Rows - 4 - ToastMarks.Ball.Length - hop, ToastMarks.Key);
        }

        Mark(canvas);
        ToastPixels.Draw(drawingContext, this, canvas);
    }

    private static void Well(ToastPixels.Canvas canvas, Color accent)
    {
        canvas.Box(0, 0, Columns, Rows, ToastPixels.Ink);
        canvas.Box(1, 1, Columns - 2, Rows - 2, ToastPixels.Well, notched: false);

        // El suelo: trama ordenada de dos colores, más densa abajo. Nada de fundido.
        var floorColour = ToastPixels.Mix(ToastPixels.WellFloor, accent, 0.28);

        for (var y = Rows - 1 - Floor; y < Rows - 1; y++)
        {
            var depth = y - (Rows - 1 - Floor);

            for (var x = 1; x < Columns - 1; x++)
            {
                var lit = depth switch
                {
                    0 => false,
                    1 => (x % 4 == 0),
                    2 => ((x + y) % 2 == 0) && (x % 2 == 0),
                    3 => (x + y) % 2 == 0,
                    _ => (x + y) % 2 == 0 || depth >= 5
                };

                if (lit)
                {
                    canvas.Put(x, y, floorColour);
                }
            }
        }
    }

    private void Mark(ToastPixels.Canvas canvas)
    {
        var blink = (_frame / 2) % 2 == 0;

        switch (Kind)
        {
            case ToastKind.Shiny:
                canvas.Sprite(blink ? ToastMarks.SparkleBig : ToastMarks.SparkleSmall, Columns - 10, 2, ToastMarks.Key);
                canvas.Sprite(blink ? ToastMarks.SparkleTiny : ToastMarks.Empty, 3, 9, ToastMarks.Key);
                break;

            case ToastKind.BallsTaken:
                canvas.Sprite(ToastMarks.Forbidden, Columns - 12, Rows - 14, ToastMarks.Key);
                break;

            case ToastKind.BallsBack:
                canvas.Sprite(ToastMarks.Arrow, Columns - 10, blink ? 3 : 2, ToastMarks.Key);
                break;

            case ToastKind.Death:
                canvas.Sprite(ToastMarks.Cross, Columns - 8, 3, ToastMarks.Key);
                break;

            case ToastKind.Warning:
                // Sin sprite ya sale la señal entera, y dos exclamaciones no dicen más que una.
                if (blink && Sprite is not null)
                {
                    canvas.Sprite(ToastMarks.Bang, Columns - 7, 3, ToastMarks.Key);
                }

                break;
        }
    }

    private static void SpriteInto(ToastPixels.Canvas canvas, ToastPixels.SpritePixels pixels, int left, int top, bool grey)
    {
        // Si alguno se pasa de la placa, se recorta en vez de encogerlo: encoger un sprite de píxeles es emborronarlo.
        for (var y = 0; y < Math.Min(pixels.Height, Rows - 3); y++)
        {
            for (var x = 0; x < Math.Min(pixels.Width, Columns - 2); x++)
            {
                if (!pixels.Solid(x, y))
                {
                    continue;
                }

                var colour = pixels.At(x, y);

                if (grey)
                {
                    var luma = (byte)Math.Round((0.30 * colour.R) + (0.59 * colour.G) + (0.11 * colour.B));
                    colour = ToastPixels.Mix(Color.FromRgb(luma, luma, luma), ToastPixels.Well, 0.35);
                }

                canvas.Put(left + x, top + y, colour);
            }
        }
    }

    private static ToastPixels.SpritePixels? Pixels(BitmapSource sprite) => ToastPixels.SpritePixels.From(sprite);
}

/// <summary>
/// How long a notice has left: twelve segments that go out one by one, left to right.
/// </summary>
public sealed class ToastCountdown : FrameworkElement
{
    private const int Segments = 12;
    private const int Tall = 2;

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ToastKind), typeof(ToastCountdown),
        new FrameworkPropertyMetadata(ToastKind.Info, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        nameof(Shown), typeof(DateTime), typeof(ToastCountdown),
        new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LingerProperty = DependencyProperty.Register(
        nameof(Linger), typeof(TimeSpan), typeof(ToastCountdown),
        new FrameworkPropertyMetadata(TimeSpan.FromSeconds(6), FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _step = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private int _lit = -1;

    public ToastCountdown()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);

        // Se pregunta a menudo pero solo se repinta cuando se apaga un segmento: a saltos.
        _step.Tick += (_, _) =>
        {
            if (Lit() != _lit)
            {
                InvalidateVisual();
            }
        };
        Loaded += (_, _) => _step.Start();
        Unloaded += (_, _) => _step.Stop();
    }

    public ToastKind Kind
    {
        get => (ToastKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public DateTime Shown
    {
        get => (DateTime)GetValue(ShownProperty);
        set => SetValue(ShownProperty, value);
    }

    public TimeSpan Linger
    {
        get => (TimeSpan)GetValue(LingerProperty);
        set => SetValue(LingerProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, ToastPixels.Units(this, Tall));

    private int Lit()
    {
        var left = 1 - ((DateTime.UtcNow - Shown).TotalMilliseconds / Math.Max(1, Linger.TotalMilliseconds));
        return Math.Clamp((int)Math.Ceiling(left * Segments), 0, Segments);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell);

        if (columns < Segments * 2)
        {
            return;
        }

        _lit = Lit();

        var canvas = new ToastPixels.Canvas(columns, Tall);
        var accent = ToastPixels.AccentOf(Kind);
        var gap = 1;
        var width = (columns - ((Segments - 1) * gap)) / Segments;
        var left = (columns - ((width * Segments) + ((Segments - 1) * gap))) / 2;

        for (var segment = 0; segment < Segments; segment++)
        {
            // Se apagan de izquierda a derecha: lo que queda está a la derecha, donde se lee el final.
            var on = segment >= Segments - _lit;
            canvas.Box(left + (segment * (width + gap)), 0, width, Tall, on ? accent : ToastPixels.Off, notched: false);
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }
}

/// <summary>The marks drawn on top of a sprite, written by hand one cell per character.</summary>
internal static class ToastMarks
{
    public static readonly IReadOnlyDictionary<char, Color> Key = new Dictionary<char, Color>
    {
        ['k'] = ToastPixels.Ink,
        ['W'] = ToastPixels.Rgb(0xFF, 0xF7, 0xDE),
        ['y'] = ToastPixels.Rgb(0xD8, 0xA8, 0x3C),
        ['w'] = ToastPixels.Rgb(0xF7, 0xE6, 0xAB),
        ['R'] = ToastPixels.Rgb(0xE0, 0x4A, 0x3C),
        ['r'] = ToastPixels.Rgb(0x8E, 0x2A, 0x22),
        ['G'] = ToastPixels.Rgb(0x6F, 0xC8, 0x86),
        ['g'] = ToastPixels.Rgb(0x2E, 0x73, 0x45),
        ['O'] = ToastPixels.Rgb(0xF0, 0x8A, 0x3A),
        ['l'] = ToastPixels.Rgb(0xA0, 0x98, 0xB8),
        ['m'] = ToastPixels.Rgb(0x6C, 0x64, 0x84),
        ['d'] = ToastPixels.Rgb(0x4E, 0x48, 0x66),
        ['s'] = ToastPixels.Rgb(0xD6, 0xD2, 0xE0),
        ['b'] = ToastPixels.Rgb(0x58, 0xA5, 0x6E)
    };

    public static readonly string[] Empty = ["."];

    /// <summary>The shiny sparkle, big: four points and a white heart.</summary>
    public static readonly string[] SparkleBig =
    [
        "....k....",
        "...kwk...",
        "...kyk...",
        "..kyWyk..",
        "kwyWWWywk",
        "..kyWyk..",
        "...kyk...",
        "...kwk...",
        "....k...."
    ];

    /// <summary>The same sparkle, pulled in: the second frame of its blink.</summary>
    public static readonly string[] SparkleSmall =
    [
        ".........",
        ".........",
        "....k....",
        "...kyk...",
        "..kyWyk..",
        "...kyk...",
        "....k....",
        ".........",
        "........."
    ];

    public static readonly string[] SparkleTiny =
    [
        ".k.",
        "kWk",
        ".k."
    ];

    /// <summary>A prohibition sign, for the balls taken away.</summary>
    public static readonly string[] Forbidden =
    [
        "...kkkkk...",
        "..kRRRRRk..",
        ".kRRkkkRRk.",
        "kRRRRkkkRRk",
        "kRkkRRkkkRk",
        "kRkkkRRkkRk",
        "kRkkkkRRkRk",
        "kRRkkkkRRRk",
        ".kRRkkkRRk.",
        "..kRRRRRk..",
        "...kkkkk..."
    ];

    /// <summary>An arrow up, for the balls given back.</summary>
    public static readonly string[] Arrow =
    [
        "....k....",
        "...kGk...",
        "..kGGGk..",
        ".kGGGGGk.",
        "kGGGGGGGk",
        "kkkGGGkkk",
        "..kGGGk..",
        "..kgggk..",
        "..kkkkk.."
    ];

    /// <summary>The cemetery's cross, small.</summary>
    public static readonly string[] Cross =
    [
        "..kkk..",
        "..ksk..",
        "kkksmkk",
        "ksssssk",
        "kkkslkk",
        "..ksk..",
        "..ksk..",
        "..kmk..",
        "..kkk.."
    ];

    public static readonly string[] Bang =
    [
        ".kkk.",
        "kOOOk",
        "kOOOk",
        "kOOOk",
        ".kOk.",
        ".kOk.",
        ".kkk.",
        ".kOk.",
        ".kkk."
    ];

    /// <summary>A warning triangle, for something PermaLocke could not do and no Pokémon to show.</summary>
    public static readonly string[] Sign =
    [
        ".......k.......",
        "......kOk......",
        ".....kOOOk.....",
        ".....kOkOk.....",
        "....kOOkOOk....",
        "....kOOkOOk....",
        "...kOOOkOOOk...",
        "...kOOOkOOOk...",
        "..kOOOOOOOOOk..",
        "..kOOOOkOOOOk..",
        ".kOOOOOOOOOOOk.",
        "kkkkkkkkkkkkkkk"
    ];

    /// <summary>A headstone on a tuft of grass, for a team that fell or a death with no sprite.</summary>
    public static readonly string[] Grave =
    [
        "....kkkkkk....",
        "..kkmmmmmmkk..",
        ".kmmllllllmmk.",
        ".kmllllllllmk.",
        "kmlllllkklllmk",
        "kmllllkkkkllmk",
        "kmlllllkklllmk",
        "kmlllllkklllmk",
        "kmllllllllllmk",
        "kmllllllllllmk",
        "kmllllllllllmk",
        "kmmmmmmmmmmmdk",
        "kddddddddddddk",
        "kkkkkkkkkkkkkk",
        ".b.gb....bg.b."
    ];

    /// <summary>A Poké Ball drawn by hand, for when the cartridge's own icon is not there.</summary>
    public static readonly string[] Ball =
    [
        "...kkkkkk...",
        ".kkRRRRRRkk.",
        ".kRRRRWWRRk.",
        "kRRRRRRWRRRk",
        "kRRRRRRRRRRk",
        "kkkkkkkkkkkk",
        "kssskWWksssk",
        "ksssskkssssk",
        "kssssssssssk",
        ".kssssssssk.",
        ".kksssssskk.",
        "...kkkkkk..."
    ];
}
