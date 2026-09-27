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

    // Como IDisposable y no como NotifyIcon: ver ShowIcon.
    private IDisposable? _icon;
    private bool _hidden;
    private Window? _window;
    private bool _quitting;

    /// <summary>Set before PermaLocke restarts itself (an update, a transfer): closing then really closes (1.0.5.10).</summary>
    public static bool Leaving { get; set; }

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
            if (closing.Cancel || _quitting || Leaving || !settings.Current.Background)
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
        ShowIcon(true);
        _hidden = true;
        logger.LogInformation("PermaLocke sigue en segundo plano");
    }

    /// <summary>
    /// Everything that touches WinForms, apart (1.0.5.10): a method that names its types makes the runtime load the
    /// assembly when it is compiled, and the close of an update — running from the renamed <c>.old</c> — could not.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ShowIcon(bool visible)
    {
        if (_icon is null)
        {
            if (!visible) return;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir PermaLocke", null, (_, _) => Restore());
            menu.Items.Add("Cerrar del todo", null, (_, _) => Quit());

            var icon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
                Text = "PermaLocke (segundo plano)",
                ContextMenuStrip = menu
            };
            icon.DoubleClick += (_, _) => Restore();
            _icon = icon;
        }

        ((System.Windows.Forms.NotifyIcon)_icon).Visible = visible;
    }

    private void Restore()
    {
        if (_window is null) return;

        if (_hidden)
        {
            _hidden = false;
            ShowIcon(false);
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
