using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>A cell of the fallen one: of its picture (<see cref="Dx"/>, <see cref="Dy"/> from its middle) or of what is written beside it.</summary>
internal sealed class DeathCell
{
    public int X;
    public int Y;
    public int Dx;
    public int Dy;
    public uint C;
    public double H;
    public bool Eye;
    public int Piece;

    /// <summary>A cell of what is written beside it, not of its picture.</summary>
    public bool Writing;
}

/// <summary>A piece the fallen one breaks into: cells of its own picture, the moment it comes away, its middle.</summary>
internal sealed class DeathPiece
{
    public int Id;
    public List<DeathCell> Cells = [];
    public HashSet<int> Set = [];
    public double Cx;
    public double Cy;
    public double Leave;
    public bool Face;

    public bool Has(int dx, int dy) => Set.Contains(Key(dx, dy));

    public static int Key(int dx, int dy) => ((dx + 500) * 1000) + dy + 500;
}

/// <summary>How a death breaks its fallen one: its pieces, and the moment each crack cell shows (cracks are the edges between pieces).</summary>
internal sealed class DeathModel
{
    public DeathPiece[] Pieces = [];
    public Dictionary<DeathCell, double> Cracks = [];
    public double Hx;
    public double Hy;
}

/// <summary>
/// One death being painted in the party's window (2026-10-15): the fallen one taken apart in the three phases every death shares (the impact,
/// the breaking, the end), in the way of its <see cref="CapDeathKind"/>. Built when the death starts, from the creature as it was then: its
/// picture cell by cell and everything written beside it (its name, level, HP and their bar, types, ability), which go with it.
/// </summary>
public sealed class CapDeathScene
{
    internal readonly List<DeathCell> Body = [];
    internal readonly List<DeathCell> Info = [];
    internal readonly CapWorldLayout Layout;
    private readonly DeathStyle _style;

    internal int X;
    internal int Y;
    internal int Left;
    internal int Right;
    internal int Top;
    internal int Bottom;
    internal int TextLeft;
    internal int InfoX0;
    internal int InfoX1;
    internal double LMin;
    internal double LMax;
    internal (int X, int Y) Core;

    public CapDeathTurn Turn { get; }

    /// <param name="picture">A sheet of the window's size with only the creature's picture on it (its body and outline).</param>
    /// <param name="writing">A sheet of the window's size with only what is written beside it.</param>
    /// <param name="others">The rest of the party, for the deaths that touch them.</param>
    /// <param name="rowLine">The row of the line under its place in the party.</param>
    internal CapDeathScene(CapDeathTurn turn, CapWorldLayout layout, CapSheet picture, CapSheet writing, int textLeft, int rowLine, IReadOnlyList<CapCreature> others)
    {
        Turn = turn;
        Layout = layout;
        TextLeft = textLeft;
        RowLine = rowLine;
        Others = others;
        for (var y = 0; y < picture.H; y++)
        {
            for (var x = 0; x < picture.W; x++)
            {
                if (picture.Solid(x, y)) Body.Add(new DeathCell { X = x, Y = y, C = picture.At(x, y) });
                if (writing.Solid(x, y)) Info.Add(new DeathCell { X = x, Y = y, C = writing.At(x, y), H = Rnd((x * 13) + 5, (y * 29) + 1), Writing = true });
            }
        }

        if (Body.Count > 0)
        {
            X = R(Body.Average(q => q.X));
            Y = R(Body.Average(q => q.Y));
        }

        foreach (var q in Body)
        {
            q.Dx = q.X - X;
            q.Dy = q.Y - Y;
            q.H = Rnd((q.Dx * 31) + 7, (q.Dy * 17) + 3);
            q.Eye = Red(q.C) > 150 && Green(q.C) < 110 && Red(q.C) > Blue(q.C);
        }

        Left = Body.Count > 0 ? Body.Min(q => q.Dx) : 0;
        Right = Body.Count > 0 ? Body.Max(q => q.Dx) : 0;
        Top = Body.Count > 0 ? Body.Min(q => q.Dy) : 0;
        Bottom = Body.Count > 0 ? Body.Max(q => q.Dy) : 0;
        LMin = Body.Count > 0 ? Body.Min(q => Luma(q.C)) : 0;
        LMax = Body.Count > 0 ? Body.Max(q => Luma(q.C)) : 255;
        InfoX0 = Info.Count > 0 ? Info.Min(q => q.X) : textLeft;
        InfoX1 = Info.Count > 0 ? Info.Max(q => q.X) : textLeft + 1;
        Core = CapFarSky.WormholeAt(layout) ?? (layout.Main.Left + (layout.Main.Width / 2), Math.Max(10, layout.SurfaceY / 2));
        _style = DeathStyle.For(turn.Kind, this);
    }

