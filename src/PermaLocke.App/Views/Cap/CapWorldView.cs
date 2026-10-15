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
    private readonly int[] _builtCap;
    private readonly CapBuoy _buoy = new();
    private readonly CapSeaPalette _writing = CapSeaPalette.Of(CapMood.Calm);

    private CapPlaqueData? _data;
    private CapSeaContext _context = new();
    private string _sceneKey = string.Empty;
    private string _buoyKey = string.Empty;
    private int[] _fossils = [];
    private readonly Dictionary<string, CapDeathScene> _deaths = [];
    private CapTrial.Plaque? _plaque;
    private string _plaqueKey = string.Empty;

    public CapWorldView(CapMode mode, CapPanelKind panel, int cols, int rows, (int X0, int X1, int Y1) notch)
    {
        Layout = CapWorld.Arrange(mode, panel, cols, rows, notch);
        _creatures = [.. Layout.Creatures.Select(slot => new CapCreature { Slot = slot })];
        _keys = new string?[_creatures.Length];
        _builtCap = new int[_creatures.Length];
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
            // The fallen are not drawn in the overlay any more (2026-10-15): the cemetery keeps them.
            _fossils = [];
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
            _builtCap[i] = data.Cap;
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

        // The deaths of the party, one after another (2026-10-15): the one being shown now, and in this window its scene.
        var deaths = anim is null ? [] : CapDeath.Schedule(data, anim, t);
        var dying = data.DeathScenes ? deaths.FirstOrDefault(d => t >= d.Start && t < d.End) : null;
        var scene = Layout.Panel == CapPanelKind.Party && dying is not null ? SceneOf(dying, data, pal) : null;

        // A trial passed (2026-10-15): its two films, the crystal's and the cap's; until the cap's breaks it, the plaque and the levels keep the old cap.
        var trial = data.Cap > 0 ? CapTrial.Now(data, anim, t) : null;

        frame.CopyFrom(Scene(mood.Now));
        if (mood.Blend < 1 && mood.Before != mood.Now) DitherFrom(frame, Scene(mood.Before), mood.Blend);

        // The stars twinkle behind what stands far off in the sky (the Sky Pillar, the Ultra Wormhole, 2026-10-15), and the sea is in front of it.
        CapSeaLive.Stars(frame, Layout, _context, phase);
        CapFarSky.Paint(frame, Layout, pal, _context, phase, scene?.WormholeSize(t) ?? 1);
        CapSeaLive.Surface(frame, Layout, pal, _context, phase);
        CapSeaLive.Shafts(frame, Layout, pal, _context, phase);
        CapSeaLive.Snow(frame, Layout, pal, _context, phase);
        CapFlora.Paint(frame, Layout, pal, _context, _fossils, phase);
        CapRuins.Live(frame, Layout, pal, _context, _fossils, phase);
        // In the party's window, the sign with the cap (2026-10-15). The coral and the little things on the sand (CapReefLife) were not liked
        // and are not painted until something else is chosen.
        if (Layout.Panel == CapPanelKind.Party)
        {
            var atCap = (mood.Now == CapMood.Cap ? mood.Blend : 0) + (mood.Before == CapMood.Cap ? 1 - mood.Blend : 0);
            var number = CapTrial.SignNumber(trial, data.Cap);
            CapSign.Paint(frame, Layout, pal, _context, data, _fossils, atCap, phase, number ?? data.Cap, number is not null);
        }

        if (anim is not null) CapFauna.Paint(frame, Layout, pal, mood, _context, t, phase, []);

        if (Layout.Panel == CapPanelKind.Party) PaintParty(frame, data, pal, mood, anim, t, phase, deaths, trial);

        // The death being shown: in the party's window the fallen one taken apart; in the deep's, its share of the deaths that reach it.
        scene?.Paint(frame, t, phase);
        if (Layout.Panel == CapPanelKind.Deep && dying is not null)
        {
            var since = t - dying.Start;
            switch (dying.Kind)
            {
                case CapDeathKind.Sky:
                    CapDeathSky.Deep(frame, Layout, since);
                    break;
                case CapDeathKind.Storm:
                    CapDeathStormSky.Deep(frame, Layout, since);
                    break;
                case CapDeathKind.Necrozma:
                    NecrozmaDeath.Deep(frame, Layout, _context, since);
                    break;
            }
        }

        if (trial is not null)
        {
            CapTrial.PaintCrystal(frame, Layout, _context, trial);
            if (Layout.Panel == CapPanelKind.Party && PlaqueOf(trial, data) is { } plaque) CapTrial.PaintCap(frame, Layout, trial, plaque);
        }
    }

    /// <summary>What the cap's film breaks and remakes, worked out when a trial starts and kept while it plays.</summary>
    private CapTrial.Plaque? PlaqueOf(CapTrialTurn trial, CapPlaqueData data)
    {
        if (CapSign.Where(Layout, data, _context, _fossils) is not { } sign) return null;
        var key = $"{trial.From}|{trial.To}|{sign.Board}|{sign.NumberX}|{sign.NumberY}|{sign.Scale}";
        if (key != _plaqueKey || _plaque is null)
        {
            _plaqueKey = key;
            _plaque = new CapTrial.Plaque(sign.Board, sign.Ground, sign.NumberX, sign.NumberY, sign.Scale, trial.From, trial.To, Layout.Cols, Layout.Rows);
        }

        return _plaque;
    }

    /// <summary>
    /// The scene of a death, made when it starts from the creature as it was: its picture in its own colours (not the grey of the fallen) and
    /// everything written beside it, and the rest of the party for the deaths that touch them. Kept while it plays.
    /// </summary>
    private CapDeathScene? SceneOf(CapDeathTurn turn, CapPlaqueData data, CapSeaPalette pal)
    {
        var key = $"{turn.Pid}|{turn.Index}|{turn.Start:R}";
        if (_deaths.TryGetValue(key, out var made)) return made;
        if (turn.Index >= _creatures.Length || _creatures[turn.Index].Member is not { } member) return null;

        var creature = _creatures[turn.Index];
        // a fallen one is drawn grey; its death starts from it in its own colours (one point of life for the drawing, none for its bar)
        var m = member with { Hp = Math.Max(1, member.Hp) };
        var shadow = new CapCreature { Slot = creature.Slot };
        shadow.Build(m, _writing, data.Cap, Layout.Mode != CapMode.Column);
        var picture = new CapSheet(Layout.Cols, Layout.Rows);
        shadow.PaintPicture(picture, 0, 0, pal.Ink, 0.5, 1, null, 0, 0, 0);
        var writing = new CapSheet(Layout.Cols, Layout.Rows);
        if (shadow.Info is { } info) writing.Blit(info, shadow.InfoAt.X, shadow.InfoAt.Y);
        var share = m.MaxHp > 0 ? Math.Clamp(member.Hp / (double)m.MaxHp, 0, 1) : 0;
        PaintBeads(writing, shadow, m, share, 0.5, pal, null);
        var others = _creatures.Where((c, i) => i != turn.Index && c.Member is { Fallen: false }).ToList();
        var rowLine = creature.Slot.Info.Height > 0 ? creature.Slot.Info.Bottom - 1 : creature.Slot.Sprite.Bottom + 3;

        if (_deaths.Count > 8) _deaths.Clear();
        return _deaths[key] = new CapDeathScene(turn, Layout, picture, writing, shadow.InfoAt.X, rowLine, others);
    }

    private void PaintParty(CapSheet p, CapPlaqueData data, CapSeaPalette pal, (CapMood Now, CapMood Before, double Blend) mood, CapAnim? anim, double t, double phase,
        IReadOnlyList<CapDeathTurn> deaths, CapTrialTurn? trial)
    {
        var ground = trial is null ? Layout.Rows : CapSign.Where(Layout, data, _context, _fossils)?.Ground ?? Layout.Rows;

        // Singleplayer (2026-10-13): no buoy with the cap, no tag under it, no sounding line; the level turns gold at the cap and that is all.
        var graves = data.Graves?.Count ?? 0;

        for (var i = 0; i < _creatures.Length; i++)
        {
            var creature = _creatures[i];
            if (creature.Member is not { } m)
            {
                PaintVacant(p, creature, pal, phase);
                continue;
            }

            // A fallen one is drawn until its death's turn comes; from then on its death paints it, and after it nothing does: its row is empty.
            // One that fell before the panel opened is not drawn at all; with the death scenes off, it goes when it falls.
            var turn = deaths.FirstOrDefault(d => d.Index == i);
            if (turn is not null ? !data.DeathScenes || t >= turn.Start : m.Fallen) continue;

            // While a trial plays, its level keeps the old cap until the cap's last wave passes it.
            var cap = CapTrial.CapOf(trial, data.Cap, creature.Slot.Info.Top + 3, ground);
            if (cap != _builtCap[i])
            {
                _builtCap[i] = cap;
                creature.Build(m, _writing, cap, Layout.Mode != CapMode.Column);
            }

            var sway = Layout.Mode == CapMode.Slim || Layout.Dense ? 1.0 : 1.7;
            var share = anim?.HpFraction(i, t) ?? (m.MaxHp > 0 ? Math.Clamp(m.Hp / (double)m.MaxHp, 0, 1) : 0);
            var pose = CapFx.Pose(anim, i, t, m, share, data.InBattle, Layout, _context, graves);

            // the light of a trial passing over it: it hops, washes towards the light and its halo grows
            var lift = trial is null ? null : CapTrial.Lift(trial, Layout, _context, creature.Centre, ground);
            var ox = pose.Dx + (anim is null ? 0 : (int)Math.Round(sway * Math.Sin((phase * 0.8) + (i * 1.3))));
            var oy = pose.Dy - (lift?.Hop ?? 0) + (anim is null ? 0 : (int)Math.Round(1.2 * Math.Sin((phase * 1.1) + (i * 1.7))));
            if (!pose.Hidden)
            {
                creature.PaintPicture(p, ox, oy, pal.Ink, pose.Breath, pose.Strength, lift?.Wash ?? pose.HaloTint, pose.Wave, (phase * pose.WaveSpeed) + (i * 1.1),
                    pose.HaloScale + (lift?.Halo ?? 0));
                if (pose.Wash is { } wash) creature.PaintTint(p, ox, oy, wash, pose.WashAlpha);
                if (lift is { } l) creature.PaintTint(p, ox, oy, l.Wash, l.Alpha);
            }

            if (creature.Info is { } info) p.Blit(info, creature.InfoAt.X, creature.InfoAt.Y);
            PaintBeads(p, creature, m, share, pose.Breath, pal, anim?.TrailFraction(i, t), anim, i, t);
            PaintTicker(p, creature, m, i, anim is null ? 0 : t, pal);

            if (anim is not null)
            {
                CapFx.Overlay(p, Layout, pal, creature, m, anim, i, t, phase);
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
