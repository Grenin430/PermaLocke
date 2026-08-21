using System.Diagnostics;
using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using PermaLocke.Randomizer.Sprites;
using pk3DS.Core;
using pk3DS.Core.CTR;

// Herramienta de desarrollo: ejecuta el randomizador contra la ROM real y deja el mod donde
// Azahar lo lee. No forma parte de la aplicación que usan los jugadores.

var root = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
var work = Path.Combine(Path.GetTempPath(), "permalocke-romtool");

var command = args.Length > 0 ? args[0] : "help";
switch (command)
{
    case "inspect":
        await InspectAsync();
        break;
    case "names":
        await NamesAsync(args.Skip(1).Select(int.Parse).ToArray());
        break;
    case "items":
        await ItemsAsync(int.Parse(args[1]), int.Parse(args[2]));
        break;
    case "fielditems":
        await FieldItemsAsync();
        break;
    case "shops":
        await ShopsAsync();
        break;
    case "pokemon":
        await PokemonAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818);
        break;
    case "trainers":
        await TrainersAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818);
        break;
    case "statics":
        await StaticsAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818);
        break;
    case "species":
        await SpeciesAsync();
        break;
    case "sprites":
        Sprites(args.Contains("--sheets"));
        break;
    case "zones":
        await ZonesAsync();
        break;
    case "dump":
        await DumpAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818, args.Length > 2 ? args[2] : "Ruta 1");
        break;
    case "randomize":
        await RandomizeAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818);
        break;
    default:
        Console.WriteLine("""
            Uso:
              PermaLocke.RomTool inspect              datos de la ROM y de los GARC
              PermaLocke.RomTool names <id> [id...]   nombres de especie leídos de la ROM
              PermaLocke.RomTool randomize [seed]     genera el mod en la carpeta de Azahar
              PermaLocke.RomTool sprites [--sheets]   vuelca los iconos de Pokémon a Data/sprites
            """);
        break;
}

string? FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
    {
        directory = directory.Parent;
    }
    return directory?.FullName;
}

string RequireRom()
{
    var folder = Path.Combine(root, "ROM");
    var rom = RomInspector.ScanFolder(folder).FirstOrDefault(r => r.IsSupported)
              ?? throw new FileNotFoundException($"No hay ninguna ROM compatible y desencriptada en {folder}.");
    Console.WriteLine($"ROM: {rom.FileName}");
    Console.WriteLine($"     {rom.Game}  TitleID {rom.TitleId}  {rom.ProductCode}  desencriptada={rom.IsDecrypted}");
    return rom.Path;
}

async Task InspectAsync()
{
    var sw = Stopwatch.StartNew();
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    Console.WriteLine($"\nExtraídos {GameFiles.All.Count} ficheros en {sw.ElapsedMilliseconds} ms");

    var config = workspace.Config;
    Console.WriteLine($"{config.Version}  gen {config.Generation}  especies {config.MaxSpeciesID}  encdata -> {config.GetGARCFileName("encdata")}");
    Console.WriteLine($"personal {config.Personal.Table.Length}  movimientos {config.Moves.Length}  evoluciones {config.Evolutions.Length}");

    foreach (var file in GameFiles.All)
    {
        var info = new FileInfo(workspace.PathOf(file));
        Console.WriteLine($"  {file}  {info.Length,12:N0} bytes");
    }
}

async Task NamesAsync(int[] ids)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    foreach (var id in ids)
    {
        Console.WriteLine($"{id,4}  {(id < names.Length ? names[id] : "(fuera de rango)")}");
    }
}

async Task RandomizeAsync(ulong seed)
{
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));

    // Por defecto se escribe en Randomized/, no en Azahar: instalar el mod cambia la partida en
    // curso, así que es una decisión explícita del jugador.
    var install = args.Contains("--install");
    var mod = install
        ? LayeredFsMod.DirectoryFor(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar"))
        : Path.Combine(root, "Randomized", $"seed-{seed}");

    Console.WriteLine($"seed {seed}\nsalida {mod}{(install ? "  (INSTALANDO EN AZAHAR)" : "")}\n");
    var report = await new RandomizerService(options).RandomizeAsync(RequireRom(), work, mod, seed);

    Console.WriteLine();
    foreach (var step in report.Steps)
    {
        Console.WriteLine($"  {step.Module}: {step.Detail}");
    }
    Console.WriteLine($"\n{report.Files.Count} ficheros, {report.TotalBytes / 1024.0 / 1024.0:F1} MB, en {report.Elapsed.TotalSeconds:F1} s");

    if (report.PreviousModKept is { } kept)
    {
        Console.WriteLine($"El mod anterior está guardado en {kept}, por si hay que volver a él.");
    }
}

