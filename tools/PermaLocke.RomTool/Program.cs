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
        // Una seed mira lo GENERADO; una ruta a un a/1/0/7 mira lo que sea, el mod INSTALADO
        // incluido, que es el unico que alguien esta jugando de verdad. Mismo criterio que
        // «quien-lleva» y «entrenador» del §85.
        await TrainersAsync(
            args.Length > 1 && ulong.TryParse(args[1], out var trainerSeed) ? trainerSeed : 20260818,
            args.Length > 1 && !ulong.TryParse(args[1], out _) ? args[1] : null);
        break;
    case "estaticos-crudo":
        await EstaticosCrudoAsync([.. args.Skip(1).Select(int.Parse)]);
        break;
    case "estaticos-dump":
        await EstaticosDumpAsync(args.Length > 1 && args[1] != "-" ? args[1] : null,
            args.Length > 2 ? args[2] : null);
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
    case "mapa-pistas":
        MapaPistas(args[1]);
        break;
    case "mapa-datos-volcar":
        MapaDatosVolcar(args[1]);
        break;
    case "mapa-datos":
        MapaDatos(args[1], args.Length > 2 ? int.Parse(args[2]) : 16);
        break;
    case "mapa-islas":
        MapaIslas(args.Length > 1 ? int.Parse(args[1]) : 200,
            args.Length > 2 ? double.Parse(args[2]) : 0.15);
        break;
    case "mapa-transparencia":
        MapaTransparencia(args[1], args.Length > 2 ? double.Parse(args[2]) : 0.15);
        break;
    case "mapa-tallar-png":
        MapaTallarPng(args[1], args.Length > 2 ? int.Parse(args[2]) : 128);
        break;
    case "mapa-tallar":
        MapaTallar(args.Length > 1 ? int.Parse(args[1]) : 128);
        break;
    case "mapa-exportar":
        MapaExportar(args.Length > 1 ? args[1] : "a/1/6/3", args.Length > 2 ? int.Parse(args[2]) : 4,
            args.Length > 3 ? args[3] : Path.Combine(root, "Data", "mapa-areas"));
        break;
    case "mapa-vacias":
        MapaVacias(args.Length > 1 ? args[1] : "a/1/6/3");
        break;
    case "mapa-perfil":
        MapaPerfil(args[1], args.Length > 2 ? int.Parse(args[2]) : 4);
        break;
    case "mapa-segmentar":
        MapaSegmentar(args[1], args.Length > 2 ? int.Parse(args[2]) : 4,
            args.Length > 3 ? double.Parse(args[3]) : 40);
        break;
    case "mapa-cuatro":
        MapaCuatro("a/1/6/3", Path.Combine(root, "Data", "mapa-islas"));
        break;
    case "mapa-isla":
        MapaIsla(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]),
            Path.Combine(root, "Data", "mapa-islas"));
        break;
    case "mapa-fase":
        MapaFase(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]));
        break;
    case "mapa-medir":
        MapaMedir(args[1], args.Length > 2 ? int.Parse(args[2]) : 0,
            args.Length > 3 ? int.Parse(args[3]) : 64);
        break;
    case "mapa-coser":
        MapaCoser(args[1], int.Parse(args[2]), args.Length > 3 ? int.Parse(args[3]) : 0,
            args.Length > 4 ? int.Parse(args[4]) : 1000);
        break;
    case "mapa-volcar":
        MapaVolcar(args[1], args.Length > 2 ? int.Parse(args[2]) : 0,
            args.Length > 3 ? int.Parse(args[3]) : 20);
        break;
    case "mapa-buscar":
        MapaBuscar();
        break;
    case "mundos":
        await MundosAsync();
        break;
    case "mapas":
        await MapasAsync(args.Skip(1).Where(a => a != "--escribir").Select(int.Parse).ToArray(), args.Contains("--escribir"));
        break;
    case "dump":
        await DumpAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260818, args.Length > 2 ? args[2] : "Ruta 1");
        break;
    case "item-iconos":
        ItemIcons(int.Parse(args[1]), int.Parse(args[2]));
        break;
    case "entrenadores-ev":
        await EntrenadoresEvAsync();
        break;
    case "liga":
        await LigaAsync(args.Length > 1 ? ulong.Parse(args[1]) : 20260902);
        break;
    case "parchear-tiendas":
        await ParchearTiendasAsync(args[1], args.Contains("--escribir"));
        break;
    case "parchear-megas":
        await ParchearMegasAsync(args[1],
            args.Skip(2).Where(a => int.TryParse(a, out _)).Select(int.Parse).ToArray(),
            args.Contains("--escribir"));
        break;
    case "movimientos":
        await MovimientosAsync(args.Length > 1 ? args[1] : null);
        break;
    case "aprendizajes":
        await AprendizajesAsync(args[1]);
        break;
    case "clases":
        await ClasesAsync();
        break;
    case "importantes":
        await ImportantesAsync();
        break;
    case "quien-lleva":
        await QuienLlevaAsync(int.Parse(args[1]), args.Length > 2 ? args[2] : null);
        break;
    case "entrenador":
        await QuienLlevaAsync(0, args.Length > 2 ? args[2] : null, int.Parse(args[1]));
        break;
    case "megas-cuenta":
        await MegasCuentaAsync();
        break;
    case "megas":
        await MegasAsync();
        break;
    case "evo-dump":
        EvoDump(args.Length > 1 ? args[1] : null);
        break;
    case "variocolor":
        await VariocolorAsync();
        break;
    case "iconos-nombres":
        await IconosNombresAsync();
        break;
    case "informacion":
        await InformacionAsync();
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
            MonoType = role.MonoType,
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
    // Por el MISMO instalador que la aplicacion, no por una copia a mano. Aqui habia una tercera
    // implementacion de «que ficheros ganan», y se dejaba el exefs GENERADO: copiaba el code.bin del
    // mod base y no el que lleva la tabla de MT y la de tutores barajadas. Dos instalaciones hechas
    // desde aqui dejaron al jugador con las MT y los tutores del mod en vez de los de su mundo, y
    // nada fallo: el informe de la generacion seguia diciendo «100 de las 100 MT enseñan otro
    // movimiento». El instalador ademas guarda copia de lo que va a pisar.
    var sw = Stopwatch.StartNew();

    var copy = ModInstaller.Install(mod, target, baseLayer,
        baseLayer is null ? null : Path.Combine(root, "Expansion", "exefs"),
        message => Console.WriteLine("  " + message));

    Console.WriteLine(copy is null
        ? "  No habia nada instalado que se fuera a perder, asi que no hace falta copia."
        : $"  Copia del mundo que habia: {copy}");
    Console.WriteLine($"Instalado en {target} en {sw.Elapsed.TotalSeconds:F0} s. Cierra Azahar del todo antes de abrirlo.");
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
    // --tipo N: huecos de CUALQUIER tabla (dia, noche, agua, SOS...) cuya especie y forma no tengan ese tipo (§223).
    if (args.SkipWhile(a => a != "--tipo").Skip(1).FirstOrDefault() is { } typeText && int.TryParse(typeText, out var wanted))
    {
        var wrong = new List<string>();

        foreach (var area in areas.Where(a => a.HasTables))
        {
            for (var t = 0; t < area.Tables.Count; t++)
            {
                for (var set = 0; set < area.Tables[t].Encounter7s.Length; set++)
                {
                    foreach (var slot in area.Tables[t].Encounter7s[set])
                    {
                        if (slot.Species == 0) continue;
                        var types = workspace.Config.Personal.GetFormEntry((int)slot.Species, (int)slot.Forme).Types;
                        if (!types.Contains(wanted) || slot.Species == 56)
                        {
                            wrong.Add($"{area.Name} tabla {t} serie {set}: #{slot.Species} {SpeciesName(names, (int)slot.Species)} forma {slot.Forme}");
                        }
                    }
                }
            }
        }

        // Lo mismo leido a mano, byte a byte, como lo escribe el randomizador: cubre lo que pk3DS no enseña.
        var raw = new GARC.LazyGARC(File.ReadAllBytes(generated));
        var rawWrong = 0;

        for (var area = 0; area < raw.FileCount / 11; area++)
        {
            var payload = raw[(area * 11) + 9];
            if (payload.Length < 4 || payload[0] != (byte)'E' || payload[1] != (byte)'A') continue;

            for (var entry = 0; entry < BitConverter.ToUInt16(payload, 2); entry++)
            {
                var start = BitConverter.ToInt32(payload, 4 + (entry * 4));
                var end = BitConverter.ToInt32(payload, 8 + (entry * 4));
                if (end - start < EncounterTable7.MinimumEntrySize)
                {
                    // Entradas que el randomizador se salta por no caber dos tablas: se miran aparte, por si llevan algo.
                    var oneTable = new EncounterTable7(payload, start + EncounterTable7.DayTableOffset);
                    var held = (end - start) >= EncounterTable7.DayTableOffset + EncounterTable7.Size
                        ? EncounterTable7.SlotOffsets().Select(oneTable.GetSpecies).Where(s => s != 0).Distinct().ToArray()
                        : [];
                    Console.WriteLine($"  SALTADA area {area} entrada {entry} tamaño {end - start}: niv {(held.Length > 0 ? oneTable.MinLevel + "-" + oneTable.MaxLevel : "-")} {string.Join(",", held.Select(s => SpeciesName(names, s)))}");
                    continue;
                }

                foreach (var tableOffset in (int[])[EncounterTable7.DayTableOffset, EncounterTable7.NightTableOffset])
                {
                    var table = new EncounterTable7(payload, start + tableOffset);
                    foreach (var slotOffset in EncounterTable7.SlotOffsets())
                    {
                        var species = table.GetSpecies(slotOffset);
                        if (species == 0) continue;
                        if (!workspace.Config.Personal.GetFormEntry(species, table.GetForme(slotOffset)).Types.Contains(wanted))
                        {
                            rawWrong++;
                            if (rawWrong <= 20)
                                Console.WriteLine($"  RAW area {area} entrada {entry} niv {table.MinLevel}-{table.MaxLevel}: #{species} {SpeciesName(names, species)}");
                        }
                    }
                }
            }
        }

        Console.WriteLine($"RAW huecos sin el tipo: {rawWrong}");
        Console.WriteLine($"\nhuecos sin el tipo {wanted}: {wrong.Count}");
        foreach (var line in wrong.Take(40)) Console.WriteLine("  " + line);
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

async Task TrainersAsync(ulong seed, string? path = null)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var banned = options.BannedSpecies.ToHashSet();

    var generatedPath = path ?? Path.Combine(root, "Randomized", $"seed-{seed}", "romfs",
        GameFiles.TrainerPokemon.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(generatedPath))
    {
        Console.WriteLine(path is null
            ? $"No existe {generatedPath}. Ejecuta antes: randomize {seed}"
            : $"No existe {generatedPath}.");
        return;
    }

    var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.TrainerPokemon)));
    var modded = new GARC.LazyGARC(await File.ReadAllBytesAsync(generatedPath));
    Console.WriteLine($"\nentrenadores: vanilla {vanilla.FileCount}, generado {modded.FileCount}");

    int levelsMoved = 0, sizeChanged = 0, offenders = 0, replaced = 0, total = 0, itemsLost = 0, itemsAdded = 0, evsChanged = 0, evTotalChanged = 0, evOverCap = 0;
    var highest = 0;
    var highestTrainer = -1;

    // La regla de la sexta prueba se comprueba releyendo lo generado, no fiandose del informe: el
    // modulo cuenta lo que cree haber hecho y esto cuenta lo que hay en el fichero.
    var evolutionPath = Path.Combine(Path.GetDirectoryName(generatedPath)!, "..", "..", "0", "1", "4");
    var evolutions = File.Exists(Path.GetFullPath(evolutionPath))
        ? EvolutionTable.Read(Path.GetFullPath(evolutionPath))
        : null;
    var threshold = options.FullyEvolvedFromLevel;
    var unevolved = 0;
    var checkedAbove = 0;

    for (var t = 0; t < modded.FileCount; t++)
    {
        var before = vanilla[t];
        var after = modded[t];

        var wasSlots = TrainerPokemonTable.Count(before);
        var nowSlots = TrainerPokemonTable.Count(after);

        // Un equipo que CRECE es lo normal cuando el rol añade un Pokemon (§47). Antes esto era un
        // «continue» y se saltaba el equipo entero, asi que los 106 combates importantes quedaban
        // FUERA de todas las comprobaciones de abajo -- incluida la de la sexta prueba, que es
        // justo donde estaba el segundo agujero de la regla: el añadido no evolucionaba y nada lo
        // decia. Encoger si sigue siendo imposible.
        if (nowSlots < wasSlots) { sizeChanged++; continue; }

        for (var s = 0; s < nowSlots; s++)
        {
            total++;

            // Los huecos que YA EXISTIAN en el cartucho se pueden comparar uno a uno; el añadido
            // no tiene con que compararse.
            if (s < wasSlots)
            {
                if (TrainerPokemonTable.GetLevel(before, s) != TrainerPokemonTable.GetLevel(after, s)) levelsMoved++;
                // La dificultad del §122 AÑADE objetos donde no habia y deja los que habia. Lo que
                // no puede pasar es que un objeto del cartucho se cambie por otro o desaparezca.
                var itemBefore = TrainerPokemonTable.GetItem(before, s);
                var itemAfter = TrainerPokemonTable.GetItem(after, s);
                if (itemBefore != 0 && itemAfter != itemBefore) itemsLost++;
                if (itemBefore == 0 && itemAfter != 0) itemsAdded++;
                if (TrainerPokemonTable.GetSpecies(before, s) != TrainerPokemonTable.GetSpecies(after, s)) replaced++;

                // Los EV SI se escriben desde el §122: se reparten otra vez para la especie nueva,
                // con la MISMA cantidad. Lo que se comprueba es esa promesa -mismo total y ninguno
                // por encima de 252-, no que no se muevan. Antes este contador decia «debe ser 0»
                // y daba cientos con un mundo correcto, que es como se aprende a ignorar una alarma.
                var evBefore = TrainerPokemonTable.GetEvs(before, s).ToArray();
                var evAfter = TrainerPokemonTable.GetEvs(after, s).ToArray();
                if (!evBefore.SequenceEqual(evAfter)) evsChanged++;
                if (evBefore.Sum(v => (int)v) != evAfter.Sum(v => (int)v)) evTotalChanged++;
                if (evAfter.Any(v => v > 252)) evOverCap++;
            }

            if (banned.Contains(TrainerPokemonTable.GetSpecies(after, s))) offenders++;

            var level = TrainerPokemonTable.GetLevel(after, s);
            if (level > highest) { highest = level; highestTrainer = t; }

            // Contra el nivel del CARTUCHO, que es el mismo criterio con el que se genero. Un
            // hueco AÑADIDO no tiene nivel propio en el cartucho: hereda el del ultimo que el
            // entrenador ya llevaba, que es de quien se copio.
            var story = wasSlots == 0
                ? -1
                : TrainerPokemonTable.GetLevel(before, Math.Min(s, wasSlots - 1));

            if (evolutions is not null && threshold > 0 && story >= threshold)
            {
                checkedAbove++;
                var species = TrainerPokemonTable.GetSpecies(after, s);

                if (species > 0 && evolutions.FinalOf(species) != species)
                {
                    unevolved++;

                    // Dichos por su nombre y no solo contados: «3 sin evolucionar» no se puede
                    // comprobar en el juego y «el 473 lleva un Larvitar» si.
                    if (unevolved <= 20)
                    {
                        Console.WriteLine($"    SIN EVOLUCIONAR  entrenador {t,3} hueco {s}"
                                          + $"{(s >= wasSlots ? " AÑADIDO" : "        ")}  "
                                          + $"{SpeciesName(names, species)} (nivel de cartucho {story})");
                    }
                }
            }
        }
    }

    // AUDITORIA DE CLASES IMPORTANTES.
    //
    // La lista de roles.json se hizo mirando el cartucho y ya se le encontro un hueco -- la clase
    // 222, el combate que el jugador jugaba --, asi que conviene poder repasarla entera. Un
    // personaje con nombre deja firma: aparece POCAS veces y con equipos GRANDES. Un entrenador
    // de relleno aparece decenas de veces y lleva uno o dos Pokemon.
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);

    // La lista sale de Data/roles.json y NO se copia aquí. Estaba copiada, dos veces, y las dos
    // copias se quedaron atrás: ésta tenía 43 clases y la de más abajo 35, mientras el fichero que
    // gobierna la generación tenía 47. O sea que la herramienta que existe para repasar la lista
    // repasaba OTRA lista, y decía «96 combates importantes» de un mundo que tiene más.
    var keyClasses = PermaLocke.Data.JsonRoleCatalog
        .Load(Path.Combine(root, "Data", "roles.json"))
        .ImportantTrainerClasses;

    var classPath = Path.Combine(Path.GetDirectoryName(generatedPath)!, "..", "0", "6");

    if (File.Exists(classPath))
    {
        var meta = new GARC.LazyGARC(await File.ReadAllBytesAsync(classPath));
        var seen = new Dictionary<int, (int Count, int MaxParty, int MaxLevel)>();
        var byClassIndex = new Dictionary<int, List<int>>();
        var trainerNames = workspace.Config.GetText(TextName.TrainerNames);

        for (var t = 0; t < Math.Min(meta.FileCount, vanilla.FileCount); t++)
        {
            var e = meta[t];
            var cls = e.Length >= 0x14 ? BitConverter.ToUInt16(e, 0x00) : -1;

            if (cls < 0) { continue; }

            var party = vanilla[t];
            var n = TrainerPokemonTable.Count(party);

            if (n == 0 || party.Length != n * TrainerPokemonTable.EntrySize) { continue; }

            var top = Enumerable.Range(0, n).Max(s => TrainerPokemonTable.GetLevel(party, s));
            var had = seen.GetValueOrDefault(cls);
            seen[cls] = (had.Count + 1, Math.Max(had.MaxParty, n), Math.Max(had.MaxLevel, top));

            if (!byClassIndex.TryGetValue(cls, out var who2)) { byClassIndex[cls] = who2 = []; }

            who2.Add(t);
        }

        Console.WriteLine("\n  CLASES SOSPECHOSAS que NO están en la lista:");
        Console.WriteLine("  (pocas apariciones y equipo grande = personaje con nombre)");

        foreach (var (cls, info) in seen
            .Where(kv => !keyClasses.Contains(kv.Key) && kv.Value.Count <= 12 && kv.Value.MaxParty >= 4)
            .OrderByDescending(kv => kv.Value.MaxParty)
            .ThenBy(kv => kv.Value.Count))
        {
            var name = cls < classNames.Length ? classNames[cls] : "?";
            var who = string.Join(", ", byClassIndex.GetValueOrDefault(cls, [])
                .Select(t => t < trainerNames.Length ? trainerNames[t] : "?")
                .Distinct()
                .Take(4));

            Console.WriteLine($"    clase {cls,3} «{name}»: {info.Count} entrenadores, "
                + $"hasta {info.MaxParty} Pokémon, Nv. máx {info.MaxLevel}  -> {who}");
        }

        Console.WriteLine("\n  LAS QUE SÍ ESTÁN:");

        foreach (var cls in keyClasses.OrderBy(c => c))
        {
            var name = cls < classNames.Length ? classNames[cls] : "?";
            var info = seen.GetValueOrDefault(cls);
            Console.WriteLine($"    clase {cls,3} «{name}»: {info.Count} entrenadores, "
                + $"hasta {info.MaxParty} Pokémon");
        }
    }

    Console.WriteLine($"  {total} Pokémon, {replaced} con especie nueva");

    Console.WriteLine($"  equipos que cambiaron de tamaño: {sizeChanged}   (debe ser 0)");
    // Este renglon decia «debe ser 0» y dejo de ser verdad el dia que los roles empezaron a subir
    // los niveles: generado con un rol, se mueven TODOS. Un contador que se lee como una alarma
    // cuando lo normal es que no lo sea acaba enseñando a ignorarlo.
    Console.WriteLine($"  NIVELES movidos: {levelsMoved}   "
                      + "(con rol se mueven todos; 0 solo si se generó sin rol)");
    Console.WriteLine($"  objetos del cartucho cambiados o quitados: {itemsLost}   (debe ser 0)");
    Console.WriteLine($"  objetos añadidos donde no había: {itemsAdded}   (los pone la dificultad, §122)");
    Console.WriteLine($"  EV repartidos otra vez: {evsChanged}   (los reparte la dificultad, §122)");
    Console.WriteLine($"  EV con otro total: {evTotalChanged}   (debe ser 0: se reparten, no se añaden)");
    Console.WriteLine($"  EV por encima de 252: {evOverCap}   (debe ser 0)");
    Console.WriteLine($"  especies prohibidas: {offenders}   (debe ser 0)");
    Console.WriteLine($"  nivel más alto del juego: {highest} (entrenador {highestTrainer})");

    // Que combates llevan un Pokemon ya megaevolucionado, y a que nivel van. Sin esto, «no me
    // sale ninguna mega» solo se puede contestar suponiendo.
    var withMega = new List<string>();

    for (var t = 0; t < modded.FileCount; t++)
    {
        var party = modded[t];
        var slots = TrainerPokemonTable.Count(party);

        if (slots == 0 || party.Length != slots * TrainerPokemonTable.EntrySize)
        {
            continue;
        }

        var mega = Enumerable.Range(0, slots)
            .Any(s => TrainerPokemonTable.GetForm(party, s) > 0);

        if (mega)
        {
            var top = Enumerable.Range(0, slots).Max(s => TrainerPokemonTable.GetLevel(party, s));
            withMega.Add($"#{t} Nv.{top}");
        }
    }

    // Y los que caen en la franja del umbral, con y sin mega: es donde se ve si un combate se
    // quedo fuera por nivel o por no ser de una clase importante.
    var band = new List<string>();

    for (var t = 0; t < modded.FileCount; t++)
    {
        var party = modded[t];
        var slots = TrainerPokemonTable.Count(party);

        if (slots == 0 || party.Length != slots * TrainerPokemonTable.EntrySize)
        {
            continue;
        }

        var top = Enumerable.Range(0, slots).Max(s => TrainerPokemonTable.GetLevel(party, s));

        if (top is >= 33 and <= 42)
        {
            var mega = Enumerable.Range(0, slots).Any(s => TrainerPokemonTable.GetForm(party, s) > 0);
            band.Add($"#{t} Nv.{top}{(mega ? " MEGA" : "")}");
        }
    }

    Console.WriteLine($"  entrenadores de Nv.33 a 42: {band.Count} -> {string.Join(", ", band.Take(24))}");
    // Los combates IMPORTANTES por clase, con su nivel y si llevan mega. Es lo unico que contesta
    // «por que este no lleva»: o esta por debajo del umbral, o su clase no cuenta como importante.
    var classFile = Path.Combine(Path.GetDirectoryName(generatedPath)!, "..", "0", "6");

    if (File.Exists(classFile))
    {
        var meta = new GARC.LazyGARC(await File.ReadAllBytesAsync(classFile));
        var important = keyClasses;

        // Una mega es una FORMA de su especie (§73), y desde el §138 también lo son las regionales:
        // un Meowth de Galar tiene forma 1 y no es una mega. Contar «forma > 0» daba 154 megas en un
        // mundo con 96 combates importantes, que es un número que no significa nada. La tabla del
        // cartucho dice cuáles lo son de verdad.
        var megaPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(generatedPath)!, "..", "..", "0", "1", "5"));
        var megaForms = File.Exists(megaPath)
            ? MegaTrainerRandomizer.ReadForms(megaPath)
            : new Dictionary<int, IReadOnlyList<int>>();

        bool IsMega(byte[] party, int slot)
        {
            var form = TrainerPokemonTable.GetForm(party, slot);

            return form > 0
                && megaForms.TryGetValue(TrainerPokemonTable.GetSpecies(party, slot), out var forms)
                && forms.Contains(form);
        }

        var rows = new List<(int Level, string Text)>();
        var withoutMega = new List<string>();
        var extras = new Dictionary<int, int>();

        for (var t = 0; t < Math.Min(meta.FileCount, modded.FileCount); t++)
        {
            var entry = meta[t];
            var cls = entry.Length >= 0x14 ? BitConverter.ToUInt16(entry, 0x00) : -1;

            if (!important.Contains(cls))
            {
                continue;
            }

            var party = modded[t];
            var slots = TrainerPokemonTable.Count(party);

            if (slots == 0 || party.Length != slots * TrainerPokemonTable.EntrySize)
            {
                continue;
            }

            var top = Enumerable.Range(0, slots).Max(s => TrainerPokemonTable.GetLevel(party, s));
            var mega = Enumerable.Range(0, slots).Any(s => IsMega(party, s));

            // El nivel del CARTUCHO, que es el que sitúa el combate en la historia: el que hay en el
            // fichero ya lleva el porcentaje del rol encima (§85).
            var was = vanilla[t];
            var wasSlots = TrainerPokemonTable.Count(was);
            var story = wasSlots > 0
                ? Enumerable.Range(0, wasSlots).Max(s => TrainerPokemonTable.GetLevel(was, s))
                : top;

            // Por tamaño del cartucho, no solo por el extra: «+1 en 42 combates» no dice nada por sí
            // solo, porque seis es el techo del motor de combate y a quien ya iba con cinco solo le
            // cabe uno (§47). Lo que hay que poder ver es si alguno se quedó corto SIN tocar el techo.
            extras[wasSlots * 10 + slots] = extras.GetValueOrDefault(wasSlots * 10 + slots) + 1;

            // La sexta prueba es el Dominante Vikavolt, nivel 29 de cartucho (§85). Es ahí donde el
            // jugador puede megaevolucionar (§72), así que es ahí donde importa que el rival pueda.
            if (!mega && story >= 29)
            {
                withoutMega.Add($"#{t} clase {cls} Nv.{top} (cartucho {story})");
            }

            rows.Add((top, $"#{t} clase {cls} Nv.{top}{(mega ? " MEGA" : " ---")}"));
        }

        Console.WriteLine($"\n  IMPORTANTES ({rows.Count}), de menor a mayor nivel:");

        foreach (var row in rows.OrderBy(r => r.Level))
        {
            Console.WriteLine($"    {row.Text}");
        }

        Console.WriteLine("\n  TAMAÑO DE LOS EQUIPOS IMPORTANTES (cartucho -> ahora):");

        foreach (var pair in extras.OrderBy(p => p.Key))
        {
            var was = pair.Key / 10;
            var now = pair.Key % 10;
            var why = now == 6 && now - was < 2 ? "  (tope de seis)" : string.Empty;

            Console.WriteLine($"    {was} -> {now}  ({now - was:+0;-0;0}): {pair.Value} combate(s){why}");
        }

        Console.WriteLine($"\n  DE LA 6ª PRUEBA EN ADELANTE SIN MEGA: {withoutMega.Count}   (debe ser 0)");

        foreach (var one in withoutMega.Take(20))
        {
            Console.WriteLine($"    {one}");
        }
    }

    Console.WriteLine($"  combates con mega: {withMega.Count}");


    Console.WriteLine($"    los de nivel más bajo: {string.Join(", ",
        withMega.OrderBy(w => int.Parse(w.Split("Nv.")[1])).Take(8))}");


    if (evolutions is not null && threshold > 0)
    {
        Console.WriteLine($"  de nivel {threshold} en adelante (6ª prueba): {checkedAbove} Pokémon, "
                          + $"{unevolved} SIN evolucionar del todo   (debe ser 0)");
    }

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
        : LatestGenerated(root);
    var useGenerated = args.Contains("--gen") && File.Exists(generated);
    var cro = await File.ReadAllBytesAsync(useGenerated ? generated : workspace.PathOf(GameFiles.Shop));
    Console.WriteLine(useGenerated ? "=== FICHERO GENERADO ===" : "=== VANILLA ===");
    var shops = ShopTable.Read(cro);

    Console.WriteLine($"\nShop.cro: {cro.Length} bytes, {shops.Count} inventarios");
    Console.WriteLine($"MT: ids {ShopTable.FirstTechnicalMachine} ({items[ShopTable.FirstTechnicalMachine]}) .. {ShopTable.LastTechnicalMachine} ({items[ShopTable.LastTechnicalMachine]})");

    foreach (var shop in shops)
    {
        // Con el id delante del nombre: la lista de regularMartReplaced va por id, y ponerlos de
        // memoria es exactamente el error del §45 -un id que cae en otro objeto no falla nunca-.
        var contents = Enumerable.Range(0, shop.Count)
            .Select(s => ShopTable.GetItem(cro, shop, s))
            .Select(id => $"{id} {items[id]}");
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

    // Las formas regionales que el gacha y el wonder trade pueden dar: las de la configuracion que
    // ESTE mundo declara, con el nombre que les da PKHeX en español (§139).
    var regional = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json")).RegionalForms;
    var spanish = PKHeX.Core.GameInfo.GetStrings("es");
    var speciesNames = workspace.Config.GetText(TextName.SpeciesNames);
    var abilityNames = workspace.Config.GetText(TextName.AbilityNames);
    var natureNames = workspace.Config.GetText(TextName.Natures);
    var personal = workspace.Config.GetGARCData("personal");

    // El último subfichero es la tabla entera empaquetada; las especies son los anteriores.
    var count = Math.Min(personal.Files.Length - 1, speciesNames.Length);
    var entries = new List<object>();
    var histogram = new Dictionary<string, int>();
    var legendaries = 0;

    // Los ids que de verdad han entrado en la lista. Las familias se recortan contra esto para no
    // nombrar a nadie que la aplicacion no conozca.
    var known = new HashSet<int>();

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
        // Con sus nueve bits: la expansión guarda el noveno en el último byte (§132).
        var abilities = Enumerable.Range(0, PersonalEntry7.AbilityOffsets.Length)
            .Select(slot => PersonalEntry7.GetAbility(raw, 0, slot))
            .Where(ability => ability > 0 && ability < abilityNames.Length)
            .Select(ability => abilityNames[ability])
            .Distinct()
            .ToArray();

        var declared = PersonalEntry7.GetFormCount(raw, 0);
        var formNames = PKHeX.Core.FormConverter.GetFormList(id, spanish.Types, spanish.forms,
            PKHeX.Core.GameInfo.GenderSymbolASCII, PKHeX.Core.EntityContext.Gen9);
        var forms = regional.Where(entry => entry.Species == id)
            .SelectMany(entry => entry.Forms)
            .Where(form => form > 0 && form < declared)
            .Distinct()
            .Order()
            .Select(form => new { form, name = form < formNames.Length ? formNames[form] : form.ToString() })
            .ToArray();

        entries.Add(new { id, name = speciesNames[id], baseStatTotal = total, legendary = special, abilities, forms });
        known.Add(id);

        if (special)
        {
            legendaries++;
        }

        var bucket = total <= 400 ? "1 (<=400)" : total <= 490 ? "2 (<=490)"
            : total <= 535 ? "3 (<=535)" : total <= 590 ? "4 (<=590)" : "5 (>590)";
        histogram[bucket] = histogram.GetValueOrDefault(bucket) + 1;
    }

    // LAS FAMILIAS. El gacha reparte lineas evolutivas y no especies sueltas, asi que necesita
    // saber quien evoluciona en quien; y tiene que salir de aqui, porque la aplicacion no lleva
    // ROM. Se leen del mundo que se este mirando -- el mod de expansion incluido --, no de una
    // lista escrita a mano que envejeceria.
    var evolutions = EvolutionTable.Read(workspace.PathOf(GameFiles.Evolution));

    // Se descarta la familia entera si su PRIMERA etapa no sobrevive al recorte, y ese caso existe:
    // las formas de Alola tienen linea propia -- Rattata de Alola evoluciona a Raticate de Alola --
    // y su base es una entrada de FORMA, con indice por encima de las especies. Quedarse solo con
    // lo conocido dejaba una familia cuya unica etapa era Raticate, asi que el gacha entregaba un
    // Arcanine o un Golem como si fuera una primera etapa. Medido: 24 especies en dos familias.
    var lines = evolutions.Lines()
        .Where(line => line[0].Any(known.Contains))
        .Select(line => line.Select(stage => stage.Where(id => known.Contains(id)).ToArray())
            .Where(stage => stage.Length > 0)
            .ToArray())
        .Where(line => line.Length > 0)
        .ToList();

    // Y se comprueba, porque un descarte mal puesto no falla: reparte de mas y calla.
    var twice = lines.SelectMany(line => line.SelectMany(stage => stage))
        .GroupBy(id => id)
        .Where(group => group.Count() > 1)
        .ToList();

    if (twice.Count > 0)
    {
        Console.WriteLine($"  AVISO: {twice.Count} especies en mas de una familia, o sea repartidas "
            + $"desde dos sitios: {string.Join(", ", twice.Take(20).Select(g => g.Key))}");
    }

    // Una especie que no cae en ninguna familia no puede salir del gacha, y desapareceria sin
    // decir nada. Las hay: las que SOLO evolucionan de una forma regional -- Obstagoon de
    // Zigzagoon de Galar, Sirfetch'd, Basculegion... --, cuya familia se acaba de descartar por lo
    // de arriba. Se les da familia PROPIA de una etapa, que es lo que son para el gacha: algo que
    // no se alcanza subiendo desde ninguna base que el gacha reparta, igual que un Paradoja o un
    // Farfetch'd. Dejarlas fuera seria peor que el fallo que se acaba de arreglar.
    var inALine = lines.SelectMany(line => line.SelectMany(stage => stage)).ToHashSet();
    var orphans = known.Where(id => !inALine.Contains(id)).ToList();

    foreach (var id in orphans)
    {
        lines.Add([[id]]);
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
            species = entries,
            lines
        },
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"{lines.Count} familias evolutivas, "
        + $"{lines.Count(l => l.Length == 3)} de tres etapas, "
        + $"{lines.Count(l => l.Length == 2)} de dos, "
        + $"{lines.Count(l => l.Length == 1)} sin evolucion");

    if (orphans.Count > 0)
    {
        Console.WriteLine($"  {orphans.Count} especies sin familia propia, con familia de una etapa: "
            + $"{string.Join(", ", orphans.Take(20))}");
    }

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
        Console.WriteLine($"  {row.Species,4} {SpeciesName(species, row.Species),-13} {row.Stone,5} {(row.Stone < items.Length ? items[row.Stone] : "#" + row.Stone),-18} "
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
// Quien lleva una especie concreta, con su clase, su nivel y su equipo entero. Existe porque la
// pregunta «me ha salido un X, de donde sale» se ha hecho ya tres veces -la clase 222 de Tilo, el
// Nihilego del Paraiso, y ahora un Larvitar de un recluta- y las tres se contestaron a mano.
//
// Sin ruta mira el cartucho; con ruta lee el trpoke de un mod GENERADO O INSTALADO, que es lo que
// hace falta casi siempre: lo que el jugador se encuentra es lo que hay instalado, no lo que
// diria el codigo de hoy.
async Task QuienLlevaAsync(int species, string? trpokePath, int onlyTrainer = -1)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));

    var trpoke = trpokePath is not null && File.Exists(trpokePath)
        ? trpokePath
        : workspace.PathOf(GameFiles.TrainerPokemon);

    // La tabla de entrenadores tiene que venir del MISMO sitio que el trpoke: el modulo del
    // Pokemon extra cambia las cuentas y cruzarlas descuadra todas las clases (§47).
    var trdata = trpoke == workspace.PathOf(GameFiles.TrainerPokemon)
        ? workspace.PathOf(GameFiles.TrainerData)
        : Path.Combine(Path.GetDirectoryName(trpoke)!, "6");

    Console.WriteLine($"trpoke: {trpoke}");
    Console.WriteLine(onlyTrainer >= 0
        ? $"entrenador {onlyTrainer}"
        : $"buscando: {(species < names.Length ? names[species] : "?")} ({species})");

    // El cartucho, para poder decir a que nivel puso el juego a este entrenador: es ESE nivel, y
    // no el ya subido por el rol, el que decide la regla de la sexta prueba.
    var vanilla = new GARC.LazyGARC(
        await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.TrainerPokemon)));
    var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(trpoke));

    using var trainers = new GarcPatcher(trdata);
    var found = 0;

    for (var trainer = 0; trainer < Math.Min(trainers.FileCount, parties.FileCount); trainer++)
    {
        var entry = trainers.Read(trainer);
        if (entry.Length < 0x14)
        {
            continue;
        }

        var party = parties[trainer];
        var count = TrainerPokemonTable.Count(party);

        // Por id de entrenador, o por especie: la misma impresion sirve para las dos preguntas y
        // se hacen las dos igual de a menudo.
        var wanted = onlyTrainer >= 0
            ? trainer == onlyTrainer
            : count > 0 && Enumerable.Range(0, count)
                .Any(s => TrainerPokemonTable.GetSpecies(party, s) == species);

        if (count == 0 || !wanted)
        {
            continue;
        }

        found++;
        var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
        var team = new List<string>();

        for (var slot = 0; slot < count; slot++)
        {
            var id = TrainerPokemonTable.GetSpecies(party, slot);
            var name = id < names.Length ? names[id] : $"?{id}";
            var level = TrainerPokemonTable.GetLevel(party, slot);

            // El nivel del cartucho en el mismo hueco, cuando el equipo no ha cambiado de tamano.
            var before = trainer < vanilla.FileCount ? vanilla[trainer] : [];
            var story = slot < TrainerPokemonTable.Count(before)
                ? TrainerPokemonTable.GetLevel(before, slot)
                : -1;

            team.Add($"{name} Nv{level}"
                + (story >= 0 ? $" (cartucho {story}{(story >= options.FullyEvolvedFromLevel ? "" : " POR DEBAJO")})" : ""));
        }

        Console.WriteLine();
        Console.WriteLine($"  entrenador {trainer}  clase {trainerClass} "
                          + $"«{(trainerClass < classNames.Length ? classNames[trainerClass] : "?")}» "
                          + $"{(trainer < trainerNames.Length ? trainerNames[trainer] : "?")}");

        foreach (var member in team)
        {
            Console.WriteLine($"      {member}");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"{found} encontrado(s). El corte de la sexta prueba esta en nivel de "
                      + $"cartucho {options.FullyEvolvedFromLevel}.");
}

