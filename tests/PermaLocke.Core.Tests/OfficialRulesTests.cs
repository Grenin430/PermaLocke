using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The admin's rules in <c>reglas/</c>, compared with and adopted over this machine's <c>Data/</c> (§123).
/// </summary>
public sealed class OfficialRulesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-reglas-{Guid.NewGuid():N}");
    private string Rules => Path.Combine(_root, "reglas");
    private string Data => Path.Combine(_root, "Data");
    private string Backups => Path.Combine(_root, "backup");

    public OfficialRulesTests()
    {
        Directory.CreateDirectory(Rules);
        Directory.CreateDirectory(Data);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Stands in for the application's loaders: anything that says "roto" does not load.</summary>
    private static string? Validate(string name, string path) =>
        File.ReadAllText(path).Contains("roto") ? "no se entiende" : null;

    private static readonly DateTimeOffset Now = new(2026, 9, 14, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Each_file_is_compared_and_only_rules_files_count()
    {
        File.WriteAllText(Path.Combine(Rules, "shop.json"), "{\"precio\":100}");
        File.WriteAllText(Path.Combine(Data, "shop.json"), "{\"precio\":100}");
        File.WriteAllText(Path.Combine(Rules, "rewards.json"), "{\"a\":2}");
        File.WriteAllText(Path.Combine(Data, "rewards.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(Rules, "grants.json"), "{}");
        File.WriteAllText(Path.Combine(Rules, "species.json"), "{}");
        File.WriteAllText(Path.Combine(Rules, "randomizer.json"), "{}");

        var statuses = OfficialRules.Compare(Rules, Data, Validate).ToDictionary(s => s.Name);

        Assert.Equal(RuleFileState.Same, statuses["shop.json"].State);
        Assert.Equal(RuleFileState.Different, statuses["rewards.json"].State);
        Assert.Equal(RuleFileState.New, statuses["grants.json"].State);
        Assert.Equal(RuleFileKind.NeedsRegeneration, statuses["randomizer.json"].Kind);

        // Lo que se saca del cartucho de cada uno no es una regla y no se reparte nunca.
        Assert.False(statuses.ContainsKey("species.json"));
    }

    [Fact]
    public void Adopting_copies_the_old_files_aside_first_and_reads_the_new_ones_back()
    {
        File.WriteAllText(Path.Combine(Rules, "rewards.json"), "{\"a\":2}");
        File.WriteAllText(Path.Combine(Data, "rewards.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(Rules, "grants.json"), "{}");

        var result = OfficialRules.Adopt(Rules, Data, Backups, Validate, Now);

        Assert.Empty(result.Problem);
        Assert.Equal(2, result.Adopted.Count);
        Assert.Equal("{\"a\":2}", File.ReadAllText(Path.Combine(Data, "rewards.json")));
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(result.Backup!, "rewards.json")));
        Assert.True(File.Exists(Path.Combine(Data, "grants.json")));

        var rewards = result.Adopted.Single(a => a.Name == "rewards.json");
        Assert.NotEqual(rewards.Before, rewards.After);
        Assert.Equal(string.Empty, result.Adopted.Single(a => a.Name == "grants.json").Before);

        Assert.All(OfficialRules.Compare(Rules, Data, Validate), s => Assert.Equal(RuleFileState.Same, s.State));
    }

    /// <summary>A half-adopted set of rules is worse than either set whole: one bad file stops them all.</summary>
    [Fact]
    public void One_file_that_does_not_load_stops_everything()
    {
        File.WriteAllText(Path.Combine(Rules, "rewards.json"), "{\"a\":2}");
        File.WriteAllText(Path.Combine(Data, "rewards.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(Rules, "shop.json"), "roto");

        var result = OfficialRules.Adopt(Rules, Data, Backups, Validate, Now);

        Assert.Empty(result.Adopted);
        Assert.Contains("shop.json", result.Problem);
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(Data, "rewards.json")));
        Assert.False(Directory.Exists(Backups));
    }

    [Fact]
    public void Nothing_to_adopt_is_said_and_touches_nothing()
    {
        File.WriteAllText(Path.Combine(Rules, "shop.json"), "{}");
        File.WriteAllText(Path.Combine(Data, "shop.json"), "{}");

        var result = OfficialRules.Adopt(Rules, Data, Backups, Validate, Now);

        Assert.Empty(result.Adopted);
        Assert.NotEmpty(result.Problem);
        Assert.False(Directory.Exists(Backups));
    }
}