async Task DumpAsync(ulong seed, string zone)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var locations = workspace.Config.GetText(TextName.metlist_000000);

    var generated = Path.Combine(root, "Randomized", $"seed-{seed}", "romfs",
        GameFiles.EncounterDataUltraMoon.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(generated))
    {
        Console.WriteLine($"No existe {generated}. Ejecuta antes: randomize {seed}");
        return;
    }

    // Se lee el fichero generado con el lector de pk3DS, no con el código que lo escribió.
    File.Copy(generated, workspace.PathOf(GameFiles.EncounterDataUltraMoon), overwrite: true);
    var areas = Area7.GetArray(workspace.Config.GetlzGARCData("encdata"),
        workspace.Config.GetlzGARCData("zonedata"),
        workspace.Config.GetlzGARCData("worlddata"), locations);

    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var banned = options.BannedSpecies.ToHashSet();
    var offenders = 0;
    var total = 0;

    foreach (var area in areas.Where(a => a.HasTables))
    {
        foreach (var table in area.Tables)
        {
            foreach (var slot in table.Encounter7s[0])
            {
                if (slot.Species == 0) continue;
                total++;
                if (banned.Contains((int)slot.Species)) offenders++;
            }
        }
    }
    Console.WriteLine($"\n{areas.Count(a => a.HasTables)} zonas con tablas, {total} huecos base");
    Console.WriteLine($"huecos con especie prohibida: {offenders}");

    foreach (var area in areas.Where(a => a.HasTables && a.Name.Contains(zone, StringComparison.OrdinalIgnoreCase)).Take(2))
    {
        var t = area.Tables[0];
        Console.WriteLine($"\n### {area.Name}   niveles {t.MinLevel}-{t.MaxLevel}");
        for (var i = 0; i < 10; i++)
        {
            var e = t.Encounter7s[0][i];
            if (e.Species == 0) continue;
            Console.WriteLine($"  {t.Rates[i],3}%  #{e.Species,3} {names[(int)e.Species]}");
        }
    }
}

async Task StaticsAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var banned = options.BannedSpecies.ToHashSet();

    var vanillaPath = workspace.PathOf(GameFiles.EncounterStatic);
    var generatedPath = Path.Combine(root, "Randomized", $"seed-{seed}", "romfs",
        GameFiles.EncounterStatic.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(generatedPath))
    {
        Console.WriteLine($"No existe {generatedPath}. Ejecuta antes: randomize {seed}");
        return;
    }

    // Lector de pk3DS sobre ambos ficheros, no el parcheador que escribió uno de ellos.
    var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(vanillaPath));
    var modded = new GARC.LazyGARC(await File.ReadAllBytesAsync(generatedPath));
    Console.WriteLine($"\nsubficheros: vanilla {vanilla.FileCount}, generado {modded.FileCount}");

    var offenders = 0;
    var changed = 0;
    var total = 0;

    foreach (var layout in (EncounterEntryLayout[])
             [StaticEncounterTable.Gifts, StaticEncounterTable.Statics, StaticEncounterTable.Trades])
    {
        var before = vanilla[layout.Subfile];
        var after = modded[layout.Subfile];
        var count = StaticEncounterTable.Count(after, layout);
        var localChanged = 0;

        for (var i = 0; i < count; i++)
        {
            var oldSpecies = StaticEncounterTable.GetSpecies(before, layout, i);
            var newSpecies = StaticEncounterTable.GetSpecies(after, layout, i);
            if (oldSpecies == 0) continue;
            total++;
            if (oldSpecies != newSpecies) localChanged++;
            if (banned.Contains(newSpecies) && !options.ProtectedSpecies.Contains(newSpecies))
            {
                offenders++;
                Console.WriteLine($"    ¡PROHIBIDA! {layout.Name} entrada {i}: {names[newSpecies]}");
            }
        }

        Console.WriteLine($"  {layout.Name,-14} {count,4} entradas, {before.Length} -> {after.Length} bytes, {localChanged} cambiadas");
        changed += localChanged;
    }

    Console.WriteLine($"\n{changed} de {total} entradas cambiadas; especies prohibidas coladas: {offenders}");

    // Las entradas que siguen igual deberían ser exactamente las protegidas. Se comprueba, no
    // se supone: una entrada sin cambiar por accidente sería un fallo silencioso.
    foreach (var layout in (EncounterEntryLayout[])
             [StaticEncounterTable.Gifts, StaticEncounterTable.Statics, StaticEncounterTable.Trades])
    {
        var before = vanilla[layout.Subfile];
        var after = modded[layout.Subfile];
        for (var i = 0; i < StaticEncounterTable.Count(after, layout); i++)
        {
            var oldSpecies = StaticEncounterTable.GetSpecies(before, layout, i);
            if (oldSpecies == 0 || oldSpecies != StaticEncounterTable.GetSpecies(after, layout, i))
            {
                continue;
            }
            var expected = options.ProtectedSpecies.Contains(oldSpecies) ? "protegida" : "¡SIN CAMBIAR SIN MOTIVO!";
            Console.WriteLine($"  intacta: {layout.Name} entrada {i} = {names[oldSpecies]} ({expected})");
        }
    }

    var gifts = modded[StaticEncounterTable.Gifts.Subfile];
    var vanillaGifts = vanilla[StaticEncounterTable.Gifts.Subfile];
    Console.WriteLine("\niniciales y fósiles:");
    for (var i = 0; i < 14; i++)
    {
        var was = StaticEncounterTable.GetSpecies(vanillaGifts, StaticEncounterTable.Gifts, i);
        var now = StaticEncounterTable.GetSpecies(gifts, StaticEncounterTable.Gifts, i);
        var label = i < 3 ? "inicial" : "fósil";
        Console.WriteLine($"  {label,-8} {names[was],-14} -> {names[now]}");
    }
}

