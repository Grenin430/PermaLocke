using Color = PermaLocke.App.Views.PixelColour;
using PermaLocke.App.Services;
using PermaLocke.App.Views.Pixel;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>
/// The finish of a card, as the print runs of the real game have them: the rarer, the more it shines (§187).
/// </summary>
public enum TcgFinish
{
    /// <summary>Tiers 1 and 2, eggs and the fallen: printed paper.</summary>
    Plain,

    /// <summary>Tier 3 (★): the picture is holographic, a night of tiny stars that twinkle.</summary>
    Holo,

    /// <summary>Tier 4 (silver ★): reverse holo, the panel around the picture shines silver and the picture does not.</summary>
    Reverse,

    /// <summary>Tier 5 (gold ★): gold border with an engraved grain and a holographic picture.</summary>
    Gold,

    /// <summary>A shiny: the whole card is rainbow foil, border included, whatever its tier.</summary>
    Polychrome
}

/// <summary>Where a cell of a card sits, for the finishes that shine only on one part of it.</summary>
public enum TcgRegion : byte
{
    None,
    Art,
    Panel,
    Rim
}

/// <summary>
/// A card drawn once, with what moves on it kept apart: which part of the card each cell is, for the foil; the stars of
/// a holographic picture; and the embers along a burnt edge.
/// </summary>
public sealed class TcgRender(CellCanvas canvas, TcgRegion[] regions, TcgFinish finish,
    IReadOnlyList<(int X, int Y, double Phase)> embers, IReadOnlyList<(int X, int Y, double Phase)>? stars = null)
{
    public CellCanvas Canvas { get; } = canvas;

    /// <summary>Per cell, the part of the card it belongs to; <see cref="TcgRegion.None"/> for the figure and the text.</summary>
    public TcgRegion[] Regions { get; } = regions;

    public TcgFinish Finish { get; } = finish;

    public IReadOnlyList<(int X, int Y, double Phase)> Embers { get; } = embers;

    /// <summary>The twinkling stars of a holographic picture.</summary>
    public IReadOnlyList<(int X, int Y, double Phase)> Stars { get; } = stars ?? [];

    /// <summary>Whether anything on it moves: a finish that shines or embers that glow.</summary>
    public bool IsLive => Finish != TcgFinish.Plain || Embers.Count > 0;
}

/// <summary>
/// The album's trading cards (§186), drawn cell by cell like the rest of the pixel art: whole cells, flat colours,
/// ordered dithering instead of gradients, nothing smoothed.
/// </summary>
/// <remarks>
/// <para>
/// A card is laid out like a Pokémon TCG card — yellow rim, the stage and the PS on top, the name, the picture in its
/// frame, the moves as its attacks — but printed only with what the game knows (<see cref="TcgCard"/>). The panel
/// and the picture's background are in the colour of its first type, and the background has a motif of its type:
/// flames, waves, grass, a night sky…
/// </para>
/// <para>
/// Two designs: <see cref="TcgLayout.Full"/> for the 3×3 pages and the zoom, <see cref="TcgLayout.Mini"/> for 4×4, with
/// the picture and the numbers but without the moves. The back is the card's sheet: stats, IV, EV, nature, ability,
/// object and where it was caught.
/// </para>
/// <para>
/// Pure: the same card and the same moment draw the same cells, so it is tested without a window. Static drawing and
/// what moves are split (<see cref="Render"/>, <see cref="Animate"/>) so a page redraws only the cards that shine or
/// smoulder.
/// </para>
/// </remarks>
public static class TcgCardArt
{
    public const int FullWidth = 70;
    public const int FullHeight = 96;
    public const int MiniWidth = 50;
    public const int MiniHeight = 68;

    private static readonly Color Outline = Rgb(0x0B, 0x09, 0x10);
    private static readonly Color Ink = Rgb(0x1C, 0x16, 0x26);
    private static readonly Color InkDim = Rgb(0x5E, 0x55, 0x6E);
    private static readonly Color White = Rgb(0xFA, 0xF8, 0xFF);
    private static readonly Color Black = Rgb(0x10, 0x0C, 0x16);

    // El borde amarillo de las cartas de siempre.
    private static readonly Color RimHi = Rgb(0xFF, 0xF0, 0xA0);
    private static readonly Color Rim = Rgb(0xF2, 0xCC, 0x3C);
    private static readonly Color RimDark = Rgb(0xB0, 0x82, 0x1C);

    // El borde dorado del tier 5, grabado.
    private static readonly Color GoldRimHi = Rgb(0xFF, 0xF8, 0xD8);
    private static readonly Color GoldRim = Rgb(0xC8, 0x8A, 0x22);
    private static readonly Color GoldRimLight = Rgb(0xFF, 0xE2, 0x8A);
    private static readonly Color GoldRimDeep = Rgb(0x9A, 0x62, 0x14);
    private static readonly Color GoldRimDark = Rgb(0x6E, 0x40, 0x0E);

    private static readonly Color SilverHi = Rgb(0xF4, 0xF6, 0xFF);
    private static readonly Color Smoke = Rgb(0x6A, 0x62, 0x70);
    private static readonly Color SmokeLight = Rgb(0x9A, 0x94, 0xA0);

    private static readonly Color Up = Rgb(0xC8, 0x32, 0x32);
    private static readonly Color Down = Rgb(0x2E, 0x5E, 0xC8);
    private static readonly Color Gold = Rgb(0xFF, 0xD2, 0x3C);
    private static readonly Color GoldDark = Rgb(0x9A, 0x6A, 0x10);
    private static readonly Color Silver = Rgb(0xC8, 0xCC, 0xD8);

    private static readonly Color Char = Rgb(0x14, 0x0E, 0x0C);
    private static readonly Color CharBrown = Rgb(0x3A, 0x22, 0x14);
    private static readonly Color Scorch = Rgb(0x6E, 0x44, 0x22);
    private static readonly Color Sepia = Rgb(0xA8, 0x88, 0x5C);
    private static readonly Color Ember = Rgb(0xFF, 0x7A, 0x1E);
    private static readonly Color EmberHot = Rgb(0xFF, 0xD0, 0x50);

    private static readonly Color[] Rainbow =
    [
        Rgb(0xFF, 0x8A, 0x9A), Rgb(0xFF, 0xB8, 0x6A), Rgb(0xFF, 0xEE, 0x7A), Rgb(0x9A, 0xF0, 0x8A),
        Rgb(0x7A, 0xE6, 0xF0), Rgb(0x8A, 0xA8, 0xFF), Rgb(0xD0, 0x9A, 0xFF)
    ];

    private static readonly string[] StatShort = ["PS", "ATQ", "DEF", "ATE", "DFE", "VEL"];

    /// <summary>Cells a card of this design takes.</summary>
    public static (int Width, int Height) SizeOf(TcgLayout layout) =>
        layout == TcgLayout.Full ? (FullWidth, FullHeight) : (MiniWidth, MiniHeight);

    // ====================================================================================================== ENTRY

    /// <summary>
    /// Draws the front of a card, or its back when <paramref name="back"/> — the sheet for a Pokémon, the album's back
    /// for an egg. What moves is left for <see cref="Animate"/>.
    /// </summary>
    public static TcgRender Render(TcgCard card, TcgLayout layout, bool back = false)
    {
        var (width, height) = back ? (FullWidth, FullHeight) : SizeOf(layout);
        var regions = new TcgRegion[width * height];
        var canvas = new CellCanvas(width, height) { Regions = regions };
        List<(int, int, double)> embers = [];
        List<(int, int, double)> stars = [];

        if (card.Egg)
        {
            DrawCardBack(canvas, true);
            return new TcgRender(canvas, regions, TcgFinish.Plain, embers);
        }

        var finish = FinishOf(card);

        if (back)
        {
            DrawSheet(canvas, card, finish);
        }
        else if (layout == TcgLayout.Full)
        {
            DrawFull(canvas, card, finish);
        }
        else
        {
            DrawMini(canvas, card, finish);
        }

        if (card.Fallen)
        {
            Ruin(canvas, card.Seed, embers, mirrored: back);
            Array.Clear(regions);
            return new TcgRender(canvas, regions, TcgFinish.Plain, embers);
        }

        Laminate(canvas, finish, card.Seed, stars);
        return new TcgRender(canvas, regions, finish, embers, stars);
    }

