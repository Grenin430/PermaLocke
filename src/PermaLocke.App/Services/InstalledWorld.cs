using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.Services;

/// <summary>
/// Reads how big the world Azahar will actually load is, and tells the live readers.
/// </summary>
/// <remarks>
/// <para>
/// The species ceiling of <see cref="WorldLimits"/> has to come from somewhere, and the only honest
/// source is <b>the files the emulator is going to read</b> — the installed LayeredFS mod — rather
/// than a setting somebody has to remember to change. A mod that adds Pokémon and a ceiling left at
/// 807 is not a visible failure: the readers would quietly discard every new Pokémon as heap
/// rubbish, so nothing would be captured, nothing would die, and no error would ever appear.
/// </para>
/// <para>
/// When there is no mod installed, or its personal table cannot be read, the ceiling stays at the
/// cartridge's. Failing towards the cartridge is the safe direction: the worst case is that the
/// player has installed the expansion and PermaLocke does not notice, which is loud — the new
/// Pokémon simply never register, and HOME says nothing was found. The opposite default would
/// weaken the rubbish filter for everybody.
/// </para>
/// </remarks>
public sealed class InstalledWorld(AzaharInstallation azahar, ILogger<InstalledWorld> logger)
{
    /// <summary>Where the mod's species table lives, if there is a mod at all.</summary>
    private static string PersonalPath(string modDirectory) =>
        Path.Combine(modDirectory, "romfs",
            GameFiles.Personal.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// The same thing, for an entry point that has no dependency injection.
    /// </summary>
    /// <remarks>
    /// <see cref="WorldLimits"/> is global state, so <b>every</b> entry point that reads the game
    /// live has to do this or its readers throw away the mod's Pokémon as heap rubbish. The probe
    /// was not doing it, and it produced two confident wrong diagnoses in one sitting — a party it
    /// could not find, and a level two off — while the application next to it read both correctly.
    /// Hence a door that needs nothing but a folder.
    /// </remarks>
    /// <summary>
    /// The moves nobody may learn or be handed (§162), for what the application teaches and builds on its own. From
    /// the same <c>bannedMoves</c> the randomizer takes out of the world, so the two cannot disagree.
    /// </summary>
    private void ApplyBannedMoves(string appDirectory)
    {
        try
        {
            var banned = PermaLocke.Randomizer.RandomizerOptionsLoader
                .Load(Path.Combine(appDirectory, "Data", "randomizer.json")).BannedMoves;
            WorldMoves.Banned = banned.ToHashSet();
            logger.LogInformation("Movimientos prohibidos: {Count} ({Ids})", banned.Count, string.Join(", ", banned));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer la lista de movimientos prohibidos de randomizer.json");
        }
    }

    public static void ApplyQuietly(string appDirectory) =>
        new InstalledWorld(
                new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
                NullLogger<InstalledWorld>.Instance)
            .Apply(appDirectory);

    /// <summary>
    /// Applies the installed world's species ceiling. Called once at startup, before the game link.
    /// </summary>
    public void Apply(string appDirectory)
    {
        ApplyBannedMoves(appDirectory);
        ApplyMoves(appDirectory);

        var (species, growth, bases, types, forms, formTypes) = Read(appDirectory);

        if (types is not null)
        {
            // Los tipos del juego que se juega: PKHeX acaba en la 807 y el wonder trade enseñaba «?» (§121).
            WorldLimits.Types = types;
            logger.LogInformation("Mundo instalado: tipos leidos para {Count} especies", (types.Length / 2) - 1);
        }

        if (formTypes is not null)
        {
            // Los de cada forma con fila propia: un Vulpix de Alola es de Hielo, no de Fuego (§139).
            WorldLimits.FormTypes = formTypes;
        }

        if (bases is not null)
        {
            // En el orden de la FICHA, no en el de la tabla: quien las use no tiene por que
            // acordarse de que el cartucho pone la Velocidad la cuarta.
            WorldLimits.BaseStats = bases;
            logger.LogInformation(
                "Mundo instalado: estadisticas base leidas para {Count} especies", (bases.Length / 6) - 1);
        }

        if (forms is not null)
        {
            // Las formas con fila propia: sin esto, a un Raichu de Alola se le recalculaban las estadísticas
            // con las del Raichu normal al guardarle EV (§131).
            WorldLimits.FormBaseStats = forms;
            logger.LogInformation("Mundo instalado: {Count} formas con estadisticas propias", forms.Count);
        }

        if (growth is not null)
        {
            // Las curvas van SIEMPRE que se pueda leer la tabla, aunque el techo de especies no
            // cambie: es la tabla del juego que se está jugando, y manda sobre la de PKHeX.
            WorldLimits.GrowthRates = growth;
            logger.LogInformation("Mundo instalado: curvas de experiencia leídas para {Count} especies",
                growth.Length - 1);
        }

        if (species is null)
        {
            logger.LogInformation(
                "Mundo instalado: sin mod, o sin tabla de especies legible. El techo se queda en {Max}.",
                WorldLimits.CartridgeMaxSpecies);
            return;
        }

        if (species.Value <= WorldLimits.CartridgeMaxSpecies)
        {
            return;
        }

        WorldLimits.MaxSpecies = species.Value;
        logger.LogInformation(
            "Mundo instalado: el mod declara {Max} especies. Los lectores en vivo suben su techo.",
            species.Value);
    }

    /// <summary>
    /// Publishes what each Pokémon learns and what each move is, from the installed world (§142).
    /// </summary>
    /// <remarks>
    /// The learnsets are randomized in this competition, so the cartridge's list — PKHeX's — is the wrong answer for
    /// the move reminder and for anything the application builds. Each table is published only if the mod carries
    /// it; one it does not carry is the cartridge's in the game too, and the readers fall back to PKHeX for it.
    /// </remarks>
    private void ApplyMoves(string appDirectory)
    {
        try
        {
            var location = azahar.Locate(appDirectory);
            var modDirectory = AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId);
            var romfs = Path.Combine(modDirectory, "romfs");

            if (!Directory.Exists(romfs))
            {
                return;
            }

            var tables = WorldMoveTables.Read(romfs);

            if (tables.Learnsets is { } learnsets)
            {
                WorldMoves.Learnsets = learnsets;
                WorldMoves.FormRows = FormRows(PersonalPath(modDirectory));
                logger.LogInformation("Mundo instalado: aprendizajes leidos para {Count} filas, {Forms} formas con los suyos",
                    learnsets.Count, WorldMoves.FormRows.Count);
            }

            if (tables.Moves is { } moves)
            {
                WorldMoves.Moves =
                    [.. moves.Select(move => new WorldMove(move.Type, move.Category, move.Power, move.Accuracy, move.PP))];
                logger.LogInformation("Mundo instalado: {Count} movimientos leidos", moves.Count - 1);
            }

            if (tables.Names is { } names)
            {
                WorldMoves.MoveNames = names;
            }

            // Lo que hace cada movimiento, del texto del propio juego (§144): el recuerda-movimientos lo enseña.
            if (tables.Descriptions is { } descriptions)
            {
                WorldMoves.MoveDescriptions = descriptions;
            }
        }
        catch (Exception ex)
        {
            // Como el resto: no impide arrancar. Sin tablas del mundo se pregunta al cartucho, y el
            // recuerda-movimientos dice de dónde saca la lista.
            logger.LogWarning(ex, "No se pudieron leer los aprendizajes del mundo instalado");
        }
    }