/// <summary>
/// Lists the trainer classes that are NOT counted as important, so a missing one can be seen.
/// </summary>
/// <remarks>
/// This exists because the same failure has now happened three times: the player fights somebody
/// with a name, no mega and no extra Pokémon show up, and the class turns out to be missing from
/// <c>clasesImportantes</c>. First it was 222, then eight more found in a review, and then class
/// <b>78</b> — Francine, who also appears as class 79, which <em>was</em> in the list. A character
/// with two class ids is exactly what a review done by reading names misses.
///
/// So the review stops being something somebody remembers to do. What gives a boss away is cheap
/// to compute and hard to argue with: <b>few trainers in the class</b> and <b>a big party</b>.
/// Filler classes have dozens of trainers with two or three Pokémon.
/// </remarks>
/// <summary>
/// Gives a mega to the important battles of some classes, inside a mod that is already installed.
/// </summary>
/// <remarks>
/// <para>
/// For when a class turns out to have been missing from <c>clasesImportantes</c> after the world
/// was already generated. Fixing the list only changes the <b>next</b> randomization, and
/// re-randomizing mid-run would hand the player a different world, so this patches just those
/// battles where they are.
/// </para>
/// <para>
/// A mega is a <b>form</b> of its species, not a species of its own, so this is two bytes in place
/// and the subfile does not change size. That is the whole reason it can be done here at all: the
/// extra Pokémon of the same feature <em>adds</em> a party member, which means repacking a GARC,
/// and repacking is the operation that has cost this project the most. It is not done here.
/// </para>
/// <para>
/// Idempotent: a party that already carries a form above zero is left alone, so running it twice
/// costs nothing. And it refuses to touch a trainer whose party does not measure what its own
/// table says, which is the check §47 earned.
/// </para>
/// </remarks>
/// <summary>
/// Applies <c>regularMartReplaced</c> to the Pokémon Centre counters of a mod already installed.
/// </summary>
/// <remarks>
/// Same reason as the mega patch: the list decides how the <b>next</b> randomization goes, and
/// re-randomizing mid-run would hand the player a different world. This reads the very same
/// configuration, so what it writes now is what the next generation would write.
///
/// It is a straight swap of one item id for another inside <c>Shop.cro</c> — two bytes per slot,
/// nothing changes size — and it is idempotent, because an id already replaced is no longer in the
/// list to match. Names are checked against the cartridge's own item table before anything is
/// written: an id that lands on a different item would stock the shop with something else and would
/// never fail, which is the §52 rule.
/// </remarks>
async Task ParchearTiendasAsync(string croPath, bool write)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var items = workspace.Config.GetText(TextName.ItemNames);

    if (options.RegularMartReplaced.Count == 0)
    {
        Console.WriteLine("regularMartReplaced esta vacia: no hay nada que cambiar.");
        return;
    }

    // El nombre contra la tabla del cartucho ANTES de tocar nada. Un id que caiga en otro objeto
    // surtiria la tienda con otra cosa y no fallaria nunca (§52).
    foreach (var entry in options.RegularMartReplaced.Append(options.RegularMartReplacement))
    {
        var real = entry.Id < items.Length ? items[entry.Id] : "?";

        if (!string.Equals(real, entry.Name, StringComparison.Ordinal))
        {
            Console.WriteLine($"El objeto {entry.Id} es «{real}» y la configuracion dice «{entry.Name}».");
            Console.WriteLine("No se toca ninguna tienda.");
            return;
        }
    }

    var replaced = options.RegularMartReplaced.Select(e => e.Id).ToHashSet();
    var cro = await File.ReadAllBytesAsync(croPath);
    var shops = ShopTable.Read(cro);
    var changed = 0;

    foreach (var shop in shops.Where(s => s.Index < ShopTable.RegularMartCount))
    {
        var before = Enumerable.Range(0, shop.Count).Select(s => ShopTable.GetItem(cro, shop, s)).ToList();
        var hits = before.Count(replaced.Contains);

        Console.WriteLine($"  tienda {shop.Index}: {hits} de {shop.Count} objetos se cambian");

        for (var slot = 0; slot < shop.Count; slot++)
        {
            if (replaced.Contains(before[slot]))
            {
                ShopTable.SetItem(cro, shop, slot, options.RegularMartReplacement.Id);
                changed++;
            }
        }
    }

    if (!write)
    {
        Console.WriteLine();
        Console.WriteLine($"Ensayo: {changed} huecos. Con --escribir se escribe, con copia previa.");
        return;
    }

    var copy = Path.Combine(root, "Randomized", "copias-mod",
        $"Shop.cro.antes-de-tiendas-{DateTime.Now:yyyyMMdd-HHmmss}");

    Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
    File.Copy(croPath, copy);
    await File.WriteAllBytesAsync(croPath, cro);

    // Y se relee, que es lo unico que convierte «escrito» en «hecho» (§19).
    var back = ShopTable.Read(await File.ReadAllBytesAsync(croPath));
    var left = 0;

    foreach (var shop in back.Where(s => s.Index < ShopTable.RegularMartCount))
    {
        left += Enumerable.Range(0, shop.Count)
            .Count(s => replaced.Contains(ShopTable.GetItem(cro, shop, s)));
    }

    Console.WriteLine();
    Console.WriteLine($"Copia previa: {copy}");
    Console.WriteLine(left == 0
        ? $"{changed} huecos cambiados y releidos: no queda ni una cura en las tiendas normales."
        : $"Quedan {left} curas despues de escribir. Restaura la copia.");
}

