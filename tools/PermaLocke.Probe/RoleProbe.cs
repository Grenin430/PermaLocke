using PermaLocke.Core;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.Infrastructure;

namespace PermaLocke.Probe;

/// <summary>Commits a role migration after its new LayeredFS mod has been generated and installed.</summary>
public static class RoleProbe
{
    public static async Task<int> ChangeAsync(string targetRoleId)
    {
        var root = Root();
        var saves = Path.Combine(root, "Saves");
        var roles = JsonRoleCatalog.Load(Path.Combine(root, "Data", "roles.json"));

        if (roles.Find(targetRoleId) is not { } target)
        {
            Console.WriteLine($"No existe el rol «{targetRoleId}». Hay: {string.Join(", ", roles.All.Select(r => r.Id))}");
            return 1;
        }

        var repository = new JsonRunRepository(saves);
        var all = await repository.GetAllAsync();

        if (all.Count == 0)
        {
            Console.WriteLine("No hay ninguna run.");
            return 1;
        }

        var run = all.OrderByDescending(r => r.CreatedAt).First();

        if (string.Equals(run.RoleId, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"La run «{run.Name}» ya es {target.Name}.");
            return 0;
        }

        using var events = new SqliteEventStore(Path.Combine(saves, "permalocke.db"));
        var context = new RunContext();
        var pokemon = new SqlitePokemonRepository(Path.Combine(saves, "permalocke.db"));
        var service = new RunService(repository, events, pokemon, context, new SystemClock());
        var updated = await service.ChangeRoleAsync(run, target.Id,
            "Mod LayeredFS regenerado e instalado antes de migrar la run.");

        // A role change is not considered done until the readable run file agrees with the chain.
        var reread = await repository.GetAsync(updated.Id);
        var migration = (await events.GetAllAsync(updated.Id)).LastOrDefault(e => e.Type == GameEventType.RoleChanged);

        if (reread?.RoleId != target.Id || migration is null)
        {
            Console.WriteLine("La migración no se pudo comprobar. La run no se da por cambiada.");
            return 1;
        }

        Console.WriteLine($"Run: {updated.Name}");
        Console.WriteLine($"Rol: {run.RoleId} -> {updated.RoleId}");
        Console.WriteLine($"Evento: {migration.Id}");
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
