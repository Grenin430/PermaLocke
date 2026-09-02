using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer;

/// <param name="Module">Which randomizer produced this line.</param>
/// <param name="Detail">What it did, in words the player can read.</param>
public sealed record RandomizerStep(string Module, string Detail);

/// <param name="Files">RomFS paths written into the mod folder.</param>
/// <param name="TotalBytes">Size of everything written, which the player has to keep on disk.</param>
public sealed record RandomizationReport(
    ulong Seed,
    IReadOnlyList<RandomizerStep> Steps,
    IReadOnlyList<string> Files,
    long TotalBytes,
    TimeSpan Elapsed,
    string? PreviousModKept = null,

    /// <summary>Files taken from a base layer instead of from the cartridge. Empty is the norm.</summary>
    IReadOnlyList<string>? BaseLayerFiles = null,

    /// <summary>How many species the world that was randomized turned out to hold.</summary>
    int MaxSpecies = 0);

/// <summary>
/// Turns a vanilla cartridge plus a seed into a LayeredFS mod folder that Azahar loads.
/// The cartridge is opened read-only and never modified; undoing a randomization is deleting
/// the mod folder.
/// </summary>
public sealed class RandomizerService(RandomizerOptions options)
{
    /// <summary>
    /// Salts must never change once a competition has started: they are what makes a given seed
    /// reproduce a given world, and what keeps one module's output independent of the others.
    /// </summary>
    private static class Salts
    {
        public const string Wild = "wild-encounters";
        public const string Static = "static-encounters";
        public const string Trainers = "trainers";
        public const string PokemonData = "pokemon-data";
        public const string Shops = "special-marts";
        public const string Machines = "technical-machines";
        public const string FieldItems = "field-items";
    }

