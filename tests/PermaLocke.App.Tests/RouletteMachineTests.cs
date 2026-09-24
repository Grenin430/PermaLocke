using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The LUDÓPATA wheel in pixel art (§175): it stops on the winner with every braking profile and from wherever the last
/// spin left it, never carries past it more than a bounce, never changes its mind once it is slow, and no frame of a
/// spin can fail to draw.
/// </summary>
public sealed class RouletteMachineTests
{
    private static readonly double[] Starts = [0, 30, 90, 150, 210.5, 330, 359.9, -45, 725];

    /// <summary>The resting angle of every wedge puts that wedge under the pointer, and nothing else.</summary>
    [Fact]
    public void Each_wedge_rests_under_the_pointer()
    {
        for (var i = 0; i < RouletteTimeline.Wedges; i++)
        {
            Assert.Equal(i, RouletteTimeline.Under(RouletteTimeline.RestingAngle(i)));
        }
    }

    /// <summary>
    /// With every profile, every winner and any starting angle, the wheel ends on the winner — and stays there through
    /// the rock and after it.
    /// </summary>
    [Fact]
    public void It_always_stops_on_the_winner()
    {
        foreach (var ending in WheelEnding.All)
        {
            foreach (var start in Starts)
            {
                for (var winner = 0; winner < RouletteTimeline.Wedges; winner++)
                {
                    for (var t = RouletteTimeline.Stopped; t < RouletteTimeline.Landed + 3; t += 0.02)
                    {
                        var angle = RouletteTimeline.AngleAt(start, winner, ending, t);
                        Assert.Equal(winner, RouletteTimeline.Under(angle));
                    }

                    var rest = RouletteTimeline.AngleAt(start, winner, ending, RouletteTimeline.Landed + 5);
                    Assert.Equal(RouletteTimeline.Wrap(RouletteTimeline.RestingAngle(winner)), RouletteTimeline.Wrap(rest), 6);
                }
            }
        }
    }

    /// <summary>
    /// One sweep: once let go the wheel only turns forward until it reaches the winner, overshoots it by no more than the
    /// profile's bounce — always under half a wedge — and in the last half second the wedge under the pointer is the
    /// winner and nothing else (§86: where it stops, it stopped).
    /// </summary>
    [Fact]
    public void It_does_not_change_its_mind()
    {
        foreach (var ending in WheelEnding.All)
        {
            foreach (var start in Starts)
            {
                for (var winner = 0; winner < RouletteTimeline.Wedges; winner++)
                {
                    var sweepEnd = RouletteTimeline.SpinStart + RouletteTimeline.SpinTime
                                   - (ending.Bounce > 0 ? RouletteTimeline.SettleTime : 0);
                    var previous = double.MinValue;
                    var resting = RouletteTimeline.AngleAt(start, winner, ending, RouletteTimeline.Landed + 5);

                    for (var t = RouletteTimeline.SpinStart; t < sweepEnd; t += 0.01)
                    {
                        var angle = RouletteTimeline.AngleAt(start, winner, ending, t);
                        Assert.True(angle >= previous - 1e-9, $"«{ending.Name}» va hacia atrás en {t:0.00}");
                        Assert.True(angle <= resting + WheelEnding.MaxBounce + 1e-9, $"«{ending.Name}» se pasa en {t:0.00}");
                        previous = angle;
                    }

                    for (var t = RouletteTimeline.Stopped - 0.5; t < RouletteTimeline.Stopped; t += 0.01)
                    {
                        Assert.Equal(winner, RouletteTimeline.Under(RouletteTimeline.AngleAt(start, winner, ending, t)));
                    }
                }
            }
        }
    }

    /// <summary>The six faces turn over one at a time, all before the wheel moves, and the card comes after it lands.</summary>
    [Fact]
    public void The_faces_turn_over_before_it_moves()
    {
        var reveals = Enumerable.Range(0, RouletteTimeline.Wedges).Select(RouletteTimeline.RevealAt).ToList();

        Assert.Equal(reveals.Order(), reveals);
        Assert.True(RouletteTimeline.ChipIn < RouletteTimeline.FirstReveal);
        Assert.True(reveals[^1] + RouletteTimeline.RevealGap <= RouletteTimeline.SpinStart - RouletteTimeline.WindUp);
        Assert.True(RouletteTimeline.SpinStart < RouletteTimeline.Stopped);
        Assert.True(RouletteTimeline.Stopped < RouletteTimeline.Landed);
        Assert.All(reveals.Zip(reveals.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 1.5));
    }

