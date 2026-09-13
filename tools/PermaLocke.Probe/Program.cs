using System.Globalization;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

// Diagnostic tool, not part of the shipped applications. Talks to a running Azahar over its
// RPC server to locate the game structures PermaLocke needs.
//
//   Probe                       ping, process list and a dump of the code entry point
//   Probe --species             checks the PKHeX species table
//   Probe --map                 which address ranges are mapped right now
//   Probe --dump 0x8123456 64   hex dump
//   Probe --text "GRENIN"       finds UTF-16 text, e.g. the trainer name
//   Probe --scan 25             addresses currently holding the 16 bit value 25
//   Probe --refine 26           keeps the previous candidates that now hold 26
//   Probe --write-test          checks that WriteMemory is accepted
//   Probe --search "GRENIN"     native search, only on PermaLocke fork of Azahar
//   Probe --candy [n] [--sweep] bag: locate, list, and optionally write n Rare Candies
//   Probe --flags [fichero]     dumps the save event flags and counters, to calibrate achievements
//   Probe --flags-diff a b      what changed between two dumps
//   Probe --nombres [--arreglar] lists, and optionally restores, the Pokémon left without a name
//   Probe --ev [--probar]       lists the effort values; --probar proves the write on a COPY
//   Probe --equipo [--cap N]    the live party in every copy, both level fields side by side
//   Probe --zona                the four copies of the area field, and why they are believed or not
//   Probe --pk <fichero>        reads a party slot backup; --cap N says what a cap write changes
//   Probe --pids [--arreglar]   gives a PID to what was delivered without one and links it to the run
//   Probe --pids --probar       does the whole PID repair on a COPY of the partida and checks it
//   Probe --etapas [n]          stages marked by hand; with a number, corrects it through the service
//   Probe --ruleta [--probar]   every face of the wheel against a COPY of the partida
//   Probe --tiradas [n] "motivo"  roulette ledger; with a number, grants spins through the service
//   Probe --intercambiados      Pokemon handed over in a wonder trade that the run still counts alive
//   Probe --combate buscar|filtrar|vigilar   where the party HP lives DURING a battle, at the move menu

Console.OutputEncoding = System.Text.Encoding.UTF8;

// LO PRIMERO DE TODO, antes de leer un solo byte: qué mundo va a leer Azahar.
//
// WorldLimits es estado global y arranca con el techo del cartucho, 807. Con el mod de expansión
// puesto, el equipo del jugador lleva Pokémon por encima de ese número, y TODOS los lectores en
// vivo los tiran como basura del heap. La aplicación llama a esto al arrancar; la sonda no lo
// hacía, y el resultado fueron dos diagnósticos falsos el mismo día:
//
//   «--equipo no encuentra ninguna copia del equipo» — el barrido exige un segundo Pokémon válido
//   a la distancia del salto, y el segundo del equipo era un Ursaluna (901). Rechazado por el
//   techo, no hay salto, no hay copia. Mientras tanto la aplicación leía el equipo sin problema.
//
//   «Tinkaton va dos niveles por detrás del juego» — el nivel sale de la experiencia y la curva es
//   una propiedad de la especie. Sin la tabla del mod, PKHeX cae a Medium Fast y da un número
//   equivocado con toda confianza. Es exactamente el fallo que GameLevels existe para evitar.
//
// Una herramienta de diagnóstico que miente sobre el estado del juego es peor que no tenerla.
PermaLocke.App.Services.InstalledWorld.ApplyQuietly(AppContext.BaseDirectory);

// Calibración de banderas: no necesita emulador, solo la partida guardada.
if (args.Length >= 1 && args[0] == "--flags")
{
    var file = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "banderas.txt");
    return PermaLocke.Probe.FlagProbe.Dump(file);
}

if (args.Length >= 3 && args[0] == "--flags-diff")
{
    return PermaLocke.Probe.FlagProbe.Diff(args[1], args[2]);
}

// Reparación de nombres: tampoco necesita emulador, y para escribir lo exige cerrado.
if (args.Length >= 1 && args[0] == "--nombres")
{
    return PermaLocke.Probe.NameProbe.Run(args.Contains("--arreglar"));
}

// Huevo: las copias de entradas de equipo que PermaLocke guarda antes de escribir.
if (args.Length >= 1 && args[0] == "--huevo-copias")
{
    return PermaLocke.Probe.EggBackups.Run(args.Length >= 2 ? args[1] : "Saves/backup");
}


// Quien escribe en un rango de memoria. Necesita el parche 3 del fork.
// Saca de la memoria la copia de trabajo de la partida a un fichero aparte. Solo lee.
if (args.Length >= 1 && args[0] == "--rescate")
{
    return PermaLocke.Probe.RescueProbe.Run();
}

// Los PS durante un combate: graba mientras se juega. Solo lee.
if (args.Length >= 1 && args[0] == "--combate")
{
    return PermaLocke.Probe.BattleHpProbe.Run(args.Length >= 2 ? args[1] : string.Empty,
        args.Length >= 3 && int.TryParse(args[2], out var segundos) ? segundos : 180);
}

if (args.Length >= 1 && args[0] == "--escrituras")
{
    if (args.Contains("--leer"))
    {
        return PermaLocke.Probe.WriteWatchProbe.Read();
    }

    if (args.Length < 2)
    {
        Console.WriteLine("Uso: --escrituras <direccion> [tamano]   ·   --escrituras --leer");
        Console.WriteLine("     --escrituras <direccion> 0          deja de vigilar");
        return 1;
    }

    var watched = uint.Parse(args[1].Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase),
        NumberStyles.HexNumber);

    return PermaLocke.Probe.WriteWatchProbe.Watch(watched,
        args.Length >= 3 ? uint.Parse(args[2]) : 2u);
}

// Los PS: dentro de las entradas ya DESCIFRADAS, a cualquier offset.
if (args.Length >= 2 && args[0] == "--ps-cola")
{
    return PermaLocke.Probe.HpProbe.Tail(int.Parse(args[1]));
}

// Los PS: todas las copias de cada Pokemon, halladas por su constante de encriptacion.
if (args.Length >= 1 && args[0] == "--ps-copias")
{
    return PermaLocke.Probe.HpProbe.Copies();
}

// Los PS: el bloque de estadisticas de UN Pokemon, sin exigir paso entre ellos.
if (args.Length >= 1 && args[0] == "--ps-bloque")
{
    return PermaLocke.Probe.HpProbe.Block(args.Length >= 2 ? int.Parse(args[1]) : 64);
}

// Los PS: buscar la tabla del equipo por su forma, no el valor por su cara.
if (args.Length >= 1 && args[0] == "--ps-tabla")
{
    return PermaLocke.Probe.HpProbe.Table(args.Length >= 2 ? int.Parse(args[1]) : 1024);
}

// Los PS: escribir uno concreto en un hueco, para poder mirar la pantalla.
if (args.Length >= 3 && args[0] == "--ps-escribir")
{
    return PermaLocke.Probe.HpProbe.Write(int.Parse(args[1]) - 1, int.Parse(args[2]));
}

// Los PS: donde los guarda el juego de verdad.
if (args.Length >= 3 && args[0] == "--ps")
{
    return PermaLocke.Probe.HpProbe.Run(int.Parse(args[1]), int.Parse(args[2]), args.Contains("--reiniciar"));
}

// Huevo: comparar la partida con una copia previa a una escritura.
if (args.Length >= 2 && args[0] == "--huevo-diff")
{
    return PermaLocke.Probe.EggCompare.Run(args[1], args.Length >= 3 ? int.Parse(args[2]) - 1 : 0);
}

// Huevo: distingue un huevo de verdad de un Huevo Malo. Solo lee la partida.
if (args.Length >= 1 && args[0] == "--huevo")
{
    // El hueco iba fijo en el primero, porque ahi estaba el Huevo Malo del §97. El siguiente
    // aparecio en el tercero, asi que se elige: una herramienta de reparacion que solo sabe
    // arreglar el sitio donde paso la primera vez no sirve la segunda.
    var brokenSlot = args.Contains("--hueco")
        ? int.Parse(args[Array.IndexOf(args, "--hueco") + 1]) - 1
        : 0;

    return args.Contains("--arreglar")
        ? PermaLocke.Probe.EggProbe.Repair(args[Array.IndexOf(args, "--arreglar") + 1], brokenSlot,
            args.Contains("--probar"))
        : PermaLocke.Probe.EggProbe.Run();
}

