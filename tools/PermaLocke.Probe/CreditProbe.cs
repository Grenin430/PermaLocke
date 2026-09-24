using Microsoft.Extensions.DependencyInjection;
using PermaLocke.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;

namespace PermaLocke.Probe;

/// <summary>
/// Shows the run's free gacha rolls and wonder trades, and can grant a roll by hand.
/// </summary>
/// <remarks>
/// <para>
/// Granting exists for the case the rules deliberately do not cover: a prize that only started
/// handing out a roll <em>after</em> somebody claimed it. Credit is counted from what each event
/// recorded, so the old claim grants nothing and no amount of editing the configuration will
/// change that -- which is correct, and is exactly why there has to be a way to say "give me one"
/// out loud instead of quietly making the rule retroactive.
/// </para>
/// <para>
/// It writes an <see cref="GameEventType.AdminAdjustment"/> carrying the same <c>credito</c> field
/// the wheel and the prizes write, so it is counted the same way and, more to the point, it is
/// visible: the history says a roll was granted by hand, when, and why. Nothing here edits a
/// number anywhere.
/// </para>
/// </remarks>
public static class CreditProbe
{
    public static async Task<int> RunAsync(string? banner, string? reason)
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
        collection.AddSingleton<ICreditCatalog>(_ =>
            JsonCreditCatalog.Load(Path.Combine(paths.Data, "grants.json")));
        collection.AddSingleton<CreditService>();

        using var services = collection.BuildServiceProvider();

        var all = await services.GetRequiredService<IRunRepository>().GetAllAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("No hay ninguna run.");
            return 1;
        }

        var run = all.OrderByDescending(r => r.CreatedAt).First();
        services.GetRequiredService<IRunContext>().SetCurrent(run);

        var credits = services.GetRequiredService<CreditService>();
        var events = services.GetRequiredService<IEventStore>();
        var clock = services.GetRequiredService<IClock>();

        await ShowAsync(credits, run);

        if (banner is null)
        {
            Console.WriteLine();
            Console.WriteLine("Esto solo mira. Para conceder una tirada: --credito <banner> [motivo]");
            return 0;
        }

        var why = string.IsNullOrWhiteSpace(reason)
            ? "Concedida a mano desde la sonda."
            : reason.Trim();

        // «intercambio N» concede wonder trades en vez de una tirada de gacha. Existe para no
        // tener que apagar limitarWonderTrades, que no concede uno: quita la regla para siempre.
        var trades = banner.StartsWith("intercambio", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(1, int.TryParse(banner.AsSpan("intercambio".Length).Trim(), out var n) ? n : 1)
            : 0;

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.AdminAdjustment,
            Source = EventSource.Admin,
            Actor = run.PlayerName,
            Description = trades > 0
                ? $"{trades} wonder trade(s) concedidos a mano."
                : $"Tirada de gacha concedida a mano en {banner}.",
            Reason = why,
            Data = trades > 0
                ? new Dictionary<string, string>
                {
                    ["creditoIntercambio"] = trades.ToString(),
                    ["concedidoAMano"] = bool.TrueString
                }
                : new Dictionary<string, string>
                {
                    ["credito"] = banner,
                    ["concedidoAMano"] = bool.TrueString
                }
        });

        Console.WriteLine();
        Console.WriteLine(trades > 0
            ? $"Concedidos {trades} wonder trade(s). Motivo: {why}"
            : $"Concedida 1 tirada en {banner}. Motivo: {why}");
        Console.WriteLine();

        await ShowAsync(credits, run);
        return 0;
    }

    private static async Task ShowAsync(CreditService credits, Run run)
    {
        var earned = await credits.EarnedAsync(run);
        var spent = await credits.SpentAsync(run.Id);
        var available = await credits.AvailableAsync(run);

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine();
        Console.WriteLine("TIRADAS DE GACHA");

        var banners = earned.Rolls.Keys
            .Concat(spent.Rolls.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (banners.Count == 0)
        {
            Console.WriteLine("  ninguna");
        }

        foreach (var name in banners)
        {
            Console.WriteLine($"  {name,-10} ganadas {earned.Rolls.GetValueOrDefault(name),3}"
                              + $"   gastadas {spent.Rolls.GetValueOrDefault(name),3}"
                              + $"   quedan {available.Rolls.GetValueOrDefault(name),3}");
        }

        Console.WriteLine($"WONDER TRADES  ganados {earned.WonderTrades}"
                          + $"   gastados {spent.WonderTrades}   quedan {available.WonderTrades}");
    }

    private static string Root()
    {
        // PERMALOCKE_ROOT apunta la sonda a otra instalación (p. ej. la carpeta de prueba del jugador).
        if (Environment.GetEnvironmentVariable("PERMALOCKE_ROOT") is { Length: > 0 } root)
        {
            return root;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
