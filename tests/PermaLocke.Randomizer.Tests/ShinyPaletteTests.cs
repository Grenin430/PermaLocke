using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The shiny recolouring learns from two renders and applies to an icon lit differently, so these check what
/// must hold whatever the tuning: a change is carried over, what does not change stays, and no reference means no table.
/// </summary>
public class ShinyPaletteTests
{
    /// <summary>A render half red body, half white belly.</summary>
    private static byte[] Render(int bodyR, int bodyG, int bodyB)
    {
        var px = new byte[20 * 20 * 4];
        for (var i = 0; i < 400; i++)
        {
            var body = i < 200;
            px[i * 4] = (byte)(body ? bodyR : 235);
            px[i * 4 + 1] = (byte)(body ? bodyG : 235);
            px[i * 4 + 2] = (byte)(body ? bodyB : 235);
            px[i * 4 + 3] = 255;
        }
        return px;
    }

    private static readonly byte[] Icon =
    [
        150, 20, 25, 255,     // a darker red than the render's
        250, 250, 250, 255,   // a brighter white
        0, 0, 0, 0            // transparent: not in the table
    ];

    [Fact]
    public void A_body_that_turns_blue_turns_the_icons_red_blue_and_leaves_the_white()
    {
        var map = ShinyPalette.Build(Render(200, 40, 40), Render(40, 60, 200), Icon)!;

        Assert.Equal(2, map.Count);
        var red = map[(150 << 16) | (20 << 8) | 25];
        Assert.True((red & 0xFF) > (red >> 16) + 40, $"El rojo debía volverse azul y salió {red:X6}.");
        Assert.True((red & 0xFF) < 200, $"Debía seguir más oscuro que el render y salió {red:X6}.");

        var white = map[0xFAFAFA];
        Assert.All(new[] { white >> 16, (white >> 8) & 0xFF, white & 0xFF }, c => Assert.InRange(c, 238, 255));
    }

    /// <summary>Showdown has shiny references that are the normal picture again (Gholdengo): nothing to learn.</summary>
    [Fact]
    public void Identical_renders_give_no_table()
    {
        Assert.Null(ShinyPalette.Build(Render(200, 40, 40), Render(200, 40, 40), Icon));
    }

    /// <summary>Showdown's Ogerpon has its shiny in another pose: same size, but pixel for pixel it means nothing.</summary>
    [Fact]
    public void Renders_in_different_poses_give_no_table()
    {
        var shifted = Render(40, 60, 200);
        for (var i = 0; i < 400; i++)
        {
            shifted[i * 4 + 3] = (byte)(i % 20 < 10 ? 255 : 0);
        }
        var normal = Render(200, 40, 40);
        for (var i = 0; i < 400; i++)
        {
            normal[i * 4 + 3] = (byte)(i % 20 < 10 ? 0 : 255);
        }

        Assert.Null(ShinyPalette.Build(normal, shifted, Icon));
    }

    [Fact]
    public void Renders_of_different_size_give_no_table()
    {
        Assert.Null(ShinyPalette.Build(Render(200, 40, 40), new byte[16], Icon));
    }
}
