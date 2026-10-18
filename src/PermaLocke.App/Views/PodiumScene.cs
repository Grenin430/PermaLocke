using System.Windows.Media;
using PermaLocke.Core.Domain;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>
/// COMPETICIÓN's podium, painted cell by cell: a night stadium with a crowd in the stands, two searchlights sweeping,
/// a scoreboard of bulbs, and the top three on gold, silver and bronze blocks with their photo in a frame.
/// </summary>
/// <remarks>
/// <para>
/// Built like the other scenes (§171, §174, §175): whole cells, flat colours, ordered dithering, the stands painted once
/// and the rest drawn every frame on a copy. The real things in it are each player's photo (pixelated to 24 cells),
/// name, place, points, stage and team, and the colour of the frame, which is their presence as in JUGAR. The crowd,
/// the lights and the confetti are decoration and mean nothing.
/// </para>
/// </remarks>
public sealed class PodiumScene : PixelScene
{
    public const int DesignWidth = 320;
    public const int DesignRows = 164;

    /// <summary>Cells a photo takes, square.</summary>
    public const int PhotoSize = 24;

    private const int FloorTop = 140;
    private const int BlockWidth = 54;

    private static readonly Color SkyTop = Rgb(0x07, 0x06, 0x10);
    private static readonly Color SkyLow = Rgb(0x16, 0x10, 0x2A);
    private static readonly Color Stand = Rgb(0x1A, 0x14, 0x2A);
    private static readonly Color StandStep = Rgb(0x26, 0x1E, 0x3C);
    private static readonly Color StandEdge = Rgb(0x0F, 0x0B, 0x19);
    private static readonly Color Plank = Rgb(0x3A, 0x2A, 0x3C);
    private static readonly Color PlankLight = Rgb(0x4C, 0x38, 0x4C);
    private static readonly Color PlankSeam = Rgb(0x24, 0x1A, 0x28);
    private static readonly Color StageEdge = Rgb(0x5E, 0x46, 0x5A);
    private static readonly Color Beam = Rgb(0xE8, 0xE2, 0xFF);

    private static readonly Color Playing = Rgb(0x90, 0xBA, 0x3C);
    private static readonly Color InApp = Rgb(0x57, 0xCB, 0xDE);
    private static readonly Color Offline = Rgb(0x5C, 0x5C, 0x5C);

    // Oro, plata y bronce: brillo, claro, base, sombra.
    private static readonly Color[] GoldTones = [GoldHi, GoldLight, Gold, GoldDark];
    private static readonly Color[] SilverTones = [ChromeHi, ChromeLight, ChromeMid, ChromeDark];
    private static readonly Color[] BronzeTones = [Rgb(0xF6, 0xC8, 0x9C), Rgb(0xD8, 0x92, 0x58), Rgb(0xA8, 0x64, 0x32), Rgb(0x64, 0x38, 0x1A)];
    private static readonly Color[] EmptyTones = [Rgb(0x4A, 0x44, 0x5C), Rgb(0x3A, 0x35, 0x4A), Rgb(0x2C, 0x28, 0x3A), Rgb(0x1C, 0x19, 0x26)];

    private static readonly Color[] Shirts =
    [
        Rgb(0x8C, 0x3A, 0x4A), Rgb(0x3A, 0x5A, 0x8C), Rgb(0x4A, 0x7A, 0x42), Rgb(0x8C, 0x6A, 0x2E),
        Rgb(0x6A, 0x42, 0x8C), Rgb(0x2E, 0x6E, 0x74), Rgb(0x7A, 0x7A, 0x88), Rgb(0x9A, 0x4A, 0x2A)
    ];

    private static readonly Color[] Skins = [Rgb(0xE8, 0xB8, 0x96), Rgb(0xC0, 0x8A, 0x62), Rgb(0x8A, 0x5A, 0x3C), Rgb(0xF2, 0xCC, 0xAE)];

