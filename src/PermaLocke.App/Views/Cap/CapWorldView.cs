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
    private readonly CapShiny _shiny = new();
    private readonly CapVial _vial = new();
    private readonly CapRepel _repel = new();
    private readonly CapEggTurbo _turbo = new();
    private readonly List<CapVial.Target> _vialTargets = [];
    private readonly string?[] _keys;
    private readonly int[] _builtCap;
    private readonly CapBuoy _buoy = new();
    private readonly CapSeaPalette _writing = CapSeaPalette.Of(CapMood.Calm);

    private CapPlaqueData? _data;
    private CapSeaContext _context = new();

    /// <summary>The lab only: draws every window at this hour of Alola instead of the run's.</summary>
    public static double? ForcedHour { get; set; }
    private string _sceneKey = string.Empty;
    private string _buoyKey = string.Empty;
    private int[] _fossils = [];
    private readonly Dictionary<string, CapDeathScene> _deaths = [];
    private CapTrial.Plaque? _plaque;

    /// <summary>Where each of the party was last drawn (by PID): its slot, and where it slides from when it moves to another.</summary>
    private readonly Dictionary<uint, (int Slot, double FromY, double At)> _rows = [];
    private CapSheet? _rowSheet;
    private const double SlideSeconds = 0.6;
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
        _context = new CapSeaContext(data.Seed, ForcedHour ?? data.Hour, worldX, worldWidth);

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
        var order = Layout.Panel == CapPanelKind.Party ? Arrange(data, deaths, anim, t) : [];
        var scene = Layout.Panel == CapPanelKind.Party && dying is not null ? SceneOf(dying, data, pal, order) : null;

        // A trial passed (2026-10-15): its two films, the crystal's and the cap's; until the cap's breaks it, the plaque and the levels keep the old cap.
        var trial = data.Cap > 0 ? CapTrial.Now(data, anim, t) : null;

        // The Mega Evolution unlocked at the sixth trial (2026-10-16): the Megas' parade while its films play; the visitors are Megas once they come out.
        var parade = CapMega.Since(data, anim, t);
        _context = _context with
        {
            Megas = data.Cleared >= 6 && !(parade < CapMega.Reveal), Parade = parade is not null,
            Shiny = data.Shiny is { } rainbow && t >= rainbow.Start && rainbow.End is null ? t - rainbow.Start : -1,
            // the ruins' buried storey rises after the Electrostal's film (2026-10-19), and stays risen
            Rise = trial?.Crystal?.Item == CapRaikou.Electrostal ? CapGimmighoulRuins.RiseOf(trial.CrystalT - CapGimmighoulRuins.RiseStart)
                : Won(data, CapRaikou.Electrostal) ? 1 : 0,
            Rising = trial?.Crystal?.Item == CapRaikou.Electrostal ? trial.CrystalT - CapGimmighoulRuins.RiseStart : -1,
        };
        CapShiny.BeginFrame(frame.W);

        frame.CopyFrom(Scene(mood.Now));
        if (mood.Blend < 1 && mood.Before != mood.Now) DitherFrom(frame, Scene(mood.Before), mood.Blend);

        // The stars twinkle behind what stands far off in the sky (the Sky Pillar, the Ultra Wormhole, 2026-10-15), and the sea is in front of it.
        CapSeaLive.Stars(frame, Layout, _context, phase);
        CapClouds.Paint(frame, Layout, pal, _context, t);
        CapFarSky.Paint(frame, Layout, pal, _context, phase, scene?.WormholeSize(t) ?? 1);
        CapSeaLive.Surface(frame, Layout, pal, _context, phase);
        CapSeaLive.Snow(frame, Layout, pal, _context, phase);
        CapFlora.Paint(frame, Layout, pal, _context, _fossils, phase);
        // In the party's window, the sign with the cap (2026-10-15). The coral and the little things on the sand (CapReefLife) were not liked
        // and are not painted until something else is chosen.
        if (Layout.Panel == CapPanelKind.Party)
        {
            var atCap = (mood.Now == CapMood.Cap ? mood.Blend : 0) + (mood.Before == CapMood.Cap ? 1 - mood.Blend : 0);
            var number = CapTrial.SignNumber(trial, data.Cap);
            CapSign.Paint(frame, Layout, pal, _context, data, _fossils, atCap, phase, number ?? data.Cap, number is not null);
        }

        if (anim is not null) CapFauna.Paint(frame, Layout, pal, mood, _context, t, phase, []);
        if (parade is { } megaSince) CapMega.Paint(frame, Layout, _context, megaSince, Layout.Panel == CapPanelKind.Deep);

        // The crystals of the trials cleared, set in the abyss's sand (2026-10-16).
        // prototype of the plan (2026-10-18): the Gimmighoul ruins take the altar's place; the crystals go into them in the next step
        CapGimmighoulRuins.Paint(frame, Layout, pal, _context, phase);

        // the Normastal in the ruins' window for the rest of the run (2026-10-18), unless its film is showing it coming
        if (Layout.Panel == CapPanelKind.Deep && trial?.Crystal?.Item != CapCrystalFall.Normastal && data.Crystals is { } won)
        {
            foreach (var (item, icon) in won)
            {
                if (item == CapCrystalFall.Normastal && CapTrialCrystal.Of(item, icon) is { } normastal) CapCrystalFall.PaintRest(frame, Layout, _context, normastal, phase);
                if (item == CapAreaZero.Litostal && trial?.Crystal?.Item != CapAreaZero.Litostal && CapTrialCrystal.Of(item, icon) is { } litostal)
                {
                    CapAreaZero.PaintRest(frame, Layout, _context, litostal, phase);
                }

                if (item == CapRaikou.Electrostal && trial?.Crystal?.Item != CapRaikou.Electrostal && CapTrialCrystal.Of(item, icon) is { } electrostal)
                {
                    CapRaikou.PaintRest(frame, Layout, _context, electrostal, phase);
                }

                if (item == CapGroudon.Geostal && trial?.Crystal?.Item != CapGroudon.Geostal && CapTrialCrystal.Of(item, icon) is { } geostal)
                {
                    CapGroudon.PaintRest(frame, Layout, _context, geostal, phase);
                }

                if (item == CapDiancie.Feeristal && trial?.Crystal?.Item != CapDiancie.Feeristal && CapTrialCrystal.Of(item, icon) is { } feeristal)
                {
                    CapDiancie.PaintRest(frame, Layout, _context, feeristal, phase);
                }

                if (item == CapDragapult.Dracostal && trial?.Crystal?.Item != CapDragapult.Dracostal && CapTrialCrystal.Of(item, icon) is { } dracostal)
                {
                    CapDragapult.PaintRest(frame, Layout, _context, dracostal, phase);
                }

                if (item == CapNictostal.Nictostal && trial?.Crystal?.Item != CapNictostal.Nictostal && CapTrialCrystal.Of(item, icon) is { } nictostal)
                {
                    CapNictostal.PaintRest(frame, Layout, _context, nictostal, phase);
                }

                if (item == CapGiratina.Spectrostal && trial?.Crystal?.Item != CapGiratina.Spectrostal && CapTrialCrystal.Of(item, icon) is { } spectrostal)
                {
                    CapGiratina.PaintRest(frame, Layout, _context, spectrostal, phase);
                }

                if (item == CapShaymin.Fitostal && trial?.Crystal?.Item != CapShaymin.Fitostal && CapTrialCrystal.Of(item, icon) is { } fitostal)
                {
                    CapShaymin.PaintRest(frame, Layout, _context, fitostal, phase);
                }

                if (item == CapHoOh.Pirostal && trial?.Crystal?.Item != CapHoOh.Pirostal && CapTrialCrystal.Of(item, icon) is { } pirostal)
                {
                    CapHoOh.PaintRest(frame, Layout, _context, pirostal, phase);
                }

                if (item == CapGreninja.Hidrostal && trial?.Crystal?.Item != CapGreninja.Hidrostal && CapTrialCrystal.Of(item, icon) is { } hidrostal)
                {
                    CapGreninja.PaintRest(frame, Layout, _context, hidrostal, phase);
                }

                if (item == CapMachamp.Lucharastal && trial?.Crystal?.Item != CapMachamp.Lucharastal && CapTrialCrystal.Of(item, icon) is { } lucharastal)
                {
                    CapMachamp.PaintRest(frame, Layout, _context, lucharastal, phase);
                }
            }
        }

        if (Layout.Panel == CapPanelKind.Party) PaintParty(frame, data, pal, mood, anim, t, phase, deaths, trial, order);

        // The Pokévial (2026-10-16): beside the cap's sign, and its healing over the party while it plays.
        if (Layout.Panel == CapPanelKind.Party && data.Vial is { } vial && CapSign.Where(Layout, data, _context, _fossils) is { } sign)
        {
            _vialTargets.Clear();
            for (var slot = 0; slot < order.Count && slot < _creatures.Length; slot++)
            {
                var c = _creatures[slot];
                if (c.Member is not { Fallen: false, Egg: false }) continue;
                _vialTargets.Add(new CapVial.Target(c.Centre.X, c.Centre.Y, c.Slot.Sprite, c.BeadsAt.X, c.BeadsAt.Y + 1, c.Beads * 4));
            }

            _vial.Paint(frame, sign.Board, sign.Ground, vial, _vialTargets, t);
        }

        // The Incubadora Turbo (2026-10-16): right of the Pokévial, overcharged while it is on (2026-10-17: it and the repel swapped places).
        if (Layout.Panel == CapPanelKind.Party && data.Turbo is { } turbo && CapSign.Where(Layout, data, _context, _fossils) is { } stand)
            _turbo.Paint(frame, stand.Board.Left + stand.Board.Width + 13 + 27, frame.H - 10, turbo, t);

        // The Repelente Infinito (2026-10-16): right of the Incubadora Turbo, its smoke rising from the nozzle while it is on.
        if (Layout.Panel == CapPanelKind.Party && data.Repel is { } repel && CapSign.Where(Layout, data, _context, _fossils) is { } post)
            _repel.Paint(frame, post.Board.Left + post.Board.Width + 13 + 27 + 24, frame.H - 10, repel, x => CapSea.Relief(Layout, _context, x, _fossils), t);

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
                case CapDeathKind.Corruption:
                    CorruptionDeath.Deep(frame, since);
                    break;
            }
        }

        if (trial is not null)
        {
            CapTrial.PaintCrystal(frame, Layout, _context, trial);
            if (Layout.Panel == CapPanelKind.Party && PlaqueOf(trial, data) is { } plaque) CapTrial.PaintCap(frame, Layout, trial, plaque);
        }

        // the ground shaking while the ruins' storey rises
        CapGimmighoulRuins.Rumble(frame, Layout, _context);

        // A shiny in this battle (2026-10-16): over everything, the look drawn for it.
        if (data.Shiny is { } shiny) _shiny.Paint(frame, Layout, shiny, t);
    }

    /// <summary>Whether a trial's crystal has been won.</summary>
    private static bool Won(CapPlaqueData data, int item)
    {
        if (data.Crystals is not { } won) return false;
        foreach (var (got, _) in won) if (got == item) return true;
        return false;
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
    private CapDeathScene? SceneOf(CapDeathTurn turn, CapPlaqueData data, CapSeaPalette pal, IReadOnlyList<int> order)
    {
        var key = $"{turn.Pid}|{turn.Index}|{turn.Start:R}";
        if (_deaths.TryGetValue(key, out var made)) return made;
        var slot = order.ToList().IndexOf(turn.Index);
        if (slot < 0 || _creatures[slot].Member is not { } member) return null;

        var creature = _creatures[slot];
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
        var others = _creatures.Where((c, j) => j != slot && c.Member is { Fallen: false }).ToList();
        var rowLine = creature.Slot.Info.Height > 0 ? creature.Slot.Info.Bottom - 1 : creature.Slot.Sprite.Bottom + 3;

        if (_deaths.Count > 8) _deaths.Clear();
        return _deaths[key] = new CapDeathScene(turn, Layout, picture, writing, shadow.InfoAt.X, rowLine, others);
    }

    /// <summary>
    /// Which of the party each slot shows, in the game's order (2026-10-15): the fallen keep their slot until their death has been shown, and
    /// then leave it; the ones under them move up and the empty slots are the last ones. A slot whose Pokémon changed is built again.
    /// </summary>
    private List<int> Arrange(CapPlaqueData data, IReadOnlyList<CapDeathTurn> deaths, CapAnim? anim, double t)
    {
        var order = new List<int>();
        for (var i = 0; i < data.Party.Count && order.Count < _creatures.Length; i++)
        {
            var m = data.Party[i];
            var turn = deaths.FirstOrDefault(d => d.Index == i);
            if (m.Fallen && (turn is null ? anim?.AwaitingFall(i, t) != true : !data.DeathScenes || t >= turn.End)) continue;
            order.Add(i);
        }

        var narrow = Layout.Mode != CapMode.Column;
        for (var j = 0; j < _creatures.Length; j++)
        {
            var member = j < order.Count ? data.Party[order[j]] : null;
            var key = CapCreature.KeyOf(member) + "|" + data.Cap + "|" + narrow;
            if (key == _keys[j]) continue;

            _keys[j] = key;
            _builtCap[j] = data.Cap;
            _creatures[j].Build(member, _writing, data.Cap, narrow);
        }

        return order;
    }

    /// <summary>How far above or below its slot a row is drawn now: it slides from where it was when it changed slot.</summary>
    internal int Slide(CapPlaqueMember m, int slot, double t, bool animated)
    {
        var id = m.Pid != 0 ? m.Pid : (uint)m.Name.GetHashCode();
        var top = _creatures[slot].Slot.Sprite.Top;
        if (!_rows.TryGetValue(id, out var row) || !animated)
        {
            _rows[id] = (slot, top, t);
            return 0;
        }

        if (row.Slot != slot)
        {
            // from wherever it is drawn now, the slide it was in included
            var was = _creatures[row.Slot].Slot.Sprite.Top;
            var now = row.FromY + ((was - row.FromY) * Ease(Math.Clamp((t - row.At) / SlideSeconds, 0, 1)));
            _rows[id] = row = (slot, now, t);
        }

        var k = Math.Clamp((t - row.At) / SlideSeconds, 0, 1);
        return (int)Math.Round((row.FromY - top) * (1 - Ease(k)));
    }

    private static double Ease(double k) => k * k * (3 - (2 * k));

    private void PaintParty(CapSheet p, CapPlaqueData data, CapSeaPalette pal, (CapMood Now, CapMood Before, double Blend) mood, CapAnim? anim, double t, double phase,
        IReadOnlyList<CapDeathTurn> deaths, CapTrialTurn? trial, IReadOnlyList<int> order)
    {
        var ground = trial is null ? Layout.Rows : CapSign.Where(Layout, data, _context, _fossils)?.Ground ?? Layout.Rows;

        // Singleplayer (2026-10-13): no buoy with the cap, no tag under it, no sounding line; the level turns gold at the cap and that is all.
        var graves = data.Graves?.Count ?? 0;

        for (var slot = 0; slot < _creatures.Length; slot++)
        {
            var creature = _creatures[slot];
            if (slot >= order.Count || creature.Member is not { } m)
            {
                PaintVacant(p, creature, pal, phase);
                continue;
            }

            // The slot shows the party's member i (the game's order, the fallen whose death has been shown left out).
            var i = order[slot];

            // A fallen one is drawn until its death's turn comes; from then on its death paints it.
            var turn = deaths.FirstOrDefault(d => d.Index == i);
            if (turn is not null ? !data.DeathScenes || t >= turn.Start : m.Fallen && anim?.AwaitingFall(i, t) != true) continue;

            // While a trial plays, its level keeps the old cap until the cap's last wave passes it.
            var cap = CapTrial.CapOf(trial, data.Cap, creature.Slot.Info.Top + 3, ground);
            if (cap != _builtCap[slot])
            {
                _builtCap[slot] = cap;
                creature.Build(m, _writing, cap, Layout.Mode != CapMode.Column);
            }

            // A row that moved up (one above it gone) or down (the order changed in the game) slides there: drawn apart, then put in place.
            var dy = Slide(m, slot, t, anim is not null);
            var target = p;
            if (dy != 0)
            {
                _rowSheet ??= new CapSheet(p.W, p.H);
                if (_rowSheet.W != p.W || _rowSheet.H != p.H) _rowSheet = new CapSheet(p.W, p.H);
                _rowSheet.Clear();
                target = _rowSheet;
            }

            PaintRow(target, data, pal, anim, t, phase, trial, ground, graves, creature, m, i);
            if (dy != 0) p.Blit(_rowSheet!, 0, dy);
        }
    }

    private void PaintRow(CapSheet p, CapPlaqueData data, CapSeaPalette pal, CapAnim? anim, double t, double phase, CapTrialTurn? trial, int ground, int graves,
        CapCreature creature, CapPlaqueMember m, int i)
    {
        {

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