async Task TrainersAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var banned = options.BannedSpecies.ToHashSet();

    var generatedPath = Path.Combine(root, "Randomized", $"seed-{seed}", "romfs",
        GameFiles.TrainerPokemon.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(generatedPath))
    {
        Console.WriteLine($"No existe {generatedPath}. Ejecuta antes: randomize {seed}");
        return;
    }

    var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.TrainerPokemon)));
    var modded = new GARC.LazyGARC(await File.ReadAllBytesAsync(generatedPath));
    Console.WriteLine($"\nentrenadores: vanilla {vanilla.FileCount}, generado {modded.FileCount}");

    int levelsMoved = 0, sizeChanged = 0, offenders = 0, replaced = 0, total = 0, itemsLost = 0;
    var highest = 0;
    var highestTrainer = -1;

    for (var t = 0; t < modded.FileCount; t++)
    {
        var before = vanilla[t];
        var after = modded[t];
        if (before.Length != after.Length) { sizeChanged++; continue; }

        for (var s = 0; s < TrainerPokemonTable.Count(after); s++)
        {
            total++;
            if (TrainerPokemonTable.GetLevel(before, s) != TrainerPokemonTable.GetLevel(after, s)) levelsMoved++;
            if (TrainerPokemonTable.GetItem(before, s) != TrainerPokemonTable.GetItem(after, s)) itemsLost++;
            if (TrainerPokemonTable.GetSpecies(before, s) != TrainerPokemonTable.GetSpecies(after, s)) replaced++;
            if (banned.Contains(TrainerPokemonTable.GetSpecies(after, s))) offenders++;

            var level = TrainerPokemonTable.GetLevel(after, s);
            if (level > highest) { highest = level; highestTrainer = t; }
        }
    }

    Console.WriteLine($"  {total} Pokémon, {replaced} con especie nueva");
    Console.WriteLine($"  equipos que cambiaron de tamaño: {sizeChanged}   (debe ser 0)");
    Console.WriteLine($"  NIVELES movidos: {levelsMoved}   (debe ser 0: de ahí salen los caps)");
    Console.WriteLine($"  objetos alterados: {itemsLost}   (debe ser 0)");
    Console.WriteLine($"  especies prohibidas: {offenders}   (debe ser 0)");
    Console.WriteLine($"  nivel más alto del juego: {highest} (entrenador {highestTrainer})");

    var sample = modded[highestTrainer];
    var sampleVanilla = vanilla[highestTrainer];
    Console.WriteLine($"\nentrenador {highestTrainer}, el de nivel más alto:");
    for (var s = 0; s < TrainerPokemonTable.Count(sample); s++)
    {
        Console.WriteLine($"  Nv.{TrainerPokemonTable.GetLevel(sample, s),3}  {names[TrainerPokemonTable.GetSpecies(sampleVanilla, s)],-14} -> {names[TrainerPokemonTable.GetSpecies(sample, s)]}");
    }
}

