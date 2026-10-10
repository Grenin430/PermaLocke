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
public sealed record CapReading(string Trial, int Cap, int? Next, int? Highest, int Cleared = 0, int Trials = 0);

/// <summary>
/// The level cap, always on screen over the emulator (1.0.4.7): since 2026-10-10 a walnut showcase that fills the black band beside the
/// 3DS screens, in the mode that fits it, with the party in a niche each and, if the other band has room, a memorial of the fallen.
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
    private readonly BarSync _bar;
    private readonly HpPresenter _hp = new();
    private readonly DispatcherTimer _due = new();
    private readonly DispatcherTimer _timer = new() { Interval = Follow };
    private readonly DispatcherTimer _frames = new() { Interval = Frames };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>The party, one niche per Pokémon (1.0.4.8).</summary>
    private IReadOnlyList<CapPlaqueMember> _party = [];
    private readonly PokemonSpriteService _sprites;
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
        IMoveCatalog moves, CemeteryService cemetery, VitalsPump pump, BarSync bar, ILogger<CapBadge> logger)
    {
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
        _cemetery = cemetery;
        _pump = pump;
        _bar = bar;
        pump.Changed += OnVitals;
        bar.Signal += OnBar;
        _due.Tick += (_, _) =>
        {
            _due.Stop();
            Repaint();
        };
        _timer.Tick += (_, _) => _ = TickAsync();
        _frames.Tick += (_, _) => Frame();
        monitor.RunDataChanged += (_, _) => _readAt = DateTime.MinValue;
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
    /// <c>--ensayar-cap [escenario] [segundos]</c>: the panel with no real party, from a scenario of the lab (<see cref="CapScenarios"/>:
    /// completo, caidos, huevos, combate, tope, inicio, liga, subida, golpe, caida, curacion, cambio-cap, eclosion, estados, casi-tope) on a loop.
    /// With the game open it goes over its real band, with the real layout, whichever window has the focus; without it, over PermaLocke's own
    /// window. Thirty seconds by default. Reads nothing of the run and writes nothing: only the panel is shown.
    /// </summary>
    public async Task RehearseAsync(string? name = null, double seconds = 30)
    {
        await _sprites.PrepareAsync();
        var scenarios = CapScenarios.All(species => _sprites.Get(species, 0, false));
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

            if (game == IntPtr.Zero || GetForegroundWindow() != game
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

    /// <summary>The fast reading found a change: the interface hears of it at once and does not wait for its half-second timer.</summary>
    private void OnVitals(VitalsFrame frame)
    {
        // One push waiting at most: a hundred changes a second cost one repaint a frame, the last one.
        if (Interlocked.Exchange(ref _pushing, 1) == 1) return;
        _ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            Interlocked.Exchange(ref _pushing, 0);
            Repaint();
        });
    }

    /// <summary>The bar of the game started to move: the oldest change held back is shown now.</summary>
    private void OnBar(BarSignal signal)
    {
        if (signal.Kind != BarSignalKind.Started) return;
        var ago = (Stopwatch.GetTimestamp() - signal.At) / (double)Stopwatch.Frequency;
        _ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            _hp.BarStarted(Now - ago);
            Repaint();
        });
    }

    /// <summary>Shows the party as it is now, at once: a change of the fast reading, the bar of the game, or the time of a held change coming.</summary>
    private void Repaint()
    {
        try
        {
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
        return new CapReading(label, stage.Level, next, Highest(), cleared, trials);
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
            reading.Trials, _sky.IsKnown ? _sky.Hour : -1, seed);
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
    }

    private void HideAll()
    {
        _main?.Window.Hide();
        _memorial?.Window.Hide();
    }

    /// <summary>One frame of the moving part, for each window that is showing.</summary>
    private void Frame()
    {
        var busy = Step();

        // Twelve frames a second for the flame and the dust. While something is happening the frames come with the screen, sixty a second, from the
        // render loop of WPF: a timer cannot (it wakes on the 15.6 ms tick, so the 40 ms of «fast» was really 47).
        if (busy && !_rendering)
        {
            _rendering = true;
            _frames.Stop();
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private bool _rendering;

    private void OnRendering(object? sender, EventArgs e)
    {
        if (Step()) return;

        // Nothing is moving any more: back to the slow beat.
        CompositionTarget.Rendering -= OnRendering;
        _rendering = false;
        _frames.Start();
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
            $"{p.Pid}:{p.Species}:{p.Form}:{p.Level}:{p.CurrentHp}/{p.MaxHp}:{p.IsFainted}:{p.Nickname}:{p.IsEgg}:{p.HeldItem}:{p.Status}:{p.ExpProgress:F2}:{string.Join(",", p.Pp ?? [])}"));
        if (key == _partyKey)
        {
            return;
        }

        _partyKey = key;
        _party = [.. party.OrderBy(p => p.Slot).Select(member => ToMember(member, details))];
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
            member.HeldItem > 0 ? _items.GetName(member.HeldItem) : null, _battle ? CapStatus.None : StatusOf(member.Status),
            member.ExpProgress, moves, member.HeldItem > 0 ? _sprites.GetItem(member.HeldItem) : null);
    }

    /// <summary>The status byte of a generation 7 party Pokémon: bits 0-2 the turns of sleep, then poison, burn, freeze, paralysis and bad poison.</summary>
    public static CapStatus StatusOf(int status) =>
        (status & 7) != 0 ? CapStatus.Sleep
        : (status & 0x88) != 0 ? CapStatus.Poison
        : (status & 0x10) != 0 ? CapStatus.Burn
        : (status & 0x20) != 0 ? CapStatus.Freeze
        : (status & 0x40) != 0 ? CapStatus.Paralysis
        : CapStatus.None;

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