// EV: leer no exige nada, y la prueba de escritura va sobre una copia, nunca sobre la partida.
if (args.Length >= 1 && args[0] == "--ev")
{
    return PermaLocke.Probe.EvProbe.Run(args.Contains("--probar"));
}

// Entregados en un wonder trade y contados como vivos.
if (args.Length >= 1 && args[0] == "--intercambiados")
{
    return await PermaLocke.Probe.TradedAwayProbe.RunAsync(args.Contains("--arreglar"));
}

// Cada cara de la ruleta contra una COPIA de la partida. La partida no se toca nunca.
if (args.Length >= 1 && args[0] == "--tiradas")
{
    return await PermaLocke.Probe.GrantSpinProbe.RunAsync(
        args.Length > 1 && int.TryParse(args[1], out var tiradas) ? tiradas : null,
        args.Length > 2 ? args[2] : null);
}

if (args.Length >= 1 && args[0] == "--ruleta")
{
    return await PermaLocke.Probe.RouletteProbe.RunAsync(args.Contains("--probar"));
}

// Etapas marcadas a mano: mirar y, con un numero, corregir. Pasa por ProgressService, asi que
// la correccion queda en el historial.
if (args.Length >= 1 && args[0] == "--credito")
{
    return await PermaLocke.Probe.CreditProbe.RunAsync(
        args.Length > 1 ? args[1] : null,
        args.Length > 2 ? string.Join(" ", args.Skip(2)) : null);
}

if (args.Length >= 1 && args[0] == "--etapas")
{
    return await PermaLocke.Probe.StageProbe.RunAsync(
        args.Length > 1 && int.TryParse(args[1], out var etapas) ? etapas : null);
}

// PID que faltan: empareja por IVs lo que la run entregó antes de que se guardara el PID.
if (args.Length >= 1 && args[0] == "--pids")
{
    return await PermaLocke.Probe.PidRepairProbe.RunAsync(args.Contains("--arreglar"), args.Contains("--probar"));
}

// Empareja las fotos de zona de islas/ con las zonas del mapa. No necesita emulador ni run.
if (args.Length >= 1 && args[0] == "--fotos")
{
    return PermaLocke.Probe.ZonePhotoMatcher.Run(
        new PermaLocke.Infrastructure.AppPaths().Root);
}

// Auditoría de la run contra su propio historial. No necesita emulador.
if (args.Length >= 1 && args[0] == "--run")
{
    return await PermaLocke.Probe.RunAuditProbe.RunAsync();
}

// Solo se usa tras regenerar e instalar el LayeredFS para el destino. El cambio de rol queda
// firmado en el historial; editar run.json a mano dejaría una partida distinta sin decir por qué.
if (args.Length >= 2 && args[0] == "--rol")
{
    return await PermaLocke.Probe.RoleProbe.ChangeAsync(args[1]);
}

// Las cuatro copias del campo de zona, y por qué se creen o no. Con --vigilar, en bucle: se anda
// por el juego y va diciendo cada valor que toma el campo, que es como se recalibra el ancla.
if (args.Length >= 1 && args[0] == "--zona")
{
    return args.Contains("--vigilar")
        ? PermaLocke.Probe.ZoneProbe.Watch()
        : PermaLocke.Probe.ZoneProbe.Run();
}

// El equipo vivo en todas sus copias, con los dos niveles al lado. Con --cap, además escribe.
if (args.Length >= 1 && args[0] == "--equipo")
{
    var wantedCap = Array.IndexOf(args, "--cap");
    var wantedTrainer = Array.IndexOf(args, "--entrenador");

    return PermaLocke.Probe.PartyLiveProbe.Run(
        wantedTrainer >= 0 && args.Length > wantedTrainer + 1 ? args[wantedTrainer + 1] : null,
        wantedCap >= 0 && args.Length > wantedCap + 1 ? int.Parse(args[wantedCap + 1]) : null);
}

// Lee una copia de seguridad de un hueco del equipo. Sin emulador: son bytes en disco.
if (args.Length >= 2 && args[0] == "--pk")
{
    return args.Length >= 4 && args[2] == "--cap"
        ? PermaLocke.Probe.PartyDumpProbe.WhatWouldChange(args[1], int.Parse(args[3]))
        : PermaLocke.Probe.PartyDumpProbe.Run(args[1]);
}

// Comprobación del parche SearchMemory del fork. Va lo primero porque no necesita nada más.
if (args.Length >= 1 && args[0] == "--candy")
{
    var amount = args.FirstOrDefault(a => int.TryParse(a, out _));
    PermaLocke.Probe.BagProbe.Run(amount is null ? null : int.Parse(amount), args.Contains("--sweep"));
    return 0;
}

// Pone un Pokemon concreto en el PC, para medir cosas que necesitan una especie que la run no
// tiene: --dar-pokemon <especie> <nivel>. Exige el juego cerrado, como el gacha.
if (args.Length >= 3 && args[0] == "--dar-pokemon")
{
    return PermaLocke.Probe.GivePokemonProbe.Run(int.Parse(args[1]), int.Parse(args[2]));
}

// Que sabe PKHeX del desbloqueo de megaevolucion en la partida. Solo mira, no escribe.
if (args.Length >= 1 && args[0] == "--mega")
{
    return args.Contains("--activar")
        ? PermaLocke.Probe.MegaProbe.Enable()
        : PermaLocke.Probe.MegaProbe.Run();
}

// Escribe CUALQUIER objeto en la mochila del juego en marcha, para medir cosas que aún no tienen
// botón: --dar <id> <cantidad>. Misma ruta que la tienda, o sea con relectura.
if (args.Length >= 3 && args[0] == "--dar")
{
    PermaLocke.Probe.BagProbe.Run(int.Parse(args[2]), sweep: false, itemId: int.Parse(args[1]));
    return 0;
}

if (args.Length >= 2 && args[0] == "--search")
{
    PermaLocke.Probe.SearchProbe.Run(args[1]);
    return 0;
}

var candidateFile = Path.Combine(AppContext.BaseDirectory, "candidatos.txt");

if (args.Contains("--pkhex-test"))
{
    // Decides, without guessing, whether PK7 wants encrypted or decrypted bytes. Everything
    // about how memory is searched depends on the answer.
    var sample = new PKHeX.Core.PK7
    {
        Species = 722,
        CurrentLevel = 6,
        Stat_HPMax = 24,
        Stat_HPCurrent = 24,
        OriginalTrainerName = "Grenin430"
    };

    sample.RefreshChecksum();

    var decryptedBytes = new byte[sample.SIZE_PARTY];
    sample.WriteDecryptedDataParty(decryptedBytes);

    var encryptedBytes = new byte[sample.SIZE_PARTY];
    sample.WriteEncryptedDataParty(encryptedBytes);

    Console.WriteLine($"PK7 construido desde bytes DESCIFRADOS → especie {new PKHeX.Core.PK7(decryptedBytes).Species}");
    Console.WriteLine($"PK7 construido desde bytes CIFRADOS    → especie {new PKHeX.Core.PK7(encryptedBytes).Species}");
    Console.WriteLine();
    Console.WriteLine($"Sanity={sample.Sanity}  Checksum=0x{sample.Checksum:X4}");
    Console.WriteLine($"Cabecera descifrada: {Convert.ToHexString(decryptedBytes.AsSpan(0, 16))}");
    Console.WriteLine($"Cabecera cifrada:    {Convert.ToHexString(encryptedBytes.AsSpan(0, 16))}");
    return 0;
}

