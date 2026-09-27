using Color = PermaLocke.App.Views.PixelColour;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>The moments of the card trade, in seconds from the button (1.0.4.7).</summary>
public static class CardTradeTimeline
{
    /// <summary>The two cards fly in from below and settle either side of the circle.</summary>
    public const double Arrive = 0.9;

    /// <summary>The trade circle draws itself under them.</summary>
    public const double Circle = 1.9;

    /// <summary>Turned over, they orbit the centre, faster and closer.</summary>
    public const double Orbit = 3.3;

    /// <summary>They meet: flash, shockwave, shards.</summary>
    public const double Clash = 3.5;

    /// <summary>The shards are drawn back into an orb that pulses in the colour of what is coming.</summary>
    public const double Orb = 5.0;

    /// <summary>The orb folds into a card, face down.</summary>
    public const double Card = 5.8;

    /// <summary>The card turns over.</summary>
    public const double Reveal = 6.5;

    /// <summary>From here it rests and the name is written under it.</summary>
    public const double Rest = Reveal;

    /// <summary>What <see cref="CardTradeScene.Render"/> still animates after <see cref="Rest"/>, forever.</summary>
    public const double Length = Reveal + 1.2;
}

/// <summary>
/// The card trade of the ALBUM (1.0.4.7): two cards go into the circle and one comes out. Pixel art at the screen's own
/// resolution, drawn a frame at a time from a moment, so it is tested without a window.
/// </summary>
/// <remarks>
/// The cards are the album's own (<see cref="HandScene"/>, in perspective, with their finish and their rays); around them
/// everything is blocks of one cell: the dark of the room, the circle with its runes, the trails, the flash, the shards,
/// the orb and its beams, the stars. The colour of the orb and its beams is the rarity of the card that comes out, so
/// the player knows how good it is a moment before seeing it.
/// </remarks>
public sealed class CardTradeScene
{
    private static readonly Color Ink = Rgb(0x09, 0x07, 0x0E);
    private static readonly Color RuneDim = Rgb(0x3A, 0x27, 0x66);
    private static readonly Color Rune = Rgb(0x8E, 0x6A, 0xE0);
    private static readonly Color RuneHot = Rgb(0xD2, 0xAD, 0xFF);
    private static readonly Color White = Rgb(0xFA, 0xF8, 0xFF);
    private static readonly Color Gold = Rgb(0xFF, 0xD2, 0x4A);

    /// <summary>The gacha's tier colours: common to gold, the colour of the orb for each rarity.</summary>
    private static readonly Color[] Tiers =
    [
        Rgb(0x9C, 0xA5, 0xB3), Rgb(0x6F, 0xD8, 0x7F), Rgb(0x5E, 0xAB, 0xF9), Rgb(0xF7, 0x6F, 0xC8), Rgb(0xFF, 0xC4, 0x41)
    ];

    private readonly HandScene _left;
    private readonly HandScene _right;
    private readonly HandScene _result;
    private readonly (double X, double Y, double Vx, double Vy, int Colour)[] _shards;