    internal IReadOnlyList<CapCreature> Others { get; }

    internal int RowLine { get; }

    /// <summary>The share of light of a colour against the picture's darkest and lightest: 0 to 1.</summary>
    internal double Tone(uint c) => (Luma(c) - LMin) / Math.Max(1, LMax - LMin);

    /// <summary>The picture greyed by the tone of each cell.</summary>
    internal uint Greyed(uint c) => CapSheet.Mix(0x50545Cu, 0xE6E8F0u, Tone(c));

    /// <summary>The size the wormhole is drawn at while this death plays (it swells in some of them).</summary>
    public double WormholeSize(double t) => _style.WormholeSize(t - Turn.Start);

    /// <summary>Paints the death at a moment of the window's clock, after the rest of the window.</summary>
    public void Paint(CapSheet p, double now, double phase)
    {
        var t = now - Turn.Start;
        if (t < 0 || t > Seconds(Turn.Kind)) return;
        _style.Frame(p, t, phase);
    }

    // ---------------------------------------------------------------- the shared way of breaking

    /// <summary>Splits the picture into pieces, gives each the moment it comes away and each edge cell the moment its crack shows.</summary>
    internal DeathModel Model(Func<DeathCell, int> split, Func<DeathPiece, double> order, double from, double to, (double X, double Y) hit,
        double crackFrom = 0.5, double crackTo = 1.15)
    {
        var byId = new Dictionary<int, DeathPiece>();
        foreach (var q in Body)
        {
            var id = split(q);
            if (!byId.TryGetValue(id, out var piece)) byId[id] = piece = new DeathPiece { Id = byId.Count };
            piece.Cells.Add(q);
            q.Piece = piece.Id;
        }

        var pieces = byId.Values.ToArray();
        foreach (var piece in pieces)
        {
            piece.Cx = piece.Cells.Average(q => q.Dx);
            piece.Cy = piece.Cells.Average(q => q.Dy);
            foreach (var q in piece.Cells) piece.Set.Add(DeathPiece.Key(q.Dx, q.Dy));
            piece.Face = piece.Cells.Any(q => q.Eye);
        }

        if (!pieces.Any(pc => pc.Face) && pieces.Length > 0) pieces.MinBy(pc => pc.Cy)!.Face = true;

        var ranked = pieces.OrderBy(order).ToArray();
        for (var i = 0; i < ranked.Length; i++) ranked[i].Leave = from + (i / (double)Math.Max(1, ranked.Length - 1) * (to - from));

        var hx = Left + ((Right - Left) * (0.5 + hit.X));
        var hy = Top + ((Bottom - Top) * (0.5 + hit.Y));
        var pieceAt = new Dictionary<int, int>();
        foreach (var q in Body) pieceAt[DeathPiece.Key(q.Dx, q.Dy)] = q.Piece;
        var far = Body.Count == 0 ? 1 : Body.Max(q => Math.Sqrt(((q.Dx - hx) * (q.Dx - hx)) + ((q.Dy - hy) * (q.Dy - hy))));
        var model = new DeathModel { Pieces = pieces, Hx = hx, Hy = hy };
        foreach (var q in Body)
        {
            var edge = Round4.Any(o => pieceAt.TryGetValue(DeathPiece.Key(q.Dx + o.X, q.Dy + o.Y), out var other) && other != q.Piece);
            if (edge) model.Cracks[q] = crackFrom + (Math.Sqrt(((q.Dx - hx) * (q.Dx - hx)) + ((q.Dy - hy) * (q.Dy - hy))) / Math.Max(1, far) * (crackTo - crackFrom));
        }

        return model;
    }

