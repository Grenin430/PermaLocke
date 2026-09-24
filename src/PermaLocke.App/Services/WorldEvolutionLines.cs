using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using PermaLocke.Rules;

namespace PermaLocke.App.Services;

/// <summary>
/// Evolutionary families, read from the world the player is actually playing.
/// </summary>
/// <remarks>
/// <para>
/// The duplicates clause the player asked for (§117) is about lines, not species: owning a Frogadier makes Froakie,
/// Frogadier and Greninja all duplicates. The families come from <c>a/0/1/4</c> of the world Azahar loads — the
/// installed mod first, because it may carry evolutions of its own or the expansion's new species; then the
/// expansion base layer; then the player's cartridge. A family is identified by the species it starts from.
/// </para>
/// <para>
/// When no table can be read, <see cref="HasData"/> is false and callers fall back to exact species, which is the
/// weaker clause and never the wrong one.
/// </para>
/// </remarks>
public sealed class WorldEvolutionLines(AzaharInstallation azahar, AppPaths paths, ILogger<WorldEvolutionLines> logger)
    : IEvolutionLineProvider
{
    private readonly object _gate = new();
    private IReadOnlyDictionary<int, int>? _baseOf;
    private bool _loaded;

    public bool HasData => Lines() is { Count: > 0 };

    public int GetLineId(int species) =>
        Lines() is { } lines && lines.TryGetValue(species, out var start) ? start : species;

    /// <summary>Reads the table now instead of at the first battle. Never throws.</summary>
    public void Warm() => _ = Lines();

    /// <summary>Drops what was read, so the next question reads the world installed now.</summary>
    public void Forget()
    {
        lock (_gate)
        {
            _loaded = false;
            _baseOf = null;
        }
    }

    private IReadOnlyDictionary<int, int>? Lines()
    {
        lock (_gate)
        {
            if (_loaded)
            {
                return _baseOf;
            }

            _loaded = true;

            try
            {
                if (Find() is not { } path)
                {
                    logger.LogWarning("Sin tabla de evoluciones: los duplicados se comparan por especie exacta");
                    return null;
                }

                var table = EvolutionTable.Read(path);
                var baseOf = new Dictionary<int, int>();

                foreach (var line in table.Lines())
                {
                    var start = line[0][0];

                    foreach (var stage in line)
                    {
                        foreach (var species in stage)
                        {
                            baseOf.TryAdd(species, start);
                        }
                    }
                }

                _baseOf = baseOf;
                logger.LogInformation("Líneas evolutivas leídas de {Path}: {Count} especies", path, baseOf.Count);
                return _baseOf;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo leer la tabla de evoluciones; los duplicados irán por especie exacta");
                return null;
            }
        }
    }

    private string? Find()
    {
        var relative = GameFiles.Evolution.Replace('/', Path.DirectorySeparatorChar);

        var installed = Path.Combine(
            AzaharInstallation.ModDirectory(azahar.Locate(AppContext.BaseDirectory), LayeredFsMod.UltraMoonProgramId),
            "romfs", relative);

        if (File.Exists(installed))
        {
            return installed;
        }

        var expansion = Path.Combine(paths.Expansion, "romfs", relative);

        if (File.Exists(expansion))
        {
            return expansion;
        }

        if (RomInspector.ScanFolder(paths.Rom).FirstOrDefault(rom => rom.IsSupported) is not { } cartridge)
        {
            return null;
        }

        var extracted = Path.Combine(Path.GetTempPath(), "permalocke-evoluciones", "a-0-1-4.garc");
        Directory.CreateDirectory(Path.GetDirectoryName(extracted)!);

        return File.Exists(extracted) || new RomFsReader(cartridge.Path).ExtractTo(GameFiles.Evolution, extracted)
            ? extracted
            : null;
    }
}
