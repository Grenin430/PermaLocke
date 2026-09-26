using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The gacha's capsule machine (§171): the dome tells the banner's odds, the ball climbs without giving the tier away,
/// and no frame of a pull can fail to draw.
/// </summary>
public sealed class CapsuleMachineTests
{
    private static readonly CapsuleBanner Pocho = new("POCHO", 1, [0.15, 0.60, 0.25, 0, 0], "75", false);
    private static readonly CapsuleBanner Bueno = new("BUENO", 3, [0, 0, 0.15, 0.60, 0.25], "300", true);

    /// <summary>The dome holds every ball that fits, in the banner's proportions, and none of a tier it cannot give.</summary>
    [Fact]
    public void The_dome_holds_the_banner_in_its_proportions()
    {
        var pocho = CapsuleMachineScene.DomeMix(Pocho.Odds, 20);
        var bueno = CapsuleMachineScene.DomeMix(Bueno.Odds, 23);

        Assert.Equal([3, 12, 5, 0, 0], pocho);
        Assert.Equal(23, bueno.Sum());
        Assert.Equal(0, bueno[0]);
        Assert.Equal(0, bueno[1]);
        Assert.True(bueno[3] > bueno[4] && bueno[4] > bueno[2]);
    }

    /// <summary>A banner with no odds at all still fills the dome, rather than leaving an empty machine.</summary>
    [Fact]
    public void A_banner_without_odds_still_fills_the_dome()
    {
        Assert.Equal(12, CapsuleMachineScene.DomeMix([0, 0, 0], 12)[0]);
    }

    /// <summary>
    /// The ball drops as the cheapest ball the banner holds and climbs at most twice, ending on the tier that came out.
    /// </summary>
    [Theory]
    [InlineData(0, 0, new[] { 0 })]
    [InlineData(0, 1, new[] { 0, 1 })]
    [InlineData(0, 2, new[] { 0, 1, 2 })]
    [InlineData(2, 4, new[] { 2, 3, 4 })]
    [InlineData(1, 4, new[] { 1, 2, 4 })]
    [InlineData(3, 3, new[] { 3 })]
    [InlineData(3, 2, new[] { 2 })]
    public void The_ball_climbs_from_the_cheapest_ball_of_the_banner(int lowest, int final, int[] expected)
    {
        Assert.Equal(expected, CapsulePlay.StepsFor(lowest, final));
    }

    /// <summary>
    /// Where the ball climbs comes from the pull's seed alone: one climb per step, on distinct wobbles, in order, and the
    /// same for any tier that needs as many climbs — the timing cannot tell a Tier 3 from a Tier 5.
    /// </summary>
    [Fact]
    public void Where_the_ball_climbs_says_nothing_about_the_tier()
    {
        for (var seed = -50; seed < 50; seed++)
        {
            for (var climbs = 0; climbs <= 2; climbs++)
            {
                var wobbles = CapsuleTimeline.UpgradeWobbles(climbs, seed);

                Assert.Equal(climbs, wobbles.Length);
                Assert.Equal(wobbles.Distinct().Order(), wobbles);
                Assert.All(wobbles, w => Assert.InRange(w, 0, CapsuleTimeline.Wobbles - 1));
                Assert.All(wobbles, w => Assert.True(CapsuleTimeline.UpgradeAt(w) < CapsuleTimeline.Open));
            }
        }
    }

    /// <summary>Every pull reaches the same moments at the same time: the machine's clock does not depend on the tier.</summary>
    [Fact]
    public void The_timeline_is_the_same_for_every_pull()
    {
        Assert.True(CapsuleTimeline.Exit < CapsuleTimeline.Settled);
        Assert.True(CapsuleTimeline.Settled <= CapsuleTimeline.WobbleStart);
        Assert.True(CapsuleTimeline.WobbleEnd <= CapsuleTimeline.Open);
        Assert.True(CapsuleTimeline.Open < CapsuleTimeline.Emerge && CapsuleTimeline.Emerge < CapsuleTimeline.Revealed);
    }

