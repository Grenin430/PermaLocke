namespace PermaLocke.App.Views;

/// <summary>
/// One window of the sea (2026-10-10): its layout, the pictures it keeps (the still scene of each mood, the buoy, the creatures) and the way
/// it paints a frame from them. <see cref="SetData"/> rebuilds only what the data changed; <see cref="Paint"/> is a function of the data, the
/// animation state and the moment, nothing else.
/// </summary>
public sealed class CapWorldView
{
    private readonly Dictionary<CapMood, CapSheet> _scenes = [];
    private readonly CapCreature[] _creatures;
    private readonly string?[] _keys;
    private readonly CapBuoy _buoy = new();
    private readonly CapSeaPalette _writing = CapSeaPalette.Of(CapMood.Calm);

    private CapPlaqueData? _data;
    private CapSeaContext _context = new();
    private string _sceneKey = string.Empty;
    private string _buoyKey = string.Empty;
    private int[] _fossils = [];
    private CapFossil[] _fossilViews = [];

    public CapWorldView(CapMode mode, CapPanelKind panel, int cols, int rows, (int X0, int X1, int Y1) notch)
    {
        Layout = CapWorld.Arrange(mode, panel, cols, rows, notch);
        _creatures = [.. Layout.Creatures.Select(slot => new CapCreature { Slot = slot })];
        _keys = new string?[_creatures.Length];
    }

    public CapWorldLayout Layout { get; }

    public CapSeaContext Context => _context;

    /// <summary>The creature of a slot, for the effects that need to know where it is.</summary>
    public CapCreature? Creature(int index) => index >= 0 && index < _creatures.Length ? _creatures[index] : null;

    /// <summary>Tells the view what the sea says now: what changed is rebuilt, what did not is kept.</summary>
    public void SetData(CapPlaqueData data, int worldX = 0, int worldWidth = 0)
    {
        _data = data;
        _context = new CapSeaContext(data.Seed, data.Hour, worldX, worldWidth);

        // The scene is remade when the floor changes (a grave, a stratum), the hour moves a quarter, or the run is another.
        var graves = data.Graves ?? [];
        var key = $"{data.Seed}|{(int)(_context.SkyHour * 4)}|{worldX}|{data.Cleared}|{graves.Count}|{string.Join(",", graves.Select(g => g.Level + ":" + Math.Min(4, g.AgeDays / 3)))}";
        if (key != _sceneKey)
        {
            _sceneKey = key;
            _scenes.Clear();
            _fossils = CapSea.FossilColumns(Layout, graves.Count, _context);
            _fossilViews = Layout.Panel == CapPanelKind.Deep ? CapFossils.Build(Layout, graves, _context, _fossils) : [];
        }

        if (Layout.Panel != CapPanelKind.Party) return;

        var buoyKey = CapBuoy.KeyOf(data) + "|" + Layout.Buoy;
        if (buoyKey != _buoyKey)
        {
            _buoyKey = buoyKey;
            _buoy.Build(Layout, _writing, data);
        }

        var narrow = Layout.Mode != CapMode.Column;
        for (var i = 0; i < _creatures.Length; i++)
        {
            var member = i < data.Party.Count ? data.Party[i] : null;
            var memberKey = CapCreature.KeyOf(member) + "|" + data.Cap + "|" + narrow;
            if (memberKey == _keys[i]) continue;

            _keys[i] = memberKey;
            _creatures[i].Build(member, _writing, data.Cap, narrow);
        }
    }

    private CapSheet Scene(CapMood mood)
    {
        if (_scenes.TryGetValue(mood, out var scene)) return scene;

        scene = new CapSheet(Layout.Cols, Layout.Rows) { NotchX0 = Layout.Notch.X0, NotchX1 = Layout.Notch.X1, NotchY1 = Layout.Notch.Y1 };
        CapSea.Paint(scene, Layout, CapSeaPalette.Of(mood), _data!, _context);
        _scenes[mood] = scene;
        return scene;
    }

