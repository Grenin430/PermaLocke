using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Who can link-battle whom (§124). COMBATES swaps every randomization out, so only the base game
/// has to match — and a run's randomizer options never make it «cannot battle» any more.
/// </summary>
public sealed class LinkBattleAdviceTests
{
    private static RunSnapshot Player(string name, int? species, bool? ready = false) => new()
    {
        RunId = Guid.NewGuid(),
        PlayerName = name,
        RunName = "PRUEBA",
        WorldSpecies = species,
        BattleReady = ready
    };

    /// <summary>The case the player saw: random abilities on, and the old note said «Grenin no puede».</summary>
    [Fact]
    public void Random_abilities_no_longer_stop_anybody()
    {
        var note = LinkBattleAdvice.Summarize([Player("Grenin", 1025, ready: false), Player("Ash", 1025, ready: false)]);

        Assert.Equal("ok", note.State);
        Assert.DoesNotContain("no puede", note.Note);
        Assert.Contains("COMBATES", note.Note);
    }

    [Fact]
    public void Different_base_games_are_warned_by_name()
    {
        var note = LinkBattleAdvice.Summarize([Player("Grenin", 1025), Player("Ash", 1025), Player("Brock", 807)]);

        Assert.Equal("warn", note.State);
        Assert.Contains("Grenin, Ash con las generaciones 8 y 9", note.Note);
        Assert.Contains("Brock con el cartucho sin mod", note.Note);
    }

    [Fact]
    public void Unknown_is_said_apart_and_never_called_incompatible()
    {
        var note = LinkBattleAdvice.Summarize([Player("Grenin", 1025), Player("Viejo", null)]);

        Assert.Equal("warn", note.State);
        Assert.Contains("De Viejo no se sabe", note.Note);
        Assert.DoesNotContain("desincroniza aunque", note.Note);
    }

    [Fact]
    public void Alone_there_is_nobody_to_battle_and_nothing_to_flag()
    {
        var note = LinkBattleAdvice.Summarize([Player("Grenin", 1025)]);

        Assert.Equal("none", note.State);
        Assert.Contains("nadie más", note.Note);
    }

    [Fact]
    public void Nobody_published_says_nothing() =>
        Assert.Equal(new LinkBattleNote(string.Empty, "none"), LinkBattleAdvice.Summarize([]));
}
