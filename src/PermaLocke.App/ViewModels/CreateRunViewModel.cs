using PermaLocke.App.Services;
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
    private readonly PlayerProfileService _profiles;
    private readonly AppPaths _paths;
    private readonly DiscordLogin _discord;
    private readonly ILogger<CreateRunViewModel> _logger;

    public CreateRunViewModel(RunService runs, PlayerProfileService profiles, AppPaths paths,
        IRoleCatalog roles, DiscordLogin discord, ILogger<CreateRunViewModel> logger)
    {
        _runs = runs;
        _profiles = profiles;
        _paths = paths;
        _discord = discord;
        _logger = logger;

        foreach (var role in roles.All)
        {
            Roles.Add(new RoleChoiceViewModel(role, OnRoleChosen));
        }

        // Sin roles no se crea nada. Arrancar a todo el mundo con reglas inventadas sería peor
        // que no arrancar, porque la competición no se enteraría hasta el recuento final.
        RoleProblem = Roles.Count == 0
            ? "No se han podido cargar los roles."
            : string.Empty;

        DetectRom();
        _ = PrefillPlayerAsync();
    }

    /// <summary>
    /// Offers the name this machine's player already has, so a new run goes out under the same
    /// person instead of whatever gets typed this time (§123).
    /// </summary>
    private async Task PrefillPlayerAsync()
    {
        try
        {
            if (PlayerName.Length == 0 && await _profiles.CurrentAsync() is { } profile)
            {
                PlayerName = profile.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido leer el perfil del jugador");
        }
    }

    /// <summary>The roles on offer, in the order the catalogue lists them.</summary>
    public System.Collections.ObjectModel.ObservableCollection<RoleChoiceViewModel> Roles { get; } = [];

    [ObservableProperty]
    private string _roleProblem = string.Empty;

    private void OnRoleChosen(RoleChoiceViewModel chosen)
    {
        foreach (var other in Roles.Where(r => !ReferenceEquals(r, chosen)))
        {
            other.Clear();
        }

        RoleId = chosen.Role.Id;
        CreateCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _runName = "Mi PermaLocke";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _playerName = string.Empty;

    /// <summary>Chosen role. Empty until one is picked, which is what keeps the button off.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _roleId = string.Empty;

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
            // Una sola run por jugador en el torneo: la primera la crea él; empezar de cero lo decide el organizador,
            // que la reinicia desde Admin. El servidor lo impone igualmente (tools/supabase/07-una-run.sql).
            if ((await _discord.CallAsync("puedo_crear_run"))?.Trim() != "true")
            {
                ErrorMessage = "Ya tienes una run en el torneo. Para empezar de cero, pide al organizador que te la reinicie.";
                return;
            }

            // El perfil antes que la run: la run nace ya con dueño y no hace falta vincularla luego.
            var profile = await _profiles.EnsureAsync(PlayerName.Trim());

            var run = await _runs.CreateAsync(new CreateRunRequest(
                RunName.Trim(),
                PlayerName.Trim(),
                RoleId.Trim(),
                GameVersion.UltraMoon,
                TitleId: _rom?.TitleId,
                PlayerId: profile.Id));

            _logger.LogInformation("Run creada: {Name} ({Seed}) para {Player}",
                run.Name, run.SeedLabel, run.PlayerName);

            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear la run");
            ErrorMessage = "No se pudo crear la run.";
        }
    }

    private bool CanCreate() =>
        RomIsValid
        && !string.IsNullOrWhiteSpace(RoleId)
        && !string.IsNullOrWhiteSpace(RunName)
        && !string.IsNullOrWhiteSpace(PlayerName);

    private void DetectRom()
    {
        var found = RomInspector.ScanFolder(_paths.Rom);

        if (found.Count == 0)
        {
            RomStatus = "No se encuentra tu ROM. Ponla en la carpeta ROM.";
            RomIsValid = false;
            return;
        }

        _rom = found.FirstOrDefault(r => r.IsSupported) ?? found[0];

        if (_rom.Game is null)
        {
            RomStatus = $"«{_rom.FileName}» no es Pokémon Ultra Luna.";
            RomIsValid = false;
            return;
        }

        if (!_rom.IsDecrypted)
        {
            RomStatus = $"«{_rom.FileName}» no sirve: tiene que estar desencriptada.";
            RomIsValid = false;
            return;
        }

        RomStatus =
            $"Pokémon Ultra Luna · {_rom.FileName}";
        RomIsValid = true;
        _logger.LogInformation("ROM válida detectada: {File} ({TitleId})", _rom.FileName, _rom.TitleId);
    }
}

/// <summary>One role on the creation screen, with its rules spelled out.</summary>
/// <remarks>
/// The effects line is generated from the role's own numbers rather than written by hand, so a
/// competition that edits <c>Data/roles.json</c> cannot end up with a screen that describes the
/// old rules.
/// </remarks>
public sealed partial class RoleChoiceViewModel(Role role, Action<RoleChoiceViewModel> chosen)
    : ObservableObject
{
    public Role Role { get; } = role;

    public string Name => Role.Name;

    public string Description => Role.Description;

    /// <summary>The numbers, in one line, so nobody has to take the description on trust.</summary>
    public string Effects
    {
        get
        {
            var parts = new List<string>
            {
                Role.Earn == 1 ? "puntos normales" : $"ganas ×{Role.Earn:0.##}",
                Role.Lose == 0 ? "no pierdes puntos" : Role.Lose == 1 ? "pierdes normal" : $"pierdes ×{Role.Lose:0.##}",
                $"enemigos +{Role.EnemyLevelPercent}%",
                $"tu cap +{Role.PlayerCapPercent}%"
            };

            if (Role.ExtraTrainerPokemon > 0)
            {
                parts.Add($"+{Role.ExtraTrainerPokemon} Pokémon en combates importantes");
            }

            if (Role.Roulette)
            {
                parts.Add("ruleta obligatoria tras cada hito");
            }

            return string.Join(" · ", parts);
        }
    }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            chosen(this);
        }
    }

    /// <summary>
    /// Unticks this one. Safe against re-entering the callback because only ticking calls it.
    /// </summary>
    public void Clear() => IsSelected = false;
}
