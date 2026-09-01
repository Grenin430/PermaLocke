using System.IO;
using Microsoft.Extensions.Logging;
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
    /// Applies the installed world's species ceiling. Called once at startup, before the game link.
    /// </summary>
    public void Apply(string appDirectory)
    {
        var species = Read(appDirectory);

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

    private int? Read(string appDirectory)
    {
        try
        {
            var location = azahar.Locate(appDirectory);
            var path = PersonalPath(
                AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId));

            if (!File.Exists(path))
            {
                return null;
            }

            using var personal = new GarcPatcher(path);
            return PersonalEntry7.SpeciesCount(personal.Read(personal.FileCount - 1));
        }
        catch (Exception ex)
        {
            // Nunca impide arrancar: un techo que no se ha podido leer se queda en el del cartucho,
            // que es el lado que no afloja ningún filtro.
            logger.LogWarning(ex, "No se pudo leer cuántas especies tiene el mundo instalado");
            return null;
        }
    }
}
