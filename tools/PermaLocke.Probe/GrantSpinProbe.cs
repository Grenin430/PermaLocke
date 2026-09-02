using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermaLocke.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;

namespace PermaLocke.Probe;

/// <summary>
/// Shows the roulette's ledger, and can hand the player spins they did not earn.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="StageProbe"/>, and it exists for the same reason: a correction has
/// to leave a record. What is owed is what the milestones earned minus the spins taken, both read
/// out of the chained history, so there is no field anywhere to nudge — and there must not be. A
/// spin cannot be un-spun: deleting a single event is impossible by design, and the only DELETE in
/// the codebase drops a whole run.
/// </para>
/// <para>
/// So a grant is an <b>addition</b>, with a reason attached, and both halves stay visible: the
/// spins that were taken are still there, and so is the fact that somebody handed some back and
/// why. It requires a reason for the same purpose the role change does — in six months the entry
/// has to explain itself without anybody remembering the evening it happened.
/// </para>
/// </remarks>
public static class GrantSpinProbe
{
    public static async Task<int> RunAsync(int? count, string? reason)
    {
        var root = Root();
        var paths = new AppPaths(root);

        var collection = new ServiceCollection();
        collection.AddPermaLockeInfrastructure(paths, "probe");
        collection.AddPermaLockeData(paths.Saves);
        collection.AddPermaLockeCore();
        collection.AddPermaLockeRules(Path.Combine(paths.Data, "rules.json"));
        collection.AddPermaLockeGameLink(paths.SaveBackups);
        collection.AddSingleton<AzaharInstallation>();
        collection.AddSingleton<IAchievementCatalog>(_ =>
            JsonAchievementCatalog.Load(Path.Combine(paths.Data, "achievements.json")));
        collection.AddSingleton<IRoleCatalog>(_ =>
            JsonRoleCatalog.Load(Path.Combine(paths.Data, "roles.json")));
        collection.AddSingleton<IRunRoles, RunRoles>();
        collection.AddSingleton<AchievementService>();
        collection.AddSingleton<IAbilityLookup>(_ => new PkhexAbilityLookup());
        collection.AddSingleton<IRouletteCatalog>(sp =>
            JsonRouletteCatalog.Load(Path.Combine(paths.Data, "roulette.json"),
                sp.GetRequiredService<IAbilityLookup>()));

        // El puerto del mundo va al TEMPORAL. Conceder una tirada no escribe en la partida, pero
        // registrar el puerto de verdad dejaria un camino de escritura abierto sin necesidad.
        var scratch = Path.Combine(Path.GetTempPath(), $"permalocke-conceder-{Guid.NewGuid():N}");

        collection.AddSingleton<IRouletteWorldPort>(sp => new SaveRouletteWorld(
            sp.GetRequiredService<PlayerSave>(), Path.Combine(scratch, "backup"),
            sp.GetRequiredService<ISpeciesLookup>(), sp.GetRequiredService<IItemLookup>(),
            sp.GetRequiredService<IAbilityLookup>(),
            sp.GetRequiredService<ILogger<SaveRouletteWorld>>()));
        collection.AddSingleton<RouletteService>();

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

        var wheel = services.GetRequiredService<RouletteService>();

        var earned = await wheel.EarnedAsync(run);
        var granted = await wheel.GrantedAsync(run.Id);
        var spun = await wheel.SpunAsync(run.Id);

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine($"  ganadas por hitos:  {earned - granted}");
        Console.WriteLine($"  concedidas a mano:  {granted}");
        Console.WriteLine($"  giradas:            {spun}");
        Console.WriteLine($"  pendientes:         {await wheel.OwedAsync(run)}");
        Console.WriteLine();

        if (count is not { } many)
        {
            Console.WriteLine("Esto solo mira. Para conceder: --tiradas <n> \"<motivo>\"");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            Console.WriteLine("Hace falta un motivo. Un apunte sin motivo no se puede leer luego.");
            return 1;
        }

        var owed = await wheel.GrantAsync(run, many, reason);

        // Se relee del historial: una concesion que no se ha vuelto a ver no se da por buena.
        var after = await wheel.GrantedAsync(run.Id);

        Console.WriteLine($"Concedidas: {granted} -> {after}");
        Console.WriteLine($"Pendientes: {owed}");

        return after == granted + many ? 0 : 1;
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
