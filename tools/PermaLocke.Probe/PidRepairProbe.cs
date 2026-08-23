using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Gives the Pokémon PermaLocke already delivered a personality value, and teaches the run which
/// one belongs to which of its own records.
/// </summary>
/// <remarks>
/// <para>
/// Two halves of one problem. <see cref="PokemonBuilder"/> never set the PID, so 149 of the 154
/// Pokémon in the real save carried zero; and the gacha and the wonder trade saved their entry
/// before the Pokémon existed, so the run kept no PID either. <c>GameWatcher</c> matches the live
/// party against the run by PID and by nothing else, so both halves have to be fixed for a death
/// to be detectable: a Pokémon needs an identity, and the run needs to know it.
/// </para>
/// <para>
/// The run side is matched by the <b>six IVs plus the shiny flag</b>, and nothing else. Level
/// rises, EVs change, nicknames change and species evolve; IVs never do. A signature is only
/// accepted when it is unique on <em>both</em> sides — one entry, one Pokémon in the save — so an
/// ambiguous case is reported and skipped rather than guessed.
/// </para>
/// <para>
/// Looking, proving and writing are three separate commands. <c>--probar</c> does the whole repair
/// on a <b>copy</b> of the partida and throws it away, which is the only way to see the round trip
/// through a real file without gambling with the player's game.
/// </para>
/// </remarks>
public static class PidRepairProbe
{
    public static async Task<int> RunAsync(bool repair, bool test)
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

        var player = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (player.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Run: {run.Name}");
        Console.WriteLine($"Partida: {path}");
        Console.WriteLine();

        var pids = new SavePidRepair(player, Path.Combine(saves, "backup"),
            NullLogger<SavePidRepair>.Instance);

        if (test)
        {
            return TestOnACopy(pids, path);
        }

        // Primero la partida: sin PID en el juego no hay nada que guardar en la run.
        var report = repair ? pids.Repair() : pids.Inspect();

        Console.WriteLine("EN LA PARTIDA");
        Console.WriteLine($"  {report.Message}");

        foreach (var given in report.Given.Take(6))
        {
            Console.WriteLine($"    {given.Where,-24} {given.Species,-14} PID {given.Pid:X8}");
        }

        if (report.Given.Count > 6)
        {
            Console.WriteLine($"    ... y {report.Given.Count - 6} más");
        }

        Console.WriteLine();

        if (repair && report.WithoutPid > 0 && !report.Written)
        {
            // La partida no se ha tocado, así que emparejar ahora guardaría ceros en la run.
            return 1;
        }

        return await LinkAsync(run, repository, new SqliteEventStore(database), player, path, repair);
    }

