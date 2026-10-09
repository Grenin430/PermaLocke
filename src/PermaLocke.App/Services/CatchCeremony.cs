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
    PokemonSpriteService sprites, IItemLookup items, PermaLocke.Core.Domain.IShopCatalog shop, WorldItemCatalog worldItems,
    ILogger<CatchCeremony> logger)
{
    /// <summary>Items celebrated so far in this run of the app: with the item's id it is the seed, so the same item never plays twice alike.</summary>
    private int _itemsSeen;

    /// <summary>Cards waiting their turn. Only touched on the UI thread.</summary>
    private readonly Queue<object> _waiting = new();

    private bool _playing;
    private CatchWindow? _window;

    /// <summary>1 is real time. The rehearsal (<c>--ensayar-lento</c>) lowers it to be able to look at an animation and capture a frame.</summary>
    public double Speed { get; set; } = 1;

    /// <summary>Off in CONFIGURACIÓN: the capture is detected all the same, only the card is not shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>What was celebrated lately, so a wild capture that lands in the party does not get two cards.</summary>
    private readonly Dictionary<uint, DateTime> _recent = [];

    /// <summary>
    /// A Pokémon that arrived without a wild battle (2026-09-28): egg, fossil, gift. The same card and the same flight.
    /// Skipped when that same Pokémon was just celebrated as a capture.
    /// </summary>
    public void CelebrateNewcomer(PKHeX.Core.PK7 pokemon)
    {
        if (!Enabled || pokemon.IsEgg) return;

        _ = ui.InvokeAsync(async () =>
        {
            if (_recent.TryGetValue(pokemon.PID, out var at) && DateTime.UtcNow - at < TimeSpan.FromMinutes(2)) return;
            _recent[pokemon.PID] = DateTime.UtcNow;

            try
            {
                _waiting.Enqueue(cards.Make(boxes.Describe(pokemon)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo hacer la carta del Pokémon nuevo");
                return;
            }

            if (!_playing)
            {
                await PlayAllAsync();
            }
        });
    }

    /// <summary>
    /// An item the player just got (2026-09-28): off the floor, bought or given. Its icon from the cartridge jumps into a
    /// bag over the game, in the same queue as the cards. Safe to call from any thread.
    /// </summary>
    public void CelebrateItem(int itemId, int amount)
    {
        if (!Enabled) return;

        _ = ui.InvokeAsync(async () =>
        {
            try
            {
                await sprites.PrepareAsync();
                // Los objetos del mod (megapiedras nuevas) no están en la lista de PKHeX, que da otro nombre con su
                // número: la TIENDA los tiene con el suyo (1.0.7.9, una Heatranita salía como Caramelo Exeggcute).
                var name = (shop.Items.FirstOrDefault(i => i.Id == itemId)?.Name ?? items.GetName(itemId)).ToUpperInvariant();

                var (icon, width, height) = sprites.GetItem(itemId) is { } bitmap
                    ? (CemeteryScene.Pixels(bitmap), bitmap.PixelWidth, bitmap.PixelHeight)
                    : ItemScene.Parcel();
                var kind = worldItems.Current.Classify(itemId);
                _waiting.Enqueue(new ItemScene.Item(icon, width, height, name, amount, kind.Category, kind.Power, ItemTint.Of(icon),
                    ItemScene.SeedFor(itemId, _itemsSeen++)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo preparar la animación del objeto {Item}", itemId);
                return;
            }

            if (!_playing)
            {
                await PlayAllAsync();
            }
        });
    }

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
                if (_recent.TryGetValue(pokemon.PID, out var at) && DateTime.UtcNow - at < TimeSpan.FromMinutes(2)) return;
                _recent[pokemon.PID] = DateTime.UtcNow;
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
                var next = _waiting.Dequeue();
                if (next is ItemScene.Item item)
                {
                    var (corner, itemPixel) = BesideBottomScreen(ItemScene.HeightFor(item));
                    logger.LogInformation("Objeto a la mochila: {Item} ×{Amount}", item.Name, item.Amount);
                    _window ??= new CatchWindow();

                    // The player carrying on (a key or a button of the game going down) sends it out at once; so does another
                    // item waiting, at triple speed. What is held when it begins does not count: see GameInputWatch.
                    var input = new GameInputWatch();
                    input.Begin();
                    await _window.PlayAsync(item, corner, itemPixel, () => _waiting.Count > 0, input.Advanced, Speed);
                    continue;
                }

                var card = (TcgCard)next;
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
    /// Where the item animation goes (2026-09-28, where the organiser drew it): in the dark band left of the bottom screen,
    /// centred in it and level with the middle of that screen, at the game's own pixel size, smaller if the band is narrow.
    /// Without the emulator, the bottom-left of the desktop.
    /// </summary>
    /// <param name="sceneHeight">The height the item's style asks for, in pixels of the game: only the height changes with it,
    /// the pixel is still computed from the width, so a taller scene is the same drawing scaled the same.</param>
    private static ((int Left, int Top) Corner, double Pixel) BesideBottomScreen(int sceneHeight)
    {
        var handle = GameWindow.Handle();
        if (GameWindow.RenderBox(handle) is { } render && GameWindow.TopScreen(handle) is { } top)
        {
            var bottomLeft = top.Left + (40 * top.Scale);
            var band = bottomLeft - render.Left;
            var pixel = Math.Max(1, Math.Min(top.Scale, band * 0.9 / ItemScene.SceneWidth));
            var width = ItemScene.SceneWidth * pixel;
            var height = sceneHeight * pixel;
            var middle = top.Top + (360 * top.Scale);
            return (((int)Math.Round(render.Left + ((band - width) / 2)), (int)Math.Round(middle - (height / 2))), pixel);
        }

        var area = OverlayWindows.WorkArea();
        return ((area.Left + 24, area.Top + area.Height - (sceneHeight * 3) - 24), 3);
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
