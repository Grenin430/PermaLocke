using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Run creation. Detects the vanilla ROM in ROM/ and refuses to continue unless it is a
/// decrypted Ultra Moon dump, because everything downstream assumes exactly that.
/// </summary>
public sealed partial class CreateRunViewModel : ObservableObject
{
    private readonly RunService _runs;
    private readonly AppPaths _paths;
    private readonly ILogger<CreateRunViewModel> _logger;

    public CreateRunViewModel(RunService runs, AppPaths paths, ILogger<CreateRunViewModel> logger)
    {
        _runs = runs;
        _paths = paths;
        _logger = logger;
        DetectRom();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _runName = "Mi PermaLocke";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private string _roleId = "experto";

    [ObservableProperty]
    private string _romStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _romIsValid;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Raised once the run exists, so the hosting window can close itself.</summary>
    public event EventHandler? Finished;

    private RomInfo? _rom;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        try
        {
            var run = await _runs.CreateAsync(new CreateRunRequest(
                RunName.Trim(),
                PlayerName.Trim(),
                RoleId.Trim(),
                GameVersion.UltraMoon,
                TitleId: _rom?.TitleId));

            _logger.LogInformation("Run creada: {Name} ({Seed}) para {Player}",
                run.Name, run.SeedLabel, run.PlayerName);

            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear la run");
            ErrorMessage = "No se pudo crear la run. Revisa la carpeta Logs para el detalle.";
        }
    }

    private bool CanCreate() =>
        RomIsValid
        && !string.IsNullOrWhiteSpace(RunName)
        && !string.IsNullOrWhiteSpace(PlayerName);

    private void DetectRom()
    {
        var found = RomInspector.ScanFolder(_paths.Rom);

        if (found.Count == 0)
        {
            RomStatus = $"No se ha encontrado ninguna ROM en:\n{_paths.Rom}";
            RomIsValid = false;
            return;
        }

        _rom = found.FirstOrDefault(r => r.IsSupported) ?? found[0];

        if (_rom.Game is null)
        {
            RomStatus = $"«{_rom.FileName}» no es Pokémon Ultra Luna.\nCódigo de producto: {_rom.ProductCode}";
            RomIsValid = false;
            return;
        }

        if (!_rom.IsDecrypted)
        {
            RomStatus = $"«{_rom.FileName}» está encriptada.\nAzahar necesita un volcado desencriptado.";
            RomIsValid = false;
            return;
        }

        RomStatus =
            $"Pokémon Ultra Luna detectada\n{_rom.FileName}\nTitle ID: {_rom.TitleId}   ·   desencriptada";
        RomIsValid = true;
        _logger.LogInformation("ROM válida detectada: {File} ({TitleId})", _rom.FileName, _rom.TitleId);
    }
}