    /// <summary>
    /// The finish a card is printed with: a shiny is always polychrome; otherwise its rarity decides, like the ★ of a
    /// real card. The fallen lose theirs with the fire, and an egg is face down.
    /// </summary>
    public static TcgFinish FinishOf(TcgCard card)
    {
        if (card.Egg || card.Fallen) return TcgFinish.Plain;
        if (card.Shiny) return TcgFinish.Polychrome;

        return card.Rarity switch
        {
            4 => TcgFinish.Gold,
            3 => TcgFinish.Reverse,
            2 => TcgFinish.Holo,
            _ => TcgFinish.Plain
        };
    }

    /// <summary>
    /// The back every card has, the one seen when a card lies face down: an egg, whose contents the game does not show.
    /// </summary>
    public static TcgRender RenderBack(TcgLayout layout)
    {
        var (width, height) = SizeOf(layout);
        var canvas = new CellCanvas(width, height);
        DrawCardBack(canvas, false);
        return new TcgRender(canvas, new TcgRegion[width * height], TcgFinish.Plain, []);
    }

    /// <summary>
    /// The moving part of a card at <paramref name="seconds"/>, drawn into <paramref name="target"/> where the card sits:
    /// the light running over its foil — rainbow, silver or gold by its finish —, the twinkling stars of a holographic
    /// picture, the sparkles of a shiny, and the embers and smoke of a fallen card.
    /// </summary>
    /// <param name="light">
    /// Where the light crosses the card, 0 at its top-left corner to 1 at the bottom-right: the card in the hand passes
    /// the tilt here, so the foil catches the light as it turns. Null lets it sweep by itself, with a pause between
    /// passes, as on the page.
    /// </param>
    public static void Animate(TcgRender render, CellCanvas target, int left, int top, double seconds, uint seed, double? light = null)
    {
        var canvas = render.Canvas;
        var width = canvas.Width;
        var height = canvas.Height;
        var finish = render.Finish;

        if (finish != TcgFinish.Plain)
        {
            var span = width + (height * 0.8) + 32;
            double centre;
            if (light is { } l)
            {
                centre = (l * span) - 16;
            }
            else
            {
                // Una pasada cada 3,4 s: 2,4 s cruzando y un segundo de calma, para que no canse.
                var cycle = seconds % 3.4 / 2.4;
                centre = cycle <= 1 ? (cycle * span) - 16 : -999;
            }

            var reach = finish == TcgFinish.Polychrome ? 9.0 : 6.0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var region = render.Regions[(y * width) + x];
                    if (region == TcgRegion.None || !Shines(finish, region))
                    {
                        continue;
                    }

                    var distance = x + (y * 0.8) - centre;
                    var strength = 1 - (Math.Abs(distance) / reach);

                    // Los bordes de la franja se deshacen en tramado: la luz no tiene un filo recto.
                    if (strength <= 0 || !Dither(x, y, Math.Min(1, strength * 1.6)))
                    {
                        continue;
                    }

                    var (tint, amount) = Shine(finish, region, distance, reach);
                    target.Put(left + x, top + y, Mix(canvas.At(x, y), tint, amount));
                }
            }

            // Las estrellas del dibujo holográfico se encienden a su ritmo.
            foreach (var (x, y, phase) in render.Stars)
            {
                var t = ((seconds * 0.55) + phase) % 1;
                if (t < 0.08)
                {
                    Sparkle(target, left + x, top + y, 1);
                }
                else if (t < 0.2)
                {
                    target.Put(left + x, top + y, White);
                }
            }

