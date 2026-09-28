using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

public class NicknameVoteTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 28, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Votes_decide_and_without_votes_the_most_proposed_wins_ties_to_the_first()
    {
        Assert.Equal("Toby", NicknameVote.Winner([
            new("a", "Pepe", "toby", T), new("b", "Toby", "Toby", T.AddSeconds(1)), new("c", " pepe ", "Pepe", T.AddSeconds(2))]));

        Assert.Equal("Pepe", NicknameVote.Winner([
            new("a", "Pepe", null, T), new("b", "Toby", null, T.AddSeconds(1)), new("c", "pepe", null, T.AddSeconds(2))]));

        Assert.Equal("Toby", NicknameVote.Winner([new("a", "Toby", null, T), new("b", "Pepe", null, T.AddSeconds(1))]));
        Assert.Equal(["Pepe", "Toby"], NicknameVote.Options([new("a", "Pepe", null, T), new("b", "PEPE", null, T), new("c", "Toby", null, T)]));
        Assert.Null(NicknameVote.Winner([new("a", "   ", null, T), new("b", "Demasiado largo para el juego", null, T)]));
    }
}

public class NicknameRulesTests
{
    [Fact]
    public void Only_what_the_game_keyboard_types_and_at_most_twelve()
    {
        Assert.Null(RenameService.Problem("Pepé ¡Ñu!"));
        Assert.Null(RenameService.Problem("Toby♂"));
        Assert.NotNull(RenameService.Problem("Perrito😀"));
        Assert.NotNull(RenameService.Problem("犬"));
        Assert.NotNull(RenameService.Problem("Trececaracter"));
        Assert.Null(RenameService.Problem("Docecaracte1"));
    }
}
