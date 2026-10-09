using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// The Mega Stone going into the bag (2026-10-09): the flash that is there at any frame rate, the plate that says what it is,
/// the vignette that shows on black, the glyph of each hue and the player carrying on.
/// </summary>
public sealed class MegaStoneStyleTests
{
    private const uint Red = 0xFFC03030;

    private static ItemScene.Item Stone(int seed = 7, uint tint = Red, string name = "BLAZIKENITA", int amount = 1)
    {
        var (icon, width, height) = ItemScene.Parcel();
        return new ItemScene.Item(icon, width, height, name, amount, ItemCategory.MegaStone, 0, tint, seed);
    }

    private static uint At(ItemScene scene, int gx, int gy)
    {
        var i = ((gy * scene.Width) + gx) * 4;
        return scene.Pixels[i] | ((uint)scene.Pixels[i + 1] << 8) | ((uint)scene.Pixels[i + 2] << 16) | ((uint)scene.Pixels[i + 3] << 24);
    }

    private static uint Bgra(byte r, byte g, byte b) => 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;

    // ============================================================ EL DESTELLO

    /// <summary>
    /// The two frames of white are seen at 30, 45 and 60 frames a second, whichever moment the first frame falls on: each of
    /// the two lasts a little over the time of a frame at 30, so that no frame rate above that skips one.
    /// </summary>
    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void The_flash_gives_two_visible_frames_whatever_the_frame_rate(int framesPerSecond)
    {
        var item = Stone();
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        var impact = MegaStoneStyle.ImpactAt(ItemStyles.For(ItemCategory.MegaStone).Phases(item));
        var step = 1.0 / framesPerSecond;

        // The disc of white covers a cell that the ring does not, and the ring one that the disc does not.
        var white = Bgra(0xFA, 0xF8, 0xFF);

        for (var shift = 0; shift < 10; shift++)
        {
            var disc = 0;
            var ring = 0;

            for (var t = impact - 0.25 + (shift * step / 10); t < impact + 0.25; t += step)
            {
                scene.Render(item, t);
                if (At(scene, MegaStoneStyle.Cx - 18, (int)MegaStoneStyle.RestY) == white) disc++;
                if (At(scene, MegaStoneStyle.Cx - 30, (int)MegaStoneStyle.RestY) == white) ring++;
            }

            Assert.True(disc >= 1, $"{framesPerSecond} fps, desfase {shift}: el disco blanco no salió en ningún fotograma.");
            Assert.True(ring >= 1, $"{framesPerSecond} fps, desfase {shift}: el anillo blanco no salió en ningún fotograma.");
        }
    }

    [Fact]
    public void The_mark_of_impact_is_there_too_at_thirty_frames_a_second()
    {
        var item = Stone();
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        var phases = ItemStyles.For(ItemCategory.MegaStone).Phases(item);
        var landing = MegaStoneStyle.ImpactAt(phases) + 0.26 + 0.32;
        var pale = ItemTint.Shade(Red, 0, 0.90, 0.6);
        var white = Bgra(0xFA, 0xF8, 0xFF);
        var step = 1.0 / 30;

        for (var shift = 0; shift < 10; shift++)
        {
            var seen = 0;

            for (var t = landing - 0.2 + (shift * step / 10); t < landing + 0.2; t += step)
            {
                scene.Render(item, t);
                var cell = At(scene, MegaStoneStyle.Cx + 5, 90);
                if (cell == white || cell == pale) seen++;
            }

            Assert.True(seen >= 2, $"desfase {shift}: la marca de impacto dio {seen} fotogramas a 30 por segundo.");
        }
    }

    // ============================================================ LA PLACA

    [Fact]
    public void The_plate_says_megapiedra_and_the_amount_and_never_goes_outside_of_the_scene()
    {
        var gold = Bgra(0xFF, 0xDC, 0x7A);

        // «MEGAPIEDRA» is 39 cells wide: the amount starts where it ends, 115 columns in, and not where «A LA MOCHILA» would.
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        scene.Render(Stone(), 3.0);
        var found = Enumerable.Range(58, 5).Any(y => At(scene, 115, y) == gold);
        Assert.True(found, "la cantidad no empieza justo detrás de «MEGAPIEDRA»");

        // A name that does not fit is cut, and a plate with the most it can have still fits in the scene.
        foreach (var name in new[] { "CHARIZARDITA X", "UN NOMBRE MUCHO MAS LARGO QUE LA PLACA", "AAAAAAAAAAAAAAAAAAAAAAAAAAAA" })
        {
            var item = Stone(1, Red, name, 12);
            for (var t = 0.0; t < 4.5; t += 1 / 60.0)
            {
                scene.Render(item, t);
                Assert.True(scene.Overdraw == 0, $"{name}, t={t:0.00}: la placa se sale de la escena.");
            }
        }
    }

    [Fact]
    public void The_plate_is_opaque_from_the_stone_coming_out_to_the_bag_closing()
    {
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);

