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
    private readonly GroupSectionViewModel _play;
    private readonly SectionViewModel _gacha;
    private readonly PermaLocke.App.Services.IUiDispatcher _ui;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(LauncherViewModel launcher, HomeViewModel home, RandomizerViewModel randomizer,
        MiscellaneousViewModel miscellaneous, GachaViewModel gacha, PokemonViewerViewModel viewer,
        EvTrainingViewModel evTraining, MoveReminderViewModel moveReminder, AchievementsViewModel achievements,
        ShopViewModel shop, PokePasteViewModel pokePaste,
        MapViewModel map,
        RouletteViewModel roulette, RouletteService wheel, SettingsViewModel settings,
        CemeteryViewModel cemetery, SyncViewModel sync, BattleModeViewModel battle,
        GiftInboxViewModel gifts, InformationViewModel information,
        PermaLocke.App.Services.GameLinkMonitor gameLink,
        PermaLocke.App.Services.IUiDispatcher ui,
        PermaLocke.App.Services.AlolaSky sky,
        IRunContext runContext, ILogger<MainViewModel> logger, PermaLocke.Infrastructure.AppPaths paths)
    {
        Sky = sky;
        Launcher = launcher;
        Gifts = gifts;
        _home = home;
        _roulette = roulette;
        _wheel = wheel;
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

        // Grupos (2026-09-24): la barra tenía 17 secciones. Las que tratan de lo mismo cuelgan de una entrada.
        var play = new GroupSectionViewModel("JUGAR", "IconPlay", logger, launcher, randomizer);
        var team = new GroupSectionViewModel("EQUIPO", "IconGrid", logger, viewer, evTraining, moveReminder, pokePaste);
        var tournament = new GroupSectionViewModel("TORNEO", "IconTrophy", logger, sync, achievements, battle, cemetery);
        var info = new GroupSectionViewModel("INFORMACIÓN", "IconDocument", logger,
            new InformationPageViewModel("EVOLUCIONES", false, information),
            new InformationPageViewModel("TIENDAS", true, information),
            miscellaneous);
        _play = play;
        _gacha = gacha;
        foreach (var group in new[] { play, team, tournament, info })
        {
            group.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GroupSectionViewModel.SelectedPage) && ReferenceEquals(SelectedSection, group))
                {
                    OnPropertyChanged(nameof(NeedLabel));
                    OnPropertyChanged(nameof(NeedState));
                    OnPropertyChanged(nameof(NeedHint));
                    OnPropertyChanged(nameof(ShowsNeed));
                    OnPropertyChanged(nameof(ShowsPlayButton));
                }
            };
        }

        Sections =
        [
            play,
            home,
            gacha,
            shop,
            map,
            team,
            tournament,
            info,
            settings
        ];

        _selectedSection = Sections[0];

        // Los enlaces de debajo de la barra de JUGAR llevan a otras secciones por su título.
        launcher.NavigateRequested += title =>
        {
            Navigate(title);
        };

        // ENTRENAR EV desde la ficha del visor: el banco se abre ya con ese Pokémon.
        viewer.TrainRequested += pokemon =>
        {
            evTraining.Focus(pokemon);
            Navigate(evTraining.Title);
        };

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

        // Justo debajo del GACHA (2026-09-25: el jugador la quería suelta, no dentro de la tienda).
        if (wanted)
        {
            Sections.Insert(Sections.IndexOf(_gacha) + 1, _roulette);
            return;
        }

        // Si la sección que se va es la que está abierta, se vuelve a la primera: dejar seleccionada una sección que
        // ya no está en la lista deja la pantalla en blanco.
        if (ReferenceEquals(SelectedSection, _roulette))
        {
            SelectedSection = Sections[0];
        }

        Sections.Remove(_roulette);
    }

    public ObservableCollection<SectionViewModel> Sections { get; }

    /// <summary>JUGAR, whose small button also sits in every page's header (§125).</summary>
    public LauncherViewModel Launcher { get; }

    /// <summary>The gift in the header: what the admin has left for this player (§129).</summary>
    public GiftInboxViewModel Gifts { get; }

    /// <summary>The hour in the player's Alola, for the floor and the sidebar's window.</summary>
    public PermaLocke.App.Services.AlolaSky Sky { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedLabel))]
    [NotifyPropertyChangedFor(nameof(NeedState))]
    [NotifyPropertyChangedFor(nameof(NeedHint))]
    [NotifyPropertyChangedFor(nameof(ShowsNeed))]
    [NotifyPropertyChangedFor(nameof(ShowsPlayButton))]
    private SectionViewModel _selectedSection;

    /// <summary>Whether the emulator is answering right now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedLabel))]
    [NotifyPropertyChangedFor(nameof(NeedState))]
    [NotifyPropertyChangedFor(nameof(NeedHint))]
    private bool _gameConnected;

    public bool ShowsNeed => SelectedSection.ShowsNeed;

    /// <summary>The header's play button is for every page but JUGAR itself, which has the big one.</summary>
    public bool ShowsPlayButton => !(ReferenceEquals(SelectedSection, _play) && ReferenceEquals(_play.SelectedPage, Launcher));

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenLauncher()
    {
        _play.SelectedPage = Launcher;
        SelectedSection = _play;
    }

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
    /// <summary>
    /// The sentence under the badge, so that nobody has to guess what «JUEGO CERRADO» asks of them (2026-09-24: the
    /// player wanted it impossible to miss in every section).
    /// </summary>
    public string NeedHint => SelectedSection.Needs switch
    {
        GameNeed.Running => GameConnected
            ? "Azahar conectado."
            : "Abre Azahar con la partida cargada.",
        GameNeed.Closed => GameConnected
            ? "Guarda y cierra Azahar."
            : "Guarda en el juego antes de cerrarlo.",
        GameNeed.Either => "Usa tu última partida guardada.",
        _ => string.Empty
    };

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

    /// <summary>
    /// Opens a section by its title, also when it hangs from a group. Pages first, so «JUGAR» opens the launcher page.
    /// </summary>
    public bool Navigate(string title)
    {
        foreach (var section in Sections)
        {
            if (section is GroupSectionViewModel group
                && group.Pages.FirstOrDefault(p => string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase)) is { } page)
            {
                group.SelectedPage = page;
                SelectedSection = group;
                return true;
            }

            if (string.Equals(section.Title, title, StringComparison.OrdinalIgnoreCase))
            {
                SelectedSection = section;
                return true;
            }
        }

        return false;
    }

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
