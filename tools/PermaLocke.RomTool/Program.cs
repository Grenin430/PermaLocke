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

// El nombre de una especie, o su numero si el idioma cargado no la nombra. Hace falta porque el
// mod de expansion solo trae el texto en INGLES (a/0/3/2): en espanol la lista se acaba en Zeraora,
// 808 nombres frente a 1026, asi que pedir el 810 reventaba. Decir "#810" es la respuesta honesta;
// inventar un nombre o dejarlo en blanco seria peor que el numero.
static string SpeciesName(string[] names, int species) =>
    species >= 0 && species < names.Length && names[species].Length > 0
        ? names[species]
        : $"#{species}";

// Si hay un mod base en Expansion/romfs, TODOS los comandos trabajan sobre el en vez de sobre el
// cartucho. Es lo mismo que hace la aplicacion, y a proposito no es un argumento: una herramienta
// que puede mirar un mundo distinto del que mira la app segun como la invoques acaba dando dos
// respuestas a la misma pregunta.
var expansion = Path.Combine(root, "Expansion", "romfs");
var baseLayer = Directory.Exists(expansion) ? expansion : null;
var work = Path.Combine(Path.GetTempPath(),
    baseLayer is null ? "permalocke-romtool" : "permalocke-romtool-mod");

if (baseLayer is not null)
{
    Console.WriteLine($"MOD BASE: {expansion}");
}

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
    case "item-iconos":
        ItemIcons(int.Parse(args[1]), int.Parse(args[2]));
        break;
    case "importantes":
        await ImportantesAsync();
        break;
    case "megas":
        await MegasAsync();
        break;
    case "evo-dump":
        EvoDump(args.Length > 1 ? args[1] : null);
        break;
    case "evoluciones":
        Evoluciones(args.Length > 1 ? args[1] : null);
        break;
    case "starters":
    case "iniciales":
        Starters(args.Length > 1 ? args[1] : null);
        break;
    case "traducir":
        await TranslateAsync(args.Contains("--escribir"));
        break;
    case "randomize":
        await RandomizeAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818);
        break;
    default:
        Console.WriteLine("""
            Uso:
              PermaLocke.RomTool inspect              datos de la ROM y de los GARC
              PermaLocke.RomTool names <id> [id...]   nombres de especie leídos de la ROM
              PermaLocke.RomTool randomize [seed] [--rol id] [--install]
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
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    Console.WriteLine($"\nExtraídos {GameFiles.All.Count} ficheros en {sw.ElapsedMilliseconds} ms");

    var config = workspace.Config;
    Console.WriteLine($"{config.Version}  gen {config.Generation}  encdata -> {config.GetGARCFileName("encdata")}");

    // Las dos cuentas juntas y dichas por su nombre. La de pk3DS es una CONSTANTE que no mira los
    // ficheros, asi que sobre un mod que anade Pokemon miente; la otra sale de la tabla. Verlas
    // discrepar es la senal de que el mod se ha leido de verdad.
    Console.WriteLine($"especies: {workspace.MaxSpecies} segun la tabla, "
                      + $"{config.MaxSpeciesID} segun la constante de pk3DS");
    Console.WriteLine($"personal {config.Personal.Table.Length}  movimientos {config.Moves.Length}  evoluciones {config.Evolutions.Length}");

    foreach (var file in GameFiles.All)
    {
        var info = new FileInfo(workspace.PathOf(file));
        Console.WriteLine($"  {file}  {info.Length,12:N0} bytes");
    }
}

async Task NamesAsync(int[] ids)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    foreach (var id in ids)
    {
        Console.WriteLine($"{id,4}  {(id < names.Length ? names[id] : "(fuera de rango)")}");
    }
}

async Task RandomizeAsync(ulong seed)
{
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));

    // El rol manda sobre el fichero de opciones, igual que en la aplicación: --rol experto.
    var roles = PermaLocke.Data.JsonRoleCatalog.Load(Path.Combine(root, "Data", "roles.json"));
    var wanted = args.SkipWhile(a => a != "--rol").Skip(1).FirstOrDefault();

    if (wanted is not null)
    {
        if (roles.Find(wanted) is not { } role)
        {
            Console.WriteLine($"No existe el rol «{wanted}». Hay: {string.Join(", ", roles.All.Select(r => r.Id))}");
            return;
        }

        options = options with
        {
            EnemyLevelPercent = role.EnemyLevelPercent,
            ExtraTrainerPokemon = role.ExtraTrainerPokemon,
            ImportantTrainerClasses = roles.ImportantTrainerClasses,
        };

        Console.WriteLine($"rol {role.Id}: entrenadores +{role.EnemyLevelPercent}%, "
                          + $"+{role.ExtraTrainerPokemon} Pokémon en "
                          + $"{roles.ImportantTrainerClasses.Count} clases importantes");
    }

    // Por defecto se escribe en Randomized/, no en Azahar: instalar el mod cambia la partida en
    // curso, así que es una decisión explícita del jugador.
    var install = args.Contains("--install");

    // Siempre se genera aparte, aunque se vaya a instalar. Antes se escribía directo en la carpeta
    // de Azahar, y con una capa base eso no puede ser: RandomizeAsync VACÍA la carpeta del mod
    // antes de escribir, así que el mod base copiado ahí se habría borrado, y copiarlo después
    // habría pisado lo randomizado. Generar aparte y copiar en orden -base primero, lo nuestro
    // encima- es la única secuencia en la que ninguna de las dos capas se come a la otra.
    var mod = Path.Combine(root, "Randomized", $"seed-{seed}");

    Console.WriteLine($"seed {seed}\nsalida {mod}{(install ? "  (SE INSTALARÁ EN AZAHAR)" : "")}\n");
    var report = await new RandomizerService(options)
        .RandomizeAsync(RequireRom(), work, mod, seed, baseLayer);

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

    if (!install)
    {
        return;
    }

    var target = LayeredFsMod.DirectoryFor(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar"));
    var romfs = Path.Combine(target, "romfs");

    if (baseLayer is not null)
    {
        Console.WriteLine($"\nCopiando el mod base entero a {romfs} (son varios GB)...");
        var sw = Stopwatch.StartNew();
        var copied = ModInstaller.CopyTree(baseLayer, romfs, skipUnchanged: true);
        Console.WriteLine($"  {copied} ficheros en {sw.Elapsed.TotalSeconds:F0} s");

        var exefs = Path.Combine(root, "Expansion", "exefs");

        if (Directory.Exists(exefs))
        {
            // Fuera de romfs, a su lado. El mod parchea code.bin y sin él los Pokémon nuevos no
            // existen para el motor por muchos datos que tengan.
            ModInstaller.CopyTree(exefs, Path.Combine(target, "exefs"), skipUnchanged: true);
            Console.WriteLine("  exefs/code.bin copiado");
        }
    }

    ModInstaller.CopyTree(Path.Combine(mod, "romfs"), romfs);
    Console.WriteLine($"Instalado en {target}. Cierra Azahar del todo antes de abrirlo.");
}


async Task DumpAsync(ulong seed, string zone)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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
            Console.WriteLine($"  {t.Rates[i],3}%  #{e.Species,3} {SpeciesName(names, (int)e.Species)}");
        }
    }
}

async Task StaticsAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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
                Console.WriteLine($"    ¡PROHIBIDA! {layout.Name} entrada {i}: {SpeciesName(names, newSpecies)}");
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
            Console.WriteLine($"  intacta: {layout.Name} entrada {i} = {SpeciesName(names, oldSpecies)} ({expected})");
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
        Console.WriteLine($"  {label,-8} {SpeciesName(names, was),-14} -> {SpeciesName(names, now)}");
    }
}

async Task TrainersAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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
        Console.WriteLine($"  Nv.{TrainerPokemonTable.GetLevel(sample, s),3}  {SpeciesName(names, TrainerPokemonTable.GetSpecies(sampleVanilla, s)),-14} -> {SpeciesName(names, TrainerPokemonTable.GetSpecies(sample, s))}");
    }
}

async Task PokemonAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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
        Console.WriteLine($"\n{SpeciesName(names, species)}: {types[o1]}/{types[o2]} -> {types[t1]}/{types[t2]}");
        Console.WriteLine($"  stats {before}  ->  {after}   (total {PersonalEntry7.BaseStatTotal(packedBefore, at)} -> {PersonalEntry7.BaseStatTotal(packedAfter, at)})");
    }
}

async Task ShopsAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var items = workspace.Config.GetText(TextName.ItemNames);
    // --gen <ruta> lee el Shop.cro de un mod ya generado, que es la unica forma honesta de
    // comprobar lo que se escribio: releerlo, no fiarse del informe.
    var explicit_ = args.SkipWhile(a => a != "--gen").Skip(1).FirstOrDefault();
    var generated = explicit_ is not null && File.Exists(explicit_)
        ? explicit_
        : Path.Combine(root, "Randomized", "seed-20260818", "romfs", "Shop.cro");
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
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var items = workspace.Config.GetText(TextName.ItemNames);
    Console.WriteLine($"lista de objetos: {items.Length} entradas");
    for (var i = from; i <= to && i < items.Length; i++)
    {
        Console.WriteLine($"  {i,4}  '{items[i]}'");
    }
}

async Task FieldItemsAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var items = workspace.Config.GetText(TextName.ItemNames);
    var locations = workspace.Config.GetText(TextName.metlist_000000);
    var generatedEnc = Path.Combine(root, "Randomized", "seed-20260818", "romfs", "a", "0", "8", "3");
    var useGen = args.Contains("--gen") && File.Exists(generatedEnc);
    Console.WriteLine(useGen ? "=== FICHERO GENERADO ===" : "=== VANILLA ===");
    var garc = new GARC.LazyGARC(await File.ReadAllBytesAsync(useGen ? generatedEnc : workspace.PathOf(GameFiles.EncounterDataUltraMoon)));

    var zones = garc.FileCount / FieldItemTable.SubfilesPerZone;
    var machineIds = ShopTable.TechnicalMachines(items).ToHashSet();
    int total = 0, machines = 0, zonesWith = 0, unnamed = 0, berries = 0;
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
            if (machineIds.Contains(item)) machines++;
            if (item >= items.Length || items[item] == "(?)" || string.IsNullOrWhiteSpace(items[item])) unnamed++;
            if (sample.Count < 22 && item != 0) sample.Add($"z{zone}:{(item < items.Length ? items[item] : item.ToString())}");
            if (item < items.Length && items[item].StartsWith("Baya", StringComparison.OrdinalIgnoreCase)) berries++;
        }
    }

    Console.WriteLine($"\n{zones} zonas, {zonesWith} con objetos en el suelo");
    Console.WriteLine($"  {total} objetos localizados");
    Console.WriteLine($"  de los cuales MT (las Poké Ball doradas): {machines}");
    Console.WriteLine($"  de los cuales BAYAS: {berries}");
    Console.WriteLine($"  ids sin nombre real: {unnamed}   (mucho = el localizador está mal)");
    Console.WriteLine($"\n  muestra: {string.Join(", ", sample)}");
}

// Genera Data/zones.json: por cada área de encdata, los nombres de localización que cubre.
// Es el puente entre la zona que se lee de la memoria del juego (§23) y la que registra la run,
// que se identifica por el nombre normalizado de la localización.
async Task ZonesAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
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

// Lee los tres iniciales de una carpeta de mod ya generada o instalada. Es la comprobación
// independiente de lo que la aplicación enseña: no vuelve a tirar el dado, abre el fichero que
// el emulador carga y dice qué hay dentro.
void Starters(string? folder)
{
    var roots = folder is not null
        ? new[] { folder }
        : Directory.Exists(Path.Combine(root, "Randomized"))
            ? Directory.GetDirectories(Path.Combine(root, "Randomized"))
            : [];

    if (roots.Length == 0)
    {
        Console.WriteLine("No hay ninguna carpeta de mod que mirar.");
        return;
    }

    foreach (var mod in roots)
    {
        Console.WriteLine(mod);

        var found = StarterReader.Read(mod);

        if (found.Count == 0)
        {
            Console.WriteLine("  sin a/1/5/9, o sin iniciales dentro");
            continue;
        }

        foreach (var starter in found)
        {
            Console.WriteLine($"  opción {starter.Slot}: especie {starter.Species,4}  forma {starter.Form}");
        }
    }
}

// Cuenta, contra la tabla del cartucho, cuantas especies son primera etapa de una linea de tres:
// lo que la competicion exige de un inicial. Sin esta medida el filtro seria una afirmacion.
// Toma la ruta de a/0/1/4 ya extraido; sin argumento extrae la ROM, que tarda.
void Evoluciones(string? evolutionPath)
{
    var path = evolutionPath ?? Path.Combine(work, "a", "0", "1", "4");

    if (!File.Exists(path))
    {
        Console.WriteLine($"No esta {path}. Pasa la ruta de a/0/1/4 extraido, o ejecuta antes inspect.");
        return;
    }

    Console.WriteLine($"Tabla de evoluciones: {path}");

    var table = EvolutionTable.Read(path);
    var bases = Enumerable.Range(1, table.Count).Where(table.IsBase).ToList();

    Console.WriteLine($"Especies en la tabla: {table.Count}");
    Console.WriteLine($"Primeras etapas (nadie evoluciona en ellas): {bases.Count}");

    foreach (var group in bases.GroupBy(table.Stages).OrderBy(g => g.Key))
    {
        Console.WriteLine($"  lineas de {group.Key} etapa(s): {group.Count()}");
    }

    var three = bases.Where(table.HasTwoEvolutionsAhead).ToList();
    Console.WriteLine();
    Console.WriteLine($"CANDIDATAS A INICIAL: {three.Count}");
    Console.WriteLine("  ids: " + string.Join(" ", three.Take(30)));
    Console.WriteLine($"  de esas, con id <= 807 (el tope del randomizador): {three.Count(s => s <= 807)}");
    Console.WriteLine("  por encima de 807: " + string.Join(" ", three.Where(s => s > 807)));
    Console.WriteLine();

    // Anclas conocidas sin necesidad de nombres: si alguna falla, el lector esta mal.
    (int Species, bool Expected, string Name)[] anchors =
    [
        (1, true, "Bulbasaur"), (4, true, "Charmander"), (7, true, "Squirtle"),
        (722, true, "Rowlet"), (725, true, "Litten"), (728, true, "Popplio"),
        (10, true, "Caterpie"), (252, true, "Treecko"),
        (25, false, "Pikachu (evoluciona de Pichu)"),
        (133, false, "Eevee (ramas, pero dos etapas)"),
        (129, false, "Magikarp (dos etapas)"),
        (132, false, "Ditto (no evoluciona)"),
        (144, false, "Articuno (no evoluciona)"),
    ];

    var wrong = 0;

    foreach (var (species, expected, name) in anchors)
    {
        var actual = table.HasTwoEvolutionsAhead(species);
        wrong += actual == expected ? 0 : 1;

        Console.WriteLine($"  {(actual == expected ? "ok  " : "MAL ")}{name,-34} "
                          + $"etapas={table.Stages(species)} base={table.IsBase(species)}");
    }

    Console.WriteLine();
    Console.WriteLine(wrong == 0 ? "TODAS LAS ANCLAS CUADRAN." : $"{wrong} anclas no cuadran.");
}

// Vuelca las entradas de evolucion tal cual estan en el cartucho, agrupadas por metodo. Es lo que
// permite decidir que hacer con las evoluciones por intercambio sin suponer la tabla de metodos.
void EvoDump(string? evolutionPath)
{
    var path = evolutionPath ?? Path.Combine(work, "a", "0", "1", "4");

    if (!File.Exists(path))
    {
        Console.WriteLine($"No esta {path}.");
        return;
    }

    using var patcher = new GarcPatcher(path);
    var rows = new List<(int Species, int Slot, int Method, int Argument, int Target, int Form, int Level)>();

    for (var species = 0; species < patcher.FileCount; species++)
    {
        var entry = patcher.Read(species);

        for (var slot = 0; slot * 8 + 8 <= entry.Length; slot++)
        {
            var at = slot * 8;
            var method = BitConverter.ToUInt16(entry, at);

            if (method != 0)
            {
                rows.Add((species, slot, method,
                    BitConverter.ToUInt16(entry, at + 2),
                    BitConverter.ToUInt16(entry, at + 4),
                    (sbyte)entry[at + 6], entry[at + 7]));
            }
        }
    }

    Console.WriteLine($"Entradas con metodo: {rows.Count}");
    Console.WriteLine();
    Console.WriteLine("POR METODO");

    foreach (var group in rows.GroupBy(r => r.Method).OrderBy(g => g.Key))
    {
        Console.WriteLine($"  metodo {group.Key,3}: {group.Count(),4} entradas   "
                          + $"ejemplo especie {group.First().Species} -> {group.First().Target} "
                          + $"(arg {group.First().Argument}, nivel {group.First().Level})");
    }

    Console.WriteLine();
    Console.WriteLine("INTERCAMBIO (metodos 5, 6 y 7)");

    if (args.Length > 2 && int.TryParse(args[2], out var only))
    {
        Console.WriteLine($"SOLO EL METODO {only}");
        Show(only);
        return;
    }

    Show(5, 6, 7);

    Console.WriteLine();
    Console.WriteLine("POR MOVIMIENTO APRENDIDO (metodo 21)");
    Show(21);

    void Show(params int[] methods)
    {
        foreach (var row in rows.Where(r => methods.Contains(r.Method)).OrderBy(r => r.Species))
        {
            Console.WriteLine($"  especie {row.Species,4} hueco {row.Slot}  metodo {row.Method,2}  "
                              + $"-> {row.Target,4} forma {row.Form,2}  arg {row.Argument,4}  nivel {row.Level,3}");
        }
    }
}

// Las megaevoluciones del cartucho cruzadas con la tabla de evoluciones: que especie, con que
// piedra, y a que nivel se llega a esa especie. Lo segundo es lo que decide si una mega es
// alcanzable dentro del cap de la competicion o es decorado.
async Task MegasAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var species = workspace.Config.GetText(TextName.SpeciesNames);
    var items = workspace.Config.GetText(TextName.ItemNames);

    var megaPath = workspace.PathOf(GameFiles.MegaEvolution);
    var evoPath = workspace.PathOf(GameFiles.Evolution);

    // A que nivel se obtiene cada especie: el nivel de la entrada de evolucion que apunta a ella.
    // Cero significa que no se llega subiendo de nivel -piedra, intercambio, o es una base-.
    var levelOf = new Dictionary<int, int>();
    using (var evo = new GarcPatcher(evoPath))
    {
        for (var from = 0; from < evo.FileCount; from++)
        {
            var entry = evo.Read(from);
            for (var at = 0; at + 8 <= entry.Length; at += 8)
            {
                if (BitConverter.ToUInt16(entry, at) == 0)
                {
                    continue;
                }

                var target = BitConverter.ToUInt16(entry, at + 4);
                var level = entry[at + 7];

                if (target > 0 && (!levelOf.TryGetValue(target, out var known) || known < level))
                {
                    levelOf[target] = level;
                }
            }
        }
    }

    using var mega = new GarcPatcher(megaPath);
    var rows = new List<(int Species, int Stone, int Level)>();

    for (var s = 0; s < mega.FileCount; s++)
    {
        var entry = mega.Read(s);
        for (var at = 0; at + 8 <= entry.Length; at += 8)
        {
            if (BitConverter.ToUInt16(entry, at) == 0)
            {
                continue;
            }

            rows.Add((s, BitConverter.ToUInt16(entry, at + 4), levelOf.GetValueOrDefault(s)));
        }
    }

    Console.WriteLine($"MEGAEVOLUCIONES: {rows.Count} entradas, {rows.Select(r => r.Species).Distinct().Count()} especies");
    Console.WriteLine();

    foreach (var row in rows.OrderBy(r => r.Level).ThenBy(r => r.Species))
    {
        var reach = row.Level == 0 ? "no por nivel" : $"nivel {row.Level}";
        Console.WriteLine($"  {row.Species,4} {species[row.Species],-13} {items[row.Stone],-18} "
                          + $"se llega a la especie: {reach}");
    }

    Console.WriteLine();
    foreach (var cap in (int[])[24, 34, 40, 42, 54, 67, 85])
    {
        var reachable = rows.Count(r => r.Level > 0 && r.Level <= cap);
        var noLevel = rows.Count(r => r.Level == 0);
        Console.WriteLine($"  con cap {cap,3}: {reachable,2} alcanzables subiendo de nivel "
                          + $"(+{noLevel} que no dependen del nivel)");
    }
}

// Vuelca un tramo de iconos de OBJETO a PNG, nombrados por su indice de icono. Es la unica forma
// de anclar un objeto nuevo: el desfase entre id e indice es escalonado y solo se sabe mirando.
void ItemIcons(int fromIcon, int toIcon)
{
    var rom = RequireRom();
    var reader = ItemIconReader.Open(rom, work, baseLayer);
    var outDir = Path.Combine(Path.GetTempPath(), "permalocke-item-iconos");
    Directory.CreateDirectory(outDir);

    var written = 0;

    for (var icon = fromIcon; icon <= toIcon && icon < reader.Count; icon++)
    {
        // Read() toma el id, no el indice, y lee el indice id-1: por eso el +1.
        var picture = reader.Read(icon + 1);

        if (picture.Pixels.All(b => b == 0))
        {
            continue;
        }

        File.WriteAllBytes(Path.Combine(outDir, $"icono-{icon:0000}.png"),
            PngImage.Encode(picture.Pixels, picture.Width, picture.Height));
        written++;
    }

    Console.WriteLine($"{written} iconos ({fromIcon}..{toIcon}) escritos en {outDir}");
    Console.WriteLine($"El contenedor tiene {reader.Count} iconos.");
}

// Los combates importantes del cartucho con el nivel de su equipo, para poder elegir un corte
// -«de la 7a prueba en adelante»- con un numero medido y no a ojo.
async Task ImportantesAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var roles = PermaLocke.Data.JsonRoleCatalog.Load(Path.Combine(root, "Data", "roles.json"));
    var classes = roles.ImportantTrainerClasses.ToHashSet();
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);

    // Con una ruta, lee el trpoke de un mod ya generado: comprobar lo escrito releyendolo.
    var trpoke = args.Length > 1 && File.Exists(args[1])
        ? args[1]
        : workspace.PathOf(GameFiles.TrainerPokemon);

    Console.WriteLine($"trpoke: {trpoke}");
    var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(trpoke));

    // La tabla de entrenadores tiene que venir del MISMO sitio que el trpoke: el modulo del
    // Pokemon extra cambia las cuentas, y cruzar la tabla vanilla con un trpoke ya generado hace
    // que no cuadre ninguna y se salten en silencio.
    var trdata = trpoke == workspace.PathOf(GameFiles.TrainerPokemon)
        ? workspace.PathOf(GameFiles.TrainerData)
        : Path.Combine(Path.GetDirectoryName(trpoke)!, "6");

    Console.WriteLine($"trdata: {trdata}");

    using var trainers = new GarcPatcher(trdata);
    var rows = new List<(int Id, int Class, int Count, int Max)>();

    for (var trainer = 0; trainer < trainers.FileCount; trainer++)
    {
        var entry = trainers.Read(trainer);
        if (entry.Length < 0x14)
        {
            continue;
        }

        var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
        var count = entry[ExtraPokemonRandomizer.CountOffset];

        if (!classes.Contains(trainerClass) || count == 0 || trainer >= parties.FileCount)
        {
            continue;
        }

        var party = parties[trainer];
        if (party.Length != count * TrainerPokemonTable.EntrySize)
        {
            continue;
        }

        var max = Enumerable.Range(0, count).Max(i => TrainerPokemonTable.GetLevel(party, i));

        var megas = Enumerable.Range(0, count)
            .Where(i => TrainerPokemonTable.GetForm(party, i) > 0)
            .Select(i => $"especie {TrainerPokemonTable.GetSpecies(party, i)} "
                         + $"forma {TrainerPokemonTable.GetForm(party, i)}")
            .ToList();

        if (megas.Count > 0)
        {
            Console.WriteLine($"  FORMA entrenador {trainer,4} nivel {max,3} ({count} Pokemon): "
                              + string.Join(", ", megas));
        }

        rows.Add((trainer, trainerClass, count, max));
    }

    Console.WriteLine($"COMBATES IMPORTANTES: {rows.Count} (clases: {classes.Count})");
    Console.WriteLine();

    foreach (var row in rows.OrderBy(r => r.Max).ThenBy(r => r.Id))
    {
        var name = row.Class < classNames.Length ? classNames[row.Class] : "?";
        var who = row.Id < trainerNames.Length ? trainerNames[row.Id] : "?";
        var mark = row.Max >= 33 ? "MEGA" : "    ";

        Console.WriteLine($"  {mark} {name,-20} {who,-14} nivel {row.Max,3}, {row.Count} Pokemon "
                          + $"(entrenador {row.Id})");
    }

    Console.WriteLine();
    foreach (var floor in (int[])[25, 30, 33, 35, 40, 45, 50])
    {
        Console.WriteLine($"  con nivel maximo >= {floor,2}: {rows.Count(r => r.Max >= floor),3} combates");
    }
}

// Pone en espanol los nombres que el mod de expansion solo trae en ingles. No es una traduccion:
// los nombres espanoles de especies, movimientos, habilidades y objetos son OFICIALES y PKHeX los
// lleva, asi que esto es copiarlos a su sitio. Sin --escribir solo mide y no toca nada.
async Task TranslateAsync(bool write)
{
    const int Spanish = 6, English = 2;

    if (baseLayer is null)
    {
        Console.WriteLine("No hay mod base en Expansion/romfs: no hay nada que traducir.");
        return;
    }

    // Los cuatro ficheros de NOMBRES que el mod agranda, con la lista oficial de PKHeX que les toca.
    var es = PKHeX.Core.GameInfo.GetStrings("es");
    (TextName Name, string What, string[] Official)[] targets =
    [
        (TextName.SpeciesNames, "especies",    es.specieslist),
        (TextName.MoveNames,    "movimientos", es.movelist),
        (TextName.AbilityNames, "habilidades", es.abilitylist),
        // Los objetos van con lista oficial VACIA a proposito, o sea que los nuevos se quedan con
        // el ingles del mod. El ancla lo exige: en especies PKHeX coincide 805 de 808 y en objetos
        // solo 738 de 960, y sobre los ids nuevos se rompe del todo. Medido: el mod pone ahi sus
        // objetos de evolucion -989 es "Malicious Armor"- y PKHeX pone caramelos, asi que copiar
        // por indice bautizaba la armadura de Ceruledge como "Caramelo Mente".
        //
        // Un nombre equivocado es peor que uno en ingles: manda a buscar el objeto que no es.
        (TextName.ItemNames,    "objetos",     []),
    ];

    using var spanish = await RomWorkspace.ExtractAsync(RequireRom(),
        Path.Combine(Path.GetTempPath(), "permalocke-tr-es"), Spanish, baseLayer: baseLayer);
    using var english = await RomWorkspace.ExtractAsync(RequireRom(),
        Path.Combine(Path.GetTempPath(), "permalocke-tr-en"), English, baseLayer: baseLayer);

    // El ingles del CARTUCHO, sin mod. Es lo unico que distingue "el cartucho lo traduce distinto"
    // de "el mod ha reutilizado ese id para otra cosa". El mod renombra 16 objetos, del 505 al 520,
    // que eran Tarjetas de Datos y ahora son megapiedras.
    using var plain = await RomWorkspace.ExtractAsync(RequireRom(),
        Path.Combine(Path.GetTempPath(), "permalocke-tr-van"), English);

    Console.WriteLine("\nANCLA: los nombres que YA existen tienen que coincidir con los de PKHeX.");
    Console.WriteLine("Si no coinciden, el indice no esta alineado y traducir moveria cada nombre de sitio.\n");

    foreach (var (name, what, official) in targets)
    {
        var current = spanish.Config.GetText(name);
        var target = english.Config.GetText(name);

        var shared = Math.Min(current.Length, official.Count());
        var same = 0;
        var examples = new List<string>();

        for (var i = 0; i < shared; i++)
        {
            if (current[i] == official[i]) { same++; }
            else if (examples.Count < 4 && current[i].Length > 0)
            {
                examples.Add($"{i}: cartucho '{current[i]}' vs PKHeX '{official[i]}'");
            }
        }

        var pct = shared == 0 ? 0 : 100.0 * same / shared;
        Console.WriteLine($"{what,-12} cartucho {current.Length,5} · mod(ingles) {target.Length,5} · "
                          + $"PKHeX {official.Length,5} · faltan {Math.Max(0, target.Length - current.Length),4}");
        Console.WriteLine($"{"",-12} coinciden {same}/{shared} ({pct:F1}%)");

        foreach (var e in examples)
        {
            Console.WriteLine($"{"",-12}   {e}");
        }
    }

    if (!write)
    {
        Console.WriteLine("\nSolo medida. Añade --escribir para generar el texto en español.");
        return;
    }

    // Los DIEZ subficheros que el mod agranda se tratan igual: se conserva lo que el juego ya dice
    // y se añade lo que falta. Los cuatro de NOMBRES se rellenan con la lista oficial; los otros
    // seis son descripciones y ahí se copia el inglés del propio mod.
    //
    // Y eso último es una decisión, no una dejadez: no hay fuente oficial para las descripciones, y
    // en un Nuzlocke una descripción de movimiento equivocada mata un Pokémon. Correcta en inglés
    // vale más que bonita e inventada. Dejarlas cortas tampoco vale, porque se indexan por id.
    var index = targets.ToDictionary(t => TextIndex(t.Name), t => t);
    var garc = new GARC.LazyGARC(await File.ReadAllBytesAsync(
        spanish.PathOf(GameFiles.GameText(Spanish))));
    var theirs = new GARC.LazyGARC(await File.ReadAllBytesAsync(
        Path.Combine(baseLayer, "a", "0", "3", English.ToString())));

    Console.WriteLine();
    var touched = 0;

    for (var i = 0; i < garc.FileCount && i < theirs.FileCount; i++)
    {
        var mine = TextFile.GetStrings(spanish.Config, garc[i]);
        var mod = TextFile.GetStrings(english.Config, theirs[i]);

        if (mod.Length <= mine.Length)
        {
            continue;
        }

        var official = index.TryGetValue(i, out var t) ? t.Official : [];
        var merged = NameLocalizer.Extend(mine, mod, official,
            TextFile.GetStrings(plain.Config, new GARC.LazyGARC(
                await File.ReadAllBytesAsync(plain.PathOf(GameFiles.GameText(English))))[i]));
        garc[i] = TextFile.GetBytes(spanish.Config, merged.Lines);

        Console.WriteLine($"  subfichero {i,3}: {merged.Kept,5} tal cual · "
                          + $"+{merged.Translated,4} en español · +{merged.Borrowed,4} en inglés"
                          + (index.TryGetValue(i, out var w) ? $"   [{w.What}]" : "   [descripciones]"));
        touched++;
    }

    var outPath = Path.Combine(root, "Expansion", "romfs", "a", "0", "3", Spanish.ToString());
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    await File.WriteAllBytesAsync(outPath, garc.Save());

    // Se relee, que es lo único que convierte «escrito» en «hecho». Reempaquetar un GARC es la
    // operación que más veces ha salido mal en este proyecto.
    var back = new GARC.LazyGARC(await File.ReadAllBytesAsync(outPath));

    if (back.FileCount != garc.FileCount)
    {
        throw new InvalidDataException(
            $"El texto se quedó con {back.FileCount} subficheros en vez de {garc.FileCount}.");
    }

    Console.WriteLine();

    foreach (var (name, what, _) in targets)
    {
        var at = TextIndex(name);
        var lines = TextFile.GetStrings(spanish.Config, back[at]);
        var expected = TextFile.GetStrings(english.Config, theirs[at]).Length;

        if (lines.Length != expected)
        {
            throw new InvalidDataException(
                $"{what}: quedaron {lines.Length} y el mod tiene {expected}.");
        }

        Console.WriteLine($"  releído {what,-12} {lines.Length,5} · "
                          + $"la 808 es '{lines[Math.Min(808, lines.Length - 1)]}'");
    }

    Console.WriteLine($"\n{touched} subficheros reescritos en {outPath}");
}

// El indice de un fichero de texto dentro del GARC. pk3DS lo resuelve por dentro y no lo publica,
// pero su tabla si es publica.
static int TextIndex(TextName name) =>
    TextReference.GameText_USUM.First(r => r.Name == name).Index;
