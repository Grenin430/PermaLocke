using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Moves a run from one role to another, on purpose and on the record.
/// </summary>
/// <remarks>
/// <para>
/// A role is meant to be chosen once, before the ROM is randomized, because part of it is baked
/// into the cartridge. This exists anyway because the alternative was worse: the LUDÓPATA role
/// shipped with no way to reach it, so a run that wanted the wheel had to be started again from
/// nothing.
/// </para>
/// <para>
/// What it does <b>not</b> do is re-randomize. The trainer levels and the extra Pokémon already
/// written into the installed mod stay exactly as they were, so a run that moves to a harder role
/// keeps the world of the old one. The screen says so; hiding it would be selling a difficulty
/// change that only half happens.
/// </para>
/// <para>
/// The reason is mandatory, and it is mandatory in the domain, not here: a role change with no
/// explanation in the history is a run whose rules changed and nobody can say why.
/// </para>
/// </remarks>
public sealed partial class ChangeRoleViewModel : ObservableObject
{
    private readonly RunService _runs;
    private readonly IRunContext _runContext;
    private readonly ILogger<ChangeRoleViewModel> _logger;

    public ChangeRoleViewModel(RunService runs, IRoleCatalog roles, IRunContext runContext,
        ILogger<ChangeRoleViewModel> logger)
    {
        _runs = runs;
        _runContext = runContext;
        _logger = logger;

        Current = runContext.Current?.RoleId ?? string.Empty;

        foreach (var role in roles.All)
        {
            Roles.Add(new RoleChoiceViewModel(role, OnRoleChosen));
        }

        // El rol de ahora sale marcado, para que se vea de dónde se sale y no solo a dónde se va.
        foreach (var role in Roles.Where(r =>
                     string.Equals(r.Role.Id, Current, StringComparison.OrdinalIgnoreCase)))
        {
            role.IsSelected = true;
        }
    }

    public ObservableCollection<RoleChoiceViewModel> Roles { get; } = [];

    /// <summary>Role the run has right now, so the window can say what it is leaving.</summary>
    public string Current { get; }

    public string CurrentText => $"Ahora mismo la run es {Current.ToUpperInvariant()}.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _roleId = string.Empty;

    /// <summary>Why. Mandatory, because the history has to be able to explain itself later.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _problem = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    /// <summary>True when the window can close having done what it opened for.</summary>
    public bool Confirmed { get; private set; }

    public event EventHandler? Finished;

    public bool CanConfirm =>
        !IsWorking
        && !string.IsNullOrWhiteSpace(RoleId)
        && !string.IsNullOrWhiteSpace(Reason)
        && !string.Equals(RoleId, Current, StringComparison.OrdinalIgnoreCase);

    private void OnRoleChosen(RoleChoiceViewModel chosen)
    {
        foreach (var other in Roles.Where(r => !ReferenceEquals(r, chosen)))
        {
            other.Clear();
        }

        RoleId = chosen.Role.Id;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (_runContext.Current is not { } run)
        {
            Problem = "No hay ninguna run abierta.";
            return;
        }

        IsWorking = true;
        ConfirmCommand.NotifyCanExecuteChanged();

        try
        {
            await _runs.ChangeRoleAsync(run, RoleId, Reason.Trim());

            _logger.LogInformation("Rol cambiado de {From} a {To}: {Reason}", Current, RoleId, Reason);

            Confirmed = true;
            Finished?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el cambio de rol a {Role}", RoleId);
            Problem = "No se ha podido cambiar el rol.";
        }
        finally
        {
            IsWorking = false;
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancel() => Finished?.Invoke(this, EventArgs.Empty);
}