    /// <summary>The pegs push the pointer one way and let it snap back a little, never further than it can swing.</summary>
    [Fact]
    public void The_pointer_swings_within_its_range()
    {
        for (var angle = -720.0; angle < 720; angle += 0.25)
        {
            Assert.InRange(RouletteMachineScene.Deflection(angle), -8, 22);
        }

        Assert.Equal(0, RouletteMachineScene.Deflection(RouletteTimeline.RestingAngle(0)));
    }

    /// <summary>A long label breaks into two lines at the space nearest the middle; a short one stays whole.</summary>
    [Theory]
    [InlineData("HABILIDAD BUENA", new[] { "HABILIDAD", "BUENA" })]
    [InlineData("IV AL MÁXIMO", new[] { "IV AL", "MÁXIMO" })]
    [InlineData("TIRADAS GACHA", new[] { "TIRADAS", "GACHA" })]
    [InlineData("PUNTOS", new[] { "PUNTOS" })]
    [InlineData("CURATIVOS", new[] { "CURATIVOS" })]
    public void Labels_fit_their_wedge(string label, string[] expected)
    {
        Assert.Equal(expected, RouletteMachineScene.SplitLabel(label));
    }

    /// <summary>
    /// Every frame of a spin draws, good landing or bad, with pictures or without, from the chip to long after it has
    /// landed, at the design size, narrower and taller; and the waiting wheel draws with an empty board.
    /// </summary>
    [Fact]
    public void Every_frame_of_every_spin_draws()
    {
        var icon = Sprite(32, 32);
        IReadOnlyList<RouletteBoardItem> board =
        [
            new("a", "TIRADA GACHA", "+1", true), new("b", "HABILIDAD BUENA", "×3", true),
            new("c", "MUEREN", "3", false), new("d", "PUNTOS", "−200", false),
        ];

        foreach (var (width, height) in new[] { (RouletteMachineScene.DesignWidth, RouletteMachineScene.DesignRows), (300, 120), (520, 260) })
        {
            var scene = new RouletteMachineScene(width, height);
            scene.Render(new RouletteSceneState([], null, 0, 0), 0, toScreen: false);
            scene.Render(new RouletteSceneState(board, null, 0, 40), 1, toScreen: false);

            for (var winner = 0; winner < RouletteTimeline.Wedges; winner += 3)
            {
                var wedges = Enumerable.Range(0, RouletteTimeline.Wedges)
                    .Select(i => new RouletteWedge(i % 2 == 0 ? "HABILIDAD BUENA" : "MUEREN", i % 2 == 0 ? "×3" : "3", i % 2 == 0,
                        i % 3 == 0 ? null : icon, i % 2 == 0 ? "b" : "c"))
                    .ToList();
                var show = new RouletteShow(wedges, winner, WheelEnding.All[winner % WheelEnding.All.Length], winner * 77, winner, 1 + winner);

                for (var t = 0.0; t < RouletteTimeline.Landed + 4; t += 0.05)
                {
                    scene.Render(new RouletteSceneState(board, show, t, 0), t, toScreen: false);
                }
            }

            Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
        }
    }

    /// <summary>Narrower than the design it is not squeezed, and taller it keeps the floor at the bottom.</summary>
    [Fact]
    public void The_scene_is_never_squeezed()
    {
        var narrow = new RouletteMachineScene(200, 60);
        var tall = new RouletteMachineScene(RouletteMachineScene.DesignWidth, 260);

        Assert.Equal(RouletteMachineScene.DesignWidth, narrow.Width);
        Assert.Equal(RouletteMachineScene.DesignRows, narrow.Height);
        Assert.Equal(260, tall.Height);
    }

    /// <summary>What the view model hands over reaches the scene in the same order and with the same faces.</summary>
    [Fact]
    public void The_play_reaches_the_scene_whole()
    {
        var play = new RoulettePlay(
            [new("PUNTOS", "+200", true, null, "puntos-mas"), new("MUERE", "1", false, null, "muerte-1")],
            1, WheelEnding.All[2], 150, 9, 4, 0);

        var show = RouletteMachine.ShowOf(play);

        Assert.Equal(["puntos-mas", "muerte-1"], show.Wedges.Select(w => w.FaceId));
        Assert.Equal(1, show.WinningIndex);
        Assert.Equal(150, show.StartAngle);
        Assert.Equal(4, show.OwedBefore);
        Assert.Same(WheelEnding.All[2], show.Ending);
    }

    private static RoomSprite Sprite(int width, int height)
    {
        var bgra = new byte[width * height * 4];
        for (var y = 6; y < height - 2; y++)
        {
            for (var x = 8; x < width - 8; x++)
            {
                var at = ((y * width) + x) * 4;
                bgra[at] = 0x30;
                bgra[at + 1] = 0x90;
                bgra[at + 2] = 0xE0;
                bgra[at + 3] = 0xFF;
            }
        }

        return new RoomSprite(bgra, width, height);
    }
}
