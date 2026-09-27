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
/// <param name="Nature">
/// A nature herb (0-24, the game's own order): the Mints of later games do not exist in this one, so the herb writes the
/// nature into the save with the game closed. -1 for everything else.
/// </param>
/// <param name="UnlockMove">
/// Not an object for the bag but a move the reminder unlocks for <paramref name="UnlockSpecies"/>, bought once and
/// taught free from then on (2026-09-25: Ascenso Draco, the «mega stone» of Rayquaza). Zero for a normal item.
/// </param>
public sealed record ShopItem(int Id, string Name, int Price, string Category = ShopItem.Battle,
    int UnlockMove = 0, int UnlockSpecies = 0, int Nature = -1)
{
    public const string Battle = "combate";

    public const string MegaStones = "megapiedras";

    /// <summary>The nature herbs (2026-09-27): not an item, a nature written into a Pokémon of the save.</summary>
    public const string Herbs = "hierbas";

    /// <summary>True when buying it changes a Pokémon's nature instead of putting something in the bag.</summary>
    public bool IsHerb => Nature is >= 0 and <= 24;

    /// <summary>True when buying it unlocks a move instead of putting something in the bag.</summary>
    public bool IsUnlock => UnlockMove > 0 && UnlockSpecies > 0;
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
