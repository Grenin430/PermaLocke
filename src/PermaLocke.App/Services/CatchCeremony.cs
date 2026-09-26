using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.App.Services;

/// <summary>
/// A wild Pokémon caught becomes its card, and the card flies into the album over the game (§190).
/// </summary>
/// <remarks>
/// <para>
/// Told by <see cref="EncounterGuard.Caught"/> when the game's own record of captures goes up, which is at the end of
/// the battle: the game does not count a capture before. So it plays as the field comes back, with a ball of its own
/// where the trainer stands, not over the ball of the battle.
/// </para>
/// <para>
/// The card is the album's own (<see cref="TcgCardFactory"/>), made from the wild Pokémon as it was read in the battle:
/// level, moves, nature and figures are the real ones. Nothing is written anywhere; with no Pokémon read, nothing is
/// shown rather than a card made up.
/// </para>
/// </remarks>
public sealed class CatchCeremony(IUiDispatcher ui, SaveBoxReader boxes, TcgCardFactory cards, KillcamRecorder killcam,
    ILogger<CatchCeremony> logger)
{
    /// <summary>Cards waiting their turn. Only touched on the UI thread.</summary>
    private readonly Queue<TcgCard> _waiting = new();

    private bool _playing;
    private CatchWindow? _window;

    /// <summary>Off in CONFIGURACIÓN: the capture is detected all the same, only the card is not shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Plays one capture. Safe to call from any thread.</summary>
    public void Celebrate(WildCatch caught)
    {
        if (!Enabled)
        {
            return;
        }

        if (caught.Pokemon is not { } pokemon)
        {
            logger.LogInformation("Captura sin el Pokémon leído en el combate: no hay carta que enseñar");
            return;
        }

        _ = ui.InvokeAsync(async () =>
        {
            try
            {
                _waiting.Enqueue(cards.Make(boxes.Describe(pokemon)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo hacer la carta de la captura");
                return;
            }

            if (!_playing)
            {
                await PlayAllAsync();
            }
        });
    }

    /// <summary>
    /// A capture without the game, for <c>--ensayar-captura</c>: the card of a Pokémon of the save, shown as if it had just
    /// been caught. Nothing is recorded.
    /// </summary>
    public Task RehearseAsync(BoxedPokemon pokemon) => ui.InvokeAsync(async () =>
    {
        _waiting.Enqueue(cards.Make(pokemon));
        if (!_playing)
        {
            await PlayAllAsync();
        }
    });

    private async Task PlayAllAsync()
    {
        _playing = true;
        killcam.CoverBegins();

        try
        {
            while (_waiting.Count > 0)
            {
                var card = _waiting.Dequeue();
                var front = TcgCardArt.Render(card, TcgLayout.Full);
                var back = TcgCardArt.Render(card, TcgLayout.Full, back: true);
                var (box, pixel) = TopScreen();

                logger.LogInformation("Carta de la captura: {Pokemon}", card.Name);
                _window ??= new CatchWindow();
                await _window.PlayAsync(front, back, card, box, pixel);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la animación de la carta de una captura; la captura sí cuenta");
            _waiting.Clear();
        }
        finally
        {
            _playing = false;
            _window?.Hide();
            killcam.CoverEnds();
        }
    }

    /// <summary>
    /// The emulator's top screen in monitor pixels, and how big a pixel of the game is there; without the emulator, a
    /// screen of the same shape in the middle of the desktop.
    /// </summary>
    private static ((int Left, int Top, int Width, int Height) Box, double Pixel) TopScreen()
    {
        if (GameWindow.TopScreen() is { } top)
        {
            return (((int)Math.Round(top.Left), (int)Math.Round(top.Top),
                (int)Math.Round(CatchScene.ScreenWidth * top.Scale), (int)Math.Round(CatchScene.ScreenHeight * top.Scale)), top.Scale);
        }

        var area = OverlayWindows.WorkArea();
        var pixel = Math.Max(1, Math.Floor(Math.Min(area.Width / 500.0, area.Height / 300.0)));
        var width = (int)(CatchScene.ScreenWidth * pixel);
        var height = (int)(CatchScene.ScreenHeight * pixel);
        return ((area.Left + ((area.Width - width) / 2), area.Top + ((area.Height - height) / 2), width, height), pixel);
    }
}
