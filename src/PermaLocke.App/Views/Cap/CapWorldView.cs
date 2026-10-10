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
        var key = $"{data.Seed}|{(int)(_context.SkyHour * 4)}|{worldX}|{data.Cleared}|{graves.Count}|{string.Join(",", graves.Select(g => g.Name.Length + g.AgeDays / 7))}";
        if (key != _sceneKey)
        {
            _sceneKey = key;
            _scenes.Clear();
            _fossils = CapSea.FossilColumns(Layout, graves.Count, _context);
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

        CapSeaLive.Surface(frame, Layout, pal, _context, phase);
        CapSeaLive.Stars(frame, Layout, _context, phase);
        CapSeaLive.Shafts(frame, Layout, pal, _context, phase);
        CapSeaLive.Snow(frame, Layout, pal, _context, phase);
        CapFlora.Paint(frame, Layout, pal, _context, _fossils, phase);
        CapDeep.Lanterns(frame, Layout, pal, data, phase);

        if (Layout.Panel == CapPanelKind.Party) PaintParty(frame, data, pal, anim, t, phase);
    }

    private void PaintParty(CapSheet p, CapPlaqueData data, CapSeaPalette pal, CapAnim? anim, double t, double phase)
    {
        _buoy.Paint(p, Layout, pal, phase, 1);
        CapSounding.Paint(p, Layout, pal, data, _creatures, anim, t, phase);

        for (var i = 0; i < _creatures.Length; i++)
        {
            var creature = _creatures[i];
            if (creature.Member is not { } m)
            {
                PaintVacant(p, creature, pal, phase);
                continue;
            }

            var sway = Layout.Mode == CapMode.Slim || Layout.Dense ? 1.0 : 1.7;
            var ox = (int)Math.Round(sway * Math.Sin((phase * 0.8) + (i * 1.3)));
            var oy = (int)Math.Round(1.2 * Math.Sin((phase * 1.1) + (i * 1.7)));
            var share = anim?.HpFraction(i, t) ?? (m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0);

            // The halo breathes slower the healthier it is: a heartbeat that quickens as the life runs out.
            var rate = m.Fallen || m.Egg ? 0.5 : 0.7 + (2.0 * (1 - share));
            var breath = 0.5 + (0.5 * Math.Sin(((anim is null ? 0 : t) * rate * 2) + i));
            creature.PaintPicture(p, ox, oy, pal.Ink, breath, m.Fallen ? 0.5 : 1);

            if (creature.Info is { } info) p.Blit(info, creature.InfoAt.X, creature.InfoAt.Y);
            PaintBeads(p, creature, m, share, breath, pal);
            PaintTicker(p, creature, m, i, anim is null ? 0 : t, pal);
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

    private static void PaintBeads(CapSheet p, CapCreature c, CapPlaqueMember m, double share, double breath, CapSeaPalette pal)
    {
        if (c.Beads <= 0 || m.Egg || m.Fallen) return;

        var lit = share <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(share * c.Beads - 0.001));
        var colour = share > 0.5 ? 0x6EF0A0u : share > 0.2 ? 0xF0D060u : 0xFF6A50u;
        CapGlyphs.Beads(p, c.BeadsAt.X, c.BeadsAt.Y, c.Beads, lit, colour, pal.Ink, 0.4 + (0.6 * breath));
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
