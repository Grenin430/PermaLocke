namespace PermaLocke.App.Views;

/// <summary>
/// How one category of item goes into the bag (2026-10-09): its choreography, its colours and its particles, drawn on the
/// trunk (<see cref="ItemCanvas"/>) as a pure function of the moment and the item's seed. A style keeps no state.
/// </summary>
/// <remarks>
/// The height of the scene is a property of the style and not a case apart: the common ones leave the scene at
/// <see cref="ItemScene.SceneHeight"/>, and a style with a climax asks for what it needs. The width is always
/// <see cref="ItemScene.SceneWidth"/>, which is what the band beside the emulator's bottom screen gives.
/// </remarks>
public abstract class ItemStyle
{
    /// <summary>The height of the scene in pixels of the game. Nothing the style draws goes outside of it.</summary>
    public abstract int Height { get; }

    /// <summary>When each part happens for this item. By default what <see cref="ItemTimeline.For"/> says of its category.</summary>
    public virtual ItemPhases Phases(ItemScene.Item item) => ItemTimeline.For(item.Category, item.Power);

    /// <summary>Draws the scene at a moment, in seconds, after <see cref="ItemCanvas.Clear"/>.</summary>
    public abstract void Draw(ItemCanvas canvas, ItemScene.Item item, ItemPhases phases, double t);
}

/// <summary>Which style each category has.</summary>
public static class ItemStyles
{
    public static ItemStyle For(ItemCategory category) => category switch
    {
        ItemCategory.MegaStone => MegaStoneStyle.Instance,
        ItemCategory.ZCrystal => ZCrystalStyle.Instance,
        ItemCategory.Key => KeyItemStyle.Instance,
        ItemCategory.Evolution => EvolutionStyle.Instance,
        ItemCategory.Machine => MachineStyle.Instance,
        _ => ClassicStyle.Instance
    };

    /// <summary>Every style there is, once each: the ones the categories have and the classic scene that is left over.</summary>
    public static IReadOnlyList<ItemStyle> All { get; } =
        [.. Enum.GetValues<ItemCategory>().Select(For).Append(ClassicStyle.Instance).Distinct()];
}
