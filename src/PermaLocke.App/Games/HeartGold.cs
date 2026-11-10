using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Games;

/// <summary>
/// HeartGold, at its simplest (2026-11-10, asked so): the Universal Pokémon Randomizer does the randomizing exactly as it does on its own,
/// and melonDS plays the result. PermaLocke only finds the files and calls them. Nothing of the rest of the app is used for this game.
/// </summary>
/// <remarks>
/// The player's ROM is read from <c>ROM/</c> (the first <c>.nds</c> whose header says HeartGold) and never touched: the Randomizer writes a new
/// one in <c>Randomized/</c>. The tools live in <c>Emulator/</c>: the Randomizer in <c>upr/</c> with PermaLocke's basic settings, melonDS in
/// <c>melonDS/</c>, a portable Java in <c>java/</c> (the system's Java when there is none). A copy without them downloads them once, as one
/// zip from the repository's releases (a pre-release, so the app's updater never takes it for the latest version), checked by its SHA-256.
/// </remarks>
public sealed class HeartGold(AppPaths paths)
{
    /// <summary>The tools' zip: melonDS, the Randomizer and the portable Java, put up once as a pre-release.</summary>
    public const string ToolsUrl = "https://github.com/Grenin430/PermaLocke/releases/download/herramientas-heartgold-1/herramientas-heartgold-1.zip";

    public const string ToolsSha256 = "376a163e72c44b02f55bfa738415b070f5c3b5fe3eff13f7dc5ecbd4d21f05d3";

    private sealed record Saved(string? Settings, string? LastRandomized);

    private string StateFile => Path.Combine(paths.Config, "heartgold.json");

    private string Tools => Path.Combine(paths.Root, "Emulator");

    public string RandomizerJar => Path.Combine(Tools, "upr", "PokeRandoZX.jar");

    public string Emulator => Path.Combine(Tools, "melonDS", "melonDS.exe");

    /// <summary>PermaLocke's basic settings for the Randomizer: wilds by area, starters, trainers and statics at random.</summary>
    public string DefaultSettings => Path.Combine(Tools, "upr", "PermaLocke-basico.rnqs");

    /// <summary>Whether the Randomizer and melonDS are there.</summary>
    public bool ToolsReady => File.Exists(RandomizerJar) && File.Exists(Emulator);

    /// <summary>The portable Java in Emulator/java when it is there; otherwise the system's, by name.</summary>
    private string Java(bool window)
    {
        var own = Path.Combine(Tools, "java", "bin", window ? "javaw.exe" : "java.exe");
        return File.Exists(own) ? own : window ? "javaw" : "java";
    }

    /// <summary>The player's HeartGold ROM in ROM/, told by its header's game code (IPK and the region letter); null when there is none.</summary>
    public string? FindRom()
    {
        if (!Directory.Exists(paths.Rom)) return null;
        foreach (var file in Directory.EnumerateFiles(paths.Rom, "*.nds"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var header = new byte[16];
                if (stream.Read(header, 0, 16) == 16 && Encoding.ASCII.GetString(header, 12, 3) == "IPK") return file;
            }
            catch (IOException)
            {
                // un fichero que no se deja leer no es la ROM que buscamos
            }
        }

        return null;
    }

    /// <summary>The settings the player chose, while the file is still there.</summary>
    public string? ChosenSettings
    {
        get => Load().Settings is { } chosen && File.Exists(chosen) ? chosen : null;
        set => Save(Load() with { Settings = value });
    }

    /// <summary>The settings that will be used: the player's, or PermaLocke's basic ones; null when there is neither.</summary>
    public string? Settings => ChosenSettings ?? (WriteDefaultSettings() ? DefaultSettings : null);

    /// <summary>
    /// The basic settings travel inside the exe, so an update changes them for everyone (the tools' zip keeps the old ones): written to
    /// upr/ each time, when the Randomizer is there. Starters from three-stage lines, random movesets and abilities, impossible evolutions
    /// fixed, each species its own abilities (they change on evolving); types, stats and evolutions as the cartridge has them, like Ultra Luna.
    /// </summary>
    private bool WriteDefaultSettings() => WriteResource("PermaLocke.App.HeartGold.rnqs", DefaultSettings);

    /// <summary>PermaLocke's own step after the Randomizer (no Master Ball on the ground, no one-hit KO moves); source in Games/PermaLockeTweaks.java.</summary>
    private string ExtrasJar => Path.Combine(Tools, "upr", "PermaLocke-upr.jar");

    /// <summary>Writes a file that travels inside the exe into upr/, when the Randomizer's folder is there.</summary>
    private static bool WriteResource(string name, string path)
    {
        try
        {
            if (!Directory.Exists(Path.GetDirectoryName(path))) return false;
            using var embedded = typeof(HeartGold).Assembly.GetManifestResourceStream(name);
            if (embedded is null) return File.Exists(path);
            using var file = File.Create(path);
            embedded.CopyTo(file);
            return true;
        }
        catch (IOException)
        {
            return File.Exists(path);
        }
    }

    /// <summary>The last ROM the Randomizer made, when it is still there.</summary>
    public string? LastRandomized
    {
        get => Load().LastRandomized is { } last && File.Exists(last) ? last : null;
        private set => Save(Load() with { LastRandomized = value });
    }

    /// <summary>Opens the Randomizer's own window, to make and save the settings.</summary>
    public void OpenRandomizer() =>
        Process.Start(new ProcessStartInfo(Java(window: true)) { ArgumentList = { "-jar", RandomizerJar }, WorkingDirectory = Path.GetDirectoryName(RandomizerJar)!, UseShellExecute = false });

