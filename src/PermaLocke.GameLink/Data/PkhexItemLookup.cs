using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>Item names from PKHeX, which ships the cartridge item list for generation 7.</summary>
public sealed class PkhexItemLookup(string language = "es") : IItemLookup
{
    private readonly string[] _names = GameInfo.GetStrings(language).itemlist;

    // El SuperCarameloraro ocupa un hueco que el cartucho no nombra (2026-10-06).
    public string GetName(int itemId) => itemId == PermaLocke.Core.Domain.SuperCandy.ItemId
        ? PermaLocke.Core.Domain.SuperCandy.Name
        : itemId >= 0 && itemId < _names.Length && !string.IsNullOrWhiteSpace(_names[itemId])
            ? _names[itemId]
            : $"Objeto {itemId}";
}
