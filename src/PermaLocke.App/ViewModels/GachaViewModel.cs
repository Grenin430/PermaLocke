using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// One portal of the ultra-space animation: a tier, its colour and whether it is the one that
/// just opened.
/// </summary>
/// <remarks>
/// There is one of these per tier in <c>Data/gacha.json</c>, so adding or removing a tier changes
/// the animation without touching XAML. Each portal animates itself: a Storyboard inside a Style
/// has no name scope and cannot reach another element (ARCHITECTURE.md §26).
/// </remarks>
public sealed partial class PortalViewModel(GachaTier tier, int position) : ObservableObject
{
    public string TierId { get; } = tier.Id;

    public string Name { get; } = tier.Name;

    /// <summary>Palette key, resolved by the view. Tiers are ordered, so position picks the colour.</summary>
    public string BrushKey { get; } = $"Tier{position}Brush";

    /// <summary>True while this portal is the one the roll landed on.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// The band of base stat totals this tier covers, as the screen writes it.
    /// </summary>
    /// <remarks>
    /// Computed from the tiers themselves — each one's floor is the previous one's ceiling — and
    /// not typed in. It used to be five chips at the bottom of the screen with «T1 · ≤400» and the
    /// rest written by hand in XAML, so editing <c>Data/gacha.json</c> left the screen stating
    /// bands the roll no longer used. A number nobody can contradict is worth more than a tidy
    /// literal.
    /// </remarks>
    public string Range { get; init; } = string.Empty;

    /// <summary>Chance of this tier on the banner currently chosen, or blank when it cannot come out.</summary>
    [ObservableProperty]
    private string _chance = string.Empty;

    /// <summary>Stays lit on the tier the last roll produced, after the flash is over.</summary>
    [ObservableProperty]
    private bool _isLanded;
}

/// <summary>One line of what has already come out.</summary>
/// <remarks>
/// The screen used to forget every roll the moment the next one started; what you had pulled lived
/// only in the STATISTICS screen. A gacha with no memory of its own is missing the half that makes
/// the odds mean anything.
/// </remarks>
public sealed class GachaHistoryViewModel(string speciesName, string brushKey,
    System.Windows.Media.Imaging.BitmapSource? sprite)
{
    public string SpeciesName { get; } = speciesName;

    public string BrushKey { get; } = brushKey;

    public System.Windows.Media.Imaging.BitmapSource? Sprite { get; } = sprite;
}

/// <summary>One Pokémon that a tier can produce, as the pool list shows it.</summary>
/// <remarks>
/// The screen used to say «60 % Tier 3» and stop there: you knew the odds of a band and not who
/// was in it. The list is not a second opinion about the tiers either — it comes from the same
/// <see cref="GachaService.PoolOf"/> the roll itself draws from, so what it shows is what can
/// actually come out, not a rule written twice.
/// </remarks>
/// <param name="formName">
/// A regional form of the species, drawn beside it and without an arrow: it is not what the species
/// evolves into but another way the same rung can come out (§140).
/// </param>
public sealed class PoolStageViewModel(SpeciesStats species,
    System.Windows.Media.Imaging.BitmapSource? sprite, bool arrow, string? formName = null)
{
    public string Name { get; } = formName is null ? species.Name : $"{species.Name} de {formName}";

    public int Total { get; } = species.BaseStatTotal;

    public System.Windows.Media.Imaging.BitmapSource? Sprite { get; } = sprite;

    /// <summary>True for every rung but the first, which is where the arrow goes.</summary>
    public bool Arrow { get; } = arrow;
}

/// <summary>
/// One evolution family a tier can produce, as the pool list shows it.
/// </summary>
/// <remarks>
/// A family and not a species, because that is what the tier is a band of: the rarity is decided
/// by where the line ENDS, and what a roll hands over is one of its rungs. Showing a flat list of
/// species would say «a Tier 5 can give you a Gible» and leave out the half that makes it a Tier 5.
/// </remarks>
public sealed class PoolEntryViewModel(IReadOnlyList<PoolStageViewModel> stages, bool legendary)
{
    public IReadOnlyList<PoolStageViewModel> Stages { get; } = stages;

    public bool Legendary { get; } = legendary;

    /// <summary>Where the family ends, which is the number the tier is graded on.</summary>
    public int Total { get; } = stages.Count > 0 ? stages[^1].Total : 0;

    /// <summary>Matched against the search box — any rung, because any rung can come out.</summary>
    public IEnumerable<string> Names => Stages.Select(stage => stage.Name);
}

/// <summary>One banner as the screen shows it, with its odds spelled out.</summary>
/// <param name="BrushKey">Palette key of the tier, the same one the portal uses.</param>
/// <param name="Width">In hundredths of the bar, so the view can stretch it without knowing its width.</param>
public sealed record OddsSlice(string BrushKey, double Width, string Label);

public sealed partial class BannerViewModel(GachaBanner banner, string odds,
    IReadOnlyList<OddsSlice> slices) : ObservableObject
{
    /// <summary>
    /// The same odds as a bar, in the colours of the tiers.
    /// </summary>
    /// <remarks>
    /// The portals up top are still where the odds are WRITTEN -that decision stands, the portal is
    /// the tier and lights up when a roll lands on it-. What the card had was the same three
    /// figures repeated in the same language, six feet below. A bar is not a repetition: says the
    /// SHAPE of the banner at a glance, which is what you are comparing when you pick one.
    /// </remarks>
    public IReadOnlyList<OddsSlice> Slices { get; } = slices;

    public GachaBanner Banner { get; } = banner;

    public string Name => Banner.Name;

    public string Description => Banner.Description;

    /// <summary>The odds, written out. Hiding them would be the one thing a gacha must not do.</summary>
    public string Odds { get; } = odds;

    /// <summary>Free rolls waiting on this banner, from the trials and from the wheel.</summary>
    [ObservableProperty]
    private int _free;

    /// <summary>What the card says it costs: the points, or that it is on the house.</summary>
    public string Cost => Free > 0
        ? $"{Free} tirada{(Free == 1 ? string.Empty : "s")} gratis"
        : $"{Banner.Cost} puntos";

    public bool HasFree => Free > 0;

    partial void OnFreeChanged(int value)
    {
        OnPropertyChanged(nameof(Cost));
        OnPropertyChanged(nameof(HasFree));
    }
}

