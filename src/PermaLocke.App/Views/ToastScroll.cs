using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PermaLocke.App.Services;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

/// <summary>
/// A notice's body: a parchment scroll with a wooden roll at each end and, on the big card, a wax seal in the notice's
/// colour. It opens towards the left — the right roll stays where it is and the left one travels, turning — and the text
/// inside is uncovered as the paper goes by (2026-10-08, chosen from the prototypes).
/// </summary>
/// <remarks>
/// Drawn cell by cell like the rest of the notices (<see cref="ToastPixels"/>): hard edges, a solid shadow, and the motion
/// a whole cell at a time. The paper's stains come from a hash of the cell counted from the right roll, so they do not swim while
/// the scroll opens. Anything that is not the paper — the rolls, the seal — is painted under the content, which is clipped
/// to the sheet that is open at that step.
/// </remarks>
public sealed class ToastScroll : Decorator
{
    /// <summary>How far the shadow falls, in cells, down and to the right.</summary>
    private const int Shadow = 2;

    /// <summary>How long the scroll takes to open, and to roll up again.</summary>
    private static readonly TimeSpan OpenTime = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan CloseTime = TimeSpan.FromMilliseconds(360);

    private static readonly Color Paper = ToastPixels.Rgb(0xF0, 0xDF, 0xB0);
    private static readonly Color PaperLow = ToastPixels.Rgb(0xD8, 0xBE, 0x84);
    private static readonly Color Stain = ToastPixels.Rgb(0xA8, 0x88, 0x50);
    private static readonly Color Edge = ToastPixels.Rgb(0x3A, 0x24, 0x10);
    private static readonly Color RollLight = ToastPixels.Rgb(0xF2, 0xE2, 0xB4);
    private static readonly Color RollDark = ToastPixels.Rgb(0x8E, 0x6A, 0x3A);

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ToastKind), typeof(ToastScroll),
        new FrameworkPropertyMetadata(ToastKind.Info, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0 is closed, 1 is wide open. The entrance moves it; anything else can set it.</summary>
    public static readonly DependencyProperty RevealProperty = DependencyProperty.Register(
        nameof(Reveal), typeof(double), typeof(ToastScroll),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ToastScroll)d).ApplyClip()));

    /// <summary>When the notice appeared; with <see cref="Linger"/>, when it rolls itself up again.</summary>
    public static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        nameof(Shown), typeof(DateTime), typeof(ToastScroll), new FrameworkPropertyMetadata(DateTime.MinValue));

    /// <summary>How long it stays. Infinite (a pinned notice) never rolls up by itself.</summary>
    public static readonly DependencyProperty LingerProperty = DependencyProperty.Register(
        nameof(Linger), typeof(TimeSpan), typeof(ToastScroll), new FrameworkPropertyMetadata(Timeout.InfiniteTimeSpan));

    private static readonly DependencyPropertyKey IsOpenKey = DependencyProperty.RegisterReadOnly(
        nameof(IsOpen), typeof(bool), typeof(ToastScroll), new FrameworkPropertyMetadata(false));

    /// <summary>Wide open and not rolling up: the tab of the notice shows only then.</summary>
    public static readonly DependencyProperty IsOpenProperty = IsOpenKey.DependencyProperty;

    private readonly DispatcherTimer _close = new();
    private DateTime _motionStart;
    private double _motionFrom;
    private bool _moving;
    private bool _opened;
    private bool _closing;

    public ToastScroll()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Reveal = 0.04;

        _close.Tick += (_, _) =>
        {
            _close.Stop();
            SetValue(IsOpenKey, false);
            Move(closing: true);
        };

        Loaded += (_, _) =>
        {
            // Volver a cargarse (cambio de ventana, de tema) no la cierra ni la abre otra vez: sigue donde estaba, y lo
            // que se paró al descargarse (la apertura a medias, el cierre programado) vuelve a ponerse en marcha.
            if (_opened)
            {
                if (!_closing && Reveal < 1) Move(closing: false);
                if (!_closing) ScheduleClose();
                return;
            }

            _opened = true;
            Move(closing: false);
            ScheduleClose();
        };
        Unloaded += (_, _) =>
        {
            Stop();
            _close.Stop();
        };
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

    public bool IsOpen => (bool)GetValue(IsOpenProperty);

    /// <summary>
    /// Opens or rolls up following the screen's own frames (<see cref="CompositionTarget.Rendering"/>), not a timer:
    /// fast at first and settling at the end when it opens, slow to start and quick at the end when it rolls up. The
    /// rolls still move a whole cell at a time, so it stays pixel art; it just never skips cells.
    /// </summary>
    private void Move(bool closing)
    {
        _closing = closing;
        _motionStart = DateTime.UtcNow;
        _motionFrom = Reveal;

        // Con «menos movimiento» en Windows, sin animación: abierto al momento, y al irse se va sin enrollarse.
        if (PixelTheme.ReducedMotion)
        {
            Reveal = closing ? Reveal : 1;
            SetValue(IsOpenKey, !closing);
            return;
        }

        if (!_moving)
        {
            _moving = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    private void Stop()
    {
        if (!_moving) return;
        _moving = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var length = _closing ? CloseTime : OpenTime;
        var t = Math.Clamp((DateTime.UtcNow - _motionStart).TotalMilliseconds / length.TotalMilliseconds, 0, 1);

        // Al abrir, salida rápida y frenada suave; al cerrar, arranque lento que se acelera hasta juntar los rollos.
        // Desde donde esté: un cierre que llega con la apertura a medias se enrolla desde ahí, sin saltar a abierto.
        var reveal = _closing ? _motionFrom * (1 - (t * t * t)) : _motionFrom + ((1 - _motionFrom) * (1 - Math.Pow(1 - t, 3)));

        // Solo se repinta cuando el rollo cambia de celda.
        if (Left(reveal) != Left(Reveal) || t >= 1)
        {
            Reveal = reveal;
        }

        if (t < 1)
        {
            return;
        }

        Stop();
        Reveal = _closing ? 0 : 1;
        SetValue(IsOpenKey, !_closing);
    }

    /// <summary>
    /// Starts rolling up so that the rolls meet just as the notifier takes the notice away (<see cref="Services.Notifier"/>
    /// removes it at <see cref="Linger"/>), with a little room.
    /// </summary>
    private void ScheduleClose()
    {
        if (Linger == Timeout.InfiniteTimeSpan || Linger <= TimeSpan.Zero || Shown == DateTime.MinValue)
        {
            return;
        }

        var start = Shown + Linger - CloseTime - TimeSpan.FromMilliseconds(60);
        var wait = start - DateTime.UtcNow;
        _close.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1);
        _close.Start();
    }

    public ToastKind Kind
    {
        get => (ToastKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Reveal
    {
        get => (double)GetValue(RevealProperty);
        set => SetValue(RevealProperty, Math.Clamp(value, 0, 1));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = base.ArrangeOverride(arrangeSize);
        ApplyClip();
        return size;
    }

    /// <summary>The cells the geometry of the scroll uses: its width, height and the sheet that is open.</summary>
    private (int Columns, int Rows, int Roll, int Left, int Right) Geometry() => Geometry(Reveal);

    private (int Columns, int Rows, int Roll, int Left, int Right) Geometry(double reveal)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Floor(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Floor(ActualHeight * dpi.DpiScaleY / cell);
        var roll = rows < 30 ? 5 : 8;
        var box = columns - Shadow;
        var left = (int)Math.Round(Math.Max(0, box - (2 * roll)) * (1 - reveal));
        return (columns, rows, roll, left + roll, box - roll);
    }

    /// <summary>The cell where the sheet starts for an opening: what decides whether a frame changes anything.</summary>
    private int Left(double reveal) => Geometry(reveal).Left;

    private void ApplyClip()
    {
        if (Child is not { } child || ActualWidth <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var (_, _, _, left, right) = Geometry();
        child.Clip = new RectangleGeometry(new Rect(
            left * cell / dpi.DpiScaleX, 0, Math.Max(0, (right - left) * cell / dpi.DpiScaleX), ActualHeight));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var (columns, rows, roll, left, right) = Geometry();

        if (columns < 24 || rows < 12)
        {
            return;
        }

        var canvas = new ToastPixels.Canvas(columns, rows);
        var box = columns - Shadow;
        var height = rows - Shadow;
        var small = rows < 30;

        // La sombra de todo lo que se ve: los dos rollos y la hoja, bajados y a la derecha.
        canvas.Box(left - roll + Shadow, Shadow + 1, roll, height - 2, Colors.Black, alpha: 150, notched: false);
        canvas.Box(right + Shadow, Shadow + 1, roll, height - 2, Colors.Black, alpha: 150, notched: false);
        canvas.Box(left + Shadow, Shadow + 3, right - left, height - 6, Colors.Black, alpha: 150, notched: false);

        PaintSheet(canvas, left, right, height);
        PaintRoll(canvas, left - roll, roll, height, left);
        PaintRoll(canvas, right, roll, height, left);

        if (!small)
        {
            PaintSeal(canvas, right - 2, height - 9, ToastPixels.AccentOf(Kind));
        }

        ToastPixels.Draw(drawingContext, this, canvas);
    }

    private static int Hash(int x, int y) => (int)(((uint)((x * 73856093) ^ (y * 19349663))) >> 3);

    private static void PaintSheet(ToastPixels.Canvas canvas, int left, int right, int height)
    {
        // La hoja se mete tres celdas bajo cada rollo, para que no asome hueco al abrirse.
        for (var x = left - 3; x < right + 3; x++)
        {
            var wave = (int)Math.Round(Math.Sin((right - x) * 0.35));
            var top = 3 + wave;
            var bottom = height - 4 + wave;

            for (var y = top; y <= bottom; y++)
            {
                var t = (y - top) / (double)Math.Max(1, bottom - top);
                var colour = t < 0.34 ? Paper : t < 0.68 ? ToastPixels.Mix(Paper, PaperLow, 0.4) : ToastPixels.Mix(Paper, PaperLow, 0.8);
                var hash = Hash(right - x, y);

                if (hash % 31 == 0)
                {
                    colour = ToastPixels.Mix(colour, Stain, 0.35);
                }
                else if (hash % 97 == 1)
                {
                    colour = ToastPixels.Mix(colour, Stain, 0.6);
                }

                if (y == top || y == bottom)
                {
                    colour = Edge;
                }
                else if ((y == top + 3 || y == bottom - 3) && x > left + 1 && x < right - 1 && (right - x) % 2 == 0)
                {
                    colour = ToastPixels.Mix(colour, Stain, 0.75);
                }

                canvas.Put(x, y, colour);
            }
        }

        // La sombra que el rollo echa sobre el papel, a los dos lados.
        for (var y = 5; y < height - 5; y++)
        {
            canvas.Put(left, y, ToastPixels.Mix(PaperLow, Colors.Black, 0.30));
            canvas.Put(left + 1, y, ToastPixels.Mix(PaperLow, Colors.Black, 0.12));
            canvas.Put(right - 1, y, ToastPixels.Mix(PaperLow, Colors.Black, 0.30));
            canvas.Put(right - 2, y, ToastPixels.Mix(PaperLow, Colors.Black, 0.12));
        }
    }

    /// <summary>A wooden roll seen from the front: lit a little left of its middle, its ends darker, its grain turning with <paramref name="turn"/>.</summary>
    private static void PaintRoll(ToastPixels.Canvas canvas, int x0, int width, int height, int turn)
    {
        for (var i = 0; i < width; i++)
        {
            var shade = Math.Clamp(Math.Abs(((i + 0.5) / width) - 0.38) * 2.2, 0, 1);
            shade = Math.Round(shade * 3) / 3;

            for (var y = 0; y < height; y++)
            {
                var corner = (i == 0 || i == width - 1) && (y == 0 || y == height - 1);

                if (corner)
                {
                    continue;
                }

                var colour = ToastPixels.Mix(RollLight, RollDark, shade);

                if (y < 3 || y >= height - 3)
                {
                    // Las puntas del rollo: la madera cortada, más oscura.
                    colour = ToastPixels.Mix(colour, Edge, 0.40 + (shade * 0.2));
                }
                else if (i == 1 + (((width / 2) - 1 + turn) % (width - 2)) && (y / 3) % 2 == 0)
                {
                    colour = ToastPixels.Mix(colour, Edge, 0.22);
                }

                if (i == 0 || i == width - 1 || y == 0 || y == height - 1)
                {
                    colour = Edge;
                }

                canvas.Put(x0 + i, y, colour);
            }
        }
    }

    /// <summary>The wax seal, in the notice's colour, hanging from its two ribbon tails.</summary>
    private static void PaintSeal(ToastPixels.Canvas canvas, int cx, int cy, Color accent)
    {
        var dark = ToastPixels.Mix(accent, Colors.Black, 0.40);
        var light = ToastPixels.Mix(accent, Colors.White, 0.40);

        for (var y = cy + 3; y < cy + 9; y++)
        {
            for (var k = 0; k < 3; k++)
            {
                canvas.Put(cx - 4 + k, y, accent);
                canvas.Put(cx + 1 + k, y, dark);
            }

            canvas.Put(cx - 5, y, Edge);
            canvas.Put(cx - 1, y, Edge);
            canvas.Put(cx, y, Edge);
            canvas.Put(cx + 4, y, Edge);
        }

        for (var y = cy - 6; y <= cy + 6; y++)
        {
            for (var x = cx - 6; x <= cx + 6; x++)
            {
                var distance = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));

                if (distance > 5.5)
                {
                    continue;
                }

                canvas.Put(x, y, distance > 4.4 ? Edge : distance > 3.4 ? (x < cx ? light : dark) : distance > 2.2 ? accent : dark);
            }
        }

        // La marca del lacre: una ball, que es de lo que va todo esto.
        for (var x = cx - 2; x <= cx + 2; x++)
        {
            canvas.Put(x, cy, Edge);
        }

        canvas.Put(cx, cy - 1, light);
        canvas.Put(cx, cy + 1, light);
    }
}
