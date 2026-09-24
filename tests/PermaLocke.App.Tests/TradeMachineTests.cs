using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;
using System.Windows.Media;

namespace PermaLocke.App.Tests;

/// <summary>
/// The wonder trade's cabin (§174): the Pokémon goes in its own ball, the screen says better or worse in the right
/// colour, and no frame of a trade can fail to draw.
/// </summary>
public sealed class TradeMachineTests
{
    private static readonly TradeType Dragon = new("Dragón", Color.FromRgb(0x50, 0x60, 0xE1));
    private static readonly TradeType Ground = new("Tierra", Color.FromRgb(0x91, 0x51, 0x21));

    /// <summary>Every trade reaches the same moments in the same order, whatever comes back.</summary>
    [Fact]
    public void The_timeline_runs_in_order()
    {
        double[] moments =
        [
            TradeTimeline.BallOut, TradeTimeline.BeamStart, TradeTimeline.ShrinkStart, TradeTimeline.Closed,
            TradeTimeline.HopStart, TradeTimeline.AtHatch, TradeTimeline.Up, TradeTimeline.Gone, TradeTimeline.Connected,
            TradeTimeline.Down, TradeTimeline.InStation, TradeTimeline.Out, TradeTimeline.Dropped, TradeTimeline.RollEnd,
            TradeTimeline.Settled, TradeTimeline.Generation, TradeTimeline.Types, TradeTimeline.SecondType,
            TradeTimeline.Total, TradeTimeline.TotalLanded, TradeTimeline.Open, TradeTimeline.Emerge, TradeTimeline.Revealed,
        ];

        Assert.Equal(moments.Order(), moments);
        Assert.Equal(moments.Length, moments.Distinct().Count());
    }

    /// <summary>
    /// The five balls the scene draws are the save's own numbers for them; any other ball is left to the cartridge's
    /// icon instead of passing for one of these.
    /// </summary>
    [Theory]
    [InlineData(1, CapsuleBall.Master)]
    [InlineData(2, CapsuleBall.Ultra)]
    [InlineData(3, CapsuleBall.Great)]
    [InlineData(4, CapsuleBall.Poke)]
    [InlineData(16, CapsuleBall.Cherish)]
    [InlineData(13, null)]
    [InlineData(12, null)]
    [InlineData(0, null)]
    public void The_given_ball_is_its_own(int ballId, CapsuleBall? expected)
    {
        Assert.Equal(expected, TradeMachineScene.BallFor(ballId));
        Assert.Equal(expected, TradeMachine.ShowOf(Play(ballId)).GivenBall);
    }

    /// <summary>The rays reach further for a bigger total, from none of the extra to all of it.</summary>
    [Fact]
    public void The_rays_grow_with_the_total()
    {
        var strengths = Enumerable.Range(150, 600).Select(TradeMachineScene.RayStrength).ToList();

        Assert.Equal(strengths.Order(), strengths);
        Assert.Equal(0, strengths[0]);
        Assert.Equal(4, strengths[^1]);
    }

    /// <summary>
    /// Every frame of a trade draws: with and without pictures, one type or two or none, a drawn ball or an icon, shiny
    /// or legendary, better or worse. A frame that threw would cost the animation of a trade that is already written.
    /// </summary>
    [Fact]
    public void Every_frame_of_every_trade_draws()
    {
        var sprite = Sprite(40, 30, 0x30, 0x90, 0xE0);
        var icon = Sprite(32, 32, 0x12, 0xF0, 0x34);
        TradeShow[] shows =
        [
            new(sprite, CapsuleBall.Poke, null, sprite, 4, [Dragon, Ground], 540, 600, 11, false, false, 1),
            new(null, null, icon, null, 9, [Dragon], 600, 552, -8, true, true, -7),
            new(sprite, null, null, sprite, 1, [], 300, 300, 0, false, false, 0),
            new(sprite, CapsuleBall.Master, null, null, 7, [Ground, Dragon], 180, 216, 20, true, false, int.MaxValue),
        ];

        foreach (var (width, height) in new[] { (TradeMachineScene.DesignWidth, TradeMachineScene.DesignRows), (300, 90), (520, 260) })
        {
            var scene = new TradeMachineScene(width, height);
            scene.Render(null, 0, 0, toScreen: false);

            foreach (var show in shows)
            {
                for (var t = 0.0; t < 14; t += 0.05)
                {
                    scene.Render(show, t, t, toScreen: false);
                }
            }

            Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
        }
    }