async Task PokemonAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var types = workspace.Config.GetText(TextName.Types);

    var dir = Path.Combine(root, "Randomized", $"seed-{seed}", "romfs");
    if (!File.Exists(Path.Combine(dir, "a", "0", "1", "7")))
    {
        Console.WriteLine($"No existe {dir}. Ejecuta antes: randomize {seed}");
        return;
    }

    var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.Personal)));
    var modded = new GARC.LazyGARC(await File.ReadAllBytesAsync(Path.Combine(dir, "a", "0", "1", "7")));

    var packedIndex = modded.FileCount - 1;
    var packedBefore = vanilla[packedIndex];
    var packedAfter = modded[packedIndex];
    var rows = packedAfter.Length / PersonalEntry7.Size;

    int totalMoved = 0, disagree = 0, sameStats = 0, zeroStat = 0;
    for (var row = 1; row < rows; row++)
    {
        var at = row * PersonalEntry7.Size;
        if (PersonalEntry7.BaseStatTotal(packedBefore, at) != PersonalEntry7.BaseStatTotal(packedAfter, at)) totalMoved++;

        var same = true;
        for (var s = 0; s < 6; s++)
        {
            if (PersonalEntry7.GetStat(packedBefore, at, s) != PersonalEntry7.GetStat(packedAfter, at, s)) same = false;
            if (PersonalEntry7.GetStat(packedAfter, at, s) == 0) zeroStat++;
        }
        if (same) sameStats++;

        // La fila suelta y la fila de la tabla empaquetada tienen que decir lo mismo.
        if (row < packedIndex)
        {
            var individual = modded[row];
            if (individual.Length == PersonalEntry7.Size
                && !individual.AsSpan().SequenceEqual(packedAfter.AsSpan(at, PersonalEntry7.Size)))
            {
                disagree++;
            }
        }
    }

    Console.WriteLine($"\n{rows - 1} entradas (especies y formas)");
    Console.WriteLine($"  totales de estadísticas movidos: {totalMoved}   (debe ser 0)");
    Console.WriteLine($"  estadísticas a cero: {zeroStat}   (debe ser 0)");
    Console.WriteLine($"  filas sueltas que no cuadran con la tabla empaquetada: {disagree}   (debe ser 0)");
    Console.WriteLine($"  entradas cuyas estadísticas quedaron igual: {sameStats}");

    foreach (var species in (int[])[722, 25, 129])
    {
        var at = species * PersonalEntry7.Size;
        var (t1, t2) = PersonalEntry7.GetTypes(packedAfter, at);
        var (o1, o2) = PersonalEntry7.GetTypes(packedBefore, at);
        var before = string.Join("/", Enumerable.Range(0, 6).Select(s => PersonalEntry7.GetStat(packedBefore, at, s)));
        var after = string.Join("/", Enumerable.Range(0, 6).Select(s => PersonalEntry7.GetStat(packedAfter, at, s)));
        Console.WriteLine($"\n{names[species]}: {types[o1]}/{types[o2]} -> {types[t1]}/{types[t2]}");
        Console.WriteLine($"  stats {before}  ->  {after}   (total {PersonalEntry7.BaseStatTotal(packedBefore, at)} -> {PersonalEntry7.BaseStatTotal(packedAfter, at)})");
    }
}