async Task ParchearMegasAsync(string modRomfs, int[] wanted, bool write)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));

    var forms = MegaTrainerRandomizer.ReadForms(workspace.PathOf(GameFiles.MegaEvolution));
    var candidates = MegaTrainerRandomizer.Candidates(forms, options, workspace.MaxSpecies);

    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);

    var dataPath = Path.Combine(modRomfs, "a", "1", "0", "6");
    var partyPath = Path.Combine(modRomfs, "a", "1", "0", "7");

    Console.WriteLine($"{candidates.Length} especies con mega. Clases: {string.Join(", ", wanted)}");
    Console.WriteLine();

    var classes = wanted.ToHashSet();
    var touched = new List<(int Trainer, int Species, int Form)>();

    // La copia va ANTES de abrir el fichero, porque GarcPatcher escribe en el sitio segun se le
    // pide: cuando se cierra ya no hay nada que copiar que sea el original.
    if (write)
    {
        var copy = partyPath + $".antes-de-megas-{DateTime.Now:yyyyMMdd-HHmmss}";

        File.Copy(partyPath, copy);
        Console.WriteLine($"Copia previa: {copy}");
        Console.WriteLine();
    }

    using (var trainers = new GarcPatcher(dataPath))
    using (var parties = new GarcPatcher(partyPath))
    {
        for (var trainer = 0; trainer < trainers.FileCount && trainer < parties.FileCount; trainer++)
        {
            var entry = trainers.Read(trainer);

            if (entry.Length < 0x14)
            {
                continue;
            }

            var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
            var count = (int)entry[ExtraPokemonRandomizer.CountOffset];

            if (!classes.Contains(trainerClass) || count == 0)
            {
                continue;
            }

            var party = parties.Read(trainer);

            // La comprobacion del §47: si el equipo no mide lo que su tabla dice, aqui no se
            // escribe nada. Un desajuste significa que se esta leyendo otra cosa.
            if (party.Length != count * TrainerPokemonTable.EntrySize)
            {
                Console.WriteLine($"  entrenador {trainer,4}: el equipo mide {party.Length} y la tabla"
                                  + $" dice {count * TrainerPokemonTable.EntrySize}. NO SE TOCA.");
                continue;
            }

            if (Enumerable.Range(0, count).Any(i => TrainerPokemonTable.GetForm(party, i) > 0))
            {
                Console.WriteLine($"  entrenador {trainer,4}: ya lleva una forma. Se deja.");
                continue;
            }

            // Sembrado con el id del entrenador: la misma tirada da lo mismo, asi que repetir el
            // comando no reparte megas distintas y lo escrito se puede volver a calcular.
            var pick = new Random(unchecked(20260906 * 31 + trainer));
            var species = candidates[pick.Next(candidates.Length)];
            var form = forms[species][pick.Next(forms[species].Count)];

            // Al ultimo del equipo, que es el mas fuerte en las tablas del cartucho.
            var slot = count - 1;
            var was = TrainerPokemonTable.GetSpecies(party, slot);

            var who = trainer < trainerNames.Length ? trainerNames[trainer] : "?";
            var name = trainerClass < classNames.Length ? classNames[trainerClass] : "?";

            Console.WriteLine($"  entrenador {trainer,4}  {name} {who}: hueco {slot}, "
                              + $"especie {was} -> {species} forma {form}");

            if (!write)
            {
                continue;
            }

            TrainerPokemonTable.SetSpecies(party, slot, species, form);
            TrainerPokemonTable.ClearMoves(party, slot);
            parties.Write(trainer, party);
            touched.Add((trainer, species, form));
        }
    }

    if (!write)
    {
        Console.WriteLine();
        Console.WriteLine("Ensayo. Con --escribir se escribe en el mod, con copia previa del fichero.");
        return;
    }

    // Y se relee, que es lo unico que convierte «escrito» en «hecho» (§19).
    using var back = new GarcPatcher(partyPath);
    var wrong = 0;

    foreach (var (trainer, species, form) in touched)
    {
        var party = back.Read(trainer);
        var slot = TrainerPokemonTable.Count(party) - 1;

        if (TrainerPokemonTable.GetSpecies(party, slot) != species
            || TrainerPokemonTable.GetForm(party, slot) != form)
        {
            Console.WriteLine($"  entrenador {trainer}: al releer no esta la mega. MAL.");
            wrong++;
        }
    }

    Console.WriteLine();
    Console.WriteLine(wrong == 0
        ? $"{touched.Count} combates con mega, releidos y confirmados."
        : $"{wrong} de {touched.Count} no cuadran al releer. Restaura la copia .antes-de-megas.");
}

/// <summary>
/// Re-reads a generated learnset table and checks the three rules that matter hold in it.
/// </summary>
/// <remarks>
/// The tests pin the rules against a made-up catalogue; this is the other half, because the tests
/// cannot tell whether the rules were wired to the real table at all. Counts repeats, Pokémon whose
/// last level-one move cannot attack, and the share of moves that really hurt.
/// </remarks>
/// <summary>Los nombres de movimiento tal y como los trae el mundo que se va a randomizar.</summary>
/// <remarks>Para anclar contra ellos en vez de escribir un nombre de memoria y que no exista.</remarks>
async Task MovimientosAsync(string? filter)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.MoveNames);
    var moves = workspace.Config.Moves;

    for (var id = 0; id < names.Length; id++)
    {
        if (filter is not null && !names[id].Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var m = id < moves.Length ? moves[id] : null;

        Console.WriteLine(m is null
            ? $"  {id,4}  {names[id]}"
            : $"  {id,4}  {names[id],-22} tipo {m.Type,2}  cat {m.Category}  pot {m.Power,3}  prec {m.Accuracy,3}");
    }
}

async Task AprendizajesAsync(string learnsetPath)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var moves = workspace.Config.Moves;
    var names = workspace.Config.GetText(TextName.MoveNames);

    var perfect = MoveCatalog.DetectPerfectAccuracy([.. moves.Select(m => m.Accuracy)], names);
    var floor = options.LearnsetDamagingFloor;

    bool Hurts(int id) => id > 0 && id < moves.Length
        && new MoveFacts(id, moves[id].Type, moves[id].Power, moves[id].Accuracy,
            Math.Max(1, moves[id].HitMax), null).IsGoodDamaging(floor, perfect);

    using var patcher = new GarcPatcher(learnsetPath);
    int species = 0, repeats = 0, defenceless = 0, slots = 0, hurting = 0, noLevelOne = 0;

    for (var index = 0; index < patcher.FileCount; index++)
    {
        var entry = patcher.Read(index);
        var pairs = (entry.Length / 4) - 1;

        if (pairs <= 0)
        {
            continue;
        }

        species++;
        var learnt = new List<int>();
        var last = -1;

        for (var pair = 0; pair < pairs; pair++)
        {
            learnt.Add(BitConverter.ToUInt16(entry, pair * 4));

            if (BitConverter.ToUInt16(entry, (pair * 4) + 2) <= 1)
            {
                last = pair;
            }
        }

        // --especie N: el aprendizaje de esa especie, con nivel, tipo y potencia de cada movimiento.
        if (args.SkipWhile(a => a != "--especie").Skip(1).FirstOrDefault() is { } wanted && int.TryParse(wanted, out var id) && id == index)
        {
            var speciesNames = workspace.Config.GetText(TextName.SpeciesNames);
            var typeNames = workspace.Config.GetText(TextName.Types);
            Console.WriteLine($"\n{speciesNames[id]}: {learnt.Count} movimientos");
            for (var pair = 0; pair < learnt.Count; pair++)
            {
                var move = learnt[pair];
                Console.WriteLine($"  Nv.{BitConverter.ToUInt16(entry, (pair * 4) + 2),3}  {names[move],-18} {typeNames[moves[move].Type],-10} pot {moves[move].Power}");
            }
        }

        slots += learnt.Count;
        hurting += learnt.Count(Hurts);
        repeats += learnt.Count - learnt.Distinct().Count();

        if (last < 0)
        {
            noLevelOne++;
        }
        else if (!Hurts(learnt[last]))
        {
            defenceless++;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"{species} aprendizajes, {slots} huecos");
    Console.WriteLine($"  movimientos repetidos dentro de un aprendizaje: {repeats}   (debe ser 0)");
    Console.WriteLine($"  sin con que atacar al nivel 1: {defenceless}   (debe ser 0)");
    Console.WriteLine($"  no aprenden nada al nivel 1: {noLevelOne}   (esos no se pueden garantizar)");
    Console.WriteLine($"  huecos que de verdad hacen daño: {hurting} de {slots}"
                      + $" ({100.0 * hurting / Math.Max(1, slots):F0}%, pedido {options.LearnsetGoodDamagingPercent}% minimo)");
}