    /// <summary>
    /// A pull in a row runs faster up to the open and at its own pace from there, so the Pokémon comes out as it always
    /// does (§189); at normal speed the clock is the real one.
    /// </summary>
    [Fact]
    public void A_pull_in_a_row_hurries_only_up_to_the_open()
    {
        const double express = CapsuleTimeline.ExpressSpeed;

        Assert.Equal(3.2, CapsuleTimeline.Warp(3.2, 1), 9);
        Assert.Equal(12.5, CapsuleTimeline.Warp(12.5, 1), 9);
        Assert.Equal(1.0 * express, CapsuleTimeline.Warp(1.0, express), 9);
        Assert.Equal(CapsuleTimeline.Open, CapsuleTimeline.Warp(CapsuleTimeline.Open / express, express), 9);

        // Desde que se abre, el mismo tiempo que siempre hasta ver el Pokémon.
        var reveal = CapsuleTimeline.RealFor(CapsuleTimeline.Revealed, express) - CapsuleTimeline.RealFor(CapsuleTimeline.Open, express);
        Assert.Equal(CapsuleTimeline.Revealed - CapsuleTimeline.Open, reveal, 9);
        Assert.True(CapsuleTimeline.RealFor(CapsuleTimeline.Revealed, express) < CapsuleTimeline.Revealed / 2);

        for (var real = 0.0; real < 14; real += 0.37)
        {
            Assert.Equal(real, CapsuleTimeline.RealFor(CapsuleTimeline.Warp(real, express), express), 9);
            Assert.True(CapsuleTimeline.Warp(real + 0.01, express) > CapsuleTimeline.Warp(real, express));
        }
    }

    /// <summary>SALTAR goes to the ball about to open, then to the Pokémon out, and never back.</summary>
    [Fact]
    public void Skipping_goes_to_the_open_then_to_the_pokemon()
    {
        Assert.Equal(CapsuleTimeline.SkipTo, CapsuleTimeline.SkipTarget(0));
        Assert.Equal(CapsuleTimeline.SkipTo, CapsuleTimeline.SkipTarget(5.3));
        Assert.True(CapsuleTimeline.SkipTo > CapsuleTimeline.WobbleEnd && CapsuleTimeline.SkipTo < CapsuleTimeline.Open);
        Assert.Equal(CapsuleTimeline.Revealed, CapsuleTimeline.SkipTarget(CapsuleTimeline.SkipTo));
        Assert.Equal(CapsuleTimeline.Revealed, CapsuleTimeline.SkipTarget(CapsuleTimeline.Emerge));
        Assert.Equal(11.0, CapsuleTimeline.SkipTarget(11.0));

        foreach (var speed in new[] { 1, CapsuleTimeline.ExpressSpeed })
        {
            var play = new CapsulePlay([0, 2], null, false, false, 5, System.Diagnostics.Stopwatch.GetTimestamp(), speed);
            Assert.True(play.Skip());
            Assert.InRange(play.Elapsed, CapsuleTimeline.SkipTo, CapsuleTimeline.SkipTo + 0.5);
            Assert.True(play.Skip());
            Assert.InRange(play.Elapsed, CapsuleTimeline.Revealed, CapsuleTimeline.Revealed + 0.5);
            Assert.False(play.Skip());
        }
    }

    /// <summary>
    /// Every frame of a pull draws, for every tier, with or without a picture of the Pokémon, shiny or legendary, and
    /// so does the ball flying to the shelf afterwards. A frame that threw would cost the animation of a pull that is
    /// already written.
    /// </summary>
    [Fact]
    public void Every_frame_of_every_pull_draws()
    {
        var scene = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);
        var sprite = Sprite(40, 30);
        IReadOnlyList<CapsuleShelfItem> shelf = [new(sprite, 4), new(null, 0), new(sprite, 2)];

