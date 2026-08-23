using Microsoft.Extensions.DependencyInjection;
using PermaLocke.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.Probe;

/// <summary>
/// Shows, and can correct, the stages the player marked by hand.
/// </summary>
/// <remarks>
/// <para>
/// The cap in force is the higher of two numbers: what the achievements detect and what somebody
/// pressed. The manual button is gone from HOME — the achievements do the job and a button that
/// does the same thing only serves to run ahead of them — but a run that used it keeps the number
/// it reached, and there is nowhere left to take it back.
/// </para>
/// <para>
/// It goes through <see cref="ProgressService.AdvanceAsync"/> and not through the run file, so the
/// correction lands in the history like everything else. Fixing a cap by editing <c>run.json</c>
/// would be exactly the silent state change this project forbids.
/// </para>
/// </remarks>
public static class StageProbe
{
    public static async Task<int> RunAsync(int? target)
    {
        var root = Root();
        var paths = new AppPaths(root);

        var collection = new ServiceCollection();
        collection.AddPermaLockeInfrastructure(paths, "probe");
        collection.AddPermaLockeData(paths.Saves);
        collection.AddPermaLockeCore();
        collection.AddPermaLockeRules(Path.Combine(paths.Data, "rules.json"));
        collection.AddPermaLockeGameLink(paths.SaveBackups);
        collection.AddSingleton<IAchievementCatalog>(_ =>
            JsonAchievementCatalog.Load(Path.Combine(paths.Data, "achievements.json")));
        collection.AddSingleton<IRoleCatalog>(_ =>
            JsonRoleCatalog.Load(Path.Combine(paths.Data, "roles.json")));
        collection.AddSingleton<IRunRoles, RunRoles>();
        collection.AddSingleton<AzaharInstallation>();
        collection.AddSingleton<AchievementService>();

        using var services = collection.BuildServiceProvider();

        var runs = services.GetRequiredService<IRunRepository>();
        var all = await runs.GetAllAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("No hay ninguna run.");
            return 1;
        }

        var run = all.OrderByDescending(r => r.CreatedAt).First();
        services.GetRequiredService<IRunContext>().SetCurrent(run);

        var progress = services.GetRequiredService<ProgressService>();

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine($"  etapas marcadas a mano:  {run.ClearedStages}");
        Console.WriteLine($"  etapas por los logros:   {await progress.DetectedAsync(run)}");
        Console.WriteLine($"  en vigor (la mayor):     {await progress.ClearedAsync(run)}");
        Console.WriteLine($"  cap en vigor:            {(await progress.CurrentCapAsync(run))?.ToString() ?? "sin definir"}");
        Console.WriteLine();

        if (target is not { } wanted)
        {
            Console.WriteLine("Esto solo mira. Para corregirlo: --etapas <n>");
            return 0;
        }

        if (wanted == run.ClearedStages)
        {
            Console.WriteLine($"Ya está en {wanted}. No hay nada que cambiar.");
            return 0;
        }

        var updated = await progress.AdvanceAsync(run, wanted - run.ClearedStages, run.PlayerName);

        // Se relee del repositorio: no se da por buena una corrección que no se ha vuelto a ver.
        var after = (await runs.GetAllAsync()).First(r => r.Id == run.Id);

        Console.WriteLine($"Etapas a mano: {run.ClearedStages} -> {after.ClearedStages}");
        Console.WriteLine($"Cap en vigor:  {(await progress.CurrentCapAsync(updated))?.ToString() ?? "sin definir"}");

        return after.ClearedStages == wanted ? 0 : 1;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
