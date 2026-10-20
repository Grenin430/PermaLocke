using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

public sealed class CapVisitorLabTests
{
    [Fact]
    public void A_one_pixel_eye_is_in_the_shrunk_drawing_wherever_it_falls_on_the_grid()
    {
        // The eyes blinked on some swimmers and fliers as they moved (2026-10-20): wherever a lone dark pixel is in a cell, the drawing shrunk
        // to two thirds shows it.
        const int Cell = 9;
        var shrunk = (int)Math.Ceiling(Cell * 2.0 / 3.0);
        for (var ey = 1; ey < Cell - 1; ey++)
        {
            for (var ex = 1; ex < Cell - 1; ex++)
            {
                var bgra = new byte[Cell * Cell * 4];
                for (var i = 0; i < Cell * Cell; i++) (bgra[i * 4], bgra[(i * 4) + 1], bgra[(i * 4) + 2], bgra[(i * 4) + 3]) = ((byte)200, (byte)220, (byte)240, (byte)255);
                var eye = ((ey * Cell) + ex) * 4;
                (bgra[eye], bgra[eye + 1], bgra[eye + 2]) = ((byte)10, (byte)10, (byte)10);
                var sheet = new ToastPixels.SpritePixels(bgra, Cell, Cell);
                var seen = false;
                for (var oy = 0; oy < shrunk && !seen; oy++) for (var ox = 0; ox < shrunk && !seen; ox++) seen = CapVisitors.Pick(sheet, 0, 0, Cell, ox, oy) == eye;
                Assert.True(seen, $"the eye at ({ex}, {ey}) is lost");
            }
        }
    }

    [Fact]
    public void A_batch_with_four_fliers_keeps_every_flier_in_the_sky_preview()
    {
        int[] batch = [779, 398, 149, 6, 381];
        var selected = Enumerable.Range(0, CapVisitors.Lanes.Length)
            .Where(i => CapVisitors.Lanes[i].Flies)
            .SelectMany(i => CapVisitors.ForcedLaneSpecies(batch, i)).ToArray();
        Assert.Equal(new[] { 6, 149, 381, 398 }, selected.Order().ToArray());
        Assert.DoesNotContain(779, selected);
    }

    [Fact]
    public void Previous_batches_keep_their_original_assignments_and_empty_habitats_stay_empty()
    {
        int[] batch = [318, 119, 22, 149, 380];
        var selected = Enumerable.Range(0, CapVisitors.Lanes.Length)
            .SelectMany(i => CapVisitors.ForcedLaneSpecies(batch, i)).ToArray();
        Assert.Equal(new[] { 318, 119, 318, 22, 149, 380 }, selected);
        Assert.Empty(CapVisitors.ForcedLaneSpecies([779], 3));
        Assert.Empty(CapVisitors.ForcedLaneSpecies([381], 0));
    }
}
