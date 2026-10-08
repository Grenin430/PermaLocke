using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// Spanish labels for the enums the interface shows.
/// </summary>
/// <remarks>
/// <para>
/// The domain is written in English, as the whole codebase is, but the player reads the log and
/// the island list. <c>ToString()</c> on an enum leaks names like <c>LevelCapEnforced</c> into a
/// Spanish screen, so the translation happens once, here, at the edge.
/// </para>
/// <para>
/// This is presentation only. A value with no entry falls back to its own name rather than to an
/// empty string: a label nobody translated yet should look untranslated, not missing.
/// </para>
/// </remarks>
public static class DisplayNames
{
    private static readonly Dictionary<GameEventType, string> Events = new()
    {
        [GameEventType.RunCreated] = "Run creada",
        [GameEventType.RunCompleted] = "Run terminada",

        [GameEventType.PointsEarned] = "Puntos ganados",
        [GameEventType.PointsSpent] = "Puntos gastados",
        [GameEventType.PointsAdjusted] = "Puntos ajustados",
        [GameEventType.PointsPenalty] = "Penalización",

        [GameEventType.PokemonCaught] = "Captura",
        [GameEventType.PokemonDied] = "Muerte",
        [GameEventType.PokemonReleased] = "Liberado",
        [GameEventType.PokemonTraded] = "Intercambiado",

        [GameEventType.GachaRoll] = "Tirada de gacha",
        [GameEventType.ShopPurchase] = "Compra",
        [GameEventType.WonderTrade] = "Wonder trade",
        [GameEventType.NurseryEgg] = "Huevo de la guardería",
        [GameEventType.NurseryHatch] = "Huevo eclosionado",
        [GameEventType.PokemonEvolved] = "Cambio de especie",
        [GameEventType.AbilityRestored] = "Habilidad devuelta",

        [GameEventType.AchievementUnlocked] = "Logro conseguido",
        [GameEventType.AchievementProgressed] = "Progreso a mano",

        [GameEventType.RuleViolation] = "Regla incumplida",
        [GameEventType.RuleException] = "Excepción de regla",
        [GameEventType.LevelCapEnforced] = "Nivel máximo",

        [GameEventType.AdminAdjustment] = "Ajuste de administrador",
        [GameEventType.GameStateSynced] = "Sincronización",
        [GameEventType.RomRandomized] = "ROM randomizada",
        [GameEventType.TestItemGranted] = "Objeto de prueba",
        [GameEventType.BallsWithheld] = "Balls retiradas",
        [GameEventType.BallsReturned] = "Balls devueltas",
        [GameEventType.TeamWiped] = "Equipo caído",
        [GameEventType.EvsTrained] = "EV repartidos",
        [GameEventType.PokemonDelivered] = "Entregado en la partida",
        [GameEventType.RewardClaimed] = "Premio recogido",
        [GameEventType.RouletteSpun] = "Ruleta",
        [GameEventType.RouletteGranted] = "Tiradas concedidas",
        [GameEventType.RoleChanged] = "Cambio de rol",
        [GameEventType.BattleModeChanged] = "Modo combate",
        [GameEventType.ZoneConfirmed] = "Zona gastada",
        [GameEventType.ZoneCleared] = "Zona liberada",
        [GameEventType.DeathRevoked] = "Muerte revocada",
        [GameEventType.WipeRevoked] = "Equipo caído revocado",
        [GameEventType.PokemonRenamed] = "Mote cambiado",
        [GameEventType.IntegrityFlag] = "Aviso de integridad",
        [GameEventType.ZoneOutcomeSet] = "Zona marcada",
        [GameEventType.ZoneEncounterSpent] = "Encuentro gastado",
        [GameEventType.FirstPokeBallSeen] = "Primera Poké Ball",
        [GameEventType.PlayerLinked] = "Run vinculada al jugador",
        [GameEventType.RulesAdopted] = "Reglas oficiales adoptadas",
        [GameEventType.AdminGiftClaimed] = "Regalo recogido",
        [GameEventType.MoveRemembered] = "Movimiento recordado"
    };

    private static readonly Dictionary<EncounterType, string> Encounters = new()
    {
        [EncounterType.Wild] = "Salvaje",
        [EncounterType.Gift] = "Regalo",
        [EncounterType.Static] = "Estático",
        [EncounterType.Legendary] = "Legendario",
        [EncounterType.Starter] = "Inicial",
        [EncounterType.Trade] = "Intercambio",
        [EncounterType.Fishing] = "Pesca",
        [EncounterType.Sos] = "Petición de ayuda",
        [EncounterType.Special] = "Especial",
        [EncounterType.Unknown] = "Sin determinar"
    };

    private static readonly Dictionary<IslandState, string> Islands = new()
    {
        [IslandState.Locked] = "bloqueada",
        [IslandState.InProgress] = "en curso",
        [IslandState.Completed] = "completada"
    };

    public static string Of(GameEventType type) =>
        Events.TryGetValue(type, out var name) ? name : type.ToString();

    public static string Of(EncounterType type) =>
        Encounters.TryGetValue(type, out var name) ? name : type.ToString();

    public static string Of(IslandState state) =>
        Islands.TryGetValue(state, out var name) ? name : state.ToString();

    /// <summary>Where a Pokémon came into the run from.</summary>
    private static readonly Dictionary<PokemonOrigin, string> Origins = new()
    {
        [PokemonOrigin.Capture] = "Capturado",
        [PokemonOrigin.Gacha] = "Gacha",
        [PokemonOrigin.WonderTrade] = "Wonder trade",
        [PokemonOrigin.Gift] = "Regalo",
        [PokemonOrigin.Starter] = "Inicial",
        [PokemonOrigin.AdminGrant] = "Dado por el admin",
        [PokemonOrigin.Nursery] = "Guardería"
    };

    /// <summary>
    /// How a Pokémon ended up. In the past tense on purpose: these read as a fate in a list, not as
    /// a state in a form.
    /// </summary>
    private static readonly Dictionary<PokemonStatus, string> Fates = new()
    {
        [PokemonStatus.Alive] = "En pie",
        [PokemonStatus.Dead] = "Caído",
        [PokemonStatus.Released] = "Liberado",
        [PokemonStatus.Traded] = "Entregado"
    };

    public static string Of(PokemonOrigin origin) =>
        Origins.TryGetValue(origin, out var name) ? name : origin.ToString();

    public static string Of(PokemonStatus status) =>
        Fates.TryGetValue(status, out var name) ? name : status.ToString();
}
