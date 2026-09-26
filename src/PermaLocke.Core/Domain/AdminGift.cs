namespace PermaLocke.Core.Domain;

/// <param name="Id">The item as the game numbers it.</param>
/// <param name="Name">Its name when the gift was written, so the player reads the same thing the admin sent.</param>
/// <param name="Amount">How many.</param>
public sealed record GiftItem(int Id, string Name, int Amount);

/// <summary>
/// Something the admin sends a player through the shared folder: points, items, free rolls or wonder trades (§129).
/// </summary>
/// <remarks>
/// <para>
/// A gift is a <b>request</b> and never a change. A run lives in its own player's database, hash-chained event by
/// event, so nothing outside that machine can write into it: what travels is this, and the player's own application
/// applies it and records it. That is also what makes it honest — it lands in their history saying who sent it and why.
/// </para>
/// <para>
/// It is not a permission system. Anybody who can write in the shared folder can write one of these; among friends
/// that is the deal, and pretending otherwise would be the fake anti-cheat this project refuses to build.
/// </para>
/// </remarks>
public sealed record AdminGift
{
    /// <summary>2 since point adjustments (2026-09-24). A plain gift is still written as 1, so older players still get it.</summary>
    public const int CurrentSchema = 3;

    /// <summary>The schema an order is written with (2026-09-26): an older application skips it rather than guess.</summary>
    public const int OrderSchema = 3;

    /// <summary>The schema an adjustment is written with: an older player, who would let it wait forever, does not see it.</summary>
    public const int AdjustmentSchema = 2;

    /// <summary>What <see cref="To"/> says when the gift is for everybody.</summary>
    public const string Everybody = "todos";

    public int Schema { get; init; } = 1;

    public required Guid Id { get; init; }

    /// <summary>Who sent it, for the player to read.</summary>
    public required string From { get; init; }

    /// <summary>A player's id, or <see cref="Everybody"/>.</summary>
    public required string To { get; init; }

    /// <summary>Why, in the admin's words. Required: a gift with no reason is a number nobody can explain later.</summary>
    public required string Reason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int Points { get; init; }

    public IReadOnlyList<GiftItem> Items { get; init; } = [];

    /// <summary>Free gacha rolls, by banner id.</summary>
    public IReadOnlyDictionary<string, int> Rolls { get; init; } = new Dictionary<string, int>();

    public int WonderTrades { get; init; }

    /// <summary>
    /// A points adjustment by the organiser (a sanction or a correction): the player's application applies it on its own,
    /// without asking, because a penalty that has to be collected is one nobody collects. Only points.
    /// </summary>
    public bool Adjustment { get; init; }

    /// <summary>An order to the player's application instead of a gift (2026-09-26). Applied on its own, like an adjustment.</summary>
    public AdminOrder? Order { get; init; }

    /// <summary>True when this gift is addressed to that player.</summary>
    public bool IsFor(Guid playerId) =>
        string.Equals(To, Everybody, StringComparison.OrdinalIgnoreCase)
        || (Guid.TryParse(To, out var to) && to == playerId);

    /// <summary>Whether it carries anything at all, which is what the admin's button checks before sending.</summary>
    public bool IsEmpty => Order is null && Points == 0 && Items.Count == 0 && WonderTrades == 0
                           && !Rolls.Any(roll => roll.Value > 0);

    /// <summary>What it gives, in one line, for the inbox and for the admin's own list.</summary>
    public string Say()
    {
        var parts = new List<string>();

        if (Order is not null)
        {
            parts.Add(Order.Summary);
        }

        if (Points != 0)
        {
            parts.Add($"{Points:+#;-#;0} puntos");
        }

        parts.AddRange(Items.Where(item => item.Amount > 0)
            .Select(item => item.Amount == 1 ? item.Name : $"{item.Amount} {item.Name}"));

        parts.AddRange(Rolls.Where(roll => roll.Value > 0)
            .Select(roll => roll.Value == 1 ? $"1 tirada en «{roll.Key}»" : $"{roll.Value} tiradas en «{roll.Key}»"));

        if (WonderTrades > 0)
        {
            parts.Add(WonderTrades == 1 ? "1 wonder trade" : $"{WonderTrades} wonder trades");
        }

        return parts.Count == 0 ? "Nada" : string.Join(" · ", parts);
    }

    /// <summary>True when the gift needs the game open, which is the only part that can have to wait.</summary>
    public bool NeedsTheGame => Items.Any(item => item.Amount > 0);
}
