namespace PermaLocke.Core.Domain;

/// <summary>
/// The files of <c>Data/</c> the organiser can change for every player from Admin (2026-09-26), and what each one holds.
/// </summary>
/// <remarks>
/// Only configuration of the competition: prices, points, odds, caps, roles. Not the world (species, maps, zones), which
/// comes from the cartridge, nor the randomizer, whose change needs a new ROM.
/// </remarks>
public static class TournamentRules
{
    public static readonly IReadOnlyList<(string File, string What)> Files =
    [
        ("shop.json", "TIENDA: objetos y precios"),
        ("gacha.json", "GACHA: banners, precios y probabilidades"),
        ("achievements.json", "LOGROS y sus puntos"),
        ("penalties.json", "Penalizaciones por muerte y wipe"),
        ("levelcaps.json", "Caps de nivel por prueba"),
        ("roles.json", "Roles y sus multiplicadores"),
        ("roulette.json", "RULETA"),
        ("guarderia.json", "GUARDERÍA de los roles MONOTYPE"),
        ("grants.json", "Créditos gratis por hito"),
        ("rewards.json", "Premios de una vez"),
        ("wondertrade.json", "Wonder trade"),
        ("rules.json", "Reglas generales")
    ];

    public static bool IsRuleFile(string file) => Files.Any(f => string.Equals(f.File, file, StringComparison.OrdinalIgnoreCase));
}
