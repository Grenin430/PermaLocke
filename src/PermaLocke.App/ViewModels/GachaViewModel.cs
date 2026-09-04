using System.Collections.ObjectModel;
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
public sealed class PoolEntryViewModel(SpeciesStats species,
    System.Windows.Media.Imaging.BitmapSource? sprite)
{
    public string Name { get; } = species.Name;

    public int Total { get; } = species.BaseStatTotal;

    public bool Legendary { get; } = species.Legendary;

    public System.Windows.Media.Imaging.BitmapSource? Sprite { get; } = sprite;
}

/// <summary>One banner as the screen shows it, with its odds spelled out.</summary>
public sealed partial class BannerViewModel(GachaBanner banner, string odds) : ObservableObject
{
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
    private readonly PokemonSpriteService _sprites;
    private readonly IEventStore _events;
    private readonly ISpeciesLookup _species;
    private readonly ILogger<GachaViewModel> _logger;

    public GachaViewModel(GachaService gacha, IRunContext runContext, IPointsService points,
        IPokemonDelivery delivery, PokemonIdentityService identity, CreditService credits,
        PokemonSpriteService sprites, IEventStore events, ISpeciesLookup species,
        ILogger<GachaViewModel> logger)
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

    /// <summary>One portal per tier, in order of price. Built from the catalogue, not from XAML.</summary>
    public ObservableCollection<PortalViewModel> Portals { get; } = [];

    /// <summary>Who can come out of the tier being inspected.</summary>
    public ObservableCollection<PoolEntryViewModel> Pool { get; } = [];

    [ObservableProperty]
    private bool _showingPool;

    /// <summary>The strip of past rolls, which shares its row with the pool list.</summary>
    /// <remarks>
    /// One of the two has to give way: both live in the row that takes the leftover height, and
    /// with the pool open the strip was being drawn straight over it. The pool is what the player
    /// just asked for, so the strip is the one that waits.
    /// </remarks>
    public bool ShowHistory => HasHistory && !ShowingPool;

    partial void OnShowingPoolChanged(bool value) => OnPropertyChanged(nameof(ShowHistory));

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
            Banners.Add(new BannerViewModel(banner, DescribeOdds(banner)));
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
                    Range = last
                        ? $"{floor + 1}+"
                        : floor == 0 ? $"≤{tier.MaxBaseStatTotal}" : $"{floor + 1}-{tier.MaxBaseStatTotal}"
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
            var result = await _gacha.RollAsync(run, selected.Banner.Id, free);

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
    private void BuildReel(GachaPull pull)
    {
        Reel.Clear();

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

        foreach (var species in _gacha.PoolOf(tier, legendary: true)
            .Concat(_gacha.PoolOf(tier, legendary: false))
            .OrderByDescending(s => s.Legendary)
            .ThenByDescending(s => s.BaseStatTotal))
        {
            Pool.Add(new PoolEntryViewModel(species, _sprites.Get(species.Id)));
        }

        PoolBrushKey = portal.BrushKey;
        PoolTitle = $"{portal.Name} · {Pool.Count} Pokémon · {portal.Range}";
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

    private static string Pretty(string tierId) =>
        tierId.StartsWith("tier", StringComparison.OrdinalIgnoreCase)
            ? "Tier " + tierId[4..]
            : tierId;
}
