using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One achievement as the list shows it.</summary>
public sealed partial class AchievementRowViewModel(AchievementProgress progress) : ObservableObject
{
    public AchievementProgress Progress { get; } = progress;

    public string Id => Progress.Achievement.Id;

    public string Name => Progress.Achievement.Name;

    public string Description => Progress.Achievement.Description;

    public string Reward => $"+{Progress.Achievement.Points}";

    /// <summary>"3 / 5", or "sin detectar" when nothing can ever count towards it.</summary>
    public string Label => Progress.Label;

    public int Target => Progress.Achievement.Target;

    public int Count => Progress.Shown;

    public bool CanClaim => Progress.CanClaim;

    public bool Claimed => Progress.Claimed;

    public bool Unlocked => Progress.Unlocked;

    /// <summary>The counter can still be moved by hand, so the card shows its two buttons.</summary>
    public bool CanMark => Progress.CanMark;

    /// <summary>Where the number comes from, said out loud on the card.</summary>
    public string Source => Progress.Achievement switch
    {
        { IsFromGame: true } => "lo cuenta el juego",
        { IsAutomatic: true } => "lo cuenta PermaLocke",
        _ => "a mano"
    };

    public bool IsFromGame => Progress.Achievement.IsFromGame;

    /// <summary>First letter of the name, which is what the medal carries.</summary>
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    /// <summary>
    /// Colour of the medal: grey while it is pending, the accent once it is done.
    /// </summary>
    /// <remarks>
    /// Deliberately not a Z-Crystal out of the cartridge. There are eighteen of them and no way to
    /// tell which is which without matching them by colour, which would be guessing (§34).
    /// </remarks>
    public Brush Medal => Progress.Claimed
        ? Brushes.MediumSeaGreen
        : Progress.Unlocked ? Brushes.Gold : new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x5C));
}

/// <summary>
/// The achievements screen: what the run has earned, what it is short of, and what cannot be
/// counted yet.
/// </summary>
/// <remarks>
/// Progress is recomputed from the event log every time this opens, so it can never disagree with
/// the history. Claiming is explicit: the points appear in the log with the achievement that paid
/// for them.
/// </remarks>
public sealed partial class AchievementsViewModel : SectionViewModel
{
    private readonly AchievementService _achievements;
    private readonly PenaltyService _penalties;
    private readonly IPointsService _points;
    private readonly IRunContext _runContext;
    private readonly ILogger<AchievementsViewModel> _logger;

    public AchievementsViewModel(AchievementService achievements, PenaltyService penalties,
        IPointsService points, IRunContext runContext, ILogger<AchievementsViewModel> logger)
        : base("LOGROS")
    {
        _achievements = achievements;
        _penalties = penalties;
        _points = points;
        _runContext = runContext;
        _logger = logger;
    }

    public ObservableCollection<AchievementRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private int _balance;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>What losing costs, spelled out so nobody has to open a JSON to know.</summary>
    [ObservableProperty]
    private string _penaltyText = string.Empty;

    /// <summary>Where the game counters come from, and how fresh they are.</summary>
    [ObservableProperty]
    private string _recordsText = string.Empty;

    [ObservableProperty]
    private int _wipes;

    [ObservableProperty]
    private bool _isBusy;

    public override Task ActivateAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_runContext.Current is not { } run)
        {
            Status = "No hay ninguna run abierta.";
            Rows.Clear();
            return;
        }

        IsBusy = true;

        try
        {
            var progress = await _achievements.GetProgressAsync(run.Id);

            Rows.Clear();
            foreach (var row in progress)
            {
                Rows.Add(new AchievementRowViewModel(row));
            }

            Balance = await _points.GetBalanceAsync(run.Id);
            Wipes = await _penalties.CountWipesAsync(run.Id);

            var claimed = progress.Count(p => p.Claimed);
            var ready = progress.Count(p => p.CanClaim);
            var manual = progress.Count(p => p.Achievement.IsManual);

            Summary = $"{claimed} de {progress.Count} cobrados"
                      + (ready > 0 ? $" · {ready} listos para cobrar" : string.Empty)
                      + (manual > 0 ? $" · {manual} se marcan a mano" : string.Empty);

            // De dónde vienen los números del juego, y desde cuándo. Sin esto, un contador
            // parado parece roto cuando lo que pasa es que el jugador no ha guardado.
            RecordsText = _achievements.LastRecords switch
            {
                { Available: false } bad => bad.Problem ?? "No se han podido leer los contadores del juego.",
                { Notice: { Length: > 0 } notice } => notice,
                { Available: true } => "Los contadores del juego se leen de la partida guardada.",
                _ => string.Empty
            };

            var rules = _penalties.Rules;
            PenaltyText = $"Cada muerte cuesta {rules.PerDeath} puntos. Que caiga el equipo entero "
                          + $"cuesta {rules.PerWipe} más, hasta {rules.MaxWipes} veces "
                          + $"({rules.MaxWipeCost} como mucho). Llevas {Wipes}. "
                          + "Los puntos pueden quedarse en negativo.";

            Status = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer los logros");
            Status = "No se han podido leer los logros. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClaimAsync(AchievementRowViewModel? row)
    {
        if (row is null || _runContext.Current is not { } run)
        {
            return;
        }

        var result = await _achievements.ClaimAsync(run, row.Id);

        Status = result.Success
            ? $"«{row.Name}» cobrado: +{row.Progress.Achievement.Points} puntos."
            : result.FailureReason ?? "No se ha podido cobrar.";

        await RefreshAsync();
    }

    /// <summary>Moves a manual counter one step.</summary>
    [RelayCommand]
    private Task MarkAsync(AchievementRowViewModel? row) => AdvanceAsync(row, complete: false);

    /// <summary>Takes a manual counter straight to its target.</summary>
    [RelayCommand]
    private Task CompleteAsync(AchievementRowViewModel? row) => AdvanceAsync(row, complete: true);

    private async Task AdvanceAsync(AchievementRowViewModel? row, bool complete)
    {
        if (row is null || _runContext.Current is not { } run)
        {
            return;
        }

        var result = await _achievements.MarkAsync(run, row.Id, complete);

        Status = result.Success
            ? $"«{row.Name}» actualizado a mano. Queda anotado en el historial."
            : result.FailureReason ?? "No se ha podido marcar.";

        await RefreshAsync();
    }
}
