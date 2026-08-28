using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Reading Data/rewards.json.
/// </summary>
/// <remarks>
/// The reason this file has tests at all: a flag the parser silently ignores turns a working
/// feature off without failing anywhere. That is the shape of bug the automatic prize is most
/// exposed to, because nothing complains -- the prize simply never arrives.
/// </remarks>
public sealed class JsonRewardCatalogTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"permalocke-rewards-{Guid.NewGuid():N}.json");

    private void Given(string json) => File.WriteAllText(_path, json);

    [Fact]
    public void The_automatic_flag_is_read()
    {
        Given("""
        {
          "rewards": [
            {
              "id": "primeras-balls",
              "name": "Refuerzo",
              "objetosEnMochila": [4],
              "automatico": true,
              "items": [ { "id": 3, "name": "Super Ball", "amount": 10 } ]
            }
          ]
        }
        """);

        var reward = Assert.Single(JsonRewardCatalog.Load(_path).All);

        Assert.True(reward.Automatic);
        Assert.Equal([4], reward.HeldItems);
        Assert.Equal(1, reward.Conditions);
    }

    /// <summary>Left out means manual, which is the safe default: nothing is given unasked.</summary>
    [Fact]
    public void Without_the_flag_a_prize_waits_for_its_button()
    {
        Given("""
        {
          "rewards": [
            {
              "id": "doce-pruebas",
              "name": "Premio",
              "achievements": ["prueba-01"],
              "items": [ { "id": 25, "name": "Hiperpoción", "amount": 12 } ]
            }
          ]
        }
        """);

        Assert.False(Assert.Single(JsonRewardCatalog.Load(_path).All).Automatic);
    }

    /// <summary>The real file the application ships, so a typo in it fails here and not in a run.</summary>
    [Fact]
    public void The_shipped_file_still_has_an_automatic_prize_for_the_first_balls()
    {
        var data = Path.Combine(Repository(), "Data", "rewards.json");
        Assert.True(File.Exists(data), $"No está {data}");

        var balls = Assert.Single(JsonRewardCatalog.Load(data).All, r => r.Id == "primeras-balls");

        Assert.True(balls.Automatic);
        Assert.Equal([4], balls.HeldItems);
        Assert.Equal(3, Assert.Single(balls.Items).Id);
        Assert.Equal("decente", Assert.Single(balls.Credits));
    }

    /// <summary>A prize that only hands out rolls is valid: what it gives need not be an item.</summary>
    [Fact]
    public void A_prize_of_only_free_rolls_is_kept()
    {
        Given("""
        {
          "rewards": [
            {
              "id": "solo-tiradas",
              "name": "Tiradas",
              "objetosEnMochila": [4],
              "tiradasGratis": ["decente", "decente"]
            }
          ]
        }
        """);

        var reward = Assert.Single(JsonRewardCatalog.Load(_path).All);

        Assert.Empty(reward.Items);
        Assert.Equal(2, reward.Credits.Count);
    }

    /// <summary>But one that gives nothing at all is still dropped: a button that lies.</summary>
    [Fact]
    public void A_prize_that_gives_nothing_is_dropped()
    {
        Given("""
        {
          "rewards": [
            { "id": "nada", "name": "Nada", "objetosEnMochila": [4] }
          ]
        }
        """);

        Assert.Empty(JsonRewardCatalog.Load(_path).All);
    }

    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
