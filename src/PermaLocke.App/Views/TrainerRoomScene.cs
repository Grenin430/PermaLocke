using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>A picture from the cartridge, as the room draws it: BGRA, one icon pixel per cell.</summary>
public sealed record RoomSprite(byte[] Bgra, int Width, int Height)
{
    public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Bgra[(((y * Width) + x) * 4) + 3] >= 128;

    public Color At(int x, int y)
    {
        var at = ((y * Width) + x) * 4;
        return Color.FromRgb(Bgra[at + 2], Bgra[at + 1], Bgra[at]);
    }

    public static RoomSprite? From(BitmapSource? sprite)
    {
        if (sprite is null) return null;

        var converted = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
        var bgra = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(bgra, converted.PixelWidth * 4, 0);
        return new RoomSprite(bgra, converted.PixelWidth, converted.PixelHeight);
    }
}

/// <param name="Sprite">The crystal's own picture from the cartridge; null when it could not be read.</param>
/// <param name="Won">Whether its trial has been passed: then it shines, otherwise only its shape is there.</param>
public sealed record RoomCrystal(RoomSprite? Sprite, bool Won);

/// <summary>Everything the room shows, all of it read from the run and the cartridge.</summary>
/// <param name="Hour">Alola's hour, for the window.</param>
/// <param name="Team">The living Pokémon, up to six, standing on the rug.</param>
/// <param name="Fallen">The fallen, most recent first: a stone figurine each on the memorial shelf, up to eight.</param>
/// <param name="Crystals">The twelve trials in order, for the cabinet.</param>
/// <param name="Tv">The last killcam frame, for the television; null shows static.</param>
public sealed record TrainerRoomState(
    double Hour,
    IReadOnlyList<RoomSprite> Team,
    IReadOnlyList<RoomSprite> Fallen,
    int FallenCount,
    IReadOnlyList<RoomCrystal> Crystals,
    bool Champion,
    int LevelCap,
    int Stickers,
    int StickersTarget,
    int InPc,
    RoomSprite? Tv);

/// <summary>
/// The trainer's room on JUGAR's cover: a bedroom drawn cell by cell that fills up as the run goes on.
/// </summary>
/// <remarks>
/// <para>
/// Asked for on 2026-09-22 in a section that already exists, not a new one; tried on HOME first and moved to JUGAR,
/// where it replaces the beach (§169). Every object in it
/// says something true about the run: the cabinet holds the Z-Crystal of each trial passed, drawn from the player's
/// cartridge, with the missing ones as dark shapes of the crystal still to come; the memorial shelf has a stone
/// figurine of each fallen Pokémon made from its own sprite; the television plays the last killcam; the poster is the
/// level cap; the corkboard, the Totem Stickers; the living team hops on the rug; and the window is Alola at the
/// game's hour, as in the header.
/// </para>
/// <para>
/// Drawn the way the rest of the pixel art is (§116, §120): whole cells, a few flat colours from the application's
/// violet-tinted darks, ordered dithering instead of gradients, shapes typed in by hand. Moving things move in steps.
/// </para>
/// </remarks>
public sealed class TrainerRoomScene
{
    /// <summary>The composition is designed on this many columns and centred; wider panels get more wall.</summary>
    public const int DesignWidth = 430;

    /// <summary>The room's height as designed; a taller panel gets more wall above, and the floor stays at the bottom.</summary>
    public const int DesignRows = 72;

    private const int FloorTop = 50;
    private const int WainscotTop = 38;

