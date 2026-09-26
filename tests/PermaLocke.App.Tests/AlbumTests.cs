using System.Windows.Media;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The album (§186, §187): how the boxes are paged, how a card is drawn — its finish by rarity, fallen, shiny, an egg —,
/// which pocket or tab is under the mouse, the page turning and the card in the hand. Pure drawing, no window needed.
/// </summary>
public sealed class AlbumTests
{
    private static RoomSprite Blob()
    {
        var pixels = new byte[40 * 30 * 4];
        for (var y = 6; y < 28; y++)
        {
            for (var x = 10; x < 30; x++)
            {
                var at = ((y * 40) + x) * 4;
                pixels[at] = 0x40;
                pixels[at + 1] = 0x90;
                pixels[at + 2] = 0xF0;
                pixels[at + 3] = 255;
            }
        }

        return new RoomSprite(pixels, 40, 30);
    }

    private static TcgCard Card(bool shiny = false, bool fallen = false, bool egg = false, int type = 9, uint seed = 7,
        RoomSprite? sprite = null, int rarity = 2) => new(
        "Charizard", "Charizard", 6, "Fase 2", 50, 150, [type, 2],
        [new TcgMove("Lanzallamas", 9, 90, 100, 15, "Especial"), new TcgMove("Puño Trueno", 12, 75, 100, 15, "Físico")],
        "Mar Llamas", "Firme", 1, 3, "Restos", "Ruta 1", 5, rarity, false, shiny, egg, fallen, sprite ?? Blob(),
        [150, 120, 90, 80, 85, 110], [31, 20, 5, 31, 14, 28], [0, 252, 0, 0, 4, 252], seed);

