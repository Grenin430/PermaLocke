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
        GameLinkMonitor monitor, AppSettings settings, PokemonSpriteService sprites, AlolaSky sky, ILogger<CapBadge> logger)
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
        _timer.Tick += (_, _) => _ = TickAsync();
        _frames.Tick += (_, _) => Frame();
        monitor.RunDataChanged += (_, _) => _readAt = DateTime.MinValue;
        runContext.CurrentChanged += (_, _) => _readAt = DateTime.MinValue;
    }

    public void Start()
    {
        _timer.Start();
        _frames.Start();
    }

    private double Now => _clock.Elapsed.TotalSeconds;

    /// <summary><c>--ensayar-cap</c>: the panel over PermaLocke's own window for ten seconds, without the game.</summary>
    public async Task RehearseAsync()
    {
        await _sprites.PrepareAsync();
        _reading = _runContext.Current is { } run ? await ReadAsync(run) : null;
        var reading = (_reading ?? new CapReading("PRUEBA 3 DE 12", 24, 26, null, 2, 12)) with { Highest = 22 };
        _shown = reading;
        _partyKey = string.Empty;
        ShowParty(
        [
            new(0, 25, "Pikachu", "Chispas", 22, 48, 52, false, 1, 0, "", ""),
            new(1, 722, "Rowlet", "", 21, 20, 55, false, 2, 0, "", ""),
            new(2, 19, "Rattata", "Pepe", 14, 0, 40, false, 3, 0, "", ""),
            new(3, 131, "Lapras", "", 24, 9, 90, true, 4, 0, "", "")
        ]);
        var main = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
        if (GameWindow.ClientBox(main) is not { } box) return;

        Apply(new CapRect(box.Left, box.Top, box.Width, box.Height), AzaharScreenLayout.Default, Dpi());
        await Task.Delay(TimeSpan.FromSeconds(10));
        HideAll();
    }

    private async Task TickAsync()
    {
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
            }

            if (_reading is not { } reading)
            {
                HideAll();
                return;
            }

            // El nivel del equipo se mira cada vuelta: sube en mitad de un combate y la barra lo sigue.
            _shown = reading with { Highest = Highest() ?? reading.Highest };
            _battle = _monitor.BattleNow.Count > 0;
            ShowParty(_monitor.Latest is { Connected: true } live ? Delayed(WithLiveHp(live.Party, _monitor.BattleNow)) : []);
            Apply(new CapRect(picture.Left, picture.Top, picture.Width, picture.Height), ScreenLayout(), Dpi());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido poner el cap encima del juego");
            HideAll();
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

        var options = new CapOptions();
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
        var data = new CapPlaqueData(reading.Trial, reading.Cap, reading.Next, reading.Highest, _party, _battle, null, reading.Cleared, reading.Trials);
        _data = data;

        Show(ref _main, CapPanelKind.Party, plan.Main, data, plan.Memorial is null);
        Show(ref _memorial, CapPanelKind.Memorial, plan.Memorial, data, false);
    }

    private void Show(ref Panel? panel, CapPanelKind kind, CapSlot? slot, CapPlaqueData data, bool gravesInside)
    {
        if (slot is null)
        {
            panel?.Window.Hide();
            return;
        }

        panel ??= Create(kind);
        panel.Plaque.Set(data, slot.Mode, slot.Cols, slot.Rows, slot.Cell, gravesInside, Now);
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
        var now = Now;
        if (_main is { Window.IsVisible: true } main) main.Plaque.Tick(now);
        if (_memorial is { Window.IsVisible: true } memorial) memorial.Plaque.Tick(now);
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
        var key = cap + "|" + string.Join(";", party.Select(p =>
            $"{p.Pid}:{p.Species}:{p.Form}:{p.Level}:{p.CurrentHp}/{p.MaxHp}:{p.IsFainted}:{p.Nickname}:{p.IsEgg}"));
        if (key == _partyKey)
        {
            return;
        }

        _partyKey = key;
        _party = [.. party.OrderBy(p => p.Slot).Select(member => new CapPlaqueMember(
            member.IsEgg ? _sprites.GetEgg() : _sprites.Get(member.Species, member.Form, member.IsShiny),
            string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname,
            member.Level, member.CurrentHp, member.MaxHp, member.IsEgg, member.Pid))];
    }

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

    /// <summary>How far behind the battle's own numbers the panel shows HP (1.0.4.9 bis).</summary>
    /// <remarks>
    /// The battle table changes when the game decides the hit, at «¡X ha usado Y!», before the attack's animation and the
    /// game's own bar: shown at once, the panel spoiled the damage. Three seconds behind, it drops after the game's bar.
    /// </remarks>
    private static readonly TimeSpan HpDelay = TimeSpan.FromSeconds(3);

    /// <summary>What each Pokémon's HP was <see cref="HpDelay"/> ago, by PID.</summary>
    private readonly Dictionary<uint, Queue<(DateTime At, int Hp)>> _hpHistory = [];

    /// <summary>Each member with the HP it had <see cref="HpDelay"/> ago, so the panel never runs ahead of the game.</summary>
    private IReadOnlyList<LivePartyMember> Delayed(IReadOnlyList<LivePartyMember> party)
    {
        var now = DateTime.UtcNow;
        var shown = new List<LivePartyMember>(party.Count);

        foreach (var member in party)
        {
            if (!_hpHistory.TryGetValue(member.Pid, out var history))
            {
                _hpHistory[member.Pid] = history = new Queue<(DateTime, int)>();
            }

            if (history.Count == 0 || history.Last().Hp != member.CurrentHp)
            {
                history.Enqueue((now, member.CurrentHp));
            }

            // Se descartan los valores viejos dejando siempre el último que ya tiene más de tres segundos.
            while (history.Count > 1 && now - history.ElementAt(1).At >= HpDelay)
            {
                history.Dequeue();
            }

            shown.Add(member with { CurrentHp = Math.Min(history.Peek().Hp, member.MaxHp) });
        }

        return shown;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