    /// <summary>Narrower than the design it is not squeezed, and taller it keeps the floor at the bottom.</summary>
    [Fact]
    public void The_scene_is_never_squeezed()
    {
        var narrow = new TradeMachineScene(200, 60);
        var tall = new TradeMachineScene(TradeMachineScene.DesignWidth, 240);

        Assert.Equal(TradeMachineScene.DesignWidth, narrow.Width);
        Assert.Equal(TradeMachineScene.DesignRows, narrow.Height);
        Assert.Equal(240, tall.Height);
    }

    /// <summary>
    /// Once the total has stopped counting, the screen says it better in green, worse in red, and the same in neither.
    /// </summary>
    [Theory]
    [InlineData(11, true, false)]
    [InlineData(-8, false, true)]
    [InlineData(0, false, false)]
    public void The_screen_says_better_or_worse_in_its_colour(int difference, bool green, bool red)
    {
        var scene = new TradeMachineScene(TradeMachineScene.DesignWidth);
        var show = new TradeShow(null, CapsuleBall.Poke, null, null, 4, [Dragon], 540, 540 + (difference * 5), difference,
            false, false, 3);

        scene.Render(show, TradeTimeline.TotalLanded + 0.4, 0, toScreen: false);

        Assert.Equal(green, Cells(scene.Pixels, 0x92, 0xF2, 0x7E) > 0);
        Assert.Equal(red, Cells(scene.Pixels, 0xFF, 0x74, 0x62) > 0);
    }

    /// <summary>
    /// A ball the scene does not draw goes on screen as the cartridge's own icon: the Pokémon handed over goes into the
    /// ball it lives in, not into a Poké Ball that is not its.
    /// </summary>
    [Fact]
    public void A_ball_the_scene_does_not_draw_is_the_cartridge_icon()
    {
        var icon = Sprite(32, 32, 0x12, 0xF0, 0x34);
        var scene = new TradeMachineScene(TradeMachineScene.DesignWidth);
        var withIcon = new TradeShow(null, null, icon, null, 4, [Dragon], 540, 600, 11, false, false, 3);
        var drawn = new TradeShow(null, CapsuleBall.Poke, null, null, 4, [Dragon], 540, 600, 11, false, false, 3);

        scene.Render(withIcon, TradeTimeline.HopStart + 0.2, 0, toScreen: false);
        Assert.True(Cells(scene.Pixels, 0x12, 0xF0, 0x34) > 10);

        scene.Render(drawn, TradeTimeline.HopStart + 0.2, 0, toScreen: false);
        Assert.Equal(0, Cells(scene.Pixels, 0x12, 0xF0, 0x34));
    }

    private static TradePlay Play(int ball) =>
        new(null, ball, null, null, 4, [Dragon], 540, 600, 11, false, false, 1, 0);

    private static int Cells(byte[] bgra, byte r, byte g, byte b) =>
        Enumerable.Range(0, bgra.Length / 4).Count(i => bgra[(i * 4) + 2] == r && bgra[(i * 4) + 1] == g && bgra[i * 4] == b);

    private static RoomSprite Sprite(int width, int height, byte r, byte g, byte b)
    {
        var bgra = new byte[width * height * 4];
        for (var y = 6; y < height - 2; y++)
        {
            for (var x = 8; x < width - 8; x++)
            {
                var at = ((y * width) + x) * 4;
                bgra[at] = b;
                bgra[at + 1] = g;
                bgra[at + 2] = r;
                bgra[at + 3] = 0xFF;
            }
        }

        return new RoomSprite(bgra, width, height);
    }
}
