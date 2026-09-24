using System.Text.Json;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Play sessions as <c>Saves/&lt;run&gt;/sesiones.json</c>, beside the run they belong to.</summary>
/// <remarks>
/// In the run's folder on purpose: EMPEZAR DE CERO deletes that folder, and the hours of a run that no
/// longer exists are not the new run's hours. A file that cannot be read counts as no sessions rather
/// than stopping the launcher from opening the game.
/// </remarks>
public sealed class JsonPlaytimeStore(string savesRoot) : IPlaytimeStore
{
    public const string FileName = "sesiones.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string PathFor(Guid runId) => Path.Combine(savesRoot, runId.ToString("N"), FileName);

    public async Task<IReadOnlyList<PlaySession>> LoadAsync(Guid runId, CancellationToken ct = default)
    {
        var path = PathFor(runId);

        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<PlaySession>>(stream, Options, ct).ConfigureAwait(false) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task SaveAsync(Guid runId, IReadOnlyList<PlaySession> sessions, CancellationToken ct = default)
    {
        var path = PathFor(runId);

        // Solo si la run tiene su carpeta: escribir aqui no puede resucitar la de una run borrada.
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            return;
        }

        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, sessions, Options, ct).ConfigureAwait(false);
        }

        File.Move(temporary, path, overwrite: true);
    }
}
