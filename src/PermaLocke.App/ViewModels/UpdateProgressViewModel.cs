using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The update window (§201): how the download is going, then checking, installing and restarting. Only shows what
/// <c>UpdateService</c> tells it; the one thing it does is ask to cancel, while there is still something to cancel.
/// </summary>
public sealed partial class UpdateProgressViewModel(string version) : ObservableObject
{
    private readonly CancellationTokenSource _cancel = new();

    public string Heading { get; } = $"ACTUALIZANDO A LA {version}";

    public CancellationToken Token => _cancel.Token;

    [ObservableProperty]
    private double _fraction;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    private string _percent = "0 %";

    [ObservableProperty]
    private string _speed = string.Empty;

    [ObservableProperty]
    private string _left = "CALCULANDO";

    [ObservableProperty]
    private string _status = "DESCARGANDO";

    /// <summary>Only during the download: once installing starts, stopping halfway would be worse than finishing.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _canCancel = true;

    public void Show(DownloadProgress progress)
    {
        Fraction = progress.Fraction;
        Size = progress.Size;
        Percent = progress.Percent;
        Speed = progress.Speed;
        Left = progress.Left;
    }

    /// <summary>A step after the download: the numbers stay, the bar full.</summary>
    public void Step(string status)
    {
        CanCancel = false;
        Fraction = 1;
        Percent = "100 %";
        Speed = string.Empty;
        Left = string.Empty;
        Status = status;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        Status = "CANCELANDO";
        _cancel.Cancel();
    }
}
