using Color = PermaLocke.App.Views.PixelColour;
using System.Runtime.InteropServices;
using static PermaLocke.App.Views.CellCanvas;

namespace PermaLocke.App.Views;

/// <summary>Where the card in the hand is and how it is turned, for one frame.</summary>
/// <param name="Yaw">Turn around its vertical axis, in radians: the tilt left and right, plus π once it is turned over.</param>
/// <param name="Pitch">Turn around its horizontal axis, in radians: the tilt towards and away from the player.</param>
/// <param name="Roll">Turn in the plane of the screen, in radians: the twist as it comes out of its pocket.</param>
/// <param name="Scale">Screen pixels per cell of the card, flat.</param>
/// <param name="CentreX">Where its centre is, in screen pixels of the scene.</param>
/// <param name="Lift">How far above the table, 0 to 1: the shadow falls further and fainter the higher it is.</param>
public readonly record struct HandPose(double Yaw, double Pitch, double Roll, double Scale, double CentreX, double CentreY, double Lift);

/// <summary>
/// The card in the hand (§187): the card drawn in perspective at the screen's own resolution, turned by the mouse,
/// turned over, flying out of its pocket, with its shadow on the table and the light of the room on it.
/// </summary>
/// <remarks>
/// <para>
/// The card is still pixel art: every screen pixel takes the colour of one cell of the card, never a blend, so its
/// pixels stay square and sharp and only lean with the card — the way the cards of Balatro turn. The projection is a
/// plane rotated in 3D and seen through a lens, turned into a homography, and inverted for every pixel.
/// </para>
/// <para>
/// Around it, what makes it an event: rays behind a rare card in the colour of its finish, dust floating in the light,
/// ash falling round a fallen one, and a burst of sparkles as a rare one lands. The foil catches the light as the card
/// turns (<see cref="TcgCardArt.Animate"/> with the light where the tilt puts it) and a glare slides over its face.
/// Pure: a pose and a moment make a frame, so it is tested without a window.
/// </para>
/// </remarks>
public sealed class HandScene
{
    private static readonly Color Dust = Rgb(0xE8, 0xDE, 0xFF);
    private static readonly Color Ash = Rgb(0x8A, 0x84, 0x90);
    private static readonly Color EmberSpark = Rgb(0xFF, 0x9A, 0x3A);
    private static readonly Color RayWhite = Rgb(0xF4, 0xEE, 0xFF);
    private static readonly Color RayGold = Rgb(0xFF, 0xD8, 0x6A);
    private static readonly Color RaySilver = Rgb(0xD8, 0xE0, 0xF4);
    private static readonly Color Glare = Rgb(0xFF, 0xFF, 0xFF);

    private static readonly Color[] RainbowRays =
    [
        Rgb(0xFF, 0x9A, 0xB0), Rgb(0xFF, 0xD0, 0x8A), Rgb(0xFF, 0xF4, 0x9A), Rgb(0xA8, 0xF4, 0xA0),
        Rgb(0x9A, 0xE8, 0xFF), Rgb(0xB0, 0xB8, 0xFF), Rgb(0xE0, 0xA8, 0xFF)
    ];