    internal Func<DeathCell, int> Voronoi(IReadOnlyList<(double X, double Y)> seeds) => q =>
    {
        var best = 0;
        var bd = double.MaxValue;
        for (var i = 0; i < seeds.Count; i++)
        {
            var d = ((q.Dx - seeds[i].X) * (q.Dx - seeds[i].X)) + ((q.Dy - seeds[i].Y) * (q.Dy - seeds[i].Y));
            if (d < bd) { bd = d; best = i; }
        }

        return best;
    };

    /// <summary>Pieces of about <paramref name="step"/> cells, from a jittered grid of seeds.</summary>
    internal Func<DeathCell, int> Grid(int step, int salt)
    {
        var seeds = new List<(double, double)>();
        for (var y = Top - step; y <= Bottom + step; y += step)
        {
            for (var x = Left - step; x <= Right + step; x += step)
            {
                seeds.Add((x + ((Rnd((x * 7) + salt, y) - 0.5) * step * 0.8), y + ((Rnd((y * 5) + salt, x) - 0.5) * step * 0.8)));
            }
        }

        return Voronoi(seeds);
    }

    /// <summary>Shards like broken glass: seeds in rings round the middle.</summary>
    internal Func<DeathCell, int> Shards(int salt)
    {
        var seeds = new List<(double, double)>();
        for (var ring = 0; ring < 3; ring++)
        {
            var n = 9 + (ring * 3);
            for (var a = 0; a < n; a++)
            {
                var ang = (a + Rnd(a, ring + salt)) / n * 2 * Math.PI;
                var r = 3 + (ring * 7) + (Rnd(ring, a) * 3);
                seeds.Add((Math.Cos(ang) * r, Math.Sin(ang) * r * 0.8));
            }
        }

        return Voronoi(seeds);
    }

