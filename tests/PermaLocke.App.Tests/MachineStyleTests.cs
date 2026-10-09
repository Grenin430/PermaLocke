using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>A TM or an HM going into the bag (2026-10-09): scan, rings of data, the colour of the type of the move.</summary>
public sealed class MachineStyleTests
{
    private static ItemScene.Item Disc(int kind = 9, int power = 0, string? detail = "FUEGO", uint tint = 0xFF808080, string name = "MT01 · HIPERRAYO")
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, name, 1, ItemCategory.Machine, power, tint, 12, detail, kind);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    [Fact]
    public void The_scene_is_the_colour_of_the_type_of_the_move_and_the_plate_says_it()
    {
        var fire = MachineStyle.ColourOf(Disc(9));
        var water = MachineStyle.ColourOf(Disc(10));
        var fireColour = TypeColours.Of(9);

        Assert.NotEqual(fire, water);
        Assert.Equal(ItemCanvas.Bgra(fireColour.R, fireColour.G, fireColour.B), fire);

        // Without a type, the colour of the icon.
        Assert.Equal(0xFF808080u, MachineStyle.ColourOf(Disc(-1)));

        Assert.Equal("MT FUEGO", MachineStyle.Caption(Disc(9)));
        Assert.Equal("MO AGUA", MachineStyle.Caption(Disc(10, 1, "AGUA")));
        Assert.Equal("MT", MachineStyle.Caption(Disc(-1, 0, null)));
    }

    [Fact]
    public void Two_types_play_in_two_colours()
    {
        var fire = new ItemScene(1, 64);
        var water = new ItemScene(1, 64);
        fire.Render(Disc(9), 1.1);
        water.Render(Disc(10), 1.1);

        Assert.NotEqual(fire.Pixels, water.Pixels);
    }

    [Fact]
    public void The_line_of_the_scan_crosses_the_bag_and_the_amount_follows_the_caption()
    {
        var scene = new ItemScene(1, 64);

        // Halfway through the scan the bright cell of the line is where the sweep is.
        var phases = ItemStyles.For(ItemCategory.Machine).Phases(Disc());
        scene.Render(Disc(), phases.ClimaxAt + (0.5 * phases.Climax));
        var scanY = 38 + (int)Math.Round(20 * ((0.5 - 0.35) / 0.53));
        Assert.Equal(0xFFFAF8FFu, At(scene, 24, scanY));

        // «MT FUEGO» is 31 cells: the amount starts 4 cells after it, 93 columns in.
        scene.Render(Disc(), 1.6);
        var gold = 0xFFFFDC7Au;
        Assert.True(Enumerable.Range(32, 5).Any(y => At(scene, 93, y) == gold), "la cantidad no sigue al pie de la placa");
    }

    [Fact]
    public void An_HM_has_four_rings_and_its_bars_and_a_TM_does_not()
    {
        var tm = new ItemScene(1, 64);
        var hm = new ItemScene(1, 64);
        var phases = ItemStyles.For(ItemCategory.Machine).Phases(Disc());
        var at = phases.ClimaxAt + (0.6 * phases.Climax);
        tm.Render(Disc(9, 0), at);
        hm.Render(Disc(9, 1, "FUEGO"), at);

        Assert.NotEqual(tm.Pixels, hm.Pixels);
    }
}
