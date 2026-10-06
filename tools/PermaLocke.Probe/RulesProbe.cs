using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Probe;

/// <summary>
/// <c>--reglas-juego [quitar] [carpeta del mod]</c>: puts or takes away the rules patched into the game
/// (<see cref="RulePatches"/>, 2026-10-06) by hand, to try them before turning them on for everyone in rules.json.
/// </summary>
/// <remarks>
/// Without a folder, the mod of the Azahar this PermaLocke finds. Refuses while Azahar is running. The cap itself only
/// reaches the game with PermaLocke open and <c>gameRulePatches</c> on: with the block empty the patched game behaves as
/// the cartridge does.
/// </remarks>
internal static class RulesProbe
{
    public static int Run(string[] args)
    {
        // --reglas-juego cap 20: el cap en el bloque del juego abierto, a mano, sin encender la regla para nadie.
        if (args.Length >= 2 && args[0] == "cap" && int.TryParse(args[1], out var cap))
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var writer = new AzaharGameWriter(client, Path.GetTempPath(), NullLogger<AzaharGameWriter>.Instance);
            Console.WriteLine(writer.WriteRuleCap(cap)
                ? $"Cap {cap} escrito en 0x{PermaLocke.Core.Domain.RuleBlock.Cap:X8}. Vale hasta reiniciar el juego."
                : "No se ha podido escribir: ¿está el juego abierto con el Azahar de PermaLocke?");
            return 0;
        }

