using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The level the role gives a trainer. The competition's whole base edit is this one number, so
/// the rounding matters more than it looks.
/// </summary>
public sealed class TrainerLevelTests
{
    /// <summary>
    /// Rounds away from zero. The first trainers of the game are level 5, and rounding down would
    /// leave +20% meaning nothing at all for the whole first island: 5 would stay 5, 6 would stay
    /// 7 only by luck, and the difficulty edit would quietly not exist where it is felt most.
    /// </summary>
    [Theory]
    [InlineData(5, 20, 6)]
    [InlineData(14, 20, 17)]
    [InlineData(51, 20, 61)]
    [InlineData(63, 20, 76)]
    public void Twenty_percent_is_the_base_edit(int cartridge, int percent, int expected) =>
        Assert.Equal(expected, TrainerRandomizer.Raise(cartridge, percent));

    /// <summary>The expert's extra seven points, checked against the real boss levels.</summary>
    [Theory]
    [InlineData(51, 65)]
    [InlineData(63, 80)]
    [InlineData(68, 86)]
    [InlineData(69, 88)]
    public void The_experto_adds_seven_more(int cartridge, int expected) =>
        Assert.Equal(expected, TrainerRandomizer.Raise(cartridge, 27));

    /// <summary>Zero leaves the cartridge exactly as it came.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(51)]
    [InlineData(100)]
    public void Zero_percent_changes_nothing(int level) =>
        Assert.Equal(level, TrainerRandomizer.Raise(level, 0));

    /// <summary>
    /// A hundred is the ceiling the cartridge can hold, and the last bosses are close enough to it
    /// that a percentage would otherwise overflow the byte.
    /// </summary>
    [Fact]
    public void Nothing_goes_past_a_hundred()
    {
        Assert.Equal(100, TrainerRandomizer.Raise(90, 27));
        Assert.Equal(100, TrainerRandomizer.Raise(100, 20));
        Assert.Equal(1, TrainerRandomizer.Raise(0, 20));
    }
}
