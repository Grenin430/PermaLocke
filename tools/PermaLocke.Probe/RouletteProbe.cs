using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PKHeX.Core;
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
/// Runs every face of the LUDÓPATA wheel against a <b>copy</b> of the player's partida.
/// </summary>
/// <remarks>
/// <para>
/// The unit tests build a save in memory, which covers the edit but not the round trip through a
/// real file, and the faces that matter most here are the ones that <b>destroy</b> something: a
/// Pokémon turned into a Shedinja, six IVs set to zero. Those had never touched a real save, so the
/// first time they ran for real would also have been the first time that code ran at all.
/// </para>
/// <para>
/// Each face gets its own fresh copy so they cannot mask each other, and each is checked by reading
/// the file back. The partida is opened read-only and the copies are deleted. Nothing here can
/// write to the player's game even if it wanted to: every write goes to a path under the temporary
/// folder.
/// </para>
/// </remarks>
public static class RouletteProbe
{
    public static async Task<int> RunAsync(bool test)
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

        // El puerto del mundo apunta al TEMPORAL. Aunque algo llamase por error al camino normal
        // de la ruleta, la copia de seguridad y la escritura irian ahi y no a la partida.
        var scratch = Path.Combine(Path.GetTempPath(), $"permalocke-ruleta-{Guid.NewGuid():N}");

        collection.AddSingleton<IRouletteWorldPort>(sp => new SaveRouletteWorld(
            sp.GetRequiredService<PlayerSave>(), Path.Combine(scratch, "backup"),
            sp.GetRequiredService<ISpeciesLookup>(), sp.GetRequiredService<IItemLookup>(),
            sp.GetRequiredService<IAbilityLookup>(),
            sp.GetRequiredService<ILogger<SaveRouletteWorld>>()));
        collection.AddSingleton<RouletteService>();

        using var services = collection.BuildServiceProvider();

        var catalog = services.GetRequiredService<IRouletteCatalog>();
        var save = services.GetRequiredService<PlayerSave>();

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida: {path}");
        Console.WriteLine($"Caras configuradas: {catalog.Faces.Count}");
        Console.WriteLine();

        var world = (SaveRouletteWorld)services.GetRequiredService<IRouletteWorldPort>();
        var wheel = services.GetRequiredService<RouletteService>();

        var seen = world.ReadFrom(path);

        Console.WriteLine($"EQUIPO: {seen.Party.Count}");
        foreach (var member in seen.Party)
        {
            Console.WriteLine($"  hueco {member.Slot + 1}  {member.Name,-14} Nv.{member.Level,-3} PID {member.Pid:X8}");
        }

        Console.WriteLine($"MOCHILA: {seen.TmsHeld.Count} MT, {seen.HealingHeld.Count} curativos distintos");
        Console.WriteLine();

        if (!test)
        {
            Console.WriteLine("Esto solo mira. Para probar cada cara SOBRE UNA COPIA: --ruleta --probar");
            return 0;
        }

        if (seen.Party.Count == 0)
        {
            Console.WriteLine("Sin equipo no se puede probar nada de lo que toca al equipo.");
            return 1;
        }

        var failures = 0;

        // Se crea solo cuando de verdad se va a probar: mirar no deja carpetas por ahi.
        Directory.CreateDirectory(scratch);

