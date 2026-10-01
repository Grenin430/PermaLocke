using System.Windows;
using System.Windows.Media;
using PermaLocke.App.Views;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Shells;

/// <summary>
/// A look in miniature (2026-10-01): the composition of its window drawn in 66 by 40 cells with its own colours, for the
/// gallery. No real data and no screenshot: a sketch cannot show a player's name, and it stays true to the XAML as long
/// as the shapes here follow it.
/// </summary>
public sealed class LayoutSketch : FrameworkElement
{
    public const int Columns = 66;
    public const int Rows = 40;

    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(PixelTheme), typeof(LayoutSketch),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Screen pixels per cell of the sketch: 2 for a thumbnail, 6 for the big preview; 0 takes the interface's own.</summary>
    public static readonly DependencyProperty CellSizeProperty = DependencyProperty.Register(
        nameof(CellSize), typeof(int), typeof(LayoutSketch),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public LayoutSketch()
    {
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public int CellSize
    {
        get => (int)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    private double Cell => CellSize > 0 ? CellSize / VisualTreeHelper.GetDpi(this).DpiScaleX : ToastPixels.Units(this, 1);

    public PixelTheme? Theme
    {
        get => (PixelTheme?)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Columns * Cell, Rows * Cell);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Theme is not { } theme) return;

        var canvas = new ToastPixels.Canvas(Columns, Rows, shades: false);
        var sketch = new Sketch(canvas, theme);
        sketch.Draw();
        drawingContext.DrawImage(canvas.ToBitmap(), new Rect(0, 0, Columns * Cell, Rows * Cell));
    }

    private sealed class Sketch(ToastPixels.Canvas canvas, PixelTheme theme)
    {
        private Color Ink => theme["PxInk"];

        private Color Face => theme["PxFace"];

        private Color Band => theme["PxFaceHigh"];

        private Color Lift => theme["PxFaceLift"];

        private Color Accent => theme["PxAccent"];

        private Color Base => theme["PxBase"];

        private Color Side => theme["PxSidebar"];

        private Color Text => theme["PxText"];

        private Color Dim => theme["PxTextDim"];

        private Color Good => theme["PxGood"];

        public void Draw()
        {
            // El marco de la ventana.
            Fill(0, 0, Columns, Rows, Ink);
            Fill(1, 1, Columns - 2, Rows - 2, Side);

            // Debajo de la barra de título (los tres botones de la ventana, a la derecha).
            Fill(Columns - 8, 2, 2, 2, Dim);
            Fill(Columns - 5, 2, 2, 2, Dim);
            Fill(3, 2, 14, 2, Text);

            switch (theme.Shell)
            {
                case ShellKind.Tabs: Tabs(); break;
                case ShellKind.Menu: Menu(); break;
                case ShellKind.Dock: Dock(); break;
                case ShellKind.Keys: Keys(); break;
                default: Rail(); break;
            }
        }

        private void Window(int x, int y, int w, int h, bool band = true)
        {
            Fill(x, y, w, h, Ink);
            Fill(x + 1, y + 1, w - 2, h - 2, Face);
            if (band && h > 5) Fill(x + 1, y + 1, w - 2, 3, Band);
            for (var i = 0; i < 3 && h > 8; i++) Fill(x + 3, y + (band ? 6 : 4) + (i * 3), Math.Max(2, w - 8 - (i * 5)), 1, Dim);
        }

        private void Backdrop(int x, int y, int w, int h) => Fill(x, y, w, h, Base);

        // ---------------------------------------------------------------- el de siempre: barra a un lado
        private void Rail()
        {
            Backdrop(17, 5, Columns - 19, Rows - 7);
            Fill(17, 5, Columns - 19, 7, Lift);                  // la cabecera con el cielo
            Fill(19, 7, 12, 3, Text);
            Fill(Columns - 15, 7, 11, 3, Accent);
            for (var i = 0; i < 8; i++)
            {
                Fill(4, 10 + (i * 3), 10, 2, i == 1 ? Accent : Dim);
            }

            Fill(3, 5, 3, 3, Accent);
            Window(19, 14, Columns - 23, 11);
            Window(19, 27, 20, 10);
            Window(41, 27, Columns - 45, 10);
        }

        // ---------------------------------------------------------------- pestañas arriba y diálogo abajo
        private void Tabs()
        {
            Backdrop(2, 5, Columns - 4, Rows - 7);
            for (var i = 0; i < 9; i++)
            {
                var x = 4 + (i * 7);
                var selected = i == 1;
                Fill(x, selected ? 5 : 6, 6, selected ? 5 : 4, selected ? Accent : Lift);
            }

            Fill(4, 11, Columns - 8, 3, Face);
            Fill(6, 12, 8, 1, Accent);
            Fill(16, 12, 8, 1, Dim);
            Window(4, 15, Columns - 8, 17, band: false);
            Fill(4, Rows - 7, Columns - 8, 5, Ink);
            Fill(5, Rows - 6, Columns - 10, 3, Face);
            Fill(7, Rows - 5, 3, 1, Accent);
            Fill(12, Rows - 5, 18, 1, Text);
            Fill(Columns - 14, Rows - 5, 8, 1, Dim);
        }

        // ---------------------------------------------------------------- menú de inicio en baldosas
        private void Menu()
        {
            Backdrop(2, 5, Columns - 4, Rows - 7);
            Fill(4, 6, 14, 3, Lift);                             // ◀ MENÚ
            Fill(Columns - 22, 6, 18, 3, Lift);
            Fill(Columns - 20, 7, 8, 1, Accent);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var x = 4 + (column * 14);
                    var y = 11 + (row * 9);
                    Fill(x, y, 13, 8, Ink);
                    Fill(x + 1, y + 1, 11, 6, row == 0 && column == 1 ? theme["PxAccentDeep"] : Face);
                    Fill(x + 2, y + 2, 3, 3, Accent);
                    Fill(x + 6, y + 2, 5, 1, Text);
                    Fill(x + 6, y + 4, 4, 1, Dim);
                }
            }

            Window(Columns - 22, 11, 18, 26, band: false);
            Fill(Columns - 20, 14, 10, 3, Accent);
            Fill(Columns - 20, 19, 13, 4, Lift);
        }

