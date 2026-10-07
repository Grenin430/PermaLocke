using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Data;

/// <summary>Item names from PKHeX, which ships the cartridge item list for generation 7.</summary>
public sealed class PkhexItemLookup(string language = "es") : IItemLookup
{
    private readonly string[] _names = GameInfo.GetStrings(language).itemlist;

    // El SuperCarameloraro y el Repelente Infinito ocupan huecos que el cartucho no nombra (2026-10-06, 2026-10-07).
    public string GetName(int itemId) => itemId switch
    {
        PermaLocke.Core.Domain.SuperCandy.ItemId => PermaLocke.Core.Domain.SuperCandy.Name,
        PermaLocke.Core.Domain.InfiniteRepel.ItemId => PermaLocke.Core.Domain.InfiniteRepel.Name,
        PermaLocke.Core.Domain.EggTurbo.ItemId => PermaLocke.Core.Domain.EggTurbo.Name,
        _ => itemId >= 0 && itemId < _names.Length && !string.IsNullOrWhiteSpace(_names[itemId])
            ? _names[itemId]
            : $"Objeto {itemId}"
    };
}
