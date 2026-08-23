using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.Rules;

namespace PermaLocke.Probe;

/// <summary>
/// Audits a run against its own history: what it counts, what it recorded, and what the level cap
/// table says should have happened by now.
/// </summary>
/// <remarks>
/// HOME shows a handful of numbers — alive, dead, encounters, cap — and there was no way to check
/// any of them without believing the screen. This puts the raw material next to the totals: the
/// events that produced them and the stage table that decides the cap, so a number that is wrong
/// can be told from a number that is merely surprising.
/// </remarks>
public static class RunAuditProbe
{
    public static async Task<int> RunAsync()
    {
        var root = Root();
        var saves = Path.Combine(root, "Saves");
        var database = Path.Combine(saves, "permalocke.db");

        if (!File.Exists(database))
        {
            Console.WriteLine($"No hay base de datos en {database}");
            return 1;
        }

        var runs = new JsonRunRepository(saves);
        var all = await runs.GetAllAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("No hay ninguna run.");
            return 1;
        }

        var run = all.OrderByDescending(r => r.CreatedAt).First();

        Console.WriteLine($"Run: {run.Name}   rol {run.RoleId}   seed {run.SeedLabel}");
        Console.WriteLine($"Etapas superadas a mano: {run.ClearedStages}");
        Console.WriteLine();

        var pokemon = new SqlitePokemonRepository(database);
        var events = new SqliteEventStore(database);

        await ReportPokemonAsync(pokemon, run);
        await ReportEventsAsync(events, run);
        await ReportCapAsync(events, run, root);

        return 0;
    }

    private static async Task ReportPokemonAsync(IPokemonRepository pokemon, Run run)
    {
        var team = await pokemon.GetAllAsync(run.Id);

        Console.WriteLine($"POKÉMON REGISTRADOS: {team.Count}");

        foreach (var group in team.GroupBy(p => p.Status).OrderBy(g => g.Key.ToString()))
        {
            Console.WriteLine($"  {group.Key,-10} {group.Count(),4}");
        }

        Console.WriteLine();
        Console.WriteLine("  por origen:");

        foreach (var group in team.GroupBy(p => p.Origin).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"    {group.Key,-12} {group.Count(),4}");
        }

        // Lo que HOME llama "encuentros" es todo lo registrado, gacha y wonder trade incluidos.
        var caught = team.Count(p => p.Origin == PokemonOrigin.Capture);
        Console.WriteLine();
        Console.WriteLine($"  capturas de verdad (origen Capture): {caught}");
        Console.WriteLine($"  el resto ({team.Count - caught}) entró por gacha, intercambio o regalo");
        Console.WriteLine();

        ReportPidCoverage(team);
    }

    /// <summary>
    /// How many registered Pokémon carry a PID, which is what decides whether a death can be
    /// detected at all.
    /// </summary>
    /// <remarks>
    /// <see cref="Rules.Services.GameWatcher"/> matches the live party against the run <b>by PID</b>,
    /// so an entry without one is invisible to it: it can faint in front of the app and nothing
    /// will be recorded. That is a very different failure from "no ha muerto ninguno", and the
    /// totals on HOME cannot tell them apart, so the count goes here.
    /// </remarks>
    private static void ReportPidCoverage(IReadOnlyList<PokemonEntry> team)
    {
        var withPid = team.Count(p => p.Pid is not null and not 0);

        Console.WriteLine("DETECTABLES POR EL VIGILANTE (emparejamiento por PID)");
        Console.WriteLine($"  con PID:   {withPid,4}");
        Console.WriteLine($"  sin PID:   {team.Count - withPid,4}   <- estos no se pueden detectar muertos");

        if (withPid < team.Count)
        {
            foreach (var group in team.Where(p => p.Pid is null or 0)
                         .GroupBy(p => p.Origin).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"    {group.Key,-12} {group.Count(),4}");
            }
        }

        Console.WriteLine();
    }

    private static async Task ReportEventsAsync(IEventStore events, Run run)
    {
        var all = await events.GetAllAsync(run.Id);

        Console.WriteLine($"EVENTOS: {all.Count}");

        foreach (var group in all.GroupBy(e => e.Type).OrderByDescending(g => g.Count()))
        {
            var points = group.Sum(e => e.PointsDelta);
            var delta = points == 0 ? "" : $"   {points:+#;-#;0} puntos";
            Console.WriteLine($"  {group.Key,-22} {group.Count(),4}{delta}");
        }

        Console.WriteLine();
        Console.WriteLine($"  saldo por suma de eventos: {all.Sum(e => e.PointsDelta)}");
        Console.WriteLine();

        // Muertes: las que el historial recuerda, para poder cuadrarlas con lo que dice HOME.
        var deaths = all.Where(e => e.Type == GameEventType.PokemonDied).ToList();

        Console.WriteLine($"MUERTES REGISTRADAS: {deaths.Count}");

        foreach (var death in deaths.TakeLast(10))
        {
            Console.WriteLine($"  {death.Timestamp.LocalDateTime:dd/MM HH:mm}  {death.Description}");
        }

        Console.WriteLine();
    }

    private static async Task ReportCapAsync(IEventStore events, Run run, string root)
    {
        var caps = LevelCapTable.Load(Path.Combine(root, "Data", "levelcaps.json"));
        var claimed = (await events.GetAllAsync(run.Id))
            .Where(e => e.Type == GameEventType.AchievementUnlocked)
            .Select(e => e.Data.TryGetValue("logro", out var id) ? id : null)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("CAP DE NIVEL POR ETAPA");
        Console.WriteLine("  (el cap en vigor es el de la etapa que viene, no el de la superada)");
        Console.WriteLine();

        foreach (var stage in caps.Stages)
        {
            var done = stage.Achievement is { } id && claimed.Contains(id);
            Console.WriteLine($"  {stage.Order,2}. {stage.Name,-26} cap {stage.Level,3}   "
                              + $"logro {stage.Achievement,-22} {(done ? "COBRADO" : "")}");
        }

        Console.WriteLine();
        Console.WriteLine("  Nota: la etapa se deduce del logro, y el logro se detecta solo. Cobrarlo");
        Console.WriteLine("  a mano no es lo que mueve el cap; lo que lo mueve es que el logro esté");
        Console.WriteLine("  desbloqueado, cosa que mira AchievementService contra la partida.");
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
