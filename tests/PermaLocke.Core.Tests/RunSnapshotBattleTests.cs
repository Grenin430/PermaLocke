using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Whether a published run can link-battle travels in the snapshot, and has to survive the round
/// trip through the shared folder in all three states.
/// </summary>
/// <remarks>
/// The third state is the one worth testing. <c>null</c> means <b>not known</b> — a run randomized
/// before this was recorded — and it must never be read back as "cannot battle", which would be a
/// verdict nobody measured.
/// </remarks>
public sealed class RunSnapshotBattleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "permalocke-battle-" + Guid.NewGuid().ToString("N"));

    private readonly SnapshotStore _store = new();

    private static RunSnapshot Snapshot(string player, bool? ready) => new()
    {
        RunId = Guid.NewGuid(),
        PlayerName = player,
        RunName = "PRUEBA",
        BattleReady = ready,
        PublishedAt = DateTimeOffset.UnixEpoch
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Survives_the_round_trip_in_all_three_states(bool? ready)
    {
        _store.Publish(_folder, Snapshot("Grenin", ready));

        Assert.Equal(ready, _store.ReadAll(_folder).Single().Snapshot!.BattleReady);
    }

    /// <summary>
    /// A snapshot written by an older build has no such field at all. It must come back as unknown,
    /// not as false.
    /// </summary>
    [Fact]
    public void A_snapshot_without_the_field_reads_back_as_unknown()
    {
        Directory.CreateDirectory(_folder);

        File.WriteAllText(Path.Combine(_folder, "permalocke-viejo.json"), """
            {
              "schema": 1,
              "runId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "playerName": "Antiguo",
              "runName": "DE ANTES",
              "points": 120
            }
            """);

        var read = _store.ReadAll(_folder).Single();

        Assert.NotNull(read.Snapshot);
        Assert.Null(read.Snapshot.BattleReady);
        Assert.Equal(120, read.Snapshot.Points);
    }

    /// <summary>
    /// The schema stayed at 1 when the field was added, and that is load-bearing: a reader refuses
    /// anything from a newer schema, so bumping it would have made every friend on an older build
    /// reject every new snapshot and see an empty scoreboard.
    /// </summary>
    [Fact]
    public void Adding_the_field_did_not_bump_the_schema()
    {
        Assert.Equal(1, RunSnapshot.CurrentSchema);

        _store.Publish(_folder, Snapshot("Grenin", true));

        Assert.Equal(1, _store.ReadAll(_folder).Single().Snapshot!.Schema);
    }
}
