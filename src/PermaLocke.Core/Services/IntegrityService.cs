using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>The <c>tipo</c> of an <see cref="GameEventType.IntegrityFlag"/>.</summary>
public static class IntegrityKinds
{
    /// <summary>A save state of the emulator appeared while playing.</summary>
    public const string SaveState = "estado";

    /// <summary>The save's play time went up while PermaLocke was not watching.</summary>
    public const string PlayedOutside = "fuera";

    /// <summary>The save's play time went down: restored from a copy.</summary>
    public const string Rewound = "retrocedida";

    /// <summary>The emulator was closed, not crashed, in the middle of a battle.</summary>
    public const string BattleAbandoned = "abandono";

    /// <summary>A Pokémon the run holds as fallen entered a battle with HP: the game healed it just before (1.0.4.4).</summary>
    public const string FallenInBattle = "caidoEnCombate";

    /// <summary>
    /// A Pokémon's ability or nature changed in the game and later went back to what it was (2026-09-28): an Ability
    /// Capsule, or anything else, used and then undone by reloading without saving, to try again.
    /// </summary>
    public const string Reroll = "repeticion";

    /// <summary>The save had the Roto Loto unlocked; PermaLocke turned it off before opening the game (2026-09-28).</summary>
    public const string RotoLoto = "rotombola";
}

/// <summary>
/// Writes down what looks like getting round the rules by reloading (2026-09-26), for the organiser to read in Admin.
/// </summary>
/// <remarks>
/// It never punishes: no points, no Pokémon. A crash of the emulator is not a flag, and deciding what a flag means is the
/// organiser's job. The player's own screens leave these events out; the history keeps them, because it is uploaded
/// and the organiser reads it there.
/// </remarks>
public sealed class IntegrityService(IEventStore events, IClock clock)
{
    /// <summary>Play time the save may gain or lose without it meaning anything: the game counts in whole seconds.</summary>
    public static readonly TimeSpan PlaytimeTolerance = TimeSpan.FromMinutes(1);

    public Task FlagAsync(Guid runId, string kind, string description,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var all = new Dictionary<string, string>(data ?? new Dictionary<string, string>()) { ["tipo"] = kind };

        return events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Timestamp = clock.Now,
            Type = GameEventType.IntegrityFlag,
            Source = EventSource.System,
            Actor = "PermaLocke",
            Description = description,
            Data = all
        }, ct);
    }

    /// <summary>
    /// What the save's play time says about the time between the last session PermaLocke watched and now.
    /// </summary>
    /// <returns>Null when nothing happened; otherwise the kind and by how much.</returns>
    public static (string Kind, TimeSpan Gap)? ComparePlaytime(TimeSpan lastSeen, TimeSpan now)
    {
        var gap = now - lastSeen;

        return gap > PlaytimeTolerance ? (IntegrityKinds.PlayedOutside, gap)
            : gap < -PlaytimeTolerance ? (IntegrityKinds.Rewound, -gap)
            : null;
    }

    /// <summary>How the emulator ended, in words, from its exit code; null for a crash, which is not a flag.</summary>
    /// <param name="askedByPermaLocke">It went because the player pressed CERRAR in PermaLocke.</param>
    public static string? HowClosed(uint exitCode, bool askedByPermaLocke) =>
        askedByPermaLocke ? "cerrado desde el botón CERRAR de PermaLocke"
        : exitCode switch
        {
            0 => "cerrado desde su ventana o su menú",
            1 => "cerrado a la fuerza (Administrador de tareas)",
            0xC000013A => "cerrado desde la consola",
            _ => null
        };
}
