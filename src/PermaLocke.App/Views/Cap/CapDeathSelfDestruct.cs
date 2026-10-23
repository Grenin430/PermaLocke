using static PermaLocke.App.Views.CapDeath;

namespace PermaLocke.App.Views;

/// <summary>
/// Self-Destruct (2026-10-22, planned in the brain on 2026-10-20 as «epic, spectacular and chaotic»): the fallen one lights up from inside, trembles
/// and swells while sparks gather into it, draws in and bursts: a white flash, a fireball, a shockwave that crosses both windows and shoves the rest
/// of the party aside, its charred pieces and its writing blown out; a mushroom of smoke rises and rubble rains. In the abyss's window the wave
/// arrives from the right, lifts the sea into a wall of water that runs across it, and the rubble falls into the sea.
/// </summary>
internal sealed class SelfDestructDeath : DeathStyle
{
    /// <summary>The moment it bursts, from the start of the death.</summary>
    public const double Blast = 1.7;

    /// <summary>How fast the shockwave runs, in cells a second.</summary>
    private const double RingSpeed = 150;

    private const uint FireWhite = 0xFFFCEA, FireYellow = 0xFFE466, FireOrange = 0xFF9A30, FireRed = 0xE04A1C, FireCrimson = 0x8A2014;
    private const uint SmokeDark = 0x3A3034, SmokeMid = 0x6A5E5C, SmokeLight = 0x9A8E86, SmokeTop = 0xC4B8AE, SmokeInk = 0x221C22;
    private const uint Char = 0x2A1E1C, Ember = 0xFF8A30, Ash = 0x7A7270;

    private readonly (double Vx, double Vy)[] _flight;
    private readonly HashSet<int> _cells = [];
    private readonly double _reach;

    public SelfDestructDeath(CapDeathScene s) : base(s)
    {
        // its pieces for the burst: small chunks, each thrown straight out from the middle, a little upwards, faster the nearer the middle
        M = S.Model(S.Grid(4, 131), p => Rnd(p.Id, 7), 0, 0.05, (0, 0));
        _flight = new (double, double)[M.Pieces.Length];
        foreach (var piece in M.Pieces)
        {
            var a = Math.Atan2(piece.Cy == 0 ? 0.01 : piece.Cy, piece.Cx == 0 ? 0.01 : piece.Cx) + ((Rnd(piece.Id, 3) - 0.5) * 0.5);
            var v = 55 + (Rnd(piece.Id, 4) * 70);
            _flight[piece.Id] = (Math.Cos(a) * v, (Math.Sin(a) * v * 0.85) - 35);
        }

        foreach (var q in S.Body) _cells.Add(DeathPiece.Key(q.Dx, q.Dy));
        _reach = Math.Max(6, S.Body.Count == 0 ? 6 : S.Body.Max(q => Math.Sqrt((q.Dx * q.Dx) + (q.Dy * q.Dy))));
    }

    public override uint Glow => FireOrange;

    public override (double X, double Y, bool Flip) Motion(DeathPiece p, double s) => (p.Cx, p.Cy, false);

    public override void Particle(CapSheet p, DeathCell q, double x, double y, double age, DeathPiece? piece) { }

    /// <summary>
    /// The rest of the party: still until the shockwave reaches it, then thrown to the left and a little away from the blast, and springing back
    /// with a wobble that dies out.
    /// </summary>
    public override (int X, int Y) Push(CapCreature c, double t)
    {
        var te = t - Blast;
        var (cx, cy) = c.Centre;
        var d = Math.Sqrt(((cx - S.X) * (cx - S.X)) + ((cy - S.Y) * (cy - S.Y)));
        var u = te - (Math.Max(0, d - 8) / RingSpeed);
        if (u <= 0) return (0, 0);
        var strength = 12 * Math.Clamp(60 / Math.Max(20, d), 0.45, 1.4);
        var swing = Math.Exp(-u * 2.6) * Math.Cos(u * 7.5);
        return (-R(strength * swing), R(Math.Sign(cy - S.Y) * 3 * Math.Exp(-u * 3) * Math.Cos(u * 6)));
    }

    public override void Frame(CapSheet p, double t, double phase)
    {
        if (t < Blast)
        {
            Charge(p, t);
            return;
        }

        var te = t - Blast;
        var l = S.Layout;
        Shockwave(p, S.X, S.Y, te, l.Main.Left, l.Main.Right + 1);
        Shockwave(p, S.X, S.Y, te - 0.12, l.Main.Left, l.Main.Right + 1, 0.45);

        // the sea over it lifted into a mound that splits into two waves, one running to each side, once the shockwave reaches the surface
        var up = te - (Math.Max(0, S.Y - l.SurfaceY) / RingSpeed);
        Waves(p, l.Main.Left, l.Main.Right, l.SurfaceY, S.X - (Math.Max(0, up) * 72), -1, S.X + (Math.Max(0, up) * 72), 1, 34, up);
        Bubbles(p, te);
        Mushroom(p, te);
        Fireball(p, te);
        Pop(p, te);
        Debris(p, te);
        Writing(p, te);
        Rubble(p, te, l.Main.Left, l.Main.Right, p.H, 0);
        Shake(p, te, 4, 0.9);
    }

    // ---------------------------------------------------------------- the charge

