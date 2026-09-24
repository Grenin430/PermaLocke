using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Infrastructure;

namespace PermaLocke.Probe;

/// <summary>
/// What the move reminder offers each member of the party in the real partida, and — with <c>--probar</c> — the
/// write proved <b>on a copy</b> of it (§142).
/// </summary>
/// <remarks>
/// Same arrangement as <see cref="EvProbe"/>: the unit tests cover the edit on a save built in memory, and only a
/// real file proves the round trip. The partida is opened read-only, the copy is written, re-read and deleted, and
/// nothing is recorded in the run: the writer is called directly, never through the service, whose writer points
/// at the real save.
/// </remarks>
public static class MoveReminderProbe
{
    public static async Task<int> RunAsync(bool test)
    {
        InstalledWorld.ApplyQuietly(AppContext.BaseDirectory);

        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        var catalog = new WorldMoveCatalog();
        Console.WriteLine($"Partida: {path}");
        Console.WriteLine($"Aprendizajes del {catalog.Source}.");

        var snapshot = new SaveBoxReader(save, new PkhexLocationLookup(), "es", NullLogger<SaveBoxReader>.Instance)
            .ReadFrom(path);

        if (snapshot.Party is not { Count: > 0 } party)
        {
            Console.WriteLine(snapshot.Problem ?? "El equipo está vacío.");
            return 1;
        }

        var (service, runId) = await ServiceAsync(catalog);
        (BoxedPokemon Pokemon, MoveReminderOptions Options)? candidate = null;

        foreach (var member in party.Pokemon)
        {
            var options = await service.OptionsAsync(runId, member);
            string Name(int move) => move == 0 ? "—" : catalog.Describe(move)?.Name ?? $"#{move}";

            Console.WriteLine();
            Console.WriteLine($"{member.Slot + 1}. {member.DisplayName} (#{member.Species}"
                              + (member.Form > 0 ? $" forma {member.Form}" : string.Empty)
                              + $") Nv.{options.Level}"
                              + (member.IsIntact ? string.Empty : "  [DAÑADO]"));
            Console.WriteLine($"   sabe: {string.Join(" · ", options.Known.Select(Name))}");
            Console.WriteLine($"   al llegar apuntado: {(options.FirstKnownRecorded ? "sí" : "no")}"
                              + $"   para volver a aprender (juego): {string.Join(",", (member.RelearnMoveIds ?? []).Where(m => m > 0))}");

            foreach (var group in options.Moves.GroupBy(o => o.From))
            {
                Console.WriteLine($"   {group.Key}: " + string.Join(" · ", group.Select(o =>
                    (o.From == RememberedFrom.Level ? $"Nv{o.Level} " : string.Empty) + Name(o.Move))));
            }

            if (options.Moves.Count == 0)
            {
                Console.WriteLine("   (nada que recordar)");
            }

            if (candidate is null && member.IsIntact && options.Moves.Count > 0)
            {
                candidate = (member, options);
            }
        }

        return test ? Test(path, catalog, candidate) : 0;
    }

    /// <summary>
    /// The service over the real run, read only: its writer is one that refuses, so nothing could be written through
    /// it even by mistake.
    /// </summary>
    private static async Task<(MoveReminderService Service, Guid RunId)> ServiceAsync(IMoveCatalog catalog)
    {
        var saves = Path.Combine(Root(), "Saves");
        var database = Path.Combine(saves, "permalocke.db");
        var run = File.Exists(database)
            ? (await new JsonRunRepository(saves).GetAllAsync()).OrderByDescending(r => r.CreatedAt).FirstOrDefault()
            : null;

        IEventStore events = run is null ? new NoEvents() : new SqliteEventStore(database);
        IPokemonRepository pokemon = run is null ? new NoPokemon() : new SqlitePokemonRepository(database);

        return (new MoveReminderService(catalog, new NeverWrites(), events, pokemon, new SystemClock()),
            run?.Id ?? Guid.Empty);
    }

    private static int Test(string path, IMoveCatalog catalog,
        (BoxedPokemon Pokemon, MoveReminderOptions Options)? candidate)
    {
        Console.WriteLine();

        if (candidate is not { } chosen)
        {
            Console.WriteLine("Nadie del equipo tiene nada que recordar: no hay con qué probar.");
            return 1;
        }

        var (pokemon, options) = chosen;
        var move = options.Moves[^1].Move;
        var slot = Array.IndexOf(options.Known.ToArray(), 0) is var empty and >= 0 ? empty : 3;
        var sheet = catalog.Describe(move)!;

        var change = new MoveChange(pokemon.Box, pokemon.Slot, pokemon.Pid, pokemon.DisplayName, slot,
            options.Known[slot], move, sheet.PP);

        var copy = Path.Combine(Path.GetTempPath(), $"permalocke-mov-{Guid.NewGuid():N}.main");
        File.Copy(path, copy);

        try
        {
            Console.WriteLine($"Prueba sobre una COPIA: {pokemon.DisplayName} aprende {sheet.Name} ({sheet.PP} PP) "
                              + $"en el hueco {slot + 1}, en lugar de "
                              + (change.Replaced == 0 ? "nada" : catalog.Describe(change.Replaced)?.Name));

            var teacher = new SaveMoveTeacher(save: null!,
                Path.Combine(Path.GetTempPath(), "permalocke-mov-backup"), NullLogger<SaveMoveTeacher>.Instance);
            var result = teacher.ApplyIn(copy, change);

            Console.WriteLine($"  resultado: {result.Outcome} — {result.Message}");

            var reread = new SaveBoxReader(save: null!, new PkhexLocationLookup(), "es",
                NullLogger<SaveBoxReader>.Instance).ReadFrom(copy);
            var after = reread.Party?.Pokemon.FirstOrDefault(p => p.Pid == pokemon.Pid);

            Console.WriteLine($"  releído de la copia: {string.Join(" · ", after?.MoveIds?.Select(m => m == 0 ? "—" : catalog.Describe(m)?.Name ?? $"#{m}") ?? [])}");

            var ok = result.Delivered && after?.MoveIds?[slot] == move && after.IsIntact;
            Console.WriteLine(ok ? "  BIEN: la copia lleva el movimiento y la entrada cuadra con su firma."
                                 : "  MAL: la copia no dice lo que se escribió.");
            return ok ? 0 : 1;
        }
        finally
        {
            File.Delete(copy);
        }
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

    private sealed class NeverWrites : IMoveTeacher
    {
        public bool CanTeachNow(out string reason)
        {
            reason = "La sonda no escribe en la partida.";
            return false;
        }

        public Task<DeliveryResult> ApplyAsync(MoveChange change, CancellationToken ct = default) =>
            Task.FromResult(new DeliveryResult(DeliveryOutcome.Failed, "La sonda no escribe en la partida."));
    }

    private sealed class NoEvents : IEventStore
    {
        public Task<GameEvent> AppendAsync(GameEvent gameEvent, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GameEvent>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>([]);

        public Task<IReadOnlyList<GameEvent>> GetLatestAsync(Guid runId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GameEvent>>([]);

        public Task<IntegrityReport> VerifyChainAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoPokemon : IPokemonRepository
    {
        public Task<IReadOnlyList<PokemonEntry>> GetAllAsync(Guid runId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PokemonEntry>>([]);

        public Task<PokemonEntry?> GetAsync(Guid pokemonId, CancellationToken ct = default) =>
            Task.FromResult<PokemonEntry?>(null);

        public Task SaveAsync(PokemonEntry pokemon, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
