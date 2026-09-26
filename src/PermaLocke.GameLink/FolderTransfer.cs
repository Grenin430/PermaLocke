using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PermaLocke.GameLink;

/// <summary>One thing the transfer brings from the old folder, and where it goes.</summary>
/// <param name="What">In the player's words: «tu run», «tu partida de Ultra Luna»…</param>
/// <param name="IsFolder">A whole folder, set aside and copied as one; otherwise a single file.</param>
public sealed record TransferItem(string What, string From, string To, bool IsFolder);

/// <summary>What the transfer would do, or why it cannot.</summary>
public sealed record TransferPlan(string OldRoot, string NewRoot, IReadOnlyList<TransferItem> Items, IReadOnlyList<string> Problems)
{
    public bool CanGo => Problems.Count == 0 && Items.Count > 0;
}

/// <summary>What the transfer did.</summary>
/// <param name="SetAside">Where whatever the new folder already had in those places was put, or null if there was nothing.</param>
public sealed record TransferResult(bool Done, int Files, long Bytes, string? SetAside, IReadOnlyList<string> Log);

/// <summary>
/// Brings a player's PermaLocke from an old folder into this one, once (§195, plan del próximo torneo, paso 2): the run,
/// the Ultra Moon save, the world installed in the emulator, the player and the Discord session, and the ROM if this
/// folder has none.
/// </summary>
/// <remarks>
/// <para>
/// <b>Copies, never moves nor deletes</b>: the old folder stays exactly as it was, so a transfer that goes wrong loses
/// nothing. What the new folder already had in any of those places is <b>set aside</b> (moved into a dated folder), not
/// overwritten, so nothing of it is lost either: a folder copied over another would mix two saves' files.
/// </para>
/// <para>
/// It runs when the application starts, before anything opens the database (<see cref="RunPending"/>): the player picks
/// the folder, PermaLocke writes <see cref="PendingFile"/> and restarts. Copying a SQLite file somebody has open is the
/// one thing that must not happen.
/// </para>
/// <para>
/// <c>docs/DISTRIBUCION-LOCAL.md</c> says not to copy Config, Saves or Emulator/user from a partida: that is about
/// handing a folder to somebody else. This is one player bringing their own.
/// </para>
/// </remarks>
public static class FolderTransfer
{
    /// <summary>The transfer waiting for the next start, in <c>Config/</c>: the old folder's path.</summary>
    public const string PendingFile = "traspaso-pendiente.json";

    /// <summary>What the last transfer did, in <c>Config/</c>, for the screen to say once.</summary>
    public const string DoneFile = "traspaso-hecho.json";

    private const string SaveTitle = "001b5100";
    private const string ProgramId = "00040000001B5100";

    private sealed record Pending(string Carpeta);

    /// <summary>Works out what would be brought, and whether it can be.</summary>
    public static TransferPlan Plan(string oldRoot, string newRoot)
    {
        var problems = new List<string>();
        var items = new List<TransferItem>();

        oldRoot = Path.GetFullPath(oldRoot);
        newRoot = Path.GetFullPath(newRoot);

        if (string.Equals(Path.TrimEndingDirectorySeparator(oldRoot), Path.TrimEndingDirectorySeparator(newRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Es esta misma carpeta. Elige la carpeta de PermaLocke de antes.");
            return new TransferPlan(oldRoot, newRoot, items, problems);
        }

        var oldSaves = Path.Combine(oldRoot, "Saves");
        if (!HasRun(oldSaves))
        {
            problems.Add("En esa carpeta no hay ninguna run de PermaLocke (no hay Saves\\…\\run.json).");
            return new TransferPlan(oldRoot, newRoot, items, problems);
        }

        if (HasRun(Path.Combine(newRoot, "Saves")))
        {
            problems.Add("Aquí ya tienes una run. El traspaso es para una carpeta nueva: no se pisa una run con otra.");
        }

        items.Add(new TransferItem("Tu run: eventos, Pokémon, puntos, sesiones, copias y killcams (Saves)",
            oldSaves, Path.Combine(newRoot, "Saves"), IsFolder: true));

        var oldUser = UserDirectoryOf(oldRoot);
        var newUser = UserDirectoryOf(newRoot);
        var sameEmulator = string.Equals(Path.GetFullPath(oldUser), Path.GetFullPath(newUser), StringComparison.OrdinalIgnoreCase);

        if (!sameEmulator)
        {
            if (SaveTitleFolder(oldUser) is { } save)
            {
                items.Add(new TransferItem("Tu partida de Ultra Luna", save,
                    Path.Combine(newUser, Path.GetRelativePath(oldUser, save)), IsFolder: true));
            }
            else
            {
                problems.Add("En esa carpeta no está tu partida de Ultra Luna (Emulator\\user\\sdmc\\…\\001b5100).");
            }

            var mods = Path.Combine(oldUser, "load", "mods", ProgramId);
            if (Directory.Exists(mods))
            {
                items.Add(new TransferItem("Tu mundo instalado en el emulador (load\\mods)", mods,
                    Path.Combine(newUser, "load", "mods", ProgramId), IsFolder: true));
            }
        }

        foreach (var (file, what) in new[] { ("jugador.json", "Tu jugador"), ("discord.json", "Tu sesión de Discord") })
        {
            var from = Path.Combine(oldRoot, "Config", file);
            if (File.Exists(from))
            {
                items.Add(new TransferItem(what, from, Path.Combine(newRoot, "Config", file), IsFolder: false));
            }
        }

        // La ROM solo si aquí no hay ninguna: la del jugador ya puesta no se toca.
        var oldRom = Path.Combine(oldRoot, "ROM");
        var newRom = Path.Combine(newRoot, "ROM");
        if (!HasFiles(newRom) && HasFiles(oldRom))
        {
            items.Add(new TransferItem("Tu ROM de Ultra Luna", oldRom, newRom, IsFolder: true));
        }

        return new TransferPlan(oldRoot, newRoot, items, problems);
    }

    /// <summary>Leaves the transfer for the next start: <see cref="RunPending"/> does it before the database opens.</summary>
    public static void Schedule(string config, string oldRoot)
    {
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, PendingFile), JsonSerializer.Serialize(new Pending(oldRoot)));
    }

