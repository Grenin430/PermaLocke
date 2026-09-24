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
        collection.AddSingleton<IItemLookup>(_ => new PkhexItemLookup());
        collection.AddSingleton<IGachaCatalog>(_ =>
            JsonGachaCatalog.Load(Path.Combine(paths.Data, "gacha.json")));
        collection.AddSingleton<GiftDesk>();
        collection.AddSingleton<PermaLocke.App.Services.DiscordLogin>();
        collection.AddSingleton<AuditViewModel>();
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

        await model.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
