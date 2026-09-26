using System.Text.Json;

namespace PermaLocke.Infrastructure;

/// <summary>
/// Which crash reports still have to reach the organiser (§199, plan del próximo torneo, paso 6): the zips
/// <c>EmulatorCrashReport</c> wrote in <c>Diagnosticos\</c> that are not yet in <c>Config/informes-subidos.json</c>.
/// </summary>
/// <remarks>
/// A report written while offline, or before the server had its bucket (<c>17-informes.sql</c>), goes on the next start.
/// Only the last <see cref="MaxAge"/>: the LIMPIEZA removes older ones from the server anyway. The reports themselves
/// are never touched here; only the list of the ones sent is written.
/// </remarks>
public static class CrashReportQueue
{
    public const string SentFile = "informes-subidos.json";

    /// <summary>The start of every report's name, as <c>EmulatorCrashReport</c> writes them.</summary>
    public const string Prefix = "cierre-azahar-";

    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    /// <summary>The reports in <paramref name="folder"/> not sent yet and not older than <see cref="MaxAge"/>, oldest first.</summary>
    public static IReadOnlyList<string> Pending(string folder, string config, DateTimeOffset now)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var sent = Sent(config);
        return [.. new DirectoryInfo(folder).GetFiles(Prefix + "*.zip")
            .Where(file => !sent.Contains(file.Name) && now - file.LastWriteTimeUtc < MaxAge)
            .OrderBy(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)];
    }

    /// <summary>Notes a report as sent, so it is not sent again.</summary>
    public static void MarkSent(string config, string report)
    {
        var sent = Sent(config);
        sent.Add(Path.GetFileName(report));
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, SentFile), JsonSerializer.Serialize(sent.Order(StringComparer.Ordinal)));
    }

    private static HashSet<string> Sent(string config)
    {
        var path = Path.Combine(config, SentFile);
        try
        {
            return File.Exists(path)
                ? new HashSet<string>(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [], StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
