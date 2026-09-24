using System.Text.Json;

namespace PermaLocke.Data;

/// <summary>
/// The one place that knows where this machine's shared folder is written down (§129).
/// </summary>
/// <remarks>
/// Both programs on this PC need it — PermaLocke to publish and read, the admin to leave gifts — and two readings of
/// the same setting is two things that can disagree the day one of them changes. It is one small file and this is the
/// only code that opens it.
/// </remarks>
public sealed class SharedFolderSettings(string configFolder)
{
    public const string FileName = "sync.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string Path => System.IO.Path.Combine(configFolder, FileName);

    /// <summary>The application's folder: the one that holds <c>Config\</c>.</summary>
    private string AppRoot => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(configFolder)) ?? configFolder;

    /// <summary>The folder, or empty when nobody has chosen one. Never throws: an unreadable setting is no setting.</summary>
    /// <remarks>
    /// A relative path is taken from the application's folder: the copies handed to friends live inside the
    /// shared folder and say <c>..</c>, which is right on any machine and any drive letter.
    /// </remarks>
    public string Read()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(Path));

            var folder = document.RootElement.TryGetProperty("sharedFolder", out var value)
                         && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

            // Relativa a la carpeta de la aplicación, no al directorio desde el que se arrancó: así las
            // copias que viven DENTRO de la carpeta compartida la encuentran con «..» sea cual sea la
            // letra de unidad de cada uno. Un acceso directo arranca donde quiere, y resolverla contra
            // eso daría una carpeta distinta según cómo se abriera.
            return folder.Length == 0 || System.IO.Path.IsPathRooted(folder)
                ? folder
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(AppRoot, folder));
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Write(string folder)
    {
        Directory.CreateDirectory(configFolder);
        File.WriteAllText(Path, JsonSerializer.Serialize(new { sharedFolder = folder ?? string.Empty }, Options));
    }
}
