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
                foreach (var name in new[] { "Palette", "Icons", "Controls", "Pixel" })
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

                // Los diálogos en píxeles: se construyen sin su view model (piden uno de verdad en el constructor) y se
                // miden con datos de muestra. Con PERMALOCKE_SNAP_DIR se guardan además en PNG, para mirarlos.
                var role = Mutable(new { Name = "EXPERTO", Description = "Para quien ya sabe jugar.", Effects = "Puntos x1,5 · enemigos +27%", IsSelected = true });
                var other = Mutable(new { Name = "NORMAL", Description = "La experiencia de siempre.", Effects = "Puntos x1", IsSelected = false });
                LoadDialog<CreateRunWindow>(Mutable(new
                {
                    Roles = new[] { other, role }, RoleProblem = "", RomStatus = "Pokemon Ultra Moon (Europe) · 00040000001B5100",
                    RunName = "Mi run", PlayerName = "Grenin", ErrorMessage = "",
                }));
                LoadDialog<ChangeRoleWindow>(Mutable(new
                {
                    CurrentText = "Ahora eres NORMAL.", Roles = new[] { other, role }, Reason = "", Problem = "Falta el motivo.",
                }));
                LoadDialog<RegisterCaptureWindow>(Mutable(new
                {
                    Species = new[] { new { Name = "Rowlet" } }, KnownLocations = new[] { "Ruta 1" }, LocationName = "Ruta 1",
                    EncounterTypes = new[] { PermaLocke.Core.Domain.EncounterType.Wild }, Level = 3, Nickname = "", IsShiny = false,
                    Outcome = PermaLocke.Rules.RuleOutcome.Allowed, VerdictTitle = "PERMITIDA", VerdictMessage = "Cumple todas las reglas.",
                    Details = new[] { new { Label = "Primer encuentro", Value = "Ruta 1 sin gastar" } },
                    ErrorMessage = "", RegisterLabel = "REGISTRAR",
                }));
            }
            catch (Exception ex) { failure = ex; }
            finally { app?.Shutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF view load timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
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