async Task ClasesAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var roles = PermaLocke.Data.JsonRoleCatalog.Load(Path.Combine(root, "Data", "roles.json"));
    var classes = roles.ImportantTrainerClasses.ToHashSet();
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);

    var trdata = args.Length > 1 && File.Exists(args[1])
        ? args[1]
        : workspace.PathOf(GameFiles.TrainerData);

    using var trainers = new GarcPatcher(trdata);
    var seen = new Dictionary<int, (int Trainers, int Biggest, List<string> Names)>();

    for (var trainer = 0; trainer < trainers.FileCount; trainer++)
    {
        var entry = trainers.Read(trainer);

        if (entry.Length < 0x14)
        {
            continue;
        }

        var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
        var count = (int)entry[ExtraPokemonRandomizer.CountOffset];

        if (count == 0)
        {
            continue;
        }

        if (!seen.TryGetValue(trainerClass, out var row))
        {
            row = (0, 0, []);
        }

        row.Names.Add(trainer < trainerNames.Length ? trainerNames[trainer] : "?");
        seen[trainerClass] = (row.Trainers + 1, Math.Max(row.Biggest, count), row.Names);
    }

    // Los nombres que YA cuentan como importantes. Un personaje repartido en varias clases es
    // como se cuela uno: Francine sale en la 79, que estaba, y en la 78, que no; Fabio en la 71 y
    // en la 72; Tilo en la 101, la 102 y la 221. Buscar por nombre lo caza y contar Pokemon no.
    var covered = seen
        .Where(pair => classes.Contains(pair.Key))
        .SelectMany(pair => pair.Value.Names)
        // Los nombres en blanco los escribe el juego como puntos y los sin traducir como [~ n]:
        // los dos salen en clases de relleno y emparejarian cualquier cosa con cualquier cosa.
        .Where(name => name.Length > 0 && !name.StartsWith('[') && name.Any(char.IsLetter))
        .ToHashSet(StringComparer.Ordinal);

    Console.WriteLine($"{seen.Count} clases con entrenadores. {classes.Count} cuentan como importantes.");
    Console.WriteLine();

    var repeated = seen
        .Where(pair => !classes.Contains(pair.Key) && pair.Value.Names.Any(covered.Contains))
        .OrderBy(pair => pair.Key)
        .ToList();

    Console.WriteLine(repeated.Count == 0
        ? "MISMO PERSONAJE EN OTRA CLASE: ninguno. La lista esta completa por ese lado."
        : "MISMO PERSONAJE EN OTRA CLASE -- estos casi seguro faltan:");

    foreach (var (id, row) in repeated)
    {
        var name = id < classNames.Length ? classNames[id] : "?";
        // TODOS los nombres de la clase, no solo el que coincide: si los otros ocho son relleno,
        // meter la clase entera asciende a ocho entrenadores que nadie ha mirado.
        var who = string.Join(", ", row.Names.Distinct().Take(10));

        Console.WriteLine($"  clase {id,3}  {name,-22} {row.Trainers,3} entrenadores   {who}");
    }

    Console.WriteLine();
    Console.WriteLine("CLASES QUE NO ESTAN EN LA LISTA, las mas sospechosas primero:");
    Console.WriteLine("(pocos entrenadores y equipo grande = personaje con nombre)");
    Console.WriteLine();

    var missing = seen
        .Where(pair => !classes.Contains(pair.Key))
        .OrderByDescending(pair => pair.Value.Biggest)
        .ThenBy(pair => pair.Value.Trainers)
        .ToList();

    foreach (var (id, row) in missing.Where(pair => pair.Value.Biggest >= 3 || pair.Value.Trainers <= 6))
    {
        var name = id < classNames.Length ? classNames[id] : "?";
        var who = string.Join(", ", row.Names.Distinct().Take(6));

        Console.WriteLine($"  clase {id,3}  {name,-22} {row.Trainers,3} entrenadores, "
                          + $"hasta {row.Biggest} Pokemon   {who}");
    }

    return;
}

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

    // El corte de verdad, el del JSON: con el 33 escrito aqui la lista decia MEGA donde el randomizador no la ponia.
    // Sin ruta los niveles son del cartucho y se comparan tal cual; en un mod generado ya van subidos.
    // Con --rol, el porcentaje con el que se genero ese mod; sin el, el del fichero de opciones.
    var megaOptions = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var megaRole = args.SkipWhile(a => a != "--rol").Skip(1).FirstOrDefault() is { } roleId ? roles.Find(roleId) : null;
    var megaFloor = trpoke == workspace.PathOf(GameFiles.TrainerPokemon)
        ? megaOptions.MegaTrainerMinimumLevel
        : TrainerRandomizer.Raise(megaOptions.MegaTrainerMinimumLevel, megaRole?.EnemyLevelPercent ?? megaOptions.EnemyLevelPercent);

    Console.WriteLine($"COMBATES IMPORTANTES: {rows.Count} (clases: {classes.Count}); mega desde el nivel {megaFloor}");
    Console.WriteLine();

    foreach (var row in rows.OrderBy(r => r.Max).ThenBy(r => r.Id))
    {
        var name = row.Class < classNames.Length ? classNames[row.Class] : "?";
        var who = row.Id < trainerNames.Length ? trainerNames[row.Id] : "?";
        var mark = megaOptions.MegaTrainers && row.Max >= megaFloor ? "MEGA" : "    ";

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

// Qué sitio es cada número de mapa de zonedata: el que guarda la partida en Situation.M. Para cada
// mapa, su ParentMap -índice de la lista de lugares-, el nombre del cartucho, el de PKHeX para ese
// mismo índice y el id normalizado con el que la run y el MAPA nombran la zona. Comprueba de paso que
// los dos nombres coinciden salvo el paréntesis de PKHeX, que es lo que hace de ParentMap el puente.
async Task MapasAsync(int[] only, bool write)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var locations = workspace.Config.GetText(TextName.metlist_000000);
    var zoneGarc = workspace.Config.GetlzGARCData("zonedata");
    var worldGarc = workspace.Config.GetlzGARCData("worlddata");
    var worlds = worldGarc.Files.Select(f => pk3DS.Core.CTR.Mini.UnpackMini(f, "WD")[0]).ToArray();
    var zones = pk3DS.Core.Structures.ZoneData7.GetZoneData7Array(zoneGarc.Files[0], zoneGarc.Files[1], locations, worlds);
    var strings = PKHeX.Core.GameInfo.GetStrings("es");

    var mismatches = 0;
    Console.WriteLine($"{zones.Length} mapas en zonedata");

    foreach (var zone in zones)
    {
        var pkhex = strings.GetLocationName(false, (ushort)zone.ParentMap, 7, 7, PKHeX.Core.GameVersion.UM);
        var cut = pkhex.IndexOf(" (", StringComparison.Ordinal);
        var same = (cut < 0 ? pkhex : pkhex[..cut]) == zone.LocationName;

        if (!same)
        {
            mismatches++;
        }

        if (only.Length == 0 || only.Contains(zone.Index))
        {
            Console.WriteLine($"  mapa {zone.Index,3}  mundo {zone.WorldIndex,3}  área {zone.AreaIndex,3}  lugar {zone.ParentMap,3}  "
                              + $"«{zone.LocationName}» / PKHeX «{pkhex}» -> {PermaLocke.Rules.Services.EncounterService.NormaliseLocationId(pkhex)}"
                              + (same ? string.Empty : "   <- NO CASAN"));
        }
    }

    Console.WriteLine($"Mapas cuyo nombre del cartucho no casa con el de PKHeX: {mismatches}");

    if (!write)
    {
        return;
    }

    // Un mapa sin nombre de lugar no es un sitio al que se pueda gastar un encuentro: no va.
    var entries = zones
        .Select(zone => (Zone: zone, Name: strings.GetLocationName(false, (ushort)zone.ParentMap, 7, 7, PKHeX.Core.GameVersion.UM)))
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Name) && pair.Name.Any(char.IsLetter))
        .Select(pair => new
        {
            mapa = pair.Zone.Index,
            mundo = pair.Zone.WorldIndex,
            zona = PermaLocke.Rules.Services.EncounterService.NormaliseLocationId(pair.Name),
            nombre = pair.Name
        })
        .ToArray();

    var document = new
    {
        comentario = "Generado por: PermaLocke.RomTool mapas --escribir. No editar a mano.",
        fuente = "a/0/7/7 (zonedata, ParentMap) + a/0/9/1 (worlddata) + nombres de lugar de PKHeX",
        como = "El juego guarda en memoria el mundo y el mapa en que está el jugador. mapa es el índice de "
            + "zonedata; mundo, el que el cartucho le asigna, y sirve para descartar lecturas basura; zona es "
            + "el id normalizado del lugar, el mismo que usan la run y el MAPA. Comprobado contra el juego en "
            + "doce sitios (§117).",
        mapas = entries
    };

    var path = Path.Combine(root, "Data", "mapas.json");
    await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(document, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));

    Console.WriteLine($"{entries.Length} mapas escritos en {path}");
}

// Qué zonas cubre cada mundo. En Alola un "mundo" de zonedata es una isla, y esto es lo que lo
// demuestra: sale del cartucho -campo WorldIndex de ZoneData7- en vez de escribirse de memoria.
// Es el ancla del mapa de la aplicación: sin ella, repartir 116 zonas entre cuatro islas sería
// recordar, y de memoria ya se colocó mal Colina Saltagua.
async Task MundosAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var locations = workspace.Config.GetText(TextName.metlist_000000);

    var zoneGarc = workspace.Config.GetlzGARCData("zonedata");
    var worldGarc = workspace.Config.GetlzGARCData("worlddata");
    var worlds = worldGarc.Files.Select(f => pk3DS.Core.CTR.Mini.UnpackMini(f, "WD")[0]).ToArray();

    var files = zoneGarc.Files;
    var zones = pk3DS.Core.Structures.ZoneData7.GetZoneData7Array(
        files[0], files[1], locations, worlds);

    // Los cuatro exteriores, medidos: son los unicos mundos que cubren muchos nombres a la vez, y
    // cada uno trae el juego de rutas de su isla (1-3, 4-9, 10-17, y las de Poni).
    (int First, string Island)[] outdoors =
    [
        (0, "Melemele"), (58, "Akala"), (117, "Ula-Ula"), (197, "Poni")
    ];

    string IslandOf(int world)
    {
        var island = "Otros";
        foreach (var (first, name) in outdoors)
        {
            if (world >= first) island = name;
        }
        return island;
    }

    // Nombres que solo pueden ser de una isla, para comprobar el reparto por rangos. No cubren
    // todo: cubren lo suficiente para que un rango mal puesto choque contra alguno.
    (string Token, string Island)[] tells =
    [
        ("Melemele", "Melemele"), ("Hauoli", "Melemele"), ("Lilii", "Melemele"),
        ("Mahalo", "Melemele"), ("Kalae", "Melemele"), ("Dequilate", "Melemele"),
        ("Akala", "Akala"), ("Konikoni", "Akala"), ("Ohana", "Akala"),
        ("Kantai", "Akala"), ("Hanohano", "Akala"), ("Wela", "Akala"),
        ("Ula-Ula", "Ula-Ula"), ("Malíe", "Ula-Ula"), ("Hokulani", "Ula-Ula"),
        ("Po", "Ula-Ula"), ("Haina", "Ula-Ula"), ("Lanakila", "Ula-Ula"),
        ("Poni", "Poni"), ("Marina", "Poni"), ("Exeggutor", "Poni")
    ];

    var byName = new Dictionary<string, string>(StringComparer.Ordinal);
    var clashes = new List<string>();

    foreach (var zone in zones)
    {
        var name = zone.LocationName;
        if (string.IsNullOrWhiteSpace(name) || name == "\uFF0D") continue;

        var island = IslandOf(zone.WorldIndex);

        if (byName.TryGetValue(name, out var already) && already != island)
        {
            clashes.Add($"«{name}» sale en {already} y en {island} (mundo {zone.WorldIndex})");
            continue;
        }
        byName[name] = island;
    }

    Console.WriteLine($"{byName.Count} nombres repartidos entre {byName.Values.Distinct().Count()} islas");

    foreach (var island in byName.GroupBy(p => p.Value).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        Console.WriteLine();
        Console.WriteLine($"{island.Key.ToUpperInvariant()}  ({island.Count()})");
        foreach (var name in island.Select(p => p.Key).OrderBy(n => n, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {name}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("COMPROBACIÓN por nombres que solo pueden ser de una isla:");
    var wrong = 0;

    foreach (var (name, island) in byName)
    {
        foreach (var (token, expected) in tells)
        {
            var word = name.Contains(token, StringComparison.Ordinal);
            if (token == "Po") word = name.Contains("Pueblo Po", StringComparison.Ordinal);
            if (!word || island == expected) continue;

            Console.WriteLine($"    MAL: «{name}» cae en {island} y su nombre dice {expected}");
            wrong++;
        }
    }

    Console.WriteLine($"    contradicciones: {wrong}");
    foreach (var clash in clashes) Console.WriteLine($"    AMBIGUO: {clash}");

    WriteIslands(byName, tells, wrong, clashes.Count);
}

// Escribe Data/islas.json y comprueba que cubre la OTRA lista de nombres, la de PKHeX, que es la
// que los Pokémon llevan escrita y con la que la run identifica una zona. Son dos listas distintas
// -el cartucho dice «Ciudad Hauoli», PKHeX «Ciudad Hauoli (Puerto)»- y el puente es quitar el
// paréntesis. Un nombre de PKHeX que no case aquí saldría en el mapa sin isla, así que se cuenta.
void WriteIslands(Dictionary<string, string> byName, (string Token, string Island)[] tells,
    int wrong, int ambiguous)
{
    if (wrong > 0)
    {
        throw new InvalidDataException(
            "El reparto por islas se contradice con los nombres. No se escribe nada.");
    }

    var strings = PKHeX.Core.GameInfo.GetStrings("es");
    var met = new List<(int Id, string Name)>();

    for (var id = 0; id <= 700; id++)
    {
        var name = strings.GetLocationName(false, (ushort)id, 7, 7, PKHeX.Core.GameVersion.UM);
        if (!string.IsNullOrWhiteSpace(name)) met.Add((id, name));
    }

    string Base(string name)
    {
        var cut = name.IndexOf(" (", StringComparison.Ordinal);
        return cut < 0 ? name : name[..cut];
    }

    // Tres marcadores de PKHeX que no son sitios: no van al mapa.
    string[] notPlaces = ["Lugar lejano (-)", "Lugar misterioso", "FF0D"];

    // Y el respaldo para lo que zonedata no nombra: la propia isla lo dice. Es el MISMO criterio
    // que la comprobacion de arriba, la que dio cero contradicciones, aplicado a ocho casos.
    foreach (var name in met.Select(m => m.Name).Distinct())
    {
        if (byName.ContainsKey(Base(name)) || notPlaces.Contains(name)) continue;

        foreach (var (token, island) in tells)
        {
            if (!name.Contains(token, StringComparison.Ordinal)) continue;
            byName[name] = island;
            break;
        }
    }

    var orphans = met.Select(m => m.Name)
        .Where(n => !byName.ContainsKey(Base(n)) && !notPlaces.Contains(n) && n.Any(char.IsLetter))
        .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

    Console.WriteLine();
    Console.WriteLine($"Lista de PKHeX: {met.Count} lugares, {met.Select(m => m.Name).Distinct().Count()} nombres");
    Console.WriteLine($"    sin isla: {orphans.Length}");
    foreach (var orphan in orphans) Console.WriteLine($"        {orphan}");

    var document = new
    {
        comment = "Generado por: PermaLocke.RomTool mundos. No editar a mano.",
        source = "a/0/7/7 (zonedata, campo WorldIndex) + a/0/9/1 (worlddata)",
        how = "Los cuatro exteriores del cartucho -mundos 0, 58, 117 y 197- son las cuatro islas, "
            + "y los interiores se numeran entre ellos. Comprobado contra 21 nombres que solo "
            + "pueden ser de una isla: cero contradicciones.",
        ambiguous,

        // Se escriben los nombres de PKHEX, no los del cartucho. Son dos listas -el cartucho dice
        // «Ciudad Hauoli» y PKHeX «Ciudad Hauoli (Puerto)»- y la que la run guarda en cada captura
        // es la segunda. Un mapa dibujado con la primera tendría casillas que no casan con ningún
        // Pokémon: pinchar «Ciudad Hauoli» daría ciudad-hauoli y la captura dice ciudad-hauoli-puerto.
        // La isla la hereda el nombre largo del corto, que es de donde se midió.
        islands = met.Select(entry => entry.Name).Distinct()
            .Where(name => name.Any(char.IsLetter))
            .Select(name => (Name: name, Island: byName.GetValueOrDefault(Base(name))
                                                ?? byName.GetValueOrDefault(name)))
            .Where(pair => pair.Island is not null)
            .GroupBy(pair => pair.Island!)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.Select(pair => pair.Name)
                    .OrderBy(name => name, StringComparer.Ordinal).ToArray())
    };

    var path = Path.Combine(root, "Data", "islas.json");
    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(document,
        new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

    Console.WriteLine($"escrito {path}");
}


// Barre el RomFS entero buscando el dibujo del mapa de Alola. La pregunta que contesta es si el
// cartucho lo lleva y en que formato: si esta, el mapa de la aplicacion puede ser EL mapa, sacado
// de la ROM del propio jugador como los sprites del §28, sin que viaje ningun asset de Nintendo.
// Se buscan DIBUJOS GRANDES, que es lo que un mapa de region es y lo que un icono no.
void MapaBuscar()
{
    var reader = new RomFsReader(RequireRom());

    // Los cuatro gigantes son modelos, encuentros y sonido: ni uno es una lamina, y abrirlos
    // cuesta minutos. Todo lo demas se mira.
    var candidates = reader.Files.Values
        .Where(f => f.Size is > 4096 and < 40_000_000)
        .OrderBy(f => f.Path, StringComparer.Ordinal)
        .ToArray();

    Console.WriteLine($"{candidates.Length} ficheros por mirar (de {reader.Files.Count})");

    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var found = new List<(string File, int Sub, int W, int H, string Format)>();
    var opened = 0;

    foreach (var candidate in candidates)
    {
        var extracted = Path.Combine(temp, candidate.Path.Replace('/', '_'));

        try
        {
            if (!File.Exists(extracted) && !reader.ExtractTo(candidate.Path, extracted))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(extracted);

            if (bytes.Length < 8 || bytes[0] != 'C' || bytes[1] != 'R' || bytes[2] != 'A' || bytes[3] != 'G')
            {
                File.Delete(extracted);
                continue;
            }

            var garc = new GARC.MemGARC(bytes);
            opened++;

            for (var i = 0; i < garc.FileCount; i++)
            {
                byte[] raw;

                try
                {
                    raw = garc.GetFile(i);
                }
                catch
                {
                    continue;
                }

                if (raw.Length < 0x2C)
                {
                    continue;
                }

                var data = Decompress(raw);

                if (data.Length < 0x28)
                {
                    continue;
                }

                var footer = data.AsSpan(data.Length - 0x28);

                if (footer[0] != 'F' || footer[1] != 'L' || footer[2] != 'I' || footer[3] != 'M')
                {
                    continue;
                }

                int w = BitConverter.ToUInt16(footer[0x1C..]);
                int h = BitConverter.ToUInt16(footer[0x1E..]);
                var fmt = footer[0x22];

                // Un mapa de region es grande. Los iconos del cartucho son 40x30 y 32x32.
                if (w >= 64 && h >= 64)
                {
                    found.Add((candidate.Path, i, w, h, $"fmt{fmt}"));
                }
            }

            File.Delete(extracted);
        }
        catch
        {
            try { File.Delete(extracted); } catch { }
        }
    }

    Console.WriteLine($"{opened} GARC abiertos, {found.Count} laminas de 64x64 o mas");
    Console.WriteLine();

    foreach (var group in found.GroupBy(f => f.File).OrderByDescending(g => g.Count()))
    {
        var sizes = group.Select(f => $"{f.W}x{f.H}").Distinct().OrderBy(s => s, StringComparer.Ordinal);
        Console.WriteLine($"    {group.Key,-10} {group.Count(),4} laminas   {string.Join(" ", sizes.Take(8))}");
    }

    static byte[] Decompress(byte[] data)
    {
        if (data.Length == 0 || data[0] != 0x11)
        {
            return data;
        }

        try
        {
            using var output = new MemoryStream();
            LZSS.Decompress(new MemoryStream(data), data.Length, output);
            return output.ToArray();
        }
        catch
        {
            return data;
        }
    }
}

// Vuelca a PNG las laminas de un GARC, para poder MIRARLAS. Un barrido dice que hay 1157 dibujos
// de 512x256; solo abrirlos dice si son el mapa de Alola o el fondo de la Pokedex.
void MapaVolcar(string romfsPath, int from, int count)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        Console.WriteLine($"No pude extraer {romfsPath}");
        return;
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));
    Console.WriteLine($"{romfsPath}: {garc.FileCount} subficheros");
    var outputDir = Path.Combine(temp, "png", romfsPath.Replace('/', '_'));
    Directory.CreateDirectory(outputDir);

    var written = 0;

    for (var i = from; i < Math.Min(garc.FileCount, from + count); i++)
    {
        try
        {
            var data = garc.GetFile(i);

            if (data.Length > 0 && data[0] == 0x11)
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }

            var texture = BflimTexture.Decode(data);

            if (texture.Width < 64 || texture.Height < 64)
            {
                continue;
            }

            var png = Path.Combine(outputDir, $"{i:0000}_{texture.Width}x{texture.Height}.png");
            File.WriteAllBytes(png, PngImage.Encode(texture.Pixels, texture.Width, texture.Height));
            written++;
        }
        catch
        {
            // Un subfichero que no es una lamina no es un fallo: el GARC mezcla cosas.
        }
    }

    Console.WriteLine($"{written} PNG en {outputDir}");
}