/// <summary>One roll a milestone pays, on one banner.</summary>
/// <remarks>
/// A row of its own so the banner keeps its own colour on the table. The tier's brush and not an
/// invented one: the portals above already say POCHO in that colour, and giving the same banner
/// two colours on the same screen would be worse than giving it none.
/// </remarks>
public sealed record GrantRollViewModel(string Banner, int Count, string BrushKey)
{
    public string Text => $"{Banner} ×{Count}";
}

/// <summary>
/// What one milestone of the competition hands over: free rolls and wonder trades.
/// </summary>
/// <remarks>
/// Read from <c>Data/grants.json</c> and not written into XAML, so changing the competition's
/// table changes the screen. Whether it has been <b>reached</b> comes from the achievements, which
/// the cartridge proves on its own -- so this is a list of what is coming, with what has already
/// been paid marked, and not a checklist anybody ticks.
/// </remarks>
public sealed class GrantViewModel(string name, IReadOnlyList<GrantRollViewModel> rolls,
    int wonderTrades, bool reached)
{
    public string Name { get; } = name;

    public IReadOnlyList<GrantRollViewModel> Rolls { get; } = rolls;

    public int WonderTrades { get; } = wonderTrades;

    public string WonderTradeText => WonderTrades == 1 ? "1 INTERCAMBIO" : $"{WonderTrades} INTERCAMBIOS";

    public bool HasWonderTrades => WonderTrades > 0;

    /// <summary>True once the milestone has been reached, which is when it has already paid.</summary>
    public bool Reached { get; } = reached;
}

/// <summary>
/// The gacha screen.
/// </summary>
/// <remarks>
/// Strict MVVM: no logic here beyond asking <see cref="GachaService"/> and exposing what came
/// back. The odds are shown, never hidden, and every roll is reproducible from the run seed.
/// </remarks>
public sealed partial class GachaViewModel : SectionViewModel
{
    private readonly GachaService _gacha;
    private readonly IRunContext _runContext;
    private readonly IPointsService _points;
    private readonly IPokemonDelivery _delivery;
    private readonly PokemonIdentityService _identity;
    private readonly CreditService _credits;

    /// <summary>
    /// How far the run has got, which decides how far up an evolution family a roll reaches.
    /// </summary>
    /// <remarks>
    /// The same number the level cap is deduced from, on purpose. A second measure of progress
    /// living only in the gacha would be one more thing able to disagree with the rest of the run.
    /// </remarks>
    private readonly PermaLocke.Rules.Services.ProgressService _progress;
    private readonly PokemonSpriteService _sprites;
    private readonly IEventStore _events;
    private readonly ISpeciesLookup _species;
    private readonly ILogger<GachaViewModel> _logger;

    /// <summary>
    /// The view reporting that its animation broke.
    /// </summary>
    /// <remarks>
    /// The pull is already decided, written to the save and recorded by the time a single frame
    /// plays, so this is only ever a note in the log: the player still gets their Pokémon. The
    /// capsule machine draws on the rendering loop, outside every await here, so a failure there
    /// would otherwise reach the application's handler and tell the player something had gone
    /// wrong when nothing had (the lesson of §87).
    /// </remarks>
    public void AnimationFailed(Exception ex) =>
        _logger.LogError(ex, "Falló la animación del gacha; la tirada sí es válida");

    public GachaViewModel(GachaService gacha, IRunContext runContext, IPointsService points,
        IPokemonDelivery delivery, PokemonIdentityService identity, CreditService credits,
        PokemonSpriteService sprites, IEventStore events, ISpeciesLookup species,
        PermaLocke.Rules.Services.ProgressService progress, ILogger<GachaViewModel> logger)
        : base("GACHA", "Gasta puntos y llévate un Pokémon: a tu equipo si cabe, si no al PC")
    {
        Fleeting.Fade(this, nameof(Status));

        _gacha = gacha;
        _runContext = runContext;
        _points = points;
        _delivery = delivery;
        _identity = identity;
        _credits = credits;
        _events = events;
        _species = species;
        _sprites = sprites;
        _progress = progress;
        _logger = logger;
    }

    public ObservableCollection<BannerViewModel> Banners { get; } = [];

    /// <summary>
    /// The pull the capsule machine is playing, or null while it waits.
    /// </summary>
    /// <remarks>
    /// It replaces the reel (§171). The machine draws every frame from the moment in here, so the view keeps no clock
    /// of its own and this view model never waits on an animation: the timeline is the same for every pull, and the
    /// portals and the result card follow it from here.
    /// </remarks>
    [ObservableProperty]
    private CapsulePlay? _currentPlay;

    /// <summary>
    /// How long the result stays on the stage before the screen goes back to how it was.
    /// </summary>
    /// <remarks>
    /// Asked for by the player: the card and the reel parked on the winner used to stay until the
    /// application was closed, so the machine never looked ready for the next pull. What came out
    /// is not lost when they go: it is in the strip of past rolls, in the history and in the game.
    /// </remarks>
    private static readonly TimeSpan ResultShownFor = TimeSpan.FromSeconds(8);