    private static readonly Color Outline = Rgb(0x0B, 0x09, 0x10);
    private static readonly Color WallShadow = Rgb(0x19, 0x14, 0x25);
    private static readonly Color Wall = Rgb(0x26, 0x1F, 0x39);
    private static readonly Color WallStripe = Rgb(0x2E, 0x26, 0x45);
    private static readonly Color WoodDark = Rgb(0x2E, 0x22, 0x26);
    private static readonly Color WoodMid = Rgb(0x45, 0x33, 0x30);
    private static readonly Color WoodLight = Rgb(0x5E, 0x47, 0x3B);
    private static readonly Color WoodTrim = Rgb(0x78, 0x5B, 0x48);
    private static readonly Color FloorDark = Rgb(0x2A, 0x20, 0x24);
    private static readonly Color FloorMid = Rgb(0x3A, 0x2C, 0x2C);
    private static readonly Color FloorLight = Rgb(0x48, 0x37, 0x33);
    private static readonly Color RugBase = Rgb(0x4A, 0x30, 0x70);
    private static readonly Color RugDark = Rgb(0x36, 0x22, 0x52);
    private static readonly Color RugLight = Rgb(0x6B, 0x47, 0xA2);
    private static readonly Color Gold = Rgb(0xD9, 0xAE, 0x48);
    private static readonly Color GoldLight = Rgb(0xFF, 0xEB, 0x9C);
    private static readonly Color GoldDark = Rgb(0x95, 0x6E, 0x26);
    private static readonly Color Glass = Rgb(0x2E, 0x3E, 0x58);
    private static readonly Color GlassShine = Rgb(0x6F, 0x8C, 0xAC);
    private static readonly Color TvBody = Rgb(0x3E, 0x3A, 0x4A);
    private static readonly Color TvLight = Rgb(0x5E, 0x59, 0x6E);
    private static readonly Color TvDark = Rgb(0x24, 0x21, 0x2D);
    private static readonly Color Screen = Rgb(0x0A, 0x0D, 0x12);
    private static readonly Color Paper = Rgb(0xE6, 0xDC, 0xC4);
    private static readonly Color PaperShade = Rgb(0xC4, 0xB7, 0x98);
    private static readonly Color InkRed = Rgb(0xB8, 0x3E, 0x3A);
    private static readonly Color StoneDark = Rgb(0x45, 0x40, 0x54);
    private static readonly Color StoneMid = Rgb(0x6B, 0x65, 0x80);
    private static readonly Color StoneLight = Rgb(0x96, 0x90, 0xAA);
    private static readonly Color Wax = Rgb(0xE8, 0xE0, 0xD0);
    private static readonly Color Flame = Rgb(0xF2, 0xA6, 0x3B);
    private static readonly Color FlameCore = Rgb(0xFF, 0xE9, 0xA8);
    private static readonly Color Cork = Rgb(0x9E, 0x77, 0x48);
    private static readonly Color CorkDark = Rgb(0x7A, 0x5A, 0x36);
    private static readonly Color LeafLight = Rgb(0x5E, 0xA8, 0x4C);
    private static readonly Color LeafDark = Rgb(0x2F, 0x6A, 0x3A);
    private static readonly Color Pot = Rgb(0x8A, 0x4B, 0x32);
    private static readonly Color BoxGreen = Rgb(0x23, 0x4A, 0x32);
    private static readonly Color BoxGreenLight = Rgb(0x35, 0x66, 0x46);
    private static readonly Color Curtain = Rgb(0x5A, 0x2E, 0x4E);
    private static readonly Color CurtainDark = Rgb(0x3E, 0x1E, 0x36);

    /// <summary>Colours for the Totem Stickers pinned on the corkboard, in turn.</summary>
    private static readonly Color[] StickerColours =
        [Rgb(0xE0, 0x5A, 0x4A), Rgb(0x4A, 0x9A, 0xE0), Rgb(0x6A, 0xC0, 0x5A), Rgb(0xE8, 0xC2, 0x3E), Rgb(0xB0, 0x6A, 0xD8)];

