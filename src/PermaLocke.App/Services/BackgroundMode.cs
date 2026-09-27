using System.Windows;
using Microsoft.Extensions.Logging;

namespace PermaLocke.App.Services;

/// <summary>
/// «Seguir en segundo plano al cerrar» (1.0.5.4): closing the window hides it instead, with an icon by the clock, and the
/// friends' ghosts keep coming. Off by default. Discord stops saying «Jugando a PermaLocke» while hidden.
/// </summary>
/// <remarks>
/// Opening PermaLocke again (the shortcut) brings the hidden window back: the second copy rings <see cref="ShowSignal"/>
/// and leaves. The icon's menu has «Cerrar del todo», which closes as if the setting were off.
/// </remarks>
public sealed class BackgroundMode(AppSettings settings, DiscordPresence presence, ILogger<BackgroundMode> logger)
{
    private const string ShowSignal = @"Local\PermaLocke.App.Show";

    private System.Windows.Forms.NotifyIcon? _icon;
    private Window? _window;
    private bool _quitting;

    /// <summary>For the second copy: asks the hidden one to show itself. False when nobody was listening.</summary>
    public static bool AskOtherToShow()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(ShowSignal);
            return signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    public void Attach(Window window)
    {
        _window = window;

        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
        var listener = new Thread(() =>
        {
            while (signal.WaitOne())
            {
                window.Dispatcher.BeginInvoke(Restore);
            }
        }) { IsBackground = true, Name = "PermaLocke show signal" };
        listener.Start();

        // Después de la comprobación del juego abierto, que se añade antes: si esa cancela, aquí no se esconde nada.
        window.Closing += (_, closing) =>
        {
            if (closing.Cancel || _quitting || !settings.Current.Background)
            {
                return;
            }

            closing.Cancel = true;
            Hide();
        };
        window.Closed += (_, _) => _icon?.Dispose();
    }

    private void Hide()
    {
        _window!.Hide();
        presence.Stop();

        if (_icon is null)
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir PermaLocke", null, (_, _) => Restore());
            menu.Items.Add("Cerrar del todo", null, (_, _) => Quit());

            _icon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
                Text = "PermaLocke (segundo plano)",
                ContextMenuStrip = menu
            };
            _icon.DoubleClick += (_, _) => Restore();
        }

        _icon.Visible = true;
        logger.LogInformation("PermaLocke sigue en segundo plano");
    }

    private void Restore()
    {
        if (_window is null) return;

        if (_icon is { Visible: true })
        {
            _icon.Visible = false;
            presence.Start();
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void Quit()
    {
        _quitting = true;
        _window?.Close();
        _quitting = false;
    }
}