    public CardTradeScene(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new byte[Width * Height * 4];
        Cell = Math.Max(2, (int)Math.Round(Height / 200.0));
        _left = new HandScene(Width, Height);
        _right = new HandScene(Width, Height);
        _result = new HandScene(Width, Height);
        Peak = Math.Max(2, (int)Math.Floor(Height * 0.46 / TcgCardArt.FullHeight));

        // Las esquirlas del choque, fijas: salen del centro en todas direcciones y vuelven al orbe.
        var random = new Random(7);
        _shards = new (double, double, double, double, int)[90];
        for (var i = 0; i < _shards.Length; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2;
            var speed = 0.25 + (random.NextDouble() * 0.75);
            _shards[i] = (0, 0, Math.Cos(angle) * speed, Math.Sin(angle) * speed, random.Next(3));
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Screen pixels per block of the effects.</summary>
    public int Cell { get; }

    /// <summary>Screen pixels per cell of a card at rest: whole, so its pixels stay square.</summary>
    public int Peak { get; }

    /// <summary>The frame, premultiplied BGRA, opaque: the room goes dark behind the trade.</summary>
    public byte[] Pixels { get; }

    private double CentreX => Width / 2.0;

    private double CentreY => Height * 0.47;

    /// <summary>Draws the moment <paramref name="t"/> seconds after INTERCAMBIAR.</summary>
    /// <param name="rarity">The rarity of the card that comes out, 0 to 4: the colour of the orb.</param>
    public void Render(TcgRender first, TcgRender second, TcgRender back, TcgRender result, int rarity, bool shiny,
        uint seed, double t)
    {
        var tier = Tiers[Math.Clamp(rarity, 0, Tiers.Length - 1)];
        Room(t, tier);
        Circle(t, tier);

        if (t < CardTradeTimeline.Clash)
        {
            Trails(t);
            _left.Render(first, back, Pose(t, -1), t, seed, surroundings: false);
            _right.Render(second, back, Pose(t, 1), t + 0.37, seed ^ 0x5A5A, surroundings: false);
            Over(_left.Pixels, _left.Dirty);
            Over(_right.Pixels, _right.Dirty);
        }

        if (t >= CardTradeTimeline.Clash - 0.05)
        {
            Clash(t, tier);
        }

        if (t >= CardTradeTimeline.Card - 0.05)
        {
            var arrival = t - CardTradeTimeline.Reveal;
            _result.Render(result, back, ResultPose(t), t, seed, arrival, surroundings: false);
            Over(_result.Pixels, _result.Dirty);
        }

        if (t >= CardTradeTimeline.Reveal)
        {
            Stars(t, tier, rarity, shiny);
        }
    }

    // ====================================================================================================== ROOM

    /// <summary>The room goes dark from the edges, and the centre glows faintly in the colour of what is coming.</summary>
    private void Room(double t, Color tier)
    {
        var dark = Ease(t / 0.6);
        var glow = t < CardTradeTimeline.Clash ? 0.15 * Ease((t - 1) / 2) : 0.35 * (1 - Ease((t - CardTradeTimeline.Reveal) / 1.5)) + 0.12;
        var maxR = Math.Sqrt((CentreX * CentreX) + (Height * Height * 0.3));

        for (var y = 0; y < Height; y += Cell)
        {
            for (var x = 0; x < Width; x += Cell)
            {
                var dx = x - CentreX;
                var dy = (y - CentreY) * 1.3;
                var d = Math.Sqrt((dx * dx) + (dy * dy)) / maxR;

                // Bandas de un bloque, como una viñeta de consola, no un degradado.
                var band = Math.Floor(Math.Min(1, d) * 6) / 6;
                var shade = (0.55 + (band * 0.45)) * dark;
                var warm = Math.Max(0, 1 - (d * 2.2)) * glow;
                var b = (byte)Math.Clamp((0x14 * (1 - shade)) + (tier.B * warm), 0, 255);
                var g = (byte)Math.Clamp((0x10 * (1 - shade)) + (tier.G * warm), 0, 255);
                var r = (byte)Math.Clamp((0x1C * (1 - shade)) + (tier.R * warm), 0, 255);
                Fill(x, y, b, g, r, Cell);
            }
        }
    }

    // ==================================================================================================== CIRCLE

    /// <summary>
    /// The trade circle on the floor: two rings of blocks drawn round as a stroke, twelve runes between them, and it turns
    /// faster as the cards orbit. Seen in perspective, so it is an ellipse.
    /// </summary>
    private void Circle(double t, Color tier)
    {
        if (t < CardTradeTimeline.Arrive - 0.2)
        {
            return;
        }

        var drawn = Ease((t - (CardTradeTimeline.Arrive - 0.2)) / (CardTradeTimeline.Circle - CardTradeTimeline.Arrive));
        var fade = t < CardTradeTimeline.Orb ? 1 : 1 - Ease((t - CardTradeTimeline.Orb) / 1.2);
        if (fade <= 0)
        {
            return;
        }

        var spin = t < CardTradeTimeline.Circle ? t * 0.3 : (CardTradeTimeline.Circle * 0.3) + (Math.Pow(t - CardTradeTimeline.Circle, 2) * 1.1);
        var cy = CentreY + (Height * 0.2);
        var outer = Width * 0.3;
        var inner = outer * 0.74;
        var hot = t is > CardTradeTimeline.Circle and < CardTradeTimeline.Clash;
        var ring = hot && ((int)(t * 12) % 2 == 0) ? RuneHot : Rune;
        var colour = t >= CardTradeTimeline.Clash ? Mix(Rune, tier, 0.6) : ring;

        Ellipse(CentreX, cy, outer, outer * 0.28, drawn, spin, fade > 0.5 ? colour : RuneDim);
        Ellipse(CentreX, cy, inner, inner * 0.28, drawn, -spin * 1.3, fade > 0.5 ? RuneDim : Ink);

        // Las runas: marcas de tres bloques entre los dos anillos, que se encienden según pasa el trazo.
        for (var i = 0; i < 12; i++)
        {
            var at = i / 12.0;
            if (at > drawn)
            {
                break;
            }

            var angle = (at * Math.PI * 2) + spin;
            var r = (outer + inner) / 2;
            var x = CentreX + (Math.Cos(angle) * r);
            var y = cy + (Math.Sin(angle) * r * 0.28);
            var lit = hot && (i + (int)(t * 8)) % 3 == 0 ? RuneHot : Rune;
            Block(x, y, fade > 0.5 ? lit : RuneDim);
            Block(x, y - Cell, fade > 0.5 ? lit : RuneDim);
            if (i % 2 == 0) Block(x + Cell, y, RuneDim);
        }

        // Los rayos que suben del círculo mientras orbitan.
        if (hot)
        {
            for (var i = 0; i < 6; i++)
            {
                var angle = (i / 6.0 * Math.PI * 2) + (spin * 1.5);
                var x = CentreX + (Math.Cos(angle) * inner * 0.9);
                var baseY = cy + (Math.Sin(angle) * inner * 0.9 * 0.28);
                var tall = Height * 0.12 * (0.5 + (0.5 * Math.Sin((t * 9) + i)));
                for (var h = 0.0; h < tall; h += Cell * 2)
                {
                    Block(x, baseY - h, h < tall * 0.5 ? Rune : RuneDim);
                }
            }
        }
    }

    private void Ellipse(double cx, double cy, double rx, double ry, double drawn, double spin, Color colour)
    {
        var steps = (int)Math.Max(60, rx * 2 * Math.PI / Cell);
        var upto = (int)(steps * drawn);
        for (var i = 0; i < upto; i++)
        {
            var angle = (i / (double)steps * Math.PI * 2) + spin;
            Block(cx + (Math.Cos(angle) * rx), cy + (Math.Sin(angle) * ry), colour);
        }
    }

    // ===================================================================================================== CARDS

    /// <summary>Where each of the two cards is: <paramref name="side"/> is -1 for the left one and 1 for the right.</summary>
    private HandPose Pose(double t, int side)
    {
        var restX = CentreX + (side * Width * 0.22);
        var restY = CentreY;

        if (t < CardTradeTimeline.Arrive)
        {
            // Suben desde abajo, girando media vuelta y creciendo, con un rebote.
            var p = Math.Clamp(t / CardTradeTimeline.Arrive, 0, 1);
            var move = 1 - Math.Pow(1 - p, 3);
            var grow = BackOut(p, 1.2);
            var fromX = CentreX + (side * Width * 0.4);
            var fromY = Height * 1.25;
            return new HandPose((1 - move) * Math.PI * side, 0, side * (1 - move) * 0.6, Math.Max(0.5, Peak * (0.3 + (0.7 * grow))),
                fromX + ((restX - fromX) * move), fromY + ((restY - fromY) * move), 0.8);
        }

        if (t < CardTradeTimeline.Circle)
        {
            // Esperan meciéndose, inclinadas la una hacia la otra.
            var s = t - CardTradeTimeline.Arrive;
            return new HandPose((-side * 0.35 * Ease(s / 0.5)) + (Math.Sin((s * 3) + side) * 0.08), Math.Cos(s * 2.4) * 0.05,
                side * 0.05, Peak, restX, restY - (Math.Sin((s * 2.6) + side) * Cell), 0.8);
        }

        // Se dan la vuelta y orbitan el centro, cada vez más deprisa y más cerca, hasta chocar.
        var q = Math.Clamp((t - CardTradeTimeline.Circle) / (CardTradeTimeline.Clash - CardTradeTimeline.Circle), 0, 1);
        var angle = (side < 0 ? Math.PI : 0) + (Math.Pow(q, 2.2) * Math.PI * 5);
        var radius = Width * 0.22 * (1 - Math.Pow(q, 1.6));
        var turn = Math.Min(1, q * 3) * Math.PI;
        return new HandPose((-side * 0.35 * (1 - Math.Min(1, q * 3))) + turn + (q * q * 6), Math.Sin(angle) * 0.2 * q,
            Math.Cos(angle) * 0.25 * q, Peak * (1 - (0.45 * q)),
            CentreX + (Math.Cos(angle) * radius), restY + (Math.Sin(angle) * radius * 0.35), 0.8);
    }

    /// <summary>The trails behind the two cards while they orbit: blocks where they have been, fading.</summary>
    private void Trails(double t)
    {
        if (t < CardTradeTimeline.Circle + 0.1)
        {
            return;
        }

        for (var side = -1; side <= 1; side += 2)
        {
            for (var k = 1; k <= 14; k++)
            {
                var back = t - (k * 0.018);
                if (back < CardTradeTimeline.Circle) break;
                var pose = Pose(back, side);
                var colour = k < 4 ? RuneHot : k < 9 ? Rune : RuneDim;
                Block(pose.CentreX, pose.CentreY, colour);
                if (k % 3 == 0) Block(pose.CentreX + Cell, pose.CentreY - Cell, colour);
            }
        }
    }

    // ===================================================================================================== CLASH

    /// <summary>The meeting: a white flash, a shockwave ring, the shards flying out and drawn back into the orb.</summary>
    private void Clash(double t, Color tier)
    {
        var since = t - CardTradeTimeline.Clash;

        // El fogonazo: toda la pantalla blanca un instante y se apaga a bloques.
        if (since is >= -0.05 and < 0.35)
        {
            var strength = since < 0.05 ? 1 : 1 - ((since - 0.05) / 0.3);
            Wash(White, strength * 0.9);
        }

        // La onda: un anillo que se abre y se apaga.
        if (since is >= 0 and < 0.7)
        {
            var r = Width * 0.05 + (since / 0.7 * Width * 0.55);
            var colour = since < 0.25 ? White : Mix(White, tier, (since - 0.25) / 0.45);
            Ellipse(CentreX, CentreY, r, r * 0.62, 1, 0, colour);
            Ellipse(CentreX, CentreY, r * 0.94, r * 0.58, 1, 0.05, Mix(colour, Ink, 0.5));
        }

        // Las esquirlas: salen disparadas, frenan y el orbe se las traga.
        if (since is >= 0 and < CardTradeTimeline.Orb - CardTradeTimeline.Clash + 0.1)
        {
            var span = CardTradeTimeline.Orb - CardTradeTimeline.Clash;
            var outward = Math.Min(1, since / 0.45);
            var inward = since < 0.55 ? 0 : Ease((since - 0.55) / (span - 0.55));
            var reach = Width * 0.42 * (1 - Math.Pow(1 - outward, 3)) * (1 - inward);

            foreach (var (_, _, vx, vy, c) in _shards)
            {
                var x = CentreX + (vx * reach);
                var y = CentreY + (vy * reach * 0.7);
                var colour = c switch { 0 => White, 1 => Rune, _ => tier };
                Block(x, y, colour);
                if (inward < 0.8) Block(x - (vx * Cell * 2), y - (vy * Cell * 1.4), Mix(colour, Ink, 0.5));
            }
        }

        // El orbe: crece mientras las traga, late y se pliega en carta.
        if (since >= 0.4 && t < CardTradeTimeline.Card)
        {
            var grow = Ease((since - 0.4) / 0.9);
            var fold = t < CardTradeTimeline.Orb ? 0 : Ease((t - CardTradeTimeline.Orb) / (CardTradeTimeline.Card - CardTradeTimeline.Orb));
            var pulse = 1 + (0.08 * Math.Sin(t * 14));
            var r = Height * 0.09 * grow * pulse * (1 - (fold * 0.7));
            Beams(t, tier, r * (2.4 + fold * 2), grow * (1 - (fold * 0.3)));
            Disc(CentreX, CentreY, r * 1.35, Mix(tier, Ink, 0.55));
            Disc(CentreX, CentreY, r, tier);
            Disc(CentreX, CentreY, r * 0.55, Mix(tier, White, 0.6));
            Disc(CentreX - (r * 0.25), CentreY - (r * 0.25), r * 0.18, White);
        }

        // El destello de la carta al plegarse el orbe.
        if (t >= CardTradeTimeline.Card - 0.1 && t < CardTradeTimeline.Card + 0.25)
        {
            var s = (t - (CardTradeTimeline.Card - 0.1)) / 0.35;
            Wash(Mix(tier, White, 0.5), (1 - s) * 0.5);
        }
    }

    /// <summary>The beams of the orb: sixteen spokes of blocks turning, in the colour of what is coming.</summary>
    private void Beams(double t, Color tier, double length, double strength)
    {
        if (strength <= 0.05)
        {
            return;
        }

        for (var i = 0; i < 16; i++)
        {
            var angle = (i / 16.0 * Math.PI * 2) + (t * 0.8);
            var reach = length * (i % 2 == 0 ? 1 : 0.62) * strength;
            for (var d = length * 0.35; d < reach; d += Cell * 1.5)
            {
                var colour = d < reach * 0.6 ? tier : Mix(tier, Ink, 0.45);
                Block(CentreX + (Math.Cos(angle) * d), CentreY + (Math.Sin(angle) * d * 0.8), colour);
            }
        }
    }

    // ==================================================================================================== RESULT

    /// <summary>The card that comes out: face down from the orb, growing, and it turns over to show itself.</summary>
    private HandPose ResultPose(double t)
    {
        if (t < CardTradeTimeline.Card)
        {
            // Todavía dentro del orbe: diminuta y boca abajo.
            var p = Math.Clamp((t - (CardTradeTimeline.Orb - 0.2)) / (CardTradeTimeline.Card - CardTradeTimeline.Orb + 0.2), 0, 1);
            return new HandPose(Math.PI + (t * 3), 0, 0, Math.Max(0.4, Peak * 0.25 * p), CentreX, CentreY, 0.8);
        }

        if (t < CardTradeTimeline.Reveal)
        {
            // Crece boca abajo con un rebote y flota.
            var p = (t - CardTradeTimeline.Card) / (CardTradeTimeline.Reveal - CardTradeTimeline.Card);
            var grow = BackOut(p, 1.5);
            var turn = p < 0.7 ? 0 : Ease((p - 0.7) / 0.3);
            return new HandPose(Math.PI - (turn * Math.PI), Math.Sin(t * 3) * 0.05, 0, Math.Max(0.5, Peak * (0.25 + (0.95 * grow))),
                CentreX, CentreY - (Math.Sin(p * Math.PI) * Cell * 3), 0.8);
        }

        // De cara, un poco más grande que las otras dos, meciéndose.
        var s = t - CardTradeTimeline.Reveal;
        return new HandPose(Math.Sin(s * 1.8) * 0.12, Math.Cos(s * 1.5) * 0.05, 0, Peak * 1.2,
            CentreX, CentreY - (Math.Sin(s * 2.2) * Cell), 0.8);
    }

    /// <summary>Stars round the revealed card: more the rarer it is, gold for a shiny.</summary>
    private void Stars(double t, Color tier, int rarity, bool shiny)
    {
        var s = t - CardTradeTimeline.Reveal;
        var count = 4 + (rarity * 4) + (shiny ? 10 : 0);

        for (var i = 0; i < count; i++)
        {
            var phase = (i * 0.61) % 1;
            var life = (s * 0.8 + phase) % 1;
            var angle = (i * 2.399) + (s * 0.3);
            var r = Height * (0.22 + (life * 0.14));
            var x = CentreX + (Math.Cos(angle) * r * 1.2);
            var y = CentreY + (Math.Sin(angle) * r);
            var size = life < 0.3 ? 2 : life < 0.7 ? 1 : 0;
            Star(x, y, size, shiny && i % 2 == 0 ? Gold : life < 0.5 ? White : tier);
        }
    }

    // =================================================================================================== HELPERS

    private void Wash(Color colour, double strength)
    {
        var a = Math.Clamp(strength, 0, 1);
        if (a <= 0) return;

        for (var i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = (byte)(Pixels[i] + ((colour.B - Pixels[i]) * a));
            Pixels[i + 1] = (byte)(Pixels[i + 1] + ((colour.G - Pixels[i + 1]) * a));
            Pixels[i + 2] = (byte)(Pixels[i + 2] + ((colour.R - Pixels[i + 2]) * a));
        }
    }

    private void Disc(double cx, double cy, double r, Color colour)
    {
        if (r < 1) return;

        for (var y = cy - r; y <= cy + r; y += Cell)
        {
            for (var x = cx - r; x <= cx + r; x += Cell)
            {
                var dx = x - cx;
                var dy = y - cy;
                if ((dx * dx) + (dy * dy) <= r * r) Block(x, y, colour);
            }
        }
    }

    private void Block(double x, double y, Color colour)
    {
        // A la rejilla de bloques, para que todo encaje como pixel art.
        var gx = (int)(Math.Floor(x / Cell) * Cell);
        var gy = (int)(Math.Floor(y / Cell) * Cell);
        Fill(gx, gy, colour.B, colour.G, colour.R, Cell);
    }

    private void Fill(int left, int top, byte b, byte g, byte r, int size)
    {
        for (var y = Math.Max(0, top); y < Math.Min(Height, top + size); y++)
        {
            for (var x = Math.Max(0, left); x < Math.Min(Width, left + size); x++)
            {
                var at = ((y * Width) + x) * 4;
                Pixels[at] = b;
                Pixels[at + 1] = g;
                Pixels[at + 2] = r;
                Pixels[at + 3] = 255;
            }
        }
    }

    private void Star(double x, double y, int size, Color colour)
    {
        Block(x, y, colour);
        for (var i = 1; i <= size; i++)
        {
            Block(x + (i * Cell), y, colour);
            Block(x - (i * Cell), y, colour);
            Block(x, y + (i * Cell), colour);
            Block(x, y - (i * Cell), colour);
        }
    }

    /// <summary>Lays premultiplied pixels of the same size over the frame, inside a rectangle.</summary>
    private void Over(byte[] source, (int X, int Y, int Width, int Height) rect)
    {
        var x0 = Math.Max(0, rect.X);
        var y0 = Math.Max(0, rect.Y);
        var x1 = Math.Min(Width, rect.X + rect.Width);
        var y1 = Math.Min(Height, rect.Y + rect.Height);

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var at = ((y * Width) + x) * 4;
                var a = source[at + 3];
                if (a == 0) continue;
                var keep = 255 - a;
                Pixels[at] = (byte)(source[at] + ((Pixels[at] * keep) / 255));
                Pixels[at + 1] = (byte)(source[at + 1] + ((Pixels[at + 1] * keep) / 255));
                Pixels[at + 2] = (byte)(source[at + 2] + ((Pixels[at + 2] * keep) / 255));
                Pixels[at + 3] = 255;
            }
        }
    }

    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static double BackOut(double t, double overshoot)
    {
        var u = Math.Clamp(t, 0, 1) - 1;
        return 1 + (u * u * (((overshoot + 1) * u) + overshoot));
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Rgb((byte)(a.R + ((b.R - a.R) * t)), (byte)(a.G + ((b.G - a.G) * t)), (byte)(a.B + ((b.B - a.B) * t)));
    }

}
