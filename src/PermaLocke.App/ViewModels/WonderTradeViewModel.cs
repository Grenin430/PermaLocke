using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One type of the received Pokémon, with the colour the games have always used.</summary>
public sealed record TypeBadgeViewModel(string Name, Brush Colour);

/// <summary>
/// The wonder trade, as the viewer offers it: arm it, pick a Pokémon, confirm, watch.
/// </summary>
/// <remarks>
/// <para>
/// The order is the same as the gacha and for the same reason: the trade is <b>decided, written
/// into the save and recorded</b> before a single frame plays. The animation shows a result that
/// already exists; it never stands in for a pending computation.
/// </para>
/// <para>
/// This is the only screen in PermaLocke that destroys something of the player's, so it says
/// exactly what is going to happen before it happens, and refuses outright while the game is open.
/// </para>
/// </remarks>
public sealed partial class WonderTradeViewModel : ObservableObject
{
    private readonly WonderTradeService _trades;
    private readonly IPokemonSwap _swap;
    private readonly PokemonIdentityService _identity;
    private readonly CreditService _credits;
    private readonly IRunContext _runContext;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<WonderTradeViewModel> _logger;

    public WonderTradeViewModel(WonderTradeService trades, IPokemonSwap swap,
        PokemonIdentityService identity, CreditService credits, IRunContext runContext,
        PokemonSpriteService sprites, ILogger<WonderTradeViewModel> logger)
    {
        _trades = trades;
        _swap = swap;
        _identity = identity;
        _credits = credits;
        _runContext = runContext;
        _sprites = sprites;
        _logger = logger;
    }

    /// <summary>Raised when the trade is over and the boxes on screen are out of date.</summary>
    public event EventHandler? Finished;

    /// <summary>True once the player has armed the trade and is choosing what to give.</summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>True while the animation runs, which locks the rest of the screen.</summary>
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isWorking;

    /// <summary>What the player is about to hand over. Null while nothing is picked.</summary>
    [ObservableProperty]
    private BoxedPokemon? _given;

    [ObservableProperty]
    private BitmapSource? _givenSprite;

    [ObservableProperty]
    private BitmapSource? _receivedSprite;

    /// <summary>
    /// The trade the cabin is playing, with the moment it was confirmed. The cabin draws itself from it; this only
    /// waits for the Pokémon to be out before the card with its data comes up.
    /// </summary>
    [ObservableProperty]
    private TradePlay? _currentPlay;

    /// <summary>Colour of the received Pokémon's first type, for the strip on its card.</summary>
    [ObservableProperty]
    private Brush _typeColour = Brushes.Transparent;

    [ObservableProperty]
    private WonderTradeOffer? _offer;

    /// <summary>"Lo que vuelva valdrá entre 460 y 550", worked out before anything is decided.</summary>
    [ObservableProperty]
    private string _bandText = string.Empty;

    /// <summary>
    /// What the panel says right now: how to pick while nothing is picked, and what the trade
    /// will cost and return once something is.
    /// </summary>
    public string Instruction => BandText.Length > 0
        ? BandText
        : "Elige el Pokémon que quieres entregar, del equipo o de una caja.";

    partial void OnBandTextChanged(string value) => OnPropertyChanged(nameof(Instruction));

    [ObservableProperty]
    private string _problem = string.Empty;

    /// <summary>Wonder trades left, from the trials. Only meaningful when they are limited.</summary>
    [ObservableProperty]
    private int _left;

    /// <summary>True when the competition is counting them, so the screen can show the number.</summary>
    public bool IsLimited => _credits.LimitsWonderTrades;

    /// <summary>Refreshes how many are left. Asked for, never kept: the history owns the number.</summary>
    public async Task RefreshCreditsAsync()
    {
        if (_runContext.Current is not { } run)
        {
            Left = 0;
            return;
        }

        try
        {
            Left = (await _credits.AvailableAsync(run)).WonderTrades;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al contar los wonder trades disponibles");
        }
    }

    // Las tres pistas de antes -generación, tipos y total- las dice ya la pantalla de la cabina (§174); esto solo
    // enciende la ficha cuando el Pokémon ha salido.
    [ObservableProperty]
    private bool _showPokemon;

    public List<TypeBadgeViewModel> Types { get; private set; } = [];

    public string GenerationText => Offer is null ? string.Empty : $"GENERACIÓN {Offer.Generation}";

    public string TotalText => Offer is null ? string.Empty : Offer.BaseStatTotal.ToString();