    /// <summary>Counts down <see cref="ResultShownFor"/>; cancelled by a new roll or an earlier reset.</summary>
    private CancellationTokenSource? _resultTimer;

    /// <summary>The player left while the reel was turning, so the result goes as soon as it lands.</summary>
    private bool _idleWhenDone;

    /// <summary>
    /// True when <see cref="Status"/> says something went wrong, which is the one thing a reset
    /// must not wipe: «en la run sí está, en el juego todavía no» has to be read.
    /// </summary>
    private bool _statusIsWarning;

    /// <summary>The run the strip of past rolls belongs to.</summary>
    private Guid? _historyRun;

    /// <summary>
    /// Leaving the gacha folds the panels and puts the stage back to how it was before the pull.
    /// </summary>
    /// <remarks>
    /// The result card used to stay, on the grounds that it is what the last pull <em>was</em>.
    /// The player asked for the opposite, and they are right that it reads better: coming back to
    /// the gacha should find a machine waiting, not the last result frozen on it. Nothing is lost —
    /// the Pokémon is in the game and in the strip of past rolls. A warning in the status line is
    /// kept, because that one says something is still pending.
    /// <para>
    /// Leaving mid-spin does not cut the spin short: the pull is already decided and written, and
    /// the roll finishes on its own. It just goes straight back to idle when it lands.
    /// </para>
    /// </remarks>
    public override void ResetState()
    {
        ShowingPool = false;
        ShowingGrants = false;

        if (IsRolling)
        {
            _idleWhenDone = true;
            return;
        }

        ReturnToIdle(clearStatus: !_statusIsWarning);
    }

    /// <summary>
    /// Takes the result off the stage: no card, no lit portal, and the reel drifting again.
    /// </summary>
    private void ReturnToIdle(bool clearStatus)
    {
        _resultTimer?.Cancel();
        _resultTimer = null;
        _idleWhenDone = false;

        HasResult = false;
        LastPull = null;
        LastBadge = string.Empty;
        LastSprite = null;
        LastTier = string.Empty;
        // Apaga el portal encendido y devuelve el marcador al color de reposo.
        DisplayedTier = string.Empty;

        foreach (var portal in Portals)
        {
            portal.IsLanded = false;
        }

        if (clearStatus)
        {
            Status = string.Empty;
            _statusIsWarning = false;
        }

        // La máquina manda la ball a la estantería y se queda esperando la siguiente.
        CurrentPlay = null;
    }