    /// <summary>Paints a frame into <paramref name="frame"/>, which has the window's size and notch.</summary>
    public void Paint(CapSheet frame, double t, CapAnim? anim)
    {
        if (_data is not { } data) return;

        var mood = anim?.Mood(t) ?? (CapMood.Calm, CapMood.Calm, 1.0);
        var phase = anim?.Phase(t) ?? 0;
        var pal = CapSeaPalette.Of(mood.Now);

        frame.CopyFrom(Scene(mood.Now));
        if (mood.Blend < 1 && mood.Before != mood.Now) DitherFrom(frame, Scene(mood.Before), mood.Blend);

        // The change of the cap opens the floor: it drops away, glowing along its cracks, and comes back with its new stratum.
        var drop = anim?.FloorDrop(t, 30) ?? 0;
        if (drop > 0) DropFloor(frame, drop, pal, anim!.SinceCap(t) ?? 0);

        CapSeaLive.Surface(frame, Layout, pal, _context, phase);
        CapSeaLive.Stars(frame, Layout, _context, phase);
        CapSeaLive.Shafts(frame, Layout, pal, _context, phase);
        CapSeaLive.Snow(frame, Layout, pal, _context, phase);
        if (anim is not null) CapFauna.Paint(frame, Layout, pal, mood, _context, t, phase, data.Graves ?? []);
        CapFlora.Paint(frame, Layout, pal, _context, _fossils, phase);
        CapDeep.Lanterns(frame, Layout, pal, data, phase);
        CapDeep.Souls(frame, Layout, _fossilViews, phase);
        // A still frame (no animation) shows the focus in the middle of its time on the newest fossil, not dark at the start of it.
        CapDeep.Focus(frame, Layout, pal, _fossilViews, _context, anim is null ? CapDeep.NewestSeconds / 2 : t);

        if (Layout.Panel == CapPanelKind.Party) PaintParty(frame, data, pal, mood, anim, t, phase);

        // A fall darkens the whole sea for a moment: a veil of dither that comes down fast and lifts slowly, in both windows.
        if (anim?.LastFallSince(t) is { } since && since < 2.6)
        {
            var veil = since < 0.25 ? since / 0.25 : 1 - ((since - 0.25) / 2.35);
            frame.Dither(0, 0, frame.W, frame.H, 0x04061Au, (int)(130 * veil), (int)Math.Floor(since * 12) & 1);
        }
    }

    private void PaintParty(CapSheet p, CapPlaqueData data, CapSeaPalette pal, (CapMood Now, CapMood Before, double Blend) mood, CapAnim? anim, double t, double phase)
    {
        var danger = mood.Now == CapMood.Danger;
        _buoy.Paint(p, Layout, pal, phase, 1, anim?.HeaderDissolve(t) ?? 1, danger ? 0xFF5040u : 0xFFF0B0u);
        if (anim?.SinceCap(t) is { } sinceCap && sinceCap < 2.0) CapFx.CapRing(p, Layout, sinceCap);

        CapSounding.Paint(p, Layout, pal, data, _creatures, anim, t, phase);
        var graves = data.Graves?.Count ?? 0;

        for (var i = 0; i < _creatures.Length; i++)
        {
            var creature = _creatures[i];
            if (creature.Member is not { } m)
            {
                PaintVacant(p, creature, pal, phase);
                continue;
            }

            var sway = Layout.Mode == CapMode.Slim || Layout.Dense ? 1.0 : 1.7;
            var share = anim?.HpFraction(i, t) ?? (m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0);
            var pose = CapFx.Pose(anim, i, t, m, share, data.InBattle, Layout, _context, graves);

            var ox = pose.Dx + (anim is null ? 0 : (int)Math.Round(sway * Math.Sin((phase * 0.8) + (i * 1.3))));
            var oy = pose.Dy + (anim is null ? 0 : (int)Math.Round(1.2 * Math.Sin((phase * 1.1) + (i * 1.7))));
            if (!pose.Hidden)
            {
                creature.PaintPicture(p, ox, oy, pal.Ink, pose.Breath, pose.Strength, pose.HaloTint, pose.Wave, (phase * pose.WaveSpeed) + (i * 1.1), pose.HaloScale);
                if (pose.Wash is { } wash) creature.PaintTint(p, ox, oy, wash, pose.WashAlpha);
            }

            if (creature.Info is { } info) p.Blit(info, creature.InfoAt.X, creature.InfoAt.Y);
            PaintBeads(p, creature, m, share, pose.Breath, pal, anim?.TrailFraction(i, t), anim, i, t);
            PaintTicker(p, creature, m, i, anim is null ? 0 : t, pal);

            if (anim is not null)
            {
                CapFx.Overlay(p, Layout, pal, creature, m, anim, i, t, phase);
                CapFx.Fall(p, Layout, pal, _context, creature, anim, i, t, graves, _fossils);
            }
        }
    }