// Cose las laminas de un GARC en una sola imagen, en el orden en que estan. Es lo que dice si un
// monton de piezas de 128x64 son un mapa troceado y con que anchura se recompone.
void MapaCoser(string romfsPath, int columns, int from, int count)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        Console.WriteLine($"No pude extraer {romfsPath}");
        return;
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));
    var tiles = new List<BflimTexture>();

    for (var i = from; i < Math.Min(garc.FileCount, from + count); i++)
    {
        try
        {
            var data = garc.GetFile(i);

            if (data.Length > 0 && data[0] == 0x11)
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }

            tiles.Add(BflimTexture.Decode(data));
        }
        catch
        {
            // Un subfichero que no es lamina no rompe el cosido: se salta.
        }
    }

    if (tiles.Count == 0)
    {
        Console.WriteLine("Ninguna lamina que coser.");
        return;
    }

    var tileWidth = tiles[0].Width;
    var tileHeight = tiles[0].Height;
    var rows = (tiles.Count + columns - 1) / columns;
    var width = columns * tileWidth;
    var height = rows * tileHeight;
    var canvas = new byte[width * height * 4];

    for (var t = 0; t < tiles.Count; t++)
    {
        var tile = tiles[t];

        if (tile.Width != tileWidth || tile.Height != tileHeight)
        {
            continue;
        }

        var originX = (t % columns) * tileWidth;
        var originY = (t / columns) * tileHeight;

        for (var y = 0; y < tileHeight; y++)
        {
            for (var x = 0; x < tileWidth; x++)
            {
                var source = ((y * tileWidth) + x) * 4;
                var target = (((originY + y) * width) + originX + x) * 4;
                Array.Copy(tile.Pixels, source, canvas, target, 4);
            }
        }
    }

    var path = Path.Combine(temp, $"cosido_{romfsPath.Replace('/', '_')}_{columns}c_{from}.png");
    File.WriteAllBytes(path, PngImage.Encode(canvas, width, height));
    Console.WriteLine($"{tiles.Count} laminas cosidas en {width}x{height}: {path}");
}

// Deduce la anchura de cada mapa contando costuras. Dos piezas contiguas comparten borde, asi que
// si un mapa tiene w columnas, la pieza i y la i+w encajan por arriba y por abajo. Se prueba cada
// anchura y gana la que menos salto deja: es una medida, no un tanteo a ojo.
void MapaMedir(string romfsPath, int from, int count)
{
    var tiles = LoadTiles(romfsPath, from, count);

    if (tiles.Count < 4)
    {
        Console.WriteLine("Muy pocas laminas para medir.");
        return;
    }

    Console.WriteLine($"{tiles.Count} laminas de {tiles[0].Width}x{tiles[0].Height}");
    Console.WriteLine();
    Console.WriteLine("Salto medio entre la fila de abajo de i y la de arriba de i+w:");

    for (var w = 1; w <= 20; w++)
    {
        double total = 0;
        var pairs = 0;

        for (var i = 0; i + w < tiles.Count; i++)
        {
            total += VerticalSeam(tiles[i], tiles[i + w]);
            pairs++;
        }

        if (pairs > 0)
        {
            Console.WriteLine($"    w={w,2}  {total / pairs,8:F2}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Salto lateral entre i e i+1 (un pico marca final de fila o de mapa):");

    for (var i = 0; i + 1 < Math.Min(tiles.Count, 40); i++)
    {
        Console.WriteLine($"    {from + i,4} -> {from + i + 1,4}  {HorizontalSeam(tiles[i], tiles[i + 1]),8:F2}");
    }
}

List<BflimTexture> LoadTiles(string romfsPath, int from, int count)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        return [];
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));
    var tiles = new List<BflimTexture>();

    for (var i = from; i < Math.Min(garc.FileCount, from + count); i++)
    {
        try
        {
            var data = garc.GetFile(i);

            if (data.Length > 0 && data[0] == 0x11)
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }

            tiles.Add(BflimTexture.Decode(data));
        }
        catch
        {
            // Lo que no es lamina no cuenta como pieza del mapa.
        }
    }

    return tiles;
}

// Diferencia media por canal entre la ultima fila de arriba y la primera de abajo.
double VerticalSeam(BflimTexture above, BflimTexture below)
{
    if (above.Width != below.Width)
    {
        return 999;
    }

    double total = 0;

    for (var x = 0; x < above.Width; x++)
    {
        var a = (((above.Height - 1) * above.Width) + x) * 4;
        var b = x * 4;

        for (var channel = 0; channel < 3; channel++)
        {
            total += Math.Abs(above.Pixels[a + channel] - below.Pixels[b + channel]);
        }
    }

    return total / (above.Width * 3);
}

double HorizontalSeam(BflimTexture left, BflimTexture right)
{
    if (left.Height != right.Height)
    {
        return 999;
    }

    double total = 0;

    for (var y = 0; y < left.Height; y++)
    {
        var a = ((y * left.Width) + left.Width - 1) * 4;
        var b = (y * right.Width) * 4;

        for (var channel = 0; channel < 3; channel++)
        {
            total += Math.Abs(left.Pixels[a + channel] - right.Pixels[b + channel]);
        }
    }

    return total / (left.Height * 3);
}

// Con la anchura ya medida, busca donde acaba un mapa y empieza el siguiente: entre dos filas del
// mismo mapa el salto es pequeno, y en el corte se dispara.
void MapaSegmentar(string romfsPath, int columns, double threshold)
{
    var tiles = LoadTiles(romfsPath, 0, 5000);
    var rows = tiles.Count / columns;

    Console.WriteLine($"{tiles.Count} laminas, {rows} filas de {columns}");
    Console.WriteLine();

    var cuts = new List<int> { 0 };

    for (var row = 0; row + 1 < rows; row++)
    {
        double total = 0;

        for (var c = 0; c < columns; c++)
        {
            total += VerticalSeam(tiles[(row * columns) + c], tiles[((row + 1) * columns) + c]);
        }

        if (total / columns > threshold)
        {
            cuts.Add(row + 1);
        }
    }

    cuts.Add(rows);

    Console.WriteLine($"{cuts.Count - 1} mapas encontrados (umbral {threshold}):");

    for (var i = 0; i + 1 < cuts.Count; i++)
    {
        var height = cuts[i + 1] - cuts[i];
        Console.WriteLine($"    mapa {i,3}: laminas {cuts[i] * columns,4}..{(cuts[i + 1] * columns) - 1,4}"
            + $"  {columns}x{height}  =  {columns * 128}x{height * 64}");
    }
}

// Talla BFLIM incrustados dentro de otros ficheros. El barrido anterior solo miraba subficheros que
// SON un BFLIM, y las pantallas del juego son ALYT con las imagenes dentro: por ahi se le escapo
// todo lo que dibuja una pantalla. Es la tecnica del §61, con su misma trampa: buscar las letras
// FLIM a secas da falsos positivos dentro de los pixeles de otras imagenes, asi que ademas se exige
// la marca FEFF, el bloque imag, y que el tamano declarado quepa donde dice.
void MapaTallar(int minimum)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var found = new List<(string File, int Sub, int Offset, int W, int H)>();
    var scanned = 0;

    foreach (var candidate in reader.Files.Values
        .Where(f => f.Size is > 4096 and < 40_000_000)
        .OrderBy(f => f.Path, StringComparer.Ordinal))
    {
        var extracted = Path.Combine(temp, candidate.Path.Replace('/', '_'));

        try
        {
            if (!File.Exists(extracted) && !reader.ExtractTo(candidate.Path, extracted))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(extracted);

            if (bytes.Length < 8 || bytes[0] != 'C' || bytes[1] != 'R' || bytes[2] != 'A' || bytes[3] != 'G')
            {
                File.Delete(extracted);
                continue;
            }

            var garc = new GARC.MemGARC(bytes);
            scanned++;

            for (var i = 0; i < garc.FileCount; i++)
            {
                byte[] data;

                try
                {
                    data = garc.GetFile(i);
                }
                catch
                {
                    continue;
                }

                if (data.Length > 0 && data[0] == 0x11)
                {
                    try
                    {
                        using var output = new MemoryStream();
                        LZSS.Decompress(new MemoryStream(data), data.Length, output);
                        data = output.ToArray();
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Un BFLIM que ya es el subfichero entero lo vio el barrido anterior; aqui interesa
                // lo que va DENTRO, asi que se busca a partir del byte 1.
                for (var at = 1; at + 0x28 <= data.Length; at++)
                {
                    if (data[at] != 'F' || data[at + 1] != 'L' || data[at + 2] != 'I' || data[at + 3] != 'M')
                    {
                        continue;
                    }

                    if (data[at + 4] != 0xFF || data[at + 5] != 0xFE)
                    {
                        continue;
                    }

                    if (data[at + 0x14] != 'i' || data[at + 0x15] != 'm' ||
                        data[at + 0x16] != 'a' || data[at + 0x17] != 'g')
                    {
                        continue;
                    }

                    int w = BitConverter.ToUInt16(data, at + 0x1C);
                    int h = BitConverter.ToUInt16(data, at + 0x1E);
                    var declared = (int)BitConverter.ToUInt32(data, at + 0x0C);

                    if (w < minimum || h < minimum || declared <= 0x28 || declared > at + 0x28)
                    {
                        continue;
                    }

                    found.Add((candidate.Path, i, at + 0x28 - declared, w, h));
                }
            }

            File.Delete(extracted);
        }
        catch
        {
            try { File.Delete(extracted); } catch { }
        }
    }

    Console.WriteLine($"{scanned} GARC mirados, {found.Count} laminas incrustadas de {minimum}px o mas");
    Console.WriteLine();

    foreach (var group in found.GroupBy(f => f.File).OrderByDescending(g => g.Count()))
    {
        var sizes = group.Select(f => $"{f.W}x{f.H}").Distinct().OrderBy(s => s, StringComparer.Ordinal);
        Console.WriteLine($"    {group.Key,-10} {group.Count(),4}   {string.Join(" ", sizes.Take(10))}");
    }
}

// Talla a PNG las laminas incrustadas de UN fichero, para poder mirarlas.
void MapaTallarPng(string romfsPath, int minimum)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        Console.WriteLine($"No pude extraer {romfsPath}");
        return;
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));
    var outputDir = Path.Combine(temp, "tallado", romfsPath.Replace('/', '_'));
    Directory.CreateDirectory(outputDir);

    var written = 0;

    for (var i = 0; i < garc.FileCount; i++)
    {
        byte[] data;

        try
        {
            data = garc.GetFile(i);
        }
        catch
        {
            continue;
        }

        if (data.Length > 0 && data[0] == 0x11)
        {
            try
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }
            catch
            {
                continue;
            }
        }

        for (var at = 0; at + 0x28 <= data.Length; at++)
        {
            if (data[at] != 'F' || data[at + 1] != 'L' || data[at + 2] != 'I' || data[at + 3] != 'M') continue;
            if (data[at + 4] != 0xFF || data[at + 5] != 0xFE) continue;
            if (data[at + 0x14] != 'i' || data[at + 0x15] != 'm' ||
                data[at + 0x16] != 'a' || data[at + 0x17] != 'g') continue;

            int w = BitConverter.ToUInt16(data, at + 0x1C);
            int h = BitConverter.ToUInt16(data, at + 0x1E);
            var declared = (int)BitConverter.ToUInt32(data, at + 0x0C);

            if (w < minimum || h < minimum || declared <= 0x28 || declared > at + 0x28) continue;

            try
            {
                var start = at + 0x28 - declared;
                var texture = BflimTexture.Decode(data.AsSpan(start, declared));
                var clear = 0;
                for (var p = 3; p < texture.Pixels.Length; p += 4) if (texture.Pixels[p] < 8) clear++;
                Console.WriteLine("    sub " + i + " " + w + "x" + h + " " + texture.Format
                    + " transparente " + (100.0 * clear / (w * h)).ToString("F1") + "%");
                var png = Path.Combine(outputDir, $"{i:0000}_{start:X6}_{w}x{h}.png");
                File.WriteAllBytes(png, PngImage.Encode(texture.Pixels, texture.Width, texture.Height));
                written++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    {i} @{at:X6} {w}x{h}: {ex.Message}");
            }
        }
    }

    Console.WriteLine($"{written} PNG en {outputDir}");
}

// Busca piezas con transparencia. Una vista de isla se recorta contra el fondo, asi que su mar es
// transparente; un mapa de area tiene el agua pintada de azul y es opaco entero. Es la forma de
// encontrar las vistas de isla sin mirar 866 laminas una a una.
void MapaTransparencia(string romfsPath, double minimum)
{
    var tiles = LoadTiles(romfsPath, 0, 5000);
    Console.WriteLine($"{tiles.Count} laminas");

    var runs = new List<(int First, int Last)>();
    var start = -1;

    for (var i = 0; i < tiles.Count; i++)
    {
        var clear = 0;

        for (var p = 3; p < tiles[i].Pixels.Length; p += 4)
        {
            if (tiles[i].Pixels[p] < 8) clear++;
        }

        var fraction = (double)clear / (tiles[i].Width * tiles[i].Height);

        if (fraction >= minimum)
        {
            if (start < 0) start = i;
        }
        else if (start >= 0)
        {
            runs.Add((start, i - 1));
            start = -1;
        }
    }

    if (start >= 0)
    {
        runs.Add((start, tiles.Count - 1));
    }

    Console.WriteLine($"{runs.Count} tramos con al menos {minimum:P0} transparente:");

    foreach (var run in runs)
    {
        Console.WriteLine($"    {run.First,4}..{run.Last,4}  ({run.Last - run.First + 1} laminas)");
    }
}

// Busca vistas de isla por todo el RomFS: una lamina grande, recortada -- o sea con una buena parte
// transparente -- es la forma que tiene un mapa de isla y no la tiene ningun mapa de area, que va
// pintado hasta el borde. Junta el tallado de dentro de los ALYT con la prueba de transparencia.
void MapaIslas(int minimum, double clearAtLeast)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    var outputDir = Path.Combine(temp, "islas");
    Directory.CreateDirectory(outputDir);

    var hits = 0;

    foreach (var candidate in reader.Files.Values
        .Where(f => f.Size is > 4096 and < 40_000_000)
        .OrderBy(f => f.Path, StringComparer.Ordinal))
    {
        var extracted = Path.Combine(temp, candidate.Path.Replace('/', '_'));

        try
        {
            if (!File.Exists(extracted) && !reader.ExtractTo(candidate.Path, extracted))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(extracted);

            if (bytes.Length < 8 || bytes[0] != 'C' || bytes[1] != 'R' || bytes[2] != 'A' || bytes[3] != 'G')
            {
                File.Delete(extracted);
                continue;
            }

            var garc = new GARC.MemGARC(bytes);

            for (var i = 0; i < garc.FileCount; i++)
            {
                byte[] data;

                try { data = garc.GetFile(i); } catch { continue; }

                if (data.Length > 0 && data[0] == 0x11)
                {
                    try
                    {
                        using var output = new MemoryStream();
                        LZSS.Decompress(new MemoryStream(data), data.Length, output);
                        data = output.ToArray();
                    }
                    catch { continue; }
                }

                for (var at = 0; at + 0x28 <= data.Length; at++)
                {
                    if (data[at] != 'F' || data[at + 1] != 'L' || data[at + 2] != 'I' || data[at + 3] != 'M') continue;
                    if (data[at + 4] != 0xFF || data[at + 5] != 0xFE) continue;
                    if (data[at + 0x14] != 'i' || data[at + 0x15] != 'm' ||
                        data[at + 0x16] != 'a' || data[at + 0x17] != 'g') continue;

                    int w = BitConverter.ToUInt16(data, at + 0x1C);
                    int h = BitConverter.ToUInt16(data, at + 0x1E);
                    var declared = (int)BitConverter.ToUInt32(data, at + 0x0C);

                    if (w < minimum || h < minimum || declared <= 0x28 || declared > at + 0x28) continue;

                    try
                    {
                        var texture = BflimTexture.Decode(data.AsSpan(at + 0x28 - declared, declared));

                        if (texture.Format is BflimFormat.A4 or BflimFormat.L4)
                        {
                            continue;
                        }

                        var clear = 0;

                        for (var p = 3; p < texture.Pixels.Length; p += 4)
                        {
                            if (texture.Pixels[p] < 8) clear++;
                        }

                        var fraction = (double)clear / (texture.Width * texture.Height);

                        if (fraction < clearAtLeast)
                        {
                            continue;
                        }

                        var name = $"{candidate.Path.Replace('/', '_')}_{i:0000}_{w}x{h}_{fraction:P0}.png"
                            .Replace("%", "pc").Replace(" ", "");
                        File.WriteAllBytes(Path.Combine(outputDir, name),
                            PngImage.Encode(texture.Pixels, texture.Width, texture.Height));

                        Console.WriteLine($"    {candidate.Path,-10} sub {i,4}  {w}x{h}  {fraction:P0} transparente");
                        hits++;
                    }
                    catch
                    {
                        // Lo que no decodifica no es una isla: se pasa.
                    }
                }
            }

            File.Delete(extracted);
        }
        catch
        {
            try { File.Delete(extracted); } catch { }
        }
    }

    Console.WriteLine($"{hits} candidatas en {outputDir}");
}