if (args.Contains("--loc-api"))
{
    foreach (var method in typeof(PKHeX.Core.GameStrings).GetMethods()
                 .Where(m => m.Name.StartsWith("GetLocation", StringComparison.Ordinal)))
    {
        var parameters = string.Join(", ",
            method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine(method.ReturnType.Name + " " + method.Name + "(" + parameters + ")");
    }

    return 0;
}


if (Index("--locations") is { } locIndex)
{
    // PKHeX ships the location tables for Ultra Sun/Moon, so the zone identifier the game
    // stores can be looked up by name instead of guessed.
    var needle = args[locIndex + 1];
    var strings = PKHeX.Core.GameInfo.GetStrings("es");
    var matches = 0;

    for (ushort id = 0; id < 1000; id++)
    {
        var name = strings.GetLocationName(false, id, 7, 7, PKHeX.Core.GameVersion.UM);

        if (!string.IsNullOrWhiteSpace(name)
            && name.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  " + id + "  " + name);
            matches++;
        }
    }

    Console.WriteLine("\n" + matches + " zonas coinciden con '" + needle + "'.");
    return 0;
}


if (Index("--species-find") is { } sfIndex)
{
    var finder = new PkhexSpeciesLookup();
    var query = args[sfIndex + 1];

    foreach (var s in finder.All.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("  " + s.Number + "  " + s.Name);
    }

    return 0;
}

// Buscar un objeto por su nombre. El id es lo único que entiende la mochila, y teclearlo de
// memoria es como acaba uno entregando el objeto equivocado sin que nada falle.
// Id de un movimiento por su nombre, para anclar tablas que solo se conocen por lo que enseñan.
// Varios nombres a la vez y en orden: una lista de dieciseis movimientos leida en pantalla es lo
// unico que identifica al tutor que los vende, y buscar esa secuencia exige los ids en ese orden.
if (Index("--movimiento-find") is { } mfIndex)
{
    var moves = PKHeX.Core.GameInfo.GetStrings("es").movelist;

    foreach (var query in args.Skip(mfIndex + 1))
    {
        var hits = 0;

        for (var id = 0; id < moves.Length; id++)
        {
            if (moves[id].Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{id,4}  {moves[id]}");
                hits++;
            }
        }

        if (hits == 0)
        {
            // Ni se adivina ni se calla: un nombre que no resuelve invalida el anclaje entero.
            Console.WriteLine($"   ?  «{query}» NO EXISTE con ese nombre exacto");

            for (var id = 0; id < moves.Length; id++)
            {
                if (moves[id].Contains(query.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"        parecido: {id} {moves[id]}");
                }
            }
        }
    }

    return 0;
}

if (Index("--objeto-find") is { } ofIndex)
{
    var names = PKHeX.Core.GameInfo.GetStrings("es").itemlist;
    var query = args[ofIndex + 1];

    for (var id = 0; id < names.Length; id++)
    {
        if (names[id].Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  " + id + "  " + names[id]);
        }
    }

    return 0;
}

if (args.Contains("--species"))
{
    var lookup = new PkhexSpeciesLookup();
    Console.WriteLine($"Especies cargadas: {lookup.All.Count}");
    Console.WriteLine($"Primera: {lookup.All[0].Number} {lookup.All[0].Name}");
    Console.WriteLine($"Última:  {lookup.All[^1].Number} {lookup.All[^1].Name}");
    return 0;
}

using var client = new AzaharRpcClient();

Console.WriteLine("PermaLocke · sonda del RPC de Azahar");
Console.WriteLine(new string('-', 62));

if (!client.TryPing(out var pingMessage))
{
    Console.WriteLine($"SIN CONEXIÓN\n{pingMessage}");
    Console.WriteLine("\nAzahar debe estar abierto, con el juego cargado y el servidor RPC activado.");
    return 1;
}

Console.WriteLine(pingMessage);

foreach (var process in client.ListProcesses())
{
    Console.WriteLine($"  PID {process.ProcessId}  {process.TitleId:X16}  {process.Name}");
}

// Ultra Moon (EUR). Without attaching, the server serves reads from no process at all.
const ulong ultraMoonEur = 0x00040000001B5100;

try
{
    var attached = client.AttachTo(ultraMoonEur);
    Console.WriteLine($"Proceso fijado: PID {attached.ProcessId} ({attached.Name})");
}
catch (AzaharRpcException ex)
{
    Console.WriteLine($"No se pudo fijar el proceso: {ex.Message}");
    return 3;
}

Console.WriteLine();

var search = new MemorySearch(client);

if (args.Contains("--map"))
{
    // Unmapped pages have to fail fast or mapping takes forever.
    client.TimeoutMilliseconds = 250;

    foreach (var region in MemorySearch.DefaultRegions)
    {
        Console.WriteLine($"Explorando {region}...");

        foreach (var readable in search.MapReadableRanges(region))
        {
            Console.WriteLine($"  legible  {readable}");
        }
    }

    return 0;
}

if (args.Contains("--mapwide"))
{
    client.TimeoutMilliseconds = 150;

    foreach (var region in MemorySearch.WideRegions)
    {
        var mapped = search.MapCoarse(region);
        Console.WriteLine($"{region.Label,-11} {region.Start:X8}-{region.End:X8}  "
                          + $"{mapped.Count} MB legibles");

        if (mapped.Count > 0)
        {
            Console.WriteLine($"            desde 0x{mapped[0]:X8} hasta 0x{mapped[^1]:X8}");
        }
    }

    return 0;
}

if (Index("--text") is { } textIndex)
{
    client.TimeoutMilliseconds = 250;
    var needle = args[textIndex + 1];
    var regions = ReadableRegions();

    var hits = search.ScanText(regions, needle);
    Console.WriteLine($"«{needle}» encontrado {hits.Count} veces:");

    foreach (var hit in hits.Take(40))
    {
        Console.WriteLine($"  0x{hit:X8}");
    }

    File.WriteAllLines(candidateFile, hits.Select(h => h.ToString()));
    Console.WriteLine($"\nGuardados en {candidateFile}");
    return 0;
}

if (Index("--scan") is { } scanIndex)
{
    client.TimeoutMilliseconds = 250;
    var value = ushort.Parse(args[scanIndex + 1]);
    var regions = ReadableRegions();

    var hits = search.ScanUInt16(regions, value);
    File.WriteAllLines(candidateFile, hits.Select(h => h.ToString()));

    Console.WriteLine($"Valor {value}: {hits.Count} candidatos guardados en {candidateFile}");
    Console.WriteLine("Cambia ese valor en el juego y ejecuta:  Probe --refine <nuevoValor>");
    return 0;
}

if (Index("--refine") is { } refineIndex)
{
    if (!File.Exists(candidateFile))
    {
        Console.WriteLine("No hay candidatos previos. Ejecuta primero --scan o --text.");
        return 1;
    }

    var value = ushort.Parse(args[refineIndex + 1]);
    var previous = File.ReadAllLines(candidateFile).Select(uint.Parse);

    var survivors = search.Refine(previous, value);
    File.WriteAllLines(candidateFile, survivors.Select(h => h.ToString()));

    Console.WriteLine($"Quedan {survivors.Count} candidatos con el valor {value}:");
    foreach (var hit in survivors.Take(40))
    {
        Console.WriteLine($"  0x{hit:X8}");
    }

    return 0;
}

if (args.Contains("--pk7"))
{
    // Each candidate came from a species field, so the slot itself starts a few bytes earlier.
    if (!File.Exists(candidateFile))
    {
        Console.WriteLine("No hay candidatos. Ejecuta primero --scan <especie>.");
        return 1;
    }

    var reader = new Pk7Reader(client);
    var found = 0;

    foreach (var candidate in File.ReadAllLines(candidateFile).Select(uint.Parse))
    {
        var start = candidate - Pk7Reader.SpeciesOffset;
        var pokemon = reader.TryRead(start);

        if (pokemon is null)
        {
            continue;
        }

        found++;
        Console.WriteLine($"0x{start:X8}  {pokemon.Nickname,-12} #{pokemon.Species,-4} "
                          + $"Nv.{pokemon.Level,-3} {pokemon.CurrentHp}/{pokemon.MaxHp} "
                          + $"OT:{pokemon.TrainerName} {(pokemon.IsShiny ? "✨" : string.Empty)}");
    }

    Console.WriteLine($"\n{found} estructuras válidas de las {File.ReadAllLines(candidateFile).Length} candidatas.");
    return 0;
}

if (Index("--locate") is { } locateIndex)
{
    // Brute forces the distance between a scan hit and the start of the slot, instead of
    // assuming any offset. PKHeX decides what counts as a real Pokémon.
    var wantedSpecies = int.Parse(args[locateIndex + 1]);
    var wantedLevel = args.Length > locateIndex + 2 ? int.Parse(args[locateIndex + 2]) : 0;

    var reader = new Pk7Reader(client);
    var hits = File.ReadAllLines(candidateFile).Select(uint.Parse).ToList();
    var found = 0;

    Console.WriteLine($"Probando {hits.Count} candidatos × {Pk7Reader.PartySize} desplazamientos...\n");

    foreach (var candidate in hits)
    {
        for (var back = 0; back <= Pk7Reader.PartySize; back += 2)
        {
            if (candidate < (uint)back)
            {
                continue;
            }

            var start = candidate - (uint)back;
            var pokemon = reader.TryRead(start);

            if (pokemon is null || pokemon.Species != wantedSpecies)
            {
                continue;
            }

            if (wantedLevel > 0 && pokemon.Level != wantedLevel)
            {
                continue;
            }

            found++;
            Console.WriteLine($"0x{start:X8}  (pista 0x{candidate:X8} − 0x{back:X2})  "
                              + $"{pokemon.Nickname,-12} #{pokemon.Species} Nv.{pokemon.Level} "
                              + $"{pokemon.CurrentHp}/{pokemon.MaxHp}  OT:{pokemon.TrainerName}");
        }
    }

    Console.WriteLine($"\n{found} coincidencias.");
    return 0;
}

if (Index("--peek") is { } peekIndex)
{
    // Reads one stored block and reports what it holds, without sweeping anything.
    var partySize = Pk7Reader.PartySize;

    foreach (var raw in args[peekIndex + 1].Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var target = ParseAddress(raw);

        if (!client.TryReadMemory(target, partySize, out var bytes))
        {
            Console.WriteLine("  0x" + target.ToString("X8") + "  ilegible");
            continue;
        }

        var pokemon = new PKHeX.Core.PK7(bytes);

        // Los TRES niveles, porque no son el mismo numero y confundirlos ya ha costado un
        // diagnostico falso. «exp» es el que sale de la experiencia con la curva del MUNDO
        // INSTALADO -que es lo que hace GameLevels y lo que usa la aplicacion-; «pkhex» es el que
        // saldria con la tabla de PKHeX, que para una especie del mod cae a Medium Fast y da un
        // numero equivocado con toda confianza; y «0xEC» es Stat_Level, el campo que el juego
        // PINTA. Los tres deberian coincidir, y cuando no lo hacen eso es la noticia.
        // Los PS van AQUI y no en un volcado en crudo: el bloque de equipo se guarda cifrado
        // entero, cola incluida, asi que mirar el offset 0xF0 con --dump devuelve basura. Se
        // intento, dio ceros, y el unico camino honesto de leerlos sin escribir es este.
        Console.WriteLine("  0x" + target.ToString("X8")
                          + "  #" + pokemon.Species
                          + "  PS=" + pokemon.Stat_HPCurrent + "/" + pokemon.Stat_HPMax
                          + "  exp=" + pokemon.EXP
                          + "  Nv(exp)=" + PermaLocke.GameLink.Data.GameLevels.Of(pokemon)
                          + "  Nv(pkhex)=" + pokemon.CurrentLevel
                          + "  Nv(0xEC)=" + pokemon.Stat_Level
                          + "  checksum=" + (pokemon.ChecksumValid ? "ok" : "NO")
                          // IsEgg no es un campo suyo: es el BIT 30 del entero de 32 bits que
                          // guarda los seis IV, con el 31 para IsNicknamed. Por eso una escritura
                          // torcida en los IV puede convertir a un Pokemon en un huevo sin que
                          // nada mas cambie, y por eso hay que poder verlo de un vistazo.
                          + "  IV=" + string.Join('/', pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF,
                              pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE)
                          + (pokemon.IsEgg ? "  HUEVO" : string.Empty)
                          + (pokemon.IsNicknamed ? "  mote" : string.Empty)
                          + "  «" + pokemon.Nickname + "»"
                          + (pokemon.IsShiny ? "  SHINY" : string.Empty));
    }

    return 0;
}


if (Index("--hunt-any") is { } anyIndex)
{
    // Same stored-format sweep, but without the trainer filter: a wild Pokémon does not carry
    // the player's name. Filtered by species instead, which the player reads off the screen.
    var wantedSpecies = int.Parse(args[anyIndex + 1]);
    var storedSize = new PKHeX.Core.PK7().SIZE_STORED;
    var partySize = Pk7Reader.PartySize;
    var found = 0;

    foreach (var region in MemorySearch.DefaultRegions)
    {
        var buffer = new byte[region.Size];

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            var chunk = (int)Math.Min(0x1000, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
            }
        }

        for (var offset = 0; offset + storedSize <= buffer.Length; offset += 4)
        {
            if (BitConverter.ToUInt16(buffer, offset + 4) != 0
                || BitConverter.ToUInt16(buffer, offset + 6) == 0)
            {
                continue;
            }

            var block = new byte[partySize];
            buffer.AsSpan(offset, storedSize).CopyTo(block);

            var pokemon = new PKHeX.Core.PK7(block);

            if (!pokemon.ChecksumValid
                || pokemon.Species != wantedSpecies
                || pokemon.CurrentLevel is <= 0 or > 100)
            {
                continue;
            }

            found++;
            Console.WriteLine("  0x" + (region.Start + offset).ToString("X8")
                              + "  #" + pokemon.Species + " " + pokemon.Nickname
                              + "  Nv." + pokemon.CurrentLevel
                              + "  OT:'" + pokemon.OriginalTrainerName + "'"
                              + (pokemon.IsShiny ? "  SHINY" : string.Empty));
        }
    }

    Console.WriteLine("\n" + found + " bloques de la especie " + wantedSpecies + ".");
    return 0;
}