    /// <summary>
    /// The learnset row of every form that has its own, by the game's rule (<see cref="PersonalEntry7.RowOf"/>).
    /// </summary>
    private static Dictionary<(int Species, int Form), int> FormRows(string personalPath)
    {
        var rows = new Dictionary<(int Species, int Form), int>();

        if (!File.Exists(personalPath))
        {
            return rows;
        }

        var packed = GarcPatcher.ReadOnly(personalPath, GarcPatcher.CountReadOnly(personalPath) - 1);
        var count = PersonalEntry7.SpeciesCount(packed);

        for (var species = 1; species <= count; species++)
        {
            var formCount = PersonalEntry7.GetFormCount(packed, species * PersonalEntry7.Size);

            for (var form = 1; form < formCount; form++)
            {
                if (PersonalEntry7.RowOf(packed, species, form) is { } row && row != species)
                {
                    rows[(species, form)] = row;
                }
            }
        }

        return rows;
    }

    private (int? Species, byte[]? Growth, byte[]? Bases, byte[]? Types,
        Dictionary<(int Species, int Form), byte[]>? Forms,
        Dictionary<(int Species, int Form), (byte First, byte Second)>? FormTypes) Read(string appDirectory)
    {
        try
        {
            var location = azahar.Locate(appDirectory);
            var path = PersonalPath(
                AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId));

            if (!File.Exists(path))
            {
                return (null, null, null, null, null, null);
            }

            // De solo lectura: es la carpeta del emulador, y el constructor de GarcPatcher pide escritura para parchear.
            var packed = GarcPatcher.ReadOnly(path, GarcPatcher.CountReadOnly(path) - 1);
            var count = PersonalEntry7.SpeciesCount(packed);

            return (count, PersonalEntry7.GrowthRates(packed, count),
                PersonalEntry7.BaseStatsInScreenOrder(packed, count), PersonalEntry7.Types(packed, count),
                PersonalEntry7.FormBaseStatsInScreenOrder(packed, count), PersonalEntry7.FormTypes(packed, count));
        }
        catch (Exception ex)
        {
            // Nunca impide arrancar: un techo que no se ha podido leer se queda en el del cartucho,
            // que es el lado que no afloja ningún filtro.
            logger.LogWarning(ex, "No se pudo leer cuántas especies tiene el mundo instalado");
            return (null, null, null, null, null, null);
        }
    }

}