    private static int Solid(CellCanvas canvas)
    {
        var count = 0;
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                if (canvas.IsSet(x, y)) count++;
            }
        }

        return count;
    }

    // ============================================================================================== PAGING

    /// <summary>A box of 30 is four 3×3 pages or two 4×4 ones; the party of six still gets a whole spread.</summary>
    [Theory]
    [InlineData(30, 9, 4)]
    [InlineData(30, 16, 2)]
    [InlineData(6, 9, 2)]
    [InlineData(6, 16, 2)]
    [InlineData(0, 9, 2)]
    public void Pages_come_two_by_two(int slots, int perPage, int pages)
    {
        Assert.Equal(pages, AlbumPaging.PagesOf(slots, perPage));
    }

    /// <summary>Every binder starts on a new spread, so two boxes never share one.</summary>
    [Fact]
    public void Every_box_starts_its_own_spread()
    {
        var spreads = AlbumPaging.Spreads([6, 30, 30], 9);

        Assert.Equal(
            [new AlbumSpreadPlace(0, 0), new AlbumSpreadPlace(1, 0), new AlbumSpreadPlace(1, 2), new AlbumSpreadPlace(2, 0), new AlbumSpreadPlace(2, 2)],
            spreads);
    }

    /// <summary>A pocket holds the slot of its place in the box, gaps and the end of the box included.</summary>
    [Fact]
    public void A_page_keeps_the_order_and_the_gaps_of_the_box()
    {
        var one = Card();
        var two = Card(seed: 9);
        var slots = new TcgCard?[30];
        slots[9] = one;
        slots[29] = two;

        var second = AlbumPaging.Page(slots, 1, 9);
        var fourth = AlbumPaging.Page(slots, 3, 9);

        Assert.Same(one, second[0]);
        Assert.All(second.Skip(1), Assert.Null);
        Assert.Same(two, fourth[2]);
        Assert.Null(fourth[3]);
        Assert.Equal(9, fourth.Length);
    }

    // ============================================================================================== CARDS

    /// <summary>Every type draws, the unknown one included, in both designs and on the back, without a hole.</summary>
    [Fact]
    public void Every_type_draws_a_whole_card()
    {
        for (var type = -1; type <= 18; type++)
        {
            var card = Card(type: type);
            foreach (var layout in new[] { TcgLayout.Full, TcgLayout.Mini })
            {
                var render = TcgCardArt.Render(card, layout);
                var (width, height) = TcgCardArt.SizeOf(layout);

                Assert.Equal(width, render.Canvas.Width);
                Assert.Equal(height, render.Canvas.Height);

                // Todo menos las cuatro esquinas redondeadas.
                Assert.Equal((width * height) - 4, Solid(render.Canvas));
            }

            Assert.Equal((TcgCardArt.FullWidth * TcgCardArt.FullHeight) - 4, Solid(TcgCardArt.Render(card, TcgLayout.Full, back: true).Canvas));
        }
    }

    /// <summary>The same card draws the same cells: nothing but its data and its PID decides a pixel.</summary>
    [Fact]
    public void The_same_card_draws_the_same_cells()
    {
        var first = TcgCardArt.Render(Card(fallen: true, seed: 31), TcgLayout.Full).Canvas.Bgra;
        var again = TcgCardArt.Render(Card(fallen: true, seed: 31), TcgLayout.Full).Canvas.Bgra;

        Assert.Equal(first, again);
    }

    /// <summary>
    /// A fallen card is half burnt: a good part of it is gone, not all of it, with embers along the edge; and it is
    /// drained of colour, so no cell keeps the full red of a Fire card.
    /// </summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(77u)]
    public void A_fallen_card_is_half_burnt(uint seed)
    {
        var whole = (TcgCardArt.FullWidth * TcgCardArt.FullHeight) - 4;
        var render = TcgCardArt.Render(Card(fallen: true, seed: seed), TcgLayout.Full);
        var left = Solid(render.Canvas);

        Assert.InRange(left, whole * 0.5, whole * 0.85);
        Assert.NotEmpty(render.Embers);
        Assert.Equal(TcgFinish.Plain, render.Finish);
        Assert.All(render.Regions, region => Assert.Equal(TcgRegion.None, region));
    }

    /// <summary>The burn of the back is the front's seen from behind: the side it eats is the other one.</summary>
    [Fact]
    public void The_back_burns_on_the_other_side()
    {
        var card = Card(fallen: true, seed: 0);
        var front = TcgCardArt.Render(card, TcgLayout.Full).Canvas;
        var back = TcgCardArt.Render(card, TcgLayout.Full, back: true).Canvas;

        // La semilla 0 quema desde abajo a la derecha: por delante falta esa esquina y por detrás la otra.
        Assert.False(front.IsSet(front.Width - 3, front.Height - 3));
        Assert.True(front.IsSet(2, front.Height - 3));
        Assert.False(back.IsSet(2, back.Height - 3));
        Assert.True(back.IsSet(back.Width - 3, back.Height - 3));
    }

    /// <summary>A shiny is polychrome foil all over and its shine moves: two moments draw two different frames.</summary>
    [Fact]
    public void A_shiny_shines_and_the_shine_moves()
    {
        var render = TcgCardArt.Render(Card(shiny: true), TcgLayout.Full);
        Assert.Equal(TcgFinish.Polychrome, render.Finish);
        Assert.Contains(TcgRegion.Rim, render.Regions);

        var early = render.Canvas.Clone();
        var later = render.Canvas.Clone();
        TcgCardArt.Animate(render, early, 0, 0, 0.5, 7);
        TcgCardArt.Animate(render, later, 0, 0, 1.3, 7);

        Assert.NotEqual(early.Bgra, later.Bgra);
    }

    /// <summary>A common card has nothing that moves: no foil, no embers.</summary>
    [Fact]
    public void A_common_card_is_still()
    {
        var render = TcgCardArt.Render(Card(rarity: 0), TcgLayout.Full);

        Assert.Equal(TcgFinish.Plain, render.Finish);
        Assert.False(render.IsLive);
        Assert.Empty(render.Embers);
        Assert.False(Card(rarity: 0).IsLive);
    }

    /// <summary>
    /// The finish follows the ★ of the card, like a real print run: ● and ◆ plain, ★ holo, silver ★ reverse holo, gold
    /// ★ gold; a shiny is always polychrome, and the fallen lose their finish in the fire.
    /// </summary>
    [Theory]
    [InlineData(0, false, false, TcgFinish.Plain)]
    [InlineData(1, false, false, TcgFinish.Plain)]
    [InlineData(2, false, false, TcgFinish.Holo)]
    [InlineData(3, false, false, TcgFinish.Reverse)]
    [InlineData(4, false, false, TcgFinish.Gold)]
    [InlineData(-1, false, false, TcgFinish.Plain)]
    [InlineData(0, true, false, TcgFinish.Polychrome)]
    [InlineData(4, true, false, TcgFinish.Polychrome)]
    [InlineData(4, false, true, TcgFinish.Plain)]
    [InlineData(4, true, true, TcgFinish.Plain)]
    public void The_finish_follows_the_rarity(int rarity, bool shiny, bool fallen, TcgFinish finish)
    {
        var card = Card(rarity: rarity, shiny: shiny, fallen: fallen);

        Assert.Equal(finish, TcgCardArt.FinishOf(card));
        Assert.Equal(finish, TcgCardArt.Render(card, TcgLayout.Full).Finish);
    }

    /// <summary>
    /// A card knows which of its cells are picture, panel and border, and the text on it is none of them: the light of
    /// a reverse holo never washes out a name.
    /// </summary>
    [Fact]
    public void The_foil_never_covers_the_text()
    {
        var render = TcgCardArt.Render(Card(rarity: 3), TcgLayout.Full);
        var ink = System.Windows.Media.Color.FromRgb(0x1C, 0x16, 0x26);
        var canvas = render.Canvas;

        Assert.Contains(TcgRegion.Art, render.Regions);
        Assert.Contains(TcgRegion.Panel, render.Regions);
        Assert.Contains(TcgRegion.Rim, render.Regions);

        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                var colour = canvas.At(x, y);
                if (colour.R == ink.R && colour.G == ink.G && colour.B == ink.B)
                {
                    Assert.Equal(TcgRegion.None, render.Regions[(y * canvas.Width) + x]);
                }
            }
        }
    }

    /// <summary>A reverse holo's light passes over its panel only: two moments differ on the panel and never on the picture.</summary>
    [Fact]
    public void A_reverse_holo_shines_on_the_panel_and_not_on_the_picture()
    {
        var render = TcgCardArt.Render(Card(rarity: 3), TcgLayout.Full);
        var width = render.Canvas.Width;
        var changedPanel = false;

        foreach (var light in new[] { 0.2, 0.5, 0.8 })
        {
            var frame = render.Canvas.Clone();
            TcgCardArt.Animate(render, frame, 0, 0, 1.0, 7, light);

            for (var i = 0; i < render.Regions.Length; i++)
            {
                var same = frame.Bgra.AsSpan(i * 4, 4).SequenceEqual(render.Canvas.Bgra.AsSpan(i * 4, 4));
                if (render.Regions[i] == TcgRegion.Art)
                {
                    Assert.True(same, $"La luz del reverse holo tocó el dibujo en la celda {i % width},{i / width}.");
                }

                changedPanel |= !same && render.Regions[i] == TcgRegion.Panel;
            }
        }

        Assert.True(changedPanel);
    }

    /// <summary>An egg is a card face down: the album's back, whatever the Pokémon inside.</summary>
    [Fact]
    public void An_egg_is_face_down()
    {
        var egg = TcgCardArt.Render(Card(egg: true, type: 11), TcgLayout.Mini).Canvas.Bgra;
        var back = TcgCardArt.RenderBack(TcgLayout.Mini).Canvas.Bgra;

        Assert.NotEqual(egg, back);
        Assert.Equal(egg.Length, back.Length);
        Assert.Equal(egg.Take(egg.Length / 2), back.Take(back.Length / 2));
    }

    /// <summary>A card without an icon still draws, with a question mark where the picture would be.</summary>
    [Fact]
    public void A_card_without_icon_still_draws()
    {
        var card = Card() with { Sprite = null };

        Assert.Equal((TcgCardArt.FullWidth * TcgCardArt.FullHeight) - 4, Solid(TcgCardArt.Render(card, TcgLayout.Full).Canvas));
    }

    /// <summary>Text cut to fit ends in a point and fits; text that fits is left alone.</summary>
    [Fact]
    public void Long_names_are_cut_to_fit()
    {
        var cut = TcgCardArt.SmallTrim("Golpe Cabeza Zen Fuerte", 40);

        Assert.EndsWith(".", cut);
        Assert.True(TcgCardArt.SmallWidth(cut) <= 40);
        Assert.Equal("LANZALLAMAS", TcgCardArt.SmallTrim("Lanzallamas", 60));
    }

    // ============================================================================================== SCENE

    /// <summary>
    /// The first and last pockets of both pages are found where they are drawn, with and without the cover; the spine,
    /// the cover and the page header are no pocket.
    /// </summary>
    [Theory]
    [InlineData(TcgLayout.Full, true)]
    [InlineData(TcgLayout.Full, false)]
    [InlineData(TcgLayout.Mini, true)]
    [InlineData(TcgLayout.Mini, false)]
    public void The_mouse_finds_the_pocket_under_it(TcgLayout layout, bool dressed)
    {
        var scene = new AlbumScene(layout, dressed);
        var last = (scene.PocketsPerPage * 2) - 1;

        foreach (var pocket in new[] { 0, scene.PocketsPerPage - 1, scene.PocketsPerPage, last })
        {
            var (x, y) = scene.PocketInScene(pocket);
            Assert.Equal(pocket, scene.PocketAt(x + 3, y + 3));
        }

        Assert.Equal(-1, scene.PocketAt(scene.LeftPageX + scene.PageWidth + 5, scene.Height / 2));
        Assert.Equal(-1, scene.PocketAt(-1, 0));
        Assert.Equal(-1, scene.PocketAt(scene.LeftPageX + 2, scene.PageTop + 2));
        Assert.Equal((scene.Width, scene.Height), AlbumScene.SizeOf(layout, dressed));
    }

    /// <summary>Each box's tab is found on the album's edge; without the cover there are no tabs.</summary>
    [Fact]
    public void The_mouse_finds_the_tabs_on_the_edge()
    {
        var dressed = new AlbumScene(TcgLayout.Full, dressed: true);
        var bare = new AlbumScene(TcgLayout.Full, dressed: false);
        var hits = Enumerable.Range(0, dressed.Height).Select(y => dressed.TabAt(dressed.Width - 3, y, 5)).Where(t => t >= 0).Distinct().ToList();

        Assert.Equal([0, 1, 2, 3, 4], hits);
        Assert.Equal(-1, dressed.TabAt(dressed.Width / 2, 20, 5));
        Assert.Equal(-1, bare.TabAt(bare.Width - 3, 20, 5));
    }

    /// <summary>
    /// The album fits GRANDE at two screen pixels per cell: without the band of the game on top, the section has about
    /// 1080 × 640 pixels, and both sizes of card fit dressed in 540 × 320 cells.
    /// </summary>
    [Theory]
    [InlineData(TcgLayout.Full)]
    [InlineData(TcgLayout.Mini)]
    public void The_album_fits_grande_at_two_pixels_per_cell(TcgLayout layout)
    {
        var (width, height) = AlbumScene.SizeOf(layout, dressed: true);

        Assert.True(width * 2 <= 1080, $"{width} celdas de ancho");
        Assert.True(height * 2 <= 640, $"{height} celdas de alto");
    }

    /// <summary>
    /// A spread with every kind of card draws at any moment of a page turning, both ways, and the whole scene is
    /// painted: the pages cover it, burnt cards show their pocket and not a hole to the window.
    /// </summary>
    [Theory]
    [InlineData(TcgLayout.Full)]
    [InlineData(TcgLayout.Mini)]
    public void A_spread_draws_at_any_moment_of_a_turn(TcgLayout layout)
    {
        var scene = new AlbumScene(layout, dressed: false);
        TcgCard?[] Pockets(uint seed) =>
        [
            .. Enumerable.Range(0, scene.PocketsPerPage).Select(i => i % 4 == 3 ? null
                : Card(shiny: i % 4 == 1, fallen: i % 4 == 2, egg: i == 0, type: i % 18, seed: seed + (uint)i))
        ];

        var from = new AlbumSpread(new AlbumPage(Pockets(1)), new AlbumPage(Pockets(2)), layout, 0);
        var to = new AlbumSpread(new AlbumPage(Pockets(3)), new AlbumPage(Pockets(4)), layout, 1);

        foreach (var forward in new[] { true, false })
        {
            foreach (var progress in new[] { 0.0, 0.2, 0.49, 0.5, 0.51, 0.8, 0.99, 1.0 })
            {
                scene.Render(to, 1.7, hover: 4, new AlbumTurn(from, forward, progress));
                Assert.Equal(scene.Width * scene.Height, Solid(scene.Canvas));
            }
        }

        scene.Render(to, 0.3, hover: scene.PocketsPerPage + 1);
        Assert.Equal(scene.Width * scene.Height, Solid(scene.Canvas));
    }

    /// <summary>Dressed, with its tabs, every moment of a turn draws, both ways, and the pages are all painted.</summary>
    [Fact]
    public void A_dressed_album_draws_its_cover_and_tabs()
    {
        var scene = new AlbumScene(TcgLayout.Full, dressed: true);
        AlbumTab[] tabs = [new("EQ", true), new("1", false), new("12", false)];
        var from = new AlbumSpread(new AlbumPage([Card()], "EQUIPO", 1), new AlbumPage([], "", 2), TcgLayout.Full, 0, tabs, 0);
        var to = new AlbumSpread(new AlbumPage([Card(seed: 3)], "CAJA 1 · CAMPO", 1), new AlbumPage([Card(seed: 4)], "", 2), TcgLayout.Full, 1, tabs, 1);

        foreach (var progress in new[] { 0.0, 0.3, 0.5, 0.7, 1.0 })
        {
            scene.Render(to, 2, turn: new AlbumTurn(from, progress > 0.4, progress));
        }

        scene.Render(to, 7.4, hover: 0, hoverTab: 2);

        for (var y = scene.PageTop; y < scene.PageTop + scene.PageHeight; y++)
        {
            for (var x = scene.LeftPageX; x < scene.RightPageX + scene.PageWidth; x++)
            {
                Assert.True(scene.Canvas.IsSet(x, y), $"Hueco en {x},{y}");
            }
        }
    }

    // ============================================================================================== HAND

    private static (TcgRender Front, TcgRender Back) Faces(TcgCard card) =>
        (TcgCardArt.Render(card, TcgLayout.Full), TcgCardArt.Render(card, TcgLayout.Full, back: true));

    private static System.Windows.Media.Color At(HandScene scene, int x, int y)
    {
        var at = ((y * scene.Width) + x) * 4;
        return System.Windows.Media.Color.FromArgb(scene.Pixels[at + 3], scene.Pixels[at + 2], scene.Pixels[at + 1], scene.Pixels[at]);
    }

    /// <summary>
    /// Flat in the hand the card is drawn at whole pixels: the cell in its middle, far from the glare, is exactly its
    /// colour; turned over, the middle is the back's.
    /// </summary>
    [Fact]
    public void In_the_hand_the_card_shows_the_face_that_is_up()
    {
        var (front, back) = Faces(Card(rarity: 0));
        var scene = new HandScene(600, 700);

        scene.Render(front, back, new HandPose(0, 0, 0, 4, 300, 350, 0.3), 1, 7);
        Assert.Equal(front.Canvas.At(35, 60), At(scene, 300 - 140 + (35 * 4) + 1, 350 - 192 + (60 * 4) + 1));

        scene.Render(front, back, new HandPose(Math.PI, 0, 0, 4, 300, 350, 0.3), 1, 7);
        var mirrored = TcgCardArt.FullWidth - 1 - 35;
        Assert.Equal(back.Canvas.At(mirrored, 60), At(scene, 300 - 140 + (35 * 4) + 1, 350 - 192 + (60 * 4) + 1));
    }

    /// <summary>Edge on, half way through turning over, the card is a sliver of what it is flat.</summary>
    [Fact]
    public void Edge_on_the_card_is_a_sliver()
    {
        var (front, back) = Faces(Card(rarity: 0));
        int Opaque(HandScene scene) => Enumerable.Range(0, scene.Width * scene.Height).Count(i => scene.Pixels[(i * 4) + 3] == 255);

        var flat = new HandScene(600, 700);
        flat.Render(front, back, new HandPose(0, 0, 0, 4, 300, 350, 0.3), 1, 7);
        var edge = new HandScene(600, 700);
        edge.Render(front, back, new HandPose(Math.PI / 2, 0, 0, 4, 300, 350, 0.3), 1, 7);

        Assert.True(Opaque(edge) < Opaque(flat) / 10);
    }

    /// <summary>
    /// Any pose draws without failing — tilted, turning, flying small, half off the screen — and the part to copy to
    /// the screen stays inside it.
    /// </summary>
    [Fact]
    public void Any_pose_draws_inside_the_screen()
    {
        var (front, back) = Faces(Card(shiny: true));
        var (burnt, burntBack) = Faces(Card(fallen: true, seed: 5));
        var scene = new HandScene(500, 600);
        HandPose[] poses =
        [
            new(0.4, -0.3, 0, 4, 250, 300, 0.4),
            new(2.1, 0.2, 0.1, 3, 250, 300, 0.6),
            new(5.9, 0.1, 0.3, 1.2, 40, 560, 1),
            new(0.2, 0.2, 0, 6, -100, 700, 0.3),
            new(Math.PI, 0, 0, 4, 250, 300, 0.2)
        ];

        foreach (var pose in poses)
        {
            scene.Render(front, back, pose, 2.3, 11, arrival: 0.3);
            scene.Render(burnt, burntBack, pose, 4.1, 5);
            var (x, y, w, h) = scene.Dirty;
            Assert.InRange(x, 0, scene.Width);
            Assert.InRange(y, 0, scene.Height);
            Assert.InRange(x + w, 0, scene.Width);
            Assert.InRange(y + h, 0, scene.Height);
        }
    }
}