    /// <summary>
    /// Runs the Randomizer from the command line on the player's ROM with the settings; the new ROM goes to Randomized/. Returns what it
    /// printed, and whether it made the ROM.
    /// </summary>
    public async Task<(bool Done, string Output)> RandomizeAsync(string rom, string settings)
    {
        Directory.CreateDirectory(paths.Randomized);
        var output = Path.Combine(paths.Randomized, $"HeartGold-{DateTime.Now:yyyyMMdd-HHmmss}.nds");
        var (done, text) = await RunJavaAsync("-Xmx4096M", "-jar", RandomizerJar, "cli", "-s", settings, "-i", rom, "-o", output).ConfigureAwait(false);
        if (done && File.Exists(output))
        {
            // what the Randomizer has no option for: no Master Ball on the ground, no one-hit KO moves (PermaLockeTweaks)
            if (!WriteResource("PermaLocke.App.HeartGold.upr.jar", ExtrasJar)) return (false, text + "\nFalta el paso de PermaLocke (PermaLocke-upr.jar).");
            var (fixedOk, fixedText) = await RunJavaAsync("-cp", $"{RandomizerJar}{Path.PathSeparator}{ExtrasJar}", "PermaLockeTweaks", output).ConfigureAwait(false);
            text += "\n" + fixedText;
            done = fixedOk;
        }
        else
        {
            done = false;
        }

        if (done) LastRandomized = output;
        return (done, text);
    }

    private async Task<(bool Done, string Output)> RunJavaAsync(params string[] args)
    {
        var start = new ProcessStartInfo(Java(window: false))
        {
            WorkingDirectory = Path.GetDirectoryName(RandomizerJar)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se ha podido abrir Java.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode == 0, ((await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false))).Trim());
    }

    /// <summary>Opens melonDS with a ROM.</summary>
    public void Play(string rom) =>
        Process.Start(new ProcessStartInfo(Emulator) { ArgumentList = { rom }, WorkingDirectory = Path.GetDirectoryName(Emulator)!, UseShellExecute = false });

    /// <summary>Rare Candy's item id, the same in every generation.</summary>
    public const int RareCandy = 50;

    /// <summary>The in-game save melonDS keeps next to the ROM.</summary>
    public static string SaveOf(string rom) => Path.ChangeExtension(rom, ".sav");

    /// <summary>
    /// Puts Rare Candies in the bag of a ROM's in-game save (2026-11-10, asked for). Only with melonDS closed: it writes the save on exit.
    /// A copy of the save is left next to it first. Returns how many the bag holds now. A savestate loaded afterwards brings back its own
    /// save, so the candies are only there when the game is loaded from the in-game save.
    /// </summary>
    public static int AddRareCandies(string rom, int amount)
    {
        if (Process.GetProcessesByName("melonDS").Length > 0) throw new InvalidOperationException("Cierra melonDS primero (guardando antes en el juego).");
        var path = SaveOf(rom);
        if (!File.Exists(path)) throw new FileNotFoundException("Esta ROM aún no tiene partida guardada. Guarda en el juego y cierra melonDS.");
        if (!PKHeX.Core.SaveUtil.TryGetSaveFile(path, out var save) || save is not PKHeX.Core.SAV4HGSS)
        {
            throw new InvalidDataException("La partida no es de HeartGold o no se puede leer.");
        }

        var now = PutRareCandies(save, amount);
        File.Copy(path, $"{path}.antes-caramelos-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        File.WriteAllBytes(path + ".tmp", save.Write().ToArray());
        File.Move(path + ".tmp", path, overwrite: true);
        return now;
    }

    /// <summary>The bag part: adds the candies to the medicine pocket (the slot they have, or the first empty one), up to the pocket's cap.</summary>
    internal static int PutRareCandies(PKHeX.Core.SaveFile save, int amount)
    {
        var bag = save.Inventory;
        var pouch = bag.Pouches.First(p => p.Type == PKHeX.Core.InventoryType.Medicine);
        var slot = pouch.Items.FirstOrDefault(i => i.Index == RareCandy) ?? pouch.Items.FirstOrDefault(i => i.Index == 0)
            ?? throw new InvalidOperationException("El bolsillo de medicinas está lleno.");
        slot.Index = RareCandy;
        slot.Count = Math.Min(pouch.MaxCount, slot.Count + amount);
        bag.CopyTo(save);
        return slot.Count;
    }

    /// <summary>
    /// Downloads the tools' zip, checks it is exactly the one put up (SHA-256) and unpacks it into Emulator/. Reports the share done, 0 to 1.
    /// Nothing is unpacked from a zip that does not match.
    /// </summary>
    public async Task DownloadToolsAsync(IProgress<double> progress, CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"permalocke-heartgold-{Guid.NewGuid():N}.zip");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PermaLocke");
            using var response = await http.GetAsync(ToolsUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 60_000_000L;

            await using (var file = File.Create(temp))
            await using (var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;
                    progress.Report(Math.Min(1, done / (double)total));
                }
            }

            await using (var check = File.OpenRead(temp))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, ct).ConfigureAwait(false));
                if (!hash.Equals(ToolsSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("El paquete descargado no es el esperado (su huella no coincide). No se ha instalado nada.");
                }
            }

            ZipFile.ExtractToDirectory(temp, Tools, overwriteFiles: true);
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { }
        }
    }

    private Saved Load()
    {
        try
        {
            return File.Exists(StateFile) ? JsonSerializer.Deserialize<Saved>(File.ReadAllText(StateFile)) ?? new(null, null) : new(null, null);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new(null, null);
        }
    }

    private void Save(Saved state)
    {
        Directory.CreateDirectory(paths.Config);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(state));
    }
}