    public async Task<RandomizationReport> RandomizeAsync(string romPath, string workDirectory,
        string modDirectory, ulong seed, string? baseLayer = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var steps = new List<RandomizerStep>();

        using var workspace = await RomWorkspace.ExtractAsync(romPath, workDirectory,
            baseLayer: baseLayer, ct: ct);
        var pool = SpeciesPool.FromGame(workspace.Config, options, workspace.MaxSpecies);
        var mod = new LayeredFsMod(workspace, modDirectory);

        // Se vacía antes de escribir. Sin esto, un módulo que se apaga deja su fichero de la
        // generación anterior en la carpeta, el juego lo sigue cargando y el informe dice que no
        // hay nada randomizado: exactamente lo que pasó al desactivar las evoluciones, que
        // seguían cambiadas en la partida mientras el informe cantaba "0 evoluciones".
        var kept = mod.Clear();

        // Encuentros y objetos del suelo viven en el mismo GARC de 460 MB, así que se carga una
        // vez, se aplican los dos y se guarda una vez.
        if (options.WildEncounters || options.FieldItems)
        {
            var path = workspace.PathOf(GameFiles.EncounterDataUltraMoon);
            var garc = await Task.Run(() => new GARC.LazyGARC(File.ReadAllBytes(path)), ct);

            if (options.WildEncounters)
            {
                var random = new SeededRandomSource(seed).Derive(Salts.Wild);
                var result = new WildEncounterRandomizer(options).Apply(random, pool, garc, ct);
                steps.Add(new RandomizerStep("Encuentros salvajes",
                    $"{result.SlotsChanged} huecos en {result.AreasChanged} zonas"));
            }

            if (options.FieldItems)
            {
                var random = new SeededRandomSource(seed).Derive(Salts.FieldItems);
                var result = new FieldItemRandomizer(workspace).Apply(random, garc, ct);
                steps.Add(new RandomizerStep("Objetos del suelo",
                    $"{result.RegularItems} objetos y {result.TechnicalMachines} MT doradas en {result.Zones} zonas"));
            }

            var packed = await Task.Run(garc.Save, ct);
            if (new GARC.LazyGARC(packed).FileCount != garc.FileCount)
            {
                throw new InvalidDataException("El GARC de encuentros perdió subficheros al reempaquetarse.");
            }
            await mod.WriteAsync(GameFiles.EncounterDataUltraMoon, packed, ct);
        }


        if (options.StaticEncounters)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Static);
            var result = await new StaticEncounterRandomizer(workspace, options)
                .ApplyAsync(random, pool, mod, ct);
            steps.Add(new RandomizerStep("Iniciales, fósiles y estáticos",
                $"{result.Replaced} entradas ({result.Protected} intactas). Iniciales: {string.Join(", ", result.Starters)}"
                + (options.StartersWithTwoEvolutions
                    ? $" (elegidos entre {result.StarterCandidates} especies con dos evoluciones por delante"
                      + (options.RandomizeEvolutions
                          ? ", según las líneas del cartucho: las evoluciones se randomizan después)"
                          : ")")
                    : string.Empty)
                + (result.LevelsRaised > 0
                    ? $"; {result.LevelsRaised} niveles subidos por el rol (dominantes y legendarios incluidos)"
                    : string.Empty)));
        }

        if (options.Trainers)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Trainers);
            var result = await new TrainerRandomizer(workspace, options)
                .ApplyAsync(random, pool, mod, ct);
            steps.Add(new RandomizerStep("Entrenadores",
                $"{result.Pokemon} Pokémon de {result.Trainers} entrenadores, {result.MovesCleared} movesets devueltos al juego"
                + (result.LevelsRaised > 0 ? $", {result.LevelsRaised} niveles subidos por el rol" : string.Empty)));

            // Después de los entrenadores a propósito: el Pokémon añadido se copia de uno que ya
            // está en el equipo, así que hereda el nivel que el rol acaba de subir.
            var extra = await new ExtraPokemonRandomizer(workspace, options)
                .ApplyAsync(new SeededRandomSource(seed).Derive(Salts.Trainers).Derive("extra"), pool, mod, ct);

            if (extra.Added > 0 || extra.NoRoom > 0)
            {
                steps.Add(new RandomizerStep("Pokémon extra del rol",
                    $"{extra.Added} añadidos en {extra.Battles} combates importantes"
                    + (extra.NoRoom > 0 ? $"; {extra.NoRoom} ya iban con seis y se quedaron igual" : string.Empty)));
            }
        }

        // Va el último a propósito: los módulos anteriores emparejan por total de estadísticas
        // base, y ese total tiene que ser el del cartucho, no uno ya randomizado.
        if (options.PokemonData)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.PokemonData);
            var result = await new PokemonDataRandomizer(workspace, options).ApplyAsync(random, mod, ct);
            steps.Add(new RandomizerStep("Datos de Pokémon",
                $"{result.Entries} especies y formas, {result.Evolutions} evoluciones, {result.LearnsetMoves} movimientos por nivel"));
        }

        // Después de los entrenadores y del Pokémon extra: la mega SUSTITUYE a uno de los suyos,
        // así que tiene que ver el equipo ya completo para que «uno de los cinco» sea de verdad
        // uno de los cinco.
        if (options.MegaTrainers && options.Trainers)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Trainers).Derive("megas");
            var result = await new MegaTrainerRandomizer(workspace, options).ApplyAsync(random, mod, ct);

            steps.Add(new RandomizerStep("Megas de los combates importantes",
                $"{result.Battles} combates con un Pokémon ya megaevolucionado, elegido entre "
                + $"{result.Candidates} especies; {result.TooEarly} quedaron fuera por ser de antes "
                + $"del nivel {options.MegaTrainerMinimumLevel} del cartucho"));
        }

        // Después de los datos de Pokémon a propósito: ese módulo puede cambiar a QUIÉN evoluciona
        // cada uno, y esto arregla CÓMO. Al revés, una evolución recién redirigida se quedaría con
        // su método de intercambio intacto.
        if (options.FixImpossibleEvolutions)
        {
            var result = await new ImpossibleEvolutionFixer(options).ApplyAsync(mod, ct);
            steps.Add(new RandomizerStep("Evoluciones imposibles a solas",
                $"{result.Trades} por intercambio arregladas"
                + (result.Moves > 0 ? $", {result.Moves} por movimiento pasadas a nivel" : string.Empty)
                + (result.Left > 0
                    ? $". OJO: quedan {result.Left} que siguen exigiendo otro jugador"
                    : ". No queda ninguna que exija otro jugador")));
        }

        if (options.SpecialMarts)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Shops);
            var result = await new ShopRandomizer(workspace, options).ApplyAsync(random, mod, ct);
            steps.Add(new RandomizerStep("Tiendas",
                $"{result.TechnicalMachineShops} tiendas de MT randomizadas, "
                + $"{result.RestockedShops} surtidas, {result.Slots} huecos"
                + (result.SpecialItems > 0
                    ? $"; {result.SpecialItems} objetos de evolución del mod a la venta"
                    : string.Empty)
                + (result.MedicineSlots > 0
                    ? $"; {result.MedicineSlots} curativos de las tiendas normales pasados a Poké Ball"
                    : string.Empty)));
        }

        if (options.RandomizeMachines && baseLayer is not null)
        {
            // El exefs va AL LADO del romfs, no dentro, asi que se deduce del romfs del mod base.
            var exefs = Path.Combine(Path.GetDirectoryName(baseLayer.TrimEnd(Path.DirectorySeparatorChar))!, "exefs");
            var random = new SeededRandomSource(seed).Derive(Salts.Machines);
            var result = await new MachineRandomizer(options).ApplyAsync(random, mod, exefs, ct);

            steps.Add(new RandomizerStep("MT",
                result.Machines > 0
                    ? $"{result.Machines} de las 100 MT ensenan otro movimiento "
                      + $"(tabla encontrada en code.bin, offset {result.Offset})"
                    : "sin cambios: no hay code.bin que parchear"));
        }

        // Un módulo pedido y no implementado se dice, no se ignora. Callarlo dejaría al jugador
        // creyendo que su partida está randomizada de una forma en la que no lo está.
        steps.AddRange(NotImplemented(options));

        var files = mod.StagedFiles;
        var total = files.Sum(f => new FileInfo(
            Path.Combine(mod.RomFsDirectory, f.Replace('/', Path.DirectorySeparatorChar))).Length);

        return new RandomizationReport(seed, steps, files, total, DateTimeOffset.UtcNow - started, kept,
            workspace.BaseLayerFiles, workspace.MaxSpecies);
    }

    /// <summary>
    /// Modules the configuration asks for and PermaLocke cannot do yet. They are reported as
    /// pending so the report never overstates what was randomized.
    /// <para>
    /// Empty today: every module the options can request is implemented, field items included.
    /// </para>
    /// </summary>
    public static IReadOnlyList<RandomizerStep> NotImplemented(RandomizerOptions options) => [];
}
