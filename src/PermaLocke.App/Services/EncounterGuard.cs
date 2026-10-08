using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Field;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>Something the guard wants the player to hear, and the Pokémon it is about when there is one.</summary>
public sealed record EncounterNotice(ToastKind Kind, string Title, string Message, int? Species);

/// <summary>A wild Pokémon the game has just counted as caught (§190).</summary>
/// <param name="Pokemon">The whole Pokémon as it was read in the battle; null if it could not be read.</param>
public sealed record WildCatch(int Species, bool Shiny, PKHeX.Core.PK7? Pokemon);

/// <summary>
/// Watches the game's wild battles: marks the map with each route's first encounter, and keeps the Poké Balls where
/// the competition's first-encounter rule says.
/// </summary>
/// <remarks>
/// <para>
/// Runs inside the battle loop of <see cref="GameLinkMonitor"/>, four times a second in battle and twice outside.
/// Out of a battle it reads the zone and, with the ball rule on, takes the balls away while the player stands in a
/// spent route. When the game's wild battle counter goes up it has a wild battle: it takes the zone confirmed just
/// before, asks the battle tables for the Pokémon at once, and decides — shiny, spent, duplicate or first encounter —
/// with <see cref="EncounterPolicy"/> (§117).
/// </para>
/// <para>
/// The map is the part that does not depend on the ball rule. Since §118 nobody can mark it by hand, so this is the
/// only thing that does: the first wild battle of a free route spends it, and when it ends the map gets how. That
/// runs with the rule switched off too — a map that only fills in while another setting is on would be a map that
/// quietly stops being a record.
/// </para>
/// <para>
/// Everything it does to the bag and the run goes through <see cref="BallControlService"/>, which writes an event
/// for every step.
/// </para>
/// </remarks>
public sealed class EncounterGuard(
    FieldZoneReader zones,
    IBattleCounters counters,
    BattleTableReader battleTables,
    BallControlService balls,
    IOwnedSpecies dex,
    IPokemonRepository pokemon,
    IEvolutionLineProvider lines,
    ISpeciesLookup speciesNames,
    WorldAllowedStatics allowedStatics,
    TrialZoneService trials,
    AppPaths paths,
    IClock clock,
    RulesConfiguration rules,
    AzaharGameWriter writer,
    ILogger<EncounterGuard> logger)
{
    /// <summary>How old the confirmed zone may be for a battle to be placed in it.</summary>
    /// <remarks>
    /// Was 60 s, and that is how a battle ended up in the previous route (§118): walking into a new map left the zone
    /// unreadable, and the last one confirmed — the map just left — was still young enough. The zone is confirmed twice
    /// a second while it reads, so anything older than a few seconds means it has stopped reading, and then the
    /// battle's zone is read when it ends, where the player still stands.
    /// </remarks>
    private static readonly TimeSpan ZoneMaxAge = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long after a battle the game may take to count its capture or its escape. Measured: a Ditto fled from in
    /// Ruta 7 read with the escape not yet counted 11 ms after the battle tables went, while the counter had it after.
    /// </summary>
    private static readonly TimeSpan CountGrace = TimeSpan.FromSeconds(4);

    /// <summary>How long a battle may go without its Pokémon being read before the doubt goes to the player.</summary>
    private static readonly TimeSpan SpeciesDeadline = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How long after a battle the zone and the counters may take to read again before the mark is given up. Leaving
    /// a battle is a fade and a reload of the field, and the position records are not valid until it is over.
    /// </summary>
    private static readonly TimeSpan SettleDeadline = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A battle the tables never showed ends when the zone reads again; this is how long that may take before it is
    /// taken as over anyway, so a broken zone reader cannot keep every later battle from being seen.
    /// </summary>
    private static readonly TimeSpan UnseenBattleLimit = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How long an unknown zone keeps the bag as it was. A flight's loading or a trainer battle leaves the zone
    /// unreadable; handing the balls back and taking them again every time would fill the history with noise.
    /// </summary>
    private static readonly TimeSpan HoldWhileUnknown = TimeSpan.FromSeconds(60);

    /// <summary>How often, while withholding, the bag is checked for balls bought or picked up since.</summary>
    private static readonly TimeSpan RecheckWithheld = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan SpentCacheLife = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RoutesCacheLife = TimeSpan.FromSeconds(60);

    private BattleCounters? _last;

    /// <summary>The run the battle state belongs to; a different one throws it all away.</summary>
    private Guid? _runId;
    private string? _zoneProblem = "Todavía no se ha identificado la ruta.";
    private string? _reportedProblem;
    public string? Problem => counters.Problem ?? _zoneProblem;

    private void ReportProblem()
    {
        var problem = Problem;
        if (problem == _reportedProblem) return;
        _reportedProblem = problem;
        if (problem is null) logger.LogInformation("Detección de encuentros preparada: contadores y zona disponibles");
        else logger.LogWarning("Detección de encuentros pendiente: {Problem}", problem);
    }
    private WildBattle? _battle;
    private BallAction? _applied;
    private DateTimeOffset _appliedAt;
    private DateTimeOffset? _unknownSince;
    private IReadOnlySet<string>? _spent;
    private DateTimeOffset _spentAt;
    private IReadOnlySet<string>? _routes;
    private DateTimeOffset _routesAt;

    /// <summary>Something the player should hear about: balls taken, given back, a route spent.</summary>
    public event EventHandler<EncounterNotice>? Said;

    /// <summary>
    /// A wild battle ended in a capture, as soon as the game's own record of captures says so (§190): the card flies
    /// into the album over the game. Raised once per battle, after the battle, because that is when the game counts it.
    /// </summary>
    public event EventHandler<WildCatch>? Caught;

    /// <summary>
    /// The battle whose capture is still being watched for. Apart from <see cref="_battle"/>, which is let go as soon as
    /// the map is marked — and a battle that spends nothing is let go before the game has counted anything.
    /// </summary>
    private WildBattle? _watched;

    /// <summary>How long after a battle its capture may still be counted.</summary>
    private static readonly TimeSpan CatchWatch = TimeSpan.FromSeconds(20);

    /// <summary>Whether the last thing done to the bag was a withholding nobody was told about.</summary>
    private bool _withheldQuietly;

    private sealed class WildBattle
    {
        /// <summary>Where it happened; null until known, which may be only once it is over.</summary>
        public required FieldZone? Zone { get; set; }

        /// <summary>The last zone confirmed before it started, however old: what a zone read afterwards is checked against.</summary>
        public required FieldZone? ZoneBefore { get; init; }

        /// <summary>
        /// The zone read when settling that is still waiting to be read a second time.
        /// </summary>
        /// <remarks>
        /// A zone read while the field is reloading — which is exactly when a battle ends — is not worth what a zone
        /// read standing still is worth. Measured on 2026-09-21: the reading that spent Ruta 1's encounter, marked
        /// the map and took the player's balls was taken at 02:50:49.116, and at 02:50:49.667 the records no longer
        /// agreed on anything. It only had to hold still for one more tick to be caught.
        /// </remarks>
        public FieldZone? ZoneSeenOnce { get; set; }

        public required bool IsRoute { get; set; }
        public required bool SpentBefore { get; set; }
        public required BattleCounters Start { get; init; }

        /// <summary>
        /// The counters read the tick before the battle was seen: the baseline for the shiny counter, so a shiny is
        /// caught whenever in the battle the game counts it. It is only a fallback — the game counts it too late for
        /// the balls — and the shininess comes from the wild Pokémon itself (§118).
        /// </summary>
        public required BattleCounters Before { get; init; }

        public required DateTimeOffset StartedAt { get; init; }

        /// <summary>
        /// The news of this battle already said: 0 none, 1 an allowed capture or a duplicate to choose, 2 a shiny. Once per
        /// battle, but a shiny read after a duplicate is still said: it is the bigger news.
        /// </summary>
        public int NewsSaid { get; set; }

        public int? Species { get; set; }

        /// <summary>What it is, when it is one of the static captures allowed in its zone (§119).</summary>
        public string? AllowedStatic { get; set; }

        public bool Duplicate { get; set; }
        public bool Shiny { get; set; }

        /// <summary>The whole wild Pokémon behind the block has been read, so its shininess is known.</summary>
        public bool PokemonRead { get; set; }

        /// <summary>That Pokémon, for its card if it is caught (§190).</summary>
        public PKHeX.Core.PK7? Wild { get; set; }

        /// <summary>Its capture has been told already.</summary>
        public bool CatchTold { get; set; }
        public bool TablesSeen { get; set; }
        public bool WildFainted { get; set; }
        public bool PlayerFainted { get; set; }
        public bool SpentByThis { get; set; }

        /// <summary>Fought before the run had ever had a Poké Ball: it spends nothing and marks nothing (§149).</summary>
        public bool NoBallsYet { get; set; }

        /// <summary>
        /// The trial this battle belongs to, when it was fought in a trial zone before the trial was passed (§160):
        /// it spends nothing and marks nothing unless it ends in a capture. Null for every other battle.
        /// </summary>
        public string? Trial { get; set; }

        /// <summary>When it ended; set while the mark on the map waits for the zone or the counters.</summary>
        public DateTimeOffset? EndedAt { get; set; }
    }

    /// <returns>True when the run changed, so the screens refresh.</returns>
    public async Task<bool> TickAsync(Run run, IReadOnlyList<BattleTable> tables, IReadOnlyList<BattleFaint> faints,
        bool tablesSayBattle, CancellationToken ct, IReadOnlyCollection<int>? party = null)
    {
        _party = party ?? [];
        var now = clock.Now;

        // Lo que estuviera a medias es de la run de antes. Visto en la carpeta de prueba el 2026-09-21: Azahar se cerró
        // en mitad de un combate, se empezó de cero, y cuatro minutos después la run NUEVA terminó aquel combate y avisó
        // de «Mapa sin marcar». No marcó nada porque justo no se leían los contadores; si se hubieran leído, habría
        // marcado una ruta de la run nueva con un combate de la vieja. Y la cuenta de combates de la partida vieja
        // tampoco vale: la nueva empieza de cero, y hasta superarla no se veía empezar ningún combate.
        if (_runId != run.Id)
        {
            _runId = run.Id;
            _battle = null;
            _watched = null;
            _last = null;
            _spent = null;
            _routes = null;
        }

        var read = counters.Read();
        ReportProblem();

        // El contador de combates salvajes no baja jugando: si baja, se ha recargado la partida o se ha abierto otra, y
        // el combate a medias —si lo había— no terminó en esta.
        if (read is not null && _last is not null && read.WildBattles < _last.WildBattles)
        {
            if (_battle is not null)
            {
                logger.LogInformation("El juego ha vuelto atrás ({Antes} combates salvajes, ahora {Ahora}): se descarta el combate a medias",
                    _last.WildBattles, read.WildBattles);
            }

            _battle = null;
            _watched = null;
            _last = read;
        }

        WatchCatch(read, now);

        var started = read is not null && _last is not null && read.WildBattles > _last.WildBattles;

        if (started && _battle is not null && (_battle.EndedAt is not null || !_battle.TablesSeen))
        {
            // El contador ha vuelto a subir: el anterior se acabó. Sus contadores ya llevan dentro el combate nuevo,
            // así que cómo acabó no se puede leer sin mezclar los dos.
            GiveUp(_battle, "empezó otro combate antes de poder leer cómo acabó");
        }

        if (started && _battle is null)
        {
            await StartBattleAsync(run, read!, _last!, now, ct);
        }

        _last = read ?? _last;

        if (_battle is null && tablesSayBattle && await AllowedStaticWithoutCounterAsync(run, tables, now, ct) is { } handled)
        {
            return handled;
        }

        if (_battle is null)
        {
            return await OverworldTickAsync(run, now, ct, allowSearch: !tablesSayBattle);
        }

        if (_battle.EndedAt is not null)
        {
            var settled = await SettleAsync(run, _battle, read, now, ct);
            return await OverworldTickAsync(run, now, ct, allowSearch: !tablesSayBattle) | settled;
        }

        return await BattleTickAsync(run, read, tables, faints, tablesSayBattle, now, ct);
    }

    /// <summary>
    /// Tells <see cref="Caught"/> once the game's record of captures has gone up since the watched battle began, and
    /// stops watching a while after it ended.
    /// </summary>
    private void WatchCatch(BattleCounters? read, DateTimeOffset now)
    {
        if (_watched is not { } battle)
        {
            return;
        }

        if (read is not null && !battle.CatchTold && read.Caught > battle.Start.Caught)
        {
            battle.CatchTold = true;
            _watched = null;
            var species = battle.Species ?? battle.Wild?.Species ?? 0;
            logger.LogInformation("Captura contada: {Species}{Read}", species > 0 ? speciesNames.GetName(species) : "?",
                battle.Wild is null ? " (sin leer el Pokémon)" : string.Empty);

            try
            {
                Caught?.Invoke(this, new WildCatch(species, battle.Shiny, battle.Wild));
            }
            catch (Exception ex)
            {
                // Lo que se enseñe encima del juego no puede tumbar la detección.
                logger.LogWarning(ex, "Falló el aviso de una captura");
            }

            return;
        }

        if (battle.EndedAt is { } ended && now - ended > CatchWatch)
        {
            _watched = null;
        }
    }

    private async Task StartBattleAsync(Run run, BattleCounters start, BattleCounters before, DateTimeOffset now,
        CancellationToken ct)
    {
        var confirmed = zones.LastConfirmed;
        var zone = confirmed is { } fresh && now - fresh.At <= ZoneMaxAge ? fresh.Zone : null;
        var isRoute = zone is not null && Routes().Contains(zone.LocationId);
        var spent = zone is not null && (await SpentAsync(run, now, ct)).Contains(zone.LocationId);
        var trial = zone is not null ? await trials.PendingAsync(zone.LocationId, ct) : null;

        _battle = new WildBattle
        {
            Zone = zone,
            ZoneBefore = confirmed?.Zone,
            IsRoute = isRoute,
            SpentBefore = spent,
            Start = start,
            Before = before,
            StartedAt = now,
            Trial = trial
        };

        _watched = _battle;

        if (trial is not null)
        {
            logger.LogInformation("Combate de prueba en {Zone} ({Trial}): no gasta la ruta salvo que acabe en captura",
                zone!.LocationName, trial);
        }

        _zoneProblem = zone is null ? "No se pudo confirmar la ruta al empezar este combate." : null;
        ReportProblem();

        // Las tablas del combate, ya: el contador ha subido antes de que aparezcan, y un duplicado no puede
        // quedarse con Poké Balls mientras se espera el turno de búsqueda.
        battleTables.SearchSoon();

        logger.LogInformation("Combate salvaje en {Zone} ({Route}, {Spent}); variocolor {Before} -> {Start}",
            zone?.LocationName ?? "una zona desconocida", isRoute ? "ruta" : "no es ruta",
            spent ? "ya gastada" : "libre", before.ShinyEncountered, start.ShinyEncountered);
    }

    private async Task<bool> BattleTickAsync(Run run, BattleCounters? read, IReadOnlyList<BattleTable> tables,
        IReadOnlyList<BattleFaint> faints, bool tablesSayBattle, DateTimeOffset now, CancellationToken ct)
    {
        var battle = _battle!;
        var changed = false;

        if (tables.FirstOrDefault(table => table.HasOpponent)?.Blocks.FirstOrDefault(block => !block.IsPlayers) is { } wild)
        {
            battle.TablesSeen = true;

            // El brillo sale del propio salvaje: el récord de variocolor no sube al empezar el combate (§118). Se
            // pregunta hasta que se lee, porque el bloque puede aparecer antes que el Pokémon detrás de su puntero.
            if (!battle.PokemonRead && wild.Species > 0 && battleTables.ReadPokemon(wild) is { } pokemon)
            {
                battle.PokemonRead = true;
                battle.Wild = pokemon;

                if (pokemon.IsShiny)
                {
                    battle.Shiny = true;
                    logger.LogInformation("Variocolor: el salvaje lo es (PID {Pid:X8})", pokemon.PID);
                }
            }

            if (battle.Species is null && wild.Species > 0)
            {
                battle.Species = wild.Species;
                battle.Duplicate = await IsDuplicateAsync(run, wild.Species, ct);
                battle.AllowedStatic = battle.Zone is { } here ? allowedStatics.Allowed(here.LocationId, wild.Species) : null;
                logger.LogInformation("Salvaje: {Species}{Duplicate}{Allowed}", speciesNames.GetName(wild.Species),
                    battle.Duplicate ? " (duplicado)" : string.Empty,
                    battle.AllowedStatic is { } note ? $" (captura permitida: {note})" : string.Empty);
            }
        }

        battle.WildFainted |= faints.Any(faint => !faint.IsPlayers);
        battle.PlayerFainted |= faints.Any(faint => faint.IsPlayers);
        // Segunda señal, por si el salvaje no se deja leer. Solo segunda: medido en pleno combate contra un Wooloo
        // variocolor, el récord 127 seguía en 5, así que se cuenta más tarde y no llega a tiempo de devolver nada.
        if (!battle.Shiny && read is not null && battle.Before.ShinyEncountered >= 0
            && read.ShinyEncountered > battle.Before.ShinyEncountered)
        {
            battle.Shiny = true;
            logger.LogInformation("Variocolor: el récord 127 pasó de {Before} a {Now}",
                battle.Before.ShinyEncountered, read.ShinyEncountered);
        }

        var decision = Decide(battle, over: battle.Species is null && now - battle.StartedAt > SpeciesDeadline);
        changed |= await SpendIfDueAsync(run, battle, decision, ct);

        // Mientras se lee qué Pokémon es, sin avisar: la retirada dura un segundo y la de verdad llega detrás.
        var checking = battle.Species is null && decision.Action == BallAction.Withhold && !battle.SpentBefore && !battle.Shiny;
        changed |= await ApplyAsync(run, decision, battle.Zone, now, quiet: checking, ct, battle.Species,
            shiny: battle.Shiny, allowed: battle.AllowedStatic is not null);

        // Fin: las tablas se han ido, o nunca aparecieron y la zona vuelve a leerse.
        var ended = battle.TablesSeen
            ? !tablesSayBattle
            : now - battle.StartedAt > TimeSpan.FromSeconds(3)
              && (zones.CurrentZone(allowSearch: false) is not null || now - battle.StartedAt > UnseenBattleLimit);

        if (!ended)
        {
            return changed;
        }

        logger.LogInformation("Fin del combate salvaje en {Zone}", battle.Zone?.LocationName ?? "una zona desconocida");
        battle.EndedAt = now;
        _applied = null;

        return await SettleAsync(run, battle, read, now, ct) | changed;
    }

    /// <summary>
    /// Puts the battle that just ended on the map: finds where it was if that was not known, and marks how it ended.
    /// </summary>
    /// <remarks>
    /// Waits, a tick at a time, for what is not readable yet; after <see cref="SettleDeadline"/> it gives up and
    /// says so rather than guess.
    /// </remarks>
    private async Task<bool> SettleAsync(Run run, WildBattle battle, BattleCounters? read, DateTimeOffset now,
        CancellationToken ct)
    {
        var overdue = now - battle.EndedAt!.Value > SettleDeadline;
        var changed = false;

        if (battle.Zone is null)
        {
            var here = zones.CurrentZone(allowSearch: false);

            if (here is null)
            {
                battle.ZoneSeenOnce = null;

                if (overdue)
                {
                    GiveUp(battle, "no se pudo leer en qué zona fue");
                }

                return false;
            }

            // Dos lecturas seguidas iguales, y solo entonces. Esto gasta el encuentro de una ruta, la marca en el
            // MAPA y le quita las Poké Balls al jugador, así que una lectura que no aguanta un tic no basta (§55).
            // Cuesta como mucho un segundo dentro de los quince que SettleAsync ya espera.
            if (battle.ZoneSeenOnce != here)
            {
                battle.ZoneSeenOnce = here;

                if (!overdue)
                {
                    return false;
                }

                GiveUp(battle, $"la zona no se lee igual dos veces seguidas (la última, {here.LocationName})");
                return false;
            }

            // Un combate no te mueve de sitio, salvo perderlo: te lleva al último Centro Pokémon, y hay Centros en las
            // rutas. Con una baja propia, la zona de ahora solo vale si es la última que se leyó antes de empezar.
            if (battle.PlayerFainted && here != battle.ZoneBefore)
            {
                GiveUp(battle, $"hubo bajas y ahora estás en {here.LocationName}, que puede no ser donde fue");
                return false;
            }

            battle.Zone = here;
            battle.Trial = await trials.PendingAsync(here.LocationId, ct);
            battle.IsRoute = Routes().Contains(here.LocationId);
            battle.AllowedStatic = battle.Species is { } wildSpecies ? allowedStatics.Allowed(here.LocationId, wildSpecies) : null;
            _spent = null;
            battle.SpentBefore = (await SpentAsync(run, now, ct)).Contains(here.LocationId);

            logger.LogInformation("El combate salvaje fue en {Zone} ({Route}, {Spent}), leída al acabar",
                here.LocationName, battle.IsRoute ? "ruta" : "no es ruta", battle.SpentBefore ? "ya gastada" : "libre");

            changed |= await SpendIfDueAsync(run, battle, Decide(battle, over: true), ct);

            if ((!battle.IsRoute || battle.SpentBefore) && battle.AllowedStatic is null && !battle.Shiny
                && read is not null && read.Caught > battle.Start.Caught)
            {
                var why = battle.IsRoute ? "ya había gastado su encuentro" : "no está en el mapa";
                logger.LogWarning("Captura en {Zone}, que {Why}, sin haber sabido la zona a tiempo de retirar las Poké Balls",
                    here.LocationName, why);
                Say(ToastKind.Warning, "Captura donde no tocaba",
                    $"{here.LocationName} {why}.", battle.Species);
            }
        }

        // Un duplicado lo elige el jugador (2026-10-07): capturarlo gasta la ruta, dejarlo pasar no gasta nada y la ruta sigue
        // libre para el siguiente encuentro. La captura se mira en los contadores del juego, que la apuntan un momento
        // después de irse las tablas: se espera ese momento antes de decidir que se dejó pasar.
        if (!battle.SpentByThis && battle.Duplicate && battle.IsRoute && !battle.SpentBefore && battle.Trial is null
            && battle.Zone is { } duplicateZone)
        {
            if (read is null)
            {
                if (!overdue)
                {
                    return changed;
                }

                GiveUp(battle, "no se pudieron leer los contadores del juego");
                _battle = null;
                return changed;
            }

            if (read.Caught <= battle.Start.Caught)
            {
                if (now - battle.EndedAt!.Value < CountGrace)
                {
                    return changed;
                }

                logger.LogInformation("Duplicado en {Zone} dejado pasar: no gasta la ruta", duplicateZone.LocationName);
                _battle = null;
                return changed;
            }

            if (await balls.HasHadBallsAsync(run, ct))
            {
                await balls.SpendZoneAsync(run, duplicateZone, battle.Species ?? 0,
                    battle.Species is { } caughtId ? speciesNames.GetName(caughtId) : string.Empty,
                    $"Duplicado capturado en {duplicateZone.LocationName}: cuenta como el encuentro de la ruta.", ct);

                battle.SpentByThis = true;
                _spent = null;
                changed = true;
                logger.LogInformation("Duplicado capturado en {Zone}: gasta la ruta", duplicateZone.LocationName);
            }
        }

        if (!battle.SpentByThis)
        {
            _battle = null;
            return changed;
        }

        if (read is null)
        {
            if (overdue)
            {
                GiveUp(battle, "no se pudieron leer los contadores del juego");
            }

            return changed;
        }

        // El juego apunta la huida o la captura un momento después de que se vayan las tablas: sin nada contado, se
        // espera un poco antes de decidir que el salvaje se escapó.
        var counted = read.Caught > battle.Start.Caught || read.Fled > battle.Start.Fled || battle.WildFainted;

        if (!counted && now - battle.EndedAt!.Value < CountGrace)
        {
            return changed;
        }

        // Un combate de antes de la primera Poké Ball no marca el MAPA: la ruta sigue libre (§149).
        if (battle.NoBallsYet || !await balls.HasHadBallsAsync(run, ct))
        {
            _battle = null;
            return changed;
        }

        // Un combate de prueba no es el encuentro de la ruta (§160). Salvo que acabe en captura: entonces cuenta como
        // cualquier otra, porque si no la prueba serviría para atrapar uno de regalo.
        if (battle.Trial is { } trial)
        {
            if (read.Caught <= battle.Start.Caught)
            {
                logger.LogInformation("Combate de prueba en {Zone} ({Trial}): no gasta la ruta ni marca el MAPA",
                    battle.Zone!.LocationName, trial);
                _battle = null;
                return changed;
            }

            logger.LogInformation("Captura en un combate de prueba en {Zone}: cuenta como el encuentro de la ruta",
                battle.Zone!.LocationName);
            battle.Trial = null;
            changed |= await SpendIfDueAsync(run, battle, Decide(battle, over: true), ct);
        }

        var (outcome, how) = EncounterPolicy.Ending(battle.Start, read, battle.WildFainted);
        var species = battle.Species is { } id ? speciesNames.GetName(id) : null;

        await balls.RecordOutcomeAsync(run, battle.Zone!, outcome, species, how, ct);
        logger.LogInformation("Mapa: {Zone} marcada como {Outcome}. {How}", battle.Zone!.LocationName, outcome, how);

        _battle = null;
        return true;
    }

    /// <summary>
    /// A battle against an allowed static capture that the wild battle counter did not announce.
    /// </summary>
    /// <returns>Null when this is not one, so the ordinary tick runs; otherwise whether the run changed.</returns>
    /// <remarks>
    /// Whether a static encounter raises the game's wild battle record is not measured (§119). If it does not, the
    /// battle is never seen starting, the tick outside battle keeps the balls away, and the Tapu in its ruins could
    /// not be caught. So the battle tables are asked directly: a rival of the species allowed in the zone the player
    /// was last confirmed in gets the balls back. It opens nothing — in a trainer battle a ball cannot be thrown.
    /// The zone may be old because a long battle ages it; the player cannot move during one.
    /// </remarks>
    private async Task<bool?> AllowedStaticWithoutCounterAsync(Run run, IReadOnlyList<BattleTable> tables,
        DateTimeOffset now, CancellationToken ct)
    {
        if (zones.LastConfirmed is not { } confirmed || now - confirmed.At > TimeSpan.FromMinutes(15)
            || tables.FirstOrDefault(table => table.HasOpponent)?.Blocks.FirstOrDefault(block => !block.IsPlayers)
                is not { Species: > 0 } rival
            || allowedStatics.Allowed(confirmed.Zone.LocationId, rival.Species) is not { } note)
        {
            return null;
        }

        var decision = EncounterPolicy.Decide(new EncounterSituation(confirmed.Zone,
            Routes().Contains(confirmed.Zone.LocationId), Spent: false, InWildBattle: true, rival.Species,
            AllowedStatic: note, HasAllowedStatic: true), balls.ShinyConsumesEncounter);

        return await ApplyAsync(run, decision, confirmed.Zone, now, quiet: false, ct, rival.Species, allowed: true);
    }

    private EncounterDecision Decide(WildBattle battle, bool over) => EncounterPolicy.Decide(new EncounterSituation(
        battle.Zone, battle.IsRoute, battle.SpentBefore, InWildBattle: true, battle.Species, battle.Shiny,
        battle.Duplicate, SpeciesOverdue: over, battle.AllowedStatic,
        HasAllowedStatic: battle.Zone is { } zone && allowedStatics.HasAny(zone.LocationId), PendingTrial: battle.Trial),
        balls.ShinyConsumesEncounter);

    private async Task<bool> SpendIfDueAsync(Run run, WildBattle battle, EncounterDecision decision, CancellationToken ct)
    {
        if (!decision.SpendZone || battle.SpentByThis || battle.Zone is not { } zone)
        {
            return false;
        }

        // Un combate de prueba se decide al acabar: solo una captura gasta la ruta (§160).
        if (battle.Trial is not null)
        {
            return false;
        }

        // Sin haber tenido nunca una Poké Ball, la hierba de la historia no gasta la ruta (§149).
        if (!await balls.HasHadBallsAsync(run, ct))
        {
            battle.NoBallsYet = true;
            logger.LogInformation("Combate salvaje en {Zone} sin Poké Balls todavía: no gasta la ruta", zone.LocationName);
            return false;
        }

        await balls.SpendZoneAsync(run, zone, battle.Species ?? 0,
            battle.Species is { } id ? speciesNames.GetName(id) : string.Empty, decision.Reason, ct);

        battle.SpentByThis = true;
        _spent = null;
        // Un rato después (2026-09-27): el combate se ve al empezar y el Pokémon sale unos segundos más tarde; el aviso
        // antes de verlo en el juego destripaba el encuentro.
        var species = battle.Species;
        _ = Task.Delay(FirstEncounterDelay).ContinueWith(_ =>
            Say(ToastKind.FirstEncounter, "Primer encuentro", decision.Reason, species), TaskScheduler.Default);
        return true;
    }

    /// <summary>Drops a battle the map could not be told about, saying why when it may have been a first encounter.</summary>
    private void GiveUp(WildBattle battle, string why)
    {
        logger.LogWarning("Combate salvaje en {Zone} sin marcar en el mapa: {Why}",
            battle.Zone?.LocationName ?? "una zona desconocida", why);

        if (battle.SpentByThis)
        {
            Say(ToastKind.Warning, "Mapa sin marcar",
                $"{battle.Zone!.LocationName}: no se ha podido marcar en el mapa.", battle.Species);
        }
        else if (battle.Zone is null)
        {
            Say(ToastKind.Warning, "Mapa sin marcar",
                "No se ha podido marcar el último combate en el mapa.",
                battle.Species);
        }

        _battle = null;
    }

    private async Task<bool> OverworldTickAsync(Run run, DateTimeOffset now, CancellationToken ct,
        bool allowSearch = true)
    {
        // Se lee siempre, con la regla apagada también: es la zona confirmada lo que sitúa el próximo combate.
        var zone = zones.CurrentZone(allowSearch);
        _zoneProblem = zone is null
            ? "No se identifica la ruta. Guarda fuera de combate y espera a que desaparezca este aviso."
            : null;
        ReportProblem();

        if (!balls.IsActive)
        {
            return false;
        }

        if (zone is null)
        {
            _unknownSince ??= now;

            // Sin saber dónde está, se deja la mochila como estaba un rato; pasado ese rato, la duda es del jugador.
            if (now - _unknownSince < HoldWhileUnknown)
            {
                return false;
            }
        }
        else
        {
            _unknownSince = null;
        }

        var isRoute = zone is not null && Routes().Contains(zone.LocationId);
        var spent = zone is not null && isRoute && (await SpentAsync(run, now, ct)).Contains(zone.LocationId);

        var trial = zone is not null && isRoute ? await trials.PendingAsync(zone.LocationId, ct) : null;
        var decision = EncounterPolicy.Decide(new EncounterSituation(zone, isRoute, spent, InWildBattle: false,
            PendingTrial: trial), balls.ShinyConsumesEncounter);

        return await ApplyAsync(run, decision, zone, now, quiet: false, ct);
    }

    /// <param name="species">The wild Pokémon, when in a battle and read, so the notice can show it.</param>
    /// <param name="shiny">The balls are back because it is a shiny.</param>
    /// <param name="allowed">The balls are back because it is an allowed static capture.</param>
    private async Task<bool> ApplyAsync(Run run, EncounterDecision decision, FieldZone? zone, DateTimeOffset now,
        bool quiet, CancellationToken ct, int? species = null, bool shiny = false, bool allowed = false)
    {
        if (!balls.IsActive)
        {
            return false;
        }

        // Lo que es noticia en un combate se dice una vez, cambien o no las balls (2026-10-08). Se decía solo al pasar de
        // retirar a devolver, y si el Pokémon se leía en la primera vuelta no había retirada: las balls seguían permitidas
        // desde que se entró en la ruta y el duplicado a elegir (o un variocolor) pasaba sin aviso.
        var tellNews = decision.Action != BallAction.Withhold && !quiet && (shiny || allowed || decision.Optional);
        if (tellNews && _battle is { } current && current.NewsSaid < (shiny ? 2 : 1))
        {
            current.NewsSaid = shiny ? 2 : 1;
            SayNews(decision, species, shiny, allowed, inGame: rules.GameRulePatches);
        }

        var due = decision.Action != _applied
                  || (decision.Action == BallAction.Withhold && now - _appliedAt >= RecheckWithheld);

        if (!due)
        {
            return false;
        }

        if (rules.GameRulePatches)
        {
            return await RefuseInGameAsync(run, decision, zone, now, ct, species, shiny, allowed);
        }

        var affected = await balls.ApplyAsync(run, decision, zone, ct);
        _applied = decision.Action;
        _appliedAt = now;

        if (affected == 0)
        {
            return false;
        }

        // Devolver lo que se quitó en silencio mientras se leía el Pokémon es devolverlo en silencio también: el aviso
        // de ese combate es «Primer encuentro», y dos avisos por una misma cosa tapan el juego. Salvo que lo devuelto
        // sea la noticia, que es lo que pasa con un variocolor o una captura permitida.
        var news = shiny || allowed || decision.Optional;
        var silent = quiet || (decision.Action == BallAction.GiveBack && _withheldQuietly && !news);
        _withheldQuietly = decision.Action == BallAction.Withhold && quiet;

        if (!silent)
        {
            if (decision.Action == BallAction.Withhold)
            {
                Say(ToastKind.BallsTaken, "Poké Balls retiradas", decision.Reason, species);
            }
            else if (shiny || allowed || decision.Optional)
            {
                // En un combate ya se ha dicho arriba, una sola vez.
                if (_battle is null) SayNews(decision, species, shiny, allowed, inGame: false);
            }
            else
            {
                Say(ToastKind.BallsBack, "Poké Balls devueltas", decision.Reason, species);
            }
        }

        logger.LogInformation("Regla de primer encuentro: {Action} ({Count} tipos de ball). {Reason}",
            decision.Action, affected, decision.Reason);

        return true;
    }


    /// <summary>
    /// With the rules inside the game on (2026-10-06), the patched battle menu refuses the ball itself, with the spent
    /// zone's message and no ball spent: the bag is not touched. What was taken before the rules were on goes back.
    /// </summary>
    private async Task<bool> RefuseInGameAsync(Run run, EncounterDecision decision, FieldZone? zone, DateTimeOffset now,
        CancellationToken ct, int? species, bool shiny, bool allowed)
    {
        var refuse = decision.Action == BallAction.Withhold;
        var changed = decision.Action != _applied;
        _applied = decision.Action;
        _appliedAt = now;

        try
        {
            if (!writer.WriteRuleBallRefusal(!refuse ? (byte)0 : decision.Trial ? RuleBlock.TrialReason : RuleBlock.SpentZoneReason))
            {
                logger.LogWarning("El rechazo de balls no se ha quedado en el bloque de reglas del juego");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se ha podido escribir el rechazo de balls para el juego parcheado");
        }

        await balls.ApplyAsync(run, decision with { Action = BallAction.GiveBack }, zone, ct);

        if (!changed)
        {
            return false;
        }

        logger.LogInformation("Regla de primer encuentro en el juego: {Action}. {Reason}",
            refuse ? "balls rechazadas" : "balls permitidas", decision.Reason);

        // El juego ya dice por qué no se puede lanzar; el aviso queda para lo que es noticia, y en un combate ya se ha dicho.
        if (!refuse && _battle is null && (shiny || allowed || decision.Optional))
        {
            SayNews(decision, species, shiny, allowed, inGame: true);
        }

        return true;
    }

    /// <summary>The news of a battle: a shiny, an allowed static capture or a duplicate the player may take or leave.</summary>
    private void SayNews(EncounterDecision decision, int? species, bool shiny, bool allowed, bool inGame)
    {
        var balls = inGame ? " Puedes capturarlo." : " Tienes tus Poké Balls.";

        if (shiny)
        {
            Say(ToastKind.Shiny, "¡Es variocolor!", decision.Reason + balls, species);
        }
        else if (allowed)
        {
            Say(ToastKind.AllowedCapture, "Captura permitida", decision.Reason + balls, species);
        }
        else if (decision.Optional)
        {
            SayDuplicateLater(decision.Reason, species);
        }
    }

    /// <summary>
    /// What the team carries right now, from memory. A Pokémon the game gave away (the Totem Sticker reward, a gift) is
    /// not a capture, so it is not in the run; and until the player saves, the Pokédex of the save does not know it either
    /// (2026-09-24: a Zweilous from the stickers, then a wild Zweilous that kept its Poké Balls).
    /// </summary>
    private IReadOnlyCollection<int> _party = [];

    private async Task<bool> IsDuplicateAsync(Run run, int species, CancellationToken ct)
    {
        var owned = await OwnedAsync(run, ct);
        var line = lines.GetLineId(species);
        return owned.Any(own => lines.GetLineId(own) == line);
    }

    /// <summary>
    /// Every species below <paramref name="limit"/> of a family the run already has, the same families
    /// <see cref="IsDuplicateAsync"/> judges by: for the patched game, which rerolls them (2026-10-06).
    /// </summary>
    public async Task<IReadOnlySet<int>> DuplicateSpeciesAsync(Run run, int limit, CancellationToken ct)
    {
        var families = (await OwnedAsync(run, ct)).Select(lines.GetLineId).ToHashSet();
        return Enumerable.Range(1, limit - 1).Where(species => families.Contains(lines.GetLineId(species))).ToHashSet();
    }

    private async Task<HashSet<int>> OwnedAsync(Run run, CancellationToken ct)
    {
        var owned = new HashSet<int>();

        if (await dex.CaughtAsync(ct) is { } caught)
        {
            owned.UnionWith(caught);
        }

        owned.UnionWith(_party);

        foreach (var entry in await pokemon.GetAllAsync(run.Id, ct))
        {
            owned.Add(entry.Species);
        }

        return owned;
    }

    private async Task<IReadOnlySet<string>> SpentAsync(Run run, DateTimeOffset now, CancellationToken ct)
    {
        if (_spent is null || now - _spentAt > SpentCacheLife)
        {
            _spent = await balls.SpentZonesAsync(run.Id, ct);
            _spentAt = now;
        }

        return _spent;
    }

    /// <summary>The routes of the map screen, placed by hand by the player (<c>Data/marcadores.json</c>).</summary>
    private IReadOnlySet<string> Routes()
    {
        var now = clock.Now;

        if (_routes is null || now - _routesAt > RoutesCacheLife)
        {
            _routes = JsonZoneMarkers.Load(Path.Combine(paths.Data, "marcadores.json")).All.Keys.ToHashSet(StringComparer.Ordinal);
            _routesAt = now;
        }

        return _routes;
    }

    /// <summary>How long after the battle starts the first-encounter notice waits: the wild Pokémon shows up first.</summary>
    private static readonly TimeSpan FirstEncounterDelay = TimeSpan.FromSeconds(6);

    /// <summary>The duplicate notice, a few seconds after the battle starts: the Pokémon shows up first, and the notice names no one.</summary>
    private void SayDuplicateLater(string reason, int? species) =>
        _ = Task.Delay(FirstEncounterDelay).ContinueWith(_ => Say(ToastKind.Duplicate, "Duplicado: tú eliges", reason, species),
            TaskScheduler.Default);

    private void Say(ToastKind kind, string title, string message, int? species = null) =>
        Said?.Invoke(this, new EncounterNotice(kind, title, message, species));
}