async Task ShopsAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var items = workspace.Config.GetText(TextName.ItemNames);
    var generated = Path.Combine(root, "Randomized", "seed-20260818", "romfs", "Shop.cro");
    var useGenerated = args.Contains("--gen") && File.Exists(generated);
    var cro = await File.ReadAllBytesAsync(useGenerated ? generated : workspace.PathOf(GameFiles.Shop));
    Console.WriteLine(useGenerated ? "=== FICHERO GENERADO ===" : "=== VANILLA ===");
    var shops = ShopTable.Read(cro);

    Console.WriteLine($"\nShop.cro: {cro.Length} bytes, {shops.Count} inventarios");
    Console.WriteLine($"MT: ids {ShopTable.FirstTechnicalMachine} ({items[ShopTable.FirstTechnicalMachine]}) .. {ShopTable.LastTechnicalMachine} ({items[ShopTable.LastTechnicalMachine]})");

    foreach (var shop in shops)
    {
        var contents = Enumerable.Range(0, shop.Count).Select(s => items[ShopTable.GetItem(cro, shop, s)]);
        var kind = shop.Index < ShopTable.RegularMartCount ? "normal"
            : ShopTable.SellsTechnicalMachines(cro, shop) ? "MT" : "especial";
        Console.WriteLine($"  {shop.Index,2} [{kind,-8}] @0x{shop.Offset:X} x{shop.Count,2}: {string.Join(", ", contents)}");
    }
}

async Task ItemsAsync(int from, int to)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var items = workspace.Config.GetText(TextName.ItemNames);
    Console.WriteLine($"lista de objetos: {items.Length} entradas");
    for (var i = from; i <= to && i < items.Length; i++)
    {
        Console.WriteLine($"  {i,4}  '{items[i]}'");
    }
}

async Task FieldItemsAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var items = workspace.Config.GetText(TextName.ItemNames);
    var locations = workspace.Config.GetText(TextName.metlist_000000);
    var generatedEnc = Path.Combine(root, "Randomized", "seed-20260818", "romfs", "a", "0", "8", "3");
    var useGen = args.Contains("--gen") && File.Exists(generatedEnc);
    Console.WriteLine(useGen ? "=== FICHERO GENERADO ===" : "=== VANILLA ===");
    var garc = new GARC.LazyGARC(await File.ReadAllBytesAsync(useGen ? generatedEnc : workspace.PathOf(GameFiles.EncounterDataUltraMoon)));

    var zones = garc.FileCount / FieldItemTable.SubfilesPerZone;
    int total = 0, machines = 0, zonesWith = 0, unnamed = 0;
    var sample = new List<string>();

    for (var zone = 0; zone < zones; zone++)
    {
        var environment = garc[zone * FieldItemTable.SubfilesPerZone];
        var slots = FieldItemTable.Locate(environment);
        if (slots.Count == 0) continue;
        zonesWith++;

        foreach (var slot in slots)
        {
            var item = FieldItemTable.GetItem(environment, slot);
            total++;
            if (ShopTable.IsTechnicalMachine(item)) machines++;
            if (item >= items.Length || items[item] == "(?)" || string.IsNullOrWhiteSpace(items[item])) unnamed++;
            if (sample.Count < 22 && item != 0) sample.Add($"z{zone}:{(item < items.Length ? items[item] : item.ToString())}");
        }
    }

    Console.WriteLine($"\n{zones} zonas, {zonesWith} con objetos en el suelo");
    Console.WriteLine($"  {total} objetos localizados");
    Console.WriteLine($"  de los cuales MT (las Poké Ball doradas): {machines}");
    Console.WriteLine($"  ids sin nombre real: {unnamed}   (mucho = el localizador está mal)");
    Console.WriteLine($"\n  muestra: {string.Join(", ", sample)}");
}

// Genera Data/zones.json: por cada área de encdata, los nombres de localización que cubre.
// Es el puente entre la zona que se lee de la memoria del juego (§23) y la que registra la run,
// que se identifica por el nombre normalizado de la localización.
async Task ZonesAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var locations = workspace.Config.GetText(TextName.metlist_000000);

    var areas = Area7.GetArray(workspace.Config.GetlzGARCData("encdata"),
        workspace.Config.GetlzGARCData("zonedata"),
        workspace.Config.GetlzGARCData("worlddata"), locations);

    // Area7.Name viene como "000 - Ruta 1 / 003 - Ruta 1 / 010 - Mar de Melemele": una entrada
    // por zona del área. Lo que importa es cuántos nombres DISTINTOS cubre.
    var entries = areas.Select((area, index) => new
    {
        area = index,
        names = area.Name.Split('/')
            .Select(part => part.Split('-', 2)[^1].Trim())
            .Where(name => name.Length > 0)
            .Distinct()
            .ToArray(),
        hasEncounters = area.HasTables
    }).ToArray();

    var ambiguous = entries.Count(e => e.names.Length > 1);
    var withEncounters = entries.Count(e => e.hasEncounters);

    var document = new
    {
        comment = "Generado por: PermaLocke.RomTool zones. No editar a mano.",
        source = "a/0/8/3 (encdata) + a/0/7/7 (zonedata) + a/0/9/1 (worlddata)",
        areas = entries
    };

    var path = Path.Combine(root, "Data", "zones.json");
    await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(document,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"{entries.Length} áreas escritas en {path}");
    Console.WriteLine($"  con encuentros: {withEncounters}");
    Console.WriteLine($"  con más de un nombre (ambiguas): {ambiguous}");

    foreach (var entry in entries.Where(e => e.names.Length > 1 && e.hasEncounters))
    {
        Console.WriteLine($"    área {entry.area,3}: {string.Join(" | ", entry.names)}");
    }

    // Releído del disco, como manda la norma del randomizador: no se publica lo que no se relee.
    using var check = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
    Console.WriteLine($"\nRelectura: {check.RootElement.GetProperty("areas").GetArrayLength()} áreas.");
}