    /// <summary>Digits and the few letters the room writes, three cells wide and five tall.</summary>
    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['0'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['1'] = [".#.", "##.", ".#.", ".#.", "###"],
        ['2'] = ["###", "..#", "###", "#..", "###"],
        ['3'] = ["###", "..#", ".##", "..#", "###"],
        ['4'] = ["#.#", "#.#", "###", "..#", "..#"],
        ['5'] = ["###", "#..", "###", "..#", "###"],
        ['6'] = ["###", "#..", "###", "#.#", "###"],
        ['7'] = ["###", "..#", ".#.", ".#.", ".#."],
        ['8'] = ["###", "#.#", "###", "#.#", "###"],
        ['9'] = ["###", "#.#", "###", "..#", "###"],
        ['/'] = ["..#", "..#", ".#.", "#..", "#.."],
        ['N'] = ["#.#", "###", "###", "#.#", "#.#"],
        ['V'] = ["#.#", "#.#", "#.#", "#.#", ".#."],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
        ['O'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['P'] = ["###", "#.#", "###", "#..", "#.."],
        ['E'] = ["###", "#..", "##.", "#..", "###"],
        ['+'] = ["...", ".#.", "###", ".#.", "..."]
    };

    private static readonly string[] Trophy =
    [
        ".gGGGGGGGg.",
        "gGYYYYYYYGg",
        "G.GYYYYYG.G",
        "G.GYYYYYG.G",
        ".GGYYYYYGG.",
        "...GYYYG...",
        "....GYG....",
        "....GYG....",
        "...GGGGG...",
        "..ddddddd.."
    ];

    private readonly byte[] _background;
    private readonly byte[] _frame;
    private readonly int _ox;
    private readonly int _oy;

    private readonly int _covered;

    /// <param name="covered">
    /// Rows at the bottom that something is drawn over — JUGAR's play bar overlaps the cover by 22 pixels. The room sits
    /// above them and the floor carries on underneath, so no one's feet end up behind the bar.
    /// </param>
    public TrainerRoomScene(int width, int height = DesignRows, int covered = 0)
    {
        _covered = Math.Max(0, covered);
        Width = Math.Max(DesignWidth, width);
        Height = Math.Max(DesignRows + _covered, height);
        _ox = (Width - DesignWidth) / 2;
        _oy = Height - DesignRows - _covered;
        _background = new byte[Width * Height * 4];
        _frame = new byte[Width * Height * 4];
        Bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
        PaintRoom();
    }

    public int Width { get; }

    public int Height { get; }

    public WriteableBitmap Bitmap { get; }

    /// <param name="still">The game is open: the team stands instead of hopping, as the beach did before.</param>
    public void Render(TrainerRoomState state, double seconds, bool still = false)
    {
        Buffer.BlockCopy(_background, 0, _frame, 0, _frame.Length);
        var step = (int)(seconds * 4);

        DrawWindow(state.Hour);
        DrawBed();
        DrawPoster(state.LevelCap);
        DrawCabinet(state.Crystals, step);
        DrawTelevision(state.Tv, state.Champion, step);
        DrawMemorial(state.Fallen, state.FallenCount, step);
        DrawDesk(state.InPc);
        DrawCorkboard(state.Stickers, state.StickersTarget);
        DrawTeam(state.Team, step, still);

        Bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), _frame, Width * 4, 0);
    }

    /// <summary>The frame as BGRA, for a picture of the room outside the screen.</summary>
    public byte[] Pixels => _frame;

    // ------------------------------------------------------------------ the room itself, painted once

    private void PaintRoom()
    {
        var target = _background;

        for (var y = -_oy; y < DesignRows + _covered; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                Color colour;

                if (y < WainscotTop)
                {
                    // Papel pintado: rayas verticales anchas, oscureciendo hacia el techo con trama.
                    colour = ((x / 6) % 2 == 0) ? Wall : WallStripe;
                    if (y + _oy < 6 && Bayer[(y + _oy) % 4, x % 4] < (6 - y - _oy) * 3) colour = WallShadow;
                }
                else if (y < FloorTop)
                {
                    // Zócalo de madera: una moldura arriba, paneles con su marco.
                    colour = y == WainscotTop ? WoodTrim
                        : y == WainscotTop + 1 ? WoodDark
                        : ((x % 24) is 0 or 23 || y == FloorTop - 2) ? WoodDark
                        : ((x % 24) == 1 || y == WainscotTop + 2) ? WoodLight
                        : WoodMid;
                }
                else
                {
                    // Tarima: tablas de cinco filas con las juntas al tresbolillo.
                    var plank = (y - FloorTop) / 5;
                    var joint = ((x + (plank % 2 * 17)) % 34) == 0;
                    colour = (y - FloorTop) % 5 == 0 || joint ? FloorDark
                        : (y - FloorTop) % 5 == 1 ? FloorLight
                        : FloorMid;

                    if (y == FloorTop) colour = Outline;
                }

                Put(target, x, y, colour);
            }
        }

        // La alfombra, bajo el equipo: violeta con cenefa dorada y rombos.
        var rugLeft = _ox + 150;
        var rugRight = _ox + 340;

        for (var y = 56; y < DesignRows - 2; y++)
        {
            for (var x = rugLeft; x < rugRight; x++)
            {
                var edge = y == 56 || y == DesignRows - 3 || x == rugLeft || x == rugRight - 1;
                var border = y == 58 || y == DesignRows - 5 || x == rugLeft + 2 || x == rugRight - 3;
                var diamond = Math.Abs(((x - rugLeft) % 12) - 6) + Math.Abs(((y - 56) % 8) - 4) == 4;

                Put(target, x, y, edge ? Outline : border ? Gold : diamond ? RugLight : ((x + y) % 2 == 0 ? RugBase : RugDark));
            }
        }
    }

    // ------------------------------------------------------------------ objects

    private void DrawWindow(double hour)
    {
        const int left = 12, top = 6, width = 50, height = 27;
        var ox = _ox + left;
        var (skyTop, skyMiddle, skyLow) = SkyAt(hour);
        var sea = Lerp(skyLow, Rgb(0x10, 0x1E, 0x3A), 0.55);
        var night = NightAmount(hour);

        // El cristal: cielo en tres tonos con trama, estrellas de noche, mar abajo con una isla.
        for (var y = top + 2; y < top + height - 2; y++)
        {
            for (var x = ox + 2; x < ox + width - 2; x++)
            {
                var along = (y - top - 2) / (double)(height - 4);
                Color colour;

                if (along > 0.72)
                {
                    colour = (x + y) % 3 == 0 ? Lerp(sea, Rgb(0xFF, 0xFF, 0xFF), 0.10) : sea;
                }
                else
                {
                    var t = along / 0.72;
                    colour = t < 0.5
                        ? (Bayer[y % 4, x % 4] < t * 32 ? skyMiddle : skyTop)
                        : (Bayer[y % 4, x % 4] < (t - 0.5) * 32 ? skyLow : skyMiddle);

                    if (night > 0.5 && ((x * 7) + (y * 13)) % 37 == 0) colour = Star;
                }

                Put(_frame, x, y, colour);
            }
        }

        // La isla del fondo, y el sol o la luna en su arco.
        int[] island = [1, 2, 3, 4, 5, 6, 5, 4, 4, 3, 2, 1];
        var seaTop = top + 2 + (int)((height - 4) * 0.72);
        for (var i = 0; i < island.Length; i++)
        {
            for (var h = 0; h < island[i]; h++) Put(_frame, ox + 28 + i, seaTop - h, Lerp(sea, Outline, 0.55));
        }

        var (isDay, along2) = Body(hour);
        var bx = ox + 4 + (int)(along2 * (width - 12));
        var by = top + 3 + (int)(Math.Pow((along2 * 2) - 1, 2) * 9);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                if ((x is 0 or 3) && (y is 0 or 3)) continue;
                Put(_frame, bx + x, by + y, isDay ? (x + y < 3 ? SunHigh : SunLow) : (x > 1 && y < 2 ? MoonShade : MoonLight));
            }
        }

        // Marco, parteluces, alféizar y cortinas.
        Frame(ox, top, width, height, WoodLight, WoodDark);
        Rect(ox + (width / 2), top + 2, 1, height - 4, WoodLight);
        Rect(ox + 2, top + (height / 2) - 2, width - 4, 1, WoodLight);
        Rect(ox - 3, top + height, width + 6, 2, WoodTrim);
        Rect(ox - 3, top + height + 2, width + 6, 1, Outline);
        Rect(ox - 5, top - 2, width + 10, 1, WoodTrim);

        foreach (var cx in new[] { ox - 5, ox + width - 2 })
        {
            for (var y = top - 1; y < top + height + 4; y++)
            {
                for (var x = 0; x < 7; x++)
                {
                    Put(_frame, cx + x, y, x is 0 or 6 ? Outline : (x % 2 == 0 ? CurtainDark : Curtain));
                }
            }
        }

        // Una maceta en el alféizar.
        DrawPlant(ox + 6, top + height - 1);
    }

    /// <summary>The bed under the window: decoration, and what keeps the floor from being a bare strip.</summary>
    private void DrawBed()
    {
        var ox = _ox + 6;

        // Cabecero y piecero de madera, con las patas en el suelo.
        Rect(ox, 38, 5, 20, WoodMid);
        Rect(ox, 38, 5, 1, WoodTrim);
        Border(ox - 1, 37, 7, 22, Outline);
        Rect(ox + 60, 43, 4, 15, WoodMid);
        Rect(ox + 60, 43, 4, 1, WoodTrim);
        Border(ox + 59, 42, 6, 17, Outline);

        // Colchón, manta violeta con su doblez y la almohada.
        Rect(ox + 5, 47, 55, 7, RugBase);
        Rect(ox + 5, 47, 55, 1, RugLight);
        Rect(ox + 5, 53, 55, 1, RugDark);
        Rect(ox + 5, 54, 55, 2, Paper);
        Rect(ox + 5, 56, 55, 1, PaperShade);
        Rect(ox + 24, 47, 2, 7, RugLight);
        Border(ox + 4, 46, 57, 12, Outline);
        Rect(ox + 6, 43, 14, 4, Paper);
        Rect(ox + 6, 46, 14, 1, PaperShade);
        Border(ox + 5, 42, 16, 6, Outline);
    }

    private void DrawPlant(int x, int bottom)
    {
        string[] plant =
        [
            "..l.L..",
            ".lL.Ll.",
            "LlLlLlL",
            ".lLLLl.",
            "..LlL..",
            ".ppppp.",
            ".PPPPP.",
            "..PPP.."
        ];

        Shape(plant, x, bottom - plant.Length + 1, c => c switch
        {
            'l' => LeafLight,
            'L' => LeafDark,
            'p' => Lerp(Pot, Rgb(0xFF, 0xFF, 0xFF), 0.2),
            'P' => Pot,
            _ => (Color?)null
        });
    }

    private void DrawPoster(int cap)
    {
        const int left = 76, top = 6, width = 30, height = 29;
        var ox = _ox + left;

        Rect(ox, top, width, height, Paper);
        Rect(ox + 1, top + height - 2, width - 2, 1, PaperShade);
        Rect(ox + width - 2, top + 1, 1, height - 2, PaperShade);
        Border(ox, top, width, height, Outline);
        Put(_frame, ox + (width / 2), top - 1, InkRed);
        Put(_frame, ox + (width / 2), top, InkRed);

        Text("TOPE", ox + 7, top + 3, InkRed, 1);
        var number = Math.Clamp(cap, 0, 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var wide = (number.Length * 4 * 3) - 3;
        Text(number, ox + ((width - wide) / 2), top + 10, Rgb(0x2A, 0x22, 0x30), 3);
    }

    private void DrawCabinet(IReadOnlyList<RoomCrystal> crystals, int step)
    {
        const int left = 112, top = 3, width = 62, height = 48;
        var ox = _ox + left;

        // Cuerpo de madera, cornisa y patas.
        Rect(ox, top, width, height, WoodMid);
        Rect(ox - 2, top - 1, width + 4, 3, WoodTrim);
        Border(ox - 2, top - 1, width + 4, 3, Outline);
        Rect(ox, top + height - 5, width, 5, WoodDark);
        Rect(ox + 1, top + height - 5, width - 2, 1, WoodLight);
        Border(ox, top, width, height, Outline);

        // El cristal del frente: la pared se ve a través, con reflejos en diagonal.
        const int glassTop = top + 4, glassBottom = top + height - 7;
        for (var y = glassTop; y < glassBottom; y++)
        {
            for (var x = ox + 3; x < ox + width - 3; x++)
            {
                var shine = ((x - ox) + (y - glassTop)) % 37 == 0;
                Put(_frame, x, y, shine ? Lerp(Glass, GlassShine, 0.5) : (Bayer[y % 4, x % 4] < 8 ? Glass : Lerp(Glass, WallShadow, 0.5)));
            }
        }

        Border(ox + 2, glassTop - 1, width - 4, glassBottom - glassTop + 2, WoodDark);

        // Tres baldas de cuatro huecos: los doce cristales, en el orden de las pruebas.
        for (var shelf = 0; shelf < 3; shelf++)
        {
            var shelfY = glassTop + 12 + (shelf * 12);
            if (shelfY >= glassBottom) shelfY = glassBottom - 1;
            Rect(ox + 3, shelfY, width - 6, 1, WoodLight);
            Rect(ox + 3, shelfY + 1, width - 6, 1, WoodDark);

            for (var slot = 0; slot < 4; slot++)
            {
                var index = (shelf * 4) + slot;
                var crystal = index < crystals.Count ? crystals[index] : new RoomCrystal(null, false);
                DrawCrystal(crystal, ox + 10 + (slot * 14), shelfY, index, step);
            }
        }
    }

    /// <summary>
    /// A crystal at half its size, standing on its shelf: in colour when won, as the shape of the crystal still to come
    /// when not — the same sprite, so the empty slot already says which one it waits for.
    /// </summary>
    private void DrawCrystal(RoomCrystal crystal, int centre, int shelfY, int index, int step)
    {
        if (crystal.Sprite is not { } sprite)
        {
            Rect(centre - 3, shelfY - 1, 7, 1, WallShadow);
            return;
        }

        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var w = (maxX - minX + 2) / 2;
        var h = (maxY - minY + 2) / 2;
        var left = centre - (w / 2);
        var top = shelfY - h;
        var shape = Lerp(WallShadow, GlassShine, 0.30);
        var shapeEdge = Lerp(WallShadow, GlassShine, 0.55);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var sx = minX + (x * 2);
                var sy = minY + (y * 2);

                if (!Neighbour(sprite, sx, sy, 0, 0)) continue;

                var edge = !Neighbour(sprite, sx, sy, -2, 0) || !Neighbour(sprite, sx, sy, 2, 0) || !Neighbour(sprite, sx, sy, 0, -2)
                           || !Neighbour(sprite, sx, sy, 0, 2);
                var colour = sprite.Solid(sx, sy) ? sprite.At(sx, sy) : sprite.Solid(sx + 1, sy + 1) ? sprite.At(sx + 1, sy + 1) : Outline;

                Put(_frame, left + x, top + y, crystal.Won ? colour : edge ? shapeEdge : shape);
            }
        }

        // Un destello que va pasando por los cristales ganados, de uno en uno.
        if (crystal.Won && step % 12 == index % 12)
        {
            Put(_frame, left + w - 2, top, GoldLight);
            Put(_frame, left + w - 1, top + 1, GoldLight);
            Put(_frame, left + w - 3, top + 1, GoldLight);
            Put(_frame, left + w - 2, top + 2, GoldLight);
        }
    }

    private void DrawTelevision(RoomSprite? frame, bool champion, int step)
    {
        const int left = 186, top = 19, width = 58, height = 27;
        var ox = _ox + left;

        // Mueble bajo con dos puertas.
        Rect(ox - 4, top + height, width + 8, 6, WoodMid);
        Rect(ox - 4, top + height, width + 8, 1, WoodTrim);
        Border(ox - 4, top + height, width + 8, 6, Outline);
        Rect(ox + (width / 2), top + height + 1, 1, 4, Outline);
        Put(_frame, ox + (width / 2) - 3, top + height + 3, Gold);
        Put(_frame, ox + (width / 2) + 3, top + height + 3, Gold);

        // Antena, cuerpo con las esquinas recortadas y el panel de mandos.
        for (var i = 0; i < 7; i++)
        {
            Put(_frame, ox + 22 - i, top - 1 - i, TvLight);
            Put(_frame, ox + 30 + i, top - 1 - i, TvLight);
        }

        Rect(ox + 1, top, width - 2, height, TvBody);
        Rect(ox, top + 1, width, height - 2, TvBody);
        Rect(ox + 1, top, width - 2, 1, TvLight);
        Rect(ox + 1, top + height - 1, width - 2, 1, TvDark);
        Rect(ox + 1, top - 1, width - 2, 1, Outline);
        Rect(ox + 1, top + height, width - 2, 1, Outline);
        Rect(ox - 1, top + 1, 1, height - 2, Outline);
        Rect(ox + width, top + 1, 1, height - 2, Outline);
        Put(_frame, ox, top, Outline);
        Put(_frame, ox + width - 1, top, Outline);
        Put(_frame, ox, top + height - 1, Outline);
        Put(_frame, ox + width - 1, top + height - 1, Outline);

        const int screenWidth = 44, screenHeight = 21;
        var sx0 = ox + 3;
        var sy0 = top + 3;
        Rect(sx0 - 1, sy0 - 1, screenWidth + 2, screenHeight + 2, TvDark);

        for (var y = 0; y < screenHeight; y++)
        {
            for (var x = 0; x < screenWidth; x++)
            {
                Color colour;

                if (frame is { } picture)
                {
                    colour = Posterize(Sample(picture, x, y, screenWidth, screenHeight));
                }
                else
                {
                    var noise = Hash(x, y, step) % 5;
                    colour = noise switch { 0 => Rgb(0xC8, 0xC4, 0xD4), 1 => Rgb(0x70, 0x6C, 0x7C), _ => Rgb(0x24, 0x22, 0x2C) };
                }

                // Líneas de tubo: una de cada dos, un poco más oscura.
                if (y % 2 == 1) colour = Lerp(colour, Screen, 0.22);
                Put(_frame, sx0 + x, sy0 + y, colour);
            }
        }

        Put(_frame, sx0 + 1, sy0 + 1, Rgb(0xFF, 0xFF, 0xFF));
        Put(_frame, sx0 + 2, sy0 + 1, Rgb(0xC8, 0xC8, 0xD8));

        var panel = ox + 49;
        foreach (var knobY in new[] { top + 5, top + 11 })
        {
            Rect(panel + 1, knobY, 3, 3, TvDark);
            Put(_frame, panel + 2, knobY + 1, TvLight);
        }

        for (var y = top + 17; y < top + height - 3; y += 2) Rect(panel, y, 5, 1, TvDark);

        // El trofeo de campeón encima de la tele, solo cuando se ha ganado la liga.
        if (champion)
        {
            Shape(Trophy, ox + 24, top - Trophy.Length - 1, c => c switch
            {
                'g' => GoldDark,
                'G' => Gold,
                'Y' => GoldLight,
                'd' => WoodDark,
                _ => (Color?)null
            });
        }
    }

    private void DrawMemorial(IReadOnlyList<RoomSprite> fallen, int count, int step)
    {
        const int left = 258, width = 100;
        var ox = _ox + left;
        int[] planks = [24, 40];

        foreach (var plankY in planks)
        {
            Rect(ox, plankY, width, 2, WoodLight);
            Rect(ox, plankY + 2, width, 1, WoodDark);
            Border(ox - 1, plankY - 1, width + 2, 4, Outline);

            foreach (var bracket in new[] { ox + 4, ox + width - 6 })
            {
                for (var i = 0; i < 3; i++) Rect(bracket + i, plankY + 3, 3 - i, 1, WoodDark);
            }
        }

        // Una figurita de piedra por caído, hecha con su propio sprite a media escala, sobre su peana.
        for (var i = 0; i < fallen.Count && i < 8; i++)
        {
            var plankY = planks[i / 4];
            var centre = ox + 12 + ((i % 4) * 24);
            DrawFigurine(fallen[i], centre, plankY - 1);
        }

        // La vela del final, con la llama moviéndose a saltos.
        var candleX = ox + width - 5;
        Rect(candleX, planks[0] - 5, 2, 5, Wax);
        Put(_frame, candleX, planks[0] - 6, step % 2 == 0 ? Flame : FlameCore);
        Put(_frame, candleX + 1, planks[0] - 7, step % 3 == 0 ? FlameCore : Flame);

        // Placa de latón con la cuenta de caídos.
        var text = Math.Min(count, 999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var plateWidth = (text.Length * 4) + 5;
        var plateX = ox + ((width - plateWidth) / 2);
        Rect(plateX, planks[1] + 4, plateWidth, 9, GoldDark);
        Rect(plateX + 1, planks[1] + 5, plateWidth - 2, 7, Gold);
        Border(plateX, planks[1] + 4, plateWidth, 9, Outline);
        Text(text, plateX + 3, planks[1] + 6, Outline, 1);
    }

    private void DrawFigurine(RoomSprite sprite, int centre, int standOn)
    {
        var (minX, minY, maxX, maxY) = Bounds(sprite);
        var w = (maxX - minX + 2) / 2;
        var h = (maxY - minY + 2) / 2;
        var left = centre - (w / 2);
        var top = standOn - 2 - h;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var sx = minX + (x * 2);
                var sy = minY + (y * 2);
                var solid = sprite.Solid(sx, sy) || sprite.Solid(sx + 1, sy) || sprite.Solid(sx, sy + 1) || sprite.Solid(sx + 1, sy + 1);

                if (!solid) continue;

                var c = sprite.Solid(sx, sy) ? sprite.At(sx, sy) : sprite.Solid(sx + 1, sy) ? sprite.At(sx + 1, sy) : Outline;
                var light = ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) / 255.0;
                var edge = !Neighbour(sprite, sx, sy, -2, 0) || !Neighbour(sprite, sx, sy, 2, 0) || !Neighbour(sprite, sx, sy, 0, -2);

                Put(_frame, left + x, top + y, edge ? Outline : light > 0.62 ? StoneLight : light > 0.32 ? StoneMid : StoneDark);
            }
        }

        Rect(centre - 5, standOn - 2, 11, 2, StoneDark);
        Rect(centre - 5, standOn - 2, 11, 1, StoneMid);
    }

    private static bool Neighbour(RoomSprite sprite, int sx, int sy, int dx, int dy) =>
        sprite.Solid(sx + dx, sy + dy) || sprite.Solid(sx + dx + 1, sy + dy) || sprite.Solid(sx + dx, sy + dy + 1) ||
        sprite.Solid(sx + dx + 1, sy + dy + 1);

    private void DrawDesk(int inPc)
    {
        const int left = 364, top = 41, width = 60;
        var ox = _ox + left;

        Rect(ox, top, width, 2, WoodTrim);
        Rect(ox, top + 2, width, 1, WoodDark);
        Border(ox - 1, top - 1, width + 2, 4, Outline);
        Rect(ox + 2, top + 3, 2, 9, WoodDark);
        Rect(ox + width - 4, top + 3, 2, 9, WoodDark);
        Rect(ox + 36, top + 3, 18, 8, WoodMid);
        Border(ox + 36, top + 3, 18, 8, Outline);
        Rect(ox + 43, top + 6, 4, 1, Gold);

        // Monitor con la caja del PC: rejilla verde y un punto por cada Pokémon guardado.
        var mx = ox + 8;
        var my = top - 17;
        Rect(mx, my, 32, 16, TvBody);
        Border(mx - 1, my - 1, 34, 18, Outline);
        Rect(mx + 2, my + 2, 28, 12, BoxGreen);

        for (var slot = 0; slot < 24; slot++)
        {
            var cx = mx + 3 + ((slot % 6) * 4);
            var cy = my + 3 + ((slot / 6) * 3);
            Rect(cx, cy, 3, 2, slot < inPc ? StickerColours[slot % StickerColours.Length] : BoxGreenLight);
        }

        Rect(mx + 14, my + 16, 4, 1, TvDark);
        Rect(mx + 2, top - 1, 28, 1, TvLight);
        for (var k = 0; k < 7; k++) Put(_frame, mx + 4 + (k * 4), top - 1, TvDark);
    }

    private void DrawCorkboard(int stickers, int target)
    {
        const int left = 366, top = 4, width = 54, height = 18;
        var ox = _ox + left;

        for (var y = top; y < top + height; y++)
        {
            for (var x = ox; x < ox + width; x++)
            {
                Put(_frame, x, y, Bayer[y % 4, x % 4] < 7 ? CorkDark : Cork);
            }
        }

        Border(ox, top, width, height, WoodLight);
        Border(ox - 1, top - 1, width + 2, height + 2, Outline);

        // Todos los huecos de la colección, y una pegatina en cada Dominsignia conseguida: la cuenta se ve sin leerla.
        var slots = Math.Clamp(target, 1, 39);
        var perRow = (slots + 2) / 3;
        var spacing = Math.Max(4, (width - 6) / perRow);

        for (var i = 0; i < slots; i++)
        {
            var sx = ox + 3 + ((i % perRow) * spacing);
            var sy = top + 3 + ((i / perRow) * 5);

            if (i < stickers)
            {
                Rect(sx, sy, 3, 3, StickerColours[i % StickerColours.Length]);
                Put(_frame, sx + 1, sy, Rgb(0xF4, 0xF0, 0xF8));
            }
            else
            {
                Put(_frame, sx + 1, sy + 1, Lerp(CorkDark, Outline, 0.5));
            }
        }
    }

    private void DrawTeam(IReadOnlyList<RoomSprite> team, int step, bool still)
    {
        var count = Math.Min(team.Count, 6);
        if (count == 0) return;

        const int feet = DesignRows - 5;
        var spacing = 30;
        var first = _ox + 245 - (((count - 1) * spacing) / 2);

        for (var i = 0; i < count; i++)
        {
            var sprite = team[i];
            var (minX, minY, maxX, maxY) = Bounds(sprite);
            var hop = !still && ((step + (i * 3)) % 6) < 1 ? 2 : 0;
            var left = first + (i * spacing) - ((maxX - minX + 1) / 2);
            var top = feet - (maxY - minY + 1) - hop;

            // Sombra en el suelo, que no salta.
            Rect(first + (i * spacing) - 7, feet, 15, 1, Lerp(RugDark, Outline, 0.5));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (sprite.Solid(x, y)) Put(_frame, left + (x - minX), top + (y - minY), sprite.At(x, y));
                }
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    private static (int MinX, int MinY, int MaxX, int MaxY) Bounds(RoomSprite sprite)
    {
        int minX = sprite.Width, minY = sprite.Height, maxX = -1, maxY = -1;

        for (var y = 0; y < sprite.Height; y++)
        {
            for (var x = 0; x < sprite.Width; x++)
            {
                if (!sprite.Solid(x, y)) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < 0 ? (0, 0, sprite.Width - 1, sprite.Height - 1) : (minX, minY, maxX, maxY);
    }

    private static Color Sample(RoomSprite picture, int x, int y, int width, int height)
    {
        // Media de la caja de origen que le toca a cada celda, para que la foto no salga con dientes.
        var x0 = x * picture.Width / width;
        var x1 = Math.Max(x0 + 1, (x + 1) * picture.Width / width);
        var y0 = y * picture.Height / height;
        var y1 = Math.Max(y0 + 1, (y + 1) * picture.Height / height);
        long r = 0, g = 0, b = 0, n = 0;

        for (var sy = y0; sy < y1; sy += 2)
        {
            for (var sx = x0; sx < x1; sx += 2)
            {
                var c = picture.At(sx, sy);
                r += c.R;
                g += c.G;
                b += c.B;
                n++;
            }
        }

        return n == 0 ? Screen : Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    /// <summary>
    /// Six levels per channel and no dithering: at forty-four cells wide a dithered photograph was a smear, and flat
    /// steps are what makes a picture read as pixels.
    /// </summary>
    private static Color Posterize(Color c)
    {
        static byte Level(byte v) => (byte)(Math.Round(v / 255.0 * 5) * 51);

        return Color.FromRgb(Level(c.R), Level(c.G), Level(c.B));
    }

    private static int Hash(int x, int y, int step) => Math.Abs(((x * 73856093) ^ (y * 19349663) ^ (step * 83492791)) % 9973);

    private void Text(string text, int left, int top, Color colour, int scale)
    {
        var x = left;

        foreach (var ch in text)
        {
            if (Font.TryGetValue(ch, out var glyph))
            {
                for (var gy = 0; gy < 5; gy++)
                {
                    for (var gx = 0; gx < 3; gx++)
                    {
                        if (glyph[gy][gx] == '#') Rect(x + (gx * scale), top + (gy * scale), scale, scale, colour);
                    }
                }
            }

            x += 4 * scale;
        }
    }

    private void Shape(string[] rows, int left, int top, Func<char, Color?> paint)
    {
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                if (paint(rows[y][x]) is { } colour) Put(_frame, left + x, top + y, colour);
            }
        }
    }

    private void Frame(int x, int y, int width, int height, Color light, Color dark)
    {
        Rect(x, y, width, 2, light);
        Rect(x, y + height - 2, width, 2, dark);
        Rect(x, y, 2, height, light);
        Rect(x + width - 2, y, 2, height, dark);
        Border(x - 1, y - 1, width + 2, height + 2, Outline);
    }

    private void Border(int x, int y, int width, int height, Color colour)
    {
        Rect(x, y, width, 1, colour);
        Rect(x, y + height - 1, width, 1, colour);
        Rect(x, y, 1, height, colour);
        Rect(x + width - 1, y, 1, height, colour);
    }

    private void Rect(int x, int y, int width, int height, Color colour)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            for (var xx = x; xx < x + width; xx++) Put(_frame, xx, yy, colour);
        }
    }

    /// <summary>Paints one cell, in design coordinates: the extra wall of a taller panel is above, so everything moves down.</summary>
    private void Put(byte[] target, int x, int y, Color colour)
    {
        y += _oy;
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        var at = ((y * Width) + x) * 4;
        target[at] = colour.B;
        target[at + 1] = colour.G;
        target[at + 2] = colour.R;
        target[at + 3] = 255;
    }
}