if (Index("--hunt-stored") is { } storedIndex)
{
    // The party sweep reads 0x104 bytes and demands coherent battle stats, so it discards the
    // save-resident copies, which are stored in the 0xE8 "stored" format with no battle stats
    // at all. This looks for those: checksum valid, sane species and level, right trainer.
    var trainer = args[storedIndex + 1];
    var storedSize = new PKHeX.Core.PK7().SIZE_STORED;
    var partySize = Pk7Reader.PartySize;
    var found = 0;

    foreach (var region in MemorySearch.DefaultRegions)
    {
        Console.WriteLine("Barriendo " + region + " en formato stored (0x"
                          + storedSize.ToString("X") + ")...");

        var buffer = new byte[region.Size];

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            var chunk = (int)Math.Min(0x1000, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
            }
        }

        Console.WriteLine("  leido, analizando...");

        for (var offset = 0; offset + storedSize <= buffer.Length; offset += 4)
        {
            if (BitConverter.ToUInt16(buffer, offset + 4) != 0
                || BitConverter.ToUInt16(buffer, offset + 6) == 0)
            {
                continue;
            }

            // Pad the stored block up to party size; the tail stays zero and is ignored.
            var block = new byte[partySize];
            buffer.AsSpan(offset, storedSize).CopyTo(block);

            var pokemon = new PKHeX.Core.PK7(block);

            if (!pokemon.ChecksumValid
                || pokemon.Species is <= 0 or > 807
                || pokemon.CurrentLevel is <= 0 or > 100
                || !string.Equals(pokemon.OriginalTrainerName, trainer, StringComparison.Ordinal))
            {
                continue;
            }

            found++;
            Console.WriteLine("  0x" + (region.Start + offset).ToString("X8")
                              + "  #" + pokemon.Species + " " + pokemon.Nickname
                              + "  Nv." + pokemon.CurrentLevel);
        }
    }

    Console.WriteLine("\n" + found + " bloques stored de '" + trainer + "'.");
    return 0;
}


if (Index("--hunt") is { } huntIndex)
{
    // Brute force with no structural assumptions: every 4 byte aligned offset is offered to
    // PKHeX, and only the ones that come back as a real Pokémon belonging to this trainer
    // survive. Sanity is a plaintext field even when the block is encrypted, so it is a cheap
    // pre-filter that removes almost everything before the expensive parse.
    var trainer = args[huntIndex + 1];
    var slotSize = Pk7Reader.PartySize;
    var found = 0;

    foreach (var region in MemorySearch.DefaultRegions)
    {
        Console.WriteLine($"Barriendo {region}...");
        var buffer = new byte[region.Size];
        var read = 0;

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            var chunk = (int)Math.Min(0x1000, region.Size - offset);

            if (client.TryReadMemory(region.Start + offset, chunk, out var data))
            {
                data.CopyTo(buffer.AsSpan((int)offset));
                read += chunk;
            }

            if (offset % 0x100000 == 0)
            {
                Thread.Sleep(1);
            }
        }

        Console.WriteLine($"  {read / 1024} KB en memoria local, analizando...");

        for (var offset = 0; offset + slotSize <= buffer.Length; offset += 4)
        {
            if (BitConverter.ToUInt16(buffer, offset + 4) != 0
                || BitConverter.ToUInt16(buffer, offset + 6) == 0)
            {
                continue;
            }

            var pokemon = new PKHeX.Core.PK7(buffer.AsSpan(offset, slotSize).ToArray());

            if (pokemon.Species is <= 0 or > 807
                || pokemon.CurrentLevel is <= 0 or > 100
                || !string.Equals(pokemon.OriginalTrainerName, trainer, StringComparison.Ordinal))
            {
                continue;
            }

            found++;
            Console.WriteLine($"  0x{region.Start + offset:X8}  #{pokemon.Species} "
                              + $"{pokemon.Nickname,-12} Nv.{pokemon.CurrentLevel} "
                              + $"{pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax} "
                              + $"{(pokemon.IsShiny ? "shiny" : string.Empty)}");
        }
    }

    Console.WriteLine($"\n{found} Pokémon de «{trainer}» encontrados.");
    return 0;
}

