using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <inheritdoc cref="IRunContext"/>
/// <remarks>
/// <see cref="CurrentChanged"/> is raised on the context the run context was made on (the UI thread in the apps): the
/// run services set the run after <c>ConfigureAwait(false)</c>, and listeners that touch the screen crashed with
/// «el subproceso que realiza la llamada no puede obtener acceso» (logs of 2026-09-28, start over and new run).
/// </remarks>
public sealed class RunContext : IRunContext
{
    private readonly SynchronizationContext? _owner = SynchronizationContext.Current;

    // Por hilo y no por contexto: WPF estrena un DispatcherSynchronizationContext en cada operación.
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;

    public Run? Current { get; private set; }

    public event EventHandler? CurrentChanged;

    public void SetCurrent(Run? run)
    {
        if (ReferenceEquals(Current, run))
        {
            return;
        }

        Current = run;

        if (_owner is null || Environment.CurrentManagedThreadId == _ownerThread)
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _owner.Post(_ => CurrentChanged?.Invoke(this, EventArgs.Empty), null);
        }
    }
}
