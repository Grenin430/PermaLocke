using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/shop.json: what the shop sells, in what order and for how much.</summary>
/// <remarks>
/// An entry with no id or a price below zero is dropped, because a shop cannot sell something it
/// cannot name. Everything else is kept in file order: the grid on screen is the file.
/// </remarks>
public sealed class JsonShopCatalog(IReadOnlyList<ShopItem> items, bool open = true, int opensAtTrial = 0) : IShopCatalog
{
    public IReadOnlyList<ShopItem> Items { get; } = items;

    public bool Open { get; } = open;

    public int OpensAtTrial { get; } = opensAtTrial;

    /// <summary>Empty when the file is missing, so the screen says so instead of inventing a shop.</summary>
    public static JsonShopCatalog Empty { get; } = new([]);

    public static JsonShopCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<ShopFile>(stream);

        if (file?.Items is not { Count: > 0 } entries)
        {
            return Empty;
        }

        return new JsonShopCatalog(
        [
            .. entries
                .Where(entry => entry.Id > 0 && entry.Price >= 0)
                .Select(entry => new ShopItem(
                    entry.Id,
                    string.IsNullOrWhiteSpace(entry.Name) ? $"Objeto {entry.Id}" : entry.Name,
                    entry.Price,
                    string.IsNullOrWhiteSpace(entry.Category) ? ShopItem.Battle : entry.Category.Trim(),
                    entry.UnlockMove ?? 0, entry.UnlockSpecies ?? 0, entry.Nature ?? -1))
        ], file.Open ?? true, Math.Max(0, file.OpensAtTrial ?? 0));
    }

    private sealed record ShopFile(
        [property: JsonPropertyName("items")] IReadOnlyList<ShopEntry>? Items,
        [property: JsonPropertyName("abierta")] bool? Open = null,
        [property: JsonPropertyName("abreEnPrueba")] int? OpensAtTrial = null);

    private sealed record ShopEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("price")] int Price,
        [property: JsonPropertyName("categoria")] string? Category,
        [property: JsonPropertyName("desbloqueaMovimiento")] int? UnlockMove = null,
        [property: JsonPropertyName("especie")] int? UnlockSpecies = null,
        [property: JsonPropertyName("naturaleza")] int? Nature = null);
}
