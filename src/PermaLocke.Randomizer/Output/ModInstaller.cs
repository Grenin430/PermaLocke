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

    /// <summary>How many copies of an overwritten world are kept, oldest dropped first.</summary>
    /// <remarks>
    /// Three and not ten like the run's backups (§76): a world is about 460 MB of randomized files,
    /// and what a copy is for is undoing the <em>last</em> install, or the one before it.
    /// </remarks>
    public const int BackupsKept = 3;

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
    /// <param name="backupRoot">Where copies go; by default beside the emulator's <c>mods</c> folder.</param>
    /// <returns>The copy made of what was about to be overwritten, or null when nothing was.</returns>
    /// <remarks>
    /// <para>
    /// <b>Before writing anything, whatever this is about to overwrite and cannot be rebuilt is
    /// copied aside</b>, and the copy is read back. A world somebody is playing is the one thing an
    /// install destroys, and for a while it destroyed it for good: the docs said the previous mod
    /// was kept in <c>load\permalocke-mod-anterior</c>, and that folder was from three weeks earlier —
    /// the promise had stopped being true when installing was rewritten for the base layer, and it
    /// was repeated to a player as reassurance before anybody checked the date on the folder.
    /// </para>
    /// <para>
    /// If the copy cannot be made or does not read back the same, <b>nothing is installed</b>. That
    /// is the rule <c>SaveEraser</c> follows (§67): without a copy it does not destroy.
    /// </para>
    /// </remarks>
    public static string? Install(string generated, string modDirectory, string? baseLayerRomfs,
        string? baseLayerExefs, Action<string>? onProgress = null, string? backupRoot = null)
    {
        var romfs = Path.Combine(modDirectory, "romfs");
        var exefs = Path.Combine(modDirectory, "exefs");
        var hasBase = baseLayerRomfs is not null && Directory.Exists(baseLayerRomfs);
        var hasBaseExefs = hasBase && baseLayerExefs is not null && Directory.Exists(baseLayerExefs);
        var ours = Path.Combine(generated, "exefs");

        var writes = new List<Write>();

        if (hasBase)
        {
            writes.AddRange(Writes(baseLayerRomfs!, romfs, fromBase: true));

            if (hasBaseExefs)
            {
                writes.AddRange(Writes(baseLayerExefs!, exefs, fromBase: true));
            }
        }

        writes.AddRange(Writes(Path.Combine(generated, "romfs"), romfs, fromBase: false));

        if (Directory.Exists(ours))
        {
            writes.AddRange(Writes(ours, exefs, fromBase: false));
        }

        var kept = KeepWhatWouldBeLost(writes, modDirectory,
            hasBase ? baseLayerRomfs : null, hasBaseExefs ? baseLayerExefs : null,
            backupRoot, onProgress);

        if (hasBase)
        {
            onProgress?.Invoke("Instalando... (la primera vez tarda unos minutos)");
            CopyTree(baseLayerRomfs!, romfs, skipUnchanged: true);

            if (hasBaseExefs)
            {
                CopyTree(baseLayerExefs!, exefs, skipUnchanged: true);
            }
        }

        onProgress?.Invoke("Instalando...");
        CopyTree(Path.Combine(generated, "romfs"), romfs);

        // Y el exefs generado ENCIMA del de la capa base, si lo hay: es el mismo code.bin del mod
        // con la tabla de MT barajada. Va despues por la misma razon que el romfs, y si no existe
        // no pasa nada, porque entonces el bueno es el que ya se copio.
        if (Directory.Exists(ours))
        {
            CopyTree(ours, exefs);
        }

        return kept;
    }

    /// <summary>
    /// Puts the base mod back over an installed world — the battle mode's swap — keeping a copy first.
    /// </summary>
    /// <remarks>
    /// It is the other way a world gets overwritten, so it takes the same copy. Returning from the
    /// swap reinstalls the generated folder, and if that folder has since been regenerated or deleted,
    /// the copy is the only place the world still exists.
    /// </remarks>
    public static string? SwitchToBase(string modDirectory, string baseLayerRomfs, string? baseLayerExefs,
        Action<string>? onProgress = null, string? backupRoot = null)
    {
        var romfs = Path.Combine(modDirectory, "romfs");
        var exefs = Path.Combine(modDirectory, "exefs");
        var hasExefs = baseLayerExefs is not null && Directory.Exists(baseLayerExefs);

        var writes = new List<Write>(Writes(baseLayerRomfs, romfs, fromBase: true));

        if (hasExefs)
        {
            writes.AddRange(Writes(baseLayerExefs!, exefs, fromBase: true));
        }

        var kept = KeepWhatWouldBeLost(writes, modDirectory, baseLayerRomfs,
            hasExefs ? baseLayerExefs : null, backupRoot, onProgress);

        CopyTree(baseLayerRomfs, romfs, skipUnchanged: true);

        if (hasExefs)
        {
            CopyTree(baseLayerExefs!, exefs);
        }

        return kept;
    }

    /// <summary>One file an operation is going to write.</summary>
    private sealed record Write(string Source, string Target, bool FromBase);

    private static IEnumerable<Write> Writes(string source, string destination, bool fromBase) =>
        Directory.Exists(source)
            ? Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                .Select(file => new Write(file, Path.Combine(destination, Path.GetRelativePath(source, file)), fromBase))
            : throw new DirectoryNotFoundException($"No existe {source}.");

    /// <summary>
    /// Copies aside every installed file the operation would change and nobody could rebuild.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Would change</b>: the file exists and what ends up there is different. The last writer
    /// decides — a randomized file wins over the base mod's — and "different" is a hash for the
    /// randomized files, which are small and whose timestamps always move, and length plus time for
    /// the base layer, where hashing 2,5 GB to learn nothing is what <see cref="IsAlreadyThere"/>
    /// exists to avoid.
    /// </para>
    /// <para>
    /// <b>Nobody could rebuild</b>: a file identical to the base mod's own copy is not kept, because
    /// the base mod is still sitting in <c>Expansion/</c>. That is not a size optimisation, it is what
    /// keeps the three copies meaningful — coming back from the battle mode would otherwise spend a
    /// slot on a copy of the unrandomized mod and push out a real world.
    /// </para>
    /// </remarks>
    private static string? KeepWhatWouldBeLost(IReadOnlyList<Write> writes, string modDirectory,
        string? baseLayerRomfs, string? baseLayerExefs, string? backupRoot, Action<string>? onProgress)
    {
        var last = new Dictionary<string, Write>(StringComparer.OrdinalIgnoreCase);

        foreach (var write in writes)
        {
            // Un fichero randomizado manda sobre el del mod base; el del mod base no pisa uno ya
            // decidido como randomizado.
            if (!last.TryGetValue(write.Target, out var earlier) || earlier.FromBase || !write.FromBase)
            {
                last[write.Target] = write;
            }
        }

        var lost = last.Values
            .Where(write => File.Exists(write.Target))
            .Where(write => write.FromBase
                ? !IsAlreadyThere(write.Source, write.Target)
                : !SameBytes(write.Source, write.Target))
            .Where(write => !IsBaseLayerCopy(write.Target, modDirectory, baseLayerRomfs, baseLayerExefs))
            .Select(write => write.Target)
            .ToList();

        if (lost.Count == 0)
        {
            return null;
        }

        var shelf = backupRoot ?? DefaultBackupRoot(modDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var copy = Path.Combine(shelf, stamp);

        // Dos instalaciones en el mismo segundo no se pisan: la segunda lleva sufijo. Sin esto la copia
        // chocaria con la anterior y, como no se sobrescribe una copia, no se instalaria nada.
        for (var n = 2; Directory.Exists(copy); n++)
        {
            copy = Path.Combine(shelf, stamp + "-" + n);
        }

        onProgress?.Invoke("Guardando una copia de tu mundo anterior...");

        try
        {
            foreach (var file in lost)
            {
                var target = Path.Combine(copy, Path.GetRelativePath(modDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);

                if (!SameBytes(file, target))
                {
                    throw new IOException($"La copia de {file} no se lee igual que el original.");
                }
            }

            File.WriteAllText(Path.Combine(copy, "LEEME.txt"),
                $"Copia del mundo instalado, hecha el {DateTime.Now:yyyy-MM-dd HH:mm:ss} justo antes de "
                + "sobrescribirlo.\r\n\r\nPara volver a él: con Azahar CERRADO, copia las carpetas romfs y "
                + "exefs de aquí encima de\r\n" + modDirectory + "\r\n\r\nSolo están los ficheros que se "
                + "iban a perder: lo que es igual que el mod base no se guarda, porque sigue en Expansion.\r\n\r\n"
                + string.Join("\r\n", lost.Select(file => Path.GetRelativePath(modDirectory, file))));
        }
        catch (Exception ex)
        {
            throw new IOException(
                "No se ha podido guardar copia del mundo instalado, así que NO se ha instalado nada: "
                + "sin copia no se sobrescribe un mundo que alguien está jugando. " + ex.Message, ex);
        }

        // Solo se borra lo viejo cuando la copia nueva ya está comprobada.
        foreach (var old in Directory.GetDirectories(shelf)
                     .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                     .Skip(BackupsKept))
        {
            Directory.Delete(old, recursive: true);
        }

        return copy;
    }

    /// <summary>
    /// Beside the emulator's <c>mods</c> folder, where Azahar does not look for mods.
    /// </summary>
    /// <remarks>
    /// Not inside <c>mods</c>: anything in there with a title id for a name is a mod the emulator
    /// would load. For a folder that is not inside a <c>mods</c> folder at all, next to it.
    /// </remarks>
    private static string DefaultBackupRoot(string modDirectory)
    {
        var parent = Directory.GetParent(Path.GetFullPath(modDirectory));

        return parent is { Name: var name, Parent: { } load }
               && string.Equals(name, "mods", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(load.FullName, "permalocke-copias")
            : Path.GetFullPath(modDirectory).TrimEnd(Path.DirectorySeparatorChar) + "-copias";
    }

    /// <summary>True when the installed file is still exactly the base mod's own copy.</summary>
    private static bool IsBaseLayerCopy(string installed, string modDirectory, string? baseRomfs, string? baseExefs)
    {
        var relative = Path.GetRelativePath(modDirectory, installed);
        var parts = relative.Split(Path.DirectorySeparatorChar, 2);

        if (parts.Length < 2)
        {
            return false;
        }

        var origin = parts[0].ToLowerInvariant() switch
        {
            "romfs" => baseRomfs,
            "exefs" => baseExefs,
            _ => null
        };

        if (origin is null)
        {
            return false;
        }

        // Sin esta comprobacion, un fichero que el mod base no trae -Shop.cro, por ejemplo- hace que
        // IsAlreadyThere pida la longitud de algo que no existe, y eso lanza.
        var baseFile = Path.Combine(origin, parts[1]);

        return File.Exists(baseFile) && IsAlreadyThere(baseFile, installed);
    }

    /// <summary>Same length and same bytes.</summary>
    private static bool SameBytes(string a, string b)
    {
        var first = new FileInfo(a);
        var second = new FileInfo(b);

        if (!first.Exists || !second.Exists || first.Length != second.Length)
        {
            return false;
        }

        using var sha = System.Security.Cryptography.SHA256.Create();
        using var one = File.OpenRead(a);
        using var two = File.OpenRead(b);

        return sha.ComputeHash(one).AsSpan().SequenceEqual(sha.ComputeHash(two));
    }
}