        // ---------------------------------------------------------------- HUD arriba y muelle abajo
        private void Dock()
        {
            Backdrop(2, 5, Columns - 4, Rows - 7);
            for (var i = 0; i < 18; i++) Fill(5 + ((i * 11) % (Columns - 10)), 6 + ((i * 7) % (Rows - 14)), 1, 1, Dim);

            Fill(4, 5, 22, 6, Accent);
            Fill(5, 6, 20, 4, Face);
            Fill(7, 7, 4, 2, Accent);
            Fill(13, 7, 9, 1, Text);
            Fill(Columns - 26, 5, 22, 6, Accent);
            Fill(Columns - 25, 6, 20, 4, Face);
            Fill(Columns - 22, 7, 8, 2, Accent);
            Fill(Columns - 12, 7, 5, 2, Lift);

            Window(4, 13, Columns - 8, 15);
            Fill(Columns / 2 - 12, Rows - 12, 24, 3, Face);       // las páginas, en pastillas
            Fill(Columns / 2 - 10, Rows - 11, 6, 1, Accent);
            Fill(Columns / 2 - 2, Rows - 11, 8, 1, Dim);
            for (var i = 0; i < 9; i++)
            {
                var selected = i == 2;
                var w = selected ? 10 : 4;
                var x = 5 + (i * 6) + (i > 2 ? 6 : 0);
                Fill(x, Rows - 8, w, 5, selected ? Accent : Ink);
                Fill(x + 1, Rows - 7, w - 2, 3, selected ? theme["PxAccentDeep"] : Face);
            }
        }

        // ---------------------------------------------------------------- aparato: teclas, pantalla, teclas blandas
        private void Keys()
        {
            Fill(1, 1, 17, Rows - 2, Side);
            Backdrop(18, 5, Columns - 20, Rows - 7);
            Fill(4, 6, 9, 5, Ink);                               // la lente
            Fill(5, 7, 7, 3, Accent);
            for (var i = 0; i < 10; i++)
            {
                var x = 3 + ((i % 2) * 7);
                var y = 13 + ((i / 2) * 5);
                Fill(x, y, 6, 4, Ink);
                Fill(x + 1, y + 1, 4, 2, i == 1 ? Accent : Face);
            }

            Fill(20, 6, Columns - 24, 4, Side);                  // la barra roja de la pantalla
            Fill(22, 7, 10, 2, Text);
            Fill(Columns - 14, 7, 8, 2, Accent);
            Fill(20, 11, Columns - 24, Rows - 21, Ink);          // el marco de la pantalla
            Fill(21, 12, Columns - 26, Rows - 23, theme["PxWell"]);
            Window(23, 14, Columns - 30, 8);
            Window(23, 24, Columns - 30, 6);
            Fill(20, Rows - 9, 14, 4, Face);                     // teclas blandas
            Fill(36, Rows - 9, 14, 4, Lift);
            Fill(20, Rows - 4, Columns - 24, 2, theme["PxGoodDeep"]);
            Fill(22, Rows - 4, 12, 1, Good);
        }

        private void Fill(int x, int y, int w, int h, Color colour)
        {
            for (var yy = y; yy < y + h; yy++)
            {
                for (var xx = x; xx < x + w; xx++) canvas.Put(xx, yy, colour);
            }
        }
    }
}