// Enseña la forma de un GARC que no lleva imágenes: cuántos subficheros, de qué tamaño y con qué
// empiezan. Es el primer paso para saber si un fichero es una tabla y de qué.
void MapaDatos(string romfsPath, int show)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    Directory.CreateDirectory(temp);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        Console.WriteLine($"No pude extraer {romfsPath}");
        return;
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));
    Console.WriteLine($"{romfsPath}: {garc.FileCount} subficheros");

    var sizes = new List<int>();

    for (var i = 0; i < garc.FileCount; i++)
    {
        try { sizes.Add(garc.GetFile(i).Length); } catch { sizes.Add(-1); }
    }

    foreach (var group in sizes.Where(s => s >= 0).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(10))
    {
        Console.WriteLine($"    {group.Count(),5} subficheros de {group.Key,9:N0} bytes");
    }

    Console.WriteLine();

    for (var i = 0; i < Math.Min(garc.FileCount, show); i++)
    {
        byte[] data;
        try { data = garc.GetFile(i); } catch { continue; }

        if (data.Length > 0 && data[0] == 0x11)
        {
            try
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }
            catch { }
        }

        var head = string.Join(" ", data.Take(24).Select(b => b.ToString("X2")));
        var text = new string(data.Take(32).Select(b => b >= 32 && b < 127 ? (char)b : (char)46).ToArray());
        Console.WriteLine($"    {i,4}  {data.Length,8:N0}  {head}  |{text}|");
    }
}

// Escribe a disco los subficheros ya descomprimidos, para poder mirarlos con otras herramientas.
void MapaDatosVolcar(string romfsPath)
{
    var reader = new RomFsReader(RequireRom());
    var temp = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    var outputDir = Path.Combine(temp, "datos", romfsPath.Replace('/', '_'));
    Directory.CreateDirectory(outputDir);

    var extracted = Path.Combine(temp, romfsPath.Replace('/', '_'));

    if (!File.Exists(extracted) && !reader.ExtractTo(romfsPath, extracted))
    {
        Console.WriteLine($"No pude extraer {romfsPath}");
        return;
    }

    var garc = new GARC.MemGARC(File.ReadAllBytes(extracted));

    for (var i = 0; i < garc.FileCount; i++)
    {
        byte[] data;
        try { data = garc.GetFile(i); } catch { continue; }

        if (data.Length > 0 && data[0] == 0x11)
        {
            try
            {
                using var output = new MemoryStream();
                LZSS.Decompress(new MemoryStream(data), data.Length, output);
                data = output.ToArray();
            }
            catch { }
        }

        File.WriteAllBytes(Path.Combine(outputDir, $"{i:0000}.bin"), data);
    }

    Console.WriteLine($"{garc.FileCount} subficheros en {outputDir}");
}

// Busca en un fichero de datos rastros de lo que ya conocemos: referencias a las 866 piezas del
// mapa, a las 336 areas de encdata, o coordenadas en coma flotante. Es la forma de decidir si una
// tabla desconocida habla de lo que nos interesa antes de gastar horas en su formato.
void MapaPistas(string folder)
{
    foreach (var path in Directory.EnumerateFiles(folder, "*.bin").OrderBy(p => p, StringComparer.Ordinal))
    {
        var data = File.ReadAllBytes(path);

        var floats = 0;
        var tileRefs = 0;
        var areaRefs = 0;
        var runs = 0;
        var previous = -1;

        for (var i = 0; i + 4 <= data.Length; i += 4)
        {
            var raw = BitConverter.ToSingle(data, i);

            if (float.IsFinite(raw) && Math.Abs(raw) is > 0.01f and < 100000f)
            {
                floats++;
            }
        }

        for (var i = 0; i + 2 <= data.Length; i += 2)
        {
            int value = BitConverter.ToUInt16(data, i);

            if (value < 866) tileRefs++;
            if (value < 336) areaRefs++;

            // Una tabla de indices suele ir en cuesta: 0,1,2,... o al menos creciendo.
            if (value < 866 && value == previous + 1) runs++;
            previous = value < 866 ? value : -1;
        }

        var words = Math.Max(1, data.Length / 2);
        Console.WriteLine($"    {Path.GetFileName(path)}  {data.Length,8:N0} bytes"
            + $"   floats {floats * 4 * 100 / Math.Max(1, data.Length),3}%"
            + $"   <866 {tileRefs * 100 / words,3}%"
            + $"   <336 {areaRefs * 100 / words,3}%"
            + $"   cuestas {runs,6}");
    }
}

// El perfil de saltos entre filas, para elegir el corte con criterio en vez de a ojo.
void MapaPerfil(string romfsPath, int columns)
{
    var tiles = LoadTiles(romfsPath, 0, 5000);
    var rows = tiles.Count / columns;
    var costs = new List<double>();

    for (var row = 0; row + 1 < rows; row++)
    {
        double total = 0;

        for (var c = 0; c < columns; c++)
        {
            total += VerticalSeam(tiles[(row * columns) + c], tiles[((row + 1) * columns) + c]);
        }

        costs.Add(total / columns);
    }

    Console.WriteLine($"{costs.Count} saltos entre filas");
    Console.WriteLine();

    foreach (var threshold in new[] { 20, 30, 40, 50, 60, 70, 80, 90, 100, 120, 140 })
    {
        var over = costs.Count(c => c > threshold);
        Console.WriteLine($"    umbral {threshold,3}  ->  {over,4} cortes, {over + 1,4} mapas");
    }

    Console.WriteLine();
    Console.WriteLine("Los veinte saltos mayores, con su fila:");

    foreach (var top in costs.Select((c, i) => (Cost: c, Row: i))
        .OrderByDescending(x => x.Cost).Take(20).OrderBy(x => x.Row))
    {
        Console.WriteLine($"    fila {top.Row,4} -> {top.Row + 1,4}   {top.Cost,7:F1}");
    }
}

// Exporta cada mapa de area como un PNG, para que una persona que conozca el juego les ponga
// nombre. Es lo unico que falta para poder usar el arte real del cartucho: las piezas se cosen
// solas, pero QUE SITIO es cada mapa no lo dice el cartucho en ningun lado que se haya encontrado.
//
// El corte va en dos pasadas a proposito. Un umbral unico no vale: alto fusiona dos sitios en una
// imagen y bajo parte una fila de agua en dos. Primero se cortan los saltos fuertes y despues, si
// un trozo sigue siendo demasiado alto para ser un mapa, se vuelve a partir por su mayor salto
// interno. Y ante la duda se corta de mas: dos mitades del mismo sitio se nombran igual y se
// arreglan, mientras que dos sitios en una imagen no hay forma de nombrarlos.
void MapaExportar(string romfsPath, int _, string destination)
{
    var tiles = LoadTiles(romfsPath, 0, 5000);

    // Cada mapa trae SU anchura, y eso costo cuatro intentos descubrirlo: todo el corte anterior
    // daba por hecho cuatro columnas para las 866 piezas porque el primer mapa las tiene, y en la
    // pieza 256 gana cinco -- 31,9 de salto contra 48,2 de cuatro--. Un mapa de cinco cosido a
    // cuatro sale en diagonal y no hay umbral que arregle eso.
    //
    // Asi que se buscan las dos cosas a la vez y de izquierda a derecha: en cada posicion se elige
    // la anchura que mejor encaja y despues se alarga el mapa mientras las filas sigan encajando.
    var maps = new List<(int First, int Width, int Height)>();
    var at = 0;

    while (at < tiles.Count)
    {
        var left = tiles.Count - at;
        var bestWidth = Math.Min(4, left);
        var bestScore = double.MaxValue;

        for (var w = 2; w <= 8; w++)
        {
            if (w > left)
            {
                continue;
            }

            var score = Fit(tiles, at, w, Math.Max(1, Math.Min(3, left / w)));

            if (score < bestScore)
            {
                bestScore = score;
                bestWidth = w;
            }
        }

        // Y ahora cuanto dura: se anaden filas mientras la que viene encaje tan bien como las que
        // ya hay. El «tan bien como» es relativo al propio mapa a proposito -- un mapa de desierto
        // tiene saltos altos en todas sus filas y uno de mar los tiene bajos, asi que una cifra
        // fija cortaria el desierto en tiras y no cortaria el mar nunca.
        // Empieza en UNA fila, no en dos: de la pieza 366 en adelante los mapas son franjas anchas
        // y bajas, y exigir dos filas hacia que el algoritmo tragase dos mapas de una vez o se
        // inventase una anchura absurda para que le cuadrasen.
        var height = 1;
        double inside = -1;

        // Con tope: un mapa no mide sesenta filas, y sin el tope el algoritmo se quedaba con una
        // anchura mala y engullia la mitad del cartucho en un solo bloque. Al toparlo vuelve a
        // elegir anchura, que es la decision que hay que darle otra oportunidad de acertar.
        while ((height + 1) * bestWidth <= left && height < 12)
        {
            var next = RowSeam(tiles, at, bestWidth, height - 1);
            var limit = inside < 0 ? 30 : Math.Max(28, inside * 2.0);

            if (next > limit)
            {
                break;
            }

            inside = inside < 0 ? next : ((inside * (height - 1)) + next) / height;
            height++;
        }

        if (bestWidth * height > left)
        {
            height = left / bestWidth;
        }

        if (height < 1)
        {
            break;
        }

        maps.Add((at, bestWidth, height));
        at += bestWidth * height;
    }

    Directory.CreateDirectory(destination);

    foreach (var stale in Directory.EnumerateFiles(destination, "*.png"))
    {
        File.Delete(stale);
    }

    var written = 0;

    foreach (var map in maps)
    {
        var tileWidth = tiles[0].Width;
        var tileHeight = tiles[0].Height;
        var width = map.Width * tileWidth;
        var canvas = new byte[width * map.Height * tileHeight * 4];

        for (var row = 0; row < map.Height; row++)
        {
            for (var c = 0; c < map.Width; c++)
            {
                var tile = tiles[map.First + (row * map.Width) + c];

                for (var y = 0; y < tile.Height; y++)
                {
                    var source = y * tile.Width * 4;
                    var target = ((((row * tileHeight) + y) * width) + (c * tileWidth)) * 4;
                    Array.Copy(tile.Pixels, source, canvas, target, tile.Width * 4);
                }
            }
        }

        written++;
        var last = map.First + (map.Width * map.Height) - 1;
        File.WriteAllBytes(
            Path.Combine(destination, $"{written:00}_{map.Width}x{map.Height}_piezas-{map.First:000}-{last:000}.png"),
            PngImage.Encode(canvas, width, map.Height * tileHeight));
    }

    Console.WriteLine($"{written} mapas en {destination}");

    foreach (var group in maps.GroupBy(m => m.Width).OrderBy(g => g.Key))
    {
        Console.WriteLine($"    {group.Count(),3} mapas de {group.Key} columnas");
    }
}

// Como de bien encaja una rejilla de anchura w empezando en «at»: mezcla el salto lateral dentro
// de cada fila con el vertical entre filas. Cuanto mas bajo, mejor encaja.
double Fit(List<BflimTexture> tiles, int at, int w, int rows)
{
    if (rows < 1)
    {
        return double.MaxValue;
    }

    double total = 0;
    var count = 0;

    for (var row = 0; row < rows; row++)
    {
        for (var c = 0; c + 1 < w; c++)
        {
            total += HorizontalSeam(tiles[at + (row * w) + c], tiles[at + (row * w) + c + 1]);
            count++;
        }
    }

    for (var row = 0; row + 1 < rows; row++)
    {
        for (var c = 0; c < w; c++)
        {
            total += VerticalSeam(tiles[at + (row * w) + c], tiles[at + ((row + 1) * w) + c]);
            count++;
        }
    }

    return count == 0 ? double.MaxValue : total / count;
}

// El salto vertical entre la fila «row» y la siguiente, dentro de un mapa que empieza en «at».
double RowSeam(List<BflimTexture> tiles, int at, int w, int row)
{
    double total = 0;

    for (var c = 0; c < w; c++)
    {
        var above = at + (row * w) + c;
        var below = above + w;

        if (below >= tiles.Count)
        {
            return double.MaxValue;
        }

        total += VerticalSeam(tiles[above], tiles[below]);
    }

    return total / w;
}

// Busca piezas de relleno: si un mapa no llena su ultima fila, lo que sobra va en negro o vacio, y
// eso marca donde acaba mucho mejor que cualquier umbral sobre las costuras.
void MapaVacias(string romfsPath)
{
    var tiles = LoadTiles(romfsPath, 0, 5000);
    var empty = new List<int>();

    for (var i = 0; i < tiles.Count; i++)
    {
        var dark = 0;

        for (var p = 0; p < tiles[i].Pixels.Length; p += 4)
        {
            if (tiles[i].Pixels[p] < 12 && tiles[i].Pixels[p + 1] < 12 && tiles[i].Pixels[p + 2] < 12)
            {
                dark++;
            }
        }

        if (dark > tiles[i].Width * tiles[i].Height * 0.9)
        {
            empty.Add(i);
        }
    }

    Console.WriteLine($"{tiles.Count} laminas, {empty.Count} practicamente negras");
    Console.WriteLine($"    {string.Join(" ", empty.Take(80))}");
    Console.WriteLine();
    Console.WriteLine("Su posicion dentro de la fila de cuatro (3 = ultima columna):");
    Console.WriteLine($"    {string.Join(" ", empty.GroupBy(i => i % 4).OrderBy(g => g.Key).Select(g => $"col {g.Key}: {g.Count()}"))}");
}

// Encuentra la FASE de una rejilla: con que pieza empieza cada fila. La anchura la dan las
// costuras verticales, pero se cumplen igual para cualquier desplazamiento -- si todas las filas
// arrancan corridas lo mismo, la pieza i y la i+w siguen siendo vecinas. Lo que delata la fase es
// la costura LATERAL: dentro de una fila las piezas encajan, y en el salto de linea no.
void MapaFase(string romfsPath, int at, int count, int width)
{
    var tiles = LoadTiles(romfsPath, at, count);
    Console.WriteLine($"{tiles.Count} laminas desde {at}, rejilla de {width}");
    Console.WriteLine();
    Console.WriteLine("Por cada fase: salto lateral DENTRO de la fila y EN el salto de linea.");
    Console.WriteLine("La buena es la que tiene el de dentro bajo y el de fuera alto.");

    for (var phase = 0; phase < width; phase++)
    {
        double inside = 0, outside = 0;
        int insideCount = 0, outsideCount = 0;

        for (var i = 0; i + 1 < tiles.Count; i++)
        {
            var seam = HorizontalSeam(tiles[i], tiles[i + 1]);

            if (((i - phase) % width + width) % width == width - 1)
            {
                outside += seam;
                outsideCount++;
            }
            else
            {
                inside += seam;
                insideCount++;
            }
        }

        var dentro = insideCount == 0 ? 0 : inside / insideCount;
        var fuera = outsideCount == 0 ? 0 : outside / outsideCount;
        Console.WriteLine($"    fase {phase}   dentro {dentro,7:F2}   fuera {fuera,7:F2}   diferencia {fuera - dentro,7:F2}");
    }
}

// Encuentra el encuadre de un mapa de isla probando por donde empieza y quedandose con el que deja
// los bordes laterales limpios de tierra.
//
// Es la medida que habia que usar desde el principio. Las costuras no valen aqui: un mapa de isla
// es casi todo oceano, y el oceano encaja consigo mismo en cualquier desplazamiento, asi que la
// fase salia con una diferencia de 5,5 contra 8,9 -- ruido-. Lo que NO se cumple por casualidad es
// que la isla quede entera dentro del marco: si el arranque esta corrido, la isla se parte y sus
// dos mitades tocan los dos bordes.
void MapaIsla(string romfsPath, int around, int width, int height, string destination,
    string? name = null)
{
    // Se busca tambien la ALTURA, y se miran los cuatro bordes. Mirando solo los laterales, Akala
    // salia con el rancho cortado por abajo: el marco estaba bien de ancho y mal de alto.
    var from = Math.Max(0, around - (width * 4));
    var tiles = LoadTiles(romfsPath, from, width * (height + 12));

    var best = -1;
    var bestHeight = height;
    var bestEdge = double.MaxValue;

    // De mayor a menor: cuando varios marcos empatan a cero -- y empatan, porque cualquier recorte
    // que caiga en mar puntua igual de bien -- gana el mas grande, que es el que no deja fuera
    // ningun trozo de isla.
    for (var h = height + 5; h >= height - 2; h--)
    {
        for (var start = 0; start + (width * h) <= tiles.Count; start++)
        {
            double edge = 0;

            for (var row = 0; row < h; row++)
            {
                edge += EdgeLand(tiles[start + (row * width)], left: true);
                edge += EdgeLand(tiles[start + (row * width) + width - 1], left: false);
            }

            for (var c = 0; c < width; c++)
            {
                edge += EdgeRowLand(tiles[start + c], top: true);
                edge += EdgeRowLand(tiles[start + ((h - 1) * width) + c], top: false);
            }

            // Por lamina de borde, para que un marco mas alto no gane solo por tener mas bordes.
            var score = edge / ((h * 2) + (width * 2));

            if (score < bestEdge)
            {
                bestEdge = score;
                best = start;
                bestHeight = h;
            }
        }
    }

    height = bestHeight;
    var span = width * height;
    var first = from + best;
    Console.WriteLine($"Mejor encuadre: empieza en la pieza {first}, {width}x{height}, "
        + $"tierra en los bordes {bestEdge:P1}");

    Directory.CreateDirectory(destination);
    var tileWidth = tiles[0].Width;
    var tileHeight = tiles[0].Height;
    var canvasWidth = width * tileWidth;
    var canvas = new byte[canvasWidth * height * tileHeight * 4];

    for (var row = 0; row < height; row++)
    {
        for (var c = 0; c < width; c++)
        {
            var tile = tiles[best + (row * width) + c];

            for (var y = 0; y < tile.Height; y++)
            {
                Array.Copy(tile.Pixels, y * tile.Width * 4, canvas,
                    ((((row * tileHeight) + y) * canvasWidth) + (c * tileWidth)) * 4, tile.Width * 4);
            }
        }
    }

    // Y se recorta a la isla, con su margen. Un mapa del cartucho es en su mayor parte oceano
    // vacio -- Poni ocupa menos de la mitad de su lamina-, asi que sin recortar la isla sale
    // pequena y descentrada por mucho que el marco sea correcto.
    var cropped = CropToLand(canvas, canvasWidth, height * tileHeight, 24,
        out var croppedWidth, out var croppedHeight);

    var path = Path.Combine(destination,
        name is null ? $"isla_{first:000}_{width}x{height}.png" : $"{name}.png");
    File.WriteAllBytes(path, PngImage.Encode(cropped, croppedWidth, croppedHeight));
    Console.WriteLine($"    {path}");
}

