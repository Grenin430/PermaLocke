using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The standings, publishing this run into them, who this machine's player is, and the official rules.
/// </summary>
/// <remarks>
/// See <see cref="SyncService"/> for why this is a shared folder and not a server, and for what the
/// check against each player's history does and does not prove.
/// </remarks>
public sealed partial class SyncViewModel : SectionViewModel
{
    private readonly SyncService _sync;
    private readonly OfficialRulesService _rules;
    private readonly PlayerProfileService _profiles;
    private readonly IRunContext _runContext;
    private readonly ILogger<SyncViewModel> _logger;

    public SyncViewModel(SyncService sync, OfficialRulesService rules, PlayerProfileService profiles,
        IRunContext runContext, IUiDispatcher ui, ILogger<SyncViewModel> logger)
        : base("COMPETICIÓN", "La clasificación de tu grupo")
    {
        _sync = sync;
        _rules = rules;
        _profiles = profiles;
        _runContext = runContext;
        _logger = logger;

        _runContext.CurrentChanged += (_, _) => _ = ui.InvokeAsync(SafeRefreshAsync);
    }

    public override string IconKey => "IconPeople";

    /// <summary>
    /// Indifferent. Everything here is files: this run's own database and the folder everyone
    /// shares. The emulator has nothing to do with it.
    /// </summary>
    public override GameNeed Needs => GameNeed.Either;

    public ObservableCollection<StandingRow> Rows { get; } = [];

    /// <summary>
    /// The top three, in the order a podium is drawn: second, first, third.
    /// </summary>
    /// <remarks>
    /// A podium and not three more rows of a table, because this is the screen of a competition
    /// between five friends and the only thing anybody opens it for is to see who is winning. The
    /// order is the drawing order, not the ranking: the tallest block goes in the middle.
    /// </remarks>
    public ObservableCollection<StandingRow> Podium { get; } = [];

    /// <summary>From fourth place down, as a plain table.</summary>
    public ObservableCollection<StandingRow> Rest { get; } = [];

    [ObservableProperty]
    private bool _hasPodium;

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
    [NotifyCanExecuteChangedFor(nameof(PublishCommand), nameof(AdoptRulesCommand), nameof(SaveNameCommand))]
    private bool _isBusy;

    // ---------------------------------------------------------------- the player

    /// <summary>The name being edited. Saved only with GUARDAR.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveNameCommand))]
    private string _playerName = string.Empty;

    /// <summary>The short id, shown so two players with the same name can tell their folders apart.</summary>
    [ObservableProperty]
    private string _playerId = string.Empty;

    /// <summary>Whose the loaded run is, in one line.</summary>
    [ObservableProperty]
    private string _ownershipNote = string.Empty;

    [ObservableProperty]
    private bool _runIsForeign;

    [ObservableProperty]
    private string _nameStatus = string.Empty;

    private string _savedName = string.Empty;

    // ---------------------------------------------------------------- the official rules

    public ObservableCollection<RuleFileStatus> RuleFiles { get; } = [];

    [ObservableProperty]
    private string _rulesMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AdoptRulesCommand))]
    private bool _rulesAdoptable;

    [ObservableProperty]
    private bool _hasRuleFiles;

    [ObservableProperty]
    private string _rulesStatus = string.Empty;

    private bool CanPublish => !IsBusy && Folder.Length > 0 && _runContext.Current is not null && !RunIsForeign;

    private bool CanAdoptRules => !IsBusy && RulesAdoptable;

    private bool CanSaveName => !IsBusy
        && PlayerProfileService.CleanName(PlayerName) is { } clean
        && clean != _savedName;

    public override Task ActivateAsync() => SafeRefreshAsync();

    private async Task SafeRefreshAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se ha podido refrescar la competición");
            Message = "No se ha podido leer la carpeta compartida.";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Folder = _sync.SharedFolder;

        await RefreshPlayerAsync();

        var standings = await _sync.ReadAsync();

        Rows.Clear();
        foreach (var row in standings.Rows)
        {
            Rows.Add(row);
        }

        Podium.Clear();
        Rest.Clear();

        // Segundo, primero, tercero. Con menos de tres no hay podio: dos bloques y un hueco se
        // leerian como que falta alguien, y lo que falta es gente que publique.
        var top = standings.Rows.Take(3).ToList();
        HasPodium = top.Count == 3;

        if (HasPodium)
        {
            Podium.Add(top[1]);
            Podium.Add(top[0]);
            Podium.Add(top[2]);
        }

        foreach (var row in standings.Rows.Skip(HasPodium ? 3 : 0))
        {
            Rest.Add(row);
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

        RefreshRules();
    }

    private async Task RefreshPlayerAsync()
    {
        var status = await _sync.PlayerAsync();

        _savedName = status.Profile?.Name ?? string.Empty;
        PlayerName = _savedName;
        PlayerId = status.Profile?.ShortId ?? string.Empty;
        RunIsForeign = status.Ownership == RunOwnership.Foreign;

        OwnershipNote = status.Ownership switch
        {
            RunOwnership.NoRun => "No hay ninguna run cargada.",
            RunOwnership.Foreign => "Esta run es de otro jugador.",
            RunOwnership.Linked => "Tu run está vinculada a tu perfil.",
            _ => "La run cargada es tuya."
        };

        PublishCommand.NotifyCanExecuteChanged();
        SaveNameCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRules()
    {
        var status = _rules.Status();

        RuleFiles.Clear();
        foreach (var file in status.Files)
        {
            RuleFiles.Add(file);
        }

        HasRuleFiles = RuleFiles.Count > 0;
        RulesMessage = status.Message;
        RulesAdoptable = status.CanAdopt;
    }

    /// <summary>
    /// Picks the folder everyone shares.
    /// </summary>
    /// <remarks>
    /// A folder and not a file: whoever chooses it points at the Drive or Dropbox folder the group
    /// already uses, and from then on PermaLocke only ever writes inside its own player folder in it.
    /// </remarks>
    [RelayCommand]
    private async Task ChooseFolderAsync()
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
        await SafeRefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        IsBusy = true;
        PublishStatus = "Publicando...";

        try
        {
            PublishStatus = await _sync.PublishAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la publicación de la instantánea");
            PublishStatus = "No se ha podido publicar.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Renames this machine's player. The id stays, so the row in the standings and the folder are
    /// the same ones, renamed the next time it publishes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveName))]
    private async Task SaveNameAsync()
    {
        IsBusy = true;

        try
        {
            var renamed = await _profiles.RenameAsync(PlayerName);

            NameStatus = renamed is null
                ? "Escribe un nombre."
                : Folder.Length > 0
                    ? $"Ahora eres {renamed.Name}. Publica para que los demás lo vean."
                    : $"Ahora eres {renamed.Name}.";

            await RefreshPlayerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se ha podido cambiar el nombre del jugador");
            NameStatus = "No se ha podido guardar el nombre.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdoptRules))]
    private async Task AdoptRulesAsync()
    {
        IsBusy = true;
        RulesStatus = "Adoptando...";

        try
        {
            RulesStatus = await _rules.AdoptAsync();
            RefreshRules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se han podido adoptar las reglas oficiales");
            RulesStatus = "No se han podido adoptar.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
