using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

[CollectionDefinition("Wpf resources", DisableParallelization = true)]
public sealed class WpfResourceCollection { }

[Collection("Wpf resources")]
public sealed class HomeViewTests
{
    [Fact]
    public void Home_and_viewer_load_with_real_theme_resources_and_home_toggles_the_encounter_warning()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Application? app = null;
            try
            {
                app = new Application();
                foreach (var name in new[] { "Palette", "Icons", "Controls", "Pixel", "Shells" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/PermaLocke.App;component/Themes/{name}.xaml", UriKind.Relative)
                    });
                var view = new HomeView
                {
                    DataContext = new { HasRun = true, GameLinkConnected = true, HasEncounterWarning = true, EncounterWarning = "Ruta pendiente" }
                };
                view.Measure(new Size(1200, 900));
                view.Arrange(new Rect(0, 0, 1200, 900));
                view.UpdateLayout();
                var text = Descendants(view).OfType<PermaLocke.App.Views.Pixel.PixelText>().Single(t => t.Text == "Ruta pendiente");
                var border = Assert.IsType<Grid>(VisualTreeHelper.GetParent(text));
                Assert.Equal(Visibility.Visible, border.Visibility);
                view.DataContext = new { HasRun = true, GameLinkConnected = true, HasEncounterWarning = false, EncounterWarning = "" };
                border.GetBindingExpression(UIElement.VisibilityProperty)!.UpdateTarget();
                Assert.Equal(Visibility.Collapsed, border.Visibility);

                // El visor en píxeles, con los mismos recursos: un recurso mal escrito revienta aquí y no al abrir la pantalla.
                var viewer = new PokemonViewerView { DataContext = new { HasSelection = false, Summary = "resumen" } };
                viewer.Measure(new Size(1300, 860));
                viewer.Arrange(new Rect(0, 0, 1300, 860));
                viewer.UpdateLayout();
                var titles = Descendants(viewer).OfType<PermaLocke.App.Views.Pixel.PixelWindow>().Select(w => w.Title).ToList();
                Assert.Equal(["EQUIPO", "CAJAS DEL PC", "FICHA"], titles);

                // Y el resto de pantallas en píxeles (§176): que se construyan y se midan sin datos, que es cuando un
                // StaticResource que no existe o un estilo mal heredado lanza.
                UserControl[] screens =
                [
                    new LauncherView(), new RandomizerView(), new GachaView(), new ShopView(), new AchievementsView(),
                    new MapView(), new AlbumView(), new EvTrainingView(), new MoveReminderView(), new PokePasteView(), new CemeteryView(),
                    new BattleModeView(), new RouletteView(), new MiscellaneousView(), new SettingsView(),
                ];
                foreach (var screen in screens)
                {
                    screen.Measure(new Size(1300, 860));
                    screen.Arrange(new Rect(0, 0, 1300, 860));
                    screen.UpdateLayout();
                }

                // La guardería de los roles MONOTYPE (§221), con una tanda de huevos a medio dar la vuelta.
                PermaLocke.Core.Domain.GachaPull Pull(int species, string name, int total, int number) => new("guarderia", "guarderia", species, name, false, total, 1, false,
                    [31, 20, 15, 10, 5, 0], 3, "Firme", 66, "Mar llamas", 1UL, number);
                var nursery = new NurseryView
                {
                    DataContext = new
                    {
                        RoleName = "MONOTYPE FUEGO", TypeColour = System.Windows.Media.Color.FromRgb(0xE6, 0x28, 0x29),
                        TypeText = "Solo salen especies de tu tipo, sin legendarios. Cada tirada es un huevo a nivel 1 que va a una caja de tu PC.",
                        StrengthText = "Los huevos apuntan a especies más fuertes con cada prueba que superas.",
                        StrengthShare = 0.35, StrengthLabel = "~338 PTS", ProgressLabel = "PRUEBAS 2/8",
                        Trials = new[] { new PermaLocke.App.ViewModels.TrialPipViewModel(true), new(true), new(false), new(false), new(false), new(false), new(false), new(false) },
                        OwedEggs = new[] { new PermaLocke.App.ViewModels.OwedEggViewModel(Placeholder(1)), new(Placeholder(1)), new(Placeholder(1)) }, OwedMore = "",
                        IsPlaying = false, PlayDone = false, Play = (PermaLocke.App.Views.NurseryPlay?)null, ContinueCommand = new PermaLocke.App.Tests.NoCommand(),
                        Owed = 3, Status = "Huevo en caja 1, hueco 3. Todavía te quedan 3.", StatusIsWarning = false, ButtonText = "PEDIR UN HUEVO (3)",
                        Milestones = new[] { new PermaLocke.App.ViewModels.MilestoneEggsViewModel("Prueba 1", 1, true), new("Prueba 2", 1, true), new("Prueba 3", 1, false), new("Campeón de la Liga", 3, false) },
                        MilestonesSummary = "2 de 4 conseguidos: 2 de 6 huevos.",
                        HasReceived = true, ArrivedPlace = "CAJA 1 · HUECO 3",
                        Received = new[] { new PermaLocke.App.ViewModels.ReceivedEggViewModel(2, "CAJA 1 · HUECO 3", Placeholder(1)), new(1, "CAJA 1 · HUECO 2", Placeholder(1)) },
                        GetEggCommand = new PermaLocke.App.Tests.NoCommand(),
                    }
                };
                LoadDialogHost(nursery, 900, 1500);

                // Los diálogos en píxeles: se construyen sin su view model (piden uno de verdad en el constructor) y se
                // miden con datos de muestra. Con PERMALOCKE_SNAP_DIR se guardan además en PNG, para mirarlos.
                var role = Mutable(new { Name = "EXPERTO", Description = "Para quien ya sabe jugar.", Effects = "Puntos x1,5 · enemigos +27%", IsSelected = true });
                var other = Mutable(new { Name = "NORMAL", Description = "La experiencia de siempre.", Effects = "Puntos x1", IsSelected = false });
                // El menú de roles de verdad (§220), con los de Data/roles.json: las tarjetas, MONOTYPE como una más y sus tipos.
                var catalog = PermaLocke.Data.JsonRoleCatalog.Load(RolesFile());
                Assert.Equal(12, catalog.All.Count);
                var menu = new PermaLocke.App.ViewModels.RoleMenuViewModel(catalog.All);
                Assert.Equal(["normal", "cagoneta", "experto", "ludopata", ""], menu.Cards.Select(c => c.Role.Id));
                Assert.Equal(8, menu.Types.Count);
                Assert.False(menu.ShowTypes);
                menu.Cards.Last().IsSelected = true;
                Assert.True(menu.ShowTypes);
                Assert.Equal(string.Empty, menu.SelectedRoleId);      // MONOTYPE sin tipo no es un rol todavía
                menu.Types.Single(t => t.Role.Id == "monotype_fuego").IsSelected = true;
                Assert.Equal("monotype_fuego", menu.SelectedRoleId);
                Assert.Single(menu.Types, t => t.IsSelected);
                menu.Cards.First().IsSelected = true;
                Assert.False(menu.ShowTypes);
                Assert.Equal("normal", menu.SelectedRoleId);
                Assert.DoesNotContain(menu.Types, t => t.IsSelected);
                menu.Select("monotype_agua");
                Assert.True(menu.ShowTypes);
                Assert.Equal("monotype_agua", menu.SelectedRoleId);
                foreach (var type in menu.Types) type.Sprite = Placeholder(type.Role.IconSpecies ?? 1);

                // El menú entero, sin el scroll de la ventana, para poder mirarlo.
                var whole = new RoleMenuView { DataContext = menu };
                var holder = new Border { Child = whole, Background = (Brush)app.Resources["PxBaseBrush"], Padding = new Thickness(14), Width = 600 };
                holder.Measure(new Size(600, double.PositiveInfinity));
                holder.Arrange(new Rect(0, 0, 600, holder.DesiredSize.Height));
                holder.UpdateLayout();
                Assert.NotEmpty(Descendants(whole).OfType<PermaLocke.App.Views.Pixel.PixelText>().Where(t => t.Text == "ELIGE TU TIPO"));
                if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is { Length: > 0 } snap)
                {
                    var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)holder.ActualWidth, (int)holder.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    shot.Render(holder);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                    using var shotFile = System.IO.File.Create(System.IO.Path.Combine(snap, "RoleMenu.png"));
                    encoder.Save(shotFile);
                }

                LoadDialog<CreateRunWindow>(Mutable(new
                {
                    Menu = menu, RoleProblem = "", RomStatus = "Pokemon Ultra Moon (Europe) · 00040000001B5100",
                    RunName = "Mi run", PlayerName = "Grenin", ErrorMessage = "",
                }));
                LoadDialog<ChangeRoleWindow>(Mutable(new
                {
                    CurrentText = "Ahora eres NORMAL.", Menu = menu, Reason = "", Problem = "Falta el motivo.",
                }));
                LoadDialog<RegisterCaptureWindow>(Mutable(new
                {
                    Species = new[] { new { Name = "Rowlet" } }, KnownLocations = new[] { "Ruta 1" }, LocationName = "Ruta 1",
                    EncounterTypes = new[] { PermaLocke.Core.Domain.EncounterType.Wild }, Level = 3, Nickname = "", IsShiny = false,
                    Outcome = PermaLocke.Rules.RuleOutcome.Allowed, VerdictTitle = "PERMITIDA", VerdictMessage = "Cumple todas las reglas.",
                    Details = new[] { new { Label = "Primer encuentro", Value = "Ruta 1 sin gastar" } },
                    ErrorMessage = "", RegisterLabel = "REGISTRAR",
                }));

                // Los cinco diseños (2026-10-01): cada marco se construye y se mide con los recursos de cada uno, y cambiar de
                // diseño cambia los colores que leen los estilos. Un recurso que falta o un estilo mal escrito lanza aquí.
                foreach (var theme in PermaLocke.App.Views.Pixel.PixelTheme.All)
                {
                    PermaLocke.App.Views.Pixel.PixelTheme.Apply(theme.Key, app.Resources);
                    Assert.Equal(theme["PxAccent"], (Color)app.Resources["PxAccent"]);
                    Assert.Equal(theme.Key, PermaLocke.App.Views.Pixel.PixelTheme.Current.Key);

                    UserControl[] shells =
                    [
                        new PermaLocke.App.Shells.RailShell(), new PermaLocke.App.Shells.TabsShell(), new PermaLocke.App.Shells.MenuShell(),
                        new PermaLocke.App.Shells.DockShell(), new PermaLocke.App.Shells.KeysShell(),
                    ];
                    foreach (var shell in shells)
                    {
                        shell.Measure(new Size(1360, 860));
                        shell.Arrange(new Rect(0, 0, 1360, 860));
                        shell.UpdateLayout();
                    }

                    var themed = new PokemonViewerView { DataContext = new { HasSelection = false, Summary = "resumen" } };
                    themed.Measure(new Size(1300, 860));
                    themed.Arrange(new Rect(0, 0, 1300, 860));
                    themed.UpdateLayout();
                    Assert.NotEmpty(Descendants(themed).OfType<PermaLocke.App.Views.Pixel.PixelWindow>());
                }

                PermaLocke.App.Views.Pixel.PixelTheme.Apply("clasico", app.Resources);
                RenderNotices();
            }
            catch (Exception ex) { failure = ex; }
            finally { app?.Shutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF view load timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>
    /// The notices over the game (the scroll): every kind builds, measures and draws through the real template, at the
    /// first step of the entrance and wide open. With PERMALOCKE_SNAP_DIR it saves both as PNG, over a game-coloured floor.
    /// </summary>
    private static void RenderNotices()
    {
        var now = DateTime.UtcNow;
        var linger = TimeSpan.FromSeconds(6);
        var toasts = new[]
        {
            new PermaLocke.App.Services.Toast(PermaLocke.App.Services.ToastKind.Death, "pedicure ha caído", "-25 puntos · Nv. 20", Placeholder(2), now.AddSeconds(-1), linger),
            new PermaLocke.App.Services.Toast(PermaLocke.App.Services.ToastKind.Shiny, "¡Variocolor!", "Charizard · siempre se captura", Placeholder(3), now.AddSeconds(-3), linger),
            new PermaLocke.App.Services.Toast(PermaLocke.App.Services.ToastKind.Duplicate, "Ya lo tienes", "Frogadier · captúralo o pasa y se descuenta", null, now.AddSeconds(-5), linger),
            new PermaLocke.App.Services.Toast(PermaLocke.App.Services.ToastKind.Update, "Hay una versión nueva", "PermaLocke 1.0.14 está lista. Abre PermaLocke para actualizar.", null, now, Timeout.InfiniteTimeSpan),
            new PermaLocke.App.Services.Toast(PermaLocke.App.Services.ToastKind.FriendPlaying, "Bavi está jugando a PermaLocke", "Ya lleva 3 medallas", Placeholder(1), now, linger)
        };

        var window = new PermaLocke.App.Views.ToastWindow { DataContext = new { Showing = toasts } };
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border { Child = content, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x8A, 0x4A)), Width = 470, Height = 860, DataContext = new { Showing = toasts } };

        string? dir = Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR");
        foreach (var (reveal, name) in new[] { (0.48, "ToastScroll-abriendo"), (1.0, "ToastScroll-abierto") })
        {
            host.Measure(new Size(470, 860));
            host.Arrange(new Rect(0, 0, 470, 860));
            host.UpdateLayout();
            var scrolls = Descendants(host).OfType<PermaLocke.App.Views.ToastScroll>().ToList();
            Assert.Equal(toasts.Length * 2, scrolls.Count);
            foreach (var scroll in scrolls) scroll.Reveal = reveal;
            host.UpdateLayout();

            if (string.IsNullOrEmpty(dir)) continue;
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(470, 860, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var file = System.IO.File.Create(System.IO.Path.Combine(dir, name + ".png"));
            png.Save(file);
        }
    }

    private static string RolesFile()
    {
        for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = System.IO.Path.Combine(dir.FullName, "Data", "roles.json");
            if (System.IO.File.Exists(path)) return path;
        }

        throw new System.IO.FileNotFoundException("Data/roles.json");
    }

    /// <summary>A stand-in for a species icon, so the type menu can be looked at without a ROM.</summary>
    private static System.Windows.Media.Imaging.BitmapSource Placeholder(int seed)
    {
        var pixels = new byte[40 * 30 * 4];
        for (var i = 0; i < 40 * 30; i++)
        {
            pixels[i * 4] = (byte)(60 + (seed * 37 % 150));
            pixels[i * 4 + 1] = (byte)(80 + (i % 40) * 3);
            pixels[i * 4 + 2] = (byte)(100 + (i / 40) * 4);
            pixels[i * 4 + 3] = 255;
        }

        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    // Los diálogos enlazan en los dos sentidos, y un tipo anónimo es de solo lectura.
    private static System.Dynamic.ExpandoObject Mutable(object values)
    {
        var bag = new System.Dynamic.ExpandoObject();
        var map = (IDictionary<string, object?>)bag;
        foreach (var property in values.GetType().GetProperties()) map[property.Name] = property.GetValue(values);
        return bag;
    }

    private static void LoadDialog<T>(object data) where T : Window
    {
        // Sin pasar por el constructor que pide el view model: el objeto se crea vacío, se le corre el de Window y
        // luego el XAML generado.
        var window = (T)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T));
        typeof(Window).GetConstructor(Type.EmptyTypes)!.Invoke(window, null);
        window.DataContext = data;
        typeof(T).GetMethod("InitializeComponent")!.Invoke(window, null);

        var content = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border { Child = content, Background = window.Background, DataContext = data };
        var size = new Size(window.Width, window.Height - 40);
        host.Measure(size);
        host.Arrange(new Rect(size));
        host.UpdateLayout();

        var dir = Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(dir, typeof(T).Name + ".png"));
        png.Save(file);
    }

    /// <summary>Measures a screen at a fixed size and, with PERMALOCKE_SNAP_DIR, saves it as a PNG.</summary>
    private static void LoadDialogHost(FrameworkElement view, double width, double height)
    {
        var host = new Border { Child = view, Background = (Brush)Application.Current.Resources["PxBaseBrush"], Width = width, Height = height };
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();

        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is not { Length: > 0 } dir)
        {
            return;
        }

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(dir, view.GetType().Name + ".png"));
        png.Save(file);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

/// <summary>A command that does nothing, for a screen shown only to be looked at.</summary>
internal sealed class NoCommand : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) { }
}
