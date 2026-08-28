using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// What the run has recorded about itself, read back.
/// </summary>
/// <remarks>
/// The application kept a hash-chained log of every point that ever moved, showed the last twenty
/// lines on HOME and threw the rest away. Nothing new is computed here: it is that log, counted.
/// </remarks>
public sealed partial class StatisticsViewModel : SectionViewModel
{
    private readonly StatisticsService _statistics;
    private readonly IRunContext _runContext;
    private readonly ILogger<StatisticsViewModel> _logger;

    public StatisticsViewModel(
        StatisticsService statistics,
        IRunContext runContext,
        ILogger<StatisticsViewModel> logger)
        : base("ESTADÍSTICAS", "De dónde han salido tus puntos y qué ha pasado en la run")
    {
        _statistics = statistics;
        _runContext = runContext;
        _logger = logger;

        _runContext.CurrentChanged += (_, _) => _ = RefreshAsync();
    }

    public override string IconKey => "IconChart";

    /// <summary>
    /// Indifferent. Everything on this screen comes out of the run's own database, so the emulator
    /// has nothing to do with it either way.
    /// </summary>
    public override GameNeed Needs => GameNeed.Either;

    public ObservableCollection<LedgerLine> Income { get; } = [];
    public ObservableCollection<LedgerLine> Outgoings { get; } = [];
    public ObservableCollection<Tally> Origins { get; } = [];
    public ObservableCollection<Tally> Fates { get; } = [];
    public ObservableCollection<RecordLine> Records { get; } = [];

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private string _balanceText = "—";

    [ObservableProperty]
    private string _earnedText = "—";

    [ObservableProperty]
    private string _spentText = "—";

    /// <summary>
    /// The balance curve, already as drawable points.
    /// </summary>
    /// <remarks>
    /// Built in a fixed 100×40 box and drawn with <c>Stretch="Fill"</c>, so the line scales to
    /// whatever width the panel ends up with without this having to know the width. Y is flipped
    /// here because screen coordinates grow downwards and a balance that goes up should go up.
    /// </remarks>
    [ObservableProperty]
    private PointCollection _curve = [];

    [ObservableProperty]
    private bool _hasCurve;

    public override Task ActivateAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var report = await _statistics.BuildAsync();

            HasRun = report.HasRun;

            BalanceText = report.HasRun ? report.Balance.ToString() : "—";
            EarnedText = report.HasRun ? $"+{report.Earned}" : "—";
            SpentText = report.HasRun ? $"−{report.Spent}" : "—";

            Fill(Income, report.Income);
            Fill(Outgoings, report.Outgoings);
            Fill(Origins, report.Origins);
            Fill(Fates, report.Fates);
            Fill(Records, report.Records);

            Curve = ToPolyline(report.Curve);
            HasCurve = Curve.Count > 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al construir las estadísticas");
            HasRun = false;
        }
    }

    private static void Fill<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static PointCollection ToPolyline(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return [];
        }

        var points = new PointCollection(values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var x = 100.0 * i / (values.Count - 1);
            points.Add(new Point(x, 40 - values[i] * 40));
        }

        return points;
    }
}
