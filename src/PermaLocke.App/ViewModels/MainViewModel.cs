using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>Shell view model: owns the sidebar, the current section and the run summary strip.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRunContext _runContext;
    private readonly HomeViewModel _home;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(HomeViewModel home, RandomizerViewModel randomizer,
        MiscellaneousViewModel miscellaneous, GachaViewModel gacha, PokemonViewerViewModel viewer,
        AchievementsViewModel achievements, ShopViewModel shop, IRunContext runContext,
        ILogger<MainViewModel> logger)
    {
        _home = home;
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
            new PendingSectionViewModel("POKE PASTE", "Fase 5",
                "Importación y exportación en formato Showdown con validación."),
            miscellaneous
        ];

        _selectedSection = Sections[0];

        // The balance is computed by HOME when it refreshes; mirroring it here avoids
        // querying the event store twice for the same number.
        _home.PropertyChanged += OnHomeChanged;
        _runContext.CurrentChanged += (_, _) => UpdateRole();
        UpdateRole();
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