// Genera Data/species.json: por cada especie, el total de estadísticas base y si es especial.
// El total sale de la ROM, que es la fuente de verdad; la clasificación de legendario no está
// en el cartucho de forma utilizable, así que viene de PKHeX.
//
// El total importa porque el gacha reparte por rangos de él. El módulo de datos de Pokémon del
// randomizador BARAJA las estadísticas pero conserva el total, así que esta tabla sigue valiendo
// con una ROM randomizada.
async Task SpeciesAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work);
    var speciesNames = workspace.Config.GetText(TextName.SpeciesNames);
    var abilityNames = workspace.Config.GetText(TextName.AbilityNames);
    var natureNames = workspace.Config.GetText(TextName.Natures);
    var personal = workspace.Config.GetGARCData("personal");

    // El último subfichero es la tabla entera empaquetada; las especies son los anteriores.
    var count = Math.Min(personal.Files.Length - 1, speciesNames.Length);
    var entries = new List<object>();
    var histogram = new Dictionary<string, int>();
    var legendaries = 0;

    for (ushort id = 1; id < count; id++)
    {
        var raw = personal.Files[id];

        if (raw.Length < PersonalEntry7.Size || string.IsNullOrWhiteSpace(speciesNames[id]))
        {
            continue;
        }

        var total = PersonalEntry7.StatOffsets.Sum(offset => (int)raw[offset]);

        if (total <= 0)
        {
            continue;
        }

        var special = PKHeX.Core.SpeciesCategory.IsLegendary(id)
                      || PKHeX.Core.SpeciesCategory.IsSubLegendary(id)
                      || PKHeX.Core.SpeciesCategory.IsMythical(id)
                      || PKHeX.Core.SpeciesCategory.IsUltraBeast(id);

        // Las tres habilidades de la especie, ya resueltas a nombre para que la app no
        // necesite ni la ROM ni PKHeX para enseñarlas.
        var abilities = PersonalEntry7.AbilityOffsets
            .Select(offset => (int)raw[offset])
            .Where(ability => ability > 0 && ability < abilityNames.Length)
            .Select(ability => abilityNames[ability])
            .Distinct()
            .ToArray();

        entries.Add(new { id, name = speciesNames[id], baseStatTotal = total, legendary = special, abilities });

        if (special)
        {
            legendaries++;
        }

        var bucket = total <= 400 ? "1 (<=400)" : total <= 490 ? "2 (<=490)"
            : total <= 535 ? "3 (<=535)" : total <= 590 ? "4 (<=590)" : "5 (>590)";
        histogram[bucket] = histogram.GetValueOrDefault(bucket) + 1;
    }

    var path = Path.Combine(root, "Data", "species.json");
    await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(
        new
        {
            comment = "Generado por: PermaLocke.RomTool species. No editar a mano.",
            natures = natureNames,
            // SIN filtrar: la posición en esta lista ES el id de la habilidad en el cartucho, y
            // quitar los huecos vacíos desplazaría todos los ids a partir del primero.
            abilities = abilityNames,
            species = entries
        },
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"{entries.Count} especies escritas en {path}, {legendaries} especiales\n");
    Console.WriteLine("Reparto por los rangos del gacha:");

    foreach (var bucket in histogram.OrderBy(b => b.Key))
    {
        Console.WriteLine($"  tier {bucket.Key,-12} {bucket.Value,4} especies");
    }

    using var check = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
    Console.WriteLine($"\nRelectura: {check.RootElement.GetProperty("species").GetArrayLength()} especies.");
}

