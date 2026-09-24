using System.IO;
using Microsoft.Extensions.Logging;
using pk3DS.Core.CTR;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using PermaLocke.Rules;

namespace PermaLocke.App.Services;

/// <summary>
/// The static captures the competition allows (§119), as the species they are in the world the player is playing.
/// </summary>
/// <remarks>
/// <para>
/// <c>Data/rules.json</c> names them by what the cartridge holds — Necrozma of form 0 at level 65, the four Tapus
/// at 60 — because the randomizer changes the species and the list has to hold for every world. The static table
/// is patched in place, so the same row of the installed world's <c>a/1/5/9</c> says what each one became. In the
/// player's world on 2026-09-14 the Necrozma of Monte Lanakila is a Wo-Chien, and the Tapus a Bellibolt, a
/// Flamariete, a Wo-Chien and an Ursaluna.
/// </para>
/// <para>
/// An entry that cannot be resolved allows nothing and says so in the log: the safe side of this rule is no balls.
/// Where the cartridge has two identical rows (Tapu Koko) both species are allowed, which costs nothing because they
/// are fought in the ruins, where there are no wild Pokémon.
/// </para>
/// </remarks>
public sealed class WorldAllowedStatics(RulesConfiguration configuration, AzaharInstallation azahar, AppPaths paths,
    ISpeciesLookup names, ILogger<WorldAllowedStatics> logger)
{
    private readonly object _gate = new();
    private Dictionary<(string Zone, int Species), string>? _allowed;
    private HashSet<string> _zones = new(StringComparer.Ordinal);

    /// <summary>What the capture is, when this species in this zone is one of the allowed ones; otherwise null.</summary>
    public string? Allowed(string zone, int species) =>
        Load().TryGetValue((zone, species), out var note) ? note : null;

    /// <summary>The zone holds one of the allowed captures.</summary>
    public bool HasAny(string zone)
    {
        Load();
        return _zones.Contains(zone);
    }

    /// <summary>Resolves the list now instead of at the first battle. Never throws.</summary>
    public void Warm() => Load();

    /// <summary>
    /// Drops what was read, so the next question reads the world installed now. Called when a world is installed or
    /// removed with the app open: which species each allowed static turned into is different in every world.
    /// </summary>
    public void Forget()
    {
        lock (_gate)
        {
            _allowed = null;
            _zones = new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private Dictionary<(string Zone, int Species), string> Load()
    {
        lock (_gate)
        {
            if (_allowed is not null)
            {
                return _allowed;
            }

            _allowed = [];
            var wanted = configuration.BallControl.AllowedStatics;

            if (wanted.Count == 0)
            {
                return _allowed;
            }

            try
            {
                if (Cartridge() is not { } cartridgePath)
                {
                    logger.LogWarning("Sin tabla de estáticos del cartucho: ninguna captura estática está permitida");
                    return _allowed;
                }

                var worldPath = Installed() ?? cartridgePath;
                var cartridge = new GARC.LazyGARC(File.ReadAllBytes(cartridgePath))[StaticEncounterTable.Statics.Subfile];
                var world = new GARC.LazyGARC(File.ReadAllBytes(worldPath))[StaticEncounterTable.Statics.Subfile];

                if (StaticEncounterTable.Count(cartridge, StaticEncounterTable.Statics)
                    != StaticEncounterTable.Count(world, StaticEncounterTable.Statics))
                {
                    logger.LogWarning("La tabla de estáticos del mundo instalado no mide lo mismo que la del cartucho; " +
                                      "ninguna captura estática está permitida");
                    return _allowed;
                }

                foreach (var entry in wanted)
                {
                    var rows = StaticEncounterTable.RowsOf(cartridge, StaticEncounterTable.Statics,
                        entry.Species, entry.Form, entry.Level);

                    if (rows.Count == 0)
                    {
                        logger.LogWarning("Captura permitida sin encontrar en el cartucho: {Note} (especie {Species}, forma {Form}, Nv {Level})",
                            entry.Note, entry.Species, entry.Form, entry.Level);
                        continue;
                    }

                    foreach (var row in rows)
                    {
                        var species = StaticEncounterTable.GetSpecies(world, StaticEncounterTable.Statics, row);
                        _allowed[(entry.Zone, species)] = entry.Note;
                        _zones.Add(entry.Zone);

                        logger.LogInformation("Captura permitida: {Note} en {Zone} es {Name} (#{Species}, fila {Row})",
                            entry.Note, entry.Zone, names.GetName(species), species, row);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudieron leer las capturas estáticas permitidas; ninguna lo está");
                _allowed = [];
                _zones = new HashSet<string>(StringComparer.Ordinal);
            }

            return _allowed;
        }
    }

    /// <summary>The static table the player's world is patched from: the expansion's, if it has one, or the cartridge's.</summary>
    private string? Cartridge()
    {
        var relative = GameFiles.EncounterStatic.Replace('/', Path.DirectorySeparatorChar);
        var expansion = Path.Combine(paths.Expansion, "romfs", relative);

        if (File.Exists(expansion))
        {
            return expansion;
        }

        if (RomInspector.ScanFolder(paths.Rom).FirstOrDefault(rom => rom.IsSupported) is not { } rom)
        {
            return null;
        }

        var extracted = Path.Combine(Path.GetTempPath(), "permalocke-estaticos", "a-1-5-9.garc");
        Directory.CreateDirectory(Path.GetDirectoryName(extracted)!);

        return File.Exists(extracted) || new RomFsReader(rom.Path).ExtractTo(GameFiles.EncounterStatic, extracted)
            ? extracted
            : null;
    }

    /// <summary>The installed mod's static table, or null when the world does not change it.</summary>
    private string? Installed()
    {
        var path = Path.Combine(
            AzaharInstallation.ModDirectory(azahar.Locate(AppContext.BaseDirectory), LayeredFsMod.UltraMoonProgramId),
            "romfs", GameFiles.EncounterStatic.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(path) ? path : null;
    }
}
