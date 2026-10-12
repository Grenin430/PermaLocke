using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>
/// The blood rain when the whole team falls (§184), here only: in the singleplayer app there is nobody else to send it to.
/// Over the emulator while it is open; one rain at a time.
/// </summary>
public sealed class WipeRain(EmulatorLauncher launcher)
{
    private GhostWindow? _window;
    private bool _raining;

    /// <summary>Starts the rain. Called from the watcher's thread; the window is the screen's.</summary>
    public void Play() => System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
    {
        if (_raining || !launcher.IsRunning) return;
        _raining = true;
        try
        {
            _window ??= new GhostWindow();
            await _window.RainAsync(null);
        }
        finally
        {
            _raining = false;
        }
    });

    /// <summary><c>--ensayar-lluvia</c>: the rain without the game, to look at it.</summary>
    public async Task RehearseAsync()
    {
        _window ??= new GhostWindow();
        await _window.RainAsync(null);
    }
}
