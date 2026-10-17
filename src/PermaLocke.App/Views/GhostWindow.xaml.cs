using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// The blood rain over the emulator when the whole team falls (§184). It also held a friend's ghost and its notice in the tournament's app;
/// the singleplayer has neither (2026-10-17, the notices taken out).
/// </summary>
public partial class GhostWindow : Window
{
    private readonly DispatcherTimer _follow;

    public GhostWindow()
    {
        InitializeComponent();
        OverlayWindows.MakeUntouchable(this);

        _follow = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Render,
            (_, _) => OverlayWindows.PlaceOver(this, Target()), Dispatcher);
    }

    /// <summary>Blood rain over the emulator for <see cref="BloodRain.Length"/> (§184). Completes when the last drop is gone.</summary>
    public async Task RainAsync()
    {
        ShowOver();
        await FallAsync();
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

    private static int CellFor(double height) => Math.Max(3, (int)Math.Round(height / 140));

    private static (int Left, int Top, int Width, int Height) Target() =>
        GameWindow.ClientBox(GameWindow.Handle()) ?? OwnWindow() ?? OverlayWindows.WorkArea();

    /// <summary>PermaLocke's own window when it is on screen (1.0.5.9): without the game, the ghost crosses it.</summary>
    private static (int Left, int Top, int Width, int Height)? OwnWindow() =>
        Application.Current?.MainWindow is { IsVisible: true, WindowState: not WindowState.Minimized } main
            ? GameWindow.ClientBox(new System.Windows.Interop.WindowInteropHelper(main).Handle)
            : null;
}
