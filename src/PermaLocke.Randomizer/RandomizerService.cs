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
    TimeSpan Elapsed);

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
        public const string FieldItems = "field-items";
    }

    public async Task<RandomizationReport> RandomizeAsync(string romPath, string workDirectory,
        string modDirectory, ulong seed, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var steps = new List<RandomizerStep>();

        using var workspace = await RomWorkspace.ExtractAsync(romPath, workDirectory, ct: ct);
        var pool = SpeciesPool.FromGame(workspace.Config, options);
        var mod = new LayeredFsMod(workspace, modDirectory);

        // Se vacía antes de escribir. Sin esto, un módulo que se apaga deja su fichero de la
        // generación anterior en la carpeta, el juego lo sigue cargando y el informe dice que no
        // hay nada randomizado: exactamente lo que pasó al desactivar las evoluciones, que
        // seguían cambiadas en la partida mientras el informe cantaba "0 evoluciones".
        mod.Clear();

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
                $"{result.Replaced} entradas ({result.Protected} intactas). Iniciales: {string.Join(", ", result.Starters)}"));
        }

        if (options.Trainers)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Trainers);
            var result = await new TrainerRandomizer(workspace, options)
                .ApplyAsync(random, pool, mod, ct);
            steps.Add(new RandomizerStep("Entrenadores",
                $"{result.Pokemon} Pokémon de {result.Trainers} entrenadores, {result.MovesCleared} movesets devueltos al juego"));
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

        if (options.SpecialMarts)
        {
            var random = new SeededRandomSource(seed).Derive(Salts.Shops);
            var result = await new ShopRandomizer(workspace, options).ApplyAsync(random, mod, ct);
            steps.Add(new RandomizerStep("Tiendas especiales",
                $"{result.TechnicalMachineShops} tiendas de MT randomizadas, {result.RestockedShops} surtidas de Poké Balls, {result.Slots} huecos"));
        }

        // Un módulo pedido y no implementado se dice, no se ignora. Callarlo dejaría al jugador
        // creyendo que su partida está randomizada de una forma en la que no lo está.
        steps.AddRange(NotImplemented(options));

        var files = mod.StagedFiles;
        var total = files.Sum(f => new FileInfo(
            Path.Combine(mod.RomFsDirectory, f.Replace('/', Path.DirectorySeparatorChar))).Length);

        return new RandomizationReport(seed, steps, files, total, DateTimeOffset.UtcNow - started);
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