// Cuanta tierra toca el borde de una pieza. El oceano del mapa es un azul muy saturado y bastante
// plano, asi que se cuenta lo que NO lo es.
double EdgeLand(BflimTexture tile, bool left)
{
    var land = 0;

    for (var y = 0; y < tile.Height; y++)
    {
        var at = ((y * tile.Width) + (left ? 0 : tile.Width - 1)) * 4;
        int r = tile.Pixels[at], g = tile.Pixels[at + 1], b = tile.Pixels[at + 2];

        if (!(b > 120 && b > r + 60 && b > g + 40))
        {
            land++;
        }
    }

    return (double)land / tile.Height;
}

// Lo mismo para el borde de arriba o el de abajo de una pieza.
double EdgeRowLand(BflimTexture tile, bool top)
{
    var land = 0;

    for (var x = 0; x < tile.Width; x++)
    {
        var at = (((top ? 0 : tile.Height - 1) * tile.Width) + x) * 4;
        int r = tile.Pixels[at], g = tile.Pixels[at + 1], b = tile.Pixels[at + 2];

        if (!(b > 120 && b > r + 60 && b > g + 40))
        {
            land++;
        }
    }

    return (double)land / tile.Width;
}

// Saca los cuatro mapas de isla del cartucho, ya encuadrados y con su nombre.
//
// Las piezas por las que empieza cada uno estan MEDIDAS -- son las que la segmentacion encontro y
// el encuadre afino-, no elegidas. Y el nombre de cada isla se leyo de sus propios accidentes:
// Malie amurallada con su jardin, el Lanakila nevado y el desierto de Haina no son Melemele.
void MapaCuatro(string _, string destination)
{
    Directory.CreateDirectory(destination);
    var scratch = Path.Combine(Path.GetTempPath(), "permalocke-mapa");
    var reader = IslandMapReader.Open(RequireRom(), scratch);

    foreach (var island in reader.ReadAll())
    {
        var path = Path.Combine(destination, $"{island.Name}.png");
        File.WriteAllBytes(path, PngImage.Encode(island.Pixels, island.Width, island.Height));
        Console.WriteLine($"    {island.Name,-10} {island.Width}x{island.Height}  {path}");
    }
}

// Recorta una lamina a lo que no es oceano, dejando un margen. Es lo que centra la isla: el marco
// del cartucho la deja en una esquina rodeada de mar, porque ese mar es donde el juego dibuja las
// otras islas cuando te alejas.
byte[] CropToLand(byte[] pixels, int width, int height, int margin, out int outWidth, out int outHeight)
{
    int minX = width, minY = height, maxX = -1, maxY = -1;

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var at = ((y * width) + x) * 4;
            int r = pixels[at], g = pixels[at + 1], b = pixels[at + 2];

            if (b > 120 && b > r + 60 && b > g + 40)
            {
                continue;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
    }

    if (maxX < 0)
    {
        outWidth = width;
        outHeight = height;
        return pixels;
    }

    minX = Math.Max(0, minX - margin);
    minY = Math.Max(0, minY - margin);
    maxX = Math.Min(width - 1, maxX + margin);
    maxY = Math.Min(height - 1, maxY + margin);

    outWidth = maxX - minX + 1;
    outHeight = maxY - minY + 1;
    var output = new byte[outWidth * outHeight * 4];

    for (var y = 0; y < outHeight; y++)
    {
        Array.Copy(pixels, (((minY + y) * width) + minX) * 4,
            output, y * outWidth * 4, outWidth * 4);
    }

    return output;
}

// A partir de que punto los entrenadores llevan los EV al maximo. La pregunta importa para un
// Nuzlocke: un rival con 252 en dos estadisticas pega bastante mas de lo que su nivel sugiere, y
// saber donde empieza eso dice donde hay que dejar de improvisar.
//
// El EV vive en los bytes 0x2 a 0x7 de cada entrada de trpoke, uno por estadistica.
async Task EntrenadoresEvAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);
    var species = workspace.Config.GetText(TextName.SpeciesNames);

    var parties = new GARC.LazyGARC(
        await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.TrainerPokemon)));
    using var trainers = new GarcPatcher(workspace.PathOf(GameFiles.TrainerData));

    var rows = new List<(int Id, int Class, string Name, int Level, int Total, int Best, int Count)>();

    for (var trainer = 0; trainer < Math.Min(trainers.FileCount, parties.FileCount); trainer++)
    {
        var entry = trainers.Read(trainer);

        if (entry.Length < 0x14)
        {
            continue;
        }

        var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
        var count = entry[ExtraPokemonRandomizer.CountOffset];
        var party = parties[trainer];

        if (count == 0 || party.Length != count * TrainerPokemonTable.EntrySize)
        {
            continue;
        }

        var level = 0;
        var total = 0;
        var best = 0;

        for (var i = 0; i < count; i++)
        {
            var at = i * TrainerPokemonTable.EntrySize;
            var sum = 0;

            for (var stat = 0; stat < 6; stat++)
            {
                var ev = party[at + 2 + stat];
                sum += ev;
                if (ev > best) best = ev;
            }

            if (sum > total) total = sum;
            level = Math.Max(level, party[at + 0x0E]);
        }

        var name = trainer < trainerNames.Length ? trainerNames[trainer] : "?";
        rows.Add((trainer, trainerClass, name, level, total, best, count));
    }

    var withEv = rows.Where(r => r.Total > 0).ToArray();

    Console.WriteLine($"{rows.Count} entrenadores con equipo, {withEv.Length} con algun EV puesto");
    Console.WriteLine();

    Console.WriteLine("Reparto del EV mas alto de una sola estadistica:");
    foreach (var group in rows.GroupBy(r => r.Best).OrderBy(g => g.Key))
    {
        Console.WriteLine($"    EV maximo {group.Key,3}: {group.Count(),4} entrenadores");
    }

    if (withEv.Length == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Ninguno lleva EV. En este cartucho no existe ese punto.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Por nivel del entrenador, cuantos llevan EV:");

    foreach (var band in rows.GroupBy(r => r.Level / 10).OrderBy(g => g.Key))
    {
        var some = band.Count(r => r.Total > 0);
        Console.WriteLine($"    Nv {band.Key * 10,2}-{(band.Key * 10) + 9,2}: "
            + $"{some,4} de {band.Count(),4} llevan EV");
    }

    Console.WriteLine();
    Console.WriteLine("Por CLASE de entrenador, cuantos de los suyos llevan EV:");

    foreach (var group in rows.GroupBy(r => r.Class)
        .Select(g => new
        {
            Class = g.Key,
            Total = g.Count(),
            With = g.Count(r => r.Total > 0),
            Level = g.Min(r => r.Level)
        })
        .Where(g => g.With > 0)
        .OrderByDescending(g => (double)g.With / g.Total)
        .ThenByDescending(g => g.Total))
    {
        var name = group.Class < classNames.Length ? classNames[group.Class] : $"clase {group.Class}";
        Console.WriteLine($"    [{group.Class,3}] {name,-24} {group.With,3} de {group.Total,3}"
            + $"   ({group.With * 100 / group.Total,3}%)  nivel minimo {group.Level}");
    }

    Console.WriteLine();
    Console.WriteLine("Reparto total de EV por Pokemon (cuantas estadisticas a 252):");

    foreach (var group in withEv.GroupBy(r => r.Total).OrderBy(g => g.Key))
    {
        Console.WriteLine($"    {group.Key,4} EV: {group.Count(),4} entrenadores");
    }

    Console.WriteLine();
    Console.WriteLine("Los primeros veinte con EV, en orden de id (que es orden de juego):");

    foreach (var row in withEv.OrderBy(r => r.Id).Take(20))
    {
        var className = row.Class < classNames.Length ? classNames[row.Class] : $"clase {row.Class}";
        Console.WriteLine($"    id {row.Id,4}  Nv{row.Level,3}  EV total {row.Total,4} "
            + $"(mayor {row.Best,3})  {className} {row.Name}");
    }
}

// Vuelca la tabla de estaticos entera: indice, especie, forma, nivel y tipo. Es lo que hace falta
// para senalar una entrada concreta -- el Ultra Necrozma del jefe, el Lunala que se atrapa- sin
// adivinar cual es.
async Task EstaticosDumpAsync(string? filter, string? generated = null)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);

    var source = generated ?? workspace.PathOf(GameFiles.EncounterStatic);
    Console.WriteLine($"leyendo {source}");
    using var patcher = new GarcPatcher(source);

    foreach (var layout in new[]
    {
        StaticEncounterTable.Gifts, StaticEncounterTable.Statics, StaticEncounterTable.Trades
    })
    {
        var payload = patcher.Read(layout.Subfile);
        var count = StaticEncounterTable.Count(payload, layout);

        Console.WriteLine();
        Console.WriteLine($"=== {layout.Name}: {count} entradas de {layout.Stride} bytes ===");

        for (var i = 0; i < count; i++)
        {
            var species = StaticEncounterTable.GetSpecies(payload, layout, i);

            var name = species > 0 && species < names.Length ? names[species] : "??? " + species;

            if (species == 0)
            {
                continue;
            }


            if (species == 0 && filter is not null) continue;
            if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var form = StaticEncounterTable.GetForm(payload, layout, i);
            var level = layout.LevelOffset is null
                ? -1
                : StaticEncounterTable.GetLevel(payload, layout, i);
            var kind = layout == StaticEncounterTable.Statics
                ? payload[(i * layout.Stride) + StaticEncounterTable.KindOffset]
                : -1;

            Console.WriteLine($"    [{i,3}] {name,-14} especie {species,4}  forma {form,2}"
                + $"  nivel {level,3}  tipo {kind,2}");
        }
    }
}

// Cuantas entradas tiene la tabla de megas y hasta que numero llegan las que la tienen. Con el mod
// de expansion instalado la cuenta no es la del cartucho, y suponerla escribe una especie que no
// existe sin que nada falle.
async Task MegasCuentaAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var forms = MegaTrainerRandomizer.ReadForms(workspace.PathOf(GameFiles.MegaEvolution));

    using var patcher = new GarcPatcher(workspace.PathOf(GameFiles.MegaEvolution));
    Console.WriteLine($"la tabla de megas tiene {patcher.FileCount} subficheros");
    Console.WriteLine($"nombres de especie disponibles: {names.Length}");
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    Console.WriteLine($"claves con mega: {forms.Count}, de {forms.Keys.Min()} a {forms.Keys.Max()}");
    Console.WriteLine($"workspace.MaxSpecies = {workspace.MaxSpecies}");
    Console.WriteLine($"options.MaxSpecies = {options.MaxSpecies}");
    Console.WriteLine($"EffectiveMaxSpecies = {options.EffectiveMaxSpecies(workspace.MaxSpecies)}");
    Console.WriteLine($"bannedSpecies = {options.BannedSpecies.Count}");
    Console.WriteLine($"candidatas = {MegaTrainerRandomizer.Candidates(forms, options, workspace.MaxSpecies).Length}");
    Console.WriteLine();

    foreach (var key in forms.Keys.Order())
    {
        var name = key > 0 && key < names.Length ? names[key] : "??? FUERA DE LA LISTA";
        Console.WriteLine($"    {key,5}  {name,-16} formas {string.Join(",", forms[key])}");
    }
}

// Los equipos de una clase de entrenador en el mod ya generado, con el total base de cada Pokemon.
// Es la unica forma de comprobar que un suelo se aplico: el informe dice lo que quiso hacer, el
// fichero dice lo que hay.
async Task LigaAsync(ulong seed)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var classNames = workspace.Config.GetText(TextName.TrainerClasses);
    var trainerNames = workspace.Config.GetText(TextName.TrainerNames);
    var pool = SpeciesPool.FromGame(workspace.Config, options, workspace.MaxSpecies);

    var folder = Path.Combine(root, "Randomized", $"seed-{seed}", "romfs");
    var partyPath = Path.Combine(folder, GameFiles.TrainerPokemon.Replace('/', Path.DirectorySeparatorChar));
    var dataPath = Path.Combine(folder, GameFiles.TrainerData.Replace('/', Path.DirectorySeparatorChar));

    if (!File.Exists(partyPath))
    {
        Console.WriteLine($"No existe {partyPath}. Ejecuta antes: randomize {seed}");
        return;
    }

    var wanted = options.TrainerMinimums.ToDictionary(m => m.Class);
    var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath));
    using var trainers = new GarcPatcher(dataPath);

    var below = 0;
    var seen = 0;

    for (var trainer = 0; trainer < Math.Min(trainers.FileCount, parties.FileCount); trainer++)
    {
        var entry = trainers.Read(trainer);

        if (entry.Length < 0x14)
        {
            continue;
        }

        var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);

        if (!wanted.TryGetValue(trainerClass, out var rule))
        {
            continue;
        }

        var party = parties[trainer];
        var count = TrainerPokemonTable.Count(party);

        if (count == 0)
        {
            continue;
        }

        seen++;
        var line = new List<string>();
        var megas = 0;

        for (var slot = 0; slot < count; slot++)
        {
            var species = TrainerPokemonTable.GetSpecies(party, slot);
            var form = TrainerPokemonTable.GetForm(party, slot);
            var bst = pool.BaseStatTotal(species);
            var name = species < names.Length ? names[species] : $"?{species}";

            // Una forma alternativa no se juzga por el total de su forma BASE: Mega Sableye suma
            // 480 y el Sableye normal 380, asi que contarla como baja seria contar otra cosa.
            // Ese hueco lo gobierna la regla de las megas, no el suelo.
            var alternate = form > 0;

            if (alternate) megas++;
            else if (bst < rule.MinimumBaseStatTotal) below++;

            line.Add($"{name}{(alternate ? $"-{form}" : "")} {bst}"
                + (alternate ? " (forma)" : bst < rule.MinimumBaseStatTotal ? " BAJO" : ""));
        }

        Console.WriteLine($"  {classNames[trainerClass]} {trainerNames[trainer],-10} Nv"
            + $"{TrainerPokemonTable.GetLevel(party, count - 1),3}  formas>0: {megas}");
        Console.WriteLine($"      {string.Join(" | ", line)}");
    }

    Console.WriteLine();
    Console.WriteLine($"{seen} combates de la liga, {below} Pokemon de forma base por debajo del suelo");
    Console.WriteLine("Las formas alternativas no cuentan: las gobierna la regla de las megas.");
}

// Los bytes en crudo de unas entradas de estaticos, para compararlas entre si. Cuando dos filas
// tienen la misma especie y no se sabe que son, lo unico que lo dice es en que se diferencian.
async Task EstaticosCrudoAsync(int[] indices)
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var layout = StaticEncounterTable.Statics;

    using var patcher = new GarcPatcher(workspace.PathOf(GameFiles.EncounterStatic));
    var payload = patcher.Read(layout.Subfile);

    foreach (var index in indices)
    {
        var at = index * layout.Stride;
        var species = StaticEncounterTable.GetSpecies(payload, layout, index);
        var name = species > 0 && species < names.Length ? names[species] : $"?{species}";

        Console.WriteLine($"[{index,3}] {name}");

        for (var row = 0; row < layout.Stride; row += 16)
        {
            var bytes = payload.Skip(at + row).Take(Math.Min(16, layout.Stride - row));
            Console.WriteLine($"    {row:X2}: {string.Join(" ", bytes.Select(b => b.ToString("X2")))}");
        }
    }

    // Y en que se diferencian, que es la pregunta de verdad.
    if (indices.Length == 2)
    {
        Console.WriteLine();
        Console.WriteLine($"diferencias entre [{indices[0]}] y [{indices[1]}]:");

        for (var b = 0; b < layout.Stride; b++)
        {
            var a = payload[(indices[0] * layout.Stride) + b];
            var c = payload[(indices[1] * layout.Stride) + b];

            if (a != c)
            {
                Console.WriteLine($"    byte 0x{b:X2}: {a,3} vs {c,3}   (0x{a:X2} vs 0x{c:X2})");
            }
        }
    }
}

// El Shop.cro generado mas reciente, para que «shops --gen» sin ruta lea lo ultimo.
//
// Antes aqui habia una seed escrita a mano -seed-20260818-, asi que el volcado enseñaba
// tranquilamente el mod de otro dia mientras uno creia estar mirando el que acababa de generar.
// No fallaba, no avisaba: contestaba de otro fichero. Es la misma clase de mentira que los rangos
// de tier escritos a mano en el XAML del gacha, y cuesta el mismo rato descubrirla.
static string LatestGenerated(string root)
{
    var randomized = Path.Combine(root, "Randomized");

    if (!Directory.Exists(randomized))
    {
        return Path.Combine(randomized, "sin-generar", "romfs", "Shop.cro");
    }

    var newest = Directory.EnumerateDirectories(randomized, "seed-*")
        .Select(d => Path.Combine(d, "romfs", "Shop.cro"))
        .Where(File.Exists)
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault();

    return newest ?? Path.Combine(randomized, "sin-generar", "romfs", "Shop.cro");
}

