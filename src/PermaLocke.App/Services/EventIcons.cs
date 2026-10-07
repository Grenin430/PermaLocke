using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// The pixel icon of each kind of event, for the history lists drawn in the pixel style (§176).
/// </summary>
/// <remarks>
/// Next to <see cref="DisplayNames"/> and for the same reason: the domain enum is translated at the edge, once, and an
/// event nobody has given an icon falls back to the plain dot rather than to nothing.
/// </remarks>
public static class EventIcons
{
    public static string Of(GameEventType type) => type switch
    {
        GameEventType.PokemonDied or GameEventType.TeamWiped or GameEventType.DeathRevoked
            or GameEventType.WipeRevoked => "IconGrave",
        GameEventType.PokemonCaught or GameEventType.PokemonDelivered or GameEventType.GachaRoll or GameEventType.NurseryEgg or GameEventType.NurseryHatch
            or GameEventType.FirstPokeBallSeen or GameEventType.BallsWithheld or GameEventType.BallsReturned => "IconGacha",
        GameEventType.WonderTrade or GameEventType.PokemonTraded or GameEventType.PokemonReleased
            or GameEventType.RoleChanged or GameEventType.PlayerLinked => "IconPeople",
        GameEventType.ShopPurchase => "IconShop",
        GameEventType.AchievementUnlocked or GameEventType.AchievementProgressed => "IconTrophy",
        GameEventType.RouletteSpun or GameEventType.RouletteGranted => "IconWheel",
        GameEventType.RewardClaimed or GameEventType.AdminGiftClaimed or GameEventType.TestItemGranted => "IconGift",
        GameEventType.PointsEarned or GameEventType.PointsSpent or GameEventType.PointsAdjusted
            or GameEventType.PointsPenalty or GameEventType.AdminAdjustment => "IconPoints",
        GameEventType.LevelCapEnforced or GameEventType.EvsTrained => "IconDumbbell",
        GameEventType.MoveRemembered => "IconRefresh",
        GameEventType.RomRandomized => "IconRandomizer",
        GameEventType.ZoneConfirmed or GameEventType.ZoneOutcomeSet or GameEventType.ZoneCleared
            or GameEventType.ZoneEncounterSpent => "IconGrid",
        GameEventType.BattleModeChanged => "IconSwords",
        GameEventType.RuleViolation or GameEventType.RuleException or GameEventType.RulesAdopted => "IconDocument",
        GameEventType.RunCreated or GameEventType.RunCompleted => "IconPlay",
        GameEventType.GameStateSynced => "IconCheck",
        _ => "IconDot",
    };
}
