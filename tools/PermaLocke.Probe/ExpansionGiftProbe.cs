using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;
using PermaLocke.Core.Domain;
using EncounterType = PermaLocke.Core.Domain.EncounterType;
using GameConfig = pk3DS.Core.GameConfig;
using Pk3dsVersion = pk3DS.Core.GameVersion;
using TextFile = pk3DS.Core.TextFile;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Probe;

/// <summary>
/// Puts a Pokémon with an ability and moves of the expansion mod into the player's PC, for
/// trying them in battle without waiting for the randomizer to hand one out.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GivePokemonProbe"/> cannot do it: PKHeX stops at 807, so a Pokémon of the mod built
/// its way comes out with a level on the wrong curve, no moves, zero PP for anything past 742, and
/// an ability cut to one byte. Here the level goes on the installed world's curve, the ability
/// keeps its ninth bit (§134) and every move gets the PP <b>the installed world</b> gives it — a
/// move with zero PP is Struggle waiting to happen, so a move the world does not have is refused.
/// </para>
/// <para>
/// Unlike <see cref="GivePokemonProbe"/>, this one is for <b>playing</b>, in the run the player
/// is playing, so it is registered: the run gets its entry with the PID, which is what lets the
/// watcher recognise it in the party, and the history gets a <c>PokemonDelivered</c> event from
/// the admin saying what it is and why. Unregistered, the first time it joined the party it would
/// be registered on its own as a capture, which it is not.
/// </para>
/// <para>
/// Same guards as every save write: Azahar closed — by its process, not only by RPC, because an
/// emulator without the RPC server answers nothing and would pass for closed —, the whole save
/// copied first, and the written slot read back field by field before anything is recorded.
/// <c>--probar</c> does all of it on a copy and records nothing.
/// </para>
/// </remarks>
public static class ExpansionGiftProbe
{
    // Ficheros de texto del idioma 6 (español) del mundo instalado, medidos en el §133.
    private const int ItemNames = 40;
    private const int SpeciesNames = 60;
    private const int AbilityNames = 101;
    private const int MoveNames = 118;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("--dar-mod <especie> <nivel> <habilidad> <mov1,mov2,mov3,mov4> "
                              + "[--forma n] [--objeto id] [--naturaleza n] [--probar]");
            return 1;
        }

        var species = int.Parse(args[1]);
        var level = int.Parse(args[2]);
        var ability = int.Parse(args[3]);
        var moves = args[4].Split(',').Select(int.Parse).ToArray();
        var form = Option(args, "--forma") ?? 0;
        var item = Option(args, "--objeto") ?? 0;
        var nature = Option(args, "--naturaleza") ?? Random.Shared.Next(25);
        var dryRun = args.Contains("--probar");

        var root = Root();
        var paths = new AppPaths(root);
        var location = new AzaharInstallation(NullLogger<AzaharInstallation>.Instance).Locate(AppContext.BaseDirectory);
        var romfs = Path.Combine(AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId), "romfs");

        if (!Directory.Exists(romfs))
        {
            Console.WriteLine($"No hay mundo instalado en {romfs}.");
            return 1;
        }

        var world = new World(romfs);

        if (world.Check(species, level, ability, moves, item) is { } refusal)
        {
            Console.WriteLine(refusal);
            return 1;
        }

        using var client = new AzaharRpcClient();
        var playerSave = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client, AppContext.BaseDirectory);

        if (!dryRun && (IsEmulatorRunning() || playerSave.IsGameLoaded()))
        {
            Console.WriteLine("Azahar está abierto. Guarda, ciérralo y vuelve a lanzarlo.");
            return 1;
        }

        if (playerSave.Find() is not { } real)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        var path = real;

        if (dryRun)
        {
            path = Path.Combine(Path.GetTempPath(), $"permalocke-dar-mod-{Guid.NewGuid():N}.sav");
            File.Copy(real, path);
        }

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM save)
        {
            Console.WriteLine("No se ha podido leer la partida.");
            return 1;
        }

        var (box, slot) = FreeSlot(save);

        if (box < 0)
        {
            Console.WriteLine("Todas las cajas del PC están llenas.");
            return 1;
        }

        int[] ivs = [.. Enumerable.Range(0, 6).Select(_ => Random.Shared.Next(32))];
        var pokemon = PokemonBuilder.Build(new NewPokemon(species, level, nature, ability, ivs, false), save);
        pokemon.Form = (byte)form;
        pokemon.HeldItem = item;

        // Los PP del mundo instalado: PKHeX no conoce nada por encima de la 742 y daría cero.
        ushort MoveAt(int i) => (ushort)(i < moves.Length ? moves[i] : 0);
        pokemon.SetMoves([MoveAt(0), MoveAt(1), MoveAt(2), MoveAt(3)]);
        pokemon.Move1_PP = world.PpOf(MoveAt(0));
        pokemon.Move2_PP = world.PpOf(MoveAt(1));
        pokemon.Move3_PP = world.PpOf(MoveAt(2));
        pokemon.Move4_PP = world.PpOf(MoveAt(3));
        pokemon.Move1_PPUps = pokemon.Move2_PPUps = pokemon.Move3_PPUps = pokemon.Move4_PPUps = 0;

        pokemon.RefreshChecksum();

        if (!dryRun)
        {
            Directory.CreateDirectory(paths.SaveBackups);
            var copy = Path.Combine(paths.SaveBackups, $"main-{DateTime.Now:yyyyMMdd-HHmmss}-dar-mod.sav");
            File.Copy(path, copy, overwrite: false);
            Console.WriteLine($"Copia de la partida: {copy}");
        }

        save.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.Handover);
        File.WriteAllBytes(path, save.Write().ToArray());

        // Se relee del disco, campo a campo: nada se registra sin haberlo vuelto a ver.
        var problems = Verify(path, box, slot, pokemon, level, ability, world);
        var who = $"{world.Species(species)} Nv.{level} con {world.Ability(ability)}";

        Console.WriteLine($"{who}, caja {box + 1} hueco {slot + 1}, PID {pokemon.PID:X8}");
        Console.WriteLine($"  naturaleza {(Nature)nature}, IV {string.Join('/', ivs)}"
                          + (item == 0 ? string.Empty : $", lleva {world.Item(item)}"));

        foreach (var move in moves)
        {
            Console.WriteLine($"  {move,4} {world.Move(move),-20} PP {world.PpOf(move)}");
        }

        if (problems.Count > 0)
        {
            Console.WriteLine("LA RELECTURA NO CUADRA:");
            problems.ForEach(p => Console.WriteLine("  " + p));
            Console.WriteLine(dryRun ? "(era una copia)" : "Restaura la copia de arriba antes de jugar.");
            return 1;
        }

        Console.WriteLine("  releído del fichero: especie, forma, nivel, habilidad con su noveno bit, "
                          + "movimientos, PP, objeto y firma cuadran.");

        if (dryRun)
        {
            File.Delete(path);
            Console.WriteLine("Era una copia: la partida no se ha tocado y no se ha registrado nada.");
            return 0;
        }

        await RegisterAsync(paths, pokemon, who, box + 1, slot + 1, level, ability, moves, world);
        return 0;
    }

    /// <summary>
    /// The run's entry and its event, after the write has been read back and not before.
    /// </summary>
    private static async Task RegisterAsync(AppPaths paths, PK7 pokemon, string who, int box, int slot,
        int level, int ability, int[] moves, World world)
    {
        var database = Path.Combine(paths.Saves, "permalocke.db");
        var runs = await new JsonRunRepository(paths.Saves).GetAllAsync();

        if (runs.Count == 0 || !File.Exists(database))
        {
            Console.WriteLine("No hay run: el Pokémon está en la partida pero no se ha registrado.");
            return;
        }

        var run = runs.OrderByDescending(r => r.CreatedAt).First();
        var now = DateTimeOffset.Now;

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = pokemon.Species,
            SpeciesName = world.Species(pokemon.Species),
            Level = level,
            IsShiny = pokemon.IsShiny,
            Origin = PokemonOrigin.AdminGrant,
            EncounterType = EncounterType.Gift,
            ObtainedAt = now,

            // No viene de ninguna zona, así que no gasta el encuentro de ninguna.
            ConsumedZoneEncounter = false,
            Pid = pokemon.PID,
            Form = pokemon.Form
        };

        await new SqlitePokemonRepository(database).SaveAsync(entry);
        await new SqliteEventStore(database).AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = now,
            Type = GameEventType.PokemonDelivered,
            Source = EventSource.Admin,
            Actor = "sonda --dar-mod",
            Description = $"Pokémon de prueba del mod de gen 8-9: {who}. Está en la caja {box}, hueco {slot}.",
            PokemonId = entry.Id,
            Data = new Dictionary<string, string>
            {
                ["pid"] = pokemon.PID.ToString("X8"),
                ["caja"] = box.ToString(),
                ["hueco"] = slot.ToString(),
                ["origen"] = entry.Origin.ToString(),
                ["especie"] = pokemon.Species.ToString(),
                ["forma"] = pokemon.Form.ToString(),
                ["nivel"] = level.ToString(),
                ["habilidad"] = $"{ability} {world.Ability(ability)}",
                ["movimientos"] = string.Join(", ", moves.Select(m => $"{m} {world.Move(m)}")),
                ["objeto"] = pokemon.HeldItem == 0 ? "-" : $"{pokemon.HeldItem} {world.Item(pokemon.HeldItem)}",
                ["motivo"] = "probar en combate habilidades y movimientos del mod"
            }
        });

        Console.WriteLine($"Registrado en la run «{run.Name}» como dado por el admin.");
    }

    private static List<string> Verify(string path, int box, int slot, PK7 expected, int level, int ability,
        World world)
    {
        var problems = new List<string>();

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM save
            || save.GetBoxSlotAtIndex(box, slot) is not PK7 found)
        {
            problems.Add("no se puede releer el hueco");
            return problems;
        }

        void Expect(string what, object wanted, object got)
        {
            if (!Equals(wanted, got))
            {
                problems.Add($"{what}: esperado {wanted}, leído {got}");
            }
        }

        Expect("PID", expected.PID, found.PID);
        Expect("especie", expected.Species, found.Species);
        Expect("forma", expected.Form, found.Form);
        Expect("nivel", level, GameLevels.Of(found));
        Expect("habilidad", ability, PokemonAbility.Of(found));
        Expect("objeto", expected.HeldItem, found.HeldItem);
        Expect("firma", true, found.ChecksumValid);

        (int Move, int PP)[] wanted =
            [(expected.Move1, expected.Move1_PP), (expected.Move2, expected.Move2_PP),
             (expected.Move3, expected.Move3_PP), (expected.Move4, expected.Move4_PP)];
        (int Move, int PP)[] got =
            [(found.Move1, found.Move1_PP), (found.Move2, found.Move2_PP),
             (found.Move3, found.Move3_PP), (found.Move4, found.Move4_PP)];

        for (var i = 0; i < 4; i++)
        {
            Expect($"movimiento {i + 1}", wanted[i], got[i]);

            if (got[i].Move != 0 && got[i].PP != world.PpOf(got[i].Move))
            {
                problems.Add($"movimiento {i + 1} con {got[i].PP} PP y el mundo le da {world.PpOf(got[i].Move)}");
            }
        }

        return problems;
    }

    private static (int Box, int Slot) FreeSlot(SAV7USUM save)
    {
        for (var box = 0; box < save.BoxCount; box++)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                if (save.GetBoxSlotAtIndex(box, slot) is not { Species: > 0 })
                {
                    return (box, slot);
                }
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// Any Azahar at all. The RPC check alone answers «closed» for an emulator that has the game
    /// loaded but no RPC server, and that is the one case where a write would be silently lost.
    /// </summary>
    private static bool IsEmulatorRunning() =>
        Process.GetProcesses().Any(p => p.ProcessName.StartsWith("azahar", StringComparison.OrdinalIgnoreCase)
                                        || p.ProcessName.StartsWith("citra", StringComparison.OrdinalIgnoreCase));

    private static int? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? int.Parse(args[at + 1]) : null;
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

    /// <summary>What the installed world says about moves and names. Read only.</summary>
    private sealed class World
    {
        private readonly Move7[] _moves;
        private readonly string[] _species;
        private readonly string[] _abilities;
        private readonly string[] _moveNames;
        private readonly string[] _items;

        public World(string romfs)
        {
            _moves = [.. Mini.UnpackMini(GarcPatcher.ReadOnly(Path.Combine(romfs, GameFiles.Move), 0), "WD")
                .Select(data => new Move7(data))];

            var config = new GameConfig(Pk3dsVersion.UM);
            var text = Path.Combine(romfs, GameFiles.GameText(6));
            string[] Text(int file) => TextFile.GetStrings(config, GarcPatcher.ReadOnly(text, file));

            _species = Text(SpeciesNames);
            _abilities = Text(AbilityNames);
            _moveNames = Text(MoveNames);
            _items = Text(ItemNames);
        }

        public int PpOf(int move) => move > 0 && move < _moves.Length ? _moves[move].PP : 0;

        public string Species(int id) => Name(_species, id);

        public string Ability(int id) => Name(_abilities, id);

        public string Move(int id) => Name(_moveNames, id);

        public string Item(int id) => Name(_items, id);

        /// <summary>Why this cannot be given, or null when it can.</summary>
        public string? Check(int species, int level, int ability, int[] moves, int item)
        {
            if (species <= 0 || species > WorldLimits.MaxSpecies)
            {
                return $"La especie {species} no existe en el mundo instalado (hasta {WorldLimits.MaxSpecies}).";
            }

            if (level is < 1 or > 100)
            {
                return $"Nivel {level} fuera de 1-100.";
            }

            if (ability <= 0 || ability >= _abilities.Length || ability > PokemonAbility.MaxStorable
                || !HasName(_abilities, ability))
            {
                return $"La habilidad {ability} no existe en el mundo instalado.";
            }

            if (moves.Length is 0 or > 4)
            {
                return "Hacen falta de uno a cuatro movimientos.";
            }

            foreach (var move in moves)
            {
                // Sin PP, el juego no deja usarlo y el Pokémon acaba con Forcejeo: los huecos
                // vacíos del mod (los de Let's Go y los Dinamax) tienen exactamente cero.
                if (PpOf(move) == 0 || !HasName(_moveNames, move))
                {
                    return $"El movimiento {move} no existe en el mundo instalado o no tiene PP.";
                }
            }

            if (item != 0 && !HasName(_items, item))
            {
                return $"El objeto {item} no existe en el mundo instalado.";
            }

            return null;
        }

        private static bool HasName(string[] names, int id) =>
            id > 0 && id < names.Length && !string.IsNullOrWhiteSpace(names[id]) && names[id] != "-";

        private static string Name(string[] names, int id) =>
            id >= 0 && id < names.Length ? names[id] : $"#{id}";
    }
}
