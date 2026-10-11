using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>What the cap panel reads of each Pokémon and how it says it (2026-10-10).</summary>
public sealed class CapDataTests
{
    [Theory]
    [InlineData(0, CapStatus.None)]
    [InlineData(1, CapStatus.Paralysis)]
    [InlineData(2, CapStatus.Sleep)]
    [InlineData(3, CapStatus.Freeze)]
    [InlineData(4, CapStatus.Burn)]
    [InlineData(5, CapStatus.Poison)]
    [InlineData(0x0300 | 4, CapStatus.Burn)]
    [InlineData(6, CapStatus.None)]
    [InlineData(0x10, CapStatus.None)]
    public void The_status_of_a_party_pokemon_is_the_games_number_and_a_burnt_one_is_not_asleep(int raw, CapStatus expected)
    {
        Assert.Equal(expected, CapBadge.StatusOf(raw));
    }

    [Fact]
    public void A_pokemon_is_near_the_cap_one_level_under_it_with_most_of_the_experience_and_at_it_from_there_on()
    {
        CapPlaqueMember Member(int level, double exp, int hp = 10, bool egg = false) =>
            new(null, "X", level, hp, 10, egg, Exp: exp);

        Assert.True(Member(41, 0.85).NearCap(42));
        Assert.False(Member(41, 0.5).NearCap(42));
        Assert.False(Member(40, 0.95).NearCap(42));
        Assert.False(Member(42, 0.95).NearCap(42));
        Assert.False(Member(41, -1).NearCap(42));
        Assert.True(Member(42, 0).AtCap(42));
        Assert.True(Member(45, 0).AtCap(42));
        Assert.False(Member(41, 0).AtCap(42));

        // A fallen one and an egg are neither: nothing about them is going anywhere.
        Assert.False(Member(41, 0.9, hp: 0).NearCap(42));
        Assert.False(Member(42, 0, hp: 0).AtCap(42));
        Assert.False(Member(41, 0.9, egg: true).NearCap(42));
    }

    [Fact]
    public void The_new_settings_have_defaults_that_keep_the_panel_as_asked_for()
    {
        var settings = new AppSettingsData();
        Assert.True(settings.CapPanel);
        Assert.Equal(0, settings.CapSize);
        Assert.Equal(0, settings.CapMaxCell);
        Assert.True(settings.CapMemorial);
        Assert.True(settings.CapDetails);
        Assert.True(settings.CapAnimations);
        Assert.Equal(0x13, settings.CapKey);
    }

    [Fact]
    public void Settings_written_before_these_existed_still_read_with_the_defaults()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<AppSettingsData>("{\"CapPanel\":false,\"Resolution\":2}");
        Assert.NotNull(old);
        Assert.False(old!.CapPanel);
        Assert.Equal(2, old.Resolution);
        Assert.True(old.CapMemorial);
        Assert.Equal(0x13, old.CapKey);
    }
}
