using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.Admin.ViewModels;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink.Data;
using PermaLocke.Infrastructure;

namespace PermaLocke.Admin;

/// <summary>
/// The admin's tool: one window over the shared folder (§129).
/// </summary>
/// <remarks>
/// It shares this machine's <c>Config/</c> and <c>Data/</c> with PermaLocke, so it reads the same shared folder and
/// the same banners without being told twice. It never opens the run database: an admin does not edit anybody's run,
/// not even their own from here.
/// </remarks>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        paths.EnsureCreated();

        var collection = new ServiceCollection();
        collection.AddPermaLockeInfrastructure(paths, "permalocke-admin");
        collection.AddSingleton<IGachaCatalog>(_ =>
            JsonGachaCatalog.Load(Path.Combine(paths.Data, "gacha.json")));
        collection.AddSingleton<GiftDesk>();
        collection.AddSingleton<ISpeciesStatsCatalog>(_ =>
            JsonSpeciesStatsCatalog.Load(Path.Combine(paths.Data, "species.json")));
        collection.AddSingleton<IShopCatalog>(_ =>
            JsonShopCatalog.Load(Path.Combine(paths.Data, "shop.json")));
        // --demo (2026-10-08): jugadores inventados en memoria, para mirar Admin sin leer ni tocar el torneo.
        var demo = e.Args.Contains("--demo");
        collection.AddSingleton(new AdminMode(demo));
        collection.AddSingleton<PermaLocke.App.Services.DiscordLogin>();
        if (demo) collection.AddSingleton<PermaLocke.App.Services.ITournamentServer, DemoServer>();
        else collection.AddSingleton<PermaLocke.App.Services.ITournamentServer>(sp => sp.GetRequiredService<PermaLocke.App.Services.DiscordLogin>());
        collection.AddSingleton<LogViewModel>();
        collection.AddSingleton<AuditViewModel>();
        collection.AddSingleton<WhitelistViewModel>();
        collection.AddSingleton<AnnouncementsViewModel>();
        collection.AddSingleton<UsageViewModel>();
        collection.AddSingleton<CleanupViewModel>();
        collection.AddSingleton<ReportsViewModel>();
        collection.AddSingleton<SuggestionsViewModel>();
        collection.AddSingleton<MotesViewModel>();
        collection.AddSingleton<FallenViewModel>();
        collection.AddSingleton<RulesViewModel>();
        collection.AddSingleton<AdminViewModel>();

        _services = collection.BuildServiceProvider();

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("PermaLocke Admin iniciado. Raíz de datos: {Root}", paths.Root);

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Excepción no controlada en la interfaz del admin");
            MessageBox.Show("Ha ocurrido un error inesperado.", "PermaLocke Admin",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var model = _services.GetRequiredService<AdminViewModel>();
        var window = new MainWindow { DataContext = model };
        MainWindow = window;
        window.Show();

        var shotsAt = Array.IndexOf(e.Args, "--capturas");
        if (shotsAt >= 0 && shotsAt + 1 < e.Args.Length)
        {
            // Las capturas leen lo que haya: con --demo, jugadores inventados; sin él, el torneo de verdad (solo lectura).
            _ = CaptureAsync(model, window, e.Args[shotsAt + 1], logger);
        }

        await model.StartAsync();
    }

    /// <summary>
    /// A picture of every page, of each tab of a player's sheet, of a confirmation and of the narrowest window, then closes
    /// (<c>--capturas carpeta</c>, 2026-10-08). Rendered by WPF itself, so nothing in front of the window ends up in them.
    /// It only looks: no button is pressed, so nothing is sent.
    /// </summary>
    private static async Task CaptureAsync(AdminViewModel model, MainWindow window, string folder, ILogger logger)
    {
        void Snap(System.Windows.Media.Visual visual, double width, double height, string name)
        {
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(visual);
            var picture = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Round(width * dpi.DpiScaleX), (int)Math.Round(height * dpi.DpiScaleY),
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
            picture.Render(visual);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(picture));
            using var file = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(file);
        }

        static string Slug(string title) => new string(title.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray()).Replace(' ', '-').ToLowerInvariant();

        try
        {
            Directory.CreateDirectory(folder);
            await Task.Delay(3000);

            var order = 0;
            foreach (var section in model.Sections)
            {
                model.SelectedSection = section;
                await Task.Delay(1500);
                Snap(window, window.ActualWidth, window.ActualHeight, $"{++order:00}-{Slug(section.Title)}");
            }

            // La ficha de un jugador con avisos, pestaña a pestaña.
            if ((model.Players.FirstOrDefault(p => p.Rewinds > 0) ?? model.Players.FirstOrDefault()) is { } player)
            {
                model.OpenSheetCommand.Execute(player);
                await Task.Delay(2000);
                for (var tab = 0; tab < PlayerSheetViewModel.Tabs.Length; tab++)
                {
                    model.Sheet!.Tab = tab;
                    await Task.Delay(700);
                    Snap(window, window.ActualWidth, window.ActualHeight, $"ficha-{tab:00}-{Slug(PlayerSheetViewModel.Tabs[tab])}");
                }
            }

            // Varios marcados: la barra de acciones a la vez.
            model.Choose(model.Players.Where(p => p.HasAccount).Take(4));
            await Task.Delay(600);
            Snap(window, window.ActualWidth, window.ActualHeight, "jugadores-varios");

            // Un filtro y una búsqueda sin resultados: el estado vacío.
            model.Search = "zzzz";
            await Task.Delay(600);
            Snap(window, window.ActualWidth, window.ActualHeight, "jugadores-vacio");
            model.Search = string.Empty;

            // La ventana más estrecha que se permite.
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            await Task.Delay(1200);
            Snap(window, window.ActualWidth, window.ActualHeight, "jugadores-estrecha");

            // Una confirmación peligrosa, como la ve el organizador.
            var dialog = new ConfirmDialog(new ConfirmRequest("Reiniciar su run", "SaintPablo",
            [
                "Su run activa pasa a archivada: deja de contar en la clasificación y no puede subir más.",
                "Al abrir PermaLocke podrá empezar una nueva desde cero.",
                "No se borra nada. AUDITORÍA puede reactivarla si no ha empezado otra."
            ], "REINICIAR", NeedsReason: false, Danger: true, TypeToConfirm: "REINICIAR")) { Owner = window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            dialog.Show();
            await Task.Delay(800);
            Snap((System.Windows.Media.Visual)dialog.Content, ((FrameworkElement)dialog.Content).ActualWidth, ((FrameworkElement)dialog.Content).ActualHeight, "confirmar-peligro");
            dialog.Close();

            var normal = new ConfirmDialog(new ConfirmRequest("Dar 2 x Caramelo Raro", "Juanito",
                ["Llega a su mochila la próxima vez que tenga el juego abierto con la partida cargada.",
                 "Su PermaLocke lo aplica solo en unos segundos si está abierto, o al abrirlo, y deja el evento en su historial."], "MANDAR"))
                { Owner = window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            normal.Show();
            await Task.Delay(800);
            Snap((System.Windows.Media.Visual)normal.Content, ((FrameworkElement)normal.Content).ActualWidth, ((FrameworkElement)normal.Content).ActualHeight, "confirmar-orden");
            normal.Close();

            logger.LogInformation("Capturas de Admin en {Folder}", folder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallaron las capturas de Admin");
        }
        finally
        {
            Current.Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