// INFORMACIÓN (2026-09-24): lo que la app enseña en su sección INFORMACIÓN, sacado del juego y no escrito a mano.
// Las evoluciones: la tabla del cartucho (con el mod de gen 8-9) antes y después de pasar por el MISMO corrector que usa
// el randomizador. Las tiendas del juego: los mostradores especiales de Data/randomizer.json.
// Deja Data/informacion.json. Volver a ejecutarlo si cambia el corrector o las tiendas.
async Task InformacionAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var speciesNames = workspace.Config.GetText(TextName.SpeciesNames);
    var itemNames = workspace.Config.GetText(TextName.ItemNames);
    var typeNames = workspace.Config.GetText(TextName.Types);
    string TypeName(int type) => type >= 0 && type < typeNames.Length ? typeNames[type] : "?";
    var moveNames = workspace.Config.GetText(TextName.MoveNames);
    var options = RandomizerOptionsLoader.Load(Path.Combine(root, "Data", "randomizer.json"));

    // Fila de la tabla -> especie y forma: las formas con fila propia van al final, a partir de su FormStatsIndex.
    var personal = new GARC.LazyGARC(await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.Personal)));
    var packed = personal[personal.FileCount - 1];
    var rowOf = new Dictionary<int, (int Species, int Form)>();
    for (var s = 1; s < packed.Length / PersonalEntry7.Size; s++)
    {
        var at = s * PersonalEntry7.Size;
        rowOf.TryAdd(s, (s, 0));
        var from = PersonalEntry7.GetFormStatsIndex(packed, at);
        for (var form = 1; from > 0 && form < PersonalEntry7.GetFormCount(packed, at); form++) rowOf.TryAdd(from + form - 1, (s, form));
    }

    int[] alola = [19, 20, 26, 27, 28, 37, 38, 50, 51, 52, 53, 74, 75, 76, 88, 89, 103, 105];
    int[] galar = [77, 78, 79, 80, 83, 110, 122, 144, 145, 146, 199, 222, 263, 264, 554, 555, 562, 618];
    int[] hisui = [58, 59, 100, 101, 157, 211, 215, 503, 549, 570, 571, 628, 705, 706, 713, 724];
    string Name(int species, int form) => speciesNames[species] + (form, species) switch
    {
        (0, _) => "",
        (2, 52) => " de Galar",
        (1, 128) or (1, 194) or (2, 128) or (3, 128) => " de Paldea",
        (_, 710) => $" (tamaño {form + 1})",
        (1, 745) => " nocturno",
        (2, 745) => " crepuscular",
        (1, 744) => " (propio)",
        (1, 902) or (1, 916) => " hembra",
        (2, 550) => " raya blanca",
        (1, 982) => " de tres segmentos",
        (1, 925) => " familia de tres",
        (1, 849) => " grave",
        (1, 892) => " estilo fluido",
        (1, 855) or (1, 854) or (1, 1013) or (1, 1012) => " (genuino)",
        (1, 999) => " andante",
        (_, 670) => form switch { 1 => " amarilla", 2 => " naranja", 3 => " azul", _ => " blanca" },
        _ when alola.Contains(species) => " de Alola",
        _ when galar.Contains(species) => " de Galar",
        _ when hisui.Contains(species) => " de Hisui",
        _ => $" (forma {form})"
    };

    string Say(int method, int argument, int level) => method switch
    {
        4 => $"al subir al nivel {level}",
        5 => "por intercambio",
        6 => $"por intercambio llevando {itemNames[argument]}",
        7 => "por intercambio con su pareja",
        8 when argument == 994 => "reuniendo 999 Monedas de Gimmighoul",
        8 => $"usando {itemNames[argument]}",
        19 => $"subiendo de nivel de día llevando {itemNames[argument]}",
        21 => $"al subir de nivel sabiendo {moveNames[argument]}",
        22 => $"subiendo de nivel con {speciesNames[argument]} en el equipo",
        1 => "al subir de nivel con mucha amistad",
        2 => "al subir de nivel de día con mucha amistad",
        3 => "al subir de nivel de noche con mucha amistad",
        9 => $"al nivel {level} con más Ataque que Defensa",
        10 => $"al nivel {level} con el mismo Ataque que Defensa",
        11 => $"al nivel {level} con más Defensa que Ataque",
        12 or 13 => $"al nivel {level}, según su personalidad",
        14 => $"al nivel {level}",
        15 => $"al nivel {level}, con hueco en el equipo y una Poké Ball",
        16 => "al subir de nivel con mucha belleza",
        17 => $"usando {itemNames[argument]} (macho)",
        18 => $"usando {itemNames[argument]} (hembra)",
        20 => $"subiendo de nivel de noche llevando {itemNames[argument]}",
        23 => $"al nivel {level} (macho)",
        24 => $"al nivel {level} (hembra)",
        25 => "al subir de nivel en una zona magnética (Cañón de Poni o Planta Energética)",
        26 => "al subir de nivel cerca de la Roca Musgo (Jungla Umbría)",
        27 => "al subir de nivel cerca de la Roca Hielo (Monte Lanakila)",
        28 => $"al nivel {level} con la consola boca abajo",
        29 => $"al subir de nivel con mucho cariño sabiendo un movimiento de tipo {TypeName(argument)}",
        30 => $"al nivel {level} con un Pokémon de tipo Siniestro en el equipo",
        31 => $"al nivel {level} mientras llueve",
        32 => $"al nivel {level} de día",
        33 => $"al nivel {level} de noche",
        34 => $"al nivel {level} (hembra)",
        36 => $"al nivel {level}",
        37 => $"al nivel {level} de día",
        38 => $"al nivel {level} de noche",
        39 => "al subir de nivel en el Monte Lanakila",
        40 => $"al nivel {level} al atardecer",
        41 => $"al nivel {level} en el Ultraespacio",
        42 => $"usando {itemNames[argument]} en el Ultraespacio",
        _ => $"método {method}"
    };

    var fixer = new ImpossibleEvolutionFixer(options);
    var evolutions = new List<object>();
    using (var patcher = new GarcPatcher(workspace.PathOf(GameFiles.Evolution)))
    {
        var partners = ImpossibleEvolutionFixer.Partners(patcher);
        for (var row = 0; row < patcher.FileCount; row++)
        {
            var before = patcher.Read(row);
            var after = (byte[])before.Clone();
            fixer.FixSpecies(after, row, partners);
            if (!rowOf.TryGetValue(row, out var who)) continue;

            // Las que cambia PermaLocke y, desde el 2026-09-25, todas las que no son «subir al nivel N» a secas: el
            // jugador buscó cómo evoluciona Gimmighoul y no estaba, porque el juego no la toca.
            for (var at = 0; at + 8 <= after.Length; at += 8)
            {
                int U16(byte[] b, int o) => BitConverter.ToUInt16(b, o);
                if (U16(after, at) == 0 || (U16(after, at) == 4 && after.AsSpan(at, 8).SequenceEqual(before.AsSpan(at, 8)))) continue;
                var target = U16(after, at + 4);
                var targetForm = Math.Max(0, (int)(sbyte)after[at + 6]);
                var method = U16(after, at);
                var argument = U16(after, at + 2);
                evolutions.Add(new
                {
                    especie = who.Species, forma = who.Form, nombre = Name(who.Species, who.Form),
                    destino = target, destinoForma = targetForm, destinoNombre = Name(target, targetForm),
                    como = method switch { 4 => "nivel", 8 or 17 or 18 or 42 => "objeto", 19 => "objetoDeDia", 22 => "compañero", _ => "otro" },
                    cambiada = !after.AsSpan(at, 8).SequenceEqual(before.AsSpan(at, 8)),
                    subeNivel = method is not (5 or 6 or 7 or 8 or 17 or 18 or 42),
                    nivel = method == 4 ? after[at + 7] : 0,
                    objeto = method is 8 or 17 or 18 or 19 or 20 or 42 ? argument : 0,
                    companero = method == 22 ? argument : 0,
                    ahora = Say(method, argument, after[at + 7]),
                    antes = Say(U16(before, at), U16(before, at + 2), before[at + 7])
                });
            }
        }
    }

    // Los mostradores con lista propia, y la lista de gen 8-9 derramada por specialMartOrder. Los huecos y los sitios
    // son los medidos jugando que documenta _specialMartOrder: 10 Hauoli (8), 15 Paniola (3), 25 y 26 (8 y 8, 2026-09-28,
    // fuera del Ultraganga por su cupón; su pueblo aún no se ha visto jugando).
    (string Place, int Slots)[] spill = [("Ciudad Hauoli · Centro Pokémon", 8), ("Pueblo Paniola · mostrador especial", 3),
        ("Mostrador especial de un Centro Pokémon (el 25, sitio por confirmar)", 8),
        ("Mostrador especial de un Centro Pokémon (el 26, sitio por confirmar)", 8)];
    var shops = options.SpecialMartShelves.Select(shelf => new
    {
        lugar = shelf.Place,
        objetos = shelf.Items.Select(i => new { id = i.Id, nombre = itemNames[i.Id], precio = i.Price > 0 ? i.Price : shelf.Price }).ToList()
    }).ToList();
    var queue = new Queue<MartItem>(options.SpecialMartItems);
    foreach (var (place, slots) in spill)
    {
        var here = Enumerable.Range(0, Math.Min(slots, queue.Count)).Select(_ => queue.Dequeue())
            .Select(i => new { id = i.Id, nombre = itemNames[i.Id], precio = i.Price > 0 ? i.Price : options.SpecialMartItemPrice }).ToList();
        if (here.Count > 0) shops.Add(new { lugar = place, objetos = here });
    }

    var output = Path.Combine(root, "Data", "informacion.json");
    await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new
    {
        _comentario = "Generado por RomTool «informacion» desde el juego con el mod de gen 8-9 y Data/randomizer.json. No se edita a mano.",
        evoluciones = evolutions,
        tiendas = shops
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    Console.WriteLine($"{evolutions.Count} evoluciones y {shops.Count} tiendas en {output}");
}

// Especie, nombre e icono de cada Pokémon, para revisar a ojo que cada nombre lleva su dibujo (2026-09-25: Xurkitree
// salía con el de Pheromosa). Escribe un CSV en la carpeta temporal; las hojas se montan aparte.
async Task IconosNombresAsync()
{
    using var workspace = await RomWorkspace.ExtractAsync(RequireRom(), work, baseLayer: baseLayer);
    var names = workspace.Config.GetText(TextName.SpeciesNames);
    var index = await PokemonIconIndex.BuildAsync(RequireRom(), Path.Combine(Path.GetTempPath(), "permalocke-iconos"), baseLayer);
    var output = Path.Combine(Path.GetTempPath(), "permalocke-iconos.csv");
    await File.WriteAllLinesAsync(output, index.OrderBy(pair => pair.Key)
        .Select(pair => $"{pair.Key};{(pair.Key < names.Length ? names[pair.Key] : "?")};{pair.Value}"));
    Console.WriteLine($"{index.Count} especies en {output}");
}

// La tabla de colores variocolor de cada icono (Data/variocolor.json). El cartucho no tiene iconos variocolor: se
// sacan los colores de los renders de Pokémon Showdown (dex y dex-shiny, mismo encuadre píxel a píxel) y se aplican
// al icono con ShinyPalette. Lo que se guarda es una tabla de colores, no un dibujo: el dibujo sigue saliendo de la
// ROM de cada jugador. Deja además hojas de revisión en la carpeta temporal, porque un icono mal recoloreado no
// falla, solo se ve feo. Las descargas se guardan en %TEMP%/permalocke-showdown para no repetirlas.
async Task VariocolorAsync()
{
    var rom = RequireRom();
    var scratch = Path.Combine(work, "iconos");
    var reader = PokemonIconReader.Open(rom, scratch, baseLayer);
    var index = await PokemonIconIndex.BuildAsync(rom, scratch, baseLayer);
    var forms = PokemonIconIndex.FormIcons(index, reader.Count);

    var strings = PKHeX.Core.GameInfo.GetStrings("en");
    static string Id(string name) => new string(name.Replace("♀", "f").Replace("♂", "m")
        .Normalize(System.Text.NormalizationForm.FormD)
        .Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // Cada icono con la especie y la forma que lo dibujan, y su nombre en Showdown.
    var targets = index.Where(p => p.Key < strings.specieslist.Length)
        .Select(p => (Icon: p.Value, Species: p.Key, Form: 0, Name: Id(strings.specieslist[p.Key])))
        .ToList();
    foreach (var ((species, form), icon) in forms)
    {
        var formNames = PKHeX.Core.FormConverter.GetFormList((ushort)species, strings.Types, strings.forms,
            PKHeX.Core.GameInfo.GenderSymbolASCII, PKHeX.Core.EntityContext.Gen9);
        if (form < formNames.Length && Id(formNames[form]) is { Length: > 0 } suffix)
        {
            targets.Add((icon, species, form, $"{Id(strings.specieslist[species])}-{suffix}"));
        }
    }

    var cache = Path.Combine(Path.GetTempPath(), "permalocke-showdown");
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("PermaLocke-RomTool/1.0");

    async Task<byte[]?> Fetch(string folder, string name)
    {
        var file = Path.Combine(cache, folder, name + ".png");
        if (File.Exists(file)) return await File.ReadAllBytesAsync(file);
        if (File.Exists(file + ".404")) return null;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var response = await http.GetAsync($"https://play.pokemonshowdown.com/sprites/{folder}/{name}.png");
        if (!response.IsSuccessStatusCode) { await File.WriteAllTextAsync(file + ".404", ((int)response.StatusCode).ToString()); return null; }
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(file, bytes);
        return bytes;
    }

    // El lector de iconos no es seguro entre hilos: se leen todos antes del bucle paralelo.
    var pixels = targets.Select(t => t.Icon).Distinct().ToDictionary(i => i, i => reader.Read(i));
    var tables = new System.Collections.Concurrent.ConcurrentDictionary<int, IReadOnlyDictionary<int, int>>();
    var missing = new System.Collections.Concurrent.ConcurrentBag<string>();
    await Parallel.ForEachAsync(targets.DistinctBy(t => t.Icon), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (t, _) =>
    {
        // Primero los renders de la dex; si faltan o el variocolor es una copia del normal (casi toda gen 9), los de HOME.
        var why = "sin render";
        foreach (var folder in new[] { "dex", "home" })
        {
            var normal = await Fetch(folder, t.Name);
            var shiny = await Fetch(folder + "-shiny", t.Name);
            if (normal is null || shiny is null) continue;

            var n = PngImage.Decode(normal);
            var s = PngImage.Decode(shiny);
            if ((n.Width, n.Height) != (s.Width, s.Height)) { why = "tamaños distintos"; continue; }

            if (ShinyPalette.Build(n.Rgba, s.Rgba, pixels[t.Icon].Pixels) is { } table)
            {
                tables[t.Icon] = table;
                return;
            }
            why = "sin pareja utilizable (igual que el normal o en otra pose)";
        }
        missing.Add($"{t.Species}/{t.Form} {t.Name}: {why}");
    });

    var output = Path.Combine(root, "Data", "variocolor.json");
    await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new
    {
        _comentario = "Generado por RomTool «variocolor». Por icono del cartucho (a/0/6/2 con el mod): color normal > color variocolor, en hexadecimal RGB. Los colores salen de comparar los renders dex y dex-shiny de Pokémon Showdown; es una aproximación. No se edita a mano: los que salen mal se quitan en «descartados».",
        iconos = tables.OrderBy(p => p.Key).ToDictionary(p => p.Key.ToString(),
            p => string.Join(' ', p.Value.OrderBy(c => c.Key).Select(c => $"{c.Key:X6}>{c.Value:X6}"))),
        sinReferencia = missing.OrderBy(m => int.Parse(m[..m.IndexOf('/')])).ToList()
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    Console.WriteLine($"{tables.Count} iconos con variocolor, {missing.Count} sin referencia, en {output}");

    // Hojas de revisión: normal y variocolor lado a lado, a escala 2, 8 por fila y 12 filas por hoja.
    var sheets = Path.Combine(Path.GetTempPath(), "permalocke-variocolor");
    Directory.CreateDirectory(sheets);
    var order = targets.DistinctBy(t => t.Icon).Where(t => tables.ContainsKey(t.Icon)).OrderBy(t => t.Species).ThenBy(t => t.Form).ToList();
    const int perRow = 8, rows = 12, scale = 2, cell = 40 * 2 * scale + 8, height = 30 * scale + 6;
    var legend = new List<string>();
    for (var page = 0; page * perRow * rows < order.Count; page++)
    {
        var sheet = new byte[perRow * cell * rows * height * 4];
        var width = perRow * cell;
        for (var k = 0; k < perRow * rows && page * perRow * rows + k < order.Count; k++)
        {
            var t = order[page * perRow * rows + k];
            legend.Add($"hoja {page:00} fila {k / perRow} col {k % perRow}: {t.Species}/{t.Form} {t.Name}");
            var icon = pixels[t.Icon];
            var recoloured = (byte[])icon.Pixels.Clone();
            for (var p = 0; p < recoloured.Length; p += 4)
            {
                if (recoloured[p + 3] == 0) continue;
                var c = tables[t.Icon][(recoloured[p] << 16) | (recoloured[p + 1] << 8) | recoloured[p + 2]];
                recoloured[p] = (byte)(c >> 16); recoloured[p + 1] = (byte)(c >> 8); recoloured[p + 2] = (byte)c;
            }
            for (var half = 0; half < 2; half++)
            {
                var src = half == 0 ? icon.Pixels : recoloured;
                for (var y = 0; y < Math.Min(icon.Height, 30) * scale; y++)
                    for (var x = 0; x < icon.Width * scale; x++)
                    {
                        var s = ((y / scale) * icon.Width + x / scale) * 4;
                        var dx = (k % perRow) * cell + half * icon.Width * scale + x;
                        var dy = (k / perRow) * height + y;
                        if (src[s + 3] != 0) src.AsSpan(s, 4).CopyTo(sheet.AsSpan((dy * width + dx) * 4));
                    }
            }
        }
        await File.WriteAllBytesAsync(Path.Combine(sheets, $"hoja-{page:00}.png"), PngImage.Encode(sheet, width, rows * height));
    }
    await File.WriteAllLinesAsync(Path.Combine(sheets, "leyenda.txt"), legend);
    Console.WriteLine($"Hojas de revisión en {sheets}");
}
