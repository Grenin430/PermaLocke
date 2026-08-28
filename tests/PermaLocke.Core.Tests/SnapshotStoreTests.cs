using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The shared folder is the whole transport, and everything in it was written by somebody else.
/// </summary>
public sealed class SnapshotStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(),
        "permalocke-sync-" + Guid.NewGuid().ToString("N"));

    private readonly SnapshotStore _store = new();

    private static RunSnapshot Snapshot(string player, int points, Guid? id = null) => new()
    {
        RunId = id ?? Guid.NewGuid(),
        PlayerName = player,
        RunName = "PRUEBA",
        Points = points,
        PublishedAt = DateTimeOffset.UnixEpoch
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Publishes_and_reads_back_what_it_wrote()
    {
        _store.Publish(_folder, Snapshot("Grenin", 75));

        var read = _store.ReadAll(_folder);

        Assert.Single(read);
        Assert.Equal("Grenin", read[0].Snapshot!.PlayerName);
        Assert.Equal(75, read[0].Snapshot!.Points);
    }

    /// <summary>
    /// Publishing twice must replace, not pile up. The file is keyed on the run id and not on the
    /// player name: two people called Ash would otherwise overwrite each other.
    /// </summary>
    [Fact]
    public void Publishing_the_same_run_twice_leaves_one_file()
    {
        var id = Guid.NewGuid();

        _store.Publish(_folder, Snapshot("Grenin", 75, id));
        _store.Publish(_folder, Snapshot("Grenin", 120, id));

        var read = _store.ReadAll(_folder);

        Assert.Single(read);
        Assert.Equal(120, read[0].Snapshot!.Points);
    }

    [Fact]
    public void Two_players_are_two_files()
    {
        _store.Publish(_folder, Snapshot("Grenin", 75));
        _store.Publish(_folder, Snapshot("Bxnny", 200));

        Assert.Equal(2, _store.ReadAll(_folder).Count);
    }

    /// <summary>
    /// The one that matters. A folder people can open will eventually have junk in it, and one bad
    /// file must never hide the good ones or take the screen down.
    /// </summary>
    [Fact]
    public void A_broken_file_is_named_and_the_others_still_load()
    {
        _store.Publish(_folder, Snapshot("Grenin", 75));
        File.WriteAllText(Path.Combine(_folder, "permalocke-roto.json"), "{ esto no es json");

        var read = _store.ReadAll(_folder);

        Assert.Equal(2, read.Count);
        Assert.Single(read, r => r.Snapshot is not null);

        var broken = read.Single(r => r.Snapshot is null);
        Assert.Equal("permalocke-roto.json", broken.File);
        Assert.NotEmpty(broken.Problem);
    }

    /// <summary>Anything that is not one of ours is not even opened.</summary>
    [Fact]
    public void Ignores_files_that_are_not_snapshots()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "notas.txt"), "la lista de la compra");
        File.WriteAllText(Path.Combine(_folder, "otra-cosa.json"), "{}");

        Assert.Empty(_store.ReadAll(_folder));
    }

    /// <summary>
    /// A snapshot from a newer PermaLocke is refused with an explanation, not read half-wrong.
    /// </summary>
    [Fact]
    public void Refuses_a_snapshot_from_a_newer_version()
    {
        _store.Publish(_folder, Snapshot("Futuro", 999) with { Schema = RunSnapshot.CurrentSchema + 1 });

        var read = _store.ReadAll(_folder).Single();

        Assert.Null(read.Snapshot);
        Assert.Contains("más nueva", read.Problem);
    }

    /// <summary>A snapshot with no player behind it is not a standing, it is a fragment.</summary>
    [Fact]
    public void Refuses_a_snapshot_with_no_player()
    {
        _store.Publish(_folder, Snapshot("", 75));

        var read = _store.ReadAll(_folder).Single();

        Assert.Null(read.Snapshot);
        Assert.Contains("jugador", read.Problem);
    }

    [Fact]
    public void A_folder_that_is_not_there_is_empty_and_not_an_error()
    {
        Assert.Empty(_store.ReadAll(Path.Combine(_folder, "no-existe")));
        Assert.Empty(_store.ReadAll(""));
    }

    /// <summary>
    /// Written to a temporary name and moved into place: the folder is being synchronised by
    /// another program, and writing in place lets Drive upload a half-written file.
    /// </summary>
    [Fact]
    public void Leaves_no_temporary_file_behind()
    {
        _store.Publish(_folder, Snapshot("Grenin", 75));

        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }
}
