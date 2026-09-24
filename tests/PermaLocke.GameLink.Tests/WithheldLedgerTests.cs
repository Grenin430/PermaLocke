using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The withheld-balls ledger belongs to one run (§147). The version without an owner handed a brand new
/// game the previous run's twelve kinds of ball, a Master Ball among them.
/// </summary>
public sealed class WithheldLedgerTests : IDisposable
{
    private const int MasterBall = 1;
    private const int PokeBall = 4;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"permalocke-ledger-{Guid.NewGuid():N}");

    private string LedgerPath => Path.Combine(_folder, "objetos-retirados.txt");

    private WithheldLedger Ledger() => new(LedgerPath, NullLogger.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void A_run_is_owed_what_it_took()
    {
        var run = Guid.NewGuid();
        Ledger().Remember(run, PokeBall, 5);
        Ledger().Remember(run, MasterBall, 1);

        Assert.Equal(5, Ledger().Owed(run, PokeBall));
        Assert.Equal(1, Ledger().Owed(run, MasterBall));
    }

    [Fact]
    public void Another_run_is_owed_nothing()
    {
        Ledger().Remember(Guid.NewGuid(), MasterBall, 1);

        Assert.Equal(0, Ledger().Owed(Guid.NewGuid(), MasterBall));
    }

    /// <summary>A file from before the owner line existed owes nothing to anybody asking now.</summary>
    [Fact]
    public void A_ledger_without_an_owner_owes_nothing()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllLines(LedgerPath, ["1=1", "3=13", "4=3"]);

        Assert.Equal(0, Ledger().Owed(Guid.NewGuid(), PokeBall));
    }

    /// <summary>
    /// The other run's numbers are not thrown away: the first write of the current run moves them aside,
    /// dated, and starts its own.
    /// </summary>
    [Fact]
    public void Another_runs_ledger_is_set_aside_and_not_lost()
    {
        var old = Guid.NewGuid();
        var now = Guid.NewGuid();
        Ledger().Remember(old, MasterBall, 1);

        Ledger().Remember(now, PokeBall, 2);

        Assert.Equal(2, Ledger().Owed(now, PokeBall));
        Assert.Equal(0, Ledger().Owed(now, MasterBall));

        var aside = Assert.Single(Directory.GetFiles(_folder, "objetos-retirados-de-otra-run-*.txt"));
        Assert.Contains($"run={old:D}", File.ReadAllLines(aside));
        Assert.Contains("1=1", File.ReadAllLines(aside));
    }

    [Fact]
    public void Zero_forgets_the_item_and_keeps_the_rest()
    {
        var run = Guid.NewGuid();
        Ledger().Remember(run, PokeBall, 5);
        Ledger().Remember(run, MasterBall, 1);

        Ledger().Remember(run, PokeBall, 0);

        Assert.Equal(0, Ledger().Owed(run, PokeBall));
        Assert.Equal(1, Ledger().Owed(run, MasterBall));
        Assert.Empty(Directory.GetFiles(_folder, "*-de-otra-run-*"));
    }
}
