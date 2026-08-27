using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>Asks the view to run the trade animation. The view owns the how; this owns the when.</summary>
/// <param name="BallArrived">Called when the incoming ball has stopped, which starts the reveals.</param>
/// <param name="Opened">Called to open the ball on the last reveal.</param>
public sealed record TradeAnimation(Action BallArrived);

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

    /// <summary>The colours the series has used for the types since forever.</summary>
    private static readonly string[] TypeColours =
    [
        "#9FA19F", "#FF8000", "#81B9EF", "#9141CB", "#915121", "#AFA981", "#91A119", "#704170",
        "#60A1B8", "#E62829", "#2980EF", "#3FA129", "#FAC000", "#EF4179", "#3DCEF3", "#5060E1",
        "#624D4E", "#EF70EF", "#2E9AA0"
    ];

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

    /// <summary>Raised when the reel of balls should run. The view animates; this waits.</summary>
    public event EventHandler<TradeAnimation>? AnimationRequested;

    /// <summary>Raised when the ball should burst open on the last reveal.</summary>
    public event EventHandler? OpenRequested;

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
    /// The Poké Ball, out of the player's own cartridge. Null when the sprites are not there,
    /// and then the animation runs without it rather than drawing a fake one.
    /// </summary>
    [ObservableProperty]
    private BitmapSource? _ballSprite;

    /// <summary>Colour of the received Pokémon's first type, which floods the reveal.</summary>
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
        : "Elige en la caja el Pokémon que quieres entregar.";

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

    // Los tres avisos previos, en orden: tipo, generación y total. Cada uno se enciende por
    // separado para que la vista los pueda animar uno a uno.
    [ObservableProperty]
    private bool _showTypes;

    [ObservableProperty]
    private bool _showGeneration;

    [ObservableProperty]
    private bool _showTotal;

    [ObservableProperty]
    private bool _showPokemon;

    public List<TypeBadgeViewModel> Types { get; private set; } = [];

    public string GenerationText => Offer is null ? string.Empty : $"GENERACIÓN {Offer.Generation}";

    public string TotalText => Offer is null ? string.Empty : Offer.BaseStatTotal.ToString();

    /// <summary>"+6 %" or "-3 %" against what was handed over, which is the whole point of the band.</summary>
    public string DifferenceText => Offer is null
        ? string.Empty
        : $"{(Offer.Difference >= 0 ? "+" : string.Empty)}{Offer.Difference} % sobre {Offer.GivenName}";

    public bool CanConfirm => Given is not null && !IsPlaying && !IsWorking;

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
        GivenSprite = pokemon is null ? null : _sprites.Get(pokemon.Species);

        if (pokemon is null)
        {
            BandText = string.Empty;
            return;
        }

        var total = _trades.BaseStatTotalOf(pokemon.Species);

        if (total == 0)
        {
            BandText = string.Empty;
            Problem = $"No sé cuánto vale {pokemon.SpeciesName} en estadísticas base. "
                      + "Genera Data/species.json con: RomTool species.";
            return;
        }

        var (min, max) = _trades.Window.Band(total);
        Problem = string.Empty;
        BandText = $"{pokemon.DisplayName} vale {total} de total base. "
                   + $"Lo que vuelva valdrá entre {min} y {max}, y llegará a nivel {pokemon.Level}.";
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
                Problem = "No te quedan wonder trades. Los dan las pruebas: uno por cada una, "
                          + "cuatro por la liga y cuatro por el rematch.";
                return;
            }

            var gift = new WonderTradeGift(given.Species, given.DisplayName, given.Level, given.Box, given.Slot);
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

            _logger.LogInformation("Wonder trade hecho: {Given} por {Received}", given.DisplayName, offer.Name);

            // El que llega solo existe en el juego a partir de aquí, así que su PID se guarda
            // ahora. Sin él la run tendría un Pokémon suyo al que no sabría reconocer.
            if (result.Entry is { } entry)
            {
                await _identity.RememberDeliveryAsync(run, entry, written.Pid,
                    written.Box, written.Slot);
            }

            // Y el que se va deja de estar vivo. Se hace aquí y no dentro del intercambio porque
            // hasta que la partida no está escrita no se ha ido nadie.
            await _trades.MarkGivenAsTradedAsync(run, given.Pid, offer.Name);

            Offer = offer;
            ReceivedSprite = _sprites.Get(offer.Species);
            BallSprite = _sprites.GetBall();
            Types = BuildTypes(offer);
            TypeColour = Types[0].Colour;
            OnPropertyChanged(nameof(Types));
            OnPropertyChanged(nameof(GenerationText));
            OnPropertyChanged(nameof(TotalText));
            OnPropertyChanged(nameof(DifferenceText));

            await PlayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el wonder trade");
            Problem = "Ha fallado el intercambio. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsWorking = false;
            await RefreshCreditsAsync();
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// The show: the balls cross, and then the three things the player is told before the
    /// Pokémon itself — its type, its generation and what it is worth.
    /// </summary>
    private async Task PlayAsync()
    {
        IsPlaying = true;
        ShowTypes = ShowGeneration = ShowTotal = ShowPokemon = false;

        var arrived = new TaskCompletionSource();
        AnimationRequested?.Invoke(this, new TradeAnimation(() => arrived.TrySetResult()));

        // Se espera a que la bola pare de verdad, no a que pase el tiempo. La red de seguridad
        // existe por si la vista nunca llegó a arrancar: una pantalla que no se cuelga.
        await Task.WhenAny(arrived.Task, Task.Delay(TimeSpan.FromSeconds(8)));

        // Generación primero y tipos después, a petición del jugador: la generación acota poco y
        // los tipos acotan mucho, así que revelarlos en ese orden va cerrando el cerco.
        ShowGeneration = true;
        await Task.Delay(TimeSpan.FromSeconds(1.3));

        ShowTypes = true;
        await Task.Delay(TimeSpan.FromSeconds(1.3));

        ShowTotal = true;
        await Task.Delay(TimeSpan.FromSeconds(1.6));

        // Los tres avisos se apagan ANTES de que salga el Pokémon. Si se quedan, la ficha final
        // cae encima de ellos y de la bola, y no hay quien lea nada.
        ShowTypes = ShowGeneration = ShowTotal = false;
        OpenRequested?.Invoke(this, EventArgs.Empty);
        await Task.Delay(TimeSpan.FromSeconds(0.35));

        ShowPokemon = true;
    }

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
        GivenSprite = null;
        ReceivedSprite = null;
        Offer = null;
        BandText = string.Empty;
        ShowTypes = ShowGeneration = ShowTotal = ShowPokemon = false;
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
        var hex = type >= 0 && type < TypeColours.Length ? TypeColours[type] : "#9FA19F";
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return new TypeBadgeViewModel(name.ToUpperInvariant(), brush);
    }

    partial void OnGivenChanged(BoxedPokemon? value) => ConfirmCommand.NotifyCanExecuteChanged();

    partial void OnIsPlayingChanged(bool value) => ConfirmCommand.NotifyCanExecuteChanged();
}
