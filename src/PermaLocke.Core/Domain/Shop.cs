namespace PermaLocke.Core.Domain;

/// <summary>One thing the shop sells.</summary>
/// <param name="Id">The cartridge's own item id, which is what gets written into the bag.</param>
/// <param name="Name">
/// What the competition calls it, which is not always what the cartridge calls it: the shop says
/// "Cinta Elección" where Ultra Moon says "Cinta Elegida". The name is decoration; the id decides.
/// </param>
/// <param name="Category">
/// Which counter it sits behind. The shop grew a second one -- the Mega Stones -- and mixing
/// forty-seven stones into the same grid as the battle items would bury the battle items.
/// </param>
public sealed record ShopItem(int Id, string Name, int Price, string Category = ShopItem.Battle)
{
    public const string Battle = "combate";

    public const string MegaStones = "megapiedras";
}

/// <summary>What the shop sells, read from configuration rather than compiled in.</summary>
public interface IShopCatalog
{
    IReadOnlyList<ShopItem> Items { get; }
}

/// <summary>How a purchase ended.</summary>
public enum PurchaseOutcome
{
    /// <summary>Paid for and seen in the bag afterwards.</summary>
    Delivered,

    /// <summary>Not enough points. Nothing was charged.</summary>
    NotEnoughPoints,

    /// <summary>The game is not there to write into. Nothing was charged.</summary>
    GameUnreachable,

    /// <summary>The bag is full for that pocket, or the write did not stick. Nothing was charged.</summary>
    NotDelivered,

    /// <summary>No such item in the catalogue.</summary>
    UnknownItem
}

/// <param name="Balance">Points left afterwards. Unchanged unless the purchase went through.</param>
/// <param name="Carried">How many of the item the bag holds now.</param>
public sealed record PurchaseResult(
    PurchaseOutcome Outcome,
    ShopItem? Item,
    int Balance,
    int Carried,
    string Message)
{
    public bool Succeeded => Outcome == PurchaseOutcome.Delivered;
}
