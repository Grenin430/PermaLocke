using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Setting the randomized world aside to link-battle, and putting it back.
/// </summary>
/// <remarks>
/// The whole risk of this feature is <b>forgetting it is on</b>: playing half an hour on the
/// cartridge without noticing, wondering why the wild Pokémon are the ordinary ones. So the state
/// is the loudest thing on the screen, and it says what to do next rather than merely what it is.
/// </remarks>
public sealed partial class BattleModeViewModel : SectionViewModel
{
    private readonly BattleModeService _battle;
    private readonly IAppDialogs _dialogs;
    private readonly ILogger<BattleModeViewModel> _logger;

    public BattleModeViewModel(BattleModeService battle, IAppDialogs dialogs,
        ILogger<BattleModeViewModel> logger)
        : base("COMBATES", "Pelear con tus amigos")
    {
        _battle = battle;
        _dialogs = dialogs;
        _logger = logger;
    }

    public override string IconKey => "IconSwords";

    /// <summary>
    /// Closed. Mods are read when the game loads, so the swap only means anything with Azahar shut.
    /// </summary>
    public override GameNeed Needs => GameNeed.Closed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInBattle))]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    [NotifyPropertyChangedFor(nameof(HasNothing))]
    [NotifyPropertyChangedFor(nameof(Headline))]
    [NotifyPropertyChangedFor(nameof(Explanation))]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private BattleModeState _state = BattleModeState.Nothing;

    public bool IsInBattle => State == BattleModeState.InBattle;
    public bool IsPlaying => State == BattleModeState.Playing;
    public bool HasNothing => State == BattleModeState.Nothing;

    public string Headline => State switch
    {
        BattleModeState.Playing => "Estás jugando tu mundo randomizado",
        BattleModeState.InBattle => "MODO COMBATE: tu mundo está guardado",
        _ => "No hay ninguna randomización instalada"
    };

    public string Explanation => State switch
    {
        BattleModeState.Playing =>
            "Cuando quedéis para pelear, pulsa el botón.",

        BattleModeState.InBattle =>
            "Cuando terminéis de pelear, devuelve tu mundo. No sigas tu run así.",

        _ => "Primero instala tu mundo desde el RANDOMIZADOR."
    };

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    private bool _isBusy;

    private bool CanPrepare => !IsBusy && State == BattleModeState.Playing;

    private bool CanRestore => !IsBusy && State == BattleModeState.InBattle;

    public override Task ActivateAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Refresh() => State = _battle.State;

    [RelayCommand(CanExecute = nameof(CanPrepare))]
    private async Task PrepareAsync()
    {
        if (!_dialogs.Confirm(
                "Preparar para combatir",
                "Tu partida no se toca. Acuérdate de devolver tu mundo al terminar.\n\n¿Preparar?"))
        {
            return;
        }

        await SwapAsync(_battle.PrepareAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private Task RestoreAsync() => SwapAsync(_battle.RestoreAsync);

    private async Task SwapAsync(Func<CancellationToken, Task<BattleModeResult>> what)
    {
        IsBusy = true;
        Status = "Cambiando...";

        try
        {
            var result = await what(default);
            Status = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el cambio de modo combate");
            Status = "No se ha podido cambiar.";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }
}
