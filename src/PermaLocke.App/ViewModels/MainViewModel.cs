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
        RouletteViewModel roulette, RouletteService wheel,
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

        Sections =
        [
            home,
            randomizer,
            gacha,
            shop,
            achievements,
            viewer,
            pokePaste,
            miscellaneous
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
    private SectionViewModel _selectedSection;

    [ObservableProperty]
    private string _roleText = "—";

    /// <summary>Em dash while there is no run: better than showing a zero that means nothing.</summary>
    [ObservableProperty]
    private string _pointsText = "—";

    /// <summary>Loads the section that is selected at startup.</summary>
    public Task InitialiseAsync() => ActivateAsync(SelectedSection);

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
        }
    }

    private void UpdateRole() => RoleText = _runContext.Current?.RoleId ?? "—";
}