    private static void PaintVacant(CapSheet p, CapCreature c, CapSeaPalette pal, double phase)
    {
        // An empty place is a bubble of dither that hangs where a creature would, and bobs a little.
        var (cx, cy) = c.Centre;
        cy += (int)Math.Round(Math.Sin((phase * 0.9) + cx));
        for (var a = 0; a < 24; a++)
        {
            var angle = a * Math.PI / 12;
            p.Set(cx + (int)Math.Round(Math.Cos(angle) * 7), cy + (int)Math.Round(Math.Sin(angle) * 6), pal.Glow, 55);
        }
    }

    /// <summary>The moves, one at a time: the name of one with a pip of the colour of its type, changing every two and a half seconds, each Pokémon on its own beat.</summary>
    private static void PaintTicker(CapSheet p, CapCreature c, CapPlaqueMember m, int index, double t, CapSeaPalette pal)
    {
        var (x, y, letters) = c.TickerAt;
        if (letters < 5 || m.Moves is not { Count: > 0 } moves || m.Egg || m.Fallen) return;

        var move = moves[((int)Math.Floor((t / 2.5) + index)) % moves.Count];
        var name = (move.Name ?? "MOVIMIENTO").ToUpperInvariant();
        if (name.Length > letters) name = name[..letters];
        var colour = move.Type >= 0 ? CapArt.TypeColour(move.Type) : 0x8A98B0u;
        CapGlyphs.Spot(p, x, y, move.Empty ? CapSheet.Darker(colour, 0.6) : colour, pal.Ink);
        CapGlyphs.Outlined(p, name, x + 7, y, move.Empty ? CapSheet.Mix(pal.Glow, pal.Water[4], 0.6) : pal.Glow, pal.Ink);
        if (move.Empty) p.HLine(x + 7, y + 2, CapGlyphs.Width(name, false), 0xFF6A50u);
    }

    private static void PaintBeads(CapSheet p, CapCreature c, CapPlaqueMember m, double share, double breath, CapSeaPalette pal, double? trail, CapAnim? anim = null,
        int index = 0, double t = 0)
    {
        if (c.Beads <= 0 || m.Egg || m.Fallen) return;

        var lit = share <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling((share * c.Beads) - 0.001));
        var colour = share > 0.5 ? 0x6EF0A0u : share > 0.2 ? 0xF0D060u : 0xFF6A50u;

        // The trail of a drop: the beads that were lit a moment ago and are not now, pale, going out one by one.
        if (trail is { } was)
        {
            var before = Math.Clamp((int)Math.Ceiling((was * c.Beads) - 0.001), 0, c.Beads);
            for (var b = lit; b < before; b++) CapGlyphs.Beads(p, c.BeadsAt.X + (b * 4), c.BeadsAt.Y, 1, 1, 0xF4E4C0u, pal.Ink, 0);
        }

        CapGlyphs.Beads(p, c.BeadsAt.X, c.BeadsAt.Y, c.Beads, lit, colour, pal.Ink, 0.4 + (0.6 * breath));