        try
        {
            foreach (var face in catalog.Faces)
            {
                failures += Try(wheel, world, path, scratch, face, seen) ? 0 : 1;
            }
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "TODAS LAS CARAS HACEN LO QUE DICEN. La partida no se ha tocado."
            : $"{failures} caras no hacen lo que dicen. La partida no se ha tocado.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Applies one face to its own copy and reads the file back to check it.</summary>
    private static bool Try(RouletteService wheel, SaveRouletteWorld world, string original,
        string scratch, RouletteFace face, RouletteWorld seen)
    {
        var copy = Path.Combine(scratch, $"{face.Id}.sav");
        File.Copy(original, copy, overwrite: true);

        // La misma semilla para todas las caras: lo que se comprueba es el efecto, no el sorteo,
        // y así dos caras que tocan al equipo caen sobre los mismos Pokémon y se pueden comparar.
        var source = new SeededRandomSource(20260827).Derive(face.Id);
        var action = wheel.Decide(face, seen, source);

        if (face.Effect is RouletteEffect.Gacha or RouletteEffect.Puntos)
        {
            Console.WriteLine($"  {face.Name,-26} no toca la partida ({face.Effect})");
            return true;
        }

        if (action.IsEmpty)
        {
            Console.WriteLine($"  {face.Name,-26} no había nada sobre lo que aplicarla");
            return true;
        }

        // Lo que habia ANTES de tocar nada, leido de la propia copia: sin eso no se puede decir
        // si un objeto bajo lo que tenia que bajar, solo si acabo con algun numero.
        var before = Counts(copy, action);

        var applied = world.ApplyIn(copy, action);

        if (!applied.Applied)
        {
            Console.WriteLine($"  {face.Name,-26} FALLA: {applied.Message}");
            return false;
        }

        var problem = Check(copy, face, action, before);

        Console.WriteLine(problem is null
            ? $"  {face.Name,-26} OK   {string.Join(" | ", applied.Lines)}"
            : $"  {face.Name,-26} MAL  {problem}");

        return problem is null;
    }

    /// <summary>
    /// Re-reads the copy and checks the face really did what it promised.
    /// </summary>
    /// <remarks>
    /// Deliberately checks the <em>meaning</em> and not that the write returned true: the writer
    /// already verifies its own bytes, and a check that only repeats what the writer said would
    /// catch nothing the writer got wrong.
    /// </remarks>
    private static string? Check(string copy, RouletteFace face, RouletteAction action,
        IReadOnlyDictionary<int, int> before)
    {
        if (!SaveUtil.TryGetSaveFile(copy, out var loaded) || loaded is not SAV7USUM game)
        {
            return "la copia ya no se lee como Ultra Luna";
        }

        for (var i = 0; i < action.Pokemon.Count; i++)
        {
            var target = action.Pokemon[i];

            if (game.GetPartySlotAtIndex(target.Slot) is not PK7 pokemon)
            {
                return $"el hueco {target.Slot + 1} se ha quedado vacío";
            }

            if (!pokemon.ChecksumValid)
            {
                return $"el hueco {target.Slot + 1} ha quedado con el checksum roto";
            }

            var wrong = face.Effect switch
            {
                RouletteEffect.HabilidadBuena or RouletteEffect.HabilidadMala =>
                    pokemon.Ability != action.Abilities[i]
                        ? $"{target.Name} tiene la habilidad {pokemon.Ability} y debía tener {action.Abilities[i]}"
                        : null,

                RouletteEffect.IvPerfectos =>
                    Ivs(pokemon).Any(iv => iv != 31) ? $"{target.Name} no tiene los seis IV a 31" : null,

                RouletteEffect.IvCero =>
                    Ivs(pokemon).Any(iv => iv != 0) ? $"{target.Name} no tiene los seis IV a 0" : null,

                RouletteEffect.Muerte =>
                    pokemon.Species != 292 || pokemon.Nickname != "MUERTO"
                    || pokemon.Move1 + pokemon.Move2 + pokemon.Move3 + pokemon.Move4 != 0
                        ? $"{target.Name} no ha quedado marcado como muerto"
                        : null,

                _ => null
            };

            if (wrong is not null)
            {
                return wrong;
            }
        }

        foreach (var change in action.Items)
        {
            var pouch = game.Inventory.Pouches
                .FirstOrDefault(p => p.GetAllItems().Contains((ushort)change.ItemId));

            if (pouch is null)
            {
                return $"el objeto {change.ItemId} no cabe en ningun bolsillo";
            }

            var was = before.GetValueOrDefault(change.ItemId);
            var wanted = Math.Clamp(was + change.Delta, 0, pouch.MaxCount);
            var now = pouch.Items.FirstOrDefault(item => item.Index == change.ItemId)?.Count ?? 0;

            if (now != wanted)
            {
                return $"el objeto {change.ItemId} estaba a {was}, debia quedar en {wanted} y hay {now}";
            }
        }

        return null;
    }

    /// <summary>How many of each item the action touches the save holds right now.</summary>
    private static Dictionary<int, int> Counts(string path, RouletteAction action)
    {
        var counts = new Dictionary<int, int>();

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return counts;
        }

        foreach (var change in action.Items)
        {
            var pouch = game.Inventory.Pouches
                .FirstOrDefault(p => p.GetAllItems().Contains((ushort)change.ItemId));

            counts[change.ItemId] =
                pouch?.Items.FirstOrDefault(item => item.Index == change.ItemId)?.Count ?? 0;
        }

        return counts;
    }

    private static int[] Ivs(PK7 pokemon) =>
        [pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE];

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
