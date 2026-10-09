using PermaLocke.App.Views;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>What an item is for its animation, and how strong: see <see cref="ItemCategory"/> for what each power means.</summary>
public readonly record struct ItemClass(ItemCategory Category, int Power = 0);

/// <summary>
/// Which <see cref="ItemCategory"/> every item of the game belongs to (2026-10-09), told by the cartridge itself.
/// </summary>
/// <remarks>
/// <para>
/// The cartridge says the pocket of every item (<c>FieldItemRandomizer.ReadPockets</c>) and, in its mega evolution table,
/// which items are Mega Stones: that settles the machines, berries, key items, Z-Crystals and the stones, new ones of the
/// mod included. It does not say a Poké Ball from a Choice Band, or a Potion from a Rare Candy, so those are told by id,
/// written down below and checked against the names of PKHeX in the tests, and against the whole item table in
/// <c>ItemCatalogTests</c>. Nothing here guesses a range for the machines: they are not contiguous (§62), the pocket says.
/// </para>
/// <para>
/// What nobody knows is <see cref="ItemCategory.Misc"/>, the plainest animation, so a new item of the mod never breaks.
/// </para>
/// </remarks>
public sealed class ItemCatalog
{
    public static ItemCatalog Empty { get; } = new([], new HashSet<int>());

    private const int GeneralPocket = 0, MedicinePocket = 1, MachinePocket = 2, BerryPocket = 3, KeyPocket = 4;
    private const int CrystalPocket = 5, HeldCrystalPocket = 7;

    /// <summary>Items the mod made from nothing or from another item, whose entry in the table may say anything.</summary>
    private static readonly Dictionary<int, ItemClass> Exceptions = new()
    {
        [SuperCandy.ItemId] = new(ItemCategory.Boost, 3),
        [InfiniteRepel.ItemId] = new(ItemCategory.Key),
        [EggTurbo.ItemId] = new(ItemCategory.Key)
    };

    private static readonly HashSet<int> HiddenMachines = [420, 421, 422, 423, 424, 425, 737];

    private static readonly Dictionary<int, int> HealingPower = Powered(
        (3, [23, 24, 29, 41, 44]),                      // Full Restore, Max Potion, Max Revive, Max Elixir, Sacred Ash
        (2, [25, 28, 37, 39, 40]),                      // Hyper Potion, Revive, Revival Herb, Max Ether, Elixir
        (1, [26, 27, 30, 31, 32, 33, 34, 35, 36, 38, 43])); // Super Potion, Full Heal, drinks, powders, Ether, Berry Juice

    private static readonly Dictionary<int, int> BoostPower = Powered(
        (2, [50]),                                      // Rare Candy
        (1, [45, 46, 47, 48, 49, 52, 53, 645]),         // the vitamins, PP Max, Ability Capsule
        (0, [51, 565, 566, 567, 568, 569, 570]));       // PP Up and the feathers

    private static readonly HashSet<int> Balls = [.. Enumerable.Range(1, 16), .. Enumerable.Range(492, 9), 576, 851];

    private static readonly Dictionary<int, int> BallPower = Powered((3, [1, 851]), (2, [2]), (1, [3, 576]));

    private static readonly HashSet<int> Stones = [80, 81, 82, 83, 84, 85, 107, 108, 109, 849];

    private static readonly HashSet<int> TradeItems =
        [110, 221, 226, 227, 233, 235, 252, 321, 322, 323, 324, 325, 326, 327, 537, 646, 647];

    /// <summary>Held items, plates, gems, drives, orbs and the items of the battle: runs of ids that are one kind.</summary>
    private static readonly HashSet<int> Equipment = Spans(
        (55, 62), (112, 112), (116, 119), (135, 136), (213, 320), (534, 535), (538, 564), (592, 615), (639, 640), (644, 644),
        (648, 650), (715, 715), (879, 884), (904, 920));

    private readonly IReadOnlyList<int> _pockets;
    private readonly IReadOnlySet<int> _megaStones;

    /// <param name="pockets">The pocket of every item by id, as the cartridge says it.</param>
    /// <param name="megaStones">The ids of the Mega Stones, from the cartridge's mega evolution table.</param>
    public ItemCatalog(IReadOnlyList<int> pockets, IReadOnlySet<int> megaStones)
    {
        _pockets = pockets;
        _megaStones = megaStones;
    }

    /// <summary>False when the cartridge could not be read: everything is then <see cref="ItemCategory.Misc"/>.</summary>
    public bool HasData => _pockets.Count > 0;

    public ItemClass Classify(int itemId)
    {
        if (Exceptions.TryGetValue(itemId, out var exception)) return exception;
        if (_megaStones.Contains(itemId)) return new(ItemCategory.MegaStone);

        var pocket = itemId >= 0 && itemId < _pockets.Count ? _pockets[itemId] : -1;

        return pocket switch
        {
            MachinePocket => new(ItemCategory.Machine, HiddenMachines.Contains(itemId) ? 1 : 0),
            BerryPocket => new(ItemCategory.Berry),
            KeyPocket => new(ItemCategory.Key),
            CrystalPocket or HeldCrystalPocket => new(ItemCategory.ZCrystal),
            MedicinePocket => BoostPower.TryGetValue(itemId, out var boost) ? new(ItemCategory.Boost, boost)
                : new(ItemCategory.Healing, HealingPower.GetValueOrDefault(itemId)),
            GeneralPocket => General(itemId),
            _ => new(ItemCategory.Misc)
        };
    }

    private static ItemClass General(int itemId)
    {
        if (Balls.Contains(itemId)) return new(ItemCategory.PokeBall, BallPower.GetValueOrDefault(itemId));
        if (Stones.Contains(itemId)) return new(ItemCategory.Evolution, 1);
        if (TradeItems.Contains(itemId)) return new(ItemCategory.Evolution);
        return Equipment.Contains(itemId) ? new(ItemCategory.Battle) : new(ItemCategory.Misc);
    }

    private static Dictionary<int, int> Powered(params (int Power, int[] Ids)[] groups) =>
        groups.SelectMany(group => group.Ids.Select(id => (id, group.Power))).ToDictionary(pair => pair.id, pair => pair.Power);

    private static HashSet<int> Spans(params (int First, int Last)[] spans) =>
        [.. spans.SelectMany(span => Enumerable.Range(span.First, span.Last - span.First + 1))];
}
