using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The three roles, checked against what the competition says they do, because these numbers are
/// the whole difference between playing one role and another.
/// </summary>
public sealed class RoleTests
{
    [Fact]
    public void The_normal_role_changes_nothing_about_points()
    {
        var role = FixedRole.Normal;

        Assert.Equal(100, role.Reward(100));
        Assert.Equal(25, role.Penalty(25));
        Assert.False(role.ChangesPoints);
    }

    /// <summary>«sin perder puntos y sólo ganando la mitad».</summary>
    [Fact]
    public void The_cagoneta_never_loses_and_earns_half()
    {
        var role = FixedRole.Cagoneta;

        Assert.Equal(50, role.Reward(100));
        Assert.Equal(0, role.Penalty(25));
        Assert.Equal(0, role.Penalty(100));
        Assert.True(role.ChangesPoints);
    }

    /// <summary>«Ganas 1.5x puntos pero pierdes el doble».</summary>
    [Fact]
    public void The_experto_earns_one_and_a_half_and_loses_double()
    {
        var role = FixedRole.Experto;

        Assert.Equal(150, role.Reward(100));
        Assert.Equal(50, role.Penalty(25));
        Assert.Equal(200, role.Penalty(100));
    }

    /// <summary>
    /// Halving rounds up, not down. A 25 point achievement pays 13: rounding to 12 would look
    /// like a bug to whoever checks the sum.
    /// </summary>
    [Fact]
    public void Halving_an_odd_reward_rounds_up()
    {
        Assert.Equal(13, FixedRole.Cagoneta.Reward(25));
        Assert.Equal(38, FixedRole.Cagoneta.Reward(75));
    }

    /// <summary>
    /// The point of the expert role: the trainers climb past the player's cap. Its own cap stays
    /// at the base 20%, so the rivals gain seven points of level on it.
    /// </summary>
    [Fact]
    public void The_experto_lets_the_trainers_outgrow_its_own_cap()
    {
        Assert.Equal(20, FixedRole.Normal.TrainerLevelPercent);
        Assert.Equal(27, FixedRole.Experto.TrainerLevelPercent);
        Assert.Equal(20, FixedRole.Experto.PlayerCapPercent);
        Assert.True(FixedRole.Experto.TrainerLevelPercent > FixedRole.Experto.PlayerCapPercent);
    }

    /// <summary>The base edit applies to everyone: nobody plays the cartridge as it came.</summary>
    [Theory]
    [InlineData(14, 17)]
    [InlineData(20, 24)]
    [InlineData(73, 88)]
    public void Every_role_raises_the_players_cap_over_the_cartridge(int cartridge, int expected)
    {
        Assert.Equal(expected, FixedRole.Normal.CapFor(cartridge));
        Assert.Equal(expected, FixedRole.Cagoneta.CapFor(cartridge));
        Assert.Equal(expected, FixedRole.Experto.CapFor(cartridge));
    }

    /// <summary>Nothing goes past 100, which is the highest level the cartridge can hold.</summary>
    [Fact]
    public void Levels_never_pass_a_hundred()
    {
        Assert.Equal(100, FixedRole.Experto.TrainerLevelFor(100));
        Assert.Equal(100, FixedRole.Normal.CapFor(95));
    }

    /// <summary>
    /// A run whose role cannot be resolved must not be paid at an invented rate: the number is
    /// left alone and the event says the role was unknown, so it can be found and fixed.
    /// </summary>
    [Fact]
    public void Without_a_role_the_number_is_left_alone_and_said_so()
    {
        var reward = RoleAdjusted.Reward(null, 100);
        var penalty = RoleAdjusted.Penalty(null, 25);

        Assert.Equal(100, reward.Final);
        Assert.Equal(25, penalty.Final);
        Assert.Equal("desconocido", reward.RoleId);
        Assert.False(reward.Changed);
    }

    /// <summary>The history has to show the working, not just the total.</summary>
    [Fact]
    public void An_adjusted_number_carries_its_own_explanation()
    {
        var reward = RoleAdjusted.Reward(FixedRole.Cagoneta, 100);

        Assert.Equal(100, reward.Base);
        Assert.Equal(50, reward.Final);
        Assert.True(reward.Changed);
        Assert.Contains("100", reward.Explain());
        Assert.Contains("cagoneta", reward.Explain());
    }
}
