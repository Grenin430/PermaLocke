using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Vitals;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>What the cap panel shows.</summary>
/// <param name="Trial">«PRUEBA 3 DE 12», or «LIGA», «REMATCH».</param>
/// <param name="Next">The cap after this stage, or null at the last one.</param>
/// <param name="Highest">The highest level in the party right now, or null without a reading.</param>
/// <param name="Cleared">How many stages the player has cleared.</param>
/// <param name="Trials">How many trials there are in all.</param>
/// <param name="CrystalItem">The Z-Crystal of the last stage cleared (its item id, from its achievement), 0 when it has none.</param>
public sealed record CapReading(string Trial, int Cap, int? Next, int? Highest, int Cleared = 0, int Trials = 0, int CrystalItem = 0);

/// <summary>
/// The level cap, always on screen over the emulator (1.0.4.7): since 2026-10-10 a sea that fills the black band beside the 3DS screens, in the
/// mode that fits it, with the party as creatures of light and the cap on a buoy and, if the other band has room, the same sea seen from the other
/// side, with the fallen as fossils and no letters. Since 2026-10-11 it reads the life of the party on a thread of its own (<see cref="VitalsPump"/>)
/// and shows each change when the game does (<see cref="HpPresenter"/>, <see cref="BarSync"/>).
/// </summary>
/// <remarks>
/// <para>
/// The band is found from the picture's box (<see cref="GameWindow.RenderBox"/>) and the layout of the screens in the emulator's
/// <c>qt-config.ini</c> (<see cref="CapGeometry"/>); <see cref="CapBandGuard"/> checks on the screen that the game is not in it, because
/// F8, F9 and F10 change the layout without the file knowing. Nothing is ever drawn over the game: a layout that is not computed, or one
/// the guard doubts, hides the panel.
/// </para>
/// <para>
/// It follows the emulator's window every half second, goes when the game closes or another window comes in front, and reads the cap
/// again every ten seconds and whenever the run changes. Click-through, like every overlay.
/// </para>
/// </remarks>
public sealed class CapBadge
{
    private static readonly TimeSpan Follow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Reread = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Frames = TimeSpan.FromMilliseconds(83);
    private static readonly TimeSpan Doubt = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Verify = TimeSpan.FromSeconds(1.5);

    private readonly EmulatorLauncher _launcher;
    private readonly ProgressService _progress;
    private readonly LevelCapTable _caps;
    private readonly GameLinkMonitor _monitor;
    private readonly IRunContext _runContext;
    private readonly ILogger<CapBadge> _logger;
    private readonly AppSettings _settings;
    private readonly AlolaSky _sky;
    private readonly ITypeLookup _types;
    private readonly IItemLookup _items;
    private readonly IMoveCatalog _moves;
    private readonly CemeteryService _cemetery;
    private readonly VitalsPump _pump;
    private readonly ActiveBar _active;
    private readonly Dictionary<uint, int> _rawStatus = [];
    private readonly BarSync _bar;
    private readonly HpPresenter _hp = new();
    private readonly CapEventFeed _feed = new() { FallsFromLife = false };
    private IReadOnlyList<CapEvent> _events = [];
    private double _eventAt;
    private long _epoch;
    private readonly DispatcherTimer _due = new();
    private readonly DispatcherTimer _timer = new() { Interval = Follow };
    private readonly DispatcherTimer _frames = new() { Interval = Frames };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>The party, one niche per Pokémon (1.0.4.8).</summary>
    private IReadOnlyList<CapPlaqueMember> _party = [];
    private readonly PokemonSpriteService _sprites;
    private readonly IAbilityLookup _abilities;
    private readonly IAchievementCatalog _achievements;
    private string _partyKey = string.Empty;
    private CapPlaqueData? _data;
    private CapReading? _shown;
    private bool _battle;
    private bool _rehearsing;
    private CapPlaqueData? _rehearsal;
    private IReadOnlyList<CapGrave> _graves = [];
    private CapRect _picture;
    private int _fold;
    private bool _keyWas;

    private Panel? _main;
    private Panel? _memorial;
    private CapReading? _reading;
    private DateTime _readAt = DateTime.MinValue;

    private (string Path, DateTime Stamp, AzaharScreenLayout Layout)? _layout;
    private CapMode? _lastMode;
    private DateTime _doubtUntil = DateTime.MinValue;
    private DateTime _verifiedAt = DateTime.MinValue;

