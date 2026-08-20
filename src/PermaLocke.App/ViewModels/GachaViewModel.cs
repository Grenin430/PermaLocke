using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One banner as the screen shows it, with its odds spelled out.</summary>
public sealed class BannerViewModel(GachaBanner banner, string odds)
{
    public GachaBanner Banner { get; } = banner;

    public string Name => Banner.Name;

    public string Description => Banner.Description;

    public string Cost => $"{Banner.Cost} puntos";

    /// <summary>The odds, written out. Hiding them would be the one thing a gacha must not do.</summary>
    public string Odds { get; } = odds;
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
    private readonly ILogger<GachaViewModel> _logger;

    public GachaViewModel(GachaService gacha, IRunContext runContext, IPointsService points,
        IPokemonDelivery delivery, ILogger<GachaViewModel> logger)
        : base("GACHA")
    {
        _gacha = gacha;
        _runContext = runContext;
        _points = points;
        _delivery = delivery;
        _logger = logger;
    }

    /// <summary>
    /// How long the reveal animation runs before the result appears.
    /// </summary>
    /// <remarks>
    /// The roll itself is instantaneous, so without this the animation would be over before it
    /// started. The wait is presentation, not suspense theatre over a pending computation: the
    /// Pokémon is already decided and stored when the wait begins.
    /// </remarks>
    private static readonly TimeSpan RevealDelay = TimeSpan.FromSeconds(2.6);

    public ObservableCollection<BannerViewModel> Banners { get; } = [];

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

    public override async Task ActivateAsync()
    {
        Banners.Clear();

        foreach (var banner in _gacha.Banners)
        {
            Banners.Add(new BannerViewModel(banner, DescribeOdds(banner)));
        }

        SelectedBanner ??= Banners.FirstOrDefault();

        if (_runContext.Current is { } run)
        {
            Balance = await _points.GetBalanceAsync(run.Id);
        }

        _logger.LogInformation("Gacha: {Count} banners cargados, saldo {Balance}", Banners.Count, Balance);

        Status = Banners.Count == 0
            ? "No hay banners configurados. Revisa Data/gacha.json y Data/species.json."
            : string.Empty;

        RollCommand.NotifyCanExecuteChanged();
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
        HasResult = false;
        RollCommand.NotifyCanExecuteChanged();
        Status = string.Empty;

        try
        {
            var result = await _gacha.RollAsync(run, selected.Banner.Id);

            Balance = result.Balance;

            if (!result.Success || result.Pull is not { } pull)
            {
                Status = result.Error ?? "La tirada no se ha podido completar.";
                return;
            }

            // El portal ya sabe de qué color es y se ilumina; el resultado aparece cuando la
            // nave entra en él. La tirada ya está decidida y guardada antes de esta espera:
            // no se está fingiendo un cálculo, solo dándole tiempo a la animación.
            LastTier = pull.TierId;
            await Task.Delay(RevealDelay);

            LastPull = pull;
            HasResult = true;

            // El Pokémon va al PC del juego. Si no se puede ahora, se dice por qué en vez de
            // dejar creer que está en la partida: en la run sí está, en el juego todavía no.
            var delivered = await _delivery.DeliverAsync(pull, run);
            Status = delivered.Message;

            _logger.LogInformation("Gacha {Banner}: {Species} Nv.{Level} ({Tier})",
                selected.Banner.Id, pull.SpeciesName, pull.Level, pull.TierId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tirada de gacha");
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsRolling = false;
            RollCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedBannerChanged(BannerViewModel? value) =>
        RollCommand.NotifyCanExecuteChanged();

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
