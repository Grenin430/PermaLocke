using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The trainer's room on JUGAR's cover (§169): it fits any panel, it never fails on an empty run, and what it shows
/// follows the run.
/// </summary>
public sealed class TrainerRoomTests
{
    private static TrainerRoomState State(bool champion = false, IReadOnlyList<RoomCrystal>? crystals = null) =>
        new(Hour: 12, Team: [], Fallen: [], FallenCount: 0, Crystals: crystals ?? [], Champion: champion, LevelCap: 20,
            Stickers: 5, StickersTarget: 25, InPc: 3, Tv: null);

    private static byte[] Row(TrainerRoomScene scene, int y) =>
        scene.Pixels.AsSpan(y * scene.Width * 4, scene.Width * 4).ToArray();

    /// <summary>A taller panel gets more wall above; the floor, and everything on it, stays at the bottom.</summary>
    [Fact]
    public void A_taller_panel_keeps_the_floor_at_the_bottom()
    {
        var designed = new TrainerRoomScene(TrainerRoomScene.DesignWidth);
        var tall = new TrainerRoomScene(TrainerRoomScene.DesignWidth, 100);

        designed.Render(State(), 0);
        tall.Render(State(), 0);

        Assert.Equal(100, tall.Height);

        for (var fromBottom = 1; fromBottom <= 30; fromBottom++)
        {
            Assert.Equal(Row(designed, designed.Height - fromBottom), Row(tall, tall.Height - fromBottom));
        }
    }

    /// <summary>
    /// JUGAR's play bar overlaps the cover by 22 pixels: the room sits above what it covers and the floor carries on
    /// underneath, so nobody's feet end up behind the bar.
    /// </summary>
    [Fact]
    public void What_the_play_bar_covers_is_bare_floor()
    {
        var designed = new TrainerRoomScene(TrainerRoomScene.DesignWidth);
        var covered = new TrainerRoomScene(TrainerRoomScene.DesignWidth, TrainerRoomScene.DesignRows + 8, covered: 8);

        designed.Render(State(), 0);
        covered.Render(State(), 0);

        // El cuarto entero, alfombra incluida, queda por encima de lo tapado...
        for (var y = 0; y < TrainerRoomScene.DesignRows; y++)
        {
            Assert.Equal(Row(designed, y), Row(covered, y));
        }

        // ...y debajo sigue la tarima, sin el dorado de la cenefa de la alfombra.
        for (var y = TrainerRoomScene.DesignRows; y < covered.Height; y++)
        {
            var row = Row(covered, y);
            Assert.DoesNotContain(Enumerable.Range(0, row.Length / 4), i => row[i * 4 + 2] == 0xD9 && row[i * 4 + 1] == 0xAE);
        }
    }

    /// <summary>Narrower than the design it is not squeezed: it keeps its width and the control centres it.</summary>
    [Fact]
    public void A_narrow_panel_does_not_squeeze_the_room()
    {
        var scene = new TrainerRoomScene(200, 40);

        Assert.Equal(TrainerRoomScene.DesignWidth, scene.Width);
        Assert.Equal(TrainerRoomScene.DesignRows, scene.Height);
    }

    /// <summary>A run with nothing yet — no team, no fallen, no crystals, no killcam — is an empty room, not an error.</summary>
    [Fact]
    public void An_empty_run_draws_an_empty_room()
    {
        var scene = new TrainerRoomScene(TrainerRoomScene.DesignWidth);

        scene.Render(State(), 0);
        scene.Render(State(), 3.7, still: true);

        Assert.Contains(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
    }

    /// <summary>The trophy on the television is only there once the league is won.</summary>
    [Fact]
    public void The_trophy_appears_only_for_a_champion()
    {
        static int GoldCells(byte[] bgra) =>
            Enumerable.Range(0, bgra.Length / 4).Count(i => bgra[i * 4 + 2] == 0xFF && bgra[i * 4 + 1] == 0xEB && bgra[i * 4] == 0x9C);

        var plain = new TrainerRoomScene(TrainerRoomScene.DesignWidth);
        var champion = new TrainerRoomScene(TrainerRoomScene.DesignWidth);

        plain.Render(State(champion: false), 0);
        champion.Render(State(champion: true), 0);

        Assert.True(GoldCells(champion.Pixels) > GoldCells(plain.Pixels));
    }

    /// <summary>A won crystal is drawn in its own colours; one still to come shows only its shape.</summary>
    [Fact]
    public void A_won_crystal_shows_its_colours_and_a_missing_one_only_its_shape()
    {
        // Un cristal de prueba de 8×8 de un rojo que no está en ningún otro sitio del cuarto.
        var bgra = new byte[8 * 8 * 4];
        for (var i = 0; i < 64; i++)
        {
            bgra[i * 4] = 0x11;
            bgra[i * 4 + 1] = 0x22;
            bgra[i * 4 + 2] = 0xF3;
            bgra[i * 4 + 3] = 0xFF;
        }

        var crystal = new RoomSprite(bgra, 8, 8);

        static int Red(byte[] pixels) =>
            Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4 + 2] == 0xF3 && pixels[i * 4 + 1] == 0x22);

        var won = new TrainerRoomScene(TrainerRoomScene.DesignWidth);
        var missing = new TrainerRoomScene(TrainerRoomScene.DesignWidth);

        won.Render(State(crystals: [new RoomCrystal(crystal, Won: true)]), 0);
        missing.Render(State(crystals: [new RoomCrystal(crystal, Won: false)]), 0);

        Assert.True(Red(won.Pixels) > 0);
        Assert.Equal(0, Red(missing.Pixels));
    }
}