// Vuelca los iconos de Pokémon del cartucho del jugador a PNG. Los sprites son de Nintendo y
// no viajan con PermaLocke: cada uno los saca de su propia ROM, igual que la randomización.
void Sprites(bool sheets)
{
    var rom = RequireRom();
    var reader = PokemonIconReader.Open(rom, work);
    var outDir = Path.Combine(root, "Data", "sprites");
    Directory.CreateDirectory(outDir);

    var sw = Stopwatch.StartNew();
    var written = 0;
    var empty = 0;
    var sizes = new Dictionary<string, int>();

    for (var i = 0; i < reader.Count; i++)
    {
        var icon = reader.Read(i);
        if (icon.Pixels.All(b => b == 0))
        {
            empty++;
            continue;
        }

        File.WriteAllBytes(Path.Combine(outDir, $"{i:0000}.png"),
            PngImage.Encode(icon.Pixels, icon.Width, icon.Height));
        written++;
        var key = $"{icon.Width}x{icon.Height}";
        sizes[key] = sizes.GetValueOrDefault(key) + 1;
    }

    Console.WriteLine($"{written} iconos escritos en {outDir} en {sw.ElapsedMilliseconds} ms ({empty} vacíos)");
    Console.WriteLine("Tamaños más frecuentes tras recortar: " + string.Join(", ",
        sizes.OrderByDescending(s => s.Value).Take(5).Select(s => $"{s.Key} x{s.Value}")));

    // Relectura: no se da por bueno un fichero que no se ha vuelto a leer del disco.
    var sample = Path.Combine(outDir, "0001.png");
    var bytes = File.ReadAllBytes(sample);
    var okSignature = bytes.Length > 8 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G';
    Console.WriteLine($"Relectura de {Path.GetFileName(sample)}: {bytes.Length} bytes, firma PNG {(okSignature ? "correcta" : "MAL")}");

    if (!sheets)
    {
        Console.WriteLine("\n(--sheets genera además hojas de contactos para identificar los iconos)");
        return;
    }

    // Hojas de contactos: 60 iconos por hoja, en el orden del contenedor. Sirven para construir
    // a mano la tabla especie -> icono, que el cartucho no expone en ninguna parte (ver §28).
    var sheetDir = Path.Combine(outDir, "hojas");
    Directory.CreateDirectory(sheetDir);
    const int columns = 10, rows = 6, cell = 68, scale = 2;
    var perSheet = columns * rows;

    for (var first = 0; first < reader.Count; first += perSheet)
    {
        var sheetWidth = columns * cell * scale;
        var sheetHeight = rows * cell * scale / 2;
        var canvas = new byte[sheetWidth * sheetHeight * 4];

        for (var n = 0; n < perSheet && first + n < reader.Count; n++)
        {
            var icon = reader.Read(first + n);
            var ox = (n % columns) * cell * scale;
            var oy = (n / columns) * cell * scale / 2;
            var checker = (n % columns + n / columns) % 2 == 0 ? (byte)250 : (byte)225;

            for (var y = 0; y < cell * scale / 2; y++)
            for (var x = 0; x < cell * scale; x++)
            {
                var target = (((oy + y) * sheetWidth) + ox + x) * 4;
                var sx = x / scale;
                var sy = y / scale;
                byte r = checker, g = checker, b = checker;

                if (sx < icon.Width && sy < icon.Height)
                {
                    var source = ((sy * icon.Width) + sx) * 4;
                    var alpha = icon.Pixels[source + 3];
                    r = (byte)((icon.Pixels[source] * alpha / 255) + (checker * (255 - alpha) / 255));
                    g = (byte)((icon.Pixels[source + 1] * alpha / 255) + (checker * (255 - alpha) / 255));
                    b = (byte)((icon.Pixels[source + 2] * alpha / 255) + (checker * (255 - alpha) / 255));
                }

                canvas[target] = r;
                canvas[target + 1] = g;
                canvas[target + 2] = b;
                canvas[target + 3] = 255;
            }
        }

        File.WriteAllBytes(Path.Combine(sheetDir, $"hoja-{first:0000}.png"),
            PngImage.Encode(canvas, sheetWidth, sheetHeight));
    }

    Console.WriteLine($"Hojas de contactos en {sheetDir} ({(reader.Count + perSheet - 1) / perSheet} hojas de {perSheet})");
}