    /// <summary>
    /// Does the transfer left by <see cref="Schedule"/>, if there is one. Called at start, before anything opens the
    /// database; never throws. What it did is written to <see cref="DoneFile"/> and to the log.
    /// </summary>
    public static TransferResult? RunPending(string newRoot, string config, ILogger logger)
    {
        var pendingPath = Path.Combine(config, PendingFile);
        if (!File.Exists(pendingPath))
        {
            return null;
        }

        TransferResult result;
        try
        {
            var pending = JsonSerializer.Deserialize<Pending>(File.ReadAllText(pendingPath))
                          ?? throw new InvalidDataException("El traspaso pendiente está vacío.");
            var plan = Plan(pending.Carpeta, newRoot);
            result = plan.CanGo
                ? Run(plan, DateTimeOffset.Now, logger)
                : new TransferResult(false, 0, 0, null, [.. plan.Problems]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el traspaso desde la carpeta vieja");
            result = new TransferResult(false, 0, 0, null, [$"Ha fallado: {ex.Message}"]);
        }

        // El pendiente es nuestro y ya se ha intentado: fuera, para no repetirlo en cada arranque.
        File.Delete(pendingPath);
        File.WriteAllText(Path.Combine(config, DoneFile), JsonSerializer.Serialize(result));
        return result;
    }

    /// <summary>Copies what the plan says. Sets aside whatever is already in the way first.</summary>
    public static TransferResult Run(TransferPlan plan, DateTimeOffset now, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanGo)
        {
            return new TransferResult(false, 0, 0, null, [.. plan.Problems]);
        }

        var log = new List<string>();
        var stamp = now.ToString("yyyyMMdd-HHmmss");
        var aside = Path.Combine(plan.NewRoot, $"Apartado antes del traspaso {stamp}");
        var setAside = false;
        var files = 0;
        long bytes = 0;

        logger.LogInformation("Traspaso desde {Old} a {New}", plan.OldRoot, plan.NewRoot);

        foreach (var item in plan.Items)
        {
            // Lo que ya hubiera aquí en ese sitio se aparta, con su misma ruta dentro de la carpeta apartada.
            if (item.IsFolder ? Directory.Exists(item.To) && HasFiles(item.To) : File.Exists(item.To))
            {
                var to = Path.Combine(aside, Path.GetRelativePath(plan.NewRoot, item.To));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (item.IsFolder) Directory.Move(item.To, to);
                else File.Move(item.To, to);
                setAside = true;
                log.Add($"Apartado lo que había en {Path.GetRelativePath(plan.NewRoot, item.To)}");
            }

            var (count, size) = item.IsFolder ? CopyFolder(item.From, item.To) : CopyFile(item.From, item.To);
            files += count;
            bytes += size;
            log.Add($"{item.What}: {count} ficheros");
            logger.LogInformation("Traspaso: {What}, {Files} ficheros ({Bytes} bytes) de {From}", item.What, count, size, item.From);
        }

        return new TransferResult(true, files, bytes, setAside ? aside : null, log);
    }

    /// <summary>Where Azahar keeps its user data for a PermaLocke folder: its own emulator's, or the installed one's.</summary>
    public static string UserDirectoryOf(string root) =>
        File.Exists(Path.Combine(root, "Emulator", "azahar.exe"))
            ? Path.Combine(root, "Emulator", "user")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar");

    /// <summary>The Ultra Moon save's title folder (<c>…/title/00040000/001b5100</c>) under an emulator's user data.</summary>
    private static string? SaveTitleFolder(string user)
    {
        var sd = Path.Combine(user, "sdmc", "Nintendo 3DS");
        if (!Directory.Exists(sd))
        {
            return null;
        }

        // Las dos carpetas de en medio son identificadores de consola: se buscan, no se suponen (como PlayerSave).
        return Directory.EnumerateDirectories(sd, SaveTitle, SearchOption.AllDirectories)
            .FirstOrDefault(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any());
    }

    /// <summary>A PermaLocke Saves folder with a run in it: a <c>run.json</c>, as <c>JsonRunRepository</c> finds them.</summary>
    private static bool HasRun(string saves) =>
        Directory.Exists(saves) && Directory.EnumerateFiles(saves, "run.json", SearchOption.AllDirectories).Any();

    private static bool HasFiles(string folder) =>
        Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any();

    private static (int Files, long Bytes) CopyFolder(string from, string to)
    {
        var files = 0;
        long bytes = 0;
        foreach (var source in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var (count, size) = CopyFile(source, Path.Combine(to, Path.GetRelativePath(from, source)));
            files += count;
            bytes += size;
        }

        Directory.CreateDirectory(to);
        return (files, bytes);
    }

    private static (int Files, long Bytes) CopyFile(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to, overwrite: false);
        return (1, new FileInfo(to).Length);
    }
}