    /// <summary>
    /// It lights up from its middle outwards, beating faster and faster, trembles harder, swells; cracks of light open across it and sparks are
    /// drawn into it from all round. At the last instant it draws in and goes white.
    /// </summary>
    private void Charge(CapSheet p, double t)
    {
        var k = t / Blast;
        var swell = 1 + (0.32 * Ease(k)) - (t > 1.5 ? 0.3 * Ease((t - 1.5) / 0.2) : 0);
        var amp = 0.3 + (2.4 * k * k);
        var tick = (int)(t * 30);
        var jx = (Rnd(tick, 1) - 0.5) * 2 * amp;
        var jy = (Rnd(tick, 2) - 0.5) * 2 * amp * 0.6;
        var beat = 0.5 + (0.5 * Math.Sin(t * t * 16));
        var heat = t > 1.5 ? 1 : Clamp01(k * 1.15) * (0.5 + (0.5 * beat));

        // a light round it, in rings through the dither, growing with the heat
        var halo = 10 + (k * 16);
        for (var dy = -(int)halo; dy <= halo; dy++)
        {
            for (var dx = -(int)halo; dx <= halo; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy)) / halo;
                if (d >= 1) continue;
                int x = S.X + dx, y = S.Y + dy;
                if (CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] >= (1 - d) * heat * 9) continue;
                p.Set(x, y, d < 0.5 ? FireYellow : FireOrange, 150);
            }
        }

        // sparks drawn into it
        if (t > 0.25)
        {
            for (var n = 0; n < 18; n++)
            {
                var life = ((t * 1.8) + Rnd(n, 11)) % 1;
                var a = (n / 18.0 * 2 * Math.PI) + (Rnd(n, 12) * 0.4);
                var r = 4 + ((1 - life) * (26 + (Rnd(n, 13) * 10)));
                for (var tail = 0; tail < 3; tail++)
                {
                    var rr = r + (tail * 2.2);
                    Put(p, S.X + (Math.Cos(a) * rr), S.Y + (Math.Sin(a) * rr * 0.85), tail == 0 ? FireWhite : tail == 1 ? FireYellow : FireOrange, 255 - (tail * 70));
                }
            }
        }

        // the picture, swollen, trembling and heated from its middle; cells doubled where it has grown so it never gets holes
        var block = swell > 1.12 ? 2 : 1;
        foreach (var q in S.Body)
        {
            var d = Math.Sqrt((q.Dx * q.Dx) + (q.Dy * q.Dy)) / _reach;
            var fire = d < 0.3 ? FireWhite : d < 0.55 ? FireYellow : d < 0.8 ? FireOrange : FireRed;
            var c = t > 1.5 ? CapSheet.Mix(fire, FireWhite, Ease((t - 1.5) / 0.15)) : Screen(q.C, fire, heat * (1 - (0.4 * d)));
            int x = R(S.X + (q.Dx * swell) + jx), y = R(S.Y + (q.Dy * swell) + jy);
            for (var by = 0; by < block; by++)
            {
                for (var bx = 0; bx < block; bx++) p.Set(x + bx, y + by, c);
            }
        }

        // cracks of light from its middle, opening one after another, only over the picture
        for (var c = 0; c < 8; c++)
        {
            var open = Clamp01((t - 0.7 - (c * 0.08)) / 0.5);
            if (open <= 0) continue;
            var a = (c / 8.0 * 2 * Math.PI) + 0.35 + (Rnd(c, 21) * 0.4);
            var bend = (Rnd(c, 22) - 0.5) * 0.6;
            for (var r = 0.0; r < _reach * open; r += 0.5)
            {
                var ang = a + (bend * r / _reach);
                double lx = Math.Cos(ang) * r, ly = Math.Sin(ang) * r;
                if (!_cells.Contains(DeathPiece.Key(R(lx), R(ly)))) continue;
                Put(p, S.X + (lx * swell) + jx, S.Y + (ly * swell) + jy, r < _reach * 0.5 ? FireWhite : FireYellow);
            }
        }

        // its writing trembles with it and warms
        foreach (var q in S.Info) p.Set(q.X + R(jx * 0.6), q.Y, CapSheet.Mix(q.C, FireOrange, heat * 0.45));
    }

    // ---------------------------------------------------------------- the burst

    /// <summary>
    /// The instant of the burst, only round it (no flash over the window): a white core that swells and a yellow ring round it, and the water
    /// near it lit in bands through the dither, fading in a quarter of a second.
    /// </summary>
    private void Pop(CapSheet p, double te)
    {
        if (te > 0.3) return;
        var k = te / 0.3;
        var light = 38 * (0.6 + (0.4 * k));
        for (var dy = -(int)light; dy <= light; dy++)
        {
            for (var dx = -(int)light; dx <= light; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy)) / light;
                if (d >= 1) continue;
                int x = S.X + dx, y = S.Y + dy;
                if (!p.Solid(x, y) || CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] >= (1 - d) * (1 - k) * 12) continue;
                p.Set(x, y, Screen(p.At(x, y), FireYellow, 0.55 * (1 - k)));
            }
        }

        var core = 5 + (k * 9);
        for (var dy = -(int)core - 2; dy <= core + 2; dy++)
        {
            for (var dx = -(int)core - 2; dx <= core + 2; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d <= core * (1 - (k * 0.6))) p.Set(S.X + dx, S.Y + dy, FireWhite);
                else if (d <= core && k < 0.8) p.Set(S.X + dx, S.Y + dy, FireYellow);
            }
        }

        // rays of the burst, short and straight, only in its first moment
        if (te < 0.18)
        {
            for (var i = 0; i < 12; i++)
            {
                var a = (i / 12.0 * 2 * Math.PI) + (Rnd(i, 71) * 0.3);
                for (var r = core; r < core + 6 + (te * 120); r += 0.7)
                {
                    Put(p, S.X + (Math.Cos(a) * r), S.Y + (Math.Sin(a) * r), r < core + 8 ? FireWhite : FireYellow, 230 * (1 - (te / 0.18)));
                }
            }
        }
    }

    /// <summary>
    /// The shockwave: a ring that runs out from the blast, a white crest with the water behind it lit and bent (the cells just inside it taken
    /// from further in), fading as it goes.
    /// </summary>
    private static void Shockwave(CapSheet p, double bx, double by, double te, int left, int right, double strength = 1)
    {
        if (te < 0) return;
        var r = 6 + (te * RingSpeed);
        var fade = (1 - Clamp01(te / 2.4)) * strength;
        if (fade <= 0) return;
        var steps = (int)(r * 7) + 16;
        for (var i = 0; i < steps; i++)
        {
            var a = i / (double)steps * 2 * Math.PI;
            double ca = Math.Cos(a), sa = Math.Sin(a);
            var cx = bx + (ca * r);
            if (cx < left - 2 || cx > right + 2) continue;

            // the bend: the band inside the crest shows what is a little further in, lit
            for (var w = 1; w <= 4; w++)
            {
                int x = R(bx + (ca * (r - w))), y = R(by + (sa * (r - w)));
                int sx = R(bx + (ca * (r - w - 3))), sy = R(by + (sa * (r - w - 3)));
                if (!p.Solid(sx, sy) || x < left || x >= right) continue;
                p.Set(x, y, CapSheet.Mix(p.At(sx, sy), 0xFFFFFFu, 0.25 * fade));
            }

            Put(p, cx, by + (sa * r), 0xFFFFFFu, 230 * fade);
            Put(p, bx + (ca * (r + 1)), by + (sa * (r + 1)), 0xFFF0D0u, 120 * fade);
        }
    }

    /// <summary>
    /// The fireball: a ball of fire with a churning edge, in bands from white to crimson through the dither, that cools from the inside out
    /// into smoke and comes apart.
    /// </summary>
    private void Fireball(CapSheet p, double te)
    {
        if (te > 1.7) return;
        var radius = (30 * EaseOut(te / 0.45)) + (te * 4);
        var cool = Clamp01((te - 0.2) / 1.2);
        var gone = Clamp01((te - 1.1) / 0.6);
        var reach = (int)(radius * 1.25) + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                var a = Math.Atan2(dy, dx);
                var edge = radius * (1 + (0.13 * Math.Sin((a * 7) + (te * 9))) + (0.07 * Math.Sin((a * 13) - (te * 6))) + (0.05 * Math.Sin((a * 3) + (te * 4))));
                var d = r / Math.Max(0.5, edge);
                if (d > 1) continue;
                int x = S.X + dx, y = S.Y + dy;
                var bayer = CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] / 16.0;
                if (bayer < gone) continue;
                var v = d + (cool * 0.9) + (bayer * 0.1) + ((Noise((dx * 0.3) + (te * 3), (dy * 0.3) - (te * 4)) - 0.5) * 0.35);
                uint c = v < 0.3 ? FireWhite : v < 0.5 ? FireYellow : v < 0.68 ? FireOrange : v < 0.84 ? FireRed : v < 1.0 ? FireCrimson : v < 1.3 ? SmokeDark : SmokeMid;
                if (v > 1.6) continue;
                p.Set(x, y, c);
            }
        }
    }

    /// <summary>Its pieces, charred, thrown out of the fireball and falling, each with a trail of smoke; they glow at first and turn to ash.</summary>
    private void Debris(CapSheet p, double te)
    {
        if (te > 2.6) return;
        foreach (var piece in M.Pieces)
        {
            var (vx, vy) = _flight[piece.Id];
            double ox = vx * te, oy = (vy * te) + (70 * te * te);
            var glow = 1 - Clamp01(te / 0.9);

            // embers shed behind it while it is hot
            if (glow > 0.2)
            {
                for (var e = 0; e < 3; e++)
                {
                    var back = Math.Max(0, te - (e * 0.05) - 0.03);
                    Put(p, S.X + piece.Cx + (vx * back) + ((Rnd(piece.Id, e + 90) - 0.5) * 3), S.Y + piece.Cy + (vy * back) + (70 * back * back) + (e * 1.5), e == 0 ? FireYellow : Ember, 255 * glow);
                }
            }

            // smoke behind it
            for (var s = 1; s <= 3; s++)
            {
                var back = Math.Max(0, te - (s * 0.06));
                Puff(p, S.X + piece.Cx + (vx * back), S.Y + piece.Cy + (vy * back) + (70 * back * back), 1.6 + (s * 0.5), s == 1 && glow > 0.3 ? FireOrange : SmokeMid, 150 - (s * 35));
            }

            foreach (var q in piece.Cells)
            {
                var goneAt = 1.3 + (q.H * 1.1);
                int x = R(S.X + q.Dx + ox), y = R(S.Y + q.Dy + oy);
                if (te >= goneAt)
                {
                    var age = te - goneAt;
                    if (age < 0.4) p.Set(x, y + R(age * 20), age < 0.2 ? Ember : Ash, (int)(220 * (1 - (age / 0.4))));
                    continue;
                }

                p.Set(x, y, CapSheet.Mix(CapSheet.Mix(q.C, Char, 0.7), FireOrange, glow * 0.8));
                foreach (var (dx, dy) in Round4)
                {
                    if (!piece.Has(q.Dx + dx, q.Dy + dy)) p.Set(x + dx, y + dy, SmokeInk, 200);
                }
            }
        }
    }

    /// <summary>Its writing blown away by the blast: every letter's cells flung out, white-hot, then embers, then ash, gone.</summary>
    private void Writing(CapSheet p, double te)
    {
        foreach (var q in S.Info)
        {
            var hit = Math.Sqrt(((q.X - S.X) * (q.X - S.X)) + ((q.Y - S.Y) * (q.Y - S.Y))) / RingSpeed;
            var u = te - hit;
            if (u <= 0)
            {
                p.Set(q.X, q.Y, CapSheet.Mix(q.C, FireOrange, 0.5));
                continue;
            }

            var goneAt = 0.5 + (q.H * 0.9);
            if (u >= goneAt) continue;
            double dx = q.X - S.X, dy = q.Y - S.Y;
            var l = Math.Max(1, Math.Sqrt((dx * dx) + (dy * dy)));
            var v = 30 + (900 / (l + 10)) + (q.H * 20);
            var x = q.X + (dx / l * v * u);
            var y = q.Y + (dy / l * v * u * 0.6) - (12 * u) + (40 * u * u);
            var c = u < 0.15 ? FireWhite : u < goneAt * 0.6 ? Ember : Ash;
            Put(p, x, y, c);
        }
    }

    // the billows of the mushroom, made again each frame into these (no list per frame)
    private const int MaxBillows = 48;
    private readonly double[] _bx = new double[MaxBillows], _by = new double[MaxBillows], _br = new double[MaxBillows], _bz = new double[MaxBillows], _bh = new double[MaxBillows];
    private int _billows, _skirtFrom, _skirtTo;
    private uint[] _smoke = [];
    private byte[] _state = [];

    private void AddBillow(double x, double y, double r, double z, double heat)
    {
        if (_billows >= MaxBillows || r < 0.8) return;
        _bx[_billows] = x;
        _by[_billows] = y;
        _br[_billows] = r;
        _bz[_billows] = z;
        _bh[_billows] = heat;
        _billows++;
    }

    /// <summary>
    /// The mushroom of smoke: billows that melt into one mass, as smoke is drawn by hand. Each billow is a lump lit as a ball (from the upper
    /// left, five tones, a little dither between them), the ones in front over the ones behind so their edges make the soft crescents of shadow
    /// between lumps; the outline goes only round the whole. The cap is a ring of billows rolling round (its underside lit by the fire while
    /// it lasts) under a dome; the stem a stack of them twisting, flared at its foot and under the cap; a skirt of dust at its foot. It rises
    /// and spreads, and frays away billow by billow, from their edges in.
    /// </summary>
    private void Mushroom(CapSheet p, double te)
    {
        if (te < 0.2) return;
        var rise = EaseOut((te - 0.2) / 2.0);
        var drift = Math.Max(0, te - 2.2);
        var top = S.Layout.SurfaceY + 4;
        var baseY = S.Y + 3.0;
        var capY = S.Y - 12 - (rise * Math.Max(20, Math.Min(60, S.Y - top - 8))) - (drift * 3);
        var capW = 7 + (rise * 15) + (drift * 2.2);
        var capH = capW * 0.55;
        var heat = 1 - Clamp01((te - 0.3) / 2.0);
        var fade = Clamp01((te - 2.6) / 1.6);
        if (fade >= 1) return;
        var roll = te * 0.7;

        _billows = 0;

        // the skirt, low and in front, shrinking away first
        _skirtFrom = _billows;
        var reach = 4 + (rise * 24) + (drift * 4);
        for (var i = 0; i < 12; i++)
        {
            var side = i % 2 == 0 ? -1 : 1;
            var k = ((i / 2) + 0.5) / 6.0;
            AddBillow(S.X + (side * reach * k), baseY + 1.5 - (Rnd(i, 41) * 1.5) - ((1 - k) * 1.5), 3.4 + (Rnd(i, 42) * 1.6) - (k * 1.2), 4 - k, heat * 0.5 * (1 - k));
        }

        _skirtTo = _billows;
        var skirtGone = Clamp01((te - 1.8) / 1.4);

        // the stem, from its foot to under the cap
        for (var i = 0; i < 9; i++)
        {
            var k = i / 8.0;
            var y = baseY - (k * (baseY - (capY + (capH * 0.45))));
            var x = S.X + (Math.Sin((k * 5) + (te * 2)) * 1.4);
            var r = 4.2 - (k * 1.2) + (2.6 * Math.Pow(1 - k, 4)) + (3 * Math.Pow(k, 6)) + (Math.Sin((i * 1.9) + (te * 3)) * 0.4);
            AddBillow(x, y, r, 1, heat * (1 - (k * 0.7)));
        }

        // the big billow filling the cap, behind the rest
        AddBillow(S.X, capY + (capH * 0.05), capH * 0.95, -capW * 0.4, heat * 0.4);

        // the ring of the cap rolling round: z from its angle, so the front ones cover the back ones
        for (var i = 0; i < 16; i++)
        {
            var a = (i / 16.0 * 2 * Math.PI) + roll;
            var r = capH * 0.5 * (0.8 + (Rnd(i, 43) * 0.45));
            AddBillow(S.X + (Math.Cos(a) * capW * 0.82), capY + (capH * 0.32) + (Math.Sin(a) * capH * 0.32), r, Math.Sin(a) * capW * 0.55, heat * 0.9);
        }

        // small lumps over the rim of the cap and its top, so its edge boils instead of being one smooth roll
        for (var i = 0; i < 14; i++)
        {
            var a = (i / 14.0 * 2 * Math.PI) - (roll * 0.8) + (Rnd(i, 54) * 0.4);
            var r = capH * (0.18 + (Rnd(i, 55) * 0.13));
            var ex = Math.Cos(a) * capW * (0.98 + (Rnd(i, 56) * 0.12));
            var ey = Math.Sin(a) * capH * (Math.Sin(a) < 0 ? 0.8 : 0.5);
            AddBillow(S.X + ex, capY + (capH * 0.1) + ey, r, (Math.Sin(a) * capW * 0.55) + 1.5, Math.Sin(a) > 0 ? heat : 0);
        }

        for (var i = 0; i < 6; i++)
        {
            var k = (i + 0.5) / 6.0;
            var y = baseY - (k * (baseY - (capY + (capH * 0.45))));
            AddBillow(S.X + ((i % 2 == 0 ? -1 : 1) * (3.2 - k)) + (Math.Sin((k * 5) + (te * 2)) * 1.4), y, 1.6 + (Rnd(i, 57) * 0.8), 2, heat * (1 - k));
        }

        // the dome on top, bobbing
        for (var j = 0; j < 7; j++)
        {
            var off = (j - 3) / 3.0;
            var r = capH * 0.58 * (0.85 + (Rnd(j, 53) * 0.3));
            AddBillow(S.X + (off * capW * 0.62), capY - (capH * 0.12) - ((1 - Math.Abs(off)) * capH * 0.38) + (Math.Sin(roll * 2 + j) * 0.7), r, -Math.Abs(off) * 2, 0);
        }

        // The mass, cell by cell. The cells are looked up through a warp of noise (two sizes, drifting with the roll) before the billows, so no
        // billow is a circle: their edges wander, swell and tuck in; the billow in front at the warped cell lights it as a ball, with a little
        // noise in the light. Round the mass a fringe: cells just outside it, drawn sparse through the dither, so its edge frays into wisps.
        int x0 = (int)(S.X - Math.Max(capW * 1.5, 34)) - 6, x1 = (int)(S.X + Math.Max(capW * 1.5, 34)) + 6;
        int y0 = (int)(capY - (capH * 1.4)) - 6, y1 = (int)baseY + 8;
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        if (_smoke.Length < w * h)
        {
            _smoke = new uint[w * h];
            _state = new byte[w * h];
        }

        const double Lx = -0.55, Ly = -0.65, Lz = 0.52, Fringe = 1.25;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var at = ((y - y0) * w) + (x - x0);
                _state[at] = 0;
                var wx = x + 0.5 + ((Noise(x * 0.13, (y * 0.13) - (roll * 0.8)) - 0.5) * 7) + ((Noise((x * 0.37) + 3, (y * 0.37) + 3 - (roll * 1.5)) - 0.5) * 2.6);
                var wy = y + 0.5 + ((Noise((x * 0.13) + 11, (y * 0.13) + 5 - (roll * 0.8)) - 0.5) * 7) + ((Noise((x * 0.37) + 17, (y * 0.37) + 9 - (roll * 1.5)) - 0.5) * 2.6);
                var best = double.NegativeInfinity;
                var pick = -1;
                var near = double.MaxValue;
                double nx = 0, ny = 0, nz = 0;
                for (var b = 0; b < _billows; b++)
                {
                    double dx = wx - _bx[b], dy = wy - _by[b], r = _br[b];
                    var d2 = (dx * dx) + (dy * dy);
                    var rel = d2 / (r * r);
                    if (rel < near) near = rel;
                    if (rel >= 1) continue;
                    var hh = Math.Sqrt((r * r) - d2);
                    if (_bz[b] + hh <= best) continue;
                    best = _bz[b] + hh;
                    pick = b;
                    nx = dx / r;
                    ny = dy / r;
                    nz = hh / r;
                }

                var bayer = CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] / 16.0;
                if (pick < 0)
                {
                    // the fringe: wisps just outside, sparser further out
                    var out_ = Math.Sqrt(near);
                    if (out_ < Fringe && fade < 0.9 && bayer < ((Fringe - out_) / (Fringe - 1) * 0.55) - (fade * 0.6))
                    {
                        _state[at] = 2;
                        _smoke[at] = Noise(x * 0.3, (y * 0.3) + roll) > 0.5 ? 0x8A7C76u : 0x645858u;
                    }

                    continue;
                }

                var gone = pick >= _skirtFrom && pick < _skirtTo ? Math.Max(fade, skirtGone) : fade;
                // it thins in patches (noise), from the edges of the billows in, not in an even checker
                if (gone > 0 && (bayer * 0.35) + (Noise((x * 0.18) + 60, (y * 0.18) - roll) * 0.75) < (gone * 1.5) - (nz * 0.4)) continue;
                _state[at] = 1;

                var lam = (nx * Lx) + (ny * Ly) + (nz * Lz) + ((bayer - 0.5) * 0.14) + ((Noise((x * 0.22) + 40, (y * 0.22) + roll) - 0.5) * 0.35);
                uint c = lam > 0.72 ? 0xC4B6ACu : lam > 0.45 ? 0x9A8C84u : lam > 0.16 ? 0x726464u : lam > -0.15 ? 0x4E4242u : 0x30262Au;
                if (_bh[pick] > 0.05 && ny > 0.2)
                {
                    var glow = _bh[pick] * Clamp01((ny - 0.2) / 0.6);
                    c = CapSheet.Mix(c, glow > 0.5 && lam > 0 ? FireYellow : FireOrange, Math.Min(0.8, glow));
                }

                _smoke[at] = c;
            }
        }

        // no black outline: only a dark edge where the mass turns away from the light (its underside and right), the lit edges left soft
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var at = ((y - y0) * w) + (x - x0);
                var st = _state[at];
                if (st == 0) continue;
                if (st == 2)
                {
                    p.Set(x, y, _smoke[at], 200);
                    continue;
                }

                var shade = (y < y1 && _state[at + w] != 1) || (x < x1 && _state[at + 1] != 1);
                p.Set(x, y, shade ? 0x30262Au : _smoke[at]);
            }
        }
    }

    /// <summary>Smooth value noise over the plane, 0 to 1.</summary>
    private static double Noise(double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - (2 * fx));
        fy = fy * fy * (3 - (2 * fy));
        double a = Rnd(ix, iy), b = Rnd(ix + 1, iy), c = Rnd(ix, iy + 1), d = Rnd(ix + 1, iy + 1);
        return (a + ((b - a) * fx)) + (((c + ((d - c) * fx)) - (a + ((b - a) * fx))) * fy);
    }

    /// <summary>
    /// Bubbles the blast throws up through the water: of one to three cells, outlined in pale light with a glint, wobbling as they rise, faster
    /// the bigger, and bursting at the surface.
    /// </summary>
    private void Bubbles(CapSheet p, double te)
    {
        if (te < 0.15) return;
        var surface = S.Layout.SurfaceY;
        for (var i = 0; i < 34; i++)
        {
            var born = 0.15 + (Rnd(i, 81) * 1.6);
            var u = te - born;
            if (u < 0) continue;
            var size = i % 5 == 0 ? 3 : i % 2 == 0 ? 2 : 1;
            var x = S.X + ((Rnd(i, 82) - 0.5) * 40) + (Math.Sin((u * 5) + i) * 1.5);
            var y = S.Y + ((Rnd(i, 83) - 0.5) * 16) - (u * (16 + (size * 9))) - (u * u * 6);
            if (y < surface + 1)
            {
                if (y > surface - 3) Put(p, x, surface, 0xFFFFFFu);
                continue;
            }

            int ix = R(x), iy = R(y);
            if (size == 1)
            {
                p.Set(ix, iy, 0xCFF4FFu, 220);
                continue;
            }

            for (var dy = -size; dy <= size; dy++)
            {
                for (var dx = -size; dx <= size; dx++)
                {
                    var dd = Math.Sqrt((dx * dx) + (dy * dy));
                    if (dd > size + 0.3 || dd < size - 0.7) continue;
                    p.Set(ix + dx, iy + dy, dx + dy < 0 ? 0xE6FAFFu : 0x9CD8F0u, 230);
                }
            }

            p.Set(ix - 1, iy - 1, 0xFFFFFFu);
        }
    }

    /// <summary>
    /// Rubble raining after the blast: rocks of two or three cells lit on top and embers with a short trail, falling across the window; into the sea
    /// at <paramref name="sea"/> (above 0) they end in a small splash.
    /// </summary>
    private static void Rubble(CapSheet p, double te, int left, int right, int bottom, int sea)
    {
        for (var i = 0; i < 44; i++)
        {
            var born = 0.5 + (i * 0.075);
            var u = te - born;
            if (u < 0) continue;
            var x = left + 4 + (Rnd(i, 51) * (right - left - 8)) - (u * 6);
            var y = -4 + ((30 + (Rnd(i, 52) * 30)) * u) + (55 * u * u);
            var stop = sea > 0 ? sea : bottom;
            if (y > stop)
            {
                if (sea > 0)
                {
                    var after = (y - stop) / 60;
                    if (after < 0.25)
                    {
                        foreach (var (dx, dy) in (ReadOnlySpan<(int, int)>)[(-2, -1), (2, -1), (0, -2), (-1, -3), (1, -3)])
                        {
                            Put(p, x + (dx * (1 + (after * 6))), stop + dy - (after * 10), 0xFFFFFFu, 230 * (1 - (after / 0.25)));
                        }
                    }
                }

                continue;
            }

            if (i % 3 == 0)
            {
                var flicker = (int)(te * 20) % 2 == 0;
                Put(p, x, y, flicker ? FireYellow : Ember);
                Put(p, x + 0.4, y - 1, FireOrange, 180);
                Put(p, x + 0.8, y - 2, FireRed, 110);
            }
            else
            {
                // a rock: two or three cells a side, outlined, lit on its top left, tumbling (its shape turns every few frames)
                int ix = R(x), iy = R(y), size = i % 3 == 1 ? 3 : 2;
                var turn = ((int)(te * 8) + i) % 2;
                for (var dy = -1; dy <= size; dy++)
                {
                    for (var dx = -1; dx <= size; dx++)
                    {
                        var inside = dx >= 0 && dy >= 0 && dx < size && dy < size && !(size == 3 && turn == 1 && dx == 2 && dy == 0);
                        if (!inside)
                        {
                            if (dx >= -1 && dy >= -1 && dx <= size && dy <= size && !((dx == -1 || dx == size) && (dy == -1 || dy == size))) p.Set(ix + dx, iy + dy, SmokeInk);
                            continue;
                        }

                        p.Set(ix + dx, iy + dy, dx + dy == 0 ? SmokeTop : dx + dy < size ? SmokeLight : dx + dy < size + 1 ? SmokeMid : SmokeDark);
                    }
                }
            }
        }
    }

    private static double EaseOut(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);

    /// <summary>Light over a colour: <paramref name="light"/> at <paramref name="k"/> screened onto it, so it brightens and never goes muddy.</summary>
    private static uint Screen(uint c, uint light, double k)
    {
        int Ch(int a, int b) => 255 - (int)Math.Round((255 - a) * (255 - (b * Clamp01(k))) / 255.0);
        return CapSheet.Rgb(Ch(Red(c), Red(light)), Ch(Green(c), Green(light)), Ch(Blue(c), Blue(light)));
    }

    // ---------------------------------------------------------------- the abyss's window

    /// <summary>
    /// The burst seen from the abyss's window: a weaker flash, the shockwave arriving from the right, the sea lifted into a wall of water with foam
    /// and spray that runs across to the left, and the rubble falling into the sea.
    /// </summary>
    public static void Deep(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double t)
    {
        var te = t - Blast;
        if (te < 0) return;

        // the blast in this window's cells: off to the right, where the party's window is
        var bx = l.Main.Right + 45;
        var by = l.SurfaceY + 30;
        Shockwave(p, bx, by, te, l.Main.Left, l.Main.Right + 1);
        Shockwave(p, bx, by, te - 0.12, l.Main.Left, l.Main.Right + 1, 0.45);
        var tw = te - (Math.Max(0, bx - l.Main.Right) / RingSpeed) - 0.25;
        Waves(p, l.Main.Left, l.Main.Right, l.SurfaceY, l.Main.Right + 14 - (Math.Max(0, tw) * 70), -1, double.NaN, 1, 50, tw);
        Rubble(p, te, l.Main.Left, l.Main.Right, p.H, l.SurfaceY);
        Shake(p, te - 0.3, 2.5, 0.7);
    }

    /// <summary>
    /// Walls of water: the sea lifted where one or two crests run (each with its <c>Dir</c>, the side it runs to); where the two are together at
    /// the start they make one mound. Each has a concave front face and a long back. The water is the window's own, in five tones in wavy bands
    /// up the face, dark in the trough of the front and light where the crest lets the light through; lace of foam on the back; on top a crest
    /// of foam in clumps that spills down the front in streaks; a lip that curls forwards over the dark hollow of the tube; whitewater churning
    /// at the foot of the front; spray and mist over it.
    /// </summary>
    private static void Waves(CapSheet p, int left, int right, int surface, double crestA, int dirA, double crestB, int dirB, double tall, double tw)
    {
        if (tw <= 0 || tw > 4.4) return;
        var height = tall * Ease(Clamp01(tw / 0.45)) * (1 - (0.35 * Clamp01(tw / 3.4))) * (1 - Clamp01((tw - 3.5) / 0.9));
        if (height < 1.5) return;
        var deep = p.Solid(left + 2, surface + 30) ? p.At(left + 2, surface + 30) : 0x1E4C8Cu;
        var shallow = p.Solid(left + 2, surface + 3) ? p.At(left + 2, surface + 3) : 0x4C8CC8u;
        uint t0 = CapSheet.Mix(deep, 0x000000u, 0.3), t1 = deep, t2 = CapSheet.Mix(deep, shallow, 0.55), t3 = shallow, t4 = CapSheet.Mix(shallow, 0xC8F6FFu, 0.55);
        var hasB = !double.IsNaN(crestB);

        for (var x = left; x < right; x++)
        {
            var (ha, fa) = Lift(x, crestA, dirA, height, tw);
            var (hb, fb) = hasB ? Lift(x, crestB, dirB, height, tw) : (0, 0);
            var front = ha >= hb ? fa : fb;
            var nearCrest = Math.Exp(-(front / 12.0) * (front / 12.0));
            var h = Math.Max(ha, hb);
            if (h < 0.5) continue;
            var topY = surface - h;
            var foam = 0.8 + ((1.5 + (Noise((x * 0.4) + (tw * 4), 1.5) * 3)) * nearCrest);
            for (var y = (int)Math.Floor(topY); y <= surface + 1; y++)
            {
                var up = (surface - y) / Math.Max(1, height);
                var fromTop = y - topY;
                var bayer = CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)];
                var band = up + (0.05 * Math.Sin((x * 0.45) + (y * 0.25) + (tw * 5))) + ((bayer - 8) / 160.0);
                uint c;
                if (front > 0)
                {
                    // the face in front: dark in the trough, lighter up it, the light through the crest at the top
                    c = band > 0.8 ? t4 : band > 0.6 ? t3 : band > 0.38 ? t2 : band > 0.16 ? t1 : t0;
                    if (up > 0.35 && Noise(x * 0.55, (y * 0.3) - (tw * 7)) > 0.7) c = fromTop < 8 ? 0xFFFFFFu : 0xD8F6FFu;   // foam running down
                }
                else
                {
                    // the back: lit, a sheen just under the crest, and trails of foam blown back off it
                    c = band > 0.72 ? t4 : band > 0.45 ? t3 : band > 0.2 ? t2 : t1;
                    if (fromTop > foam + 1 && fromTop < foam + 2.5) c = t4;
                    if (fromTop < foam + 6 && Noise((x + (tw * 25)) * 0.35, y * 0.8) > 0.66 && bayer < 10) c = 0xE6FAFFu;
                }

                if (fromTop < foam) c = fromTop < foam - 1 ? (bayer < 13 ? 0xFFFFFFu : 0xE6FAFFu) : 0xBFEFFFu;
                p.Set(x, y, c);
            }

            if (h > 3) p.Set(x, (int)Math.Floor(topY) - 1, t0, 150);
        }

        Crest(p, crestA, dirA, surface, height, tw, 0, t0);
        if (hasB) Crest(p, crestB, dirB, surface, height, tw, 40, t0);
    }

    /// <summary>
    /// How high one crest lifts the sea at a column, and where the column is against it (above 0 in front of it): a concave front face, a long
    /// back with the slosh it leaves.
    /// </summary>
    private static (double H, double Front) Lift(int x, double crest, int dir, double height, double tw)
    {
        var f = (x - crest) * dir;
        var face = height * 0.85;
        if (f > 0) return (f < face ? height * Math.Pow(1 - (f / face), 2.2) : 0, f);
        return ((height * Math.Exp(-(f / 34.0) * (f / 34.0))) + (3.5 * Math.Sin((-f * 0.2) - (tw * 6)) * Math.Exp(f / 70) * Clamp01(tw)), f);
    }

    /// <summary>
    /// The crest of one wave: the lip curling forwards and down, thick at the top and thinning, foam on top and pale water under, with the dark
    /// hollow of the tube filled under it; whitewater churning at the foot of its face; spray and a mist of droplets off its front.
    /// </summary>
    private static void Crest(CapSheet p, double crest, int dir, int surface, double height, double tw, int salt, uint hollow)
    {
        var face = height * 0.85;
        var lip = Math.Min(14, height * 0.32);
        var top = surface - height;
        double lastX = crest, lastY = top;
        for (var i = 0; i <= 26; i++)
        {
            var a = i / 26.0 * 1.95;
            var lx = crest + (dir * lip * Math.Sin(a));
            var ly = top + (lip * (1 - Math.Cos(a)) * 0.9);
            var thick = 1 + (4 * (1 - (a / 2.1)));

            // the hollow under the lip, down to the face
            var fx = (lx - crest) * dir;
            var faceTop = fx < face ? surface - (height * Math.Pow(1 - (fx / face), 2.2)) : surface;
            for (var y = ly + thick; y < faceTop; y++) Put(p, lx, y, hollow);

            for (var k = 0.0; k <= thick; k += 0.5)
            {
                var c = k < thick * 0.45 ? 0xFFFFFFu : k < thick * 0.8 ? 0xD8F6FFu : 0x9CDCF4u;
                Put(p, lx, ly + k, c);
            }

            lastX = lx;
            lastY = ly + thick;
        }

        // the tip of the lip falling in drops
        for (var i = 0; i < 3; i++) Put(p, lastX + (dir * 0.5 * i), lastY + 1 + (i * 2) + ((tw * 20) % 2), 0xE6FAFFu);

        // whitewater churning at the foot of the face
        var foot = crest + (dir * face * 0.85);
        for (var dx = -8; dx <= 10; dx++)
        {
            var x = foot + (dir * dx);
            var n = Noise((x * 0.5) + (tw * 6), 3 + salt);
            var hgt = (int)(1 + (n * 4 * Clamp01(1 - (Math.Abs(dx - 1) / 10.0))));
            for (var k = 0; k < hgt; k++) Put(p, x, surface - k + 1, k == hgt - 1 ? 0xE6FAFFu : n > 0.5 ? 0xFFFFFFu : 0xCFF4FFu);
        }

        // spray and mist off the front
        for (var i = 0; i < 30; i++)
        {
            var life = ((tw * 1.4) + Rnd(i + salt, 61)) % 1;
            var sx = crest + (dir * life * (10 + (Rnd(i + salt, 62) * 18))) - (dir * Rnd(i + salt, 63) * 6);
            var sy = top - (life * (10 + (Rnd(i + salt, 64) * 10))) + (life * life * 32);
            Put(p, sx, sy, i % 3 == 0 ? 0xFFFFFFu : 0xCFF4FFu, 255 * (1 - (life * 0.8)));
            if (i % 4 == 0) Put(p, sx + dir, sy, 0xFFFFFFu, 200 * (1 - life));
        }

        for (var dy = -10; dy <= 2; dy++)
        {
            for (var dx = -4; dx <= 16; dx++)
            {
                var x = (int)Math.Round(crest + (dir * dx));
                var y = (int)Math.Round(top + dy);
                if (CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] != 0 || Rnd(x + (int)(tw * 10), y + salt) > 0.5) continue;
                p.Set(x, y, 0xE6FAFFu, 140);
            }
        }
    }
}
