using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Services;

namespace PermaLocke.Data;

/// <summary>
/// How far this machine has seen each run of the competition, as <c>Config/competicion-vista.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// On this machine and never in the shared folder: a mark the watched player could edit would be a
/// mark they could move back. Five applications each remembering what they saw is five witnesses,
/// and a player cannot rewind what is on their friends' disks (§123).
/// </para>
/// <para>
/// Unreadable is treated as empty, with a log line: losing the marks forgets what was seen, which
/// weakens the rewind check for one refresh, and that is far better than a scoreboard that will not
/// open.
/// </para>
/// </remarks>
public sealed class SeenMarksStore(string configFolder, ILogger<SeenMarksStore>? logger = null)
{
    public const string FileName = "competicion-vista.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string PathOf => Path.Combine(configFolder, FileName);

    public Dictionary<Guid, SeenMark> Load()
    {
        try
        {
            return File.Exists(PathOf)
                ? JsonSerializer.Deserialize<Dictionary<Guid, SeenMark>>(File.ReadAllText(PathOf), Options) ?? []
                : [];
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "No se ha podido leer {File}; se empieza sin marcas", PathOf);
            return [];
        }
    }

    public void Save(IReadOnlyDictionary<Guid, SeenMark> marks)
    {
        Directory.CreateDirectory(configFolder);

        var temporary = PathOf + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(marks, Options));
        File.Move(temporary, PathOf, overwrite: true);
    }
}
