using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// The styles of the item animations (2026-10-09): each draws inside the height it declares, the scaling leaves no hole at
/// any size of the game's pixel, and the same moment of the same item is always the same frame.
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

    [Fact]
    public void The_styles_that_have_a_climax_may_ask_for_more_height_but_not_for_more_width()
    {
        Assert.Equal(ItemScene.SceneHeight, ItemStyles.For(ItemCategory.Misc).Height);
        Assert.Equal(112, ItemStyles.For(ItemCategory.MegaStone).Height);

        var scene = new ItemScene(2.3, 112);
        Assert.Equal((int)Math.Ceiling(ItemScene.SceneWidth * 2.3), scene.Width);
        Assert.Equal((int)Math.Ceiling(112 * 2.3), scene.Height);
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
        foreach (var item in new[] { Stone(77, category: ItemCategory.Misc), Stone(77), Stone(4242, 0xFF20A060) })
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
                        var want = Cell(reference, gx, gy, 1);

                        var left = (int)Math.Round(gx * pixel);
                        var top = (int)Math.Round(gy * pixel);
                        var right = Math.Max(left + 1, (int)Math.Round((gx + 1) * pixel));
                        var bottom = Math.Max(top + 1, (int)Math.Round((gy + 1) * pixel));

                        for (var y = top; y < Math.Min(scaled.Height, bottom); y++)
                        {
                            for (var x = left; x < Math.Min(scaled.Width, right); x++)
                            {
                                if (want != Cell(scaled, x, y, 1))
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

    [Fact]
    public void The_same_moment_of_the_same_item_is_the_same_frame_and_the_seed_changes_it()
    {
        var a = new ItemScene(2, 112);
        var b = new ItemScene(2, 112);
        var c = new ItemScene(2, 112);

        // A moment of the resonance, with the strands and the heartbeat, and another with the rings and the glyph.
        foreach (var t in new[] { 1.7, 2.9 })
        {
            a.Render(Stone(5), t);
            b.Render(Stone(5), t);
            c.Render(Stone(6), t);

            Assert.Equal(a.Pixels, b.Pixels);
            Assert.NotEqual(a.Pixels, c.Pixels);
        }
    }

    [Fact]
    public void The_stone_tints_the_scene_and_a_stone_of_another_colour_tints_it_another_way()
    {
        var red = new ItemScene(2, 112);
        var blue = new ItemScene(2, 112);

        red.Render(Stone(3, 0xFFD02020), 2.9);
        blue.Render(Stone(3, 0xFF2040D0), 2.9);

        // The bag is orange in both: what tells them apart is the stone's family of colours.
        Assert.NotEqual(red.Pixels, blue.Pixels);
        Assert.True(Dominant(red.Pixels) > Dominant(blue.Pixels) + 40, "a red stone makes the scene redder than a blue one does");
    }

    [Fact]
    public void The_plate_is_there_whenever_the_scene_is_whole_and_nothing_is_drawn_after_the_end()
    {
        var item = Stone(2);
        var scene = new ItemScene(2, 112);
        var phases = ItemStyles.For(ItemCategory.MegaStone).Phases(item);

        foreach (var t in new[] { 1.0, 1.7, 2.45, 2.9, 3.5 })
        {
            scene.Render(item, t);

            // Where the plate's gold edge goes, 66 columns in and 44 rows down: opaque.
            var x = (int)(68 * 2);
            var y = (int)(50 * 2);
            Assert.Equal(0xFF, scene.Pixels[(((y * scene.Width) + x) * 4) + 3]);
        }

        scene.Render(item, phases.Length + 0.1);
        Assert.All(scene.Pixels, value => Assert.Equal(0, value));
    }

    /// <summary>
    /// Nothing is allocated to draw a frame: not the plate's strings, not the arrays of a loop, not a closure. The first pass lets
    /// everything that is made once be made, and the second is measured.
    /// </summary>
    [Fact]
    public void A_frame_of_any_style_allocates_nothing()
    {
        foreach (var category in Enum.GetValues<ItemCategory>())
        {
            var item = Stone(31, 0xFF3070D0, category) with { Amount = 3, Power = 2, Kind = 9, Detail = "FUEGO", Name = "UN NOMBRE LARGO DE OBJETO" };
            var style = ItemStyles.For(category);
            var scene = new ItemScene(2.25, style.Height);
            var length = style.Phases(item).Length;

            for (var t = 0.0; t < length; t += 1 / 30.0) scene.Render(item, t);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var t = 0.0; t < length; t += 1 / 30.0) scene.Render(item, t);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated == 0, $"{category}: {allocated} bytes asignados en {(int)(length * 30)} fotogramas");
        }
    }

    /// <summary>The pixel as a number, to compare cells.</summary>
    private static uint Cell(ItemScene scene, int x, int y, int cell)
    {
        var i = (((y * scene.Width) + x) * 4 * cell);
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    /// <summary>More than zero when the frame has more red than blue in what it paints, less when it has more blue.</summary>
    private static long Dominant(byte[] bgra)
    {
        long sum = 0;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0) continue;
            var b = bgra[i];
            var g = bgra[i + 1];
            var r = bgra[i + 2];

            // Only what is clearly one or the other: the plate, the bag and the white are none of them.
            if (r > b + 60 && r > g + 40) sum++;
            else if (b > r + 60 && b > g + 40) sum--;
        }

        return sum;
    }
}
