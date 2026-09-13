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
public sealed class PoolStageViewModel(SpeciesStats species,
    System.Windows.Media.Imaging.BitmapSource? sprite, bool arrow)
{
    public string Name { get; } = species.Name;

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
    /// plays, so this is only ever a note in the log: the player still gets their Pokémon. It
    /// exists because the view starts the spin from a dispatcher callback, outside the await, so
    /// nothing the view model wraps in a try can see it — it went all the way up to the
    /// application's handler and told the player something had gone wrong when nothing had.
    /// </remarks>
    public void AnimationFailed(Exception ex) =>
        _logger.LogError(ex, "Falló la animación del gacha; la tirada sí es válida");

    public GachaViewModel(GachaService gacha, IRunContext runContext, IPointsService points,
        IPokemonDelivery delivery, PokemonIdentityService identity, CreditService credits,
        PokemonSpriteService sprites, IEventStore events, ISpeciesLookup species,
        PermaLocke.Rules.Services.ProgressService progress, ILogger<GachaViewModel> logger)
        : base("GACHA", "Gasta puntos y llévate un Pokémon al PC de la partida")
    {
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

    /// <summary>
    /// Cells the reel carries. Long enough to keep a fast cruise going for seconds before the
    /// brakes come on — a short strip would have to crawl to fill the same time.
    /// </summary>
    private const int ReelLength = 118;

    /// <summary>Where the winner sits: near the end, so the reel travels a long way first.</summary>
    private const int ReelWinnerIndex = 108;

    /// <summary>
    /// Cells repeated after the idle strip so it can loop leftwards without a visible jump.
    /// </summary>
    /// <remarks>
    /// Enough of them to cover the viewport at 72 px a cell — about 3.400 px, which is wider than
    /// the panel is ever going to be. They are the same first cells again, so when the drift has
    /// travelled one whole strip and snaps back, what is on screen is identical either side of the
    /// snap. Duplicating the WHOLE strip would work too and costs twice the cells to build.
    /// </remarks>
    private const int IdleTailLength = 48;

    /// <summary>
    /// How long a spin lasts, by tier. The cheapest resolves quickly; the rarest takes its time.
    /// </summary>
    /// <remarks>
    /// The roll itself is instantaneous, so without this the animation would be over before it
    /// started. The wait is presentation, not suspense theatre over a pending computation: the
    /// Pokémon is already decided and stored when the reel starts turning.
    /// </remarks>
    private static TimeSpan SpinTimeFor(int tierIndex) =>
        TimeSpan.FromSeconds(6.0 + (1.25 * Math.Clamp(tierIndex, 0, 4)));

    /// <summary>Raised when the reel should spin. The view owns the animation; this owns the plan.</summary>
    public event EventHandler<SpinRequest>? SpinRequested;

    public ObservableCollection<BannerViewModel> Banners { get; } = [];

    /// <summary>The spinning reel, rebuilt on every roll.</summary>
    public ObservableCollection<ReelCellViewModel> Reel { get; } = [];

    /// <summary>
    /// Cells in one turn of the idle loop, or 0 when the strip is not a loop.
    /// </summary>
    /// <remarks>
    /// The view drifts the strip by exactly this many cells and starts over, which only looks
    /// continuous because <see cref="BuildIdleReel"/> repeated the head at the tail. It is said out
    /// loud rather than measured off the strip's width so the two halves cannot drift apart: a
    /// reel built for a roll is not a loop, and says 0.
    /// </remarks>
    public int IdleLoopCells { get; private set; }

    /// <summary>
    /// Leaving the gacha folds the tier list back up, and nothing else.
    /// </summary>
    /// <remarks>
    /// The result card stays: it is what the last pull <em>was</em>, and the Pokémon is already in
    /// the box whether the card is on screen or not. What was wrong was going to the shop with a
    /// tier's list unfolded and finding it still unfolded on the way back.
    /// </remarks>
    public override void ResetState()
    {
        ShowingPool = false;
        ShowingGrants = false;
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

    /// <summary>The last few rolls, newest first. Not persisted: it is what this sitting has seen.</summary>
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
        Banners.Clear();

        foreach (var banner in _gacha.Banners)
        {
            Banners.Add(new BannerViewModel(banner, DescribeOdds(banner), SlicesOf(banner)));
        }

        if (Reel.Count == 0)
        {
            BuildIdleReel();
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
                    Range = last
                        ? $"→ {floor + 1}+"
                        : floor == 0 ? $"→ ≤{tier.MaxBaseStatTotal}" : $"→ {floor + 1}-{tier.MaxBaseStatTotal}"
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

        await SeedHistoryAsync();

        _logger.LogInformation("Gacha: {Count} banners cargados, saldo {Balance}", Banners.Count, Balance);

        Status = Banners.Count == 0
            ? "No hay banners configurados. Revisa Data/gacha.json y Data/species.json."
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
                return;
            }

            await SpinAsync(pull);

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
            LastSprite = _sprites.Get(pull.Species);
            HasResult = true;

            Remember(pull);

            // El Pokémon va al PC del juego. Si no se puede ahora, se dice por qué en vez de
            // dejar creer que está en la partida: en la run sí está, en el juego todavía no.
            var delivered = await _delivery.DeliverAsync(pull, run);
            Status = delivered.Message;

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
                pull.SpeciesName, pull.Level, pull.TierId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tirada de gacha");
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsRolling = false;

            if (_runContext.Current is { } current)
            {
                await RefreshCreditsAsync(current);
            }

            RollCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Fills the reel and runs the spin: the wheel turns, the tier climbs, and it stops on the
    /// Pokémon that had already come out.
    /// </summary>
    private async Task SpinAsync(GachaPull pull)
    {
        BuildReel(pull);

        var tierIndex = Math.Max(0, Portals.ToList().FindIndex(p => p.TierId == pull.TierId));
        var duration = SpinTimeFor(tierIndex);

        var stopped = new TaskCompletionSource();
        // El cierre se sortea con SU PROPIA semilla. Con uno solo, quien juegue mucho aprende
        // donde va a parar tres clics antes de que pare; y si dependiera del tier, lo cantaria.
        SpinRequested?.Invoke(this, new SpinRequest(ReelWinnerIndex, duration,
            () => stopped.TrySetResult(), Views.ReelEnding.For(pull.Seed, pull.Number)));

        // El engaño: se arranca encendido en el tier más barato y se sube. Los dos tiers de
        // arriba suben en dos pasos, que es lo que hace que un legendario se note venir.
        DisplayedTier = Portals.Count > 0 ? Portals[0].TierId : pull.TierId;

        var stepUp = tierIndex >= 3 ? TimeSpan.FromSeconds(duration.TotalSeconds * 0.55) : TimeSpan.Zero;
        var reveal = TimeSpan.FromSeconds(duration.TotalSeconds * 0.80);

        if (stepUp > TimeSpan.Zero)
        {
            await Task.Delay(stepUp);
            DisplayedTier = Portals[tierIndex - 2].TierId;
            await Task.Delay(reveal - stepUp);
        }
        else
        {
            await Task.Delay(reveal);
        }

        DisplayedTier = pull.TierId;
        LastTier = pull.TierId;

        // Se espera a que la rueda pare de verdad, no a que pase el tiempo. Los tres segundos de
        // más son una red por si la vista no llegó a arrancar la animación —la pantalla no se
        // queda colgada— y van holgados a propósito: si la red salta antes de que la rueda pare,
        // el ganador se revela a medio camino y aparece fuera de su marco.
        await Task.WhenAny(stopped.Task, Task.Delay(duration + TimeSpan.FromSeconds(3)));

        // La rueda ya se ha parado: el ganador crece y los demás se apagan un poco, para que se
        // lea cuál es sin quitarles el color.
        foreach (var cell in Reel)
        {
            if (cell.IsWinner)
            {
                cell.IsRevealed = true;
            }
            else
            {
                cell.IsDimmed = true;
            }
        }
    }

    /// <summary>
    /// Builds the strip: silhouettes picked at random, with the Pokémon that came out sitting at
    /// the position the view will stop on.
    /// </summary>
    /// <summary>
    /// Fills the strip with silhouettes before anybody has pulled.
    /// </summary>
    /// <remarks>
    /// The reel used to be an empty black band taking the best spot on the screen until the first
    /// pull. Filled and drifting very slowly, the machine looks like a machine that is on.
    /// No winner in it: nothing has been decided, and a highlighted cell would say otherwise.
    /// </remarks>
    private void BuildIdleReel()
    {
        Reel.Clear();

        var random = new Random();
        var sprites = Enumerable.Range(0, ReelLength).Select(_ => _sprites.GetRandom(random)).ToArray();

        foreach (var sprite in sprites)
        {
            Reel.Add(new ReelCellViewModel(sprite, false));
        }

        // Y otra vez el principio, para que la vuelta al origen caiga sobre lo mismo que había.
        foreach (var sprite in sprites.Take(IdleTailLength))
        {
            Reel.Add(new ReelCellViewModel(sprite, false));
        }

        IdleLoopCells = ReelLength;
    }

    private void BuildReel(GachaPull pull)
    {
        Reel.Clear();
        IdleLoopCells = 0;

        // La seed de la tirada también siembra la tira, para que la misma tirada se vea igual al
        // recomputarla. Es decoración, pero decoración reproducible.
        var random = new Random(unchecked((int)(pull.Seed ^ (ulong)pull.Number)));

        for (var i = 0; i < ReelLength; i++)
        {
            var winner = i == ReelWinnerIndex;
            var sprite = winner ? _sprites.Get(pull.Species) : _sprites.GetRandom(random);
            Reel.Add(new ReelCellViewModel(sprite, winner));
        }
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

        History.Insert(0, new GachaHistoryViewModel(pull.SpeciesName,
            portal?.BrushKey ?? "AccentBrush", _sprites.Get(pull.Species)));

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