    /// <summary>
    /// What every death shares, at a moment of its own clock: the glow of its light behind it, the ripple when it breaks, its writing going grey
    /// and coming apart from left to right, its pieces in place with the impact (the flash, the jerk) and their cracks, the inner light where
    /// pieces have gone, and the pieces that came away, outlined, where the death moves them, dissolving cell by cell into its particles.
    /// </summary>
    internal void Engine(CapSheet p, DeathModel m, DeathStyle s, double t)
    {
        // its own light behind it, soft at the hit and steady while it breaks
        var glowK = t < 0.5 ? Math.Sin(Math.PI * t / 0.5) : 0.4 * (1 - Clamp01((t - s.EndAt) / 0.6));
        if (glowK > 0)
        {
            for (var dy = -22; dy <= 22; dy++)
            {
                for (var dx = -26; dx <= 26; dx++)
                {
                    var d = Math.Sqrt(((dx / 26.0) * (dx / 26.0)) + ((dy / 22.0) * (dy / 22.0)));
                    if (d < 1 && ((dx + dy) & 1) == 0 && X + dx < TextLeft) Put(p, X + dx, Y + dy, s.Glow, 85 * glowK * (1 - d));
                }
            }
        }

        // its writing: it shakes with the hit, goes grey and comes apart from left to right into the same particles as the body
        var shakeX = t < 0.5 ? R(Math.Sin(t * 38) * Math.Exp(-t * 5) * 1.5) : 0;
        foreach (var q in Info)
        {
            var gone = s.InfoFrom + ((q.X - InfoX0) / (double)Math.Max(1, InfoX1 - InfoX0) * 0.75) + (q.H * 0.25);
            if (t >= gone)
            {
                s.Particle(p, q, q.X, q.Y, t - gone, null);
                continue;
            }

            var colour = s.Info(q, t) ?? CapSheet.Mix(t > gone - 0.25 ? CapSheet.Mix(q.C, s.Glow, 0.5) : q.C, 0x7C808Cu, Ease((t - 0.2) / 0.5) * 0.65);
            p.Set(q.X + shakeX, q.Y, colour);
        }

        // the ripple when the first piece goes
        if (m.Pieces.Length > 0)
        {
            var rip = t - m.Pieces.Min(pc => pc.Leave);
            if (rip > 0 && rip < 0.8)
            {
                var r = 6 + (rip * 34);
                for (var a = 0; a < 90; a++)
                {
                    var ang = a / 90.0 * 2 * Math.PI;
                    int x = R(X + (Math.Cos(ang) * r)), y = R(Y + (Math.Sin(ang) * r * 0.75));
                    if (x >= Layout.Main.Left && x < TextLeft && y > Layout.SurfaceY && p.Solid(x, y)) p.Set(x, y, CapSheet.Mix(p.At(x, y), 0xFFFFFFu, 0.35), (int)(200 * (1 - (rip / 0.8))));
                }
            }
        }

        var flash = t < 0.08 || (t > 0.16 && t < 0.22);
        var jerk = t < 0.5 ? Math.Sin(t * 38) * Math.Exp(-t * 5) : 0;
        double sx = 1 - (jerk * 0.08), sy = 1 + (jerk * 0.14);
        var standing = m.Pieces.Count(pc => t < pc.Leave);
        var holding = s.HoldOn;

        // the pieces still in place, with the colour the death gives them and their cracks; the inner light where pieces have gone
        foreach (var piece in m.Pieces)
        {
            if (t >= piece.Leave)
            {
                var k = standing > 0 ? 1 - Clamp01((t - piece.Leave) / 1.0) : 0;
                if (k > 0)
                {
                    foreach (var q in piece.Cells) Put(p, X + (q.Dx * sx), Y + (q.Dy * sy), s.Hole(t, q), 160 * k * (0.75 + (0.25 * Math.Sin((t * 18) + q.Dx))));
                }

                continue;
            }

            var shake = piece == holding && t > piece.Leave - 0.45 ? R(Math.Sin(t * 60)) : 0;
            foreach (var q in piece.Cells)
            {
                var x = R(X + (q.Dx * sx) + (t < 0.5 ? jerk * 1.5 : 0)) + shake;
                var y = R(Y + (q.Dy * sy));
                if (flash)
                {
                    p.Set(x, y, 0xFFFFFFu);
                    continue;
                }

                p.Set(x, y, s.Body(q, t));
                if (m.Cracks.TryGetValue(q, out var c0) && t >= c0)
                {
                    var age = t - c0;
                    p.Set(x, y, s.Crack(age, q, t));
                    if (age > 0.22)
                    {
                        foreach (var (ox, oy) in Round4)
                        {
                            if (piece.Has(q.Dx + ox, q.Dy + oy)) Put(p, x + ox, y + oy, s.CrackRim, 100 * Clamp01((age - 0.22) / 0.35));
                        }
                    }

                    s.FromCrack(p, x, y, age, q, t);
                }
            }
        }

        // the pieces that came away
        foreach (var piece in m.Pieces)
        {
            if (t < piece.Leave) continue;
            var since = t - piece.Leave;
            var (mx, my, flip) = s.Motion(piece, since);
            double ox = mx - piece.Cx, oy = my - piece.Cy;
            (int, int) Place(DeathCell q) => (R(X + (flip ? (2 * piece.Cx) - q.Dx : q.Dx) + ox), R(Y + q.Dy + oy));
            double Gone(DeathCell q) => piece.Leave + s.DissolveFrom + (q.H * s.DissolveSpread);

            foreach (var q in piece.Cells)
            {
                var (x, y) = Place(q);
                if (t >= Gone(q))
                {
                    s.Particle(p, q, x, y, t - Gone(q), piece);
                    continue;
                }

                foreach (var (dx, dy) in Round4)
                {
                    if (!piece.Has(q.Dx + (flip ? -dx : dx), q.Dy + dy)) p.Set(x + dx, y + dy, 0x0C0E16u, 215);
                }
            }

            foreach (var q in piece.Cells)
            {
                if (t >= Gone(q)) continue;
                var (x, y) = Place(q);
                p.Set(x, y, s.Piece(q, since, t, piece));
            }

            s.AfterPiece(p, piece, since, t);
        }
    }
}
