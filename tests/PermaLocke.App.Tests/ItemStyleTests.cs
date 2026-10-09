using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// The styles of the item animations (2026-10-09): each draws inside the height it declares, and the scaling leaves no hole at
/// any size of the game's pixel.
/// </summary>
public sealed class ItemStyleTests
{
    private const double Frame = 1 / 60.0;

    private static ItemScene.Item Stone(int seed, uint tint = 0xFFC03050, ItemCategory category = ItemCategory.MegaStone)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, "HEATRANITA", 1, category, 0, tint, seed);
    }

    /// <summary>One item of every category, so that every style is tried with something it is meant to draw.</summary>
    private static IEnumerable<ItemScene.Item> Items() =>
        Enum.GetValues<ItemCategory>().SelectMany(category => new[] { Stone(1, 0xFFC03050, category), Stone(9876, 0xFF3070D0, category) });

    [Fact]
    public void Every_style_declares_a_height_and_nothing_it_draws_goes_outside_of_it()
    {
        foreach (var item in Items())
        {
            var style = ItemStyles.For(item.Category);
            var scene = new ItemScene(1, style.Height);
            var length = style.Phases(item).Length;

            Assert.Equal(style.Height, ItemScene.HeightFor(item));

            for (var t = 0.0; t < length; t += Frame)
            {
                scene.Render(item, t);

                Assert.True(scene.Overdraw == 0,
                    $"{item.Category}, semilla {item.Seed}, t={t:0.000}: pidió {scene.Overdraw} píxeles fuera de sus {style.Height} de alto.");
            }
        }
    }

    /// <summary>
    /// A frame at any size is the frame at size one with every cell of the game blown up to the block between its edges, so
    /// that no monitor pixel inside the drawing is left transparent (the straight lines of §242) and none is painted twice.
    /// </summary>
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.3)]
    [InlineData(3.7)]
    [InlineData(1.0)]
    public void Scaling_a_frame_leaves_no_hole_at_any_size_of_the_game_pixel(double pixel)
    {
        foreach (var item in new[] { Stone(77, category: ItemCategory.Misc), Stone(4242, 0xFF20A060) })
        {
            var style = ItemStyles.For(item.Category);
            var length = style.Phases(item).Length;
            var reference = new ItemScene(1, style.Height);
            var scaled = new ItemScene(pixel, style.Height);

            foreach (var fraction in new[] { 0.15, 0.30, 0.45, 0.55, 0.60, 0.70, 0.80, 0.90 })
            {
                var t = length * fraction;
                reference.Render(item, t);
                scaled.Render(item, t);

                for (var gy = 0; gy < style.Height; gy++)
                {
                    for (var gx = 0; gx < ItemScene.SceneWidth; gx++)
                    {
                        var want = Cell(reference, gx, gy);

                        var left = (int)Math.Round(gx * pixel);
                        var top = (int)Math.Round(gy * pixel);
                        var right = Math.Max(left + 1, (int)Math.Round((gx + 1) * pixel));
                        var bottom = Math.Max(top + 1, (int)Math.Round((gy + 1) * pixel));

                        for (var y = top; y < Math.Min(scaled.Height, bottom); y++)
                        {
                            for (var x = left; x < Math.Min(scaled.Width, right); x++)
                            {
                                if (want != Cell(scaled, x, y))
                                {
                                    Assert.Fail($"{item.Category}, t={t:0.00}: la celda ({gx},{gy}) a escala {pixel} no es la del tamaño uno en ({x},{y}).");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The pixel as a number, to compare cells.</summary>
    private static uint Cell(ItemScene scene, int x, int y)
    {
        var i = ((y * scene.Width) + x) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }
}
