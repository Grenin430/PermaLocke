using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/achievements.json: what the run rewards and with how much.</summary>
/// <remarks>
/// An unknown trigger is <b>kept</b>, not dropped. A competition list names things PermaLocke does
/// not watch yet, and an achievement that quietly vanished from the screen would be worse than one
/// that says out loud it cannot be counted.
/// </remarks>
public sealed class JsonAchievementCatalog(IReadOnlyList<Achievement> all) : IAchievementCatalog
{
    public IReadOnlyList<Achievement> All { get; } = all;

    /// <summary>Empty when the file is missing, so the section says so instead of inventing rewards.</summary>
    public static JsonAchievementCatalog Empty { get; } = new([]);

    public static JsonAchievementCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<AchievementFile>(stream);

        if (file?.Achievements is not { Count: > 0 } entries)
        {
            return Empty;
        }

        return new JsonAchievementCatalog(
        [
            .. entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .Select(entry => new Achievement(
                    entry.Id,
                    string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
                    entry.Description ?? string.Empty,
                    Parse(entry.Trigger),
                    entry.Trigger ?? "sin disparador",
                    Math.Max(1, entry.Target ?? 1),
                    entry.Points ?? 0,
                    entry.Record,
                    entry.Item))
        ]);
    }

    /// <summary>The event a trigger names, or null when this build has no such event.</summary>
    private static GameEventType? Parse(string? trigger) =>
        Enum.TryParse<GameEventType>(trigger, ignoreCase: true, out var parsed) ? parsed : null;

    private sealed record AchievementFile(
        [property: JsonPropertyName("achievements")] IReadOnlyList<AchievementEntry>? Achievements);

    private sealed record AchievementEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("trigger")] string? Trigger,
        [property: JsonPropertyName("target")] int? Target,
        [property: JsonPropertyName("points")] int? Points,
        [property: JsonPropertyName("record")] int? Record,
        [property: JsonPropertyName("item")] int? Item);
}
