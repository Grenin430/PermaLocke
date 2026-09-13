using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>Whether the news is good, bad or neither. Decides the colour of the strip.</summary>
public enum ToastTone
{
    Neutral,
    Good,
    Bad
}

/// <summary>One thing worth saying while somebody is playing.</summary>
public sealed record Toast(string Title, string Message, ToastTone Tone, BitmapSource? Icon = null)
{
    public bool HasIcon => Icon is not null;
}

/// <summary>
/// Says things on top of the game, so PermaLocke can be minimised and still be useful.
/// </summary>
/// <remarks>
/// <para>
/// Everything the application does on its own — registering a death, handing over a prize, applying
/// the level cap — was only visible on HOME. While you play, PermaLocke is behind the emulator, so
/// in practice none of it was visible <b>at the moment it happened</b>, which is the only moment it
/// matters.
/// </para>
/// <para>
/// The notice is its own window, not part of the main one: a minimised window draws nothing, and
/// this has to appear while the main one is minimised. It sits over Azahar when Azahar is there,
/// and in the corner of the screen when it is not.
/// </para>
/// <para>
/// <b>It never takes the focus and never eats a click.</b> Both are deliberate and both are a flag
/// on the window: a notice that steals the focus takes you out of the game mid-battle, and one that
/// swallows a click eats a turn. <c>WS_EX_NOACTIVATE</c> and <c>WS_EX_TRANSPARENT</c>.
/// </para>
/// </remarks>
public sealed class Notifier(IUiDispatcher ui, ILogger<Notifier> logger)
{
    /// <summary>How long a notice stays before it fades.</summary>
    private static readonly TimeSpan Linger = TimeSpan.FromSeconds(6);

    /// <summary>At most this many at once; older ones go first.</summary>
    private const int AtMost = 4;

    private ToastWindow? _window;

    /// <summary>What is on screen right now, newest at the bottom.</summary>
    public ObservableCollection<Toast> Showing { get; } = [];

    /// <summary>Turned off while nobody wants to be interrupted. Nothing is queued up meanwhile.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Something has just been said. The tab uses it to light its dot.</summary>
    public event EventHandler? Said;

    public void Say(string title, string message, ToastTone tone = ToastTone.Neutral,
        BitmapSource? icon = null)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        // NADA DE LO DE AQUI PUEDE SALIR HACIA FUERA. Esto lo llama el vigilante desde su hilo
        // en mitad de registrar una muerte: un aviso que falla tiene que costar el aviso y nada
        // mas. Ya paso una vez -una excepcion de aqui se llevo por delante todo el ciclo del monitor-.
        try
        {
            Said?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al anunciar un aviso");
        }

        _ = ui.InvokeAsync(() =>
        {
            var toast = new Toast(title, message, tone, icon);

            Showing.Add(toast);

            while (Showing.Count > AtMost)
            {
                Showing.RemoveAt(0);
            }

            Open();

            // Se retira sola. Un temporizador por aviso y no una cola: dos noticias a la vez son
            // dos noticias, y hacer esperar a la segunda es esconderla.
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = Linger };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Showing.Remove(toast);

                if (Showing.Count == 0)
                {
                    _window?.Hide();
                }
            };

            timer.Start();

            return Task.CompletedTask;
        });
    }

    /// <summary>Puts the window where the player is looking, which is the game and not us.</summary>
    private void Open()
    {
        if (_window is null)
        {
            _window = new ToastWindow { DataContext = this };
            _window.Closed += (_, _) => _window = null;
        }

        var target = GameWindow.Area();

        _window.Width = 380;
        _window.Height = 300;
        _window.Left = target.Right - _window.Width - 24;
        _window.Top = target.Bottom - _window.Height - 24;

        if (!_window.IsVisible)
        {
            _window.Show();
            OverlayWindows.MakeUntouchable(_window);
        }

        // Topmost se vuelve a pedir cada vez: otra ventana que se pone delante puede desbancarla,
        // y un aviso que sale detras del juego es un aviso que no existe.
        _window.Topmost = false;
        _window.Topmost = true;
    }
}