    private static readonly Color[] Confetti =
    [
        Rgb(0xFF, 0xDC, 0x7A), Rgb(0xB0, 0x7B, 0xF0), Rgb(0x57, 0xCB, 0xDE), Rgb(0xF0, 0x6A, 0x7A), Rgb(0x90, 0xBA, 0x3C), White
    ];

    private static readonly string[] CrownArt =
    [
        "#...#...#",
        "##.###.##",
        "#########",
        "#.#.#.#.#",
        "#########",
    ];

    public PodiumScene(int width, int height) : base(width, height, DesignWidth, DesignRows)
    {
        PaintBack();
    }

    /// <summary>One step of the podium as the scene draws it; null draws an empty step.</summary>
    /// <param name="Photo">BGRA, <see cref="PhotoSize"/> square, or null while it loads.</param>
    public sealed record Step(int Position, string Name, int Points, bool IsMine, PresenceState State,
        string Stage, string Team, byte[]? Photo);

    /// <summary>The three places left to right: second, first, third.</summary>
    private static readonly (int Centre, int Height, int Tones)[] Places = [(96, 38, 1), (160, 50, 0), (224, 30, 2)];

    /// <summary>Where a step's block and photo are, for the tooltip: left, top, width, height in design cells.</summary>
    public (int X, int Y, int Width, int Height) SlotRect(int slot)
    {
        var (centre, height, _) = Places[slot];
        var top = FloorTop - height - 40;
        return (centre - (BlockWidth / 2) + Ox, top + Oy, BlockWidth, FloorTop - top);
    }

    /// <param name="steps">Left, centre and right; a missing or null one is an empty step.</param>
    public void Render(IReadOnlyList<Step?> steps, double seconds)
    {
        BeginFrame();

        Crowd(seconds);
        Searchlight(22, seconds * 0.55, 0);
        Searchlight(DesignWidth - 22, (seconds * 0.55) + 2.1, 1);
        Scoreboard(seconds);

        for (var slot = 0; slot < Places.Length; slot++)
        {
            DrawStep(slot, slot < steps.Count ? steps[slot] : null, seconds);
        }

        DrawConfetti(seconds);
        Present();
    }

    // ============================================================================================= fondo

    private void PaintBack()
    {
        for (var y = -Oy; y < DesignRows; y++)
        {
            for (var x = -Ox; x < Width - Ox; x++)
            {
                Put(Back, x, y, BackAt(x, y));
            }
        }

        // Estrellas sobre las gradas.
        var random = new Random(20260928);
        for (var i = 0; i < Width / 5; i++)
        {
            var x = random.Next(Width) - Ox;
            var y = random.Next(Math.Max(1, Oy + 30)) - Oy;
            Put(Back, x, y, random.NextDouble() < 0.3 ? Rgb(0xC8, 0xC2, 0xDA) : Rgb(0x4A, 0x44, 0x5E));
        }

        // Focos en torres a los lados, encendidos.
        foreach (var towerX in new[] { 8, DesignWidth - 14 })
        {
            for (var y = 20; y < FloorTop; y++) Put(Back, towerX + 2, y, StandEdge);
            for (var y = 20; y < FloorTop; y += 6) Put(Back, towerX + 1, y, StandEdge);
            for (var dx = 0; dx < 6; dx++)
            {
                for (var dy = 0; dy < 4; dy++)
                {
                    Put(Back, towerX + dx, 14 + dy, dy == 0 || dx == 0 || dx == 5 ? ChromeDark : BulbOn);
                }
            }
        }
    }

    private Color BackAt(int x, int y)
    {
        if (y >= FloorTop) return FloorAt(x, y);

        // Gradas: escalones de 6 filas desde la fila 34 hasta el escenario.
        if (y >= 34)
        {
            var row = (y - 34) % 6;
            return row == 0 ? StandEdge : row == 5 ? StandStep : Stand;
        }

        var t = Math.Clamp((y + Oy) / (double)(Oy + 34), 0, 0.999);
        return Bayer[y & 3, x & 3] < t * 16 ? SkyLow : SkyTop;
    }