if (Index("--read16") is { } read16Index)
{
    foreach (var raw in args[read16Index + 1].Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var target = ParseAddress(raw);

        Console.WriteLine(client.TryReadMemory(target, 2, out var bytes)
            ? "  0x" + target.ToString("X8") + " = " + BitConverter.ToUInt16(bytes)
            : "  0x" + target.ToString("X8") + " ilegible");
    }

    return 0;
}


if (args.Contains("--flag-list"))
{
    // A battle flag holds a small value, not a timer or a coordinate. Comparing the snapshot
    // taken out of battle against memory right now, and keeping only the small ones, turns an
    // unusable list of thousands into something a human can read.
    var snapshotFile = Path.Combine(AppContext.BaseDirectory, "zona.bin");
    var candidateList = Path.Combine(AppContext.BaseDirectory, "zona-candidatos.txt");
    var region = new MemoryRegion(0x33000000, 0x33200000, "estado");

    var outside = File.ReadAllBytes(snapshotFile);
    var current = new byte[region.Size];

    for (var offset = 0u; offset < region.Size; offset += 0x1000)
    {
        var chunk = (int)Math.Min(0x1000, region.Size - offset);

        if (client.TryReadMemory(region.Start + offset, chunk, out var data))
        {
            data.CopyTo(current.AsSpan((int)offset));
        }
    }

    var shown = 0;

    foreach (var flagAddress in File.ReadAllLines(candidateList).Select(uint.Parse))
    {
        var offset = (int)(flagAddress - region.Start);
        var before = BitConverter.ToUInt16(outside, offset);
        var now = BitConverter.ToUInt16(current, offset);

        if (before >= 16 || now >= 16 || before == now)
        {
            continue;
        }

        Console.WriteLine("  0x" + flagAddress.ToString("X8") + "  fuera=" + before + "  dentro=" + now);
        shown++;
    }

    Console.WriteLine("\n" + shown + " candidatos con valores pequeños que alternan.");
    return 0;
}


