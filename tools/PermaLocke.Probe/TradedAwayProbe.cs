using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Probe;

/// <summary>
/// Finds the Pokémon the run still counts as alive after they were handed over in a wonder trade.
/// </summary>
/// <remarks>
/// <para>
/// A wonder trade adds the Pokémon that arrives and says nothing about the one that leaves, so its
/// record stays <see cref="PokemonStatus.Alive"/> for ever. In the real run that is 28 Pokémon
/// that are not in the game at all, and HOME counting them is the same class of error as not
/// counting a death: a number that describes something other than what it claims to.
/// </para>
/// <para>
/// They are identified by <b>the species the event itself recorded as handed over</b>, matched
/// against the records with no PID — which, now that everything living in the partida has one, is
/// exactly the set that is no longer there. A species where the two counts disagree is reported
/// and left alone; guessing which of two Vanilluxe left would be inventing history.
/// </para>
/// </remarks>
public static class TradedAwayProbe
{
    public static async Task<int> RunAsync(bool repair)
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
        var repository = new SqlitePokemonRepository(database);
        var events = new SqliteEventStore(database);

        var registered = await repository.GetAllAsync(run.Id);
        var history = await events.GetAllAsync(run.Id);

        // Lo entregado, según lo que el propio evento guardó. No se deduce de nada.
        var handedOver = history
            .Where(e => e.Type == GameEventType.WonderTrade)
            .Select(e => e.Data.TryGetValue("entregado", out var species) && int.TryParse(species, out var id)
                ? id
                : 0)
            .Where(id => id > 0)
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        // Sin PID = no está en la partida, ahora que todo lo que vive en ella tiene uno.
        var gone = registered
            .Where(p => p.Pid is null or 0 && p.Status == PokemonStatus.Alive)
            .GroupBy(p => p.Species)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ObtainedAt).ToList());

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine($"  wonder trades en el historial: {handedOver.Values.Sum()}");
        Console.WriteLine($"  registrados vivos y sin PID:   {gone.Values.Sum(list => list.Count)}");
        Console.WriteLine();

        var matched = new List<PokemonEntry>();
        var disputed = new List<string>();

        foreach (var (species, count) in handedOver)
        {
            if (!gone.TryGetValue(species, out var candidates) || candidates.Count != count)
            {
                disputed.Add($"especie {species}: el historial dice {count} entregado(s), "
                             + $"y sin PID hay {(gone.TryGetValue(species, out var c) ? c.Count : 0)}");
                continue;
            }

            matched.AddRange(candidates);
        }

        Console.WriteLine($"  se pueden marcar como entregados: {matched.Count}");
        Console.WriteLine($"  en disputa (no se tocan):         {disputed.Count}");

        foreach (var line in disputed)
        {
            Console.WriteLine($"    {line}");
        }

        Console.WriteLine();

        foreach (var entry in matched.Take(8))
        {
            Console.WriteLine($"    {entry.SpeciesName,-14} {entry.Origin,-12} {entry.ObtainedAt.LocalDateTime:dd/MM HH:mm}");
        }

        if (matched.Count > 8)
        {
            Console.WriteLine($"    ... y {matched.Count - 8} más");
        }

        Console.WriteLine();

        if (!repair)
        {
            Console.WriteLine("Esto solo mira. Para escribirlo: --intercambiados --arreglar");
            return 0;
        }

        foreach (var entry in matched)
        {
            await repository.SaveAsync(entry with { Status = PokemonStatus.Traded });

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = DateTimeOffset.Now,
                Type = GameEventType.PokemonTraded,
                Source = EventSource.System,
                Actor = run.PlayerName,
                Description = $"{entry.Nickname ?? entry.SpeciesName} se entregó en un wonder trade "
                              + "y ya no está en la partida.",
                PokemonId = entry.Id,
                Data = new Dictionary<string, string>
                {
                    ["especie"] = entry.Species.ToString(),
                    ["motivo"] = "reparacion: entregado en wonder trade y contado como vivo"
                }
            });
        }

        // Se relee: no se da por escrito lo que no se ha vuelto a ver.
        var after = await repository.GetAllAsync(run.Id);

        Console.WriteLine($"Marcados {matched.Count}. Vivos: {after.Count(p => p.Status == PokemonStatus.Alive)} "
                          + $"de {after.Count}.");

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