    public HandScene(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new byte[Width * Height * 4];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame, premultiplied BGRA: transparent where nothing is drawn, so the screen shows through.</summary>
    public byte[] Pixels { get; }

    /// <summary>The rectangle painted in the last frame, to copy to the screen and nothing more.</summary>
    public (int X, int Y, int Width, int Height) Dirty { get; private set; }

    private (int X0, int Y0, int X1, int Y1) _previous = (0, 0, 0, 0);

    /// <summary>The face of this frame with what moves on it, reused from frame to frame.</summary>
    private CellCanvas? _live;

    /// <summary>Each cell of the face already lit and glared, as the four bytes it puts on screen; 0 for none.</summary>
    private uint[] _lit = [];

    /// <summary>
    /// Paints a frame: the effects behind the card, its shadow and the card itself.
    /// </summary>
    /// <param name="front">The card's face, as drawn once; its moving part is added here, lit by the pose.</param>
    /// <param name="back">The other face, seen once it is turned over.</param>
    /// <param name="arrival">Seconds since it landed in the hand, for the burst of a rare card; negative while it flies.</param>
    /// <param name="surroundings">
    /// False for a card over something that is not a table, such as the game (§190): no shadow and no dust, only the
    /// card with its rays and its burst.
    /// </param>
    public void Render(TcgRender front, TcgRender back, HandPose pose, double seconds, uint seed, double arrival = 99,
        bool surroundings = true)
    {
        var cardWidth = front.Canvas.Width;
        var cardHeight = front.Canvas.Height;
        var projection = Projection.Of(pose, cardWidth, cardHeight);

        // Lo que se pinta este fotograma: la carta, su sombra y el halo de efectos a su alrededor.
        var (bx0, by0, bx1, by1) = projection.Bounds;
        var halo = (int)Math.Max(40, Math.Max(bx1 - bx0, by1 - by0) * 0.55);
        var area = Clip(bx0 - halo, by0 - halo, bx1 + halo, by1 + halo + (int)(pose.Lift * 40));

        // Se borra lo pintado antes y lo que se va a pintar ahora, y solo eso se copia a la pantalla.
        var dirty = Union(_previous, area);
        Clear(dirty);
        _previous = area;
        Dirty = (dirty.X0, dirty.Y0, dirty.X1 - dirty.X0, dirty.Y1 - dirty.Y0);

        var block = Math.Max(2, (int)Math.Round(pose.Scale));
        var finish = front.Finish;
        var fallen = front.Embers.Count > 0;

        // La cara que se ve, con lo que se mueve en ella iluminado según la inclinación.
        var facing = projection.FacesFront ? front : back;
        if (_live is null || _live.Width != facing.Canvas.Width || _live.Height != facing.Canvas.Height)
        {
            _live = new CellCanvas(facing.Canvas.Width, facing.Canvas.Height);
            _lit = new uint[facing.Canvas.Width * facing.Canvas.Height];
        }

        Rays(area, pose, finish, seconds, block);
        if (surroundings)
        {
            Motes(area, pose, fallen, seconds, seed, block);
            Shadow(projection, pose, facing.Canvas, block);
        }

        var live = _live;
        live.CopyFrom(facing.Canvas);
        var light = Math.Clamp(0.5 - (Math.Sin(pose.Yaw) * 0.9) - (pose.Pitch * 0.8), 0, 1);
        TcgCardArt.Animate(facing, live, 0, 0, seconds, seed, facing.IsLive ? light : null);

        Card(projection, live, pose, light);

        if (arrival is >= 0 and < 1.2 && finish != TcgFinish.Plain)
        {
            Burst(pose, finish, arrival, seed, block);
        }
    }

    // ================================================================================================= PROJECTION

    /// <summary>
    /// The card's plane rotated and seen through a lens, as a homography from card cells to screen pixels, with its
    /// inverse for sampling.
    /// </summary>
    private readonly record struct Projection(double[] Inverse, (int X0, int Y0, int X1, int Y1) Bounds, bool FacesFront,
        double Nx, double Ny, double Nz)
    {
        public static Projection Of(HandPose pose, int width, int height)
        {
            // Columnas de la rotación: dónde acaban los ejes X e Y de la carta. Primero el giro vertical, luego el
            // horizontal y por último el del plano de la pantalla.
            var (sy, cy) = Math.SinCos(pose.Yaw);
            var (sp, cp) = Math.SinCos(pose.Pitch);
            var (sr, cr) = Math.SinCos(pose.Roll);

            // Ry, luego Rx, luego Rz aplicados a (1,0,0) y (0,1,0).
            double r1x = cy, r1y = 0, r1z = -sy;
            (r1y, r1z) = ((r1y * cp) - (r1z * sp), (r1y * sp) + (r1z * cp));
            (r1x, r1y) = ((r1x * cr) - (r1y * sr), (r1x * sr) + (r1y * cr));

            double r2x = 0, r2y = 1, r2z = 0;
            (r2y, r2z) = ((r2y * cp) - (r2z * sp), (r2y * sp) + (r2z * cp));
            (r2x, r2y) = ((r2x * cr) - (r2y * sr), (r2x * sr) + (r2y * cr));

            var s = pose.Scale;
            var focal = height * s * 3.2;

            // (X, Y, 1) en celdas desde el centro de la carta → (x·w, y·w, w) en píxeles desde su centro.
            double[] h =
            [
                focal * s * r1x, focal * s * r2x, 0,
                focal * s * r1y, focal * s * r2y, 0,
                s * r1z, s * r2z, focal
            ];

            var inverse = Invert(h);

            // Las cuatro esquinas, para saber qué rectángulo de la pantalla ocupa.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var (cx, cyy) in new[] { (-width / 2.0, -height / 2.0), (width / 2.0, -height / 2.0), (-width / 2.0, height / 2.0), (width / 2.0, height / 2.0) })
            {
                var w = (h[6] * cx) + (h[7] * cyy) + h[8];
                var px = pose.CentreX + (((h[0] * cx) + (h[1] * cyy)) / w);
                var py = pose.CentreY + (((h[3] * cx) + (h[4] * cyy)) / w);
                x0 = Math.Min(x0, (int)Math.Floor(px));
                y0 = Math.Min(y0, (int)Math.Floor(py));
                x1 = Math.Max(x1, (int)Math.Ceiling(px));
                y1 = Math.Max(y1, (int)Math.Ceiling(py));
            }

            // La normal: de frente mira lejos de quien mira (z positivo hacia dentro de la pantalla).
            var nx = (r1y * r2z) - (r1z * r2y);
            var ny = (r1z * r2x) - (r1x * r2z);
            var nz = (r1x * r2y) - (r1y * r2x);

            return new Projection(inverse, (x0, y0, x1, y1), nz > 0, nx, ny, nz);
        }

        /// <summary>The cell of the card under a screen pixel, as fractions of cells; null outside the card.</summary>
        public (double U, double V)? Sample(double px, double py, HandPose pose, int width, int height)
        {
            var qx = px - pose.CentreX;
            var qy = py - pose.CentreY;
            var i = Inverse;
            var w = (i[6] * qx) + (i[7] * qy) + i[8];
            if (Math.Abs(w) < 1e-9)
            {
                return null;
            }

            var u = (((i[0] * qx) + (i[1] * qy) + i[2]) / w) + (width / 2.0);
            var v = (((i[3] * qx) + (i[4] * qy) + i[5]) / w) + (height / 2.0);
            return u >= 0 && v >= 0 && u < width && v < height ? (u, v) : null;
        }

        private static double[] Invert(double[] m)
        {
            var a = m[0]; var b = m[1]; var c = m[2];
            var d = m[3]; var e = m[4]; var f = m[5];
            var g = m[6]; var h = m[7]; var k = m[8];

            var co0 = (e * k) - (f * h);
            var co1 = -((d * k) - (f * g));
            var co2 = (d * h) - (e * g);
            var det = (a * co0) + (b * co1) + (c * co2);
            if (Math.Abs(det) < 1e-12)
            {
                det = det < 0 ? -1e-12 : 1e-12;
            }

            var inv = 1 / det;
            return
            [
                co0 * inv, -((b * k) - (c * h)) * inv, ((b * f) - (c * e)) * inv,
                co1 * inv, ((a * k) - (c * g)) * inv, -((a * f) - (c * d)) * inv,
                co2 * inv, -((a * h) - (b * g)) * inv, ((a * e) - (b * d)) * inv
            ];
        }
    }

    // ================================================================================================= CARD

    /// <summary>
    /// The card, pixel by pixel: the colour of the cell under each one, lit by how the card faces the lamp, with the
    /// glare of the lamp sliding over its face as it turns.
    /// </summary>
    /// <remarks>
    /// The hot loop of the album: a quarter of a million pixels a frame. So the light and the glare are worked out once
    /// per cell of the card (a few thousand) into <see cref="_lit"/>, and each pixel only walks the projection — which
    /// is linear along a row, so it is advanced by adding — divides once and copies four bytes (§188).
    /// </remarks>
    private void Card(Projection projection, CellCanvas face, HandPose pose, double light)
    {
        var width = face.Width;
        var height = face.Height;

        // La lámpara está arriba a la izquierda: la carta se aclara al mirarla y se oscurece al apartarse.
        var lit = Math.Clamp(1.0 - (0.22 * (1 - Math.Abs(projection.Nz))) + (0.08 * ((-projection.Nx * 0.6) - (projection.Ny * 0.8))), 0.7, 1.08);
        var litScale = (int)Math.Round(lit * 256);

        // El brillo de la lámpara: una franja diagonal arriba a la izquierda en reposo, que baja por la carta a donde la
        // inclinación la lleva.
        var glareAt = (light * 1.6) - 0.55;
        var source = face.Bgra;

        for (var cv = 0; cv < height; cv++)
        {
            for (var cu = 0; cu < width; cu++)
            {
                var at = ((cv * width) + cu) * 4;
                if (source[at + 3] == 0)
                {
                    _lit[(cv * width) + cu] = 0;
                    continue;
                }

                int b = source[at], g = source[at + 1], r = source[at + 2];

                // Brillo por celda y no por píxel: el reflejo también va en píxeles de la carta.
                var diagonal = ((cu / (double)width) + (cv / (double)height)) / 2;
                var glare = 1 - (Math.Abs(diagonal - glareAt) / 0.09);
                if (glare > 0 && Dither(cu, cv, glare))
                {
                    b += ((Glare.B - b) * 72) >> 8;
                    g += ((Glare.G - g) * 72) >> 8;
                    r += ((Glare.R - r) * 72) >> 8;
                }

                b = Math.Min(255, (b * litScale) >> 8);
                g = Math.Min(255, (g * litScale) >> 8);
                r = Math.Min(255, (r * litScale) >> 8);
                _lit[(cv * width) + cu] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
            }
        }

        var (x0, y0, x1, y1) = projection.Bounds;
        x0 = Math.Max(0, x0);
        y0 = Math.Max(0, y0);
        x1 = Math.Min(Width - 1, x1);
        y1 = Math.Min(Height - 1, y1);
        if (x1 < x0 || y1 < y0)
        {
            return;
        }

        var target = MemoryMarshal.Cast<byte, uint>(Pixels.AsSpan());
        var m = projection.Inverse;
        var halfW = width / 2.0;
        var halfH = height / 2.0;
        var front = projection.FacesFront;

        for (var py = y0; py <= y1; py++)
        {
            // Al principio de la fila; a lo largo de ella, la proyección solo suma.
            var qx = x0 + 0.5 - pose.CentreX;
            var qy = py + 0.5 - pose.CentreY;
            var u = (m[0] * qx) + (m[1] * qy) + m[2];
            var v = (m[3] * qx) + (m[4] * qy) + m[5];
            var w = (m[6] * qx) + (m[7] * qy) + m[8];
            var row = py * Width;

            for (var px = x0; px <= x1; px++, u += m[0], v += m[3], w += m[6])
            {
                if (w > -1e-9 && w < 1e-9)
                {
                    continue;
                }

                var inverse = 1 / w;
                var su = (u * inverse) + halfW;
                var sv = (v * inverse) + halfH;
                if (su < 0 || sv < 0 || su >= width || sv >= height)
                {
                    continue;
                }

                // Por detrás, la carta está vuelta: su columna de la derecha es la de la izquierda.
                var cu = front ? (int)su : width - 1 - (int)su;
                var colour = _lit[((int)sv * width) + cu];
                if (colour != 0)
                {
                    target[row + px] = colour;
                }
            }
        }
    }

    /// <summary>
    /// The card's shadow on the table: its shape pushed away from the lamp, a flat half tone in blocks. Its real shape:
    /// where a fallen card burnt away there is no shadow either.
    /// </summary>
    private void Shadow(Projection projection, HandPose pose, CellCanvas face, int block)
    {
        var width = face.Width;
        var height = face.Height;
        var dx = 10 + (pose.Lift * 26);
        var dy = 14 + (pose.Lift * 36);
        var alpha = (byte)(150 - (pose.Lift * 60));
        var (x0, y0, x1, y1) = projection.Bounds;

        for (var by = y0 + (int)dy; by < y1 + dy + block; by += block)
        {
            for (var bx = x0 + (int)dx; bx < x1 + dx + block; bx += block)
            {
                if (projection.Sample(bx - dx + (block / 2.0), by - dy + (block / 2.0), pose, width, height) is not { } cell)
                {
                    continue;
                }

                var cu = projection.FacesFront ? (int)cell.U : width - 1 - (int)cell.U;
                if (!face.IsSet(cu, (int)cell.V))
                {
                    continue;
                }

                FillBlock(bx, by, block, Rgb(0x04, 0x02, 0x0A), alpha);
            }
        }
    }

    // ================================================================================================= AROUND

    /// <summary>Rays turning slowly behind a card with a finish: white, silver, gold or rainbow.</summary>
    private void Rays((int X0, int Y0, int X1, int Y1) area, HandPose pose, TcgFinish finish, double seconds, int block)
    {
        if (finish == TcgFinish.Plain)
        {
            return;
        }

        var reach = pose.Scale * 70;
        var count = finish == TcgFinish.Polychrome ? 14 : 10;
        var strength = finish is TcgFinish.Gold or TcgFinish.Polychrome ? 1.0 : 0.6;

        for (var by = area.Y0 - (area.Y0 % block); by < area.Y1; by += block)
        {
            for (var bx = area.X0 - (area.X0 % block); bx < area.X1; bx += block)
            {
                var dx = bx + (block / 2.0) - pose.CentreX;
                var dy = by + (block / 2.0) - pose.CentreY;
                var r = Math.Sqrt((dx * dx) + (dy * dy)) / reach;
                if (r > 1 || r < 0.1)
                {
                    continue;
                }

                var turn = (Math.Atan2(dy, dx) / (2 * Math.PI)) + (seconds * 0.02);
                var ray = ((turn * count % 1) + 1) % 1;
                if (ray > 0.38)
                {
                    continue;
                }

                // Más débil hacia fuera y en el borde de cada rayo, en tramado de bloques.
                var fade = (1 - r) * (1 - (Math.Abs(ray - 0.19) / 0.19)) * strength;
                if (!Dither(bx / block, by / block, fade * 0.9))
                {
                    continue;
                }

                var colour = finish switch
                {
                    TcgFinish.Gold => RayGold,
                    TcgFinish.Reverse => RaySilver,
                    TcgFinish.Polychrome => RainbowRays[(((int)Math.Floor(turn * count) % RainbowRays.Length) + RainbowRays.Length) % RainbowRays.Length],
                    _ => RayWhite
                };

                FillBlock(bx, by, block, colour, (byte)(55 + (fade * 70)));
            }
        }
    }

    /// <summary>Dust floating up in the lamp's light; round a fallen card, ash falling and sparks rising instead.</summary>
    private void Motes((int X0, int Y0, int X1, int Y1) area, HandPose pose, bool fallen, double seconds, uint seed, int block)
    {
        var width = area.X1 - area.X0;
        var height = area.Y1 - area.Y0;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (var i = 0; i < 28; i++)
        {
            var speed = 0.03 + (Hash(i, 1, seed) * 0.05);
            var t = ((seconds * speed) + Hash(i, 2, seed)) % 1;
            var up = fallen && i % 3 != 0 ? -1 : 1;
            var x = area.X0 + (Hash(i, 3, seed) * width) + (Math.Sin((seconds * 0.7) + (i * 1.3)) * block * 3);
            var y = up > 0 ? area.Y1 - (t * height) : area.Y0 + (t * height);
            var glow = Math.Sin(t * Math.PI);
            if (!Dither(i, (int)(seconds * 4), glow))
            {
                continue;
            }

            var colour = !fallen ? Dust : up > 0 ? EmberSpark : Ash;
            var size = i % 5 == 0 ? block : Math.Max(2, block / 2);
            FillBlock((int)x, (int)y, size, colour, (byte)(90 + (glow * 110)));
        }
    }

    /// <summary>The sparkles thrown out when a rare card lands in the hand.</summary>
    private void Burst(HandPose pose, TcgFinish finish, double since, uint seed, int block)
    {
        var fade = 1 - (since / 1.2);
        var reach = pose.Scale * (30 + (since * 60));

        for (var i = 0; i < 22; i++)
        {
            var angle = (i / 22.0 * Math.PI * 2) + (Hash(i, 7, seed) * 0.4);
            var r = reach * (0.7 + (Hash(i, 8, seed) * 0.5));
            var x = pose.CentreX + (Math.Cos(angle) * r);
            var y = pose.CentreY + (Math.Sin(angle) * r * 1.2) + (since * since * 40);
            var colour = finish switch
            {
                TcgFinish.Gold => RayGold,
                TcgFinish.Polychrome => RainbowRays[i % RainbowRays.Length],
                TcgFinish.Reverse => RaySilver,
                _ => RayWhite
            };

            var size = since < 0.4 ? block * 2 : block;
            if (Dither(i, 0, fade + 0.2))
            {
                FillBlock((int)x - (size / 2), (int)y, size, colour, (byte)(255 * Math.Clamp(fade + 0.3, 0, 1)));
                FillBlock((int)x - (size / 2), (int)y - size, size, colour, (byte)(160 * Math.Clamp(fade, 0, 1)));
            }
        }
    }

    // ================================================================================================= PIXELS

    private (int X0, int Y0, int X1, int Y1) Clip(int x0, int y0, int x1, int y1) =>
        (Math.Clamp(x0, 0, Width), Math.Clamp(y0, 0, Height), Math.Clamp(x1, 0, Width), Math.Clamp(y1, 0, Height));

    private static (int X0, int Y0, int X1, int Y1) Union((int X0, int Y0, int X1, int Y1) a, (int X0, int Y0, int X1, int Y1) b)
    {
        if (a.X1 <= a.X0 || a.Y1 <= a.Y0) return b;
        if (b.X1 <= b.X0 || b.Y1 <= b.Y0) return a;
        return (Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1));
    }

