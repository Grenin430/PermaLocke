using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The friends list of JUGAR (§126): how a presence written on another machine is read, and that it travels
/// through the shared folder next to the rest of the player's files.
/// </summary>
public sealed class PresenceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 21, 0, 0, TimeSpan.FromHours(2));

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-presence-{Guid.NewGuid():N}");
    private readonly SnapshotStore _store = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PlayerPresence Written(PresenceState state, TimeSpan ago, TimeSpan? playing = null) => new()
    {
        PlayerId = Guid.NewGuid(),
        Name = "Misty",
        State = state,
        UpdatedAt = Now - ago,
        PlayingSince = playing is { } length ? Now - length : null
    };

    [Fact]
    public void A_player_nobody_has_heard_from_is_offline()
    {
        Assert.Equal(PresenceState.Offline, Presence.StateOf(null, Now));
        Assert.Equal("Desconectado", Presence.Say(null, Now));
    }

    [Fact]
    public void A_fresh_presence_says_what_it_says()
    {
        Assert.Equal(PresenceState.Playing, Presence.StateOf(Written(PresenceState.Playing, TimeSpan.FromSeconds(30)), Now));
        Assert.Equal(PresenceState.InApp, Presence.StateOf(Written(PresenceState.InApp, TimeSpan.FromMinutes(2)), Now));
        Assert.Equal("En la app", Presence.Say(Written(PresenceState.InApp, TimeSpan.FromMinutes(2)), Now));
    }

    /// <summary>
    /// An application that crashes or loses power never writes «desconectado». Without the age limit that player
    /// would read as playing for ever.
    /// </summary>
    [Fact]
    public void A_presence_older_than_the_limit_is_offline_whatever_it_says()
    {
        var stale = Written(PresenceState.Playing, Presence.StaleAfter + TimeSpan.FromSeconds(1));

        Assert.Equal(PresenceState.Offline, Presence.StateOf(stale, Now));
        Assert.StartsWith("Desconectado · hace", Presence.Say(stale, Now));
    }

    [Fact]
    public void The_limit_leaves_room_for_several_heartbeats_to_be_late()
    {
        Assert.True(Presence.StaleAfter >= 3 * Presence.HeartbeatEvery);
    }

    [Fact]
    public void Playing_says_for_how_long_when_it_knows()
    {
        Assert.Equal("Jugando a Ultra Luna",
            Presence.Say(Written(PresenceState.Playing, TimeSpan.FromSeconds(10)), Now));
        Assert.StartsWith("Jugando a Ultra Luna · ",
            Presence.Say(Written(PresenceState.Playing, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(12)), Now));
    }

    [Fact]
    public void Offline_says_since_when_in_the_unit_that_reads()
    {
        Assert.Equal("Desconectado · hace 1 min", Presence.Say(Written(PresenceState.Offline, TimeSpan.FromSeconds(20)), Now));
        Assert.Equal("Desconectado · hace 42 min", Presence.Say(Written(PresenceState.Offline, TimeSpan.FromMinutes(42)), Now));
        Assert.Equal("Desconectado · hace 5 h", Presence.Say(Written(PresenceState.Offline, TimeSpan.FromHours(5.5)), Now));
        Assert.Equal("Desconectado · hace 3 días", Presence.Say(Written(PresenceState.Offline, TimeSpan.FromDays(3)), Now));
    }

    [Fact]
    public void A_presence_written_to_the_folder_reads_back_with_its_player()
    {
        var profile = new PlayerProfile { Id = Guid.NewGuid(), Name = "Grenin" };
        var presence = new PlayerPresence
        {
            PlayerId = profile.Id,
            Name = profile.Name,
            State = PresenceState.Playing,
            UpdatedAt = Now,
            PlayingSince = Now.AddMinutes(-7)
        };

        _store.WritePresence(_root, profile, presence);

        var read = Assert.Single(_store.ReadPlayers(_root));

        Assert.Equal(profile.Id, read.Profile.Id);
        Assert.Equal(presence, read.Presence);
        Assert.Null(read.Snapshot);
    }

    /// <summary>The heartbeat rewrites the presence and must not leave a second folder for the same player.</summary>
    [Fact]
    public void Writing_again_updates_the_same_folder()
    {
        var profile = new PlayerProfile { Id = Guid.NewGuid(), Name = "Grenin" };

        _store.WritePresence(_root, profile, new PlayerPresence { PlayerId = profile.Id, Name = profile.Name, State = PresenceState.InApp, UpdatedAt = Now });
        _store.WritePresence(_root, profile, new PlayerPresence { PlayerId = profile.Id, Name = profile.Name, State = PresenceState.Offline, UpdatedAt = Now.AddMinutes(1) });

        var read = Assert.Single(_store.ReadPlayers(_root));
        Assert.Equal(PresenceState.Offline, read.Presence!.State);
    }

    [Fact]
    public void A_folder_without_a_profile_is_not_a_player()
    {
        Directory.CreateDirectory(Path.Combine(CompetitionLayout.Players(_root), "basura"));

        Assert.Empty(_store.ReadPlayers(_root));
    }

    [Fact]
    public void An_unreadable_presence_reads_as_unknown_and_keeps_the_player()
    {
        var profile = new PlayerProfile { Id = Guid.NewGuid(), Name = "Grenin" };
        _store.WritePresence(_root, profile, new PlayerPresence { PlayerId = profile.Id, Name = profile.Name, UpdatedAt = Now });

        var folder = Assert.Single(_store.ReadPlayers(_root)).Folder;
        File.WriteAllText(Path.Combine(folder, CompetitionLayout.PresenceFile), "{ esto no es json");

        var read = Assert.Single(_store.ReadPlayers(_root));
        Assert.Null(read.Presence);
        Assert.Equal(PresenceState.Offline, Presence.StateOf(read.Presence, Now));
    }
}