        // The numbers count with the bar, and warm or cool while it moves.
        var shownHp = (int)Math.Round(share * m.MaxHp);
        var duration = anim?.BarSeconds(index) ?? 0;
        var hot = anim?.SinceHit(index, t) is { } hit && hit < duration + 0.3;
        var warm = anim?.SinceHeal(index, t) is { } heal && heal < duration + 0.3;
        if (c.NumbersAt.X > 0) CapGlyphs.Outlined(p, $"{shownHp}/{m.MaxHp}", c.NumbersAt.X, c.NumbersAt.Y, hot ? 0xFFB090u : warm ? 0xA8FFC0u : pal.Glow, pal.Ink);
        if (anim is null) return;

        // The bead that goes out throws a spark up; the one that lights flashes.
        if (anim.SinceHit(index, t) is { } since && since < duration + 0.9 && trail is { } ghost)
        {
            var before = Math.Clamp((int)Math.Ceiling((ghost * c.Beads) - 0.001), 0, c.Beads);
            for (var b = lit; b < before; b++)
            {
                var age = ((since * 5) + (b * 0.37)) % 1.0;
                p.Set(c.BeadsAt.X + (b * 4) + 1 + (int)Math.Round(Math.Sin((age * 6) + b)), c.BeadsAt.Y - 1 - (int)(age * 7), 0xFFE0B0u, (int)(220 * (1 - age)));
            }
        }

        if (anim.SinceHeal(index, t) is { } healed && healed < duration + 0.6 && lit > 0)
        {
            p.Set(c.BeadsAt.X + ((lit - 1) * 4) + 1, c.BeadsAt.Y - 1, 0xFFFFFFu, 230);
            p.Set(c.BeadsAt.X + ((lit - 1) * 4) + 1, c.BeadsAt.Y + 3, 0xFFFFFFu, 150);
        }
    }

    /// <summary>
    /// The floor with the cap changing: everything below the line of the floor is moved down <paramref name="drop"/> rows and the gap filled with water,
    /// a crack of light runs along the new edge, and sediment goes up from it.
    /// </summary>
    private void DropFloor(CapSheet frame, int drop, CapSeaPalette pal, double since)
    {
        var from = Layout.FloorTop - 18;
        if (from <= Layout.Notch.Y1 || drop <= 0) return;

        for (var y = frame.H - 1; y >= from + drop; y--)
        {
            Buffer.BlockCopy(frame.B, (y - drop) * frame.W * 4, frame.B, y * frame.W * 4, frame.W * 4);
        }

        for (var y = from; y < Math.Min(frame.H, from + drop); y++)
        {
            for (var x = 0; x < frame.W; x++) frame.Erase(x, y);
            for (var x = 0; x < frame.W; x++) frame.Set(x, y, CapSea.WaterAt(Layout, pal, x, y));
        }

        // The crack: a bright thread that follows the edge of the floor, flickering, and dust rising from it.
        var beat = (int)Math.Floor(since * 14);
        for (var x = 0; x < frame.W; x++)
        {
            var edge = CapSea.Relief(Layout, _context, x, _fossils) + drop;
            if (edge >= frame.H) continue;
            var jitter = (CapArt.Hash(x + (beat * 3), 7) % 3) - 1;
            frame.Set(x, edge + jitter, 0xFFE890u, 220);
            if (((x + beat) & 3) == 0) frame.Set(x, edge + jitter - 1 - (CapArt.Hash(x, beat) % 6), 0xFFF8D0u, 150);
        }
    }

    /// <summary>One mood's scene over another with the dither: the cells whose pattern number is above the progress still show the old one.</summary>
    private static void DitherFrom(CapSheet frame, CapSheet before, double progress)
    {
        var cut = progress * 16;
        for (var y = 0; y < frame.H; y++)
        {
            for (var x = 0; x < frame.W; x++)
            {
                if (CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] < cut) continue;
                Buffer.BlockCopy(before.B, ((y * before.W) + x) * 4, frame.B, ((y * frame.W) + x) * 4, 4);
            }
        }
    }
}