    private static Color FloorAt(int x, int y)
    {
        if (y == FloorTop) return StageEdge;
        if (y == FloorTop + 1) return PlankLight;

        // Tablas del escenario, con la luz de los focos en el centro.
        var seam = (y - FloorTop) % 5 == 0 || ((x + ((y - FloorTop) / 5 * 17)) % 29 + 29) % 29 == 0;
        if (seam) return PlankSeam;

        var pool = Math.Pow((x - 160) / 130.0, 2) + Math.Pow((y - 150) / 14.0, 2);
        var lit = pool < 0.7 || (pool < 1 && Bayer[y & 3, x & 3] < (1 - pool) * 50);
        return lit ? PlankLight : Plank;
    }

    // ============================================================================================= público

    /// <summary>A crowd on the stands; some jump now and then.</summary>
    private void Crowd(double seconds)
    {
        for (var row = 0; row < 17; row++)
        {
            var baseY = 34 + (row * 6) + 5;
            if (baseY >= FloorTop - 2) break;

            for (var x = -Ox + ((row * 3) % 5); x < Width - Ox; x += 5)
            {
                var seed = (x * 73856093) ^ (row * 19349663);
                if (((seed >> 3) & 7) == 0) continue;

                var shirt = Shirts[(seed >> 5 & 0x7FFFFFFF) % Shirts.Length];
                var skin = Skins[(seed >> 9 & 0x7FFFFFFF) % Skins.Length];
                var phase = ((seed >> 12) & 0xFF) / 255.0 * Math.PI * 2;
                var jump = Math.Sin((seconds * 5) + phase) > 0.85 ? 1 : 0;

                // Los de atrás, en penumbra.
                var dim = 0.72 - (row * 0.02);
                shirt = Lerp(shirt, SkyTop, Math.Max(0, dim));
                skin = Lerp(skin, SkyTop, Math.Max(0, dim));

                var y = baseY - jump;
                Rect(x, y - 2, 3, 2, shirt);
                Rect(x + 1, y - 4, 2, 2, skin);

                // Algunos levantan los brazos al saltar.
                if (jump == 1 && ((seed >> 20) & 1) == 0)
                {
                    Put(Canvas, x - 1, y - 4, skin);
                    Put(Canvas, x + 3, y - 4, skin);
                }
            }
        }
    }

    /// <summary>A searchlight beam from a tower, sweeping the sky.</summary>
    private void Searchlight(int footX, double beat, int side)
    {
        const int FootY = 16;
        var angle = (side == 0 ? -0.35 : Math.PI + 0.35) + (Math.Sin(beat) * 0.45 * (side == 0 ? -1 : 1));
        var dirX = Math.Cos(angle);
        var dirY = -Math.Abs(Math.Sin(angle)) - 0.35;
        var length = Math.Sqrt((dirX * dirX) + (dirY * dirY));
        dirX /= length;
        dirY /= length;

        for (var y = -Oy; y < FootY; y++)
        {
            for (var x = -Ox; x < Width - Ox; x++)
            {
                var px = x + 0.5 - (footX + 3);
                var py = y + 0.5 - FootY;
                var along = (px * dirX) + (py * dirY);
                if (along <= 0) continue;

                var across = Math.Abs((px * -dirY) + (py * dirX));
                var half = 2 + (along * 0.16);
                if (across > half) continue;

                var strength = (1 - (across / half)) * Math.Max(0, 1 - (along / 240));
                if (Bayer[(y + Oy) & 3, (x + Ox) & 3] < strength * 11)
                {
                    Put(Canvas, x, y, Lerp(At(Canvas, x, y), Beam, 0.3 + (strength * 0.25)));
                }
            }
        }
    }

    /// <summary>The scoreboard over the podium, its bulbs chasing round the edge.</summary>
    private void Scoreboard(double seconds)
    {
        const int Left = 96, Top = 4, W = 128, H = 26;
        Rect(Left, Top, W, H, Plate);
        Frame(Left, Top, W, H, ChromeLight, ChromeDeep);
        Rect(Left + 3, Top + 3, W - 6, H - 6, PlateLight);

        var chase = (int)(seconds * 12);
        var i = 0;
        for (var x = Left + 2; x < Left + W - 2; x += 3, i++) Bulb(x, Top + 1, i, chase);
        for (var x = Left + W - 3; x > Left + 1; x -= 3, i++) Bulb(x, Top + H - 2, i, chase);

        ShoutText("TOP DEL TORNEO", 160, Top + 9, GoldLight);
    }

