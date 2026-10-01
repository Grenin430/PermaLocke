using System.Windows;
using System.Windows.Media;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// A box drawn cell by cell in any colour: one-cell outline, one-cell bevel lit on top, notched corners and a hard
/// shadow. The same box as the notices (<see cref="ToastFrame"/>), for places whose colour is data — a type badge is
/// the type's colour.
/// </summary>
/// <remarks>
/// <see cref="IsSunken"/> turns the bevel round, dark on top and lit underneath, which reads as a hole instead of a
/// plate: the holes of the PC. The fill's own alpha is honoured, so a hole can let the box wallpaper show through.
/// </remarks>
public sealed class PixelPanel : FrameworkElement
{
    /// <summary>How far the shadow falls, in cells.</summary>
    private const int Shadow = 2;

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(ToastPixels.Face, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasShadowProperty = DependencyProperty.Register(
        nameof(HasShadow), typeof(bool), typeof(PixelPanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSunkenProperty = DependencyProperty.Register(
        nameof(IsSunken), typeof(bool), typeof(PixelPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderHeightProperty = DependencyProperty.Register(
        nameof(HeaderHeight), typeof(double), typeof(PixelPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderFillProperty = DependencyProperty.Register(
        nameof(HeaderFill), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(Color.FromRgb(0x21, 0x1A, 0x3A), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderAccentProperty = DependencyProperty.Register(
        nameof(HeaderAccent), typeof(Color), typeof(PixelPanel),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// A title band across the top, this tall in units; zero for none. It is how a panel becomes a window of a game's
    /// menu: the band in its own colour, an ink line under it, and a short accent under the start of the line.
    /// </summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public Color HeaderFill
    {
        get => (Color)GetValue(HeaderFillProperty);
        set => SetValue(HeaderFillProperty, value);
    }

    /// <summary>The short stripe under the start of the band; transparent for none.</summary>
    public Color HeaderAccent
    {
        get => (Color)GetValue(HeaderAccentProperty);
        set => SetValue(HeaderAccentProperty, value);
    }

    public PixelPanel()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public Color Fill
    {
        get => (Color)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    public bool IsSunken
    {
        get => (bool)GetValue(IsSunkenProperty);
        set => SetValue(IsSunkenProperty, value);
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

        if (columns < 6 || rows < 5)
        {
            return;
        }

        // El hueco de la sombra solo se guarda en los estilos que la pintan: con los otros, la placa usa todo su sitio.
        var style = PixelTheme.Current.Panels;
        var shadow = HasShadow && style is PanelStyle.Bevel or PanelStyle.Heavy or PanelStyle.Device ? Shadow : 0;
        var width = columns - shadow;
        var height = rows - shadow;
        var canvas = new ToastPixels.Canvas(columns, rows);
        var alpha = Fill.A;

        var inset = style switch
        {
            PanelStyle.Rounded => DrawRounded(canvas, width, height, alpha),
            PanelStyle.Heavy => DrawHeavy(canvas, width, height, shadow, alpha),
            PanelStyle.Neon => DrawNeon(canvas, width, height, alpha),
            PanelStyle.Device => DrawDevice(canvas, width, height, shadow, alpha),
            _ => DrawBevel(canvas, width, height, shadow, alpha)
        };

        // La banda del título: su color con su propio brillo arriba, una línea de tinta debajo y el acento que arranca.
        var band = (int)Math.Round(HeaderHeight * dpi.DpiScaleY / cell);
        // La banda empieza donde acaba el borde de cada estilo: la altura es la misma (HeaderHeight), y PixelWindow baja su
        // título lo que el borde sea más grueso que el de siempre.
        var bandEnd = inset + band - 1;
        if (band > 2 && bandEnd < height - 4)
        {
            var bandLight = ToastPixels.Mix(HeaderFill, Colors.White, 0.16);
            for (var y = inset; y <= bandEnd; y++)
            {
                for (var x = inset; x < width - inset; x++)
                {
                    var corner = y == inset && (x == inset || x == width - inset - 1);
                    if (!corner) canvas.Put(x, y, y == inset ? bandLight : HeaderFill, alpha);
                }
            }

            for (var x = inset; x < width - inset; x++)
            {
                canvas.Put(x, bandEnd + 1, ToastPixels.Ink, alpha);
            }

            if (HeaderAccent.A > 0)
            {
                for (var x = inset; x < Math.Min(width - inset, 16); x++)
                {
                    canvas.Put(x, bandEnd + 2, HeaderAccent, alpha);
                }
            }
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }

    /// <summary>The original box. Returns how many cells the edge takes, for the title band.</summary>
    private int DrawBevel(ToastPixels.Canvas canvas, int width, int height, int shadow, byte alpha)
    {
        var light = ToastPixels.Mix(Fill, Colors.White, 0.22);
        var dark = ToastPixels.Mix(Fill, Colors.Black, 0.30);
        var top = IsSunken ? dark : light;
        var bottom = IsSunken ? light : dark;

        if (shadow > 0)
        {
            canvas.Box(shadow, shadow, width, height, Colors.Black, alpha: 150);
        }

        canvas.Box(0, 0, width, height, ToastPixels.Ink, alpha);
        canvas.Box(1, 1, width - 2, height - 2, Fill, alpha, notched: false);

        for (var x = 2; x < width - 2; x++)
        {
            canvas.Put(x, 1, top, alpha);
            canvas.Put(x, height - 2, bottom, alpha);
        }

        for (var y = 2; y < height - 2; y++)
        {
            canvas.Put(1, y, top, alpha);
            canvas.Put(width - 2, y, bottom, alpha);
        }

        return 1;
    }

    /// <summary>
    /// ESMERALDA: round corners of two cells, a dark line and a pale one inside it, no shadow. A hole is the same shape
    /// with only a soft line and its top row in shade.
    /// </summary>
    private int DrawRounded(ToastPixels.Canvas canvas, int width, int height, byte alpha)
    {
        var theme = PixelTheme.Current;
        bool Inside(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            var cx = x < 2 ? x : x >= width - 2 ? width - 1 - x : 2;
            var cy = y < 2 ? y : y >= height - 2 ? height - 1 - y : 2;
            return cx + cy >= 2;
        }

        bool Edge(int x, int y) => Inside(x, y) && (!Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1));

        var line = IsSunken ? ToastPixels.Mix(Fill, theme.Ink, 0.35) : theme.Ink;
        var shade = ToastPixels.Mix(Fill, theme.Ink, 0.12);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!Inside(x, y)) continue;

                if (Edge(x, y))
                {
                    canvas.Put(x, y, line, alpha);
                    continue;
                }

                var ring = Edge(x - 1, y) || Edge(x + 1, y) || Edge(x, y - 1) || Edge(x, y + 1)
                           || Edge(x - 1, y - 1) || Edge(x + 1, y - 1) || Edge(x - 1, y + 1) || Edge(x + 1, y + 1);
                var colour = !ring ? Fill
                    : IsSunken ? (y <= 1 || x <= 1 ? shade : Fill)
                    : theme.Rim;
                canvas.Put(x, y, colour, alpha);
            }
        }

        return 2;
    }

    /// <summary>GAME BOY: a double ink line with the fill between, square corners, a solid shadow of one cell.</summary>
    private int DrawHeavy(ToastPixels.Canvas canvas, int width, int height, int shadow, byte alpha)
    {
        var theme = PixelTheme.Current;
        var ink = theme.Ink;
        var thin = IsSunken || width < 12 || height < 14;

        if (shadow > 0)
        {
            canvas.Box(1, 1, width, height, theme.Rim, alpha, notched: false);
        }

        canvas.Box(0, 0, width, height, ink, alpha, notched: false);
        canvas.Box(1, 1, width - 2, height - 2, Fill, alpha, notched: false);

        if (thin)
        {
            if (IsSunken)
            {
                var shade = ToastPixels.Mix(Fill, ink, 0.3);
                for (var x = 1; x < width - 1; x++) canvas.Put(x, 1, shade, alpha);
                for (var y = 1; y < height - 1; y++) canvas.Put(1, y, shade, alpha);
            }

            return 1;
        }

        canvas.Box(2, 2, width - 4, height - 4, ink, alpha, notched: false);
        canvas.Box(3, 3, width - 6, height - 6, Fill, alpha, notched: false);
        return 3;
    }

    /// <summary>
    /// ULTRAUMBRAL: one line of light round dark glass, brighter corners, and a dithered glow just inside the line. A
    /// bright fill (a button) gets a pale line of its own colour instead.
    /// </summary>
    private int DrawNeon(ToastPixels.Canvas canvas, int width, int height, byte alpha)
    {
        var theme = PixelTheme.Current;
        var luma = ((Fill.R * 0.299) + (Fill.G * 0.587) + (Fill.B * 0.114)) / 255.0;
        var tube = luma > 0.35 ? ToastPixels.Mix(Fill, Colors.White, 0.45) : theme["PxAccent"];
        if (IsSunken) tube = ToastPixels.Mix(tube, Fill, 0.6);
        var glow = ToastPixels.Mix(Fill, tube, 0.28);

        canvas.Box(0, 0, width, height, tube, alpha);
        canvas.Box(1, 1, width - 2, height - 2, Fill, alpha, notched: false);

        for (var x = 1; x < width - 1; x++)
        {
            if ((x & 1) == 0) canvas.Put(x, 1, glow, alpha);
            if ((x & 1) == 1) canvas.Put(x, height - 2, glow, alpha);
        }

        for (var y = 2; y < height - 2; y++)
        {
            if ((y & 1) == 1) canvas.Put(1, y, glow, alpha);
            if ((y & 1) == 0) canvas.Put(width - 2, y, glow, alpha);
        }

        if (!IsSunken && width >= 12 && height >= 10)
        {
            // Las esquinas, en el segundo color: una L de tres celdas en cada una.
            var corner = theme.Rim;
            for (var i = 1; i <= 3; i++)
            {
                canvas.Put(i, 0, corner, alpha);
                canvas.Put(0, i, corner, alpha);
                canvas.Put(width - 1 - i, 0, corner, alpha);
                canvas.Put(width - 1, i, corner, alpha);
                canvas.Put(i, height - 1, corner, alpha);
                canvas.Put(0, height - 1 - i, corner, alpha);
                canvas.Put(width - 1 - i, height - 1, corner, alpha);
                canvas.Put(width - 1, height - 1 - i, corner, alpha);
            }
        }

        return 1;
    }

    /// <summary>
    /// ROTOM DEX: a screen in a device — ink outline, a pale bezel two cells wide lit on top, a dark inner line, and the
    /// fill as the screen. A hole is only the inner line. Small plates get a bezel of one cell.
    /// </summary>
    private int DrawDevice(ToastPixels.Canvas canvas, int width, int height, int shadow, byte alpha)
    {
        var theme = PixelTheme.Current;

        if (shadow > 0)
        {
            canvas.Box(shadow, shadow, width, height, Colors.Black, alpha: 150);
        }

        canvas.Box(0, 0, width, height, theme.Ink, alpha);
        var screenLine = ToastPixels.Mix(Fill, Colors.Black, 0.45);
        // Una placa pequeña (una tecla, un botón) no cabe con marco de pantalla: el borde pálido de un solo grosor.
        if (width < 16 || height < 14)
        {
            var smallLight = ToastPixels.Mix(theme.Rim, Colors.White, 0.55);
            var smallDark = ToastPixels.Mix(theme.Rim, Colors.Black, 0.28);
            canvas.Box(1, 1, width - 2, height - 2, Fill, alpha, notched: false);
            for (var x = 1; x < width - 1; x++)
            {
                canvas.Put(x, 1, IsSunken ? smallDark : smallLight, alpha);
                canvas.Put(x, height - 2, IsSunken ? smallLight : smallDark, alpha);
            }

            for (var y = 2; y < height - 2; y++)
            {
                canvas.Put(1, y, IsSunken ? smallDark : smallLight, alpha);
                canvas.Put(width - 2, y, IsSunken ? smallLight : smallDark, alpha);
            }

            return 1;
        }


        if (IsSunken)
        {
            canvas.Box(1, 1, width - 2, height - 2, screenLine, alpha, notched: false);
            canvas.Box(2, 2, width - 4, height - 4, Fill, alpha, notched: false);
            return 2;
        }

        var bezel = width >= 16 && height >= 14 ? 2 : 1;
        var rim = theme.Rim;
        var rimLight = ToastPixels.Mix(rim, Colors.White, 0.55);
        var rimDark = ToastPixels.Mix(rim, Colors.Black, 0.28);

        canvas.Box(1, 1, width - 2, height - 2, rim, alpha, notched: false);
        for (var x = 1; x < width - 1; x++)
        {
            canvas.Put(x, 1, rimLight, alpha);
            canvas.Put(x, height - 2, rimDark, alpha);
        }

        canvas.Box(1 + bezel, 1 + bezel, width - 2 - (2 * bezel), height - 2 - (2 * bezel), screenLine, alpha, notched: false);
        canvas.Box(2 + bezel, 2 + bezel, width - 4 - (2 * bezel), height - 4 - (2 * bezel), Fill, alpha, notched: false);
        return 2 + bezel;
    }
}
