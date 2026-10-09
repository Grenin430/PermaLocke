namespace PermaLocke.App.Views;

/// <summary>
/// What kind of thing a picked-up item is (2026-10-09): each kind has its own animation going into the bag, and has to be
/// told from the others at a glance, without reading the plate.
/// </summary>
public enum ItemCategory
{
    /// <summary>Whatever fits nowhere else: treasures, repels, fossils, mail. The shortest and simplest animation.</summary>
    Misc,

    /// <summary>TMs and HMs: knowledge being downloaded.</summary>
    Machine,

    /// <summary>Berries: organic and playful.</summary>
    Berry,

    /// <summary>Potions, revives, status cures: warmth and relief. Power 0-3, from a Potion to a Max Revive.</summary>
    Healing,

    /// <summary>Rare Candy, vitamins, PP Up: progress and reward. Power 0-3, from a feather to the Super Candy.</summary>
    Boost,

    /// <summary>Evolution stones and trade items: contained energy. Power 1 for a stone, 0 for a trade item.</summary>
    Evolution,

    /// <summary>Mega Stones: the event of the whole system.</summary>
    MegaStone,

    /// <summary>Held items, plates, gems, X items: strength and presence.</summary>
    Battle,

    /// <summary>Poké Balls. Power 0-3, from a plain ball to the Master Ball.</summary>
    PokeBall,

    /// <summary>Key items: the most solemn.</summary>
    Key,

    /// <summary>Z-Crystals.</summary>
    ZCrystal
}
