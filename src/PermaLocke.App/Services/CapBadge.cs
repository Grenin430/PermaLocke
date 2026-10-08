using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Battle;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>What the cap panel shows.</summary>
/// <param name="Trial">«PRUEBA 3 DE 12», or «LIGA», «REMATCH».</param>
/// <param name="Next">The cap after this stage, or null at the last one.</param>
/// <param name="Highest">The highest level in the party right now, or null without a reading.</param>
public sealed record CapReading(string Trial, int Cap, int? Next, int? Highest);

/// <summary>
/// The level cap, always on screen over the emulator (1.0.4.7): a small pixel panel in the top right of the picture, in
/// the black band beside the 3DS screens, while the game is open and in front.
/// </summary>
/// <remarks>
/// Since §239 a trophy plaque drawn in cells (<see cref="CapPlaque"/>): the cap engraved big on brass with the stage
/// (<c>PRUEBA 3 DE 12</c>) and the next cap, the strongest of the party against it as the notices' burning fuse, and a
/// brass plate per Pokémon. A little transparent, and clear of the top edge. It follows the emulator's window every half second, goes when the game
/// closes or another window comes in front, and reads the cap again every ten seconds and whenever the run changes.
/// Click-through, like every overlay.
/// </remarks>
public sealed class CapBadge
{
    private static readonly TimeSpan Follow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Reread = TimeSpan.FromSeconds(10);

    private readonly EmulatorLauncher _launcher;
    private readonly ProgressService _progress;
    private readonly LevelCapTable _caps;
    private readonly GameLinkMonitor _monitor;
    private readonly IRunContext _runContext;
    private readonly ILogger<CapBadge> _logger;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer = new() { Interval = Follow };

    /// <summary>The panel itself: a trophy plaque drawn in cells (§239).</summary>
    private readonly CapPlaque _plaque = new();

    /// <summary>The party, one plate per Pokémon under the cap (1.0.4.8).</summary>
    private IReadOnlyList<CapPlaqueMember> _party = [];
    private readonly PokemonSpriteService _sprites;
    private string _partyKey = string.Empty;
    private string _shownKey = string.Empty;
    private CapReading? _shown;

    private Window? _window;
    private CapReading? _reading;
    private DateTime _readAt = DateTime.MinValue;

    public CapBadge(EmulatorLauncher launcher, ProgressService progress, LevelCapTable caps, IRunContext runContext,
        GameLinkMonitor monitor, AppSettings settings, PokemonSpriteService sprites, ILogger<CapBadge> logger)
    {
        _launcher = launcher;
        _progress = progress;
        _caps = caps;
        _monitor = monitor;
        _runContext = runContext;
        _logger = logger;
        _settings = settings;
        _sprites = sprites;
        _timer.Tick += (_, _) => _ = TickAsync();
        monitor.RunDataChanged += (_, _) => _readAt = DateTime.MinValue;
        runContext.CurrentChanged += (_, _) => _readAt = DateTime.MinValue;

    }

    public void Start() => _timer.Start();

    /// <summary><c>--ensayar-cap</c>: the panel over PermaLocke's own window for ten seconds, without the game.</summary>
    public async Task RehearseAsync()
    {
        await _sprites.PrepareAsync();
        _reading = _runContext.Current is { } run ? await ReadAsync(run) : null;
        Show((_reading ?? new CapReading("PRUEBA 3 DE 12", 24, 26, null)) with { Highest = 22 });
        ShowParty(
        [
            new(0, 25, "Pikachu", "Chispas", 22, 48, 52, false, 1, 0, "", ""),
            new(1, 722, "Rowlet", "", 21, 20, 55, false, 2, 0, "", ""),
            new(2, 19, "Rattata", "Pepe", 14, 0, 40, false, 3, 0, "", ""),
            new(3, 131, "Lapras", "", 24, 9, 90, true, 4, 0, "", "")
        ]);
        var main = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
        if (GameWindow.ClientBox(main) is not { } box || _window is not { } window) return;
        Place(window, _plaque, box);
        await Task.Delay(TimeSpan.FromSeconds(10));
        window.Hide();
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
                _window?.Hide();
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
                _window?.Hide();
                return;
            }

