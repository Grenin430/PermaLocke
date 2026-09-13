using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>Shell view model: owns the sidebar, the current section and the run summary strip.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRunContext _runContext;
    private readonly HomeViewModel _home;
    private readonly RouletteViewModel _roulette;
    private readonly RouletteService _wheel;
    private readonly MiscellaneousViewModel _miscellaneous;
    private readonly PermaLocke.App.Services.IUiDispatcher _ui;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(HomeViewModel home, RandomizerViewModel randomizer,
        MiscellaneousViewModel miscellaneous, GachaViewModel gacha, PokemonViewerViewModel viewer,
        AchievementsViewModel achievements, ShopViewModel shop, PokePasteViewModel pokePaste,
        MapViewModel map,
        RouletteViewModel roulette, RouletteService wheel, MaintenanceViewModel maintenance,
        StatisticsViewModel statistics, SyncViewModel sync, BattleModeViewModel battle,
        PermaLocke.App.Services.GameLinkMonitor gameLink,
        PermaLocke.App.Services.IUiDispatcher ui,
        IRunContext runContext, ILogger<MainViewModel> logger)
    {
        _home = home;
        _roulette = roulette;
        _wheel = wheel;
        _miscellaneous = miscellaneous;
        _ui = ui;
        _runContext = runContext;
        _logger = logger;

        // La insignia de cada pantalla deja de ser una etiqueta y pasa a ser un indicador: dice si
        // el requisito se CUMPLE, no cuál es.
        gameLink.SnapshotChanged += (_, snapshot) => _ = _ui.InvokeAsync(() =>
        {
            GameConnected = snapshot.Connected;
            return Task.CompletedTask;
        });

        Sections =
        [
            home,
            randomizer,
            gacha,
            shop,
            achievements,
            map,
            viewer,
            pokePaste,
            statistics,
            sync,
            battle,
            miscellaneous,
            maintenance
        ];

        _selectedSection = Sections[0];

        // The balance is computed by HOME when it refreshes; mirroring it here avoids
        // querying the event store twice for the same number.
        _home.PropertyChanged += OnHomeChanged;

        // Al hilo de la interfaz a la fuerza: la run se carga en segundo plano, y tocar desde ahí
        // la colección que pinta la barra lateral tira la ventana entera. Medido arrancando con
        // una run ya guardada, que es justo el caso que no se prueba abriendo la app vacía.
        _runContext.CurrentChanged += (_, _) => _ = _ui.InvokeAsync(() =>
        {
            UpdateRole();
            UpdateRoulette();
            return Task.CompletedTask;
        });

        UpdateRole();
        UpdateRoulette();
    }

    /// <summary>
    /// Puts the wheel in the sidebar, or takes it out, according to the role of the loaded run.
    /// </summary>
    /// <remarks>
    /// A section that appears and disappears rather than one that is always there and refuses:
    /// only LUDÓPATA plays with the wheel, and a permanent RULETA that says "esta run no juega con
    /// la ruleta" would be a menu entry that exists to say no. It goes immediately above
    /// MISCELÁNEA, which is where the player was told to look for it.
    /// </remarks>
    private void UpdateRoulette()
    {
        var wanted = _runContext.Current is { } run && _wheel.PlaysWithTheWheel(run);
        var there = Sections.Contains(_roulette);

        if (wanted == there)
        {
            return;
        }

        if (wanted)
        {
            var at = Sections.IndexOf(_miscellaneous);
            Sections.Insert(at < 0 ? Sections.Count : at, _roulette);
            return;
        }

        // Si la sección que se va es la que está abierta, se vuelve a HOME: dejar seleccionada una
        // sección que ya no está en la lista deja la pantalla en blanco.
        if (ReferenceEquals(SelectedSection, _roulette))
        {
            SelectedSection = Sections[0];
        }

        Sections.Remove(_roulette);
    }

    public ObservableCollection<SectionViewModel> Sections { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedLabel))]
    [NotifyPropertyChangedFor(nameof(NeedState))]
    [NotifyPropertyChangedFor(nameof(ShowsNeed))]
    private SectionViewModel _selectedSection;

    /// <summary>Whether the emulator is answering right now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedLabel))]
    [NotifyPropertyChangedFor(nameof(NeedState))]
    private bool _gameConnected;

    public bool ShowsNeed => SelectedSection.ShowsNeed;

    /// <summary>
    /// The badge, which says whether the section's requirement is <b>met</b> and not merely what it
    /// is.
    /// </summary>
    /// <remarks>
    /// It started out static — "JUEGO ABIERTO" whether or not it was — and that turned out to be the
    /// difference between an indicator and a label. A whole session was played with the link down:
    /// nothing was registered, no death was counted, and every screen went on calmly stating the
    /// requirement it was failing. A requirement that never turns red is decoration.
    /// </remarks>
    public string NeedLabel => SelectedSection.Needs switch
    {
        GameNeed.Running => GameConnected ? "JUEGO CONECTADO" : "SIN CONEXIÓN",
        GameNeed.Closed => GameConnected ? "CIERRA EL JUEGO" : "JUEGO CERRADO",
        GameNeed.Either => "ABIERTO O CERRADO",
        _ => string.Empty
    };

    /// <summary>
    /// Colour band. Only the two answers PermaLocke can actually confirm go green or red.
    /// </summary>
    /// <remarks>
    /// A section that needs the game <em>shut</em> and finds no answer is <b>not</b> confirmed shut:
    /// Azahar may be running with its RPC server off, and writing the save under it would fail. So
    /// that case keeps the plain "this is what is needed" amber instead of a green that would be a
    /// guess. Green and red are for what was measured.
    /// </remarks>
    public string NeedState => SelectedSection.Needs switch
    {
        GameNeed.Running => GameConnected ? "ok" : "problem",
        GameNeed.Closed => GameConnected ? "problem" : "closed",
        GameNeed.Either => "either",
        _ => "none"
    };

    [ObservableProperty]
    private string _roleText = "—";

    /// <summary>Em dash while there is no run: better than showing a zero that means nothing.</summary>
    [ObservableProperty]
    private string _pointsText = "—";

    /// <summary>The same figure as a number, for the counter that rolls up to it.</summary>
    /// <remarks>
    /// Separate from <see cref="PointsText"/> and not a parse of it, because the text is allowed to
    /// be an em dash and a counter cannot count to one. With no run the number is hidden and the
    /// dash takes its place.
    /// </remarks>
    [ObservableProperty]
    private int _pointsValue;

    [ObservableProperty]
    private bool _hasPoints;

    /// <summary>Loads the section that is selected at startup.</summary>
    public Task InitialiseAsync() => ActivateAsync(SelectedSection);

    /// <summary>
    /// The section being left closes whatever it had open before the new one comes up.
    /// </summary>
    /// <remarks>
    /// On the way out and not on the way in, because a section that failed to close itself would
    /// otherwise be seen mid-flight by whoever is coming back to it. A screen that throws while
    /// tidying up must not stop the navigation either -- the player pressed a tab, and they get
    /// that tab.
    /// </remarks>
    partial void OnSelectedSectionChanged(SectionViewModel? oldValue, SectionViewModel newValue)
    {
        if (oldValue is null)
        {
            return;
        }

        try
        {
            oldValue.ResetState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al cerrar la sección {Section}", oldValue.Title);
        }
    }

    partial void OnSelectedSectionChanged(SectionViewModel value)
    {
        _logger.LogInformation("Navegación a la sección {Section}", value.Title);
        _ = ActivateAsync(value);
    }

    /// <summary>A section that fails to load must not take the application down with it.</summary>
    private async Task ActivateAsync(SectionViewModel section)
    {
        try
        {
            await section.ActivateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al activar la sección {Section}", section.Title);
        }
    }

    private void OnHomeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeViewModel.PointsBalance) or nameof(HomeViewModel.HasRun))
        {
            PointsText = _home.HasRun ? _home.PointsBalance.ToString() : "—";
            PointsValue = _home.HasRun ? _home.PointsBalance : 0;
            HasPoints = _home.HasRun;
        }

        if (e.PropertyName is nameof(HomeViewModel.RoleName) or nameof(HomeViewModel.HasRun))
        {
            UpdateRole();
        }
    }

    /// <summary>
    /// The role as the catalogue names it, falling back to its id only if the catalogue has not
    /// answered yet. The id is a slug -- "ludopata" -- and §50 is that no identifier reaches the
    /// screen; showing it in the header as well would only have spread that further.
    /// </summary>
    private void UpdateRole() =>
        RoleText = _home.RoleName.Length > 0
            ? _home.RoleName
            : _runContext.Current?.RoleId ?? "—";
}
