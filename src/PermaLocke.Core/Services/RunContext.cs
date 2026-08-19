using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <inheritdoc cref="IRunContext"/>
public sealed class RunContext : IRunContext
{
    public Run? Current { get; private set; }

    public event EventHandler? CurrentChanged;

    public void SetCurrent(Run? run)
    {
        if (ReferenceEquals(Current, run))
        {
            return;
        }

        Current = run;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}
