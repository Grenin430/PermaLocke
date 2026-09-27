using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.App.Views.Pixel;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
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
/// A trophy and the stage (<c>PRUEBA 3 DE 12</c>), the cap big in gold, and a bar of how close the strongest of the
/// party is to it (<c>EQUIPO NV. 22 / 24</c>) that turns amber at the cap; under it, the next cap. Subtle: a little
/// transparent, and clear of the top edge. It follows the emulator's window every half second, goes when the game
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

    private readonly PixelText _trial = new() { Scale = 1, Tight = true };
    private readonly PixelText _cap = new() { Scale = 3, Tight = true };
    private readonly PixelText _team = new() { Scale = 1, Tight = true };
    private readonly PixelText _next = new() { Scale = 1, Tight = true };
    private readonly PixelBar _bar = new() { Height = 10, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>The party, one row per Pokémon under the cap (1.0.4.8).</summary>
    private readonly StackPanel _party = new();
    private readonly PokemonSpriteService _sprites;
    private string _partyKey = string.Empty;

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
        Place(window, box);
        await Task.Delay(TimeSpan.FromSeconds(10));
        window.Hide();
    }

    private async Task TickAsync()
    {
        try
        {
            var game = GameWindow.Handle();

            // Se quita en CONFIGURACIÓN (1.0.4.8).
            if (!_settings.Current.CapPanel || !_launcher.IsRunning || game == IntPtr.Zero || GetForegroundWindow() != game
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
            ShowParty(_monitor.Latest is { Connected: true } live ? live.Party : []);
            Place(_window!, picture);
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

    private static Color C(string key) => (Color)Application.Current.Resources[key];

    private void Show(CapReading reading)
    {
        var window = _window ??= Create();

        _trial.Text = reading.Trial;
        _cap.Text = $"NV. {reading.Cap}";
        _next.Text = reading.Next is { } next ? $"SIGUIENTE: NV. {next}" : "ULTIMO CAP";

        if (reading.Highest is { } highest)
        {
            var atCap = highest >= reading.Cap;
            _team.Text = $"EQUIPO NV. {highest} / {reading.Cap}";
            _bar.Value = Math.Clamp(highest / (double)reading.Cap, 0, 1);
            _bar.Fill = C(atCap ? "PxWarn" : "PxGood");
            _team.Colour = C(atCap ? "PxWarn" : "PxTextDim");
            _team.Visibility = _bar.Visibility = Visibility.Visible;
        }
        else
        {
            _team.Visibility = _bar.Visibility = Visibility.Collapsed;
        }

        if (!window.IsVisible) window.Show();
    }

    /// <summary>
    /// The party under the cap (1.0.4.8): each Pokémon's icon from the player's own ROM, its level (amber at the cap) and
    /// a small HP bar green, yellow or red as the game colours it; a fallen one greyed out with «KO». Rebuilt only when
    /// something on it changes, so it costs nothing while the player walks.
    /// </summary>
    private void ShowParty(IReadOnlyList<LivePartyMember> party)
    {
        var cap = _reading?.Cap ?? int.MaxValue;
        var key = cap + "|" + string.Join(";", party.Select(p => $"{p.Pid}:{p.Species}:{p.Form}:{p.Level}:{p.CurrentHp}/{p.MaxHp}"));
        if (key == _partyKey)
        {
            return;
        }

        _partyKey = key;
        _party.Children.Clear();

        if (party.Count == 0)
        {
            return;
        }

        _party.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(C("PxFaceLift")), Margin = new Thickness(0, 9, 0, 7) });
        _party.Children.Add(new PixelText { Text = "EQUIPO", Scale = 1, Tight = true, Colour = C("PxTextDim"), Margin = new Thickness(0, 0, 0, 5) });

        foreach (var member in party.OrderBy(p => p.Slot))
        {
            var fallen = member.IsFainted;
            var share = member.MaxHp > 0 ? Math.Clamp(member.CurrentHp / (double)member.MaxHp, 0, 1) : 0;
            var hpColour = share > 0.5 ? C("PxGood") : share > 0.2 ? C("PxWarn") : C("PxBad");

            var sprite = new System.Windows.Controls.Image
            {
                Source = _sprites.Get(member.Species, member.Form, member.IsShiny),
                Width = 40,
                Height = 30,
                Stretch = Stretch.Uniform,
                Opacity = fallen ? 0.35 : 1,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.NearestNeighbor);

            var name = string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname;
            var level = new PixelText
            {
                Text = $"NV. {member.Level}",
                Scale = 1,
                Tight = true,
                Colour = fallen ? C("PxTextFaint") : member.Level >= cap ? C("PxWarn") : C("PxText")
            };

            var info = new StackPanel { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Width = 108 };
            var top = new DockPanel();
            DockPanel.SetDock(level, Dock.Right);
            top.Children.Add(level);
            top.Children.Add(new PixelText
            {
                Text = name.Length > 10 ? name[..10] : name,
                Scale = 1,
                Tight = true,
                Colour = fallen ? C("PxTextFaint") : C("PxTextDim")
            });
            info.Children.Add(top);

            if (fallen)
            {
                info.Children.Add(new PixelText { Text = "KO", Scale = 1, Tight = true, Colour = C("PxBad"), Margin = new Thickness(0, 4, 0, 0) });
            }
            else
            {
                info.Children.Add(new PixelBar { Height = 10, Value = share, Fill = hpColour, Track = C("PxWell"), Margin = new Thickness(0, 4, 0, 0) });
                info.Children.Add(new PixelText
                {
                    Text = $"{member.CurrentHp}/{member.MaxHp}",
                    Scale = 1,
                    Tight = true,
                    Colour = C("PxTextFaint"),
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(sprite);
            row.Children.Add(info);
            _party.Children.Add(row);
        }
    }

    /// <summary>Top right of <paramref name="picture"/>, clear of its top edge and a little in from the side.</summary>
    private static void Place(Window window, (int Left, int Top, int Width, int Height) picture)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = (int)Math.Ceiling(content.DesiredSize.Width * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(content.DesiredSize.Height * dpi.DpiScaleY);
        OverlayWindows.PlaceOver(window, (picture.Left + picture.Width - width - (int)(16 * dpi.DpiScaleX),
            picture.Top + (int)(48 * dpi.DpiScaleY), width, height));
    }

    private Window Create()
    {
        _trial.Colour = C("PxAccentLight");
        _cap.Colour = C("PxGold");
        _cap.Shadow = C("PxShadow");
        _next.Colour = C("PxTextFaint");
        _bar.Track = C("PxWell");

        var label = new PixelText { Text = "CAP DE NIVEL", Scale = 1, Tight = true, Colour = C("PxTextDim") };

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(new PixelIcon { Icon = "IconTrophy", Scale = 2, VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(new StackPanel
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _trial, new Border { Height = 4 }, label }
        });

        var body = new StackPanel
        {
            Margin = new Thickness(14, 11, 18, 15),
            MinWidth = 170,
            Children = { head, new Border { Height = 8 }, _cap, new Border { Height = 8 }, _team, _bar, new Border { Height = 6 }, _next, _party }
        };

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
            Opacity = 0.9,
            Content = new Grid { Children = { new PixelPanel { Fill = C("PxFace") }, body } }
        };

        OverlayWindows.MakeUntouchable(window);
        return window;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