        // --reglas-juego caidos 33F807C4 5 [3 ...]: esos huecos del equipo, por su constante de cifrado, en la lista de caídos
        // del juego abierto. Sin huecos, la lista se vacía.
        if (args.Length >= 2 && args[0] == "caidos")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var at = Convert.ToUInt32(args[1].Replace("0x", ""), 16);
            var constants = args.Skip(2).Select(int.Parse).Select(slot =>
                BitConverter.ToUInt32(client.ReadMemory(at + (uint)slot * PermaLocke.GameLink.Data.PartyLayoutLocator.AuthoritativeStride, 4)))
                .ToList();
            var writer = new AzaharGameWriter(client, Path.GetTempPath(), NullLogger<AzaharGameWriter>.Instance);
            Console.WriteLine(writer.WriteRuleFallen(constants)
                ? $"Lista de caídos: {string.Join(", ", constants.Select(c => $"0x{c:X8}"))}"
                : "No se ha podido escribir la lista.");
            return 0;
        }

        // --reglas-juego duplicados 10 11 12: esas especies, repetidas para el juego abierto. Con «menos», todas salvo esas
        // (en una ruta donde salga una de ellas, solo sale esa). Sin especies, la lista se vacía.
        if (args.Length >= 1 && args[0] == "duplicados")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var except = args.Length >= 2 && args[1] == "menos";
            var given = args.Skip(except ? 2 : 1).Select(int.Parse).ToHashSet();
            var species = except
                ? Enumerable.Range(1, PermaLocke.Core.Domain.RuleBlock.DupesSpeciesLimit - 1).Where(s => !given.Contains(s)).ToList()
                : given.ToList();
            var writer = new AzaharGameWriter(client, Path.GetTempPath(), NullLogger<AzaharGameWriter>.Instance);
            Console.WriteLine(writer.WriteRuleDupes(species)
                ? $"Duplicados para el juego: {species.Count} especies"
                : "No se ha podido escribir la lista.");
            return 0;
        }

        // --reglas-juego balls 8: el motivo con el que el menú de combate rechaza las balls (0 = se pueden lanzar).
        if (args.Length >= 2 && args[0] == "balls" && byte.TryParse(args[1], out var reason))
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var writer = new AzaharGameWriter(client, Path.GetTempPath(), NullLogger<AzaharGameWriter>.Instance);
            Console.WriteLine(writer.WriteRuleBallRefusal(reason)
                ? $"Motivo para no lanzar balls: {reason}"
                : "No se ha podido escribir el motivo.");
            return 0;
        }

        // --reglas-juego zona: la zona en la que está el jugador según el propio juego (GameData::GetNowZoneID), sin barrer.
        if (args.Length >= 1 && args[0] == "zona")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var manager = BitConverter.ToUInt32(client.ReadMemory(0x006A3984, 4));
            var data = manager == 0 ? 0 : BitConverter.ToUInt32(client.ReadMemory(manager + 0x24, 4));
            if (data == 0)
            {
                Console.WriteLine($"Sin GameData (GameManager 0x{manager:X8})");
                return 0;
            }

            var zone = BitConverter.ToUInt16(client.ReadMemory(data + 0x62, 2));
            var map = PermaLocke.Data.JsonMapTable.Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Data", "mapas.json")).For(zone);
            Console.WriteLine($"GameManager 0x{manager:X8}, GameData 0x{data:X8}, zona {zone}: {map?.LocationName ?? "sin ruta en mapas.json"}");
            return 0;
        }

        // --reglas-juego partida [desfase]: compara cada bloque del fichero de partida con la memoria a partir de [GameData+4]
        // (+ desfase, hex). Recién guardado, los bloques que el juego no toca sin jugar tienen que coincidir enteros.
        if (args.Length >= 1 && args[0] == "partida")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var playerSave = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
                args.FirstOrDefault(a => a.Contains('\\') || a.Contains('/')) ?? AppContext.BaseDirectory);
            if (playerSave.Find() is not { } path || !PKHeX.Core.SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not PKHeX.Core.SAV7USUM game)
            {
                Console.WriteLine("No se lee la partida.");
                return 1;
            }

            var manager = BitConverter.ToUInt32(client.ReadMemory(0x006A3984, 4));
            var data = BitConverter.ToUInt32(client.ReadMemory(manager + 0x24, 4));
            var pointer = BitConverter.ToUInt32(client.ReadMemory(data + 4, 4));
            var shift = args.Length >= 2 ? (args[1].StartsWith('-') ? -1 : 1) * Convert.ToInt32(args[1].TrimStart('-').Replace("0x", ""), 16) : 0;
            var start = (uint)(pointer + shift);
            Console.WriteLine($"Partida {path} ({File.GetLastWriteTime(path):HH:mm:ss}); GameData+4 = 0x{pointer:X8}, base 0x{start:X8}");

            var file = game.Data.ToArray();
            var blocks = game.AllBlocks;

            // --reglas-juego partida contadores: dónde están los récords (contadores) respecto a la base: los 100 primeros
            // como u32, contados los que no son cero y valen lo mismo que en el fichero recién guardado.
            if (args.Contains("contadores"))
            {
                var window = new byte[0x80000]; for (var c = 0; c < 0x80000; c += 0x2000) if (client.TryReadMemory(start + (uint)c, 0x2000, out var p)) p.CopyTo(window, c);
                var best = Enumerable.Range(0, (0x80000 - 400) / 4).Select(i => i * 4)
                    .Select(o => (Offset: o, Same: Enumerable.Range(0, 100).Count(r =>
                        game.GetRecord(r) != 0 && BitConverter.ToInt32(window, o + r * 4) == game.GetRecord(r))))
                    .OrderByDescending(x => x.Same).Take(3);
                var nonZero = Enumerable.Range(0, 100).Count(r => game.GetRecord(r) != 0);
                foreach (var (offset, same) in best)
                    Console.WriteLine($"base +0x{offset:X5}: {same} de {nonZero} récords iguales");
                Console.WriteLine($"récord 4 (combates salvajes) en el fichero: {game.GetRecord(4)}, récord 6 (capturas): {game.GetRecord(6)}");
                return 0;
            }

            // --reglas-juego partida localizar 6 28: para cada bloque, el desplazamiento desde la base donde más bytes no nulos
            // del fichero coinciden con la memoria (una sola lectura de 0x80000 bytes).
            if (args.Contains("localizar"))
            {
                var memory = new byte[0x80000];
                for (var c = 0; c < memory.Length; c += 0x2000)
                    if (client.TryReadMemory(start + (uint)c, 0x2000, out var p)) p.CopyTo(memory, c);

                foreach (var n in args.SkipWhile(a => a != "localizar").Skip(1).Where(a => int.TryParse(a, out _)).Select(int.Parse))
                {
                    var block = file.AsSpan(blocks[n].Offset, Math.Min(blocks[n].Length, 0x2000)).ToArray();
                    var meaningful = block.Count(b => b != 0);
                    var scores = new List<(int Offset, int Same)>();
                    for (var o = 0; o + block.Length <= memory.Length; o += 4)
                    {
                        var same = 0;
                        for (var k = 0; k < block.Length; k++)
                            if (block[k] != 0 && memory[o + k] == block[k]) same++;
                        scores.Add((o, same));
                    }

                    var top = scores.OrderByDescending(s => s.Same).Take(3).ToList();
                    Console.WriteLine($"bloque {n} (fichero 0x{blocks[n].Offset:X6}, {meaningful} bytes no nulos): "
                                      + string.Join(", ", top.Select(t => $"+0x{t.Offset:X5} ({t.Same})")));
                }

                return 0;
            }

            // --reglas-juego partida buscar: una sola lectura de 0x80000 bytes desde la base y, para cada bloque, dónde
            // aparece en ella una firma de 32 bytes de su principio (la primera ventana con al menos 12 bytes no nulos).
            if (args.Contains("buscar"))
            {
                const int Window = 0x80000, Chunk = 0x2000;
                var memory = new byte[Window];
                for (var at = 0; at < Window; at += Chunk)
                {
                    if (client.TryReadMemory(start + (uint)at, Chunk, out var piece)) piece.CopyTo(memory, at);
                }

                for (var i = 0; i < blocks.Count; i++)
                {
                    var k = Enumerable.Range(0, Math.Max(1, Math.Min(blocks[i].Length, 0x400) - 32))
                        .FirstOrDefault(o => file.AsSpan(blocks[i].Offset + o, 32).ToArray().Count(b => b != 0) >= 12, -1);
                    if (k < 0)
                    {
                        Console.WriteLine($"bloque {i,2} 0x{blocks[i].Offset:X6}: sin firma");
                        continue;
                    }

                    var signature = file.AsSpan(blocks[i].Offset + k, 32).ToArray();
                    var hits = new List<int>();
                    for (var at = 0; at + 32 <= Window && hits.Count < 4; at += 4)
                    {
                        if (memory.AsSpan(at, 32).SequenceEqual(signature)) hits.Add(at - k);
                    }

                    Console.WriteLine($"bloque {i,2} fichero 0x{blocks[i].Offset:X6} +0x{blocks[i].Length:X5}: memoria "
                                      + (hits.Count == 0 ? "no encontrado" : string.Join(", ", hits.Select(h => $"+0x{h:X6}"))));
                }

                return 0;
            }

            for (var i = 0; i < blocks.Count; i++)
            {
                var length = Math.Min(blocks[i].Length, 0x1000);
                if (!client.TryReadMemory(start + (uint)blocks[i].Offset, length, out var live))
                {
                    Console.WriteLine($"bloque {i,2} 0x{blocks[i].Offset:X6}: no se lee");
                    continue;
                }

                var same = Enumerable.Range(0, length).Count(k => live[k] == file[blocks[i].Offset + k]);
                Console.WriteLine($"bloque {i,2} 0x{blocks[i].Offset:X6} +0x{blocks[i].Length:X5}: {100.0 * same / length,5:F1} % igual");
            }

            return 0;
        }

        // --reglas-juego pokedex [carpeta]: capturados según el fichero y según el juego abierto (LiveSave), y los contadores.
        if (args.Length >= 1 && args[0] == "pokedex")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var playerSave = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
                args.Length >= 2 ? args[1] : AppContext.BaseDirectory);
            var cache = new PermaLocke.GameLink.Field.SavedGameCache(playerSave);
            var live = new PermaLocke.GameLink.Field.LiveSave(client, cache);
            if (live.Load(PermaLocke.GameLink.Field.LiveSave.ZukanBlock, PermaLocke.GameLink.Field.LiveSave.RecordBlock) is not { } both)
            {
                Console.WriteLine("No se lee la partida.");
                return 1;
            }

            IEnumerable<int> Caught(PKHeX.Core.SAV7USUM s) => Enumerable.Range(1, s.MaxSpeciesID).Where(n => s.GetCaught((ushort)n));
            var file = Caught(both.File).ToHashSet();
            var now = Caught(both.Live).ToHashSet();
            Console.WriteLine($"Base de la partida en memoria: 0x{live.Base():X8}");
            var bag = new PermaLocke.GameLink.Data.BagBlock(live.Base()!.Value + BagService.BagFromSave, PermaLocke.GameLink.Data.BagLayout.UltraSunMoon);
            Console.WriteLine($"Mochila en 0x{bag.BaseAddress:X8}: " + (new PermaLocke.GameLink.Data.BagLocator(client).StillValid(bag) ? "válida" : "NO valida"));
            Console.WriteLine($"Capturados: fichero {file.Count}, juego {now.Count}; nuevos sin guardar: "
                              + string.Join(", ", now.Except(file)) + (file.IsSubsetOf(now) ? "" : "  (¡el juego ha perdido alguno!)"));
            Console.WriteLine($"Combates salvajes: fichero {both.File.GetRecord(4)}, juego {both.Live.GetRecord(4)}; "
                              + $"capturas: fichero {both.File.GetRecord(6)}, juego {both.Live.GetRecord(6)}");
            return 0;
        }

        // --reglas-juego combate [hex1 hex2...]: dentro de un combate, las tablas como las encuentra la app y quién guarda
        // la dirección de cada una y de su primer y último bloque: una búsqueda de un valor por megabyte en las ventanas dadas
        // (por defecto la de las tablas, 0x30000000, y la de GameData, 0x33F00000).
        if (args.Length >= 1 && args[0] == "combate")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var reader = new PermaLocke.GameLink.Battle.BattleTableReader(client, NullLogger<PermaLocke.GameLink.Battle.BattleTableReader>.Instance);
            var tables = reader.Read(DateTimeOffset.UtcNow);
            if (tables.Count == 0)
            {
                Console.WriteLine("No hay combate (o sus tablas no están en el megabyte de siempre).");
                return 0;
            }

            var windows = args.Skip(1).Select(a => Convert.ToUInt32(a.Replace("0x", ""), 16)).DefaultIfEmpty(0x30000000u).ToList();
            if (args.Length == 1) windows.Add(0x33F00000);

            foreach (var table in tables)
            {
                Console.WriteLine($"tabla 0x{table.Origin:X8}: " + string.Join(", ", table.Blocks.Select(b =>
                    $"[{b.BattleId}] especie {b.Species} PS {b.CurrentHp}/{b.MaxHp} en 0x{b.Address:X8}")));

                var targets = new[] { table.Origin, table.Origin - 0x10, table.Blocks[^1].Address, table.Blocks[^1].Address - 0x10 };
                foreach (var target in targets.Distinct())
                {
                    var hits = windows.SelectMany(w => client.SearchMemory(w, 0x00100000, BitConverter.GetBytes(target), [0xFF, 0xFF, 0xFF, 0xFF]))
                        .Take(12).ToList();
                    Console.WriteLine($"  quién apunta a 0x{target:X8}: " + (hits.Count == 0 ? "nadie" : string.Join(", ", hits.Select(h => $"0x{h:X8}"))));
                }
            }

            return 0;
        }

        // --reglas-juego posicion 120: durante esos segundos, cada cambio del registro de posición del juego (GameData +0x60)
        // con la hora, para ver qué hace en un combate, una captura y sus menús.
        if (args.Length >= 2 && args[0] == "posicion" && int.TryParse(args[1], out var seconds))
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var manager = BitConverter.ToUInt32(client.ReadMemory(0x006A3984, 4));
            var data = BitConverter.ToUInt32(client.ReadMemory(manager + 0x24, 4));
            var until = DateTime.Now.AddSeconds(seconds);
            string? last = null;
            Console.WriteLine($"Vigilando 0x{data + 0x60:X8} durante {seconds} s");
            while (DateTime.Now < until)
            {
                if (client.TryReadMemory(data + 0x60, 16, out var r))
                {
                    var now = $"mundo {BitConverter.ToUInt16(r, 0)} mapa {BitConverter.ToUInt16(r, 2)} "
                              + $"X {BitConverter.ToSingle(r, 4):F1} Y {BitConverter.ToSingle(r, 8):F1} Z {BitConverter.ToSingle(r, 12):F1}";
                    if (now != last) Console.WriteLine($"{DateTime.Now:HH:mm:ss.f}  {now}");
                    last = now;
                }

                Thread.Sleep(250);
            }

            return 0;
        }

        // --reglas-juego equipo 33F807C4: el equipo en esa dirección exacta (la que la app ya conoce), sin barrer.
        if (args.Length >= 2 && args[0] == "equipo")
        {
            using var client = new PermaLocke.GameLink.Rpc.AzaharRpcClient();
            var reader = new PermaLocke.GameLink.Data.Pk7Reader(client);
            var at = Convert.ToUInt32(args[1].Replace("0x", ""), 16);
            const uint Stride = PermaLocke.GameLink.Data.PartyLayoutLocator.AuthoritativeStride;
            const uint Stats = PermaLocke.GameLink.Data.PartyLayoutLocator.AuthoritativeStatsOffset;
            for (var slot = 0; slot < 6; slot++)
            {
                var entry = at + (uint)slot * Stride;
                var p = reader.TryRead(entry, Stats);
                Console.WriteLine(p is null
                    ? $"hueco {slot}: vacío o ilegible"
                    : $"hueco {slot}: {p.Nickname} (especie {p.Species}) PS {p.CurrentHp}/{p.MaxHp}  entrada 0x{entry:X8}  PS en 0x{entry + Stats + 8:X8}");
            }

            return 0;
        }

        var on = !args.Contains("quitar");
        var folder = args.FirstOrDefault(a => a != "quitar")
                     ?? AzaharInstallation.ModDirectory(
                         new AzaharInstallation(NullLogger<AzaharInstallation>.Instance).Locate(AppContext.BaseDirectory),
                         LayeredFsMod.UltraMoonProgramId);

        if (Process.GetProcessesByName("azahar").Length > 0)
        {
            Console.WriteLine("Azahar está abierto. Ciérralo: code.ips se lee al arrancar y no se toca el mod con el emulador abierto.");
            return 1;
        }

        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"No existe {folder}.");
            return 1;
        }

        Console.WriteLine($"{(on ? "Poniendo" : "Quitando")} las reglas en el juego de {folder}");
        foreach (var line in RulePatches.Apply(folder, on)) Console.WriteLine("  " + line);
        return 0;
    }
}