            // El nivel del equipo se mira cada vuelta: sube en mitad de un combate y la barra lo sigue.
            Show(reading with { Highest = Highest() ?? reading.Highest });
            ShowParty(_monitor.Latest is { Connected: true } live ? Delayed(WithLiveHp(live.Party, _monitor.BattleNow)) : []);
            Place(_window!, _plaque, picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido poner el cap encima del juego");
            _window?.Hide();
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
        return new CapReading(label, stage.Level, next, Highest());
    }

    private int? Highest() =>
        _monitor.Latest is { Connected: true, Party.Count: > 0 } snapshot ? snapshot.Party.Max(member => member.Level) : null;

    private void Show(CapReading reading)
    {
        _window ??= Create();

        // Solo se repinta cuando cambia algo: el panel se mira cada medio segundo y casi nunca cambia.
        var key = $"{reading}|{_partyKey}";
        if (key != _shownKey)
        {
            _shownKey = key;
            _shown = reading;
            _plaque.Data = new CapPlaqueData(reading.Trial, reading.Cap, reading.Next, reading.Highest, _party);
        }
    }

    /// <summary>
    /// The party under the cap (1.0.4.8): each Pokémon's icon from the player's own ROM, its level (gold at the cap) and an
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
            member.Level, member.CurrentHp, member.MaxHp, member.IsEgg))];

        if (_shown is { } shown)
        {
            Show(shown);
        }
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

    /// <summary>
    /// Top right of <paramref name="picture"/>, clear of its top edge and a little in from the side, with as many screen
    /// pixels per cell as fit (§239): whole pixels, so the plaque never blurs.
    /// </summary>
    private static void Place(Window window, CapPlaque plaque, (int Left, int Top, int Width, int Height) picture)
    {
        if (plaque.Data is not { } data)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var rows = CapPlaque.Rows(data);

        // Se adapta al tamaño del juego (1.0.5.2): dos píxeles por celda en un juego de 900 de alto, más a pantalla completa
        // y menos en una ventana pequeña; si cabe en la franja negra junto a las pantallas del 3DS se queda en ella, y nunca
        // pasa del alto del juego.
        var cell = 2 * dpi.DpiScaleX * Math.Clamp(picture.Height / dpi.DpiScaleY / 900, 0.6, 1.8);
        var band = ((picture.Width - (picture.Height * 400.0 / 480)) / 2) - (24 * dpi.DpiScaleX);
        if (band >= CapPlaque.Columns * cell * 0.6) cell = Math.Min(cell, band / CapPlaque.Columns);
        cell = Math.Min(cell, (picture.Height - (64 * dpi.DpiScaleY)) / rows);
        var pixels = Math.Max(1, (int)Math.Floor(cell));

        int width, height, left;
        var top = picture.Top + (int)(48 * dpi.DpiScaleY);

        // Nunca encima de la barra de PS (2026-09-28): la copia de pantalla que espera la barra a cero leería el panel.
        // Antes se escondía mientras se miraba la barra, y eso es el parpadeo al morir; ahora encoge para no llegar.
        var game = Math.Min(picture.Width / 400.0, picture.Height / 480.0);
        var gameLeft = picture.Left + ((picture.Width - (400 * game)) / 2);
        var barTop = picture.Top + ((picture.Height - (480 * game)) / 2) + (PermaLocke.GameLink.Battle.HpBar.FirstRow * game);
        var barRight = gameLeft + ((PermaLocke.GameLink.Battle.HpBar.Right + 1) * game);
        while (true)
        {
            width = CapPlaque.Columns * pixels;
            height = rows * pixels;
            left = picture.Left + picture.Width - width - (int)(16 * dpi.DpiScaleX);
            if (pixels == 1 || !(left < barRight && top + height > barTop - 6)) break;
            pixels--;
        }

        // Una ventana tan pequeña que ni a un píxel por celda cabe sin salirse del juego o pisar la barra de PS: mejor sin
        // panel que encima de lo que hay que leer.
        if (top + height > picture.Top + picture.Height || (left < barRight && top + height > barTop - 6))
        {
            window.Hide();
            return;
        }

        plaque.CellPixels = pixels;
        if (!window.IsVisible) window.Show();
        OverlayWindows.PlaceOver(window, (left, top, width, height));
    }

    private Window Create()
    {
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
            Opacity = 0.95,
            Content = _plaque
        };

        OverlayWindows.MakeUntouchable(window);
        return window;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
