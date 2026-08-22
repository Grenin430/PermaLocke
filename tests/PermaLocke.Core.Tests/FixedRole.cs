using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>Stands in for the role of a run, so the point services can be exercised on each one.</summary>
internal sealed class FixedRole(Role? role) : IRunRoles
{
    /// <summary>No role at all: what a run whose id is not in the catalogue looks like.</summary>
    public static FixedRole None { get; } = new(null);

    public static Role Normal { get; } =
        new("normal", "NORMAL", "", "", 1.0, 1.0, 20, 20, 1);

    public static Role Cagoneta { get; } =
        new("cagoneta", "CAGONETA", "", "", 0.5, 0.0, 20, 20, 1);

    public static Role Experto { get; } =
        new("experto", "EXPERTO", "", "", 1.5, 2.0, 27, 20, 2);

    public Role? Of(Guid runId) => role;
}
