namespace PermaLocke.Core.Domain;

/// <summary>What a player is doing, as a friends list says it.</summary>
/// <remarks>The numbers are written to the shared folder, so they only ever grow at the end.</remarks>
public enum PresenceState
{
    /// <summary>Grey: the application is closed, or has not been heard from.</summary>
    Offline = 0,

    /// <summary>Blue: PermaLocke is open and the game is not.</summary>
    InApp = 1,

    /// <summary>Green: the game is open.</summary>
    Playing = 2,
}

/// <summary>
/// One player's presence, as <c>jugadores/&lt;player&gt;/presencia.json</c> in the shared folder (§126).
/// </summary>
/// <remarks>
/// <para>
/// Written by that player's application every few seconds' worth of heartbeat and whenever the game opens
/// or closes, and set to <see cref="PresenceState.Offline"/> when the application closes. A folder
/// synchronised by Drive is not a live connection, so what matters is how old the file is: see
/// <see cref="Presence.StateOf"/>.
/// </para>
/// </remarks>
public sealed record PlayerPresence
{
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    public required Guid PlayerId { get; init; }

    public required string Name { get; init; }

    public PresenceState State { get; init; }

    /// <summary>When this was written, by the clock of whoever wrote it.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>When the game was opened, while <see cref="State"/> is <see cref="PresenceState.Playing"/>.</summary>
    public DateTimeOffset? PlayingSince { get; init; }
}

/// <summary>Reading presences written by other machines.</summary>
public static class Presence
{
    /// <summary>How often a running application writes its presence.</summary>
    public static readonly TimeSpan HeartbeatEvery = TimeSpan.FromSeconds(45);

    /// <summary>
    /// How old a presence may be before its player counts as gone.
    /// </summary>
    /// <remarks>
    /// Four heartbeats, because the file travels through Drive or Dropbox and those take their time: a minute
    /// either side is normal. An application that crashes cannot write «desconectado», so without this a player
    /// who lost power would read as playing for ever.
    /// </remarks>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);

    public static PresenceState StateOf(PlayerPresence? presence, DateTimeOffset now) =>
        presence is null || now - presence.UpdatedAt > StaleAfter ? PresenceState.Offline : presence.State;

    /// <summary>The line under a friend's name: what they are doing, or since when they are gone.</summary>
    public static string Say(PlayerPresence? presence, DateTimeOffset now)
    {
        switch (StateOf(presence, now))
        {
            case PresenceState.Playing:
                return presence!.PlayingSince is { } since
                    ? $"Jugando a Ultra Luna · {Playtime.Say(now - since)}"
                    : "Jugando a Ultra Luna";

            case PresenceState.InApp:
                return "En la app";

            default:
                if (presence is null)
                {
                    return "Desconectado";
                }

                var gone = now - presence.UpdatedAt;
                return gone switch
                {
                    { TotalMinutes: < 60 } => $"Desconectado · hace {Math.Max(1, (int)gone.TotalMinutes)} min",
                    { TotalHours: < 24 } => $"Desconectado · hace {(int)gone.TotalHours} h",
                    _ => $"Desconectado · hace {(int)gone.TotalDays} días"
                };
        }
    }
}