        for (var final = 0; final <= 4; final++)
        {
            var roll = new CapsuleRoll(CapsulePlay.StepsFor(Math.Max(0, final - 2), final), final % 2 == 0 ? sprite : null,
                Shiny: final is 1 or 4, Legendary: final == 4, Seed: final * 7919, Streak: 1 + (final * 3));

            for (var t = 0.0; t < 13; t += 0.05)
            {
                scene.Render(new CapsuleSceneState(final >= 3 ? Bueno : Pocho, t, roll, t, shelf), t, toScreen: false);
            }

            for (var s = 0.0; s < CapsuleMachineScene.DepartLength + 0.5; s += 0.05)
            {
                scene.Render(new CapsuleSceneState(Pocho, 5, null, 0, shelf, roll, s, s - CapsuleMachineScene.DepartLength),
                    20 + s, toScreen: false);
            }
        }

        Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
    }

    /// <summary>
    /// The party is for what deserves it (§189): once the Pokémon is out, a legendary rains gold coins and a plain pull
    /// does not; and before the ball opens both look the same apart from the ball itself.
    /// </summary>
    [Fact]
    public void Only_a_rare_pull_rains_gold()
    {
        static int Coins(byte[] bgra) =>
            Enumerable.Range(0, bgra.Length / 4).Count(i => bgra[(i * 4) + 2] == 0xFF && bgra[(i * 4) + 1] == 0xD2 && bgra[i * 4] == 0x4A);

        var sprite = Sprite(40, 30);
        var plain = new CapsuleRoll([2], sprite, false, false, 7);
        var legendary = new CapsuleRoll([2, 3, 4], sprite, false, true, 7);
        var scene = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);

        scene.Render(new CapsuleSceneState(Bueno, 5, plain, CapsuleTimeline.Revealed + 1.5, []), 3, toScreen: false);
        Assert.Equal(0, Coins(scene.Pixels));

        scene.Render(new CapsuleSceneState(Bueno, 5, legendary, CapsuleTimeline.Revealed + 1.5, []), 3, toScreen: false);
        Assert.True(Coins(scene.Pixels) > 20);

        // Antes de la primera subida posible, la sala no sabe nada: mismos píxeles para una y otra.
        var early = CapsuleTimeline.UpgradeAt(0) - 0.3;
        scene.Render(new CapsuleSceneState(Bueno, 5, plain with { Steps = [2, 3, 4] }, early, []), 3, toScreen: false);
        var before = scene.Pixels.ToArray();
        scene.Render(new CapsuleSceneState(Bueno, 5, legendary, early, []), 3, toScreen: false);
        Assert.Equal(before, scene.Pixels);
    }

    /// <summary>An empty run is a machine waiting in front of an empty shelf, not an error.</summary>
    [Fact]
    public void An_empty_run_draws_a_waiting_machine()
    {
        var scene = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);

        scene.Render(new CapsuleSceneState(Pocho, 0, null, 0, []), 0, toScreen: false);
        scene.Render(new CapsuleSceneState(Pocho, 5, null, 0, []), 5, toScreen: false);

        Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
    }

    /// <summary>Narrower than the design it is not squeezed, and taller it keeps the floor at the bottom.</summary>
    [Fact]
    public void The_scene_is_never_squeezed()
    {
        var narrow = new CapsuleMachineScene(200, 60);
        var tall = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth, 240);

        Assert.Equal(CapsuleMachineScene.DesignWidth, narrow.Width);
        Assert.Equal(CapsuleMachineScene.DesignRows, narrow.Height);
        Assert.Equal(240, tall.Height);
    }

    /// <summary>The machine is painted in the tier its banner mostly hands out, the colour its bar already uses.</summary>
    [Fact]
    public void The_machine_takes_the_colour_of_its_banner()
    {
        // Tier 2 (#5FA86F) y Tier 4 (#D75FA8), tal cual están en la paleta.
        static int Cells(byte[] bgra, byte r, byte g, byte b) =>
            Enumerable.Range(0, bgra.Length / 4).Count(i => bgra[(i * 4) + 2] == r && bgra[(i * 4) + 1] == g && bgra[i * 4] == b);

        var pocho = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);
        var bueno = new CapsuleMachineScene(CapsuleMachineScene.DesignWidth);

        pocho.Render(new CapsuleSceneState(Pocho, 5, null, 0, []), 1, toScreen: false);
        bueno.Render(new CapsuleSceneState(Bueno, 5, null, 0, []), 1, toScreen: false);

        Assert.True(Cells(pocho.Pixels, 0x5F, 0xA8, 0x6F) > 200);
        Assert.Equal(0, Cells(pocho.Pixels, 0xD7, 0x5F, 0xA8));
        Assert.True(Cells(bueno.Pixels, 0xD7, 0x5F, 0xA8) > 200);
    }

    /// <summary>The portals' balls are the machine's own drawing: one per tier, and all five different.</summary>
    [Fact]
    public void Each_tier_has_its_own_ball()
    {
        var icons = Enumerable.Range(0, 5).Select(tier =>
        {
            var icon = CapsuleMachineScene.BallIcon(tier, 14);
            var pixels = new byte[icon.PixelWidth * icon.PixelHeight * 4];
            icon.CopyPixels(pixels, icon.PixelWidth * 4, 0);
            return Convert.ToBase64String(pixels);
        }).ToList();

        Assert.Equal(5, icons.Distinct().Count());
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
