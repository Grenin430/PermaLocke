using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/rewards.json: what the competition hands over once, and for what.</summary>
/// <remarks>
/// A reward with no id, no achievements or no items is dropped. Each of the three makes it
/// meaningless in a different way — nothing to record it under, nothing to earn it with, nothing
/// to give — and a reward that looks like a button and hands over nothing is exactly the kind of
/// thing this project does not ship.
/// </remarks>
public sealed class JsonRewardCatalog(IReadOnlyList<Reward> all) : IRewardCatalog
{
    public IReadOnlyList<Reward> All { get; } = all;

    /// <summary>Empty when the file is missing, so the screen says so instead of inventing a prize.</summary>
    public static JsonRewardCatalog Empty { get; } = new([]);

    public static JsonRewardCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<RewardFile>(stream);

        if (file?.Rewards is not { Count: > 0 } entries)
        {
            return Empty;
        }

        return new JsonRewardCatalog(
        [
            .. entries
                .Select(Read)
                .OfType<Reward>()
        ]);
    }

    private static Reward? Read(RewardEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            return null;
        }

        var achievements = entry.Achievements?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? [];

        var items = entry.Items?
            .Where(item => item.Id > 0 && item.Amount > 0)
            .Select(item => new RewardItem(
                item.Id,
                string.IsNullOrWhiteSpace(item.Name) ? $"Objeto {item.Id}" : item.Name,
                item.Amount))
            .ToList() ?? [];

        var held = entry.Held?.Where(id => id > 0).ToList() ?? [];

        // Hace falta ALGO que ganar y algo que dar. Un premio sin condicion se cobraria el primer
        // dia, y uno sin objetos seria un boton que no entrega nada.
        if ((achievements.Count == 0 && held.Count == 0) || items.Count == 0)
        {
            return null;
        }

        return new Reward(
            entry.Id,
            string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
            entry.Description ?? string.Empty,
            achievements,
            items,
            held,
            entry.Automatic);
    }

    private sealed record RewardFile(
        [property: JsonPropertyName("rewards")] IReadOnlyList<RewardEntry>? Rewards);

    private sealed record RewardEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("achievements")] IReadOnlyList<string>? Achievements,
        [property: JsonPropertyName("objetosEnMochila")] IReadOnlyList<int>? Held,
        [property: JsonPropertyName("automatico")] bool Automatic,
        [property: JsonPropertyName("items")] IReadOnlyList<RewardItemEntry>? Items);

    private sealed record RewardItemEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("amount")] int Amount);
}