    /// <summary>Waits <see cref="ResultShownFor"/> and then clears the stage, unless something else did first.</summary>
    private async Task ReturnToIdleLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ResultShownFor, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // La línea de estado se queda: dice adónde ha ido el Pokémon, y vive en la barra de abajo,
        // no en el escenario.
        if (!IsRolling && HasResult)
        {
            ReturnToIdle(clearStatus: false);
        }
    }

    /// <summary>One portal per tier, in order of price. Built from the catalogue, not from XAML.</summary>
    public ObservableCollection<PortalViewModel> Portals { get; } = [];

    /// <summary>What each milestone of the competition pays, in the order the competition lists it.</summary>
    public ObservableCollection<GrantViewModel> Grants { get; } = [];

    [ObservableProperty]
    private bool _hasGrants;

    [ObservableProperty]
    private string _grantsSummary = string.Empty;

    /// <summary>Who can come out of the tier being inspected. The whole band, unfiltered.</summary>
    public ObservableCollection<PoolEntryViewModel> Pool { get; } = [];

    /// <summary>
    /// The part of it the search box leaves standing, which is what the screen is bound to.
    /// </summary>
    /// <remarks>
    /// Refilled rather than filtered through an <c>ICollectionView</c>, same as the shop: the list
    /// is bound straight to this, and a CollectionView touched from anywhere but the UI thread
    /// takes the window down (§62).
    /// </remarks>
    public ObservableCollection<PoolEntryViewModel> VisiblePool { get; } = [];

    /// <summary>
    /// What has been typed into the pool's search box.
    /// </summary>
    /// <remarks>
    /// The top tier lists a couple of hundred species, so «is Tyranitar in here» was a question
    /// answered by scrolling. Accents are ignored for the same reason as the shop's: with the
    /// expansion mod the list mixes the cartridge's Spanish with the mod's English, and somebody
    /// typing "farfetchd" is not making a mistake.
    /// </remarks>
    [ObservableProperty]
    private string _poolSearch = string.Empty;

    partial void OnPoolSearchChanged(string value) => FilterPool();

    [RelayCommand]
    private void ClearPoolSearch() => PoolSearch = string.Empty;

    /// <summary>True when the box has something in it and nothing matches.</summary>
    public bool PoolNothingFound => PoolSearch.Trim().Length > 0 && VisiblePool.Count == 0;

    /// <summary>
    /// Which rung of a family a roll lands on right now, written out.
    /// </summary>
    /// <remarks>
    /// Without this the list would show three-stage families and say nothing about which one you
    /// actually get, which is the half of the mechanic a player cannot see. It is read from the
    /// same table the roll uses, at the run's real progress.
    /// </remarks>
    [ObservableProperty]
    private string _stageOdds = string.Empty;

    /// <summary>How many of the band are on screen, said only while the search is narrowing it.</summary>
    public string PoolCount => PoolSearch.Trim().Length > 0
        ? $"{VisiblePool.Count} de {Pool.Count}"
        : $"{Pool.Count} familias";

    private void FilterPool()
    {
        var needle = PoolSearch.Trim();

        VisiblePool.Clear();

        // Cualquier etapa vale: buscar «Garchomp» tiene que encontrar la familia aunque lo que te
        // vayan a dar sea el Gible.
        foreach (var entry in Pool.Where(e => needle.Length == 0
            || e.Names.Any(name => CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                name, needle, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)))
        {
            VisiblePool.Add(entry);
        }

        OnPropertyChanged(nameof(PoolNothingFound));
        OnPropertyChanged(nameof(PoolCount));
    }

    [ObservableProperty]
    private bool _showingPool;

    /// <summary>
    /// True while the table of what each milestone pays is open over the stage.
    /// </summary>
    /// <remarks>
    /// It used to sit under the banners taking a fifth of the screen for a table nobody reads
    /// twice. Behind a button it can be as big as it deserves when it is open and cost nothing
    /// when it is not, which is the same deal the pool list gets.
    /// </remarks>
    [ObservableProperty]
    private bool _showingGrants;

    /// <summary>The strip of past rolls, which shares its room with the two panels.</summary>
    /// <remarks>
    /// One has to give way: they all live over the stage, and with a panel open the strip was
    /// being drawn straight over it. What the player just asked for wins, so the strip waits.
    /// </remarks>
    public bool ShowHistory => HasHistory && !ShowingPool && !ShowingGrants;

    partial void OnShowingPoolChanged(bool value)
    {
        // Los dos ocupan el mismo sitio, asi que abrir uno cierra el otro. Sin esto se dibujan
        // encima y lo que se lee es la mezcla de los dos.
        if (value)
        {
            ShowingGrants = false;
        }

        OnPropertyChanged(nameof(ShowHistory));
    }

    partial void OnShowingGrantsChanged(bool value)
    {
        if (value)
        {
            ShowingPool = false;
        }

        OnPropertyChanged(nameof(ShowHistory));
    }

    /// <summary>Opens the milestone table, or closes it if it is the one already open.</summary>
    [RelayCommand]
    private void ToggleGrants()
    {
        if (IsRolling)
        {
            return;
        }

        ShowingGrants = !ShowingGrants;
    }

    partial void OnHasHistoryChanged(bool value) => OnPropertyChanged(nameof(ShowHistory));

    [ObservableProperty]
    private string _poolTitle = string.Empty;

    [ObservableProperty]
    private string _poolBrushKey = "AccentBrush";

    /// <summary>The last few rolls of the loaded run, newest first, read from its history.</summary>
    public ObservableCollection<GachaHistoryViewModel> History { get; } = [];

    /// <summary>How many fit under the reel without the strip wrapping or shrinking.</summary>
    private const int HistoryKept = 10;

    /// <summary>So the strip and its heading stay out of the way until there is something to show.</summary>
    [ObservableProperty]
    private bool _hasHistory;

    [ObservableProperty]
    private BannerViewModel? _selectedBanner;

    [ObservableProperty]
    private int _balance;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isRolling;

    /// <summary>Set while the reveal animation runs, so the view can play it.</summary>
    [ObservableProperty]
    private GachaPull? _lastPull;

    /// <summary>Tier of the last roll, which is what picks the portal colour.</summary>
    [ObservableProperty]
    private string _lastTier = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    /// <summary>Name of the tier that came out, for the result card.</summary>
    [ObservableProperty]
    private string _lastTierName = string.Empty;

    /// <summary>Palette key of the tier that came out, so the flash and the card match its colour.</summary>
    [ObservableProperty]
    private string _lastTierBrushKey = "AccentBrush";

    /// <summary>"Legendario" / "Shiny", or empty. Only set when the roll really was one.</summary>
    [ObservableProperty]
    private string _lastBadge = string.Empty;

    /// <summary>
    /// True when the roll earned a badge, which is the view's cue to celebrate louder.
    /// </summary>
    /// <remarks>
    /// The extra noise is not decoration handed out at random: it maps to something that really
    /// happened and is rare, so it says as much as the word does.
    /// </remarks>
    [ObservableProperty]
    private bool _hasBadge;

    partial void OnLastBadgeChanged(string value) => HasBadge = value.Length > 0;

    /// <summary>
    /// Icon of the Pokémon that came out, taken from the player's own ROM. Null when the species
    /// is not one of the 649 whose icon is known, and then the card simply shows no picture.
    /// </summary>
    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapSource? _lastSprite;

    /// <summary>
    /// Tier the screen is <em>showing</em> while the reel turns, which is not always the one that
    /// came out.
    /// </summary>
    /// <remarks>
    /// This is the fake-out the player asked for: the reel starts lit at the lowest tier and
    /// climbs — once, or twice for the top tiers — before it stops. It is presentation and
    /// nothing else: the Pokémon, the points and the event were decided and written before the
    /// first frame, and <see cref="LastTier"/> always holds the real one.
    /// </remarks>
    [ObservableProperty]
    private string _displayedTier = string.Empty;

    /// <summary>Lights the portal that matches what is being shown, and puts out the rest.</summary>
    partial void OnDisplayedTierChanged(string value)
    {
        foreach (var portal in Portals)
        {
            portal.IsActive = portal.TierId == value;
        }

        var match = Portals.FirstOrDefault(p => p.TierId == value);
        LastTierName = match?.Name ?? value;
        LastTierBrushKey = match?.BrushKey ?? "AccentBrush";
    }

    public override string IconKey => "IconGacha";

    public override GameNeed Needs => GameNeed.Closed;

    public override async Task ActivateAsync()
    {
        // Ha vuelto antes de que parase la rueda: el resultado sí tiene quien lo mire.
        _idleWhenDone = false;

        Banners.Clear();

        foreach (var banner in _gacha.Banners)
        {
            Banners.Add(new BannerViewModel(banner, DescribeOdds(banner), SlicesOf(banner)));
        }

        if (Portals.Count == 0)
        {
            var position = 1;
            var floor = 0;

            foreach (var tier in _gacha.Tiers)
            {
                // El ultimo no tiene techo: es «de aqui para arriba», legendarios incluidos.
                var last = position == _gacha.Tiers.Count;

                Portals.Add(new PortalViewModel(tier, position++)
                {
                    // La banda es del TOTAL DE LA FORMA FINAL de la linea, no del Pokemon que
                    // te dan: por eso lleva la flecha delante. Sin ella el tier 5 diria «600+»
                    // mientras entrega un Gible de 300 y pareceria un fallo.
                    // Sin «≤» ni «→»: la letra en píxeles no los tiene y salían como «0400» (2026-09-24).
                    Range = last
                        ? $"{floor + 1}+"
                        : floor == 0 ? $"0-{tier.MaxBaseStatTotal}" : $"{floor + 1}-{tier.MaxBaseStatTotal}"
                });

                floor = tier.MaxBaseStatTotal;
            }
        }

        SelectedBanner ??= Banners.FirstOrDefault();
        ShowOddsOf(SelectedBanner);

        if (_runContext.Current is { } run)
        {
            Balance = await _points.GetBalanceAsync(run.Id);
            await RefreshCreditsAsync(run);
            await RefreshGrantsAsync(run);

            var cleared = await _progress.ClearedAsync(run);
            var odds = _gacha.OddsAt(cleared);

            StageOdds = $"Con {cleared} etapa{(cleared == 1 ? string.Empty : "s")} superada"
                        + $"{(cleared == 1 ? string.Empty : "s")}: {odds.First}% primera forma · "
                        + $"{odds.Second}% segunda · {odds.Final}% forma final";
        }

        // Los iconos salen de la ROM del propio jugador la primera vez. Si no se puede, la
        // pantalla funciona igual: enseña la ficha sin dibujo.
        await _sprites.PrepareAsync();

        // El view model vive lo que la aplicación, así que la tira de tiradas sobrevivía a
        // «empezar de cero» y la run nueva heredaba lo que le salió a la borrada. Es de una run:
        // si la cargada es otra, se vacía y se vuelve a leer de su propio historial.
        var runId = _runContext.Current?.Id;

        if (runId != _historyRun && !IsRolling)
        {
            History.Clear();
            HasHistory = false;
            ReturnToIdle(clearStatus: true);
            _historyRun = runId;
        }

        await SeedHistoryAsync();

        _logger.LogInformation("Gacha: {Count} banners cargados, saldo {Balance}", Banners.Count, Balance);

        Status = Banners.Count == 0
            ? "El gacha no está disponible."
            : string.Empty;

        RollCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Puts the free rolls each banner has onto its card.
    /// </summary>
    /// <remarks>
    /// Asked for again after every roll rather than decremented here. The credit is earned minus
    /// spent, and both come from things this screen does not own; a copy kept locally would be one
    /// more number able to disagree with the history.
    /// </remarks>
    private async Task RefreshCreditsAsync(Run run)
    {
        try
        {
            var available = await _credits.AvailableAsync(run);

            foreach (var banner in Banners)
            {
                banner.Free = available.RollsOn(banner.Banner.Id);
            }

            // El boton lleva el precio, asi que tiene que enterarse cuando el credito cambia:
            // gastar la ultima tirada gratis lo devuelve a cobrar puntos.
            OnPropertyChanged(nameof(RollLabel));
            OnPropertyChanged(nameof(RollCost));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al contar las tiradas gratis");
        }
    }

    // No se exige SelectedBanner: el ListBox escribe null en el view model mientras se
    // inicializa, y eso dejaba el botón muerto aunque hubiera banners en pantalla.
    private bool CanRoll => !IsRolling && Banners.Count > 0 && _runContext.Current is not null;

    [RelayCommand(CanExecute = nameof(CanRoll))]
    private async Task RollAsync()
    {
        // Si el ListBox aún no ha elegido, se tira del primero: el botón ya no se apoya en él.
        var selected = SelectedBanner ?? Banners.FirstOrDefault();

        if (_runContext.Current is not { } run || selected is null)
        {
            return;
        }

        _resultTimer?.Cancel();
        _resultTimer = null;
        _idleWhenDone = false;
        _statusIsWarning = false;

        IsRolling = true;
        ShowingPool = false;
        ShowingGrants = false;
        HasResult = false;
        // Se apaga todo antes de tirar. Si no, dos tiradas seguidas del mismo tier no cambiarían
        // la propiedad y el portal no volvería a abrirse.
        LastTier = string.Empty;
        DisplayedTier = string.Empty;
        LastBadge = string.Empty;
        LastSprite = null;
        RollCommand.NotifyCanExecuteChanged();
        Status = string.Empty;

        try
        {
            // Si hay credito, la tirada es gratis y el evento queda marcado como tal: el credito
            // disponible es lo ganado menos lo marcado, asi que no hay contador que llevar.
            var free = selected.Free > 0;

            // Las etapas superadas deciden hasta donde de la linea evolutiva llega la tirada, y
            // viajan en el evento: sin ellas la tirada dejaria de poder recomputarse en cuanto la
            // run superara otra prueba.
            var cleared = await _progress.ClearedAsync(run);
            var result = await _gacha.RollAsync(run, selected.Banner.Id, cleared, free);

            Balance = result.Balance;

            if (!result.Success || result.Pull is not { } pull)
            {
                Status = result.Error ?? "La tirada no se ha podido completar.";
                _statusIsWarning = true;
                return;
            }

            await SpinAsync(pull, selected);

            LastPull = pull;
            // La marca solo se pone cuando de verdad lo es: un shiny se enseña, uno que no lo es
            // no se disfraza de nada.
            LastBadge = (pull.IsShiny, pull.Legendary) switch
            {
                (true, true) => "SHINY · LEGENDARIO",
                (true, false) => "SHINY",
                (false, true) => "LEGENDARIO",
                _ => string.Empty,
            };
            LastSprite = _sprites.Get(pull.Species, pull.Form, pull.IsShiny);
            HasResult = true;

            Remember(pull);

            // El Pokémon va al equipo si tiene hueco, y si no al PC. Si no se puede ahora, se dice por qué en vez de
            // dejar creer que está en la partida: en la run sí está, en el juego todavía no.
            var delivered = await _delivery.DeliverAsync(pull, run);
            Status = delivered.Message;
            _statusIsWarning = !delivered.Delivered;

            // El PID solo existe cuando el Pokémon se ha construido de verdad, así que se guarda
            // ahora: es lo único que permite reconocerlo luego en la memoria del juego y darlo por
            // muerto cuando caiga.
            if (delivered.Delivered && result.Pokemon is { } entry)
            {
                await _identity.RememberDeliveryAsync(run, entry, delivered.Pid,
                    delivered.Box, delivered.Slot);
            }

            if (free)
            {
                Status = $"Tirada gratis. {Status}";
            }

            _logger.LogInformation("Gacha {Banner}{Free}: {Species} Nv.{Level} ({Tier})",
                selected.Banner.Id, free ? " (gratis)" : string.Empty,
                pull.DisplayName, pull.Level, pull.TierId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tirada de gacha");
            Status = "Ha fallado.";
            _statusIsWarning = true;
        }
        finally
        {
            IsRolling = false;

            if (_runContext.Current is { } current)
            {
                await RefreshCreditsAsync(current);
            }

            RollCommand.NotifyCanExecuteChanged();

            // Se ha ido a otra pestaña con la rueda girando: el resultado ya no tiene quién lo mire.
            if (_idleWhenDone)
            {
                ReturnToIdle(clearStatus: !_statusIsWarning);
            }
            else if (HasResult)
            {
                _resultTimer = new CancellationTokenSource();
                _ = ReturnToIdleLaterAsync(_resultTimer.Token);
            }
        }
    }

    /// <summary>
    /// Plays the pull on the capsule machine: the crank turns, a ball drops, climbs and opens on the Pokémon that had
    /// already come out.
    /// </summary>
    /// <remarks>
    /// The machine draws itself from <see cref="CurrentPlay"/>; this only keeps the portals in step with the ball and
    /// waits for the moment the Pokémon is out. The timeline is the same for every tier (see
    /// <see cref="Views.CapsuleTimeline"/>), so there is no animation to wait on and no safety net to fall back to.
    /// </remarks>
    private async Task SpinAsync(GachaPull pull, BannerViewModel banner)
    {
        var tierIndex = Math.Max(0, Portals.ToList().FindIndex(p => p.TierId == pull.TierId));

        // Cae como la ball más barata que tenga el banner, no la Poké Ball: en la cúpula de BUENO no hay ninguna.
        var lowest = Portals
            .Select((portal, index) => (portal, index))
            .Where(p => banner.Banner.TierChances.TryGetValue(p.portal.TierId, out var chance) && chance > 0)
            .Select(p => p.index)
            .DefaultIfEmpty(tierIndex)
            .Min();

        var steps = CapsulePlay.StepsFor(lowest, tierIndex);

        // La semilla de la tirada decide también en qué meneos sube la ball, así que la misma tirada se ve igual al
        // recomputarla; y no depende del tier, que es lo que haría que se adivinara antes de tiempo.
        var seed = unchecked((int)(pull.Seed ^ (ulong)pull.Number));
        var play = new CapsulePlay(steps, _sprites.Get(pull.Species, pull.Form, pull.IsShiny), pull.IsShiny, pull.Legendary, seed,
            System.Diagnostics.Stopwatch.GetTimestamp());

        CurrentPlay = play;
        DisplayedTier = Portals.Count > steps[0] ? Portals[steps[0]].TierId : pull.TierId;

        var climbs = Views.CapsuleTimeline.UpgradeWobbles(steps.Count - 1, seed);

        for (var i = 0; i < climbs.Length; i++)
        {
            await WaitUntil(play, Views.CapsuleTimeline.UpgradeAt(climbs[i]));
            DisplayedTier = Portals[steps[i + 1]].TierId;
        }

        await WaitUntil(play, Views.CapsuleTimeline.Revealed);

        DisplayedTier = pull.TierId;
        LastTier = pull.TierId;
    }

    private static Task WaitUntil(CapsulePlay play, double seconds)
    {
        var left = seconds - play.Elapsed;
        return left > 0 ? Task.Delay(TimeSpan.FromSeconds(left)) : Task.CompletedTask;
    }

    partial void OnSelectedBannerChanged(BannerViewModel? value)
    {
        RollCommand.NotifyCanExecuteChanged();
        ShowOddsOf(value);
        OnPropertyChanged(nameof(RollLabel));
        OnPropertyChanged(nameof(RollCost));
    }

    /// <summary>
    /// Writes each tier's chance onto its own portal, for the banner currently chosen.
    /// </summary>
    /// <remarks>
    /// The odds used to be told three times over — the portals up top, the percentages on the
    /// banner card, and five hand-written chips at the bottom with the stat bands — in three
    /// different visual languages, none of them joined up. This is the one place they belong: the
    /// portal already IS the tier, it already carries its colour, and it is what lights up when a
    /// roll lands there. A tier a banner cannot produce says so by going blank rather than by
    /// showing a zero, which would read as a number rather than as an absence.
    /// </remarks>
    private void ShowOddsOf(BannerViewModel? banner)
    {
        foreach (var portal in Portals)
        {
            portal.Chance = banner is not null
                            && banner.Banner.TierChances.TryGetValue(portal.TierId, out var chance)
                            && chance > 0
                ? chance.ToString("P0")
                : string.Empty;
        }
    }

    /// <summary>What the roll button says, so the price is on the thing you press.</summary>
    /// <remarks>
    /// It used to say «TIRAR» with the <b>balance</b> on a chip beside it, which is not the price:
    /// to find out what a roll cost you had to look away, at the badge on whichever banner card was
    /// selected — and which one that was could only be told apart by a background one shade lighter.
    /// </remarks>
    public string RollLabel => (SelectedBanner ?? Banners.FirstOrDefault())?.Free > 0
        ? "TIRAR GRATIS"
        : "TIRAR";

    public string RollCost
    {
        get
        {
            var banner = SelectedBanner ?? Banners.FirstOrDefault();

            if (banner is null)
            {
                return string.Empty;
            }

            return banner.Free > 0
                ? $"te quedan {banner.Free}"
                : $"{banner.Banner.Cost} puntos";
        }
    }

    /// <summary>
    /// Fills the strip from the run's own history when the screen opens.
    /// </summary>
    /// <remarks>
    /// Without this the strip only remembers the rolls made since the window was opened, which is
    /// not memory, it is amnesia with extra steps: open the screen and the four hundred pixels are
    /// blank again. The rolls are already in the chained history — every one carries its species
    /// and its tier — so the screen is reading what happened rather than keeping a second copy of
    /// it, which is the same rule the credits follow.
    /// <para>
    /// Failing to read them costs the strip and nothing else. A gacha screen that will not open
    /// because it could not draw its own scrollback would be a worse trade than an empty strip.
    /// </para>
    /// </remarks>
    private async Task SeedHistoryAsync()
    {
        if (History.Count > 0 || _runContext.Current is not { } run)
        {
            return;
        }

        try
        {
            var rolls = (await _events.GetAllAsync(run.Id))
                .Where(e => e.Type == GameEventType.GachaRoll)
                .Reverse()
                .Take(HistoryKept);

            foreach (var roll in rolls)
            {
                if (!roll.Data.TryGetValue("especie", out var text)
                    || !int.TryParse(text, out var species))
                {
                    continue;
                }

                var tier = roll.Data.GetValueOrDefault("rareza", string.Empty);
                var portal = Portals.FirstOrDefault(p =>
                    string.Equals(p.TierId, tier, StringComparison.OrdinalIgnoreCase));

                History.Add(new GachaHistoryViewModel(_species.GetName(species),
                    portal?.BrushKey ?? "AccentBrush", _sprites.Get(species)));
            }

            HasHistory = History.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el historial de tiradas del gacha");
        }
    }

    /// <summary>
    /// Shows who a tier can produce, or closes the list if it was already that tier's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pool is asked of <see cref="GachaService.PoolOf"/>, the same call the roll uses, rather
    /// than re-deriving the bands here from the tier ceilings. Two places computing who belongs to
    /// a tier is two places that can disagree, and the one that would be wrong is the one the
    /// player reads before deciding to spend.
    /// </para>
    /// <para>
    /// Both halves are asked for: a tier holds ordinary species and legendaries, and only the top
    /// one has any of the latter. Legendaries come first and are marked, because «who is in Tier
    /// 5» is really a question about them.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private void ShowPool(PortalViewModel? portal)
    {
        if (IsRolling)
        {
            return;
        }

        // Sin portal significa CERRAR, y es lo que manda el botón de la propia lista. Antes esto
        // caía en la misma guarda que «no hay portal» y salía sin hacer nada: el botón existía,
        // se pulsaba, se veía pulsarse, y no cerraba.
        if (portal is null)
        {
            ShowingPool = false;
            return;
        }

        // Pulsar el mismo portal otra vez lo cierra: es un desplegable, no una pantalla nueva.
        if (ShowingPool && PoolTitle.StartsWith(portal.Name, StringComparison.Ordinal))
        {
            ShowingPool = false;
            return;
        }

        var tier = _gacha.Tiers.FirstOrDefault(t =>
            string.Equals(t.Id, portal.TierId, StringComparison.OrdinalIgnoreCase));

        if (tier is null)
        {
            return;
        }

        Pool.Clear();

        foreach (var line in _gacha.LinesOf(tier, legendary: true)
            .Select(l => (Line: l, Legendary: true))
            .Concat(_gacha.LinesOf(tier, legendary: false).Select(l => (Line: l, Legendary: false))))
        {
            var stages = new List<PoolStageViewModel>();

            foreach (var stage in line.Line.Stages)
            {
                foreach (var id in stage)
                {
                    if (_gacha.StatsOf(id) is { } stats)
                    {
                        stages.Add(new PoolStageViewModel(stats, _sprites.Get(id),
                            arrow: stages.Count > 0));

                        // Sus formas regionales, al lado y sin flecha: cada tirada sortea la forma de
                        // la especie que sale, así que son otra manera de salir esa misma etapa.
                        foreach (var form in stats.RegionalForms)
                        {
                            stages.Add(new PoolStageViewModel(stats, _sprites.Get(id, form.Form),
                                arrow: false, formName: form.Name));
                        }
                    }
                }
            }

            if (stages.Count > 0)
            {
                Pool.Add(new PoolEntryViewModel(stages, line.Legendary));
            }
        }

        // Las que más lejos llegan arriba: la pregunta que se hace mirando esta lista es «qué es lo
        // mejor que puede tocarme aquí».
        var ordered = Pool
            .OrderByDescending(e => e.Legendary)
            .ThenByDescending(e => e.Total)
            .ToList();

        Pool.Clear();

        foreach (var entry in ordered)
        {
            Pool.Add(entry);
        }

        // Se abre sin filtro. Abrir un tier y encontrarlo ya recortado por lo que se buscó en otro
        // se leería como que ese tier tiene cuatro Pokémon.
        PoolSearch = string.Empty;
        FilterPool();

        PoolBrushKey = portal.BrushKey;
        PoolTitle = $"{portal.Name} · {portal.Range}";
        ShowingPool = true;
    }

    /// <summary>Adds a roll to the strip under the reel, and lights the tier it landed on.</summary>

    /// <remarks>
    /// Newest first and capped, because the point is the last handful and an unbounded list would
    /// quietly become a memory leak on a screen somebody leaves open all evening.
    /// </remarks>
    private void Remember(GachaPull pull)
    {
        var portal = Portals.FirstOrDefault(p =>
            string.Equals(p.TierId, pull.TierId, StringComparison.OrdinalIgnoreCase));

        foreach (var other in Portals)
        {
            other.IsLanded = ReferenceEquals(other, portal);
        }

        History.Insert(0, new GachaHistoryViewModel(pull.DisplayName,
            portal?.BrushKey ?? "AccentBrush", _sprites.Get(pull.Species, pull.Form, pull.IsShiny)));

        while (History.Count > HistoryKept)
        {
            History.RemoveAt(History.Count - 1);
        }

        HasHistory = History.Count > 0;
    }

    /// <summary>"60% Tier 2 · 25% Tier 3 · 15% Tier 1", ordered by how likely each one is.</summary>
    private string DescribeOdds(GachaBanner banner)
    {
        return string.Join("   ·   ", banner.TierChances
            .OrderByDescending(chance => chance.Value)
            .Select(chance => $"{chance.Value:P0} {Pretty(chance.Key)}"));
    }

    /// <summary>The odds as coloured segments adding up to a hundred, in tier order.</summary>
    /// <remarks>
    /// In tier order and not by size, because the bar is read against the OTHER banners: with the
    /// segments always in the same order, «este tiene mas de lo bueno» se ve sin leer nada.
    /// </remarks>
    private IReadOnlyList<OddsSlice> SlicesOf(GachaBanner banner)
    {
        var total = banner.TotalWeight;

        if (total <= 0)
        {
            return [];
        }

        var slices = new List<OddsSlice>();
        var position = 1;

        foreach (var tier in _gacha.Tiers)
        {
            if (banner.TierChances.TryGetValue(tier.Id, out var chance) && chance > 0)
            {
                slices.Add(new OddsSlice($"Tier{position}Brush", chance / total * 100,
                    $"{chance / total:P0} {Pretty(tier.Id)}"));
            }

            position++;
        }

        return slices;
    }

    private static string Pretty(string tierId) =>
        tierId.StartsWith("tier", StringComparison.OrdinalIgnoreCase)
            ? "Tier " + tierId[4..]
            : tierId;

    /// <summary>
    /// The colour a banner is drawn in: the one of the tier it mostly hands out.
    /// </summary>
    /// <remarks>
    /// The same rule the bar under each card already uses, so POCHO is the same colour in both
    /// places. A banner with no odds at all gets the accent rather than nothing: an invisible chip
    /// would look like a missing row.
    /// </remarks>
    private string BrushOf(string bannerId)
    {
        var banner = _gacha.Banners.FirstOrDefault(b =>
            string.Equals(b.Id, bannerId, StringComparison.OrdinalIgnoreCase));

        if (banner is null)
        {
            return "AccentBrush";
        }

        var position = 1;
        var best = 0.0;
        var key = "AccentBrush";

        foreach (var tier in _gacha.Tiers)
        {
            if (banner.TierChances.TryGetValue(tier.Id, out var chance) && chance > best)
            {
                best = chance;
                key = $"Tier{position}Brush";
            }

            position++;
        }

        return key;
    }

    /// <summary>
    /// Fills the table of what each milestone of the competition pays.
    /// </summary>
    /// <remarks>
    /// Every milestone is listed, reached or not, because the point of the table is deciding
    /// whether to spend points now or wait for the trial that is about to pay. The ones already
    /// reached are marked and not removed, for the same reason.
    /// </remarks>
    private async Task RefreshGrantsAsync(Run run)
    {
        try
        {
            var reached = await _credits.ReachedAsync(run.Id);

            Grants.Clear();

            foreach (var milestone in _credits.Milestones)
            {
                var rolls = milestone.Rolls
                    .Where(entry => entry.Value > 0)
                    .Select(entry => new GrantRollViewModel(
                        NameOfBanner(entry.Key), entry.Value, BrushOf(entry.Key)))
                    .ToList();

                Grants.Add(new GrantViewModel(milestone.Name, rolls, milestone.WonderTrades,
                    reached.Contains(milestone.Achievement)));
            }

            HasGrants = Grants.Count > 0;
            GrantsSummary = $"{Grants.Count(g => g.Reached)} de {Grants.Count} conseguidos";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer lo que dan las pruebas");
            HasGrants = false;
        }
    }

    /// <summary>The banner's name as the catalogue writes it, falling back to its id.</summary>
    private string NameOfBanner(string bannerId) =>
        _gacha.Banners.FirstOrDefault(b =>
            string.Equals(b.Id, bannerId, StringComparison.OrdinalIgnoreCase))?.Name
        ?? bannerId.ToUpperInvariant();
}
