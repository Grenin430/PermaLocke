using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The blood rain of a team wipe (§184): it rains red, it leaves nothing behind when it ends, and no size of emulator
/// can make a step fail to draw.
/// </summary>
public sealed class BloodRainTests
{
    /// <summary>Twelve seconds of rain, at the ghost's beat.</summary>
    [Fact]
    public void It_rains_for_twelve_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(12), BloodRain.Length);
        Assert.Equal(266, BloodRain.Steps);
    }

    /// <summary>Once it pours, there are drops on screen, and every one of them is red.</summary>
    [Fact]
    public void Once_it_pours_everything_drawn_is_red()
    {
        var rain = Pour(new BloodRain(200, 150, 7), 150);
        var drawn = rain.Pixels.Where(p => p >> 24 == 0xFF).ToList();

        Assert.True(drawn.Count > 200, $"Solo {drawn.Count} celdas de lluvia.");
        Assert.All(rain.Pixels, p => Assert.True(Red(p) > Green(p) * 3 && Red(p) > Blue(p) * 3, $"{p:X8} no es rojo."));
    }

    /// <summary>The wash that reddens the game stays thin: the game has to be seen through it.</summary>
    [Fact]
    public void The_wash_does_not_hide_the_game()
    {
        var rain = Pour(new BloodRain(200, 150, 7), 150);

        Assert.True(rain.Pixels.Min(p => p >> 24) <= 0x40);
    }

    /// <summary>Blood pools at the bottom while it rains.</summary>
    [Fact]
    public void Blood_pools_at_the_bottom()
    {
        var rain = Pour(new BloodRain(200, 150, 7), 210);
        var bottom = rain.Pixels.Skip(149 * 200).Count(p => p >> 24 == 0xFF);

        Assert.True(bottom > 150, $"Solo {bottom} de 200 columnas con charco.");
    }

    /// <summary>When it ends, nothing is left over the game, not even the wash.</summary>
    [Fact]
    public void When_it_ends_nothing_is_left()
    {
        var rain = Pour(new BloodRain(200, 150, 7), BloodRain.Steps + 10);

        Assert.True(rain.Done);
        Assert.All(rain.Pixels, p => Assert.Equal(0u, p >> 24));
    }

    /// <summary>The same seed draws the same rain: nothing but the seed moves it.</summary>
    [Fact]
    public void The_same_seed_draws_the_same_rain()
    {
        Assert.Equal(Pour(new BloodRain(120, 90, 3), 150).Pixels, Pour(new BloodRain(120, 90, 3), 150).Pixels);
    }

    /// <summary>A tiny or empty window still rains from start to end without failing.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(640, 4)]
    public void Any_size_rains_to_the_end(int columns, int rows)
    {
        var rain = new BloodRain(columns, rows, 11);

        while (!rain.Done)
        {
            rain.Advance();
            rain.Draw();
        }

        Assert.Equal(rain.Columns * rain.Rows, rain.Pixels.Length);
    }

    private static BloodRain Pour(BloodRain rain, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            rain.Advance();
        }

        rain.Draw();
        return rain;
    }

    private static uint Red(uint p) => (p >> 16) & 0xFF;

    private static uint Green(uint p) => (p >> 8) & 0xFF;

    private static uint Blue(uint p) => p & 0xFF;
}
