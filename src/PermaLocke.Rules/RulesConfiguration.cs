using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules;

/// <summary>Well-known rule identifiers, matching the keys in Data/rules.json.</summary>
public static class RuleIds
{
    public const string FirstEncounter = "firstEncounter";
    public const string ShinyClause = "shinyClause";
    public const string DupesClause = "dupesClause";
    public const string SpeciesClause = "speciesClause";
    public const string GiftPokemon = "giftPokemon";
    public const string StaticPokemon = "staticPokemon";
    public const string LevelCap = "levelCap";
}

public sealed record RuleSettings
{
    public bool Enabled { get; init; } = true;

    public RuleMode Mode { get; init; } = RuleMode.Block;

    /// <summary>Shiny and gift encounters may be configured not to burn the zone.</summary>
    public bool ConsumesEncounter { get; init; } = true;

    /// <summary>When true the dupes clause matches on exact species instead of the whole family.</summary>
    public bool IgnoreEvolutionaryLine { get; init; } = true;

    /// <summary>A rule with a mode of <see cref="RuleMode.Disabled"/> is never evaluated.</summary>
    public bool IsActive => Enabled && Mode != RuleMode.Disabled;
}

/// <summary>
/// Everything the rule engine needs from Data/rules.json. No rule reads the file itself and
/// no threshold lives in code.
/// </summary>
/// <param name="ItemIds">
/// Cartridge item ids of the balls to withhold. Derived from the cartridge item list, not
/// hand-written: an item counts as a ball when its name ends in "Ball" in both English and
/// Spanish, which is what keeps out the Smoke, Light and Iron Balls, that are held items.
/// </param>
public sealed record BallControlSettings
{
    public bool Enabled { get; init; }

    public IReadOnlyList<int> ItemIds { get; init; } = [];

    /// <summary>
    /// The static encounters that can be caught even where nothing else can (§119): outside the map, or in a route
    /// already spent.
    /// </summary>
    public IReadOnlyList<AllowedStatic> AllowedStatics { get; init; } = [];

    /// <summary>
    /// Zones where, until their trial is passed, the wild battles belong to the trial and not to the route (§160).
    /// </summary>
    public IReadOnlyList<TrialZone> TrialZones { get; init; } = [];
}

/// <summary>
/// A zone whose wild battles, until its trial is passed, are the trial's own: the game will not let a ball be thrown in
/// them, so they are nobody's first encounter.
/// </summary>
/// <param name="Note">What happens there, in Spanish, for the log.</param>
/// <param name="Zone">The zone id, as <c>Data/mapas.json</c> names it.</param>
/// <param name="Achievement">
/// The trial's achievement in <c>Data/achievements.json</c>. Its Z-Crystal is what says the trial is over — the same
/// anchor the achievement itself uses (§40, §43) — so the crystal is not written a second time here.
/// </param>
public sealed record TrialZone(string Note, string Zone, string Achievement);

/// <summary>
/// A static encounter the competition lets the player catch, described by what the <b>cartridge</b> holds there.
/// </summary>
/// <remarks>
/// Not by the species the player meets: the randomizer changes it, and the same list has to work for every world.
/// Species, form and level of the cartridge find the entry of the static table, and the installed world says what
/// that entry became. The three are needed together: Necrozma is four entries told apart by form, and two of form 0.
/// </remarks>
/// <param name="Note">What it is, in Spanish, for the notice and the history.</param>
/// <param name="Zone">The zone id where it is fought, as <c>Data/mapas.json</c> names it.</param>
public sealed record AllowedStatic(string Note, string Zone, int Species, int Form, int Level);

public sealed record RulesConfiguration
{
    public RuleMode DefaultRuleMode { get; init; } = RuleMode.Block;

    public IReadOnlyDictionary<string, RuleSettings> Rules { get; init; } =
        new Dictionary<string, RuleSettings>();

    public IReadOnlyList<EncounterType> EncounterTypesThatConsumeZone { get; init; } =
        [EncounterType.Wild, EncounterType.Fishing, EncounterType.Sos];

    /// <summary>
    /// Poké Balls the first-encounter rule takes away while a zone is spent, and the items it
    /// gives back when the player leaves.
    /// </summary>
    /// <remarks>
    /// Empty by default on purpose: with no list, the rule turns itself off rather than guess
    /// which items to confiscate from a player's bag.
    /// </remarks>
    public BallControlSettings BallControl { get; init; } = new();

    /// <summary>Settings for a rule, falling back to the global default when unlisted.</summary>
    public RuleSettings For(string ruleId) =>
        Rules.TryGetValue(ruleId, out var settings)
            ? settings
            : new RuleSettings { Enabled = true, Mode = DefaultRuleMode };

    /// <summary>Configuration used when Data/rules.json is missing, so the engine still works.</summary>
    public static RulesConfiguration Default { get; } = new()
    {
        Rules = new Dictionary<string, RuleSettings>
        {
            [RuleIds.FirstEncounter] = new() { Mode = RuleMode.Block },
            [RuleIds.ShinyClause] = new() { Mode = RuleMode.WarnOnly, ConsumesEncounter = false },
            [RuleIds.DupesClause] = new() { Mode = RuleMode.WarnOnly, IgnoreEvolutionaryLine = true },
            [RuleIds.SpeciesClause] = new() { Enabled = false, Mode = RuleMode.Disabled },
            [RuleIds.GiftPokemon] = new() { Mode = RuleMode.WarnOnly, ConsumesEncounter = false },
            [RuleIds.StaticPokemon] = new() { Mode = RuleMode.WarnOnly, ConsumesEncounter = false },
            [RuleIds.LevelCap] = new() { Mode = RuleMode.Block }
        }
    };
}

/// <summary>Reads Data/rules.json into a <see cref="RulesConfiguration"/>.</summary>
public static class RulesConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>
    /// Returns <see cref="RulesConfiguration.Default"/> when the file is absent, and throws on a
    /// malformed one: silently running with different rules than the file says would be worse.
    /// </summary>
    public static RulesConfiguration Load(string path)
    {
        if (!File.Exists(path))
        {
            return RulesConfiguration.Default;
        }

        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<RulesFile>(stream, Options)
                       ?? throw new InvalidDataException($"{path} está vacío o no es JSON válido.");

        return new RulesConfiguration
        {
            DefaultRuleMode = document.DefaultRuleMode ?? RuleMode.Block,
            Rules = document.Rules ?? RulesConfiguration.Default.Rules,
            EncounterTypesThatConsumeZone = document.EncounterTypesThatConsumeZone
                ?? [EncounterType.Wild, EncounterType.Fishing, EncounterType.Sos],
            BallControl = document.BallControl ?? new BallControlSettings()
        };
    }

    /// <summary>Mirror of the JSON shape; unknown members such as pointRewards are ignored here.</summary>
    private sealed record RulesFile
    {
        public RuleMode? DefaultRuleMode { get; init; }
        public Dictionary<string, RuleSettings>? Rules { get; init; }
        public List<EncounterType>? EncounterTypesThatConsumeZone { get; init; }
        public BallControlSettings? BallControl { get; init; }
    }
}
