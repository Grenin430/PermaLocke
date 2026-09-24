using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>One thing worth saying while somebody is playing.</summary>
/// <param name="Sprite">From the player's cartridge: the Pokémon, the ball or the item it is about. Null draws a mark alone.</param>
/// <param name="Shown">When it appeared, for the segments that count down its time on screen.</param>
public sealed record Toast(ToastKind Kind, string Title, string Message, BitmapSource? Sprite, DateTime Shown, TimeSpan Linger)
{
    /// <summary>The word on its tab.</summary>
    public string Label => Kind switch
    {
        ToastKind.BallsTaken or ToastKind.BallsBack => "POKÉ BALLS",
        ToastKind.FirstEncounter => "PRIMER ENCUENTRO",
        ToastKind.Shiny => "VARIOCOLOR",
        ToastKind.AllowedCapture => "CAPTURA PERMITIDA",
        ToastKind.Death => "BAJA",
        ToastKind.TeamWipe => "EQUIPO CAÍDO",
        ToastKind.Reward => "PREMIO",
        ToastKind.Warning => "ATENCIÓN",
        _ => "PERMALOCKE"
    };

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
}

/// <summary>
/// Says things on top of the game, so PermaLocke can be minimised and still be useful.
/// </summary>
/// <remarks>
/// <para>
/// Everything the application does on its own — registering a death, handing over a prize, taking the Poké Balls
/// away — was only visible on HOME. While you play, PermaLocke is behind the emulator, so in practice none of it was
/// visible <b>at the moment it happened</b>, which is the only moment it matters.
/// </para>
/// <para>
/// The notice is its own window, not part of the main one: a minimised window draws nothing, and this has to appear
/// while the main one is minimised. It sits over Azahar when Azahar is there, and in the corner of the screen when
/// it is not.
/// </para>
/// <para>
/// <b>It never takes the focus and never eats a click.</b> Both are deliberate and both are a flag on the window: a
/// notice that steals the focus takes you out of the game mid-battle, and one that swallows a click eats a turn.
/// <c>WS_EX_NOACTIVATE</c> and <c>WS_EX_TRANSPARENT</c>.
/// </para>
/// </remarks>
public sealed class Notifier(IUiDispatcher ui, ILogger<Notifier> logger)
{
    /// <summary>How long a notice stays before it goes.</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(6);

    /// <summary>At most this many at once; older ones go first.</summary>
    private const int AtMost = 4;

    private ToastWindow? _window;

    /// <summary>What is on screen right now, newest at the bottom.</summary>
    public ObservableCollection<Toast> Showing { get; } = [];

    /// <summary>Turned off while nobody wants to be interrupted. Nothing is queued up meanwhile.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Something has just been said. The tab uses it to light its dot.</summary>
    public event EventHandler? Said;

    public void Say(ToastKind kind, string title, string message, BitmapSource? sprite = null) =>
        _ = SayAsync(kind, title, message, sprite);

    // Observe the UI task here: a failed window must never interrupt game monitoring or disappear silently.
    internal async Task SayAsync(ToastKind kind, string title, string message, BitmapSource? sprite = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        if (!Enabled)
        {
            logger.LogInformation("Aviso omitido: notificaciones desactivadas ({Kind})", kind);
            return;
        }

        logger.LogInformation("Aviso solicitado: {Kind} — {Title}", kind, title);
        try
        {
            Said?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al anunciar un aviso");
        }

        try
        {
            await ui.InvokeAsync(() =>
            {
                if (!Enabled) return Task.CompletedTask;
                var toast = new Toast(kind, title, message, sprite, DateTime.UtcNow, Linger);
                Showing.Add(toast);
                while (Showing.Count > AtMost) Showing.RemoveAt(0);

                try
                {
                    Open();
                }
                catch
                {
                    Showing.Remove(toast);
                    throw;
                }

                var timer = new System.Windows.Threading.DispatcherTimer { Interval = Linger };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    Showing.Remove(toast);
                    if (Showing.Count == 0) _window?.Hide();
                };
                timer.Start();
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo mostrar el aviso {Kind} — {Title}", kind, title);
        }
    }

    /// <summary>Anchors in native screen pixels; WPF's Left/Top are not screen pixels at higher scaling.</summary>
    private void Open()
    {
        if (_window is null)
        {
            _window = new ToastWindow { DataContext = this, Width = 470, Height = 760 };
            _window.Closed += (_, _) => _window = null;
            OverlayWindows.MakeUntouchable(_window);
        }

        var game = GameWindow.Handle();
        var anchor = game;
        if (anchor == IntPtr.Zero && System.Windows.Application.Current?.MainWindow is { } main)
            anchor = new System.Windows.Interop.WindowInteropHelper(main).Handle;
        var work = GameWindow.WorkArea(anchor);
        var target = GameWindow.ClientBox(game) ?? work;

        if (!_window.IsVisible) _window.Show();

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_window);
        var box = ToastPlacement.Calculate(target, work, dpi.DpiScaleX, dpi.DpiScaleY);
        Place(box);

        // Moving between monitors can change WPF's DPI. Recalculate once using the destination scale.
        var destinationDpi = System.Windows.Media.VisualTreeHelper.GetDpi(_window);
        if (destinationDpi.DpiScaleX != dpi.DpiScaleX || destinationDpi.DpiScaleY != dpi.DpiScaleY)
        {
            dpi = destinationDpi;
            box = ToastPlacement.Calculate(target, work, dpi.DpiScaleX, dpi.DpiScaleY);
            Place(box);
        }

        logger.LogInformation(
            "Ventana de avisos mostrada: {Box}; escala {ScaleX}x{ScaleY}; monitor {Work}; juego {Game}",
            box, dpi.DpiScaleX, dpi.DpiScaleY, work, game != IntPtr.Zero);
    }

    private void Place((int Left, int Top, int Width, int Height) box)
    {
        if (!OverlayWindows.PlaceOver(_window!, box))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }
}
