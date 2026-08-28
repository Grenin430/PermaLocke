using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.Infrastructure;

namespace PermaLocke.Probe;

/// <summary>
/// Command line front for <see cref="TradedAwayReconciler"/>.
/// </summary>
/// <remarks>
/// The matching used to live here, and now lives in the reconciler so that the application and
/// this tool cannot drift apart: two implementations of "which Pokémon left" would eventually
/// disagree, and only one of them would be the one the player sees.
/// </remarks>
public static class TradedAwayProbe
{
    public static async Task<int> RunAsync(bool repair)
    {
        var paths = new AppPaths(Root());
        var database = Path.Combine(paths.Saves, "permalocke.db");

        if (!File.Exists(database))
        {
            Console.WriteLine($"No hay base de datos en {database}");
            return 1;
        }

        var all = await new JsonRunRepository(paths.Saves).GetAllAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("No hay ninguna run.");
            return 1;
        }

        var run = all.OrderByDescending(r => r.CreatedAt).First();

        var reconciler = new TradedAwayReconciler(
            new SqlitePokemonRepository(database),
            new SqliteEventStore(database),
            new SystemClock());

        var report = repair
            ? await reconciler.RepairAsync(run)
            : await reconciler.InspectAsync(run);

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine($"  wonder trades en el historial: {report.HandedOver}");
        Console.WriteLine($"  registrados vivos y sin PID:   {report.AliveWithoutPid}");
        Console.WriteLine($"  se pueden cerrar:              {report.Matched.Count}");
        Console.WriteLine($"  en disputa (no se tocan):      {report.Disputed.Count}");
        Console.WriteLine();

        foreach (var line in report.Disputed)
        {
            Console.WriteLine($"    {line}");
        }

        foreach (var entry in report.Matched.Take(8))
        {
            Console.WriteLine($"    {entry.SpeciesName,-14} {entry.ObtainedAt.LocalDateTime:dd/MM HH:mm}");
        }

        if (report.Matched.Count > 8)
        {
            Console.WriteLine($"    ... y {report.Matched.Count - 8} más");
        }

        Console.WriteLine();
        Console.WriteLine(report.Message);

        if (!repair)
        {
            Console.WriteLine("Esto solo mira. Para escribirlo: --intercambiados --arreglar");
        }

        return 0;
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
