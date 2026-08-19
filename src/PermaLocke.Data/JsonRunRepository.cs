using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>
/// Stores each run as <c>Saves/&lt;runId&gt;/run.json</c>. Kept as readable JSON on purpose:
/// the seed, the ROM hash and the randomizer options must be inspectable without tooling.
/// </summary>
public sealed class JsonRunRepository(string savesRoot) : IRunRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Run?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        var path = PathFor(runId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Run>(stream, Options, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Run>> GetAllAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(savesRoot))
        {
            return [];
        }

        var runs = new List<Run>();

        foreach (var file in Directory.EnumerateFiles(savesRoot, "run.json", SearchOption.AllDirectories))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var run = await JsonSerializer.DeserializeAsync<Run>(stream, Options, ct).ConfigureAwait(false);
                if (run is not null)
                {
                    runs.Add(run);
                }
            }
            catch (JsonException)
            {
                // A corrupt run.json must not stop the others from loading; it surfaces as a
                // missing run in the UI and the caller logs it.
            }
        }

        return runs;
    }

    public async Task SaveAsync(Run run, CancellationToken ct = default)
    {
        var path = PathFor(run.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temporary file first so an interrupted save cannot truncate run.json.
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, run, Options, ct).ConfigureAwait(false);
        }

        File.Move(temp, path, overwrite: true);
    }

    private string PathFor(Guid runId) => Path.Combine(savesRoot, runId.ToString("N"), "run.json");
}