if (Index("--zone") is { } zoneIndex)
{
    // Búsqueda diferencial de la zona actual, con el filtro que faltaba en el intento de
    // ARCHITECTURE.md §13: descartar lo que cambia mientras el jugador anda DENTRO de la misma
    // zona. Eso se lleva por delante coordenadas, contadores y animaciones, que eran el ruido
    // que dejó 7.830 candidatos y obligó a abandonar.
    //
    //   --zone snap      foto de referencia, estando en la zona A
    //   --zone stable    (tras andar sin salir de A) descarta lo que se mueve solo
    //   --zone changed   (tras pasar a la zona B) se queda con lo que cambió
    //   --zone back      (tras volver a A) se queda con lo que recuperó su valor
    //   --zone show      lista los supervivientes
    var step = args[zoneIndex + 1];
    var region = new MemoryRegion(0x33000000, 0x33200000, "estado");
    var snapshotFile = Path.Combine(AppContext.BaseDirectory, "zona-A.bin");
    var candidateFileZone = Path.Combine(AppContext.BaseDirectory, "zona-candidatos.bin");

    byte[] ReadRegion()
    {
        var buffer = new byte[region.Size];
        var requests = 0;

        for (var offset = 0u; offset < region.Size; offset += 0x1000)
        {
            if (client.TryReadMemory(region.Start + offset, (int)Math.Min(0x1000, region.Size - offset), out var page))
            {
                page.CopyTo(buffer.AsSpan((int)offset));
            }

            if (++requests % 512 == 0)
            {
                Thread.Sleep(1);
            }
        }

        return buffer;
    }

    if (step == "now")
    {
        // El camino de verdad: la mochila como ancla y la zona a un delta fijo, con las cuatro
        // copias teniendo que coincidir. Sin barrer nada.
        var backupFolder = Path.Combine(AppContext.BaseDirectory, "backup");
        var gameWriter = new PermaLocke.GameLink.AzaharGameWriter(client, backupFolder,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermaLocke.GameLink.AzaharGameWriter>.Instance);
        var bagService = new PermaLocke.GameLink.BagService(client, gameWriter,
            Path.Combine(backupFolder, "objetos-retirados.txt"),
            Path.Combine(backupFolder, "mochila.txt"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermaLocke.GameLink.BagService>.Instance);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var zoneService = new PermaLocke.GameLink.ZoneService(bagService, client,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermaLocke.GameLink.ZoneService>.Instance);
        var currentArea = zoneService.CurrentArea();

        Console.WriteLine(bagService.Block is { } anchor
            ? $"mochila (ancla) en 0x{anchor.BaseAddress:X8}"
            : "no se ha encontrado la mochila");

        Console.WriteLine(currentArea is null
            ? "\nzona: NO SE PUEDE AFIRMAR (las copias no concuerdan o la memoria está en blanco)"
            : $"\nzona: área {currentArea} de {ZoneLocator.UltraSunMoonAreaCount}, en {timer.ElapsedMilliseconds} ms");

        if (bagService.Block is { } b)
        {
            Console.WriteLine("\ncopias leídas:");
            foreach (var delta in ZoneLocator.CopyOffsets)
            {
                var at = b.BaseAddress + delta;
                Console.WriteLine(client.TryReadMemory(at, 2, out var raw)
                    ? $"  0x{at:X8} (mochila+0x{delta:X})  = {BitConverter.ToUInt16(raw)}"
                    : $"  0x{at:X8} ilegible");
            }
        }

        return 0;
    }

    var now = ReadRegion();
    var words = (int)(region.Size / 2);

    if (step == "snap")
    {
        File.WriteAllBytes(snapshotFile, now);

        // Al principio vale cualquier posicion: no se supone nada sobre el valor de la zona.
        var all = new byte[words];
        Array.Fill(all, (byte)1);
        File.WriteAllBytes(candidateFileZone, all);

        Console.WriteLine($"Foto tomada de {region}. {words} posiciones de 16 bits en juego.");
        Console.WriteLine("Ahora anda un poco SIN salir de la zona y ejecuta:  Probe --zone stable");
        return 0;
    }

    if (!File.Exists(snapshotFile) || !File.Exists(candidateFileZone))
    {
        Console.WriteLine("No hay foto previa. Ejecuta primero:  Probe --zone snap");
        return 1;
    }

    var reference = File.ReadAllBytes(snapshotFile);
    var candidates = File.ReadAllBytes(candidateFileZone);

    if (step == "show")
    {
        // Se muestran primero los que traducen a un nombre de zona real de Ultra Luna. No es
        // una certeza —el juego podria numerar las zonas de otra manera que PKHeX—, pero si la
        // numeracion coincide, el bueno salta a la vista sin mas rondas.
        var strings = PKHeX.Core.GameInfo.GetStrings("es");
        var named = new List<(uint Address, ushort Now, ushort Before, string Name)>();
        var plain = 0;

        for (var word = 0; word < words; word++)
        {
            if (candidates[word] == 0)
            {
                continue;
            }

            var offset = word * 2;
            var value = BitConverter.ToUInt16(now, offset);
            var before = BitConverter.ToUInt16(reference, offset);
            var name = value == 0
                ? string.Empty
                : strings.GetLocationName(false, value, 7, 7, PKHeX.Core.GameVersion.UM);

            if (!string.IsNullOrWhiteSpace(name))
            {
                named.Add((region.Start + (uint)offset, value, before, name));
            }
            else
            {
                plain++;
            }
        }

        Console.WriteLine($"{named.Count} candidatos con un nombre de zona real, {plain} sin nombre:\n");

        foreach (var (candidateAddress, value, before, name) in named.Take(60))
        {
            Console.WriteLine($"  0x{candidateAddress:X8}  = {value,-5} {name}"
                              + (value == before ? string.Empty : $"   (en la foto valia {before})"));
        }

        Console.WriteLine($"\n{candidates.Count(c => c != 0)} candidatos vivos en total.");
        return 0;
    }

    var survivors = 0;

    for (var word = 0; word < words; word++)
    {
        if (candidates[word] == 0)
        {
            continue;
        }

        var offset = word * 2;
        var same = BitConverter.ToUInt16(now, offset) == BitConverter.ToUInt16(reference, offset);

        // stable y back quieren el valor original; changed quiere que se haya movido.
        var keep = step switch
        {
            "stable" => same,
            "back" => same,
            "changed" => !same,
            _ => throw new ArgumentException($"Paso desconocido: {step}")
        };

        candidates[word] = keep ? (byte)1 : (byte)0;

        if (keep)
        {
            survivors++;
        }
    }

    File.WriteAllBytes(candidateFileZone, candidates);

    Console.WriteLine($"Paso '{step}': quedan {survivors} candidatos.");
    Console.WriteLine(step switch
    {
        "stable" => "Ahora cambia de zona y ejecuta:  Probe --zone changed",
        "changed" => "Ahora vuelve a la zona anterior y ejecuta:  Probe --zone back",
        _ => "Mira los supervivientes con:  Probe --zone show"
    });

    return 0;
}


if (Index("--abilities") is { } abIndex)
{
    var needle = args[abIndex + 1];
    var abilities = PKHeX.Core.GameInfo.GetStrings("es").abilitylist;

    for (var id = 0; id < abilities.Length; id++)
    {
        if (abilities[id].Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  " + id + "  " + abilities[id]);
        }
    }

    return 0;
}


if (Index("--items") is { } itemsIndex)
{
    var needle = args[itemsIndex + 1];
    var itemNames = PKHeX.Core.GameInfo.GetStrings("es").itemlist;

    for (var id = 0; id < itemNames.Length; id++)
    {
        if (itemNames[id].Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  " + id + "  " + itemNames[id]);
        }
    }

    return 0;
}

if (Index("--bag-locate") is { } bagIndex)
{
    // Localiza el bloque entero y dice dónde está un objeto dentro de él, en vez de buscar un
    // valor suelto por la memoria, que era lo que daba falsos positivos.
    var itemId = int.Parse(args[bagIndex + 1]);
    var locator = new BagLocator(client);
    var itemNames = PKHeX.Core.GameInfo.GetStrings("es").itemlist;

    foreach (var block in locator.LocateAll())
    {
        Console.WriteLine("Bloque 0x" + block.BaseAddress.ToString("X8")
                          + "  tabla 0x" + block.PointerTableAddress.ToString("X8"));

        var slot = locator.Find(block, itemId);

        Console.WriteLine(slot is null
            ? "  el jugador no lleva " + itemNames[itemId]
            : "  " + itemNames[itemId] + " x" + slot.Entry.Count
              + " en 0x" + slot.Address.ToString("X8")
              + " (" + slot.Pocket.Type + ", hueco " + slot.Index + ")");
    }

    return 0;
}


if (Index("--pattern") is { } patternIndex)
{
    // Searches only the regions Azahar lets us write to. Finding the bag anywhere else would
    // be useless for the rule that has to take Poke Balls away.
    var pattern = Convert.FromHexString(args[patternIndex + 1]);

    var regions = new[]
    {
        new MemoryRegion(0x08000000, 0x10000000, "heap"),
        new MemoryRegion(0x30000000, 0x40000000, "linear")
    };

    var hitFile = Path.Combine(AppContext.BaseDirectory, "patron.txt");
    var allHits = new List<uint>();

    foreach (var region in regions)
    {
        Console.WriteLine("Buscando " + Convert.ToHexString(pattern) + " en " + region + "...");
        var hits = 0;

        for (var offset = 0u; offset < region.Size; offset += 0x1000 - (uint)pattern.Length)
        {
            var blockSize = (int)Math.Min(0x1000, region.Size - offset);

            if (!client.TryReadMemory(region.Start + offset, blockSize, out var block))
            {
                continue;
            }

            for (var i = 0; i + pattern.Length <= block.Length; i += 2)
            {
                if (block.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                {
                    var hit = region.Start + offset + (uint)i;
                    Console.WriteLine("  0x" + hit.ToString("X8"));
                    allHits.Add(hit);
                    hits++;
                }
            }
        }

        Console.WriteLine("  " + hits + " coincidencias.");
    }

    File.WriteAllLines(hitFile, allHits.Select(h => h.ToString()));
    Console.WriteLine("Guardadas en " + Path.GetFileName(hitFile));
    return 0;
}


if (Index("--mirror") is { } mirrorIndex)
{
    // Azahar only accepts writes to a whitelist of regions, and the new linear heap where the
    // party lives is not on it. The same physical memory is mapped elsewhere too, so this
    // finds the writable alias by searching for the exact bytes instead of assuming an offset.
    var source = ParseAddress(args[mirrorIndex + 1]);

    if (!client.TryReadMemory(source, 32, out var signature))
    {
        Console.WriteLine("No se pudo leer la firma.");
        return 1;
    }

    Console.WriteLine("Firma en 0x" + source.ToString("X8") + ": " + Convert.ToHexString(signature));

    var regions = new[]
    {
        new MemoryRegion(0x08000000, 0x0E000000, "heap"),
        new MemoryRegion(0x14000000, 0x1C000000, "linear clasico")
    };

    foreach (var region in regions)
    {
        Console.WriteLine("Buscando en " + region + "...");

        for (var offset = 0u; offset < region.Size; offset += 0x1000 - 32)
        {
            var blockSize = (int)Math.Min(0x1000, region.Size - offset);

            if (!client.TryReadMemory(region.Start + offset, blockSize, out var block))
            {
                continue;
            }

            for (var i = 0; i + signature.Length <= block.Length; i += 4)
            {
                if (block.AsSpan(i, signature.Length).SequenceEqual(signature))
                {
                    var found = region.Start + offset + (uint)i;
                    Console.WriteLine("  ENCONTRADA en 0x" + found.ToString("X8")
                                      + "   delta = 0x" + (source - found).ToString("X8"));
                }
            }
        }
    }

    return 0;
}


if (Index("--set-ability") is { } setAbIndex)
{
    var target = ParseAddress(args[setAbIndex + 1]);
    var abilityId = int.Parse(args[setAbIndex + 2]);
    var partySize = Pk7Reader.PartySize;

    if (!client.TryReadMemory(target, partySize, out var original))
    {
        Console.WriteLine("No se pudo leer.");
        return 1;
    }

    var reference = new byte[partySize];
    new PKHeX.Core.PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

    var working = new PKHeX.Core.PK7((byte[])original.Clone());
    Console.WriteLine("Antes: habilidad=" + working.Ability + " slot=" + working.AbilityNumber);

    working.Ability = abilityId;
    working.RefreshChecksum();

    var modified = new byte[partySize];
    working.WriteEncryptedDataParty(modified);

    for (var i = 0; i < partySize; i++)
    {
        if (modified[i] != reference[i])
        {
            client.WriteMemory((uint)(target + i), new[] { modified[i] });
        }
    }

    client.TryReadMemory(target, partySize, out var after);
    Console.WriteLine("Después: habilidad=" + new PKHeX.Core.PK7(after).Ability);
    return 0;
}


if (Index("--capfix") is { } capIndex)
{
    // The three rules the run defines -breaking the cap with rare candies, gaining experience
    // while at the cap, and levelling past it- all end the same way: back at the cap with no
    // progress into the next level. Setting CurrentLevel does exactly that, because PKHeX
    // recomputes EXP to the minimum for that level.
    var partyBase = ParseAddress(args[capIndex + 1]);
    var cap = int.Parse(args[capIndex + 2]);
    var stride = args.Length > capIndex + 3 ? ParseAddress(args[capIndex + 3]) : 0x1E4u;
    var partySize = Pk7Reader.PartySize;

    for (var slot = 0; slot < 6; slot++)
    {
        var target = (uint)(partyBase + slot * stride);

        if (!client.TryReadMemory(target, partySize, out var original))
        {
            break;
        }

        var pokemon = new PKHeX.Core.PK7((byte[])original.Clone());

        if (!PartyLocator.IsPlausible(pokemon) && !pokemon.ChecksumValid)
        {
            break;
        }

        if (pokemon.CurrentLevel <= cap)
        {
            Console.WriteLine("  slot " + slot + "  " + pokemon.Nickname
                              + " Nv." + pokemon.CurrentLevel + "  dentro del cap");
            continue;
        }

        var backup = Path.Combine(AppContext.BaseDirectory, "backup-" + target.ToString("X8") + ".bin");

        if (!File.Exists(backup))
        {
            File.WriteAllBytes(backup, original);
        }

        var reference = new byte[partySize];
        new PKHeX.Core.PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

        var before = pokemon.CurrentLevel;
        pokemon.CurrentLevel = (byte)cap;
        pokemon.RefreshChecksum();

        var modified = new byte[partySize];
        pokemon.WriteEncryptedDataParty(modified);

        var written = 0;

        for (var i = 0; i < partySize; i++)
        {
            if (modified[i] != reference[i])
            {
                client.WriteMemory((uint)(target + i), new[] { modified[i] });
                written++;
            }
        }

        Console.WriteLine("  slot " + slot + "  " + pokemon.Nickname
                          + "  Nv." + before + " -> Nv." + cap
                          + "  (EXP " + pokemon.EXP + ", " + written + " bytes)");
    }

    Console.WriteLine("\nEl nivel mostrado no cambia hasta entrar y salir de un combate.");
    return 0;
}


if (Index("--kill") is { } killIndex)
{
    // The death transformation the run demands: Shedinja, Wonder Guard, level 1, no moves,
    // nicknamed MUERTO. Everything here lives inside the checksummed stored block, which is
    // why it can be written at all. The original bytes are backed up first.
    var target = ParseAddress(args[killIndex + 1]);
    var partySize = Pk7Reader.PartySize;

    if (!client.TryReadMemory(target, partySize, out var original))
    {
        Console.WriteLine("No se pudo leer.");
        return 1;
    }

    var backup = Path.Combine(AppContext.BaseDirectory, "backup-" + target.ToString("X8") + ".bin");

    if (!File.Exists(backup))
    {
        File.WriteAllBytes(backup, original);
    }

    var reference = new byte[partySize];
    new PKHeX.Core.PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

    var dead = new PKHeX.Core.PK7((byte[])original.Clone());
    Console.WriteLine("Antes: " + dead.Nickname + " #" + dead.Species + " Nv." + dead.CurrentLevel);

    dead.Species = (int)PKHeX.Core.Species.Shedinja;
    dead.Form = 0;

    // Ability 0: the game shows "-", no ability at all. Verified in game.
    dead.Ability = 0;

    dead.CurrentLevel = 1;

    dead.Move1 = dead.Move2 = dead.Move3 = dead.Move4 = 0;
    dead.Move1_PP = dead.Move2_PP = dead.Move3_PP = dead.Move4_PP = 0;
    dead.Move1_PPUps = dead.Move2_PPUps = dead.Move3_PPUps = dead.Move4_PPUps = 0;

    dead.Nickname = "MUERTO";
    dead.IsNicknamed = true;

    dead.RefreshChecksum();

    var modified = new byte[partySize];
    dead.WriteEncryptedDataParty(modified);

    var written = 0;

    for (var i = 0; i < partySize; i++)
    {
        if (modified[i] != reference[i])
        {
            client.WriteMemory((uint)(target + i), new[] { modified[i] });
            written++;
        }
    }

    client.TryReadMemory(target, partySize, out var after);
    var check = new PKHeX.Core.PK7(after);

    Console.WriteLine("Después: " + check.Nickname + " #" + check.Species
                      + " Nv." + check.CurrentLevel
                      + " habilidad=" + check.Ability
                      + " movs=" + check.Move1 + "," + check.Move2 + "," + check.Move3 + "," + check.Move4);
    Console.WriteLine(written + " bytes escritos. Copia de seguridad en " + Path.GetFileName(backup));
    return 0;
}


if (Index("--rename") is { } renameIndex)
{
    // Renaming touches only the checksummed stored block, which PKHeX can rewrite correctly.
    // The original bytes are saved next to the tool first, so every change is reversible.
    var target = ParseAddress(args[renameIndex + 1]);
    var newName = args[renameIndex + 2];
    var partySize = Pk7Reader.PartySize;

    if (!client.TryReadMemory(target, partySize, out var original))
    {
        Console.WriteLine("No se pudo leer.");
        return 1;
    }

    var backup = Path.Combine(AppContext.BaseDirectory, "backup-" + target.ToString("X8") + ".bin");

    if (!File.Exists(backup))
    {
        File.WriteAllBytes(backup, original);
    }

    var reference = new byte[partySize];
    new PKHeX.Core.PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

    var working = new PKHeX.Core.PK7((byte[])original.Clone());
    Console.WriteLine("Antes: " + working.Nickname + " (#" + working.Species + ")");

    working.Nickname = newName;
    working.IsNicknamed = true;
    working.RefreshChecksum();

    var modified = new byte[partySize];
    working.WriteEncryptedDataParty(modified);

    var written = 0;

    for (var i = 0; i < partySize; i++)
    {
        if (modified[i] != reference[i])
        {
            client.WriteMemory((uint)(target + i), new[] { modified[i] });
            written++;
        }
    }

    client.TryReadMemory(target, partySize, out var after);
    Console.WriteLine("0x" + target.ToString("X8") + ": " + written + " bytes escritos -> "
                      + new PKHeX.Core.PK7(after).Nickname);
    return 0;
}

if (Index("--restore") is { } restoreIndex)
{
    var target = ParseAddress(args[restoreIndex + 1]);
    var backup = Path.Combine(AppContext.BaseDirectory, "backup-" + target.ToString("X8") + ".bin");

    if (!File.Exists(backup))
    {
        Console.WriteLine("No hay copia de seguridad de esa direccion.");
        return 1;
    }

    var bytes = File.ReadAllBytes(backup);
    client.WriteMemory(target, bytes);
    client.TryReadMemory(target, bytes.Length, out var after);
    Console.WriteLine("0x" + target.ToString("X8") + " restaurado -> "
                      + new PKHeX.Core.PK7(after).Nickname);
    return 0;
}


if (Index("--pokehex") is { } pokeHexIndex)
{
    // Writes a raw byte sequence, backing up what was there first. Used to tell copies apart
    // by giving each one a different value and seeing which one the game obeys.
    var target = ParseAddress(args[pokeHexIndex + 1]);
    var bytes = Convert.FromHexString(args[pokeHexIndex + 2]);

    if (!client.TryReadMemory(target, bytes.Length, out var before))
    {
        Console.WriteLine("No se pudo leer.");
        return 1;
    }

    var backup = Path.Combine(AppContext.BaseDirectory, "raw-" + target.ToString("X8") + ".bin");

    if (!File.Exists(backup))
    {
        File.WriteAllBytes(backup, before);
    }

    client.WriteMemory(target, bytes);
    client.TryReadMemory(target, bytes.Length, out var after);

    Console.WriteLine("0x" + target.ToString("X8") + ": " + Convert.ToHexString(before)
                      + " -> " + Convert.ToHexString(after));
    return 0;
}


if (Index("--poke") is { } pokeIndex)
{
    // Persistent single-byte write, for checking whether a candidate address is the one the
    // game actually reads from. Unlike --write-probe it does not restore the old value.
    var target = ParseAddress(args[pokeIndex + 1]);
    var value = byte.Parse(args[pokeIndex + 2]);

    client.TryReadMemory(target, 1, out var before);
    client.WriteMemory(target, new[] { value });
    client.TryReadMemory(target, 1, out var after);

    Console.WriteLine("0x" + target.ToString("X8") + ": " + before[0] + " -> " + after[0]);
    return 0;
}


if (Index("--write-probe") is { } wpIndex)
{
    // Distinguishes "the write never lands" from "the write lands and the game overwrites it".
    // The byte is read back immediately and then several times over the next second.
    var target = ParseAddress(args[wpIndex + 1]);
    var value = byte.Parse(args[wpIndex + 2]);

    client.TryReadMemory(target, 1, out var before);
    Console.WriteLine("Valor original: " + before[0]);

    client.WriteMemory(target, new[] { value });

    for (var attempt = 0; attempt < 6; attempt++)
    {
        client.TryReadMemory(target, 1, out var now);
        Console.WriteLine("  lectura " + attempt + " (+" + (attempt * 200) + " ms): " + now[0]);

        if (attempt < 5)
        {
            Thread.Sleep(200);
        }
    }

    client.WriteMemory(target, before);
    client.TryReadMemory(target, 1, out var restored);
    Console.WriteLine("Restaurado a: " + restored[0]);
    return 0;
}


if (Index("--write-hp") is { } hpIndex)
{
    // Proves that WriteMemory changes MEMORY. Current HP is the safest target: it lives in the
    // party stats tail, outside the checksummed block, so nothing has to be recomputed.
    //
    // MEDIDO EL 2026-09-04, y la respuesta es la contraria de la que este comentario daba por
    // hecha: el menu del juego NO lo enseña. Con el equipo en 0x330128E4 se escribio 7 en el
    // Gyarados del hueco 0 -un solo byte, offset 0xF0-, se releyo 7/131, aguanto quince segundos
    // sin que el juego lo pisara, y la pantalla del equipo seguia marcando 131/131 con la barra
    // llena. O sea que los PS que el juego PINTA no salen de esta copia.
    //
    // Encaja con el §53: la copia autoritativa es la de salto 0x1E4 y guarda las estadisticas de
    // combate en otro sitio. Lo que si obedece esta copia es el bloque cifrado -especie, mote,
    // nivel-, que es justo lo que el marcador de muerte escribe y por eso ese si se ve.
    //
    // Consecuencia practica: NO se puede dejar a un Pokemon clavado a 0 PS por aqui. Haria falta
    // localizar donde guarda los PS actuales la estructura de 0x1E4, que es una investigacion del
    // tamaño del §22 y no un ajuste.
    var partyStart = ParseAddress(args[hpIndex + 1]);
    var slot = int.Parse(args[hpIndex + 2]);
    var wanted = int.Parse(args[hpIndex + 3]);

    var slotSize = Pk7Reader.PartySize;
    var slotAddress = (uint)(partyStart + slot * slotSize);

    if (!client.TryReadMemory(slotAddress, slotSize, out var original))
    {
        Console.WriteLine("No se pudo leer el hueco.");
        return 1;
    }

    var pokemon = new PKHeX.Core.PK7(original);
    Console.WriteLine("Antes: " + pokemon.Nickname + "  " + pokemon.Stat_HPCurrent + "/" + pokemon.Stat_HPMax);

    // The block is stored ENCRYPTED in memory, so both sides of the comparison have to be in
    // that same form. Diffing the decrypted view produced an offset that does not exist in
    // the game and corrupted an unrelated byte.
    // PK7 also wraps the array it is given instead of copying it, hence the clones.
    var reference = new byte[slotSize];
    new PKHeX.Core.PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

    var working = new PKHeX.Core.PK7((byte[])original.Clone());
    working.Stat_HPCurrent = wanted;
    working.RefreshChecksum();

    var modified = new byte[slotSize];
    working.WriteEncryptedDataParty(modified);


    var changed = new List<int>();

    for (var i = 0; i < slotSize; i++)
    {
        if (modified[i] != reference[i])
        {
            changed.Add(i);
        }
    }

    Console.WriteLine("Bytes que cambian: " + string.Join(", ", changed.Select(i => "0x" + i.ToString("X"))));

    if (changed.Count == 0)
    {
        Console.WriteLine("No hay nada que escribir.");
        return 2;
    }

    foreach (var offset in changed)
    {
        client.WriteMemory((uint)(slotAddress + offset), new[] { modified[offset] });
    }

    if (client.TryReadMemory(slotAddress, slotSize, out var after))
    {
        var reread = new PKHeX.Core.PK7(after);
        Console.WriteLine("Después: " + reread.Nickname + "  " + reread.Stat_HPCurrent + "/" + reread.Stat_HPMax);
        Console.WriteLine(reread.Stat_HPCurrent == wanted
            ? "LA MEMORIA HA CAMBIADO. Ahora abre el menú del juego y comprueba si lo refleja."
            : "El valor NO ha cambiado en memoria.");
    }

    return 0;
}


if (Index("--met") is { } metIndex)
{
    // A caught Pokémon records where it was met, which is exactly the zone the capture rules
    // need. No memory search for the current map is required at all.
    var partyStart = ParseAddress(args[metIndex + 1]);
    var strings = PKHeX.Core.GameInfo.GetStrings("es");
    var slotSize = Pk7Reader.PartySize;

    for (var slot = 0; slot < 6; slot++)
    {
        var slotAddress = (uint)(partyStart + slot * slotSize);

        if (!client.TryReadMemory(slotAddress, slotSize, out var bytes))
        {
            break;
        }

        var pokemon = new PKHeX.Core.PK7(bytes);

        if (!PartyLocator.IsPlausible(pokemon))
        {
            break;
        }

        var place = strings.GetLocationName(false, (ushort)pokemon.MetLocation, 7, 7,
            PKHeX.Core.GameVersion.UM);

        Console.WriteLine("  slot " + slot + "  #" + pokemon.Species + " " + pokemon.Nickname
                          + "  Nv." + pokemon.CurrentLevel
                          + "  encontrado en [" + pokemon.MetLocation + "] " + place
                          + "  bola=" + pokemon.Ball + "  nivel de encuentro=" + pokemon.MetLevel);
    }

    return 0;
}


if (Index("--party") is { } partyIndex)
{
    var partyAddress = ParseAddress(args[partyIndex + 1]);
    var reader = new Pk7Reader(client);

    // --vivo lee la estructura que el juego mira de verdad, cuyas estadisticas estan en 0x158 y
    // cuyas entradas van cada 0x1E4. Sin esto solo se sabia leer el espejo, que va con retraso.
    var live = args.Contains("--vivo");
    var partyStride = live ? PartyLayoutLocator.AuthoritativeStride : (uint)Pk7Reader.PartySize;
    var partyStats = live ? PartyLayoutLocator.AuthoritativeStatsOffset : (uint?)null;

    Console.WriteLine($"Equipo desde 0x{partyAddress:X8} (huecos de 0x{partyStride:X} bytes):\n");

    for (var slot = 0; slot < 6; slot++)
    {
        var slotAddress = (uint)(partyAddress + (slot * partyStride));
        var member = reader.TryRead(slotAddress, partyStats);

        Console.WriteLine(member is null
            ? $"  {slot}: 0x{slotAddress:X8}  vacío o ilegible"
            : $"  {slot}: 0x{slotAddress:X8}  #{member.Species} {member.Nickname,-12} "
              + $"Nv.{member.Level,-3} {member.CurrentHp}/{member.MaxHp}  OT:{member.TrainerName}");
    }

    return 0;
}

if (args.Contains("--write-test"))
{
    // Writes back the exact bytes just read, so the game state is unchanged either way.
    const uint probeAddress = 0x00100000;

    var before = client.ReadMemory(probeAddress, 4);
    client.WriteMemory(probeAddress, before);
    var after = client.ReadMemory(probeAddress, 4);

    Console.WriteLine($"Antes: {Convert.ToHexString(before)}   Después: {Convert.ToHexString(after)}");
    Console.WriteLine(before.AsSpan().SequenceEqual(after)
        ? "ESCRITURA OK · el servidor aceptó la petición."
        : "ATENCIÓN · el valor ha cambiado, la escritura no es fiable.");
    return 0;
}

var address = Index("--dump") is { } dumpIndex
    ? ParseAddress(args[dumpIndex + 1])
    : 0x00100000u;

var size = Index("--dump") is { } d && args.Length > d + 2 ? int.Parse(args[d + 2]) : 32;

try
{
    Dump(address, client.ReadMemory(address, size));
}
catch (Exception ex)
{
    Console.WriteLine($"La lectura falló: {ex.Message}");
    return 2;
}

return 0;

int? Index(string flag)
{
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? index : null;
}

uint ParseAddress(string value) =>
    uint.Parse(value.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase),
        NumberStyles.HexNumber);

List<MemoryRegion> ReadableRegions()
{
    Console.WriteLine("Mapeando regiones legibles...");

    // La imagen del proceso -codigo, .data y .bss- no esta en DefaultRegions porque todo lo que se
    // ha localizado hasta hoy vivia en el monton o en linear. Para un barrido a ciegas eso es una
    // suposicion, no un limite: si el valor que se busca es una global, esta aqui y en ningun otro
    // sitio. Se anade solo en la sonda, que es donde se busca lo que todavia no se sabe donde esta.
    MemoryRegion[] wider = [..MemorySearch.DefaultRegions, new MemoryRegion(0x00100000, 0x01000000, "imagen")];

    var regions = wider.SelectMany(search.MapReadableRanges).ToList();

    foreach (var region in regions)
    {
        Console.WriteLine($"  {region}");
    }

    Console.WriteLine();
    return regions;
}

void Dump(uint start, byte[] data)
{
    Console.WriteLine($"Lectura de {data.Length} bytes en 0x{start:X8}:\n");

    for (var offset = 0; offset < data.Length; offset += 16)
    {
        var line = data.AsSpan(offset, Math.Min(16, data.Length - offset)).ToArray();
        var hex = string.Join(' ', line.Select(b => b.ToString("X2")));
        var text = new string([.. line.Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.')]);
        Console.WriteLine($"{start + offset:X8}  {hex,-47}  {text}");
    }
}