        for (var t = 1.0; t < 3.9; t += 1 / 30.0)
        {
            scene.Render(Stone(), t);

            // The coloured edge of the plate, three cells in and halfway down it.
            Assert.Equal(0xFFu, At(scene, 67, 56) >> 24);
        }
    }

    // ============================================================ LA VIÑETA

    /// <summary>
    /// What the vignette is for is to be seen where the game is black: its contour of dots has to light cells that a dark colour
    /// would not show, in the stone's colour, away from the plate and from the stone.
    /// </summary>
    [Fact]
    public void The_vignette_has_dots_bright_enough_to_be_seen_on_black()
    {
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        scene.Render(Stone(), 1.7);

        var visible = 0;
        for (var gy = 0; gy < MegaStoneStyle.SceneHeight; gy++)
        {
            for (var gx = 0; gx < ItemScene.SceneWidth; gx++)
            {
                var dx = (gx + 0.5 - 75) / 75;
                var dy = (gy + 0.5 - 56) / 56;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var cell = At(scene, gx, gy);
                var strongest = Math.Max((cell >> 16) & 0xFF, Math.Max((cell >> 8) & 0xFF, cell & 0xFF));

                // On the ring of the contour, outside the plate (columns 60 and over, rows 40 to 72), and lit.
                var onPlate = gx >= 60 && gy is >= 40 and <= 72;
                if (distance is >= 0.66 and < 0.72 && !onPlate && cell >> 24 == 0xFF && strongest >= 60) visible++;
            }
        }

        Assert.True(visible >= 40, $"solo {visible} puntos del contorno se verían sobre negro");
    }

    // ============================================================ EL TIPO

    [Theory]
    [InlineData(0xFFD03020u, 0)]   // red: the sun
    [InlineData(0xFFE0B020u, 0)]   // yellow
    [InlineData(0xFF30B040u, 1)]   // green: the sprout
    [InlineData(0xFF2060D0u, 2)]   // blue: the rings
    [InlineData(0xFF40C0D0u, 2)]   // cyan
    [InlineData(0xFFA040D0u, 3)]   // violet: the crystal
    [InlineData(0xFF808080u, 4)]   // grey: the plain one
    [InlineData(0u, 4)]            // no colour at all
    public void The_family_of_the_glyph_comes_from_the_hue_of_the_stone(uint tint, int family)
    {
        Assert.Equal(family, MegaStoneStyle.Family(tint));
    }

    [Fact]
    public void Each_family_draws_its_own_glyph()
    {
        var shapes = new List<string>();

        foreach (var tint in new[] { 0xFFD03020u, 0xFF30B040u, 0xFF2060D0u, 0xFFA040D0u, 0u })
        {
            var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
            var item = Stone(11, tint);
            var at = MegaStoneStyle.ImpactAt(ItemStyles.For(ItemCategory.MegaStone).Phases(item)) + 0.40;
            scene.Render(item, at);

            // The glyph's box above the stone: the shape of what is lit there, not its colours.
            var mask = string.Concat(Enumerable.Range(6, 30).SelectMany(gy => Enumerable.Range(30, 38).Select(gx => At(scene, gx, gy) >> 24 == 0xFF ? '#' : '.')));
            shapes.Add(mask);
        }

        Assert.Equal(5, shapes.Distinct().Count());
    }

    [Fact]
    public void One_seed_in_four_has_three_strands_and_the_rest_two()
    {
        var three = Enumerable.Range(0, 400).Count(seed => MegaStoneStyle.StrandsFor(ItemScene.SeedFor(656, seed)) == 3);

        Assert.InRange(three, 60, 140);
        Assert.All(Enumerable.Range(0, 50), seed => Assert.InRange(MegaStoneStyle.StrandsFor(ItemScene.SeedFor(664, seed)), 2, 3));
    }

    // ============================================================ EL JUGADOR SIGUE

    [Fact]
    public void What_is_held_when_it_begins_does_not_count_and_a_new_press_does()
    {
        var edges = new InputEdges(4);
        edges.Reset([false, true, false, false]);

        Assert.False(edges.Rose([false, true, false, false]));   // still held
        Assert.False(edges.Rose([false, false, false, false]));  // let go
        Assert.True(edges.Rose([false, true, false, false]));    // pressed again
        Assert.False(edges.Rose([false, true, false, false]));   // held, not new
        Assert.True(edges.Rose([false, true, true, false]));     // another one
    }

    [Fact]
    public void Nothing_counts_while_the_game_is_not_the_window_in_front()
    {
        var watch = new GameInputWatch(() => false);
        watch.Begin();

        Assert.False(watch.Advanced());
        Assert.False(watch.Advanced());
    }

    [Fact]
    public void Leaving_fades_the_scene_out_to_nothing_while_it_goes_on_playing()
    {
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        var item = Stone();

        int Lit() => Enumerable.Range(0, scene.Width * scene.Height).Count(i => scene.Pixels[(i * 4) + 3] != 0);

        scene.Render(item, 2.0, 1);
        var whole = Lit();
        scene.Render(item, 2.0, 0.5);
        var half = Lit();
        scene.Render(item, 2.0, 0);
        var gone = Lit();

        Assert.True(whole > half, "a half faded scene has less drawn than a whole one");
        Assert.True(half > 0);
        Assert.Equal(0, gone);
        Assert.InRange(ItemScene.ExitSeconds, 0.10, 0.25);
    }
}
