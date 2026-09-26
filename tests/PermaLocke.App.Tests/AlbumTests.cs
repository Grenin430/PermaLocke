using System.Windows.Media;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>
/// The album (§186): how the boxes are paged, how a card is drawn — fallen, shiny, an egg — and which pocket is under
/// the mouse. Pure drawing, so no window is needed.
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
        Assert.DoesNotContain(true, render.Foil);
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

    /// <summary>A shiny's picture is foil and its shine moves: two moments draw two different frames.</summary>
    [Fact]
    public void A_shiny_shines_and_the_shine_moves()
    {
        var render = TcgCardArt.Render(Card(shiny: true), TcgLayout.Full);
        Assert.Contains(true, render.Foil);

        var early = render.Canvas.Clone();
        var later = render.Canvas.Clone();
        TcgCardArt.Animate(render, early, 0, 0, 0.5, 7);
        TcgCardArt.Animate(render, later, 0, 0, 1.3, 7);

        Assert.NotEqual(early.Bgra, later.Bgra);
    }

    /// <summary>A plain card has nothing that moves: no foil, no embers.</summary>
    [Fact]
    public void A_plain_card_is_still()
    {
        var render = TcgCardArt.Render(Card(), TcgLayout.Full);

        Assert.DoesNotContain(true, render.Foil);
        Assert.Empty(render.Embers);
        Assert.False(Card().IsLive);
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

    /// <summary>The first and last pockets of both pages are found where they are drawn; the spine is no pocket.</summary>
    [Theory]
    [InlineData(TcgLayout.Full)]
    [InlineData(TcgLayout.Mini)]
    public void The_mouse_finds_the_pocket_under_it(TcgLayout layout)
    {
        var scene = new AlbumScene(layout);
        var last = scene.PocketsPerPage - 1;
        var (lx, ly) = scene.PocketOrigin(last);

        Assert.Equal(0, scene.PocketAt(scene.PocketOrigin(0).X + 3, scene.PocketOrigin(0).Y + 3));
        Assert.Equal(last, scene.PocketAt(lx + 3, ly + 3));
        Assert.Equal(scene.PocketsPerPage, scene.PocketAt(scene.PageWidth + 12 + scene.PocketOrigin(0).X + 3, 10));
        Assert.Equal(-1, scene.PocketAt(scene.PageWidth + 5, scene.Height / 2));
        Assert.Equal(-1, scene.PocketAt(-1, 0));
        Assert.Equal(-1, scene.PocketAt(1, 1));
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
        var scene = new AlbumScene(layout);
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
}
