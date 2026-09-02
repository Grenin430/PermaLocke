namespace PermaLocke.Randomizer.Output;

/// <summary>
/// Puts a generated mod into the emulator's load folder, base layer included.
/// </summary>
/// <remarks>
/// <para>
/// The order is the whole point: <b>the base layer first and the randomized files on top</b>.
/// Installing only what the randomizer wrote left Azahar with seven files and the cartridge for
/// everything else, so a world randomized on top of the gen 8-9 expansion arrived without the
/// species table, the learnsets or the 2,5 GB of models. The new Pokémon were in the encounter
/// tables and the game had nothing to draw them with.
/// </para>
/// <para>
/// It lives here, and not as a private helper in the two places that install, because there were
/// briefly two copies of it. Two implementations of "which files win" would eventually disagree,
/// and the one that disagreed would be whichever nobody tested.
/// </para>
/// </remarks>
public static class ModInstaller
{
    /// <summary>Copies a folder tree, and says how many files it actually wrote.</summary>
    /// <param name="skipUnchanged">
    /// Leave alone a file the destination already has with the same length and timestamp.
    /// <para>
    /// Only ever for the base layer. That is a downloaded mod which does not change between
    /// installs and whose model file alone is 2,5 GB, so copying it again every time costs minutes
    /// for nothing. The randomized files are always copied, because those <em>do</em> change: a
    /// stale one surviving is the §27 failure, where the previous generation's file stayed active
    /// while the report described the new one.
    /// </para>
    /// </param>
    public static int CopyTree(string source, string destination, bool skipUnchanged = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        Directory.CreateDirectory(destination);
        var copied = 0;

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));

            if (skipUnchanged && IsAlreadyThere(file, target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Same length and same modification time. What every file sync uses, and the reason it is not
    /// a hash is that hashing 2,5 GB to decide whether to copy 2,5 GB saves nothing.
    /// </summary>
    private static bool IsAlreadyThere(string source, string target)
    {
        if (!File.Exists(target))
        {
            return false;
        }

        var from = new FileInfo(source);
        var to = new FileInfo(target);

        return from.Length == to.Length && from.LastWriteTimeUtc == to.LastWriteTimeUtc;
    }

    /// <summary>
    /// Installs a generated mod, laying the base layer down first when there is one.
    /// </summary>
    /// <param name="generated">The randomizer's output folder, the one holding <c>romfs</c>.</param>
    /// <param name="modDirectory">The emulator's folder for this title.</param>
    /// <param name="baseLayerRomfs">The base mod's <c>romfs</c>, or null for plain vanilla.</param>
    /// <param name="baseLayerExefs">
    /// The base mod's <c>exefs</c>, which sits <b>beside</b> romfs and not inside it. The expansion
    /// patches <c>code.bin</c>, and without it the new Pokémon do not exist as far as the engine is
    /// concerned however complete their data is.
    /// </param>
    /// <param name="onProgress">Told what is happening, since the base layer takes minutes.</param>
    public static void Install(string generated, string modDirectory, string? baseLayerRomfs,
        string? baseLayerExefs, Action<string>? onProgress = null)
    {
        var romfs = Path.Combine(modDirectory, "romfs");

        if (baseLayerRomfs is not null && Directory.Exists(baseLayerRomfs))
        {
            onProgress?.Invoke("Copiando el mod base... (son varios GB, la primera vez tarda)");
            CopyTree(baseLayerRomfs, romfs, skipUnchanged: true);

            if (baseLayerExefs is not null && Directory.Exists(baseLayerExefs))
            {
                CopyTree(baseLayerExefs, Path.Combine(modDirectory, "exefs"), skipUnchanged: true);
            }
        }

        onProgress?.Invoke("Copiando la randomización...");
        CopyTree(Path.Combine(generated, "romfs"), romfs);

        // Y el exefs generado ENCIMA del de la capa base, si lo hay: es el mismo code.bin del mod
        // con la tabla de MT barajada. Va despues por la misma razon que el romfs, y si no existe
        // no pasa nada, porque entonces el bueno es el que ya se copio.
        var ours = Path.Combine(generated, "exefs");

        if (Directory.Exists(ours))
        {
            CopyTree(ours, Path.Combine(modDirectory, "exefs"));
        }
    }
}
