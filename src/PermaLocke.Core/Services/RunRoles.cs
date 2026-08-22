using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>The role a given run is being played with.</summary>
/// <remarks>
/// A port of its own, small on purpose: the services that hand out or take away points need the
/// role and nothing else about the run, and asking them to carry a whole <see cref="Run"/> around
/// would spread the run through the domain for one field.
/// </remarks>
public interface IRunRoles
{
    /// <summary>The role of a run, or null when it cannot be resolved.</summary>
    Role? Of(Guid runId);
}

/// <summary>Resolves the role from the run the app currently has loaded.</summary>
/// <remarks>
/// Null when the run is not the loaded one, or when its role id is not in the catalogue. Null and
/// not a guess: whoever asks has to decide what to do without one, and every one of them writes
/// down that it could not be resolved rather than paying an invented rate.
/// </remarks>
public sealed class RunRoles(IRunContext runs, IRoleCatalog roles) : IRunRoles
{
    public Role? Of(Guid runId) =>
        runs.Current is { } run && run.Id == runId ? roles.Find(run.RoleId) : null;
}

/// <summary>
/// How a role changed a number, kept together so the history can show its working.
/// </summary>
/// <param name="Base">What the rule says before the role touches it.</param>
/// <param name="Final">What is actually earned or charged.</param>
/// <param name="RoleId">The role that did it, or "desconocido" when it could not be resolved.</param>
public readonly record struct RoleAdjusted(int Base, int Final, string RoleId, double Multiplier)
{
    /// <summary>True when the role moved the number, so it is worth explaining.</summary>
    public bool Changed => Final != Base;

    /// <summary>The three columns any event should carry, so nobody has to recompute this.</summary>
    public void Describe(IDictionary<string, string> data)
    {
        data["base"] = Base.ToString();
        data["rol"] = RoleId;
        data["multiplicador"] = Multiplier.ToString("0.##");
    }

    /// <summary>"100 × 0,5 = 50 (cagoneta)", for a description a player can check.</summary>
    public string Explain() =>
        Changed ? $" ({Base} × {Multiplier:0.##} por el rol {RoleId})" : string.Empty;

    /// <summary>Applies a role to a reward, or leaves it alone when there is no role.</summary>
    public static RoleAdjusted Reward(Role? role, int amount) => role is null
        ? new RoleAdjusted(amount, amount, "desconocido", 1)
        : new RoleAdjusted(amount, role.Reward(amount), role.Id, role.Earn);

    /// <summary>Applies a role to a penalty, or leaves it alone when there is no role.</summary>
    public static RoleAdjusted Penalty(Role? role, int amount) => role is null
        ? new RoleAdjusted(amount, amount, "desconocido", 1)
        : new RoleAdjusted(amount, role.Penalty(amount), role.Id, role.Lose);
}
