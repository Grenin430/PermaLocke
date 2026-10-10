using PermaLocke.App.Services;
using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>
/// The Mega Stone going into the bag (2026-10-09): the flash that is there at any frame rate, the plate that says what it is,
/// the background that is left alone, the glyph of each hue and the player carrying on.
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

    // ============================================================ EL FONDO

    /// <summary>
    /// The scene does not light or darken the background (1.0.16): away from the bag, the stone, its rings and the plate there
    /// is nothing at all at any moment, whatever the seed and the colour of the stone. The vignette of dots that tinted the
    /// edges of the scene in the stone's colour is gone.
    /// </summary>
    [Fact]
    public void The_background_is_left_alone_at_every_moment_of_the_scene()
    {
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        var length = ItemStyles.For(ItemCategory.MegaStone).Phases(Stone()).Length;

        foreach (var tint in new[] { Red, 0xFF2060D0u, 0xFF30B040u, 0u })
        {
            foreach (var seed in new[] { 1, 7, 99 })
            {
                var item = Stone(seed, tint);

                for (var t = 0.0; t < length; t += 1 / 30.0)
                {
                    scene.Render(item, t);

                    // The right of the scene, above and below the plate, further out than any ring reaches.
                    for (var gy = 0; gy < MegaStoneStyle.SceneHeight; gy++)
                    {
                        if (gy is >= 36 and < 84) continue;

                        for (var gx = 100; gx < ItemScene.SceneWidth; gx++)
                        {
                            Assert.True(At(scene, gx, gy) >> 24 == 0, $"tinte {tint:X8}, semilla {seed}, t={t:0.00}: hay algo en el fondo ({gx}, {gy}).");
                        }
                    }
                }
            }
        }
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

    // ============================================================ TIPO Y VARIACIÓN

    [Fact]
    public void The_family_comes_from_the_type_the_cartridge_gave_and_from_the_tint_when_it_gave_none()
    {
        var fireButBlue = Stone(1, 0xFF2060D0) with { Kind = 9 };   // Fire, with the icon of a blue stone
        var noType = fireButBlue with { Kind = -1 };

        Assert.Equal(0, MegaStoneStyle.FamilyOf(fireButBlue));
        Assert.Equal(2, MegaStoneStyle.FamilyOf(noType));

        // Every one of the eighteen types has a family.
        Assert.All(Enumerable.Range(0, 18), type => Assert.InRange(MegaStoneStyle.FamilyOf(Stone() with { Kind = type }), 0, 4));
    }

    [Fact]
    public void A_stone_the_catalog_knows_the_type_of_carries_it()
    {
        var pockets = new int[1024];
        var catalog = new ItemCatalog(pockets, new HashSet<int> { 664, 1011 }, new Dictionary<int, int> { [664] = 9 });

        Assert.Equal(new ItemClass(ItemCategory.MegaStone, 0, 9), catalog.Classify(664));
        Assert.Equal(new ItemClass(ItemCategory.MegaStone, 0, -1), catalog.Classify(1011));
    }

    [Fact]
    public void Some_seeds_play_another_score_and_the_beat_of_a_peak_can_be_seen_in_one_frame()
    {
        var variants = Enumerable.Range(0, 600).Select(n => MegaStoneStyle.VariantFor(ItemScene.SeedFor(656, n))).ToArray();

        Assert.Equal(5, variants.Distinct().Count());
        Assert.InRange(variants.Count(v => v >= 2), 150, 260);

        // A frame on the peak of a beat has the ring of dots: more lit cells round the stone than a frame between beats.
        var item = Stone();
        var scene = new ItemScene(1, MegaStoneStyle.SceneHeight);
        var lit = Enumerable.Range(0, 400).Select(n =>
        {
            scene.Render(item, 1.3 + (n * 0.001));
            var ring = 0;
            for (var gy = 30; gy < 76; gy++)
            {
                for (var gx = 26; gx < 70; gx++)
                {
                    var d = Math.Sqrt(((gx - 48) * (gx - 48)) + ((gy - 52) * (gy - 52)));
                    if (d is >= 13 and < 31 && At(scene, gx, gy) >> 24 == 0xFF) ring++;
                }
            }

            return ring;
        }).ToArray();

        Assert.True(lit.Max() > lit.Min() + 20, "the ring of the beat does not show");
    }
}