    /// <summary>"+6 %" or "-3 %" against what was handed over, which is the whole point of the band.</summary>
    public string DifferenceText => Offer is null
        ? string.Empty
        : $"{(Offer.Difference >= 0 ? "+" : string.Empty)}{Offer.Difference} % sobre {Offer.GivenName}";

    public bool CanConfirm => Given is not null && !GivenIsFallen && !IsPlaying && !IsWorking;

    /// <summary>The picked Pokémon is dead in the run, and a wonder trade only takes the living.</summary>
    [ObservableProperty]
    private bool _givenIsFallen;

    partial void OnGivenIsFallenChanged(bool value) => ConfirmCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Says it the moment a fallen Pokémon is picked, rather than after confirming. The service refuses it again on
    /// its own, so this is the courtesy and not the rule.
    /// </summary>
    private async Task CheckFallenAsync(BoxedPokemon pokemon)
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        try
        {
            var fallen = await _trades.IsFallenAsync(run.Id, pokemon.Pid);

            // Si mientras tanto se ha elegido a otro, esta respuesta ya no es de nadie.
            if (!ReferenceEquals(Given, pokemon))
            {
                return;
            }

            GivenIsFallen = fallen;

            if (fallen)
            {
                BandText = string.Empty;
                Problem = WonderTradeService.FallenMessage(pokemon.DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al comprobar si {Name} está vivo", pokemon.DisplayName);
        }
    }

    /// <summary>Turns the trade on and off. Picking a Pokémon happens in the box behind it.</summary>
    [RelayCommand]
    private void Arm()
    {
        if (IsPlaying)
        {
            return;
        }

        IsArmed = !IsArmed;
        Problem = string.Empty;
        _ = RefreshCreditsAsync();

        if (!IsArmed)
        {
            Clear();
        }
        else if (!_swap.CanSwapNow(out var reason))
        {
            // Se dice ya, antes de que elija a nadie: enterarse de que no se puede después de
            // haber elegido al Pokémon que ibas a entregar es peor.
            Problem = reason;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        IsArmed = false;
        Clear();
    }

    /// <summary>Offers a Pokémon to the trade. Ignored unless the trade is armed.</summary>
    public void Choose(BoxedPokemon? pokemon)
    {
        if (!IsArmed || IsPlaying)
        {
            return;
        }

        Given = pokemon;
        GivenIsFallen = false;
        GivenSprite = pokemon is null ? null : _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny);

        if (pokemon is null)
        {
            BandText = string.Empty;
            return;
        }

        var total = _trades.BaseStatTotalOf(pokemon.Species);

        if (total == 0)
        {
            BandText = string.Empty;
            Problem = $"{pokemon.SpeciesName} no se puede intercambiar.";
            return;
        }

        var (min, max) = _trades.Window.Band(total);
        Problem = string.Empty;
        BandText = $"Recibirás un Pokémon de nivel {pokemon.Level} con estadísticas totales entre {min} y {max}.";

        // Lo último, para que el aviso de caído no lo borren las líneas de arriba si la respuesta llega en el acto.
        _ = CheckFallenAsync(pokemon);
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (Given is not { } given || _runContext.Current is not { } run)
        {
            return;
        }

        IsWorking = true;
        ConfirmCommand.NotifyCanExecuteChanged();

        try
        {
            if (!_swap.CanSwapNow(out var reason))
            {
                Problem = reason;
                return;
            }

            // Un intercambio gasta un credito de los que dan las pruebas. Se comprueba aqui y no
            // en el servicio porque es una regla de la competicion y no un hecho del intercambio;
            // con limitarWonderTrades a false en Data/grants.json vuelven a ser libres.
            if (_credits.LimitsWonderTrades && Left <= 0)
            {
                Problem = "No te quedan wonder trades. Consigues más superando pruebas.";
                return;
            }

            var gift = new WonderTradeGift(given.Species, given.DisplayName, given.Level, given.Box, given.Slot, given.Pid);
            var result = await _trades.TradeAsync(run, gift, free: _credits.LimitsWonderTrades);

            if (!result.Success || result.Offer is not { } offer)
            {
                Problem = result.Error ?? "El intercambio no se ha podido completar.";
                return;
            }

            // Se escribe en la partida ANTES de animar nada. Si la escritura falla, no hay
            // animación que enseñe un Pokémon que el jugador no tiene.
            var written = await _swap.SwapAsync(offer, given.Box, given.Slot);

            if (!written.Delivered)
            {
                Problem = written.Message;
                return;
            }

            _logger.LogInformation("Wonder trade hecho: {Given} por {Received}", given.DisplayName, offer.DisplayName);

            // El que llega solo existe en el juego a partir de aquí, así que su PID se guarda
            // ahora. Sin él la run tendría un Pokémon suyo al que no sabría reconocer.
            if (result.Entry is { } entry)
            {
                await _identity.RememberDeliveryAsync(run, entry, written.Pid,
                    written.Box, written.Slot);
            }

            // Y el que se va deja de estar vivo. Se hace aquí y no dentro del intercambio porque
            // hasta que la partida no está escrita no se ha ido nadie.
            await _trades.MarkGivenAsTradedAsync(run, given.Pid, offer.DisplayName);

            Offer = offer;
            ReceivedSprite = _sprites.Get(offer.Species, offer.Form, offer.IsShiny);
            Types = BuildTypes(offer);
            TypeColour = Types[0].Colour;
            OnPropertyChanged(nameof(Types));
            OnPropertyChanged(nameof(GenerationText));
            OnPropertyChanged(nameof(TotalText));
            OnPropertyChanged(nameof(DifferenceText));

            await PlayAsync(given, offer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el wonder trade");
            Problem = "Ha fallado el intercambio.";
        }
        finally
        {
            IsWorking = false;
            await RefreshCreditsAsync();
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// The show: the cabin sends one ball up and brings another down, and its screen tells the three things the player
    /// learns before the Pokémon itself — its generation, its types and what it is worth. The cabin draws all of that
    /// from <see cref="CurrentPlay"/>; this waits for the Pokémon to be out and then brings up its card.
    /// </summary>
    private async Task PlayAsync(BoxedPokemon given, WonderTradeOffer offer)
    {
        IsPlaying = true;
        ShowPokemon = false;

        // El icono del cartucho solo hace falta para las balls que la cabina no dibuja. Se extraen las dieciséis
        // primeras (§28); cualquier otra sale como Poké Ball.
        var icon = TradeMachineScene.BallFor(given.Ball) is null && given.Ball is >= 1 and <= 16 ? _sprites.GetBall(given.Ball) : null;

        var play = new TradePlay(
            GivenSprite,
            given.Ball,
            icon,
            ReceivedSprite,
            offer.Generation,
            [.. Types.Select(type => new TradeType(type.Name, ((SolidColorBrush)type.Colour).Color))],
            offer.GivenBaseStatTotal,
            offer.BaseStatTotal,
            offer.Difference,
            offer.IsShiny,
            offer.Legendary,
            unchecked((int)(offer.Seed ^ (ulong)offer.Number)),
            Stopwatch.GetTimestamp());

        CurrentPlay = play;

        var left = TradeTimeline.Revealed - play.Elapsed;
        if (left > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(left));
        }

        ShowPokemon = true;
    }

    /// <summary>The cabin could not draw; the trade is written and recorded all the same.</summary>
    public void AnimationFailed(Exception ex) =>
        _logger.LogError(ex, "Falló la animación del wonder trade; el intercambio sí es válido");

    /// <summary>Closes the result and tells the viewer its boxes are out of date.</summary>
    [RelayCommand]
    private void Dismiss()
    {
        IsPlaying = false;
        IsArmed = false;
        Clear();
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void Clear()
    {
        Given = null;
        GivenIsFallen = false;
        GivenSprite = null;
        ReceivedSprite = null;
        Offer = null;
        BandText = string.Empty;
        ShowPokemon = false;
        CurrentPlay = null;
    }

    private static List<TypeBadgeViewModel> BuildTypes(WonderTradeOffer offer)
    {
        var badges = new List<TypeBadgeViewModel> { Badge(offer.Types.First, offer.Types.FirstName) };

        if (offer.Types.IsDual)
        {
            badges.Add(Badge(offer.Types.Second, offer.Types.SecondName));
        }

        return badges;
    }

    private static TypeBadgeViewModel Badge(int type, string name)
    {
        // Un tipo que no se pudo leer sale con el color del Normal, como siempre ha salido aquí.
        var brush = TypePalette.BrushOf(type < 0 ? 0 : type);
        return new TypeBadgeViewModel(name.ToUpperInvariant(), brush);
    }

    partial void OnGivenChanged(BoxedPokemon? value) => ConfirmCommand.NotifyCanExecuteChanged();

    partial void OnIsPlayingChanged(bool value) => ConfirmCommand.NotifyCanExecuteChanged();
}
