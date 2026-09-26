using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.App.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The bar at the top of every section while a new version waits (§202): the warning sign, «ACTUALIZACIÓN x.y.z
/// DISPONIBLE» and a small ACTUALIZAR that installs it. It replaces the yes/no dialog of §196. With the game open the
/// button waits and says why: installing restarts PermaLocke.
/// </summary>
public sealed partial class UpdateBannerViewModel : ObservableObject
{
    private readonly UpdateService _updates;
    private readonly EmulatorLauncher _emulator;

    public UpdateBannerViewModel(UpdateService updates, EmulatorLauncher emulator)
    {
        _updates = updates;
        _emulator = emulator;
        updates.PropertyChanged += Refresh;
        emulator.PropertyChanged += Refresh;
    }

    public bool IsVisible => _updates.IsAvailable;

    public string Text => _updates.Available is { } asset ? $"ACTUALIZACIÓN {asset.Version} DISPONIBLE" : string.Empty;

    public string Hint => _updates.Installing
        ? "ACTUALIZANDO"
        : _emulator.IsRunning ? "CIERRA EL JUEGO PARA ACTUALIZAR" : "PERMALOCKE SE REINICIA SOLO";

    private bool CanUpdate => _updates.IsAvailable && !_updates.Installing && !_emulator.IsRunning;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private Task UpdateAsync() => _updates.InstallAsync();

    private void Refresh(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Hint));
        UpdateCommand.NotifyCanExecuteChanged();
    }
}