    /// <summary>
    /// Runs the whole repair against a copy of the partida, checks it, and deletes the copy.
    /// </summary>
    private static int TestOnACopy(SavePidRepair pids, string path)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"permalocke-pid-{Guid.NewGuid():N}.sav");

        try
        {
            File.Copy(path, copy, overwrite: true);

            var before = pids.RepairIn(copy, write: false);
            Console.WriteLine($"Antes:   {before.Message}");

            var after = pids.RepairIn(copy, write: true);
            Console.WriteLine($"Escrito: {after.Message}");

            var again = pids.RepairIn(copy, write: false);
            Console.WriteLine($"Releído: {again.Message}");

            // La prueba de verdad: la segunda pasada no encuentra nada que hacer.
            return again.WithoutPid == 0 ? 0 : 1;
        }
        finally
        {
            if (File.Exists(copy))
            {
                File.Delete(copy);
            }
        }
    }

    /// <summary>Matches the run's records against the partida and, when asked, writes the link.</summary>
    private static async Task<int> LinkAsync(Run run, IPokemonRepository repository, IEventStore events,
        PlayerSave player, string path, bool write)
    {
        var registered = await repository.GetAllAsync(run.Id);
        var missing = registered.Where(p => p.Pid is null or 0).ToList();
        var taken = registered.Where(p => p.Pid is > 0).Select(p => p.Pid!.Value).ToHashSet();

        Console.WriteLine("EN LA RUN");
        Console.WriteLine($"  registrados sin PID: {missing.Count} de {registered.Count}");

        if (missing.Count == 0)
        {
            Console.WriteLine("  no hay nada que emparejar.");
            return 0;
        }

        var reader = new SaveBoxReader(player, new PkhexLocationLookup(), "es",
            NullLogger<SaveBoxReader>.Instance);

        var snapshot = reader.ReadFrom(path);

        if (!snapshot.Available)
        {
            Console.WriteLine($"  {snapshot.Problem}");
            return 1;
        }

        // Un Pokémon de la partida que ya es de otra entrada no se vuelve a repartir, y uno a
        // cero no identifica nada.
        var inGame = snapshot.Boxes
            .SelectMany(box => box.Pokemon)
            .Where(p => !p.IsEgg && p.Pid != 0 && !taken.Contains(p.Pid))
            .ToList();

        var ivs = await IvsByPokemonAsync(events, run.Id);

        // Sin candidatos libres no hay nada que emparejar, y sin IVs guardados tampoco. Son los
        // dos motivos por los que un «no está en la partida» puede no significar lo que dice.
        Console.WriteLine($"  Pokémon en la partida sin dueño: {inGame.Count} de "
                          + $"{snapshot.Boxes.Sum(box => box.Count)}");
        Console.WriteLine($"  de los que faltan, con IVs en el historial: "
                          + $"{missing.Count(p => ivs.ContainsKey(p.Id))} de {missing.Count}");

        // Los que están en la partida y no son de nadie. Al revés que la lista de abajo, y tan
        // interesante: un Pokémon sin registrar es uno que la run no sabe que tiene.
        foreach (var orphan in inGame)
        {
            Console.WriteLine($"    sin registrar: {orphan.SpeciesName,-14} «{orphan.DisplayName}» "
                              + $"Nv.{orphan.Level} PID {orphan.Pid:X8} "
                              + $"{(orphan.IsInParty ? "equipo" : $"caja {orphan.Box + 1}")}");
        }

        // Con el filtro de dueño quitado: dice si el Pokémon está en la partida pero ya asignado,
        // que es una situación muy distinta de que no esté.
        var anywhere = snapshot.Boxes
            .SelectMany(box => box.Pokemon)
            .Where(p => !p.IsEgg)
            .GroupBy(Signature)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var entry in missing.Take(5))
        {
            var signature = ivs.TryGetValue(entry.Id, out var v) ? $"{v}|{entry.IsShiny}" : "(sin IVs)";
            var found = anywhere.TryGetValue(signature, out var hits) ? hits.Count : 0;

            Console.WriteLine($"    {entry.SpeciesName,-14} {entry.Origin,-12} {signature,-24} "
                              + $"en la partida: {found}");
        }

        var byGame = inGame.GroupBy(Signature).ToDictionary(g => g.Key, g => g.ToList());
        var byRun = missing
            .GroupBy(entry => ivs.TryGetValue(entry.Id, out var v) ? $"{v}|{entry.IsShiny}" : string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        var matched = new List<(PokemonEntry Entry, BoxedPokemon Found)>();
        var ambiguous = 0;
        var absent = 0;

        foreach (var (signature, entries) in byRun)
        {
            if (signature.Length == 0 || !byGame.TryGetValue(signature, out var candidates))
            {
                // Sin IVs guardados no hay firma, y sin candidato no hay a quién emparejar. En
                // ninguno de los dos casos se inventa nada.
                absent += entries.Count;
                continue;
            }

            if (entries.Count != 1 || candidates.Count != 1)
            {
                ambiguous += entries.Count;
                continue;
            }

            matched.Add((entries[0], candidates[0]));
        }

        Console.WriteLine($"  emparejados por IVs:      {matched.Count,4}");
        Console.WriteLine($"  ambiguos (no se tocan):   {ambiguous,4}");
        Console.WriteLine($"  no están en la partida:   {absent,4}");
        Console.WriteLine();

        foreach (var (entry, found) in matched.Take(8))
        {
            Console.WriteLine($"    {entry.SpeciesName,-14} -> {found.SpeciesName,-14} PID {found.Pid:X8}");
        }

        if (matched.Count > 8)
        {
            Console.WriteLine($"    ... y {matched.Count - 8} más");
        }

        Console.WriteLine();

        if (!write)
        {
            Console.WriteLine("Esto solo mira. Para escribirlo: --pids --arreglar");
            return 0;
        }

        foreach (var (entry, found) in matched)
        {
            await repository.SaveAsync(entry with { Pid = found.Pid });
        }

        // Se relee de la base de datos: no se da por escrito lo que no se ha vuelto a ver.
        var after = await repository.GetAllAsync(run.Id);
        var left = after.Count(p => p.Pid is null or 0);

        Console.WriteLine($"Escritos {matched.Count}. Siguen sin PID: {left}.");

        return 0;
    }

    /// <summary>
    /// The six IVs and the shiny flag, which is everything about a Pokémon that never changes.
    /// </summary>
    /// <remarks>
    /// The order is deliberately the <b>roll's</b>, HP/Atk/Def/Spe/SpA/SpD, and not the reader's,
    /// HP/Atk/Def/SpA/SpD/Spe. Both are used in this codebase, and the two are indistinguishable
    /// on a Pokémon whose special and speed IVs happen to be equal — which is exactly how a
    /// mismatch of this kind hides: it matches a few and silently misses the rest.
    /// </remarks>
    private static string Signature(BoxedPokemon pokemon)
    {
        var ivs = pokemon.Ivs;
        return $"{ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[5]}/{ivs[3]}/{ivs[4]}|{pokemon.IsShiny}";
    }

    /// <summary>
    /// The IVs of every Pokémon the run granted, taken from the event that granted it.
    /// </summary>
    /// <remarks>
    /// They are not on <see cref="PokemonEntry"/>; they are in the <c>ivs</c> field of the gacha
    /// or wonder trade event, which is exactly the kind of thing the event log is for. An entry
    /// whose event does not carry them is left alone rather than matched on something weaker.
    /// </remarks>
    private static async Task<Dictionary<Guid, string>> IvsByPokemonAsync(IEventStore events, Guid runId)
    {
        var map = new Dictionary<Guid, string>();

        foreach (var e in await events.GetAllAsync(runId))
        {
            if (e.PokemonId is { } id && e.Data.TryGetValue("ivs", out var ivs) && ivs.Length > 0)
            {
                map[id] = ivs;
            }
        }

        return map;
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
