using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// A friend's fallen Pokémon over this player's emulator (§183): the notice at the top left, then its ghost crossing
/// the screen from right to left.
/// </summary>
/// <remarks>
/// Presentation only, like <see cref="DeathWindow"/>: what to show comes resolved. The ghost moves in whole cells of
/// its own pixels, a step at a time, and floats one cell up and down; nothing is smoothed.
/// </remarks>
public partial class GhostWindow : Window
{
    /// <summary>How long the ghost takes to cross, whatever the width of the emulator.</summary>
    private static readonly TimeSpan Crossing = TimeSpan.FromSeconds(4.5);

    private readonly DispatcherTimer _follow;

    public GhostWindow()
    {
        InitializeComponent();
        OverlayWindows.MakeUntouchable(this);

        _follow = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Render,
            (_, _) => OverlayWindows.PlaceOver(this, Target()), Dispatcher);
    }

    /// <summary>Shows the notice, and when it has gone, lets the ghost cross. Completes when it is out of sight.</summary>
    public async Task PlayAsync(Toast notice, BitmapSource? ghost)
    {
        ShowOver();
        await CardAsync(notice);

        if (ghost is not null)
        {
            await CrossAsync(ghost);
        }
    }

    /// <summary>
    /// Blood rain over the emulator for <see cref="BloodRain.Length"/> (§184), with the notice at the top left while it
    /// starts if there is one. Completes when the last drop is gone.
    /// </summary>
    public async Task RainAsync(Toast? notice)
    {
        ShowOver();
        var rain = FallAsync();

        if (notice is not null)
        {
            await CardAsync(notice);
        }

        await rain;
    }

    private void ShowOver()
    {
        OverlayWindows.PlaceOver(this, Target());

        if (!IsVisible)
        {
            Show();
        }

        _follow.Start();
        UpdateLayout();
    }

    private async Task CardAsync(Toast notice)
    {
        // El aviso entra a saltos desde la izquierda, como los otros entran desde la derecha.
        Card.DataContext = notice;
        Card.Visibility = Visibility.Visible;

        foreach (var x in new[] { -36.0, -22, -8, 0 })
        {
            Slide.X = x;
            await Task.Delay(50);
        }

        await Task.Delay(notice.Linger);
        Card.Visibility = Visibility.Collapsed;
    }

    private Task FallAsync()
    {
        // Las mismas celdas que el fantasma: una gota es un píxel del juego, no una raya fina.
        var cell = CellFor(ActualHeight);
        var rain = new BloodRain((int)Math.Ceiling(ActualWidth / cell), (int)Math.Ceiling(ActualHeight / cell),
            Environment.TickCount);
        var bitmap = new WriteableBitmap(rain.Columns, rain.Rows, 96, 96, PixelFormats.Bgra32, null);
        var whole = new Int32Rect(0, 0, rain.Columns, rain.Rows);

        Rain.Source = bitmap;
        Rain.Width = rain.Columns * cell;
        Rain.Height = rain.Rows * cell;
        Rain.Visibility = Visibility.Visible;

        var clock = Stopwatch.StartNew();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Frame(object? sender, EventArgs e)
        {
            var due = (int)(clock.Elapsed / BloodRain.Step);

            if (rain.Frame >= due && !rain.Done)
            {
                return;
            }

            while (rain.Frame < due && !rain.Done)
            {
                rain.Advance();
            }

            rain.Draw();
            bitmap.WritePixels(whole, rain.Pixels, rain.Columns * 4, 0);

            if (rain.Done)
            {
                CompositionTarget.Rendering -= Frame;
                Rain.Visibility = Visibility.Collapsed;
                Rain.Source = null;
                done.TrySetResult();
            }
        }

        CompositionTarget.Rendering += Frame;
        return done.Task;
    }

    /// <summary>Nothing left to show.</summary>
    public void Done()
    {
        _follow.Stop();
        Hide();
    }

    private Task CrossAsync(BitmapSource ghost)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        // Píxeles del sprite de tamaño entero: un fantasma estirado a trozos dejaría de ser pixel art.
        var cell = CellFor(height);
        var w = ghost.PixelWidth * cell;
        var h = ghost.PixelHeight * cell;
        var baseline = Snap((height * 0.42) - (h / 2), cell);

        Ghost.Source = ghost;
        Ghost.Width = w;
        Ghost.Height = h;
        Ghost.Opacity = 1;

        var clock = Stopwatch.StartNew();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Frame(object? sender, EventArgs e)
        {
            var t = clock.Elapsed.TotalMilliseconds / Crossing.TotalMilliseconds;

            if (t >= 1)
            {
                CompositionTarget.Rendering -= Frame;
                Ghost.Opacity = 0;
                done.TrySetResult();
                return;
            }

            // A pasos de 45 ms: se mueve como un sprite de juego, no se desliza.
            var stepped = Math.Floor(clock.Elapsed.TotalMilliseconds / 45) * 45 / Crossing.TotalMilliseconds;
            Canvas.SetLeft(Ghost, Snap(width - ((width + w) * stepped), cell));
            Canvas.SetTop(Ghost, baseline + ((clock.ElapsedMilliseconds / 300 % 2) * cell));
        }

        Canvas.SetLeft(Ghost, Snap(width, cell));
        Canvas.SetTop(Ghost, baseline);
        CompositionTarget.Rendering += Frame;
        return done.Task;
    }

    private static int CellFor(double height) => Math.Max(3, (int)Math.Round(height / 140));

    private static double Snap(double value, int cell) => Math.Floor(value / cell) * cell;

    private static (int Left, int Top, int Width, int Height) Target() =>
        GameWindow.ClientBox(GameWindow.Handle()) ?? OverlayWindows.WorkArea();
}
