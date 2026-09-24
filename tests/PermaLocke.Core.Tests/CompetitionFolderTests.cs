using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// One folder per player inside the shared one (§123): where things are written, that a history
/// survives the trip through a file and still verifies, and that the old layout still reads.
/// </summary>
public sealed class CompetitionFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-comp-{Guid.NewGuid():N}");
    private readonly SnapshotStore _store = new();

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PlayerProfile Profile(string name = "Grenin") => new() { Id = Guid.NewGuid(), Name = name };

    private static RunSnapshot SnapshotOf(PlayerProfile profile, Guid runId, IReadOnlyList<GameEvent> events) => new()
    {
        RunId = runId,
        PlayerId = profile.Id,
        PlayerName = profile.Name,
        RunName = "PRUEBA",
        Points = events.Sum(e => e.PointsDelta),
        EventCount = events.Count,
        ChainHead = events.Count > 0 ? events[^1].Hash : string.Empty,
        PublishedAt = DateTimeOffset.UnixEpoch
    };

    /// <summary>
    /// The one that matters most, over the real database: events with everything a real event carries
    /// — local offsets, a seed, a Pokémon, a data dictionary — are published as JSON, read back and
    /// verified. If the file lost one tick of a timestamp or reordered a key, every honest player would
    /// read as tampered.
    /// </summary>
    [Fact]
    public async Task A_real_history_survives_the_folder_and_still_checks_out()
    {
        var profile = Profile();
        var runId = Guid.NewGuid();
        var events = new SqliteEventStore(Path.Combine(_root, "permalocke.db"));

        await events.AppendAsync(Event(runId, 100, new DateTimeOffset(2026, 9, 14, 20, 15, 3, 123, TimeSpan.FromHours(2))));
        await events.AppendAsync(Event(runId, -25, DateTimeOffset.Now) with
        {
            PokemonId = Guid.NewGuid(),
            Seed = ulong.MaxValue,
            LocationId = "ruta-1",
            Reason = "caído «MUERTO» en la Ruta 1",
            Data = new Dictionary<string, string> { ["zeta"] = "1", ["alfa"] = "ñ" }
        });

        var chain = await events.GetAllAsync(runId);
        events.Dispose();

        _store.PublishPlayer(_root, profile, SnapshotOf(profile, runId, chain),
            new RunHistory { RunId = runId, PlayerId = profile.Id, Events = chain });

        var read = Assert.Single(_store.ReadAll(_root));

        Assert.NotNull(read.History);
        Assert.False(read.IsLegacy);
        Assert.Equal(AuditVerdict.Consistent, SnapshotAudit.Check(read.Snapshot!, read.History).Verdict);
    }

    private static GameEvent Event(Guid runId, int delta, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Timestamp = at,
        Type = GameEventType.PointsEarned,
        Source = EventSource.System,
        Actor = "Grenin",
        Description = "prueba",
        PointsDelta = delta
    };

    [Fact]
    public void Each_player_writes_only_inside_their_own_folder()
    {
        var profile = Profile();
        var runId = Guid.NewGuid();

        var folder = _store.PublishPlayer(_root, profile, SnapshotOf(profile, runId, []),
            new RunHistory { RunId = runId });

        Assert.Equal(Path.Combine(_root, "jugadores", $"Grenin-{profile.ShortId}"), folder);
        Assert.True(File.Exists(Path.Combine(folder, "perfil.json")));
        Assert.True(File.Exists(Path.Combine(folder, "run-activa.json")));
        Assert.True(File.Exists(Path.Combine(folder, "historial.json")));
        Assert.Equal(["jugadores"], Directory.EnumerateFileSystemEntries(_root).Select(Path.GetFileName));
    }

    /// <summary>Renamed, found by id, and the folder follows: no second folder for the same person.</summary>
    [Fact]
    public void A_renamed_player_keeps_one_folder()
    {
        var profile = Profile();
        var runId = Guid.NewGuid();

        _store.PublishPlayer(_root, profile, SnapshotOf(profile, runId, []), new RunHistory { RunId = runId });
        var renamed = profile with { Name = "Grenin430" };
        var folder = _store.PublishPlayer(_root, renamed, SnapshotOf(renamed, runId, []), new RunHistory { RunId = runId });

        var only = Assert.Single(Directory.EnumerateDirectories(Path.Combine(_root, "jugadores")));
        Assert.Equal(folder, only);
        Assert.EndsWith($"Grenin430-{profile.ShortId}", only);
    }

    /// <summary>Starting again replaces the run in the same folder: one player is one row.</summary>
    [Fact]
    public void Starting_again_replaces_the_run_instead_of_adding_a_row()
    {
        var profile = Profile();

        _store.PublishPlayer(_root, profile, SnapshotOf(profile, Guid.NewGuid(), []), new RunHistory { RunId = Guid.NewGuid() });
        var second = Guid.NewGuid();
        _store.PublishPlayer(_root, profile, SnapshotOf(profile, second, []), new RunHistory { RunId = second });

        Assert.Equal(second, Assert.Single(_store.ReadAll(_root)).Snapshot!.RunId);
    }

    [Fact]
    public void The_old_loose_snapshot_still_reads_and_is_not_counted_twice()
    {
        var profile = Profile();
        var runId = Guid.NewGuid();
        var snapshot = SnapshotOf(profile, runId, []);

        _store.Publish(_root, snapshot);
        _store.Publish(_root, SnapshotOf(Profile("Antiguo"), Guid.NewGuid(), []));
        _store.PublishPlayer(_root, profile, snapshot, new RunHistory { RunId = runId });

        var read = _store.ReadAll(_root);

        Assert.Equal(2, read.Count);
        Assert.False(read.Single(r => r.Snapshot!.RunId == runId).IsLegacy);
        Assert.True(read.Single(r => r.Snapshot!.RunId != runId).IsLegacy);

        Assert.True(_store.RemoveLegacy(_root, runId));
        Assert.False(_store.RemoveLegacy(_root, runId));
    }

    [Fact]
    public void A_broken_history_is_said_by_name_and_the_row_still_shows()
    {
        var profile = Profile();
        var runId = Guid.NewGuid();
        var folder = _store.PublishPlayer(_root, profile, SnapshotOf(profile, runId, []), new RunHistory { RunId = runId });

        File.WriteAllText(Path.Combine(folder, "historial.json"), "{ esto no es json");

        var read = Assert.Single(_store.ReadAll(_root));
        Assert.NotNull(read.Snapshot);
        Assert.Null(read.History);
        Assert.NotEmpty(read.HistoryProblem);
    }

    [Fact]
    public void A_player_who_has_not_published_is_not_a_problem()
    {
        Directory.CreateDirectory(Path.Combine(_root, "jugadores", "Nuevo-12345678"));
        File.WriteAllText(Path.Combine(_root, "jugadores", "Nuevo-12345678", "perfil.json"), "{}");

        Assert.Empty(_store.ReadAll(_root));
    }

    [Theory]
    [InlineData("Grenin", "Grenin")]
    [InlineData("José María", "José_María")]
    [InlineData("a/b\\c:d", "a_b_c_d")]
    [InlineData("???", "jugador")]
    public void Folder_names_are_safe_everywhere(string name, string expected) =>
        Assert.Equal(expected, CompetitionLayout.SafeName(name));

    [Fact]
    public async Task The_seen_marks_round_trip()
    {
        var marks = new SeenMarksStore(_root);
        var run = Guid.NewGuid();

        Assert.Empty(marks.Load());
        marks.Save(new Dictionary<Guid, SeenMark> { [run] = new(12, "abc") });

        Assert.Equal(new SeenMark(12, "abc"), marks.Load()[run]);

        await File.WriteAllTextAsync(Path.Combine(_root, SeenMarksStore.FileName), "roto");
        Assert.Empty(marks.Load());
    }
}
