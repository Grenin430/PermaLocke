using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/grants.json: what each milestone hands over in free rolls and trades.</summary>
/// <remarks>
/// A milestone with no achievement is dropped: what pays for it is the achievement, and one that
/// names none could never be earned. Banner ids are not checked here — the gacha catalogue is what
/// knows them, and a credit on a banner that does not exist is simply a credit nobody can spend.
/// </remarks>
public sealed class JsonCreditCatalog(IReadOnlyList<MilestoneGrant> milestones, bool limitWonderTrades)
    : ICreditCatalog
{
    public IReadOnlyList<MilestoneGrant> Milestones { get; } = milestones;

    public bool LimitWonderTrades { get; } = limitWonderTrades;

    /// <summary>
    /// Empty when the file is missing, and then <b>nothing is limited</b>.
    /// </summary>
    /// <remarks>
    /// Deliberately the permissive way round. Without the file nobody can earn a credit, so
    /// limiting wonder trades would leave the player unable to make any at all — a missing config
    /// file must not lock a feature that used to work.
    /// </remarks>
    public static JsonCreditCatalog Empty { get; } = new([], false);

    public static JsonCreditCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<GrantFile>(stream);

        if (file?.Milestones is not { Count: > 0 } entries)
        {
            return Empty;
        }

        return new JsonCreditCatalog(
            [
                .. entries
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Achievement))
                    .Select(entry => new MilestoneGrant(
                        entry.Achievement!,
                        string.IsNullOrWhiteSpace(entry.Name) ? entry.Achievement! : entry.Name,
                        Rolls(entry.Rolls),
                        Math.Max(0, entry.WonderTrades ?? 0)))
            ],
            file.LimitWonderTrades ?? false);
    }

    private static Dictionary<string, int> Rolls(IReadOnlyDictionary<string, int>? rolls) =>
        (rolls ?? new Dictionary<string, int>())
        .Where(entry => entry.Value > 0)
        .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

    private sealed record GrantFile(
        [property: JsonPropertyName("limitarWonderTrades")] bool? LimitWonderTrades,
        [property: JsonPropertyName("hitos")] IReadOnlyList<GrantEntry>? Milestones);

    private sealed record GrantEntry(
        [property: JsonPropertyName("logro")] string? Achievement,
        [property: JsonPropertyName("nombre")] string? Name,
        [property: JsonPropertyName("gacha")] IReadOnlyDictionary<string, int>? Rolls,
        [property: JsonPropertyName("wonderTrades")] int? WonderTrades);
}
