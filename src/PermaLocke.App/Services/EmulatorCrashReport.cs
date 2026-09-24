using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// Writes, the moment Azahar falls over, everything needed to know why: so nobody has to reproduce it on the player's
/// computer, and the player only has to pass on one file.
/// </summary>
/// <remarks>
/// <para>
/// Asked for on 2026-09-22 after a friend's Azahar kept closing and there was no way to see why short of going through
/// their computer with them: «no acabamos nunca si tengo que estar amigo por amigo probando» (§168). The manual collector
/// (<c>RECOGER DIAGNOSTICO.cmd</c>) was already there, but it has to be run, and by then the moment is gone. This runs
/// on its own and keeps what only exists at that moment: what PermaLocke had just asked the emulator for.
/// </para>
/// <para>
/// No ROM, no save and no run: the two logs, the last requests to the emulator, the exit code with what it means,
/// and the computer — Windows, processor, memory, graphics card, the folder, and the Visual C++ runtime the emulator
/// loaded. Written under <c>Diagnosticos\</c> and zipped, and sent nowhere.
/// </para>
/// </remarks>
public sealed class EmulatorCrashReport(AppPaths paths, AzaharInstallation azahar, AzaharRpcClient rpc,
    ILogger<EmulatorCrashReport> logger)
{
    /// <summary>How much of each log goes in: enough for the minutes before, not the whole day.</summary>
    public const int LogLines = 400;

    /// <returns>The zip written, or null when it could not be written.</returns>
    public string? Write(uint exitCode, TimeSpan? session, string emulatorExecutable)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var folder = Path.Combine(paths.Root, "Diagnosticos", $"cierre-azahar-{now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(folder);

            File.WriteAllText(Path.Combine(folder, "resumen.txt"), Summary(now, exitCode, session, emulatorExecutable),
                Encoding.UTF8);
            File.WriteAllLines(Path.Combine(folder, "ultimas-peticiones-al-emulador.txt"), rpc.Trace.Describe(),
                Encoding.UTF8);

            if (Newest(Path.Combine(azahar.Locate(AppContext.BaseDirectory).UserDirectory, "log"), "*.txt") is { } azaharLog)
            {
                File.WriteAllLines(Path.Combine(folder, "azahar-log.txt"), Tail(azaharLog, LogLines), Encoding.UTF8);
            }

            if (Newest(paths.Logs, "*.log") is { } appLog)
            {
                File.WriteAllLines(Path.Combine(folder, "permalocke-log.txt"), Tail(appLog, LogLines), Encoding.UTF8);
            }

            var zip = folder + ".zip";
            ZipFile.CreateFromDirectory(folder, zip);
            logger.LogWarning("Azahar se ha cerrado solo (código 0x{Code:X8}). Informe en {Zip}", exitCode, zip);
            return zip;
        }
        catch (Exception ex)
        {
            // Un informe que no se puede escribir no puede tumbar nada más.
            logger.LogWarning(ex, "No se ha podido escribir el informe del cierre de Azahar");
            return null;
        }
    }

    /// <summary>What an exit code means, in words a player can pass on. Null for a normal exit.</summary>
    public static string? Meaning(uint exitCode) => exitCode switch
    {
        0 => null,
        0xC0000005 => "acceso a memoria no válido dentro del emulador",
        0xC0000409 => "el emulador se ha detenido solo al detectar un fallo interno",
        0xC0000374 => "memoria del emulador dañada",
        0xC00000FD => "desbordamiento de pila dentro del emulador",
        0xC0000135 => "falta una DLL que el emulador necesita",
        0xC0000139 => "una DLL del sistema es demasiado antigua para el emulador (típico del Visual C++)",
        0xC000001D => "el procesador no tiene una instrucción que el emulador usa",
        0xE06D7363 => "error de C++ sin controlar dentro del emulador",
        0xC000013A => "cerrado desde fuera (Ctrl+C, cierre de sesión o apagado)",
        1 => "cerrado a la fuerza desde fuera (Administrador de tareas o similar)",
        _ => "cierre inesperado"
    };

    /// <summary>
    /// Whether an exit is one worth a report: not the player's own close, not PermaLocke's, and not a kill from outside.
    /// </summary>
    public static bool IsCrash(uint exitCode) => exitCode is not (0 or 1 or 0xC000013A);

    private string Summary(DateTimeOffset now, uint exitCode, TimeSpan? session, string emulatorExecutable)
    {
        var text = new StringBuilder();
        var runtime = VisualCppRuntime.Check(emulatorExecutable);

        text.AppendLine("INFORME DE PERMALOCKE: Azahar se ha cerrado sin que nadie lo cerrara");
        text.AppendLine("No contiene ROM, partidas ni tu run. Pásaselo a quien te dio PermaLocke.");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Fecha: {now:yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Código de salida: 0x{exitCode:X8} ({Meaning(exitCode)})");
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Duración de la sesión: {(session is { } s ? s.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "no se sabe")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Emulador: {emulatorExecutable}");
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Visual C++: el emulador necesita {runtime.Needed}; junto al emulador {Say(runtime.Local)}; " +
            $"en Windows {Say(runtime.System)}; {(runtime.Enough ? "suficiente" : "INSUFICIENTE")}");
        text.AppendLine();

        foreach (var (label, value) in Machine.Describe(paths.Root))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{label}: {value}");
        }

        return text.ToString();
    }

    private static string Say(Version? version) => version is null ? "ninguno" : version.ToString();

    private static string? Newest(string folder, string pattern)
    {
        if (!Directory.Exists(folder)) return null;

        return new DirectoryInfo(folder).GetFiles(pattern)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>The last lines of a file another program may still be writing.</summary>
    public static IReadOnlyList<string> Tail(string path, int lines)
    {
        const int Window = 512 * 1024;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var start = Math.Max(0, stream.Length - Window);
        stream.Seek(start, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var all = reader.ReadToEnd().Split('\n');

        // Si se ha empezado a mitad del fichero, la primera línea está cortada y no se enseña; y el salto de línea del
        // final deja un trozo vacío que no es una línea.
        var whole = (start > 0 ? all.Skip(1) : all).Select(line => line.TrimEnd('\r')).ToList();
        if (whole.Count > 0 && whole[^1].Length == 0) whole.RemoveAt(whole.Count - 1);

        return [.. whole.TakeLast(lines)];
    }
}

/// <summary>What there is to say about this computer, for a report. Nothing that identifies the player.</summary>
public static class Machine
{
    public static IReadOnlyList<(string Label, string Value)> Describe(string appRoot) =>
    [
        ("Windows", Environment.OSVersion.VersionString),
        ("Procesador", Registry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "no se sabe"),
        ("Núcleos", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)),
        ("Memoria", $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024):F1} GB"),
        ("Tarjeta gráfica", string.Join(" · ", GraphicsCards()) is { Length: > 0 } cards ? cards : "no se sabe"),
        ("Carpeta de PermaLocke", appRoot),
        ("Dentro de OneDrive", IsInOneDrive(appRoot) ? "SÍ" : "no"),
        ("Espacio libre", FreeSpace(appRoot))
    ];

    /// <summary>
    /// Whether a folder is synchronised by OneDrive, which locks files while it uploads them and can turn them into
    /// placeholders that are not on the disk.
    /// </summary>
    public static bool IsInOneDrive(string path)
    {
        foreach (var variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } root
                && path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return path.Contains(@"\OneDrive\", StringComparison.OrdinalIgnoreCase)
               || path.Contains(@"\OneDrive - ", StringComparison.OrdinalIgnoreCase);
    }

    public static string FreeSpace(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
            return $"{drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB en {drive.Name}";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return "no se sabe";
        }
    }

    private static IEnumerable<string> GraphicsCards()
    {
        const string Display = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(Display);
        if (root is null) yield break;

        foreach (var name in root.GetSubKeyNames().Where(name => name.All(char.IsDigit)))
        {
            using var card = root.OpenSubKey(name);
            if (card?.GetValue("DriverDesc") is string description)
            {
                yield return card.GetValue("DriverVersion") is string version ? $"{description} ({version})" : description;
            }
        }
    }

    private static string? Registry(string key, string value)
    {
        try
        {
            using var opened = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
            return (opened?.GetValue(value) as string)?.Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
