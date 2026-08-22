using System.Text.Json;

namespace PermaLocke.Rules;

/// <param name="Order">1-based position in the tour.</param>
/// <param name="Achievement">
/// The achievement that clears this stage, or null when nothing detects it.
/// <para>
/// This is what turns the cap from something the player remembers to press into something the run
/// works out on its own: the twelve trials count themselves off the Z-Crystal that lands in the
/// bag, so the cap moves the moment the trial is really cleared.
/// </para>
/// </param>
public sealed record LevelCapStage(string Id, int Order, string Name, int Level, string? Achievement = null);

/// <summary>
/// The level ceiling for each stage of the island tour, read from Data/levelcaps.json.
/// </summary>
/// <remarks>
/// The cap in force is the one of the stage the player is <em>about to face</em>: with no
/// trials cleared it is the first entry, after clearing one it is the second, and so on. That
/// is how the user stated it ("1ª prueba: nivel 14").
/// </remarks>
public sealed class LevelCapTable(IReadOnlyList<LevelCapStage> stages)
{
    public IReadOnlyList<LevelCapStage> Stages { get; } = stages;

    /// <param name="clearedStages">How many milestones the player has already completed.</param>
    public LevelCapStage? Current(int clearedStages) =>
        Stages.Count == 0 ? null : Stages[Math.Clamp(clearedStages, 0, Stages.Count - 1)];

    public int? CapFor(int clearedStages) => Current(clearedStages)?.Level;

    public static LevelCapTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LevelCapTable([]);
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<CapFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new LevelCapTable(
        [
            .. (file?.Caps ?? [])
                .OrderBy(c => c.Order)
                .Select(c => new LevelCapStage(c.Id, c.Order, c.Name, c.Level, c.Logro))
        ]);
    }

    private sealed record CapEntry(string Id, int Order, string Name, int Level, string? Logro);

    private sealed record CapFile(List<CapEntry>? Caps);
}
