using PermaLocke.Rules;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// Whether the cap is a rule or a rewrite, and which one you get by not deciding.
/// </summary>
/// <remarks>
/// Measured on the competition's own reference binaries: they make <b>no emulator memory access at
/// all</b> — no WriteMemory, no RPC, nothing on port 45987 — and carry exactly one level-cap symbol,
/// a getter for the list. There the cap is a rule the player keeps and the app shows. PermaLocke
/// keeps the machinery to force it, because it is written and tested, but not as the default: a
/// correction that goes wrong does not fail, it changes somebody's Pokémon.
/// </remarks>
public sealed class LevelCapSwitchTests
{
    private static string WriteFile(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"levelcaps-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, body);
        return path;
    }

    private const string Stage = """{ "id": "t1", "order": 1, "name": "1ª", "level": 14 }""";

    [Fact]
    public void Not_saying_gets_the_safe_answer()
    {
        var path = WriteFile($$"""{ "caps": [ {{Stage}} ] }""");

        try
        {
            Assert.False(LevelCapTable.Load(path).CorrectInMemory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_file_does_not_switch_it_on_either()
    {
        Assert.False(LevelCapTable.Load(Path.Combine(Path.GetTempPath(), "no-existe.json"))
            .CorrectInMemory);
    }

    /// <summary>Asking for it explicitly still works: the machinery is not gone, just not default.</summary>
    [Fact]
    public void Asking_for_it_turns_it_on()
    {
        var path = WriteFile($$"""{ "corregirEnMemoria": true, "caps": [ {{Stage}} ] }""");

        try
        {
            var table = LevelCapTable.Load(path);

            Assert.True(table.CorrectInMemory);
            Assert.Equal(14, table.CapFor(0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The file the app ships with asks for the correction, and says so out loud.</summary>
    /// <remarks>
    /// The default in code is off, because a correction that goes wrong changes somebody Pokémon.
    /// The file that ships turns it on, because PermaLocke has the emulator link the reference does
    /// not -- measured: no sockets anywhere in its binaries -- and the player wants the cap applied.
    /// Two different questions, answered separately on purpose.
    /// </remarks>
    [Fact]
    public void The_file_that_ships_leaves_it_off()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Data", "levelcaps.json");

        if (!File.Exists(shipped))
        {
            return;
        }

        Assert.True(LevelCapTable.Load(shipped).CorrectInMemory);
    }
}