            // Un variocolor y un dorado sueltan destellos de cuatro puntas en sitios fijos de la lámina.
            if (finish is TcgFinish.Polychrome or TcgFinish.Gold)
            {
                for (var i = 0; i < 3; i++)
                {
                    var (sx, sy) = FoilSpot(render, seed, i);
                    if (sx < 0)
                    {
                        continue;
                    }

                    var phase = (seconds / 1.7) + (i * 0.37) + (Hash(i, 3, seed) * 0.5);
                    var t = phase - Math.Floor(phase);
                    var size = t < 0.1 ? 1 : t < 0.2 ? 2 : t < 0.3 ? 1 : 0;
                    if (size > 0)
                    {
                        Sparkle(target, left + sx, top + sy, size);
                    }
                }
            }
        }

        foreach (var (x, y, phase) in render.Embers)
        {
            var glow = Math.Sin((seconds * 5.5) + (phase * 20));
            if (glow > 0.15)
            {
                target.Put(left + x, top + y, glow > 0.8 ? EmberHot : Ember);
            }
        }

        // Humo: de tres brasas suben volutas que se deshacen, cada una a su tiempo.
        for (var i = 0; i < Math.Min(3, render.Embers.Count); i++)
        {
            var (ex, ey, phase) = render.Embers[(int)(Hash(i, 5, seed) * render.Embers.Count)];
            var t = ((seconds / 2.6) + phase + (i * 0.33)) % 1;
            var rise = t * 16;
            var sx = ex + (int)Math.Round(Math.Sin((t * 6) + phase) * 2);
            var sy = ey - (int)rise;
            var fade = 1 - t;

            if (Dither(sx, sy, fade))
            {
                target.Put(left + sx, top + sy, t < 0.4 ? SmokeLight : Smoke);
            }

            if (t > 0.25 && Dither(sx + 1, sy, fade * 0.8))
            {
                target.Put(left + sx + 1, top + sy, Smoke);
            }
        }
    }

    /// <summary>Which parts of the card a finish makes shine.</summary>
    private static bool Shines(TcgFinish finish, TcgRegion region) => finish switch
    {
        TcgFinish.Polychrome => true,
        TcgFinish.Holo => region == TcgRegion.Art,
        TcgFinish.Reverse => region == TcgRegion.Panel,
        TcgFinish.Gold => region is TcgRegion.Rim or TcgRegion.Art,
        _ => false
    };

    /// <summary>The colour the light takes on a part of the card, and how much of it.</summary>
    private static (Color Tint, double Amount) Shine(TcgFinish finish, TcgRegion region, double distance, double reach)
    {
        var hue = Rainbow[Math.Clamp((int)((distance + reach) / (2 * reach) * Rainbow.Length), 0, Rainbow.Length - 1)];

        return finish switch
        {
            TcgFinish.Polychrome => (hue, 0.58),
            TcgFinish.Reverse => (SilverHi, 0.55),
            TcgFinish.Gold when region == TcgRegion.Rim => (GoldRimHi, 0.75),
            _ => (hue, 0.42)
        };
    }

    /// <summary>
    /// The still part of the foil, printed once: a holographic picture's field of tiny stars, the silver hatching of a
    /// reverse holo, and the faint rainbow stripes all over a shiny.
    /// </summary>
    private static void Laminate(CellCanvas c, TcgFinish finish, uint seed, List<(int X, int Y, double Phase)> stars)
    {
        if (finish == TcgFinish.Plain || c.Regions is not { } regions)
        {
            return;
        }

        for (var y = 0; y < c.Height; y++)
        {
            for (var x = 0; x < c.Width; x++)
            {
                var region = regions[(y * c.Width) + x];
                if (region == TcgRegion.None)
                {
                    continue;
                }

                var colour = c.At(x, y);

                if (region == TcgRegion.Art && finish is TcgFinish.Holo or TcgFinish.Gold or TcgFinish.Polychrome)
                {
                    var h = Hash(x, y, seed + 41);
                    if (h < 0.05)
                    {
                        c.Tint(x, y, Mix(colour, h < 0.02 ? White : Rainbow[(int)(h * 1000) % Rainbow.Length], 0.6));
                        if (h < 0.018 && x % 3 == 1)
                        {
                            stars.Add((x, y, Hash(y, x, seed + 43)));
                        }

                        continue;
                    }
                }

                if (region == TcgRegion.Panel && finish == TcgFinish.Reverse)
                {
                    if (((x * 3) + y) % 6 == 0 && Dither(x, y, 0.5))
                    {
                        c.Tint(x, y, Mix(colour, SilverHi, 0.35));
                    }
                    else if (Hash(x, y, seed + 47) < 0.025)
                    {
                        c.Tint(x, y, Mix(colour, White, 0.6));
                    }

                    continue;
                }

                if (finish == TcgFinish.Polychrome && ((x - y) % 9 + 9) % 9 == 0 && Dither(x, y, 0.5))
                {
                    c.Tint(x, y, Mix(colour, Rainbow[(((x + y) / 9) % Rainbow.Length + Rainbow.Length) % Rainbow.Length], 0.3));
                }
            }
        }
    }

    // ====================================================================================================== FRONT

    private static void DrawFull(CellCanvas c, TcgCard card, TcgFinish finish)
    {
        var type = TypeColours.Of(card.MainType);
        var panel = Mix(type, White, 0.62);

        Body(c, 3, panel, Mix(type, White, 0.72), Mix(type, White, 0.5), finish == TcgFinish.Gold);

        // ARRIBA: la fase en su placa y, a la derecha, la energía de sus tipos.
        var stage = card.Stage.ToUpperInvariant();
        var plate = SmallWidth(stage) + 4;
        Plate(c, 5, 4, plate, 8, Mix(panel, White, 0.55), Mix(type, Black, 0.35));
        Small(c, stage, 7, 6, Ink);
        EnergyRow(c, card.Types, FullWidth - 6, 4, Mix(panel, Black, 0.3));

        // EL NOMBRE, con la letra grande de la aplicación y su sombra, como impreso en relieve. Si es variocolor, su
        // estrella detrás.
        var name = PixelFont.Trim(card.Name, FullWidth - 12 - (card.Shiny ? 9 : 0));
        Font(c, name, 7, 12, Mix(panel, Black, 0.22));
        Font(c, name, 6, 11, Ink);
        if (card.Shiny)
        {
            Outlined(c, ShinyStar, 6 + PixelFont.Measure(name) + 4, 15, Gold, GoldDark);
        }

        // LA ILUSTRACIÓN.
        ArtWindow(c, card, 6, 24, FullWidth - 12, 34, type);

        // LA TIRA, en su placa bajo el dibujo: nivel, PS y rareza. Rótulo apagado y cifra, como el «PS 120».
        Plate(c, 5, 59, FullWidth - 10, 7, Mix(panel, White, 0.4), Mix(type, Black, 0.2));
        var after = Labelled(c, "NV", card.Level.ToString(), 7, 60, gap: 1);
        Labelled(c, "PS", card.Hp > 0 ? card.Hp.ToString() : "?", after + 4, 60, gap: 1);
        RarityMarks(c, card, FullWidth - 8, 60);

        // LOS MOVIMIENTOS, como los ataques: su energía y su nombre, en filas alternas apenas sombreadas.
        c.Region = TcgRegion.Panel;
        foreach (var row in new[] { 1, 3 })
        {
            var top = 67 + (row * 6);
            for (var yy = top; yy < top + 7 && yy < FullHeight - 4; yy++)
            {
                for (var xx = 5; xx < FullWidth - 5; xx++)
                {
                    c.Put(xx, yy, Mix(c.At(xx, yy), Black, 0.06));
                }
            }
        }

        c.Region = TcgRegion.None;

        for (var i = 0; i < 4; i++)
        {
            var y = 68 + (i * 6);
            var move = i < card.Moves.Count ? card.Moves[i] : null;

            if (move is null)
            {
                Small(c, "-", 13, y, InkDim);
                continue;
            }

            EnergyDot(c, move.Type, 6, y);
            Small(c, SmallTrim(move.Name, FullWidth - 6 - 13), 13, y, Ink);
        }
    }

    private static void DrawMini(CellCanvas c, TcgCard card, TcgFinish finish)
    {
        var type = TypeColours.Of(card.MainType);
        var panel = Mix(type, White, 0.62);

        Body(c, 2, panel, Mix(type, White, 0.72), Mix(type, White, 0.5), finish == TcgFinish.Gold);

        var name = SmallTrim(card.Name, MiniWidth - 8 - (card.Shiny ? 7 : 0));
        Small(c, name, 5, 6, Mix(panel, Black, 0.22));
        Small(c, name, 4, 5, Ink);
        if (card.Shiny)
        {
            Outlined(c, ShinyStar, 4 + SmallWidth(name) + 3, 5, Gold, GoldDark);
        }

        ArtWindow(c, card, 4, 13, MiniWidth - 8, 34, type);

        // Rótulo apagado y cifra, con una sola celda entre los dos: «NV 100» y «PS 255» caben juntos.
        Labelled(c, "NV", card.Level.ToString(), 4, 49, gap: 1);
        var hp = card.Hp > 0 ? card.Hp.ToString() : "?";
        Labelled(c, "PS", hp, MiniWidth - 5 - SmallWidth(hp) - SmallWidth("PS") - 2, 49, gap: 1);

        // Abajo, la energía de sus tipos y la rareza.
        for (var i = 0; i < Math.Min(2, card.Types.Count); i++)
        {
            Energy(c, card.Types[i], 4 + (i * 10), 55, Mix(panel, Black, 0.3));
        }

        RarityMarks(c, card, MiniWidth - 6, 57);
    }

    /// <summary>
    /// The rim, the outline with its rounded corners, and the panel: lighter at the top, joined by dithering, with a
    /// satin grain. The rim is the classic yellow with a fine diagonal grain, or gold and engraved for tier 5.
    /// </summary>
    private static void Body(CellCanvas c, int rim, Color panel, Color panelLight, Color texture, bool gold)
    {
        var w = c.Width;
        var h = c.Height;
        var (hi, mid, grain, dark) = gold
            ? (GoldRimHi, GoldRim, GoldRimLight, GoldRimDark)
            : (RimHi, Rim, Mix(Rim, RimHi, 0.45), RimDark);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var d = Math.Min(Math.Min(x, y), Math.Min(w - 1 - x, h - 1 - y));
                var corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);

                if (corner)
                {
                    continue;
                }

                if (d == 0 || (d == 1 && (x == 1 || x == w - 2) && (y == 1 || y == h - 2)))
                {
                    c.Region = TcgRegion.None;
                    c.Put(x, y, Outline);
                }
                else if (d < rim)
                {
                    c.Region = TcgRegion.Rim;
                    var lit = x == d || y == d;
                    // El dorado va grabado en cruz, que es lo que lo hace metal y no pintura amarilla.
                    var colour = d == 1 ? (lit ? hi : dark)
                        : (x + y) % (gold ? 3 : 4) == 0 ? grain
                        : gold && ((x - y) % 3 + 3) % 3 == 0 ? GoldRimDeep
                        : mid;
                    c.Put(x, y, colour);
                }
                else if (d == rim)
                {
                    c.Region = TcgRegion.Rim;
                    c.Put(x, y, dark);
                }
                else
                {
                    // Más claro arriba, fundido a tramado; y un grano diagonal muy suave, como el papel satinado.
                    c.Region = TcgRegion.Panel;
                    var level = Math.Clamp(((double)y / h - 0.12) / 0.22, 0, 1);
                    var paper = Dither(x, y, level) ? panel : panelLight;
                    c.Put(x, y, (x + y) % 5 == 0 && Dither(x, y, 0.5) ? texture : paper);
                }
            }
        }

        c.Region = TcgRegion.None;
    }

    /// <summary>The star after a shiny's name.</summary>
    private static readonly string[] ShinyStar = ["..#..", ".###.", "#####", ".###.", "#...#"];

    /// <summary>A small plate with notched corners.</summary>
    private static void Plate(CellCanvas c, int x, int y, int width, int height, Color fill, Color edge)
    {
        for (var yy = 0; yy < height; yy++)
        {
            for (var xx = 0; xx < width; xx++)
            {
                var border = xx == 0 || yy == 0 || xx == width - 1 || yy == height - 1;
                var corner = (xx == 0 || xx == width - 1) && (yy == 0 || yy == height - 1);
                if (!corner)
                {
                    c.Put(x + xx, y + yy, border ? edge : fill);
                }
            }
        }
    }

    // ====================================================================================================== PICTURE

    /// <summary>The framed picture: its type's scene behind, the Pokémon standing in it with its shadow.</summary>
    private static void ArtWindow(CellCanvas c, TcgCard card, int x, int y, int width, int height, Color type)
    {
        // El marco: una línea oscura y un filete dorado con relieve, como el de las cartas.
        for (var yy = 0; yy < height; yy++)
        {
            for (var xx = 0; xx < width; xx++)
            {
                var d = Math.Min(Math.Min(xx, yy), Math.Min(width - 1 - xx, height - 1 - yy));
                if (d == 0)
                {
                    c.Put(x + xx, y + yy, Mix(type, Black, 0.55));
                }
                else if (d == 1)
                {
                    c.Put(x + xx, y + yy, xx == 1 || yy == 1 ? RimHi : RimDark);
                }
            }
        }

        var inner = (X: x + 2, Y: y + 2, W: width - 4, H: height - 4);
        c.Region = TcgRegion.Art;
        Scene(c, card.MainType, inner.X, inner.Y, inner.W, inner.H, card.Seed);
        c.Region = TcgRegion.None;

        if (card.Sprite is { } sprite)
        {
            Figure(c, sprite, inner.X, inner.Y, inner.W, inner.H, type);
        }
        else
        {
            // Sin icono, un interrogante: el hueco se ve, no se inventa un dibujo.
            Font(c, "?", inner.X + (inner.W / 2) - 2, inner.Y + (inner.H / 2) - 7, Mix(type, Black, 0.5));
        }
    }

    /// <summary>The Pokémon, feet on the ground of the scene, outlined so it stands out, with a dithered shadow.</summary>
    private static void Figure(CellCanvas c, RoomSprite sprite, int x, int y, int width, int height, Color type)
    {
        int minX = sprite.Width, minY = sprite.Height, maxX = -1, maxY = -1;
        for (var sy = 0; sy < sprite.Height; sy++)
        {
            for (var sx = 0; sx < sprite.Width; sx++)
            {
                if (!sprite.Solid(sx, sy)) continue;
                minX = Math.Min(minX, sx);
                maxX = Math.Max(maxX, sx);
                minY = Math.Min(minY, sy);
                maxY = Math.Max(maxY, sy);
            }
        }

        if (maxX < 0)
        {
            return;
        }

        var w = maxX - minX + 1;
        var h = maxY - minY + 1;
        var left = x + ((width - w) / 2);
        var bottom = y + height - 4;
        var top = Math.Max(y + 1, bottom - h + 1);

        bool Inside(int px, int py) => px >= x && py >= y && px < x + width && py < y + height;

        // La sombra en el suelo, a medio tono.
        var shadow = Mix(type, Black, 0.55);
        var cx = left + (w / 2.0);
        var rx = Math.Max(3, w * 0.38);
        for (var sy = top + h - 1; sy <= top + h + 1; sy++)
        {
            for (var sx = (int)(cx - rx); sx <= (int)(cx + rx); sx++)
            {
                var dx = (sx + 0.5 - cx) / rx;
                var dy = (sy - (top + h)) / 1.6;
                if ((dx * dx) + (dy * dy) <= 1 && Inside(sx, sy) && Dither(sx, sy, 0.6))
                {
                    c.Put(sx, sy, shadow);
                }
            }
        }

        // El contorno oscuro de una celda, y luego el dibujo.
        var edge = Mix(type, Black, 0.7);
        for (var sy = minY - 1; sy <= maxY + 1; sy++)
        {
            for (var sx = minX - 1; sx <= maxX + 1; sx++)
            {
                if (sprite.Solid(sx, sy))
                {
                    continue;
                }

                if (sprite.Solid(sx - 1, sy) || sprite.Solid(sx + 1, sy) || sprite.Solid(sx, sy - 1) || sprite.Solid(sx, sy + 1))
                {
                    var px = left + sx - minX;
                    var py = top + sy - minY;
                    if (Inside(px, py))
                    {
                        c.Put(px, py, edge);
                    }
                }
            }
        }

        for (var sy = minY; sy <= maxY; sy++)
        {
            for (var sx = minX; sx <= maxX; sx++)
            {
                if (!sprite.Solid(sx, sy)) continue;
                var px = left + sx - minX;
                var py = top + sy - minY;
                if (Inside(px, py))
                {
                    c.Put(px, py, sprite.Cell(sx, sy));
                }
            }
        }
    }

    /// <summary>
    /// The background of the picture: a sky in two tones joined by dithering, a strip of ground, and the motif of the
    /// type.
    /// </summary>
    private static void Scene(CellCanvas c, int typeId, int x, int y, int width, int height, uint seed)
    {
        var type = TypeColours.Of(typeId);
        var night = typeId is 7 or 16;
        var skyTop = night ? Mix(type, Black, 0.6) : Mix(type, White, 0.72);
        var skyLow = night ? Mix(type, Black, 0.3) : Mix(type, White, 0.45);
        var ground = typeId == 14 ? Mix(type, White, 0.8) : Mix(type, Black, 0.22);
        var groundLine = Mix(type, Black, 0.42);
        var groundTop = y + height - 7;

        for (var yy = y; yy < y + height; yy++)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                if (yy >= groundTop)
                {
                    var stripe = (yy - groundTop) % 3 == 2 && Dither(xx, yy, 0.5);
                    c.Put(xx, yy, yy == groundTop ? groundLine : stripe ? Mix(ground, Black, 0.12) : ground);
                }
                else
                {
                    var level = (double)(yy - y) / Math.Max(1, groundTop - y);
                    c.Put(xx, yy, Dither(xx, yy, level) ? skyLow : skyTop);
                }
            }
        }

        var light = night ? Mix(type, White, 0.55) : Mix(type, White, 0.9);
        var mid = Mix(type, White, 0.25);
        var dark = Mix(type, Black, 0.35);
        var area = (X: x, Y: y, W: width, H: groundTop - y);

        switch (typeId)
        {
            case 1: Burst(c, area, light); break;
            case 2: Clouds(c, area, light, seed); Streaks(c, area, mid, seed); break;
            case 3: Bubbles(c, area, light, dark, seed); break;
            case 4:
            case 5: Rocks(c, x, groundTop, width, dark, mid, seed); Clouds(c, area, light, seed); break;
            case 6: Leaves(c, area, mid, seed); Tufts(c, x, groundTop, width, dark, seed); break;
            case 7: Stars(c, area, light, seed); Wisps(c, area, mid, seed); break;
            case 8: Grid(c, area, light); break;
            case 9: Flames(c, x, groundTop, width, Rgb(0xFF, 0xC8, 0x4A), Rgb(0xE8, 0x4A, 0x1E)); break;
            case 10: Waves(c, x, groundTop, width, y + height - groundTop, light); Bubbles(c, area, light, mid, seed); break;
            case 11: Tufts(c, x, groundTop, width, dark, seed); Leaves(c, area, mid, seed); break;
            case 12: Zigzag(c, area, Rgb(0xFF, 0xF4, 0x9A), dark, seed); break;
            case 13: Rings(c, area, light); break;
            case 14: Snow(c, area, White, seed); break;
            case 15: Streaks(c, area, light, seed); Burst(c, area, mid); break;
            case 16: Stars(c, area, light, seed); Moon(c, area, light); break;
            case 17: Sparkles(c, area, White, seed); break;
            default: Clouds(c, area, light, seed); break;
        }
    }

    private static void Clouds(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 3; i++)
        {
            var cx = a.X + 4 + (int)(Hash(i, 11, seed) * (a.W - 12));
            var cy = a.Y + 2 + (int)(Hash(i, 12, seed) * Math.Max(1, a.H / 2 - 2));
            for (var yy = 0; yy < 3; yy++)
            {
                var half = yy == 0 ? 2 : yy == 1 ? 4 : 3;
                for (var xx = -half; xx <= half; xx++)
                {
                    PutIn(c, a, cx + xx + (yy == 0 ? 1 : 0), cy + yy, colour);
                }
            }
        }
    }

    private static void Streaks(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 4; i++)
        {
            var sx = a.X + (int)(Hash(i, 21, seed) * a.W);
            var sy = a.Y + (int)(Hash(i, 22, seed) * a.H);
            for (var k = 0; k < 6; k++)
            {
                PutIn(c, a, sx + k, sy - (k / 2), colour);
            }
        }
    }

    private static void Burst(CellCanvas c, (int X, int Y, int W, int H) a, Color colour)
    {
        var cx = a.X + (a.W / 2.0);
        var cy = a.Y + (a.H * 0.55);
        for (var ray = 0; ray < 12; ray++)
        {
            var angle = ray * Math.PI / 6;
            for (var r = 9.0; r < a.W; r += 1)
            {
                if ((int)r % 3 == 0) continue;
                PutIn(c, a, (int)(cx + (Math.Cos(angle) * r)), (int)(cy + (Math.Sin(angle) * r * 0.7)), colour);
            }
        }
    }

    private static void Bubbles(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, Color rim, uint seed)
    {
        for (var i = 0; i < 6; i++)
        {
            var cx = a.X + 2 + (int)(Hash(i, 31, seed) * (a.W - 4));
            var cy = a.Y + 2 + (int)(Hash(i, 32, seed) * (a.H - 4));
            var big = Hash(i, 33, seed) > 0.5;
            if (big)
            {
                PutIn(c, a, cx, cy - 1, rim);
                PutIn(c, a, cx - 1, cy, rim);
                PutIn(c, a, cx + 1, cy, rim);
                PutIn(c, a, cx, cy + 1, rim);
                PutIn(c, a, cx - 1, cy - 1, colour);
            }
            else
            {
                PutIn(c, a, cx, cy, colour);
            }
        }
    }

    private static void Rocks(CellCanvas c, int x, int groundTop, int width, Color dark, Color light, uint seed)
    {
        for (var i = 0; i < 3; i++)
        {
            var rx = x + 2 + (int)(Hash(i, 41, seed) * (width - 8));
            var ry = groundTop - 2;
            c.Put(rx + 1, ry, light);
            c.Put(rx + 2, ry, light);
            c.Put(rx, ry + 1, dark);
            c.Put(rx + 1, ry + 1, light);
            c.Put(rx + 2, ry + 1, dark);
            c.Put(rx + 3, ry + 1, dark);
        }
    }

    private static void Leaves(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 5; i++)
        {
            var lx = a.X + 2 + (int)(Hash(i, 51, seed) * (a.W - 5));
            var ly = a.Y + 2 + (int)(Hash(i, 52, seed) * (a.H - 5));
            PutIn(c, a, lx + 1, ly, colour);
            PutIn(c, a, lx, ly + 1, colour);
            PutIn(c, a, lx + 1, ly + 1, colour);
            PutIn(c, a, lx + 2, ly + 1, colour);
            PutIn(c, a, lx + 1, ly + 2, colour);
        }
    }

    private static void Tufts(CellCanvas c, int x, int groundTop, int width, Color colour, uint seed)
    {
        for (var tx = x + 1; tx < x + width - 2; tx += 4 + (int)(Hash(tx, 61, seed) * 3))
        {
            c.Put(tx, groundTop - 1, colour);
            c.Put(tx + 2, groundTop - 1, colour);
            c.Put(tx + 1, groundTop - 2, colour);
            c.Put(tx + 1, groundTop - 1, colour);
        }
    }

    private static void Stars(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 9; i++)
        {
            PutIn(c, a, a.X + (int)(Hash(i, 71, seed) * a.W), a.Y + (int)(Hash(i, 72, seed) * a.H), colour);
        }
    }

    private static void Wisps(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 2; i++)
        {
            var wx = a.X + 3 + (int)(Hash(i, 81, seed) * (a.W - 8));
            var wy = a.Y + 3 + (int)(Hash(i, 82, seed) * (a.H - 6));
            for (var k = 0; k < 5; k++)
            {
                PutIn(c, a, wx + k, wy + (k % 2), colour);
            }
        }
    }

    private static void Moon(CellCanvas c, (int X, int Y, int W, int H) a, Color colour)
    {
        var cx = a.X + a.W - 8;
        var cy = a.Y + 5;
        for (var yy = -3; yy <= 3; yy++)
        {
            for (var xx = -3; xx <= 3; xx++)
            {
                var inside = (xx * xx) + (yy * yy) <= 10;
                var bite = ((xx + 2) * (xx + 2)) + ((yy - 1) * (yy - 1)) <= 8;
                if (inside && !bite)
                {
                    PutIn(c, a, cx + xx, cy + yy, colour);
                }
            }
        }
    }

    private static void Grid(CellCanvas c, (int X, int Y, int W, int H) a, Color colour)
    {
        for (var yy = a.Y; yy < a.Y + a.H; yy++)
        {
            for (var xx = a.X; xx < a.X + a.W; xx++)
            {
                if (((xx - a.X) % 9 == 0 || (yy - a.Y) % 7 == 0) && Dither(xx, yy, 0.5))
                {
                    c.Put(xx, yy, colour);
                }
            }
        }
    }

    private static void Flames(CellCanvas c, int x, int groundTop, int width, Color hot, Color flame)
    {
        for (var fx = x + 1; fx < x + width - 3; fx += 5)
        {
            var tall = (fx / 5) % 2 == 0 ? 4 : 3;
            for (var k = 0; k < tall; k++)
            {
                var half = (tall - k) / 2;
                for (var xx = -half; xx <= half; xx++)
                {
                    c.Put(fx + 1 + xx, groundTop - 1 - k, k < tall - 1 && xx == 0 ? hot : flame);
                }
            }
        }
    }

    private static void Waves(CellCanvas c, int x, int groundTop, int width, int depth, Color colour)
    {
        for (var row = 1; row < depth; row += 3)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                if ((xx + (row * 2)) % 6 < 2)
                {
                    c.Put(xx, groundTop + row, colour);
                }
            }
        }
    }

    private static void Zigzag(CellCanvas c, (int X, int Y, int W, int H) a, Color bolt, Color edge, uint seed)
    {
        for (var i = 0; i < 2; i++)
        {
            var zx = a.X + 3 + (int)(Hash(i, 91, seed) * (a.W - 10));
            var zy = a.Y + 1;
            int[] path = [0, 1, 2, 1, 0, 1, 2, 3, 2, 3, 4];
            for (var k = 0; k < path.Length && k < a.H - 2; k++)
            {
                PutIn(c, a, zx + path[k], zy + k, bolt);
                PutIn(c, a, zx + path[k] + 1, zy + k, edge);
            }
        }
    }

    private static void Rings(CellCanvas c, (int X, int Y, int W, int H) a, Color colour)
    {
        var cx = a.X + (a.W / 2.0);
        var cy = a.Y + (a.H * 0.6);
        for (var yy = a.Y; yy < a.Y + a.H; yy++)
        {
            for (var xx = a.X; xx < a.X + a.W; xx++)
            {
                var r = Math.Sqrt(Math.Pow(xx + 0.5 - cx, 2) + Math.Pow((yy + 0.5 - cy) * 1.3, 2));
                if ((int)r % 6 == 0 && r > 5)
                {
                    c.Put(xx, yy, colour);
                }
            }
        }
    }

    private static void Snow(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 14; i++)
        {
            PutIn(c, a, a.X + (int)(Hash(i, 101, seed) * a.W), a.Y + (int)(Hash(i, 102, seed) * a.H), colour);
        }
    }

    private static void Sparkles(CellCanvas c, (int X, int Y, int W, int H) a, Color colour, uint seed)
    {
        for (var i = 0; i < 5; i++)
        {
            var sx = a.X + 2 + (int)(Hash(i, 111, seed) * (a.W - 4));
            var sy = a.Y + 2 + (int)(Hash(i, 112, seed) * (a.H - 4));
            PutIn(c, a, sx, sy, colour);
            if (i % 2 == 0)
            {
                PutIn(c, a, sx - 1, sy, colour);
                PutIn(c, a, sx + 1, sy, colour);
                PutIn(c, a, sx, sy - 1, colour);
                PutIn(c, a, sx, sy + 1, colour);
            }
        }
    }

    private static void PutIn(CellCanvas c, (int X, int Y, int W, int H) a, int x, int y, Color colour)
    {
        if (x >= a.X && y >= a.Y && x < a.X + a.W && y < a.Y + a.H)
        {
            c.Put(x, y, colour);
        }
    }

    // ====================================================================================================== MARKS

    private static readonly Dictionary<int, string[]> Pictograms = new()
    {
        [0] = ["..#..", "..#..", "#####", ".###.", "##.##"],
        [1] = ["##.##", "#####", "#####", ".###.", ".###."],
        [2] = [".....", "#...#", ".#.#.", "..#..", "....."],
        [3] = [".###.", "#.#.#", "#####", ".###.", ".#.#."],
        [4] = ["..#..", ".###.", ".#.##", "##.##", "#####"],
        [5] = [".##..", "####.", "#####", ".####", "..##."],
        [6] = ["#...#", ".###.", "#####", ".###.", "#.#.#"],
        [7] = [".###.", "#.#.#", "#####", "#####", "#.#.#"],
        [8] = [".###.", "#####", "##.##", "#####", ".###."],
        [9] = ["..#..", ".##..", ".###.", "#####", ".###."],
        [10] = ["..#..", ".###.", "#####", "#####", ".###."],
        [11] = ["...##", "..###", ".###.", "###..", "#...."],
        [12] = ["..##.", ".##..", "#####", "..##.", ".##.."],
        [13] = [".....", ".###.", "##.##", ".###.", "....."],
        [14] = ["#.#.#", ".###.", "##.##", ".###.", "#.#.#"],
        [15] = ["#.#.#", "#####", ".###.", "..#..", "..#.."],
        [16] = [".###.", "##...", "#....", "##...", ".###."],
        [17] = ["..#..", "#.#.#", ".###.", "#.#.#", "..#.."],
    };

    /// <summary>The energy of each type, right-aligned to <paramref name="right"/>; returns where the row starts.</summary>
    private static int EnergyRow(CellCanvas c, IReadOnlyList<int> types, int right, int top, Color shadow)
    {
        var left = right + 1;
        for (var i = 0; i < Math.Min(2, types.Count); i++)
        {
            left -= 10;
            Energy(c, types[i], left + 1, top, shadow);
        }

        return left + 1;
    }

    /// <summary>A round energy symbol, nine cells across, with its type's pictogram.</summary>
    private static void Energy(CellCanvas c, int typeId, int x, int y, Color? shadow = null)
    {
        var fill = TypeColours.Of(typeId);
        var edge = Mix(fill, Black, 0.55);
        var shine = Mix(fill, White, 0.5);

        bool In(int xx, int yy) => xx >= 0 && yy >= 0 && xx < 9 && yy < 9
                                   && ((xx - 4) * (xx - 4)) + ((yy - 4) * (yy - 4)) <= 20;

        // Su sombra, una celda abajo a la derecha: la energía parece una ficha apoyada en la carta.
        if (shadow is { } dark)
        {
            for (var yy = 0; yy < 9; yy++)
            {
                for (var xx = 0; xx < 9; xx++)
                {
                    if (In(xx, yy) && !In(xx + 1, yy + 1)) c.Put(x + xx + 1, y + yy + 1, dark);
                }
            }
        }

        for (var yy = 0; yy < 9; yy++)
        {
            for (var xx = 0; xx < 9; xx++)
            {
                if (!In(xx, yy)) continue;
                var border = !In(xx - 1, yy) || !In(xx + 1, yy) || !In(xx, yy - 1) || !In(xx, yy + 1);
                c.Put(x + xx, y + yy, border ? edge : (xx + yy <= 4 ? shine : fill));
            }
        }

        if (Pictograms.TryGetValue(typeId, out var glyph))
        {
            for (var gy = 0; gy < 5; gy++)
            {
                for (var gx = 0; gx < 5; gx++)
                {
                    if (glyph[gy][gx] == '#') c.Put(x + 2 + gx, y + 2 + gy, White);
                }
            }
        }
    }

    /// <summary>The small energy dot of a move, five cells across, in its type's colour.</summary>
    private static void EnergyDot(CellCanvas c, int typeId, int x, int y)
    {
        var fill = typeId >= 0 ? TypeColours.Of(typeId) : Mix(InkDim, White, 0.4);
        var edge = Mix(fill, Black, 0.5);
        string[] shape = [".###.", "#+..#", "#...#", "#...#", ".###."];

        for (var yy = 0; yy < 5; yy++)
        {
            for (var xx = 0; xx < 5; xx++)
            {
                var ch = shape[yy][xx];
                if (ch == '#') c.Put(x + xx, y + yy, edge);
                else if (ch == '+') c.Put(x + xx, y + yy, Mix(fill, White, 0.6));
                else if (ch == '.' && xx > 0 && xx < 4 && yy > 0 && yy < 4) c.Put(x + xx, y + yy, fill);
            }
        }
    }

    /// <summary>
    /// The rarity in the corner, right-aligned to <paramref name="right"/>, and before it a tiny capsule if it came out
    /// of the gacha: ● tier 1, ◆ tier 2, ★ tier 3, silver ★ tier 4, gold ★ tier 5.
    /// </summary>
    private static void RarityMarks(CellCanvas c, TcgCard card, int right, int y)
    {
        var x = right - 4;
        string[] circle = [".###.", "#####", "#####", "#####", ".###."];
        string[] diamond = ["..#..", ".###.", "#####", ".###.", "..#.."];
        string[] star = ["..#..", ".###.", "#####", ".###.", "#...#"];

        switch (card.Rarity)
        {
            case 0: Glyph(c, circle, x, y, Ink); break;
            case 1: Glyph(c, diamond, x, y, Ink); break;
            case 2: Glyph(c, star, x, y, Ink); break;
            case 3: Outlined(c, star, x, y, Silver, Ink); break;
            case 4: Outlined(c, star, x, y, Gold, GoldDark); break;
        }

        if (card.FromGacha)
        {
            string[] capsule = [".OOO.", "ORRRO", "OKKKO", "OWWWO", ".OOO."];
            var cx = x - (card.Rarity >= 0 ? 8 : 0);
            for (var yy = 0; yy < 5; yy++)
            {
                for (var xx = 0; xx < 5; xx++)
                {
                    var colour = capsule[yy][xx] switch
                    {
                        'O' => (Color?)Ink,
                        'R' => Rgb(0xE8, 0x40, 0x3C),
                        'K' => Rgb(0x24, 0x20, 0x2C),
                        'W' => White,
                        _ => null
                    };
                    if (colour is { } paint) c.Put(cx + xx, y + yy, paint);
                }
            }
        }
    }

    private static void Glyph(CellCanvas c, string[] glyph, int x, int y, Color colour)
    {
        for (var gy = 0; gy < glyph.Length; gy++)
        {
            for (var gx = 0; gx < glyph[gy].Length; gx++)
            {
                if (glyph[gy][gx] == '#') c.Put(x + gx, y + gy, colour);
            }
        }
    }

    /// <summary>A glyph with a one-cell outline around it, for the silver and gold stars.</summary>
    private static void Outlined(CellCanvas c, string[] glyph, int x, int y, Color fill, Color edge)
    {
        bool On(int gx, int gy) => gy >= 0 && gy < glyph.Length && gx >= 0 && gx < glyph[gy].Length && glyph[gy][gx] == '#';

        for (var gy = -1; gy <= glyph.Length; gy++)
        {
            for (var gx = -1; gx <= glyph[0].Length; gx++)
            {
                if (!On(gx, gy) && (On(gx - 1, gy) || On(gx + 1, gy) || On(gx, gy - 1) || On(gx, gy + 1)))
                {
                    c.Put(x + gx, y + gy, edge);
                }
            }
        }

        Glyph(c, glyph, x, y, fill);
    }

    private static void Sparkle(CellCanvas c, int x, int y, int size)
    {
        c.Put(x, y, White);
        for (var k = 1; k <= size; k++)
        {
            c.Put(x - k, y, White);
            c.Put(x + k, y, White);
            c.Put(x, y - k, White);
            c.Put(x, y + k, White);
        }
    }

    /// <summary>A cell of foil for the i-th sparkle, from the card's seed, or (-1, -1) when the card has no foil.</summary>
    private static (int X, int Y) FoilSpot(TcgRender render, uint seed, int i)
    {
        var width = render.Canvas.Width;
        var height = render.Canvas.Height;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var x = (int)(Hash(i, attempt, seed + 17) * width);
            var y = (int)(Hash(attempt, i, seed + 29) * height);
            if (x > 1 && y > 1 && x < width - 2 && y < height - 2 && render.Regions[(y * width) + x] != TcgRegion.None)
            {
                return (x, y);
            }
        }

        return (-1, -1);
    }

    // ====================================================================================================== BACK

    /// <summary>The back of a card with the Pokémon's sheet: what the game's summary says, in print.</summary>
    private static void DrawSheet(CellCanvas c, TcgCard card, TcgFinish finish)
    {
        var type = TypeColours.Of(card.MainType);
        var paper = Mix(type, White, 0.82);
        Body(c, 3, paper, Mix(type, White, 0.88), Mix(type, White, 0.72), finish == TcgFinish.Gold);

        // El nombre de la especie en una banda de su tipo y, si cabe a su lado, su número de la Pokédex.
        var band = Mix(type, White, 0.42);
        Plate(c, 5, 4, FullWidth - 10, 14, band, Mix(type, Black, 0.35));
        var number = $"N.{card.Species}";
        var title = PixelFont.Trim(card.SpeciesName, FullWidth - 14);
        Font(c, title, 8, 5, Mix(band, Black, 0.25));
        Font(c, title, 7, 4, Ink);
        if (PixelFont.Measure(title) + 4 + SmallWidth(number) <= FullWidth - 14)
        {
            Small(c, number, FullWidth - 8 - SmallWidth(number), 9, Mix(band, Black, 0.55));
        }

        // Las estadísticas en un pozo un poco más oscuro, como la tabla del dorso de una carta.
        Plate(c, 5, 36, FullWidth - 10, 43, Mix(paper, Black, 0.05), Mix(paper, Black, 0.16));

        // Habilidad (marca roja, como en las cartas), naturaleza con lo que sube y baja, y objeto (marca azul).
        c.Rect(6, 19, 3, 5, Rgb(0xC8, 0x40, 0x2E));
        Small(c, SmallTrim(card.Ability, FullWidth - 6 - 11), 11, 19, Ink);

        Small(c, SmallTrim(card.Nature, 30), 6, 25, Ink);
        if (card.NatureUp >= 0 && card.NatureDown >= 0 && card.NatureUp != card.NatureDown)
        {
            var down = $"-{StatShort[card.NatureDown]}";
            var up = $"+{StatShort[card.NatureUp]}";
            var downX = FullWidth - 6 - SmallWidth(down);
            Small(c, down, downX, 25, Down);
            Small(c, up, downX - 3 - SmallWidth(up), 25, Up);
        }

        c.Rect(6, 31, 3, 5, Rgb(0x3A, 0x6A, 0xC8));
        Small(c, SmallTrim(card.Item.Length > 0 ? card.Item : "Sin objeto", FullWidth - 6 - 11), 11, 31, card.Item.Length > 0 ? Ink : InkDim);

        // Las estadísticas: el valor, el IV como barra (dorada si es perfecto) y los EV.
        const int valueRight = 29;
        const int barLeft = 32;
        const int barWidth = 17;
        var evRight = FullWidth - 7;
        Small(c, "VAL", valueRight - SmallWidth("VAL") + 1, 38, InkDim);
        Small(c, "IV", barLeft + ((barWidth - SmallWidth("IV")) / 2), 38, InkDim);
        Small(c, "EV", evRight - SmallWidth("EV") + 1, 38, InkDim);

        for (var i = 0; i < 6; i++)
        {
            var y = 44 + (i * 6);
            var label = i == card.NatureUp && card.NatureUp != card.NatureDown ? Up
                : i == card.NatureDown && card.NatureUp != card.NatureDown ? Down : Ink;
            Small(c, StatShort[i], 6, y, label);

            var value = i < card.Stats.Count ? card.Stats[i].ToString() : "-";
            Small(c, value, valueRight - SmallWidth(value) + 1, y, Ink);

            var iv = i < card.Ivs.Count ? Math.Clamp(card.Ivs[i], 0, 31) : 0;
            var filled = (int)Math.Round(iv / 31.0 * barWidth);
            for (var bx = 0; bx < barWidth; bx++)
            {
                var colour = bx < filled ? (iv == 31 ? Gold : Mix(type, Black, 0.2)) : Mix(paper, Black, 0.12);
                c.Rect(barLeft + bx, y + 1, 1, 3, colour);
            }

            var ev = i < card.Evs.Count ? card.Evs[i].ToString() : "0";
            Small(c, ev, evRight - SmallWidth(ev) + 1, y, card.Evs.Count > i && card.Evs[i] > 0 ? Ink : InkDim);
        }

        // Dónde y a qué nivel se capturó.
        Small(c, SmallTrim(card.MetLocation.Length > 0 ? card.MetLocation : "Origen desconocido", FullWidth - 12), 6, 81, Ink);
        Labelled(c, "CAPT. A NV", card.MetLevel > 0 ? card.MetLevel.ToString() : "?", 6, 87);

        if (card.Fallen)
        {
            // Una raya roja bajo el nombre: la ficha también dice que cayó.
            c.Rect(6, 18, FullWidth - 12, 1, Up);
        }
    }

    /// <summary>
    /// The back all cards share, violet like PermaLocke, with a Poké Ball in the middle; an egg's card is this, face
    /// down, with its label.
    /// </summary>
    private static void DrawCardBack(CellCanvas c, bool egg)
    {
        var w = c.Width;
        var h = c.Height;
        var deep = Rgb(0x1E, 0x15, 0x36);
        var violet = Rgb(0x3A, 0x28, 0x6A);
        var glow = Rgb(0x6E, 0x4E, 0xB8);
        var rimLight = Rgb(0x9A, 0x7A, 0xE0);
        var rimDark = Rgb(0x2A, 0x1E, 0x4E);

        var cx = (w - 1) / 2.0;
        var cy = (h - 1) / 2.0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var d = Math.Min(Math.Min(x, y), Math.Min(w - 1 - x, h - 1 - y));
                var corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                if (corner) continue;

                if (d == 0 || (d == 1 && (x == 1 || x == w - 2) && (y == 1 || y == h - 2)))
                {
                    c.Put(x, y, Outline);
                }
                else if (d <= 3)
                {
                    c.Put(x, y, d == 1 ? (x == 1 || y == 1 ? rimLight : rimDark) : Rgb(0x52, 0x3A, 0x96));
                }
                else
                {
                    // Un remolino: anillos a medio tono alrededor del centro, como el dorso de las cartas.
                    var r = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow((y - cy) * 0.8, 2));
                    var angle = Math.Atan2(y - cy, x - cx);
                    var band = (r + (angle * 3)) % 8;
                    c.Put(x, y, band < 3 ? (Dither(x, y, 0.5) ? violet : deep) : band < 5 ? violet : deep);
                }
            }
        }

        // La Poké Ball.
        var radius = Math.Min(w, h) * 0.2;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dx = x + 0.5 - (cx + 0.5);
                var dy = y + 0.5 - (cy + 0.5);
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > radius + 1.2)
                {
                    if (r < radius + 3 && Dither(x, y, 0.5)) c.Put(x, y, glow);
                    continue;
                }

                Color colour;
                if (r > radius) colour = Outline;
                else if (Math.Abs(dy) < 1.2) colour = Outline;
                else if (r < 3.2) colour = r < 1.8 ? White : Outline;
                else colour = dy < 0 ? (dx + dy < -radius * 0.8 ? Rgb(0xFF, 0x8A, 0x7A) : Rgb(0xE0, 0x3A, 0x34)) : White;

                c.Put(x, y, colour);
            }
        }

        var label = egg ? "HUEVO" : "PERMALOCKE";
        var labelY = h - 12;
        Small(c, label, (w - SmallWidth(label)) / 2, labelY, Rgb(0xE6, 0xDC, 0xFF));
    }

    // ====================================================================================================== RUIN

    /// <summary>
    /// The card of a fallen Pokémon: drained of colour, creased, and half burnt from a corner, with a charred edge where
    /// the embers glow. Where it burnt there is nothing, and the pocket shows through.
    /// </summary>
    /// <param name="mirrored">The back: the same burn seen from behind, so its corner is on the other side.</param>
    private static void Ruin(CellCanvas c, uint seed, List<(int X, int Y, double Phase)> embers, bool mirrored = false)
    {
        var w = c.Width;
        var h = c.Height;

        // Sin color, con un poco de sepia.
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!c.IsSet(x, y)) continue;
                var colour = c.At(x, y);
                var grey = (byte)((0.3 * colour.R) + (0.59 * colour.G) + (0.11 * colour.B));
                c.Put(x, y, Mix(Mix(colour, Color.FromRgb(grey, grey, grey), 0.6), Sepia, 0.2));
            }
        }

        // Arrugada: tres dobleces de lado a lado, claros por un lado y oscuros por el otro, y una textura de pliegues.
        for (var k = 0; k < 3; k++)
        {
            var fromLeft = Hash(k, 1, seed) > 0.5;
            var (x0, y0, x1, y1) = fromLeft
                ? (0, (int)(Hash(k, 2, seed) * h), w - 1, (int)(Hash(k, 3, seed) * h))
                : ((int)(Hash(k, 2, seed) * w), 0, (int)(Hash(k, 3, seed) * w), h - 1);
            if (mirrored)
            {
                (x0, x1) = (w - 1 - x0, w - 1 - x1);
            }

            Crease(c, x0, y0, x1, y1);
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (c.IsSet(x, y) && Hash(x / 3, y / 3, seed + 5) < 0.14 && Dither(x, y, 0.35))
                {
                    c.Put(x, y, Mix(c.At(x, y), Black, 0.14));
                }
            }
        }

        // Medio quemada desde una esquina: abajo a la derecha, abajo a la izquierda o arriba a la derecha.
        var corner = seed % 3;
        var (cu, cv) = corner switch { 0 => (1.0, 1.0), 1 => (0.0, 1.0), _ => (1.0, 0.0) };
        if (mirrored)
        {
            cu = 1 - cu;
        }
        const double reach = 0.6;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!c.IsSet(x, y)) continue;

                var u = x / (double)(w - 1);
                var v = y / (double)(h - 1);
                var edge = Math.Sqrt(Math.Pow(u - cu, 2) + Math.Pow((v - cv) * 0.75, 2))
                           + ((Noise(x, y, 5, seed) - 0.5) * 0.22)
                           + ((Hash(x, y, seed + 7) - 0.5) * 0.03);

                if (edge < reach)
                {
                    c.Erase(x, y);
                }
                else if (edge < reach + 0.025)
                {
                    c.Put(x, y, Char);
                    if (Hash(x, y, seed + 3) < 0.3)
                    {
                        embers.Add((x, y, Hash(x, y, seed + 9)));
                    }
                }
                else if (edge < reach + 0.06)
                {
                    c.Put(x, y, Dither(x, y, 0.5) ? CharBrown : Char);
                }
                else if (edge < reach + 0.14)
                {
                    var level = 1 - ((edge - reach - 0.06) / 0.08);
                    c.Put(x, y, Mix(c.At(x, y), Scorch, Dither(x, y, level) ? 0.55 : 0.22));
                }
            }
        }
    }

    /// <summary>A crease: a light line with a dark one beside it, like a fold that caught the light.</summary>
    private static void Crease(CellCanvas c, int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            if (c.IsSet(x0, y0)) c.Put(x0, y0, Mix(c.At(x0, y0), White, 0.3));
            if (c.IsSet(x0 + 1, y0 + 1)) c.Put(x0 + 1, y0 + 1, Mix(c.At(x0 + 1, y0 + 1), Black, 0.25));

            if (x0 == x1 && y0 == y1) break;
            var twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    /// <summary>Smooth value noise in [0, 1): random values on a lattice every <paramref name="step"/> cells, blended.</summary>
    private static double Noise(int x, int y, int step, uint seed)
    {
        var gx = x / step;
        var gy = y / step;
        var fx = Smooth((x % step) / (double)step);
        var fy = Smooth((y % step) / (double)step);

        var top = Lerp(Hash(gx, gy, seed), Hash(gx + 1, gy, seed), fx);
        var bottom = Lerp(Hash(gx, gy + 1, seed), Hash(gx + 1, gy + 1, seed), fx);
        return Lerp(top, bottom, fy);

        static double Smooth(double t) => t * t * (3 - (2 * t));
        static double Lerp(double a, double b, double t) => a + ((b - a) * t);
    }

    // ====================================================================================================== TEXT

    /// <summary>
    /// The N of the small font is drawn four cells wide on the cards: the app's three-cell one reads as a D in a long
    /// word, and a card is all long Spanish words («LANZALLAMAS»).
    /// </summary>
    private static readonly string[] WideN = ["#..#", "##.#", "#.##", "#..#", "#..#"];

    private static int GlyphWidth(char ch) => ch is 'N' or 'Ñ' or 'n' or 'ñ' ? 4 : 3;

    /// <summary>Cells a line of the small font takes.</summary>
    public static int SmallWidth(string text)
    {
        var width = 0;
        foreach (var ch in text)
        {
            width += GlyphWidth(ch) + 1;
        }

        return Math.Max(0, width - 1);
    }

    /// <summary>A dim label and its value, «NV 45»; returns the column after the value.</summary>
    /// <param name="gap">Empty cells between the two: two on the full card, one on the small one.</param>
    private static int Labelled(CellCanvas c, string label, string value, int x, int y, int gap = 2)
    {
        Small(c, label, x, y, InkDim);
        var valueX = x + SmallWidth(label) + 1 + gap;
        Small(c, value, valueX, y, Ink);
        return valueX + SmallWidth(value);
    }

    /// <summary>
    /// The small font in capitals, three cells by five. An accented vowel carries its accent on the row right above,
    /// so lines six rows apart do not touch.
    /// </summary>
    private static void Small(CellCanvas c, string text, int x, int y, Color colour)
    {
        foreach (var raw in text.ToUpperInvariant())
        {
            var ch = raw switch
            {
                'Á' or 'À' => 'A',
                'É' or 'È' => 'E',
                'Í' or 'Ì' => 'I',
                'Ó' or 'Ò' => 'O',
                'Ú' or 'Ù' or 'Ü' => 'U',
                '−' or '–' => '-',
                _ => raw
            };

            if (ch != raw && ch != '-')
            {
                c.Put(x + 1, y - 1, colour);
            }

            if (raw == 'Ñ')
            {
                c.Rect(x, y - 1, 4, 1, colour);
            }

            var glyph = ch is 'N' or 'Ñ' ? WideN : SmallFont.Glyph(ch);
            if (glyph is not null)
            {
                for (var gy = 0; gy < 5; gy++)
                {
                    for (var gx = 0; gx < glyph[gy].Length; gx++)
                    {
                        if (glyph[gy][gx] == '#') c.Put(x + gx, y + gy, colour);
                    }
                }
            }

            x += GlyphWidth(ch) + 1;
        }
    }

    /// <summary>The small font of the cards, for the album's headers and tabs.</summary>
    internal static void SmallText(CellCanvas c, string text, int x, int y, Color colour) => Small(c, text, x, y, colour);

    /// <summary>Text in the small font cut to fit <paramref name="cells"/>, with a point where it was cut.</summary>
    public static string SmallTrim(string text, int cells)
    {
        var upper = text.ToUpperInvariant().Trim();
        if (SmallWidth(upper) <= cells)
        {
            return upper;
        }

        var cut = upper.Length;
        while (cut > 1 && SmallWidth(upper[..cut].TrimEnd() + ".") > cells)
        {
            cut--;
        }

        return upper[..cut].TrimEnd() + ".";
    }

    /// <summary>The application's big pixel font, lower case and accents included.</summary>
    private static void Font(CellCanvas c, string text, int x, int y, Color colour) =>
        PixelFont.Draw(text, x, y, (px, py) => c.Put(px, py, colour));
}