    /// <summary>A window and the plaque in it: the party's, or the memorial's.</summary>
    private sealed record Panel(Window Window, CapPlaque Plaque);

    public CapBadge(EmulatorLauncher launcher, ProgressService progress, LevelCapTable caps, IRunContext runContext,
        GameLinkMonitor monitor, AppSettings settings, PokemonSpriteService sprites, AlolaSky sky, ITypeLookup types, IItemLookup items,
        IMoveCatalog moves, CemeteryService cemetery, VitalsPump pump, BarSync bar, ActiveBar active, IAbilityLookup abilities, IAchievementCatalog achievements,
        ILogger<CapBadge> logger)
    {
        _achievements = achievements;
        _launcher = launcher;
        _progress = progress;
        _caps = caps;
        _monitor = monitor;
        _runContext = runContext;
        _logger = logger;
        _settings = settings;
        _sprites = sprites;
        _sky = sky;
        _types = types;
        _items = items;
        _moves = moves;
        _abilities = abilities;
        _cemetery = cemetery;
        _pump = pump;
        _active = active;
        _bar = bar;
        pump.Changed += OnVitals;
        active.Moved += OnActive;
        bar.Signal += OnBar;
        _due.Tick += (_, _) =>
        {
            _due.Stop();
            Repaint();
        };
        _timer.Tick += (_, _) => _ = TickAsync();
        _frames.Tick += (_, _) => Frame();
        monitor.RunDataChanged += (_, _) => _readAt = DateTime.MinValue;

        // The creature sinks when the run records the death, not when the life reads zero: only once the PermaLocke has started (the first Poké Ball),
        // and after the game has shown the faint (singleplayer, 2026-10-13).
        monitor.PokemonDied += (_, fallen) =>
        {
            if (fallen.Pid == 0) return;
            _ = Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                _events = _feed.Fall(fallen.Pid, Now);
                Repaint();
            });
        };
        runContext.CurrentChanged += (_, _) => _readAt = DateTime.MinValue;
    }

    public void Start()
    {
        // The bar of the game is looked for where the emulator's own layout puts it, not only in the default one.
        HpBarWatcher.LayoutSource = ScreenLayout;
        _pump.Start();
        _timer.Start();
        _frames.Start();
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>
    /// <c>--ensayar-cap [escenario] [segundos]</c>: the panel with no real party, from a scenario of the lab (<see cref="CapScenarios"/>: captura, completo,
    /// caidos, huevos, combate, tope, inicio, liga, subida, golpe, caida, curacion, cambio-cap, eclosion, estados, casi-tope, batalla, luto, noche, amanecer,
    /// atardecer, nadador, nadador-raro, nadador-leyenda, volador, volador-raro, volador-leyenda, estrella, ev-golpe, ev-cura, ev-antidoto, ev-curas, ev-revivir, ev-estado, ev-objeto, ev-movimiento) on a loop.
    /// With the game open it goes over its real band, with the real layout, whichever window has the focus; without it, over PermaLocke's own
    /// window. Thirty seconds by default. Reads nothing of the run and writes nothing: only the panel is shown.
    /// </summary>
    public async Task RehearseAsync(string? name = null, double seconds = 30)
    {
        await _sprites.PrepareAsync();
        var scenarios = CapScenarios.All(species => _sprites.Get(species, 0, false), _sprites.GetItem);
        var scenario = scenarios.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? scenarios.First(s => s.Name == "completo");
        _logger.LogInformation("Ensayo del cap: {Scenario} durante {Seconds} s", scenario.Name, seconds);

        _rehearsing = true;
        _frames.Start();
        try
        {
            var started = Now;
            while (Now - started < seconds)
            {
                var game = GameWindow.Handle();
                CapRect? picture = game != IntPtr.Zero && GameWindow.RenderBox(game) is { } box ? new CapRect(box.Left, box.Top, box.Width, box.Height) : null;
                var layout = AzaharScreenLayout.Default;
                if (picture is null)
                {
                    var main = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                    if (GameWindow.ClientBox(main) is not { } own) break;
                    picture = new CapRect(own.Left, own.Top, own.Width, own.Height);
                }
                else
                {
                    layout = ScreenLayout();
                }

                // The scenarios loop: after the last moment there is a quiet second, and it starts again.
                var t = (Now - started) % (scenario.Length + 1);
                var data = scenario.At(t);
                _rehearsal = data;
                _shown = new CapReading(data.Trial, data.Cap, data.Next, data.Highest, data.Cleared, data.Trials);
                _party = data.Party;
                _battle = data.InBattle;
                _graves = data.Graves ?? [];
                Apply(picture.Value, layout, Dpi());
                await Task.Delay(40);
            }
        }
        finally
        {
            _rehearsing = false;
            _rehearsal = null;
            _party = [];
            _graves = [];
            HideAll();
        }
    }

    private async Task TickAsync()
    {
        // A rehearsal drives the panel itself, with the game in front or not.
        if (_rehearsing) return;

        try
        {
            // Se quita en CONFIGURACIÓN (1.0.4.8). Lo barato primero: sin juego abierto no se busca su ventana.
            var game = _settings.Current.CapPanel && _launcher.IsRunning ? GameWindow.Handle() : IntPtr.Zero;
            _gameHandle = game;

            // Fijo (CONFIGURACIÓN), otra ventana delante del juego no lo esconde: solo el juego cerrado o minimizado.
            if (game == IntPtr.Zero || (!_settings.Current.CapPinned && GetForegroundWindow() != game)
                || GameWindow.RenderBox(game) is not { } picture)
            {
                HideAll();
                return;
            }

            if (DateTime.UtcNow - _readAt > Reread && _runContext.Current is { } run)
            {
                _readAt = DateTime.UtcNow;
                _reading = await ReadAsync(run);
                await _sprites.PrepareAsync();
                _graves = await ReadGravesAsync();
            }

            if (_reading is not { } reading)
            {
                HideAll();
                return;
            }

            // El nivel del equipo se mira cada vuelta: sube en mitad de un combate y la barra lo sigue.
            _shown = reading with { Highest = Highest() ?? reading.Highest };
            _battle = _monitor.BattleNow.Count > 0;
            _eventAt = Now;
            TakeNewEpoch();
            ShowParty(LiveParty());
            _picture = new CapRect(picture.Left, picture.Top, picture.Width, picture.Height);
            Apply(_picture, ScreenLayout(), Dpi());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido poner el cap encima del juego");
            HideAll();
        }
    }

    /// <summary>
    /// The party to show: the full reading of every second (names, moves, the things that do not change), with the HP, the status and the item of
    /// the fast reading over it when there is one, and in a battle the HP of the battle's blocks as the fast reading last saw them.
    /// </summary>
    private IReadOnlyList<LivePartyMember> LiveParty()
    {
        if (_monitor.Latest is not { Connected: true } live) return [];
        var frame = _pump.Latest;
        var party = live.Party;

        // The fast reading of the party stands in for the slow one only for a Pokémon it agrees on by PID; anything else keeps what the slow one said.
        if (frame is { Party.Count: > 0 })
        {
            party = [.. party.Select(member => frame.Party.FirstOrDefault(s => s.Pid != 0 && s.Pid == member.Pid) is { Pid: not 0 } sample && sample.Species == member.Species
                ? member with { CurrentHp = sample.Hp, MaxHp = sample.MaxHp, Status = sample.Status, HeldItem = sample.HeldItem }
                : member)];
        }

        foreach (var member in party)
        {
            if (_rawStatus.TryGetValue(member.Pid, out var raw) && raw == member.Status) continue;
            if (_rawStatus.ContainsKey(member.Pid) || member.Status != 0)
                _logger.LogInformation("Estado de {Name} (PID {Pid:X8}): valor del juego 0x{Raw:X8}, el panel lo lee como {Shown}", member.Nickname, member.Pid, member.Status, StatusOf(member.Status));
            _rawStatus[member.Pid] = member.Status;
        }

        // In the game's order (2026-10-15): the slots in memory are not it once the player swaps two in the game's menu.
        if (live.Order is { Count: > 0 } order)
        {
            var ranks = order.ToList();
            party = [.. party.OrderBy(member => ranks.IndexOf(member.Pid) is var at && at >= 0 ? at : 6 + member.Slot)];
        }

        var tables = frame is { Battle.Count: > 0 } ? frame.ToTables(_monitor.BattleNow) : _monitor.BattleNow;
        return Present(WithLiveHp(party, tables));
    }

    /// <summary>
    /// What life to show for each of them: the real one, held back or not as the chosen mode says (<see cref="HpPresenter"/>). A change read in a battle is
    /// held until the game's bar starts to move, or until the delay worked out from the damage when the bar cannot be seen.
    /// </summary>
    private IReadOnlyList<LivePartyMember> Present(IReadOnlyList<LivePartyMember> party)
    {
        _hp.Mode = (HpSyncMode)Math.Clamp(_settings.Current.CapHpSync, 0, 2);
        var now = Now;
        var arm = false;
        foreach (var member in party)
        {
            if (!member.IsEgg) arm |= _hp.Observe(member.Pid, member.CurrentHp, member.MaxHp, now, _battle);
        }

        if (!_battle) _hp.Flush();
        _hp.Keep([.. party.Select(member => member.Pid)]);
        if (arm) _bar.Arm();

        var shown = party.Select(member => member.IsEgg ? member : member with { CurrentHp = Math.Min(_hp.Shown(member.Pid, now), member.MaxHp) }).ToList();

        // A change that is held has a time of its own to come out, if the bar never says.
        if (_hp.NextDue is { } due && _hp.HasWaiting)
        {
            _due.Interval = TimeSpan.FromSeconds(Math.Clamp(due - now, 0.01, 10));
            _due.Start();
        }

        return shown;
    }

    private int _pushing;

    /// <summary>The bar of the Pokémon on the field moved: the sea may change mood now.</summary>
    private void OnActive()
    {
        if (Interlocked.Exchange(ref _pushing, 1) == 1) return;
        _ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            Interlocked.Exchange(ref _pushing, 0);
            Repaint();
        });
    }

    /// <summary>The fast reading found a change: the interface hears of it at once and does not wait for its half-second timer.</summary>
    private void OnVitals(VitalsFrame frame)
    {
        // One push waiting at most: a hundred changes a second cost one repaint a frame, the last one.
        if (Interlocked.Exchange(ref _pushing, 1) == 1) return;
        var ago = (Stopwatch.GetTimestamp() - frame.ReadAt) / (double)Stopwatch.Frequency;
        _ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            Interlocked.Exchange(ref _pushing, 0);
            Repaint(Now - ago);
        });
    }

    /// <summary>The life of the Pokémon on the field, from the game's bar, in steps of five per cent (so that the data do not change at every look); null out of a battle or when the bar is not seen.</summary>
    private double? ActiveShare()
    {
        _active.Follow(_battle);
        return _battle && _active.Current() is { } fill ? Math.Round(fill * 20) / 20 : null;
    }

    /// <summary>The fast reading started again from nothing (the game came back): what is read next is taken as it is, nothing is announced.</summary>
    private void TakeNewEpoch()
    {
        if (_pump.Epoch == _epoch) return;
        _epoch = _pump.Epoch;
        _feed.Rebaseline();
        _hp.Reset();
    }

    /// <summary>The bar of the game started to move: the oldest change held back is shown now.</summary>
    private void OnBar(BarSignal signal)
    {
        if (signal.Kind != BarSignalKind.Started) return;
        var ago = (Stopwatch.GetTimestamp() - signal.At) / (double)Stopwatch.Frequency;
        _ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            _hp.BarStarted(Now - ago);
            Repaint(Now - ago);
        });
    }

    /// <summary>Shows the party as it is now, at once: a change of the fast reading, the bar of the game, or the time of a held change coming.</summary>
    private void Repaint(double? at = null)
    {
        try
        {
            _eventAt = at ?? Now;
            TakeNewEpoch();
            if (_rehearsing || _shown is null || _picture.Width == 0 || (_main?.Window.IsVisible != true && _memorial?.Window.IsVisible != true)) return;
            _battle = _monitor.BattleNow.Count > 0;
            ShowParty(LiveParty());
            Apply(_picture, ScreenLayout(), Dpi());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido repintar el cap");
        }
    }

    private async Task<CapReading?> ReadAsync(Run run)
    {
        var cleared = await _progress.ClearedAsync(run);
        if (_caps.Current(cleared) is not { } stage)
        {
            return null;
        }

        var trials = _caps.Stages.Count(s => s.Id.StartsWith("trial-", StringComparison.Ordinal));
        var label = stage.Id.StartsWith("trial-", StringComparison.Ordinal) ? $"PRUEBA {stage.Order} DE {trials}"
            : stage.Id == "rematch" ? "REMATCH" : stage.Name.ToUpperInvariant();
        var next = cleared + 1 < _caps.Stages.Count ? _caps.Stages[cleared + 1].Level : (int?)null;
        // the crystal of the stage just cleared, for the trial film (the league and the rematch have none)
        var last = cleared > 0 && cleared <= _caps.Stages.Count ? _caps.Stages.OrderBy(s => s.Order).ElementAt(cleared - 1) : null;
        var crystal = last?.Achievement is { } id && _achievements.All.FirstOrDefault(a => a.Id == id)?.Item is { } item and >= 807 and <= 824 ? item : 0;
        return new CapReading(label, stage.Level, next, Highest(), cleared, trials, crystal);
    }

    /// <summary>The run's fallen, the latest first, with the icon of each; empty when the run's record cannot be read.</summary>
    private async Task<IReadOnlyList<CapGrave>> ReadGravesAsync()
    {
        try
        {
            var graves = await _cemetery.GravesAsync();
            return [.. graves.OrderBy(g => g.DiedAt ?? DateTimeOffset.MinValue)
                .Select(g => new CapGrave(_sprites.Get(g.Species, g.Form, g.IsShiny), g.Name, g.FellTo, g.Level,
                    g.DiedAt is { } died ? Math.Max(0, (int)(DateTimeOffset.UtcNow - died).TotalDays) : 0))];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido leer el cementerio para el panel del cap");
            return [];
        }
    }

    private int? Highest() =>
        _monitor.Latest is { Connected: true, Party.Count: > 0 } snapshot ? snapshot.Party.Max(member => member.Level) : null;

    /// <summary>The layout of the screens, from the qt-config.ini of the emulator that runs; the file is only read again when it changes.</summary>
    private AzaharScreenLayout ScreenLayout()
    {
        try
        {
            var path = _sky.ConfigPath();
            var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_layout is { } known && known.Path == path && known.Stamp == stamp) return known.Layout;

            var layout = AzaharScreenLayout.Read(path);
            _layout = (path, stamp, layout);
            return layout;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return _layout?.Layout ?? AzaharScreenLayout.Default;
        }
    }

    private double Dpi() => _main is { } panel ? VisualTreeHelper.GetDpi(panel.Window).DpiScaleX : 1.0;

    // ---------------------------------------------------------------- the windows

    /// <summary>Plans the band, checks it, and puts each window where its slot says (or hides it).</summary>
    private void Apply(CapRect picture, AzaharScreenLayout layout, double dpi)
    {
        if (_shown is not { } reading)
        {
            HideAll();
            return;
        }

        var current = _settings.Current;

        // The key folds the panel: whole, then compact, then away.
        if (_fold == 2)
        {
            HideAll();
            return;
        }

        CapMode? forced = current.CapSize switch { 1 => CapMode.Compact, 2 => CapMode.Slim, 3 => CapMode.Column, _ => null };
        if (_fold == 1) forced = CapMode.Compact;
        var options = new CapOptions(forced, current.CapMaxCell, current.CapMemorial);
        var plan = CapGeometry.Plan(picture, layout, dpi, options, _lastMode);

        // The layout that the file says may not be the one on screen (F8, F9 and F10 do not write it until Azahar closes).
        if (DateTime.UtcNow < _doubtUntil)
        {
            HideAll();
            return;
        }

        if (plan.Main is not null && DateTime.UtcNow - _verifiedAt > Verify)
        {
            _verifiedAt = DateTime.UtcNow;
            if (CapBandGuard.LooksRight(plan, picture) == false)
            {
                _doubtUntil = DateTime.UtcNow + Doubt;
                _logger.LogWarning("El juego se ve donde la configuración de Azahar dice que hay banda ({Why}): el panel del cap se esconde {Seconds} s",
                    plan.Why, Doubt.TotalSeconds);
                HideAll();
                return;
            }
        }

        _lastMode = plan.Main?.Mode;
        var seed = _runContext.Current is { } activeRun ? activeRun.Id.GetHashCode() & 0x7FFFFFFF : 0;
        var data = _rehearsal ?? new CapPlaqueData(reading.Trial, reading.Cap, reading.Next, reading.Highest, _party, _battle, _graves, reading.Cleared,
            reading.Trials, _sky.IsKnown ? _sky.Hour : -1, seed, _events, ActiveShare(), _settings.Current.DeathScene,
            reading.CrystalItem, reading.CrystalItem > 0 ? _sprites.GetItem(reading.CrystalItem) : null);
        _data = data;

        // One sea in two windows.
        var (partyX, deepX, world) = CapGeometry.WorldOf(plan);
        Show(ref _main, CapPanelKind.Party, plan.Main, data, partyX, world);
        Show(ref _memorial, CapPanelKind.Deep, plan.Companion, data, deepX, world);
    }

    private void Show(ref Panel? panel, CapPanelKind kind, CapSlot? slot, CapPlaqueData data, int worldX, int worldWidth)
    {
        if (slot is null)
        {
            panel?.Window.Hide();
            return;
        }

        panel ??= Create(kind);
        panel.Plaque.Animated = _settings.Current.CapAnimations && !PermaLocke.App.Views.Pixel.PixelTheme.ReducedMotion;
        panel.Plaque.Set(data, slot, Now, worldX, worldWidth);
        OverlayWindows.PlaceOver(panel.Window, (slot.Box.Left, slot.Box.Top, slot.Box.Width, slot.Box.Height));
        if (!panel.Window.IsVisible) panel.Window.Show();
        EnsureRendering();
    }

    private void HideAll()
    {
        _main?.Window.Hide();
        _memorial?.Window.Hide();
    }

    /// <summary>One frame of the moving part, for each window that is showing.</summary>
    /// <summary>The slow beat (a timer, 100 ms): it keeps the key and starts the render loop when a window shows; the frames themselves come from <see cref="OnRendering"/>.</summary>
    private void Frame()
    {
        if (!_rendering) Step();
        EnsureRendering();
    }

    private bool _rendering;
    private long _lastStep;
    private double _interval = 33;
    private double _cost;
    private IntPtr _front;
    private IntPtr _gameHandle;

    private bool AnyVisible() => _main?.Window.IsVisible == true || _memorial?.Window.IsVisible == true;

    private void EnsureRendering()
    {
        if (_rendering || !AnyVisible()) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// The frames, at the pace of the screen's own render loop and not of a timer (a timer wakes on the 15.6 ms tick of the system: 83 ms was 94, 40 was 47, and
    /// the panel stuttered). Thirty a second at rest, sixty while something moves, and half of that if a frame is costing the interface too much: a layered
    /// window is expensive to update, and the panel must never be what makes the interface late. Each beat also asks who has the focus, which is cheap,
    /// so that the panel goes the moment another window comes in front and comes back the moment the game does.
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        CheckFront();
        var now = Stopwatch.GetTimestamp();
        if ((now - _lastStep) * 1000.0 / Stopwatch.Frequency < _interval) return;
        _lastStep = now;

        var busy = Step();
        var took = (Stopwatch.GetTimestamp() - now) * 1000.0 / Stopwatch.Frequency;
        _cost = (_cost * 0.8) + (took * 0.2);
        _interval = busy && _cost < 6 ? 16 : 33;

        if (!AnyVisible())
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }
    }

    /// <summary>Another window in front takes the panel away at once; the game in front again brings it back without waiting for the half-second beat.</summary>
    private void CheckFront()
    {
        var front = GetForegroundWindow();
        if (front == _front) return;
        _front = front;
        if (_rehearsing || _gameHandle == IntPtr.Zero) return;
        if (front != _gameHandle && !_settings.Current.CapPinned) HideAll();
        else if (!AnyVisible()) _ = TickAsync();
    }

    /// <summary>One frame of the moving part for each window that is showing; true while something in it needs frames at the screen's pace.</summary>
    private bool Step()
    {
        var now = Now;
        WatchKey();
        var busy = false;
        if (_main is { Window.IsVisible: true } main)
        {
            main.Plaque.Tick(now);
            busy |= main.Plaque.Busy(now);
        }

        if (_memorial is { Window.IsVisible: true } memorial)
        {
            memorial.Plaque.Tick(now);
            busy |= memorial.Plaque.Busy(now);
        }

        return busy;
    }

    /// <summary>
    /// The key that folds the panel. The windows take no clicks, so this looks at the state of the keyboard (as <see cref="GameInputWatch"/> does,
    /// with no hook) while the game has the focus, and acts on the key going down, once. Risk, said straight: a key held while the game
    /// gets the focus counts as a press, and a key another program also reads (Pausa is the one the emulator and Steam leave alone) does both.
    /// </summary>
    private void WatchKey()
    {
        var key = _settings.Current.CapKey;
        var down = key != 0 && GetForegroundWindow() is var front && front != IntPtr.Zero && front == GameWindow.Handle() && (GetAsyncKeyState(key) & 0x8000) != 0;
        var pressed = down && !_keyWas;
        _keyWas = down;
        if (!pressed) return;

        _fold = (_fold + 1) % 3;
        _logger.LogInformation("Cap del juego plegado con la tecla: {Fold}", _fold == 0 ? "completo" : _fold == 1 ? "reducido" : "escondido");
        if (_picture.Width > 0) Apply(_picture, ScreenLayout(), Dpi());
    }

    private Panel Create(CapPanelKind kind)
    {
        var plaque = new CapPlaque(kind);
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            UseLayoutRounding = true,
            Opacity = 0.97,
            Content = plaque
        };

        OverlayWindows.MakeUntouchable(window);
        return new Panel(window, plaque);
    }

    /// <summary>
    /// The party in the niches (1.0.4.8): each Pokémon's icon from the player's own ROM, its level (gold at the cap) and an
    /// HP bar green, yellow or red as the game colours it; a fallen one greyed out and crossed, with «KO». An egg, its
    /// name alone (§230). Rebuilt only when something on it changes, so it costs nothing while the player walks.
    /// </summary>
    private void ShowParty(IReadOnlyList<LivePartyMember> party)
    {
        var cap = _reading?.Cap ?? int.MaxValue;
        // El mote va en la clave: un mote votado entra en directo y, sin él, el panel seguía con el nombre de antes
        // (lista de pendientes del organizador, 2026-09-28).
        var details = _settings.Current.CapDetails;
        var key = cap + "|" + details + "|" + _battle + "|" + string.Join(";", party.Select(p =>
            $"{p.Pid}:{p.Species}:{p.Form}:{p.Level}:{p.CurrentHp}/{p.MaxHp}:{p.IsFainted}:{p.Nickname}:{p.IsEgg}:{p.HeldItem}:{p.Status}:{p.Ability}:{p.ExpProgress:F2}:{string.Join(",", p.Pp ?? [])}"));
        if (key == _partyKey)
        {
            return;
        }

        _partyKey = key;
        _party = [.. party.OrderBy(p => p.Slot).Select(member => ToMember(member, details))];

        // What happened to each of them, with the moment the news came (the reading, the bar of the game), not the moment it is painted.
        _events = _feed.Observe(new CapPlaqueData(string.Empty, 0, null, null, _party, _battle), _eventAt, Now).Events ?? [];
    }

    /// <summary>
    /// One Pokémon of the party for the panel. Everything beyond its name, level and HP is read, not guessed: the types are the installed
    /// world's, the item and the moves are the ones in its structure, the experience is measured on its own curve. The status is only
    /// shown out of battle, because in a battle the game keeps it in a copy this does not read (as with the HP, §99): showing the
    /// party's would be showing last battle's. An egg tells nothing (§230).
    /// </summary>
    private CapPlaqueMember ToMember(LivePartyMember member, bool details)
    {
        var sprite = member.IsEgg ? _sprites.GetEgg() : _sprites.Get(member.Species, member.Form, member.IsShiny);
        var name = string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname;
        if (member.IsEgg || !details)
        {
            return new CapPlaqueMember(sprite, name, member.Level, member.CurrentHp, member.MaxHp, member.IsEgg, member.Pid);
        }

        var pair = _types.GetTypes(member.Species, member.Form);
        var types = new List<int>();
        if (pair.First >= 0) types.Add(pair.First);
        if (pair.IsDual && pair.Second >= 0) types.Add(pair.Second);

        var moves = new List<CapMove>();
        for (var i = 0; i < (member.Moves?.Count ?? 0); i++)
        {
            var id = member.Moves![i];
            if (id <= 0) continue;
            var pp = member.Pp is { } left && i < left.Count ? left[i] : 1;
            var sheet = _moves.Describe(id);
            moves.Add(new CapMove(sheet?.Type ?? -1, pp <= 0, sheet?.Name));
        }

        return new CapPlaqueMember(sprite, name, member.Level, member.CurrentHp, member.MaxHp, false, member.Pid, types,
            member.HeldItem > 0 ? _items.GetName(member.HeldItem) : null, StatusOf(member.Status),
            member.ExpProgress, moves, member.HeldItem > 0 ? _sprites.GetItem(member.HeldItem) : null,
            member.Ability > 0 ? _abilities.GetName(member.Ability) : null);
    }

    /// <summary>
    /// The status of a party Pokémon as the game's structure in memory holds it: a number, not the bit field of the save file. A burnt Azumarill read as
    /// asleep (2026-10-11) because bits 0-2 were taken for the turns of sleep; the number is the game's own list, 1 paralysis, 2 sleep, 3 freeze, 4 burn,
    /// 5 poison. Only the low byte: the bytes above it are counters. The numbers are from that list and from that one burn; the log writes the raw value of
    /// every change (<c>Estado de ...</c>) so that each one can be checked against the screen, and any other value shows nothing rather than a guess.
    /// </summary>
    public static CapStatus StatusOf(int status) => (status & 0xFF) switch
    {
        1 => CapStatus.Paralysis,
        2 => CapStatus.Sleep,
        3 => CapStatus.Freeze,
        4 => CapStatus.Burn,
        5 => CapStatus.Poison,
        _ => CapStatus.None
    };

    /// <summary>
    /// In a battle, the party's HP from the battle's own copy (1.0.4.9): the game only copies it back to the party when
    /// the battle ends. Each member takes the player's block of its species (max HP breaks a tie); when that is not
    /// exactly one block, the member keeps what the party says rather than a guess.
    /// </summary>
    /// <remarks>
    /// <paramref name="bound"/> keeps each PID on the block it matched for the whole battle (1.0.5.1): a level up changes
    /// max HP in one copy before the other, and matching by max HP again lost the block for a second and showed the
    /// party's stale, full HP. Both HP and max HP come from the block.
    /// </remarks>
    public static IReadOnlyList<LivePartyMember> WithBattleHp(IReadOnlyList<LivePartyMember> party,
        IReadOnlyList<BattleTable> tables, Dictionary<uint, int>? bound = null)
    {
        if (tables.Count == 0)
        {
            bound?.Clear();
            return party;
        }

        var blocks = tables[0].Blocks.Where(block => block.IsPlayers && block.MaxHp > 0).ToList();
        return [.. party.Select(member =>
        {
            BattleBlock? block = null;

            if (bound is not null && bound.TryGetValue(member.Pid, out var id))
            {
                block = blocks.FirstOrDefault(b => b.BattleId == id && b.Species == member.Species);
            }

            if (block is null)
            {
                var match = blocks.Where(b => b.Species == member.Species).ToList();
                if (match.Count > 1) match = [.. match.Where(b => b.MaxHp == member.MaxHp)];
                if (match.Count == 1) block = match[0];
            }

            if (block is null)
            {
                return member;
            }

            if (bound is not null) bound[member.Pid] = block.BattleId;
            return member with { CurrentHp = Math.Clamp(block.CurrentHp, 0, block.MaxHp), MaxHp = block.MaxHp };
        })];
    }

    /// <summary>Which battle block each PID matched, for the battle in progress.</summary>
    private readonly Dictionary<uint, int> _bound = [];

    /// <summary>The last battle HP of each PID and the party's HP when it was read (1.0.5.1).</summary>
    private readonly Dictionary<uint, (int Hp, int MaxHp, int PartyHp, DateTime At)> _afterBattle = [];

    /// <summary>
    /// The party with the battle's HP, and just after a battle, the battle's last HP until the party copy catches up: the
    /// battle ends before the game writes it back, and in between the panel showed the HP from before the battle.
    /// </summary>
    private IReadOnlyList<LivePartyMember> WithLiveHp(IReadOnlyList<LivePartyMember> party, IReadOnlyList<BattleTable> tables)
    {
        var now = DateTime.UtcNow;

        if (tables.Count > 0)
        {
            var shown = WithBattleHp(party, tables, _bound);
            for (var i = 0; i < party.Count; i++)
            {
                if (!ReferenceEquals(shown[i], party[i]))
                    _afterBattle[party[i].Pid] = (shown[i].CurrentHp, shown[i].MaxHp, party[i].CurrentHp, now);
            }
            return shown;
        }

        _bound.Clear();
        return [.. party.Select(member =>
        {
            if (!_afterBattle.TryGetValue(member.Pid, out var last)) return member;

            // En cuanto el equipo cambia (el juego ya copió, o un Centro Pokémon), manda el equipo.
            if (member.CurrentHp != last.PartyHp || now - last.At > TimeSpan.FromSeconds(20))
            {
                _afterBattle.Remove(member.Pid);
                return member;
            }

            return member with { CurrentHp = last.Hp, MaxHp = last.MaxHp };
        })];
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