    private void Bulb(int x, int y, int index, int chase) =>
        Put(Canvas, x, y, (index + chase) % 4 == 0 ? BulbOn : (index + chase) % 4 == 1 ? BulbWarm : BulbOff);

    // ============================================================================================= podio

    private void DrawStep(int slot, Step? step, double seconds)
    {
        var (centre, height, toneIndex) = Places[slot];
        var tones = step is null ? EmptyTones : toneIndex switch { 0 => GoldTones, 1 => SilverTones, _ => BronzeTones };
        var left = centre - (BlockWidth / 2);
        var top = FloorTop - height;

        // Sombra en el suelo y el bloque, con la cara de arriba en perspectiva.
        for (var x = left - 2; x < left + BlockWidth + 4; x++)
        {
            if (Bayer[FloorTop & 3, x & 3] < 10) Put(Canvas, x, FloorTop + 1, Shadow);
            Put(Canvas, x, FloorTop + 2, Shadow);
        }

        Rect(left, top + 4, BlockWidth, height - 4, tones[2]);
        Rect(left, top, BlockWidth, 4, tones[1]);
        Rect(left, top, BlockWidth, 1, tones[0]);
        Rect(left, top + 4, 2, height - 4, tones[1]);
        Rect(left + BlockWidth - 3, top + 4, 3, height - 4, tones[3]);
        Rect(left, top + 4, BlockWidth, 1, tones[3]);
        Border(left - 1, top - 1, BlockWidth + 2, height + 2, Outline);

        // El puesto, grande, con su sombra; y la placa con los puntos.
        var place = step?.Position.ToString() ?? (slot == 1 ? "1" : slot == 0 ? "2" : "3");
        // Con contorno oscuro: sobre el bloque de oro, de plata o de bronce el número se pierde si solo es más claro que él
        // (y en el Game Boy, donde los tres bloques son casi del mismo verde, se perdía entero).
        var numberLeft = centre - (BigWidth(place, 2) / 2);
        foreach (var (ox, oy) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1), (2, 2) })
        {
            BigText(place, numberLeft + ox, top + 6 + oy, Outline, 2);
        }

        BigText(place, numberLeft, top + 6, step is null ? tones[1] : tones[0], 2);

        if (step is not null)
        {
            var points = $"{step.Points} PTS";
            var plateWidth = SmallWidth(points) + 6;
            var plateTop = top + height - 10;
            if (plateTop > top + 21)
            {
                Rect(centre - (plateWidth / 2), plateTop, plateWidth, 8, Plate);
                Border(centre - (plateWidth / 2), plateTop, plateWidth, 8, tones[3]);
                SmallText(points, centre - (SmallWidth(points) / 2), plateTop + 2, GoldHi);
            }
            else
            {
                SmallText(points, centre - (SmallWidth(points) / 2), top + 22, Outline);
            }

            // Bajo el escenario: su etapa y sus vivos.
            SmallText(step.Stage, centre - (SmallWidth(step.Stage) / 2), FloorTop + 6, Rgb(0xC4, 0xBC, 0xE0));
            SmallText(step.Team, centre - (SmallWidth(step.Team) / 2), FloorTop + 13, Rgb(0x8C, 0x84, 0xA3));
        }

        // El marco de la foto, del color de su presencia, sobre el bloque.
        var frameColour = step?.State switch
        {
            PresenceState.Playing => Playing,
            PresenceState.InApp => InApp,
            _ => Offline
        };

        var bob = step is not null && slot == 1 ? (int)Math.Round(Math.Sin(seconds * 2.2)) : 0;
        var photoLeft = centre - (PhotoSize / 2);
        var photoTop = top - PhotoSize - 5 + bob;

        Rect(photoLeft - 2, photoTop - 2, PhotoSize + 4, PhotoSize + 4, step is null ? EmptyTones[2] : frameColour);
        Border(photoLeft - 3, photoTop - 3, PhotoSize + 6, PhotoSize + 6, Outline);
        Rect(photoLeft, photoTop, PhotoSize, PhotoSize, GlassDeep);

        if (step?.Photo is { } photo)
        {
            for (var y = 0; y < PhotoSize; y++)
            {
                for (var x = 0; x < PhotoSize; x++)
                {
                    var i = ((y * PhotoSize) + x) * 4;
                    if (photo[i + 3] < 128) continue;
                    Put(Canvas, photoLeft + x, photoTop + y, Color.FromRgb(photo[i + 2], photo[i + 1], photo[i]));
                }
            }
        }
        else if (step is null)
        {
            ShoutText("?", centre, photoTop + 8, EmptyTones[0]);
        }

        // Brillo del cristal, arriba a la izquierda.
        Put(Canvas, photoLeft, photoTop, White);
        Put(Canvas, photoLeft + 1, photoTop, GlassShine);
        Put(Canvas, photoLeft, photoTop + 1, GlassShine);

        if (step is null) return;

        // El nombre encima; el tuyo en dorado.
        var name = Fit(step.Name.ToUpperInvariant(), BlockWidth + 4);
        var nameTop = photoTop - 9;
        var nameWidth = SmallWidth(name);
        Rect(centre - (nameWidth / 2) - 2, nameTop - 2, nameWidth + 4, 9, Outline);
        SmallText(name, centre - (nameWidth / 2), nameTop, step.IsMine ? GoldLight : White);

        if (slot == 1)
        {
            Crown(centre, nameTop - 9 + bob, seconds);
        }

        if (step.IsMine && (int)(seconds * 2) % 2 == 0)
        {
            // Una flecha que señala tu puesto.
            var ax = left + BlockWidth + 4;
            var ay = top + 8;
            for (var i = 0; i < 4; i++) Rect(ax + i, ay - 3 + i, 1, 7 - (i * 2), GoldLight);
        }
    }

    private void Crown(int centre, int top, double seconds)
    {
        var left = centre - 4;
        for (var y = 0; y < CrownArt.Length; y++)
        {
            for (var x = 0; x < CrownArt[y].Length; x++)
            {
                if (CrownArt[y][x] != '#') continue;
                Put(Canvas, left + x, top + y, y == 0 ? GoldHi : y == 3 ? GoldDark : Gold);
            }
        }

        // Una joya roja en el centro y un destello que va y viene.
        Put(Canvas, centre, top + 3, Rgb(0xF0, 0x4A, 0x3C));
        if (Math.Sin(seconds * 3) > 0.6) Star(left + 9, top - 1, 1, White, GoldLight);
    }

    /// <summary>Confetti falling over everything, on a loop.</summary>
    private void DrawConfetti(double seconds)
    {
        var count = Width / 5;
        var span = Height + 20;
        for (var i = 0; i < count; i++)
        {
            var seed = new Random(i * 7919);
            var x0 = seed.Next(Width) - Ox;
            var speed = 9 + (seed.NextDouble() * 14);
            var offset = seed.NextDouble() * span;
            var sway = seed.NextDouble() * Math.PI * 2;
            var colour = Confetti[seed.Next(Confetti.Length)];

            var y = (int)(((seconds * speed) + offset) % span) - 20 - Oy;
            var x = x0 + (int)Math.Round(Math.Sin((seconds * 1.8) + sway) * 3);
            var flip = (int)((seconds * 6) + i) % 3;

            if (flip == 0) Rect(x, y, 2, 1, colour);
            else if (flip == 1) Rect(x, y, 1, 2, colour);
            else Put(Canvas, x, y, Lerp(colour, Outline, 0.3));
        }
    }

    /// <summary>A name cut to the cells there are.</summary>
    private static string Fit(string text, int cells)
    {
        var max = (cells + 1) / 4;
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + ".";
    }
}