    private void Clear((int X0, int Y0, int X1, int Y1) rect)
    {
        for (var y = rect.Y0; y < rect.Y1; y++)
        {
            Array.Clear(Pixels, ((y * Width) + rect.X0) * 4, (rect.X1 - rect.X0) * 4);
        }
    }

    /// <summary>Wipes the whole frame, for a card put away.</summary>
    public void Clear()
    {
        Array.Clear(Pixels);
        Dirty = (0, 0, Width, Height);
        _previous = (0, 0, 0, 0);
    }

    /// <summary>Lays a colour over what is there with an opacity, premultiplied as the screen wants it.</summary>
    private void Blend(int x, int y, Color colour, byte alpha)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return;
        }

        // En enteros: (color·a + fondo·(255 − a)) / 255, que es lo que cuesta nada.
        var at = ((y * Width) + x) * 4;
        var keep = 255 - alpha;
        Pixels[at] = (byte)(((colour.B * alpha) + (Pixels[at] * keep)) / 255);
        Pixels[at + 1] = (byte)(((colour.G * alpha) + (Pixels[at + 1] * keep)) / 255);
        Pixels[at + 2] = (byte)(((colour.R * alpha) + (Pixels[at + 2] * keep)) / 255);
        Pixels[at + 3] = (byte)(alpha + ((Pixels[at + 3] * keep) / 255));
    }

    private void FillBlock(int x, int y, int size, Color colour, byte alpha)
    {
        for (var yy = y; yy < y + size; yy++)
        {
            for (var xx = x; xx < x + size; xx++)
            {
                Blend(xx, yy, colour, alpha);
            }
        }
    }
}
