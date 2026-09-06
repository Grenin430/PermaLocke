using PermaLocke.Infrastructure;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>
/// The tab that brings PermaLocke back while you are playing.
/// </summary>
/// <remarks>
/// <para>
/// The same floating window as the notices, with <b>one flag fewer</b>: the notices carry
/// <c>WS_EX_TRANSPARENT</c> so a click goes through to the game, and this one must not — it exists
/// to be clicked. It keeps <c>WS_EX_NOACTIVATE</c>, so hovering it never takes the focus off the
/// game; the click restores the main window, and that one does take it, which is the point.
/// </para>
/// <para>
/// It shows only when it is useful: <b>PermaLocke minimised and the emulator running</b>. Always on
/// screen it would be in the way exactly when you are using the application, and with no emulator
/// there is nothing to be on top of.
/// </para>
/// </remarks>
public sealed partial class EdgeTab : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly DispatcherTimer _watch = new() { Interval = TimeSpan.FromSeconds(1) };

    private EdgeTabWindow? _tab;
    private Window? _main;

    public EdgeTab(AppPaths paths, Notifier notifier)
    {
        _paths = paths;
        _watch.Tick += (_, _) => Decide();

        // Algo que ha pasado mientras no mirabas. El punto se enciende solo si estabas fuera: si
        // tenias la aplicacion delante, ya lo has visto y un aviso pendiente seria mentira.
        // AL HILO DE LA INTERFAZ, SIEMPRE. Esto lo dispara el vigilante desde su propio hilo, y
        // leer WindowState desde ahi lanza: una ventana de WPF solo se deja tocar por el hilo que
        // la creo. Sin este salto, el adorno se llevo por delante la deteccion de muertes -la
        // excepcion subia hasta InspectAsync-, que es exactamente lo que el §96 ya habia costado
        // una vez: una comodidad no puede tumbar el trabajo.
        notifier.Said += (_, _) => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_main?.WindowState == WindowState.Minimized)
            {
                Pending = true;
            }
        });
    }

    /// <summary>
    /// PermaLocke steps aside by itself when the emulator opens.
    /// </summary>
    /// <remarks>
    /// Asked for, and it removes the one bit of friction left: with the application behind the
    /// emulator, the first click on its taskbar button <b>brings it to the front</b> and only the
    /// second minimises it — that is Windows, not us, and no amount of polling fixes it. Getting
    /// out of the way on its own means never needing that click at all.
    /// <para>
    /// On the emulator <b>appearing</b> and not on it merely running, so it happens once and does
    /// not fight somebody who deliberately brought the application back while playing.
    /// </para>
    /// </remarks>
    public bool StepAside { get; set; } = true;

    /// <summary>Whether the emulator was there last time we looked, to spot it arriving.</summary>
    private bool _emulatorWasThere;

    /// <summary>Something happened while the application was out of sight.</summary>
    [ObservableProperty]
    private bool _pending;

    /// <summary>Starts watching, once the main window exists.</summary>
    public void Attach(Window main)
    {
        _main = main;
        main.StateChanged += (_, _) => Decide();
        _watch.Start();
        Decide();
    }

    /// <summary>Shows the tab when it is worth showing, and hides it otherwise.</summary>
    private void Decide()
    {
        var emulator = EmulatorIsRunning();

        // El emulador ACABA de aparecer: la aplicacion se aparta sola y deja la pestaña.
        if (emulator && !_emulatorWasThere && StepAside
            && _main is { WindowState: not WindowState.Minimized })
        {
            _main.WindowState = WindowState.Minimized;
        }

        _emulatorWasThere = emulator;

        if (_main?.WindowState != WindowState.Minimized || !emulator)
        {
            _tab?.Hide();
            return;
        }

        Open();
    }

    private static bool EmulatorIsRunning()
    {
        foreach (var name in new[] { "azahar", "citra" })
        {
            if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Open()
    {
        if (_tab is null)
        {
            _tab = new EdgeTabWindow { DataContext = this };
            _tab.Opened += (_, _) => Restore();
            _tab.Moved += (_, top) => Remember(top);

            // Si alguien la cierra -el apagado de WPF, sin ir mas lejos- se olvida y se hace otra
            // la proxima vez. Guardar una ventana cerrada es guardar algo que ya no se puede
            // enseñar.
            _tab.Closed += (_, _) => _tab = null;
        }

        // La pantalla DEL EMULADOR, no la principal: con dos monitores y el juego en el segundo,
        // el borde de la principal es un sitio donde nadie esta mirando.
        var area = GameWindow.Screen();

        _tab.Left = area.Left;
        _tab.Top = Math.Clamp(Recall() ?? (area.Top + ((area.Height - _tab.Height) / 2)),
            area.Top, area.Bottom - _tab.Height);

        if (!_tab.IsVisible)
        {
            _tab.Show();
            NeverTakeTheFocus(_tab);
        }

        // Se vuelve a subir en CADA vuelta, y con SetWindowPos y no toqueteando Topmost: otra
        // ventana que se ponga delante puede desbancarla, y una pestaña detras del juego no la
        // encuentra nadie. SWP_NOACTIVATE, o subirla le robaria el foco al juego.
        Raise(_tab);
    }

    private void Restore()
    {
        Pending = false;
        _tab?.Hide();

        if (_main is null)
        {
            return;
        }

        _main.WindowState = WindowState.Normal;
        _main.Activate();

        (_main as MainWindow)?.PlayUnfold();
    }

    /// <summary>Where the player left it, in <c>Config/</c> like every other per-machine thing.</summary>
    private string SettingsPath => Path.Combine(_paths.Config, "pestana.json");

    private double? Recall()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<TabPlace>(File.ReadAllText(SettingsPath))?.Top
                : null;
        }
        catch (Exception)
        {
            // Una posicion guardada que no se deja leer no es motivo para no tener pestaña.
            return null;
        }
    }

    private void Remember(double top)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new TabPlace { Top = top }));
        }
        catch (Exception)
        {
            // Ni para no dejarla donde estaba.
        }
    }

    private sealed record TabPlace
    {
        public double Top { get; init; }
    }

    /// <summary>Clickable, but never the active window.</summary>
    private static void NeverTakeTheFocus(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Sin WS_EX_TRANSPARENT, que es la diferencia con los avisos: esta SI recibe el raton.
        SetWindowLong(handle, GwlExStyle,
            GetWindowLong(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
    }

    /// <summary>Puts it back on top without touching the focus or moving it.</summary>
    private static void Raise(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, Topmost, 0, 0, 0, 0, NoMove | NoSize | NoActivate);
        }
    }

    private static readonly IntPtr Topmost = new(-1);
    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y,
        int width, int height, uint flags);

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x8000000;
    private const int WsExToolWindow = 0x80;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
