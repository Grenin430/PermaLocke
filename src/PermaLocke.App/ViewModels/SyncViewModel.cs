using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The standings, and publishing this run into them.
/// </summary>
/// <remarks>
/// See <see cref="SyncService"/> for why this is a shared folder and not a server, and for why
/// nothing here is presented as verified.
/// </remarks>
public sealed partial class SyncViewModel : SectionViewModel
{
    private readonly SyncService _sync;
    private readonly IRunContext _runContext;
    private readonly ILogger<SyncViewModel> _logger;

    public SyncViewModel(SyncService sync, IRunContext runContext, ILogger<SyncViewModel> logger)
        : base("COMPETICIÓN", "La clasificación de tu grupo, por una carpeta compartida")
    {
        _sync = sync;
        _runContext = runContext;
        _logger = logger;

        _runContext.CurrentChanged += (_, _) => Refresh();
    }

    public override string IconKey => "IconPeople";

    /// <summary>
    /// Indifferent. Everything here is files: this run's own database and the folder everyone
    /// shares. The emulator has nothing to do with it.
    /// </summary>
    public override GameNeed Needs => GameNeed.Either;

    public ObservableCollection<StandingRow> Rows { get; } = [];

    /// <summary>Files in the folder that could not be read, said by name.</summary>
    public ObservableCollection<string> Broken { get; } = [];

    [ObservableProperty]
    private bool _hasBroken;

    /// <summary>Who can link-battle whom, or empty with nobody published.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBattleNote))]
    private string _battleNote = string.Empty;

    [ObservableProperty]
    private string _battleState = "none";

    public bool HasBattleNote => BattleNote.Length > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private string _folder = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _publishStatus = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private bool CanPublish => !IsBusy && Folder.Length > 0 && _runContext.Current is not null;

    public override Task ActivateAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Refresh()
    {
        Folder = _sync.SharedFolder;

        var standings = _sync.Read();

        Rows.Clear();
        foreach (var row in standings.Rows)
        {
            Rows.Add(row);
        }

        Broken.Clear();
        foreach (var line in standings.Broken)
        {
            Broken.Add(line);
        }

        HasBroken = Broken.Count > 0;
        BattleNote = standings.BattleNote;
        BattleState = standings.BattleState;
        Message = standings.Message;
    }

    /// <summary>
    /// Picks the folder everyone shares.
    /// </summary>
    /// <remarks>
    /// A folder and not a file: whoever chooses it points at the Drive or Dropbox folder the group
    /// already uses, and from then on PermaLocke only ever writes one file of its own in it.
    /// </remarks>
    [RelayCommand]
    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Carpeta compartida de la competición",
            InitialDirectory = Folder.Length > 0 ? Folder : string.Empty
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _sync.SetSharedFolder(dialog.FolderName);
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        IsBusy = true;
        PublishStatus = "Publicando...";

        try
        {
            PublishStatus = await _sync.PublishAsync();
            Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la publicación de la instantánea");
            PublishStatus = "No se ha podido publicar. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
