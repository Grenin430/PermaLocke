using PermaLocke.Rules;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The cap table, and the tie between a stage and the achievement that clears it.
/// </summary>
/// <remarks>
/// That tie is what turned the cap from something the player had to remember to press into
/// something the run works out on its own, so it is worth a test of its own: a stage that loses
/// its achievement goes quietly back to needing a button.
/// </remarks>
public sealed class LevelCapStageTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"permalocke-caps-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        GC.SuppressFinalize(this);
    }

    private LevelCapTable Load(string json)
    {
        File.WriteAllText(_path, json);
        return LevelCapTable.Load(_path);
    }

    /// <summary>The cap in force is the stage about to be faced, not the one just cleared.</summary>
    [Theory]
    [InlineData(0, 14)]
    [InlineData(1, 20)]
    [InlineData(2, 24)]
    public void The_cap_is_the_stage_ahead(int cleared, int expected)
    {
        var table = Load("""
            {
              "caps": [
                { "id": "trial-01", "order": 1, "name": "1ª", "level": 14, "logro": "prueba-01" },
                { "id": "trial-02", "order": 2, "name": "2ª", "level": 20, "logro": "prueba-02" },
                { "id": "trial-03", "order": 3, "name": "3ª", "level": 24, "logro": "prueba-03" }
              ]
            }
            """);

        Assert.Equal(expected, table.CapFor(cleared));
    }

    [Fact]
    public void Each_stage_carries_the_achievement_that_clears_it()
    {
        var table = Load("""
            {
              "caps": [
                { "id": "trial-01", "order": 1, "name": "1ª", "level": 14, "logro": "prueba-01" },
                { "id": "otra",     "order": 2, "name": "2ª", "level": 20 }
              ]
            }
            """);

        Assert.Equal("prueba-01", table.Stages[0].Achievement);
        Assert.Null(table.Stages[1].Achievement);
    }

    /// <summary>A cap past the end of the table stays at the last one, never falls off it.</summary>
    [Fact]
    public void Past_the_end_the_last_cap_holds()
    {
        var table = Load("""
            { "caps": [ { "id": "a", "order": 1, "name": "1ª", "level": 14 } ] }
            """);

        Assert.Equal(14, table.CapFor(99));
        Assert.Equal(14, table.CapFor(-3));
    }

    [Fact]
    public void No_file_means_no_cap_rather_than_a_made_up_one()
    {
        var table = LevelCapTable.Load(Path.Combine(Path.GetTempPath(), "no-existe-caps.json"));

        Assert.Empty(table.Stages);
        Assert.Null(table.CapFor(0));
    }

    /// <summary>
    /// The table the competition ships: fourteen stages, every one tied to its achievement, so the
    /// cap can move by itself from the first trial to the rematch.
    /// </summary>
    [Fact]
    public void The_shipped_table_ties_every_stage_to_an_achievement()
    {
        var table = LevelCapTable.Load(Path.Combine(Root(), "Data", "levelcaps.json"));

        Assert.Equal(14, table.Stages.Count);
        Assert.All(table.Stages, stage => Assert.False(string.IsNullOrWhiteSpace(stage.Achievement)));

        Assert.Equal("prueba-01", table.Stages[0].Achievement);
        Assert.Equal(14, table.Stages[0].Level);
        Assert.Equal("alto-mando-otra-vez", table.Stages[^1].Achievement);
        Assert.Equal(85, table.Stages[^1].Level);

        // Los caps sólo suben: una tabla que bajase dejaría fuera de ley a un equipo ya legal.
        for (var i = 1; i < table.Stages.Count; i++)
        {
            Assert.True(table.Stages[i].Level >= table.Stages[i - 1].Level,
                $"La etapa {table.Stages[i].Id} baja el cap respecto a {table.Stages[i - 1].Id}.");
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
