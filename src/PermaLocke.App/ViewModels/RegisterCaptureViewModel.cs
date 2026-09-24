using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.ViewModels;

/// <param name="Label">Rule verdict formatted for the panel, e.g. "Zona · Ruta 1".</param>
public sealed record DetailRow(string Label, string Value);

/// <summary>
/// Registers a capture. Evaluates the rules live as the form is filled in, so the player sees
/// the verdict before committing, and never silently bypasses a rule: forcing a blocked
/// capture is an explicit act that gets written to the history as a violation.
/// </summary>
public sealed partial class RegisterCaptureViewModel : ObservableObject
{
    private readonly EncounterService _encounters;
    private readonly IRunContext _runContext;
    private readonly ILogger<RegisterCaptureViewModel> _logger;

    public RegisterCaptureViewModel(
        EncounterService encounters,
        ISpeciesLookup species,
        IRunContext runContext,
        ILogger<RegisterCaptureViewModel> logger)
    {
        _encounters = encounters;
        _runContext = runContext;
        _logger = logger;

        Species = species.All;
        EncounterTypes = Enum.GetValues<EncounterType>();
        _ = LoadKnownLocationsAsync();
    }

    public IReadOnlyList<SpeciesInfo> Species { get; }

    public IReadOnlyList<EncounterType> EncounterTypes { get; }

    public ObservableCollection<string> KnownLocations { get; } = [];

    public ObservableCollection<DetailRow> Details { get; } = [];

    /// <summary>Raised once the capture is stored, so the window can close.</summary>
    public event EventHandler? Finished;

    private uint? _pid;

    /// <summary>The form of the detected Pokémon, and which species it belongs to (§140).</summary>
    private (int Species, int Form) _detectedForm;

    /// <summary>What the detected Pokémon knew, so the move reminder can offer it later (§142).</summary>
    private IReadOnlyList<int>? _detectedMoves;

    /// <summary>
    /// Fills the form from a Pokémon detected in the running game, zone included: every
    /// Pokémon records where it was met, which is the value the encounter rules need and is
    /// more reliable than the current map, since it stays correct even if registered later.
    /// </summary>
    public void PrefillFrom(LivePartyMember member)
    {
        SelectedSpecies = Species.FirstOrDefault(s => s.Number == member.Species);
        Level = member.Level;
        IsShiny = member.IsShiny;
        Nickname = string.Equals(member.Nickname, member.SpeciesName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : member.Nickname;
        _pid = member.Pid;
        _detectedForm = (member.Species, member.Form);
        _detectedMoves = member.Moves;
        LocationName = member.MetLocationName;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private SpeciesInfo? _selectedSpecies;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _locationName = string.Empty;

    [ObservableProperty]
    private EncounterType _encounterType = EncounterType.Wild;

    [ObservableProperty]
    private bool _isShiny;

    [ObservableProperty]
    private int _level;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    private string _verdictTitle = string.Empty;

    [ObservableProperty]
    private string _verdictMessage = "Elige especie y zona para comprobar las reglas.";

    [ObservableProperty]
    private RuleOutcome _outcome = RuleOutcome.Allowed;

    [ObservableProperty]
    private bool _isBlocked;

    /// <summary>Wording changes with the verdict so the consequence of the click is obvious.</summary>
    [ObservableProperty]
    private string _registerLabel = "REGISTRAR";

    [ObservableProperty]
    private string? _errorMessage;

    partial void OnSelectedSpeciesChanged(SpeciesInfo? value) => _ = PreviewAsync();

    partial void OnLocationNameChanged(string value) => _ = PreviewAsync();

    partial void OnEncounterTypeChanged(EncounterType value) => _ = PreviewAsync();

    partial void OnIsShinyChanged(bool value) => _ = PreviewAsync();

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (_runContext.Current is not { } run || SelectedSpecies is null)
        {
            return;
        }

        try
        {
            var result = await _encounters.RegisterAsync(run.Id, BuildRequest(force: IsBlocked), run.PlayerName);

            if (!result.Registered)
            {
                ErrorMessage = "La captura no se ha registrado.";
                return;
            }

            _logger.LogInformation("Captura registrada: {Species} en {Location} ({Outcome})",
                SelectedSpecies.Name, LocationName, result.Evaluation.Outcome);

            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al registrar la captura");
            ErrorMessage = "No se pudo registrar la captura.";
        }
    }

    private bool CanRegister() => SelectedSpecies is not null && !string.IsNullOrWhiteSpace(LocationName);

    private RegisterCaptureRequest BuildRequest(bool force) => new(
        SelectedSpecies!.Number,
        SelectedSpecies.Name,
        LocationName.Trim(),
        EncounterType,
        IsShiny,
        Level,
        Nickname,
        force,
        _pid,

        // La forma solo si la especie sigue siendo la detectada: si alguien la cambia a mano, la forma
        // de otra especie no significa nada.
        _detectedForm.Species == SelectedSpecies.Number ? _detectedForm.Form : 0,

        // Lo que sabía, con la misma condición: son los movimientos del Pokémon detectado, no de otro.
        _detectedForm.Species == SelectedSpecies.Number ? _detectedMoves : null);

    private async Task PreviewAsync()
    {
        Details.Clear();
        ErrorMessage = null;

        if (_runContext.Current is not { } run || SelectedSpecies is null
            || string.IsNullOrWhiteSpace(LocationName))
        {
            VerdictTitle = string.Empty;
            VerdictMessage = "Elige especie y zona para comprobar las reglas.";
            Outcome = RuleOutcome.Allowed;
            IsBlocked = false;
            RegisterLabel = "REGISTRAR";
            return;
        }

        var evaluation = await _encounters.PreviewAsync(run.Id, BuildRequest(force: false));

        Outcome = evaluation.Outcome;
        IsBlocked = evaluation.IsBlocked;

        var primary = evaluation.Primary;

        if (primary is null)
        {
            VerdictTitle = "CAPTURA PERMITIDA";
            VerdictMessage = $"{SelectedSpecies.Name} puede registrarse en {LocationName.Trim()}.";
            RegisterLabel = "REGISTRAR";
            return;
        }

        VerdictTitle = primary.Title;
        VerdictMessage = primary.Message;

        foreach (var (label, value) in primary.Details)
        {
            Details.Add(new DetailRow(label, value));
        }

        // Every other rule that had something to say, so nothing is hidden.
        foreach (var other in evaluation.Results.Where(r => r != primary))
        {
            Details.Add(new DetailRow(
                other.Overridden ? $"{other.Title} (levantada)" : other.Title,
                other.Message));
        }

        RegisterLabel = evaluation.Outcome switch
        {
            RuleOutcome.Blocked => "REGISTRAR IGUALMENTE · QUEDA COMO INFRACCIÓN",
            RuleOutcome.AllowedWithException => "REGISTRAR CON EXCEPCIÓN",
            RuleOutcome.Warning => "REGISTRAR DE TODOS MODOS",
            _ => "REGISTRAR"
        };
    }

    private async Task LoadKnownLocationsAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        foreach (var location in await _encounters.GetKnownLocationsAsync(run.Id))
        {
            KnownLocations.Add(location);
        }
    }
}
