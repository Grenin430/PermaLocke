using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// Sends the loaded run to the tournament server: the same summary and history the shared folder used to carry.
/// </summary>
/// <remarks>
/// <para>
/// Built by <see cref="SyncService.BuildAsync"/>, so what reaches the server is exactly what
/// <c>SnapshotAudit</c> already knows how to check: the points must come out of the chain, and the chain must verify.
/// Who owns the row is decided by the server (<c>auth.uid()</c>), never by what the application says, and only an
/// account on the whitelist can write (<c>tools/supabase/02-runs.sql</c>).
/// </para>
/// <para>
/// Only what is new (§194): with <c>tools/supabase/15-eventos-y-limpieza.sql</c> on the server, the run goes through
/// <c>subir_eventos</c> — the summary every time, and only the events the server does not have yet, which it keeps one
/// row each and never rewrites. A server without that function (a 404) gets the whole history as before. If the server
/// holds more than this PC, or the chain does not follow on from what it holds (a restored copy, a rewritten history),
/// nothing is appended or overwritten: only the summary keeps going, and the audit sees the difference.
/// </para>
/// <para>
/// When: once at start, then whenever an event has been stored, at most every two minutes, and on closing. Nothing is sent while the run
/// has not changed. A failed upload is logged and tried again on the next change; playing never waits for it.
/// </para>
/// </remarks>
public sealed class TournamentUpload(
    SyncService sync,
    DiscordLogin discord,
    RunActivity activity,
    ILogger<TournamentUpload> logger)
{
    // Cada 2 minutos como mucho (antes 5; cambiado el 2026-09-24 a petición del organizador), y al cerrar: cada subida reescribe el historial entero en el servidor.
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(2);

    // Los tipos de evento como texto ("AchievementUnlocked"), para que el servidor los lea sin saber el orden del enum.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>Events sent in one call at most, so a first upload of a long run is several small requests.</summary>
    private const int Chunk = 400;

    private volatile bool _dirty = true;
    private string _sentHead = string.Empty;
    private bool _started;

    /// <summary>The server has no <c>subir_eventos</c>: whole history, as before.</summary>
    private bool _whole;

    /// <summary>The run the server's count is about, and how many of its events the server holds.</summary>
    private Guid _serverRun;
    private int? _serverCount;

    /// <summary>What the server holds is not the start of this PC's chain: only the summary is sent.</summary>
    private bool _diverged;

    public void Start()
    {
        _started = true;
        activity.Appended += (_, _) => _dirty = true;
        _ = LoopAsync();
    }

    /// <summary>On closing: sends what changed since the last upload, waiting a few seconds at most.</summary>
    public void Flush()
    {
        if (!_started || !_dirty)
        {
            return;
        }

        try
        {
            Task.Run(UploadAsync).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo subir la run al cerrar");
        }
    }

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(Every);

        do
        {
            if (!_dirty) continue;
            _dirty = false;

            try
            {
                await UploadAsync();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // El servidor la rechaza (una run archivada por el organizador): repetirlo cada dos minutos no cambia
                // nada, así que espera de verdad al próximo cambio (log del 2026-09-28: 79 avisos seguidos).
                logger.LogWarning("El servidor no acepta la run ({Message}); se reintenta con el próximo cambio", ex.Message);
            }
            catch (Exception ex)
            {
                _dirty = true;
                logger.LogWarning(ex, "No se pudo subir la run al torneo; se reintenta con el próximo cambio");
            }
        }
        while (await timer.WaitForNextTickAsync());
    }

    private async Task UploadAsync()
    {
        var player = await sync.PlayerAsync();

        // Una run que es de otro jugador (una carpeta Saves copiada) no se sube como tuya.
        if (player.Ownership == RunOwnership.Foreign || player.Profile is not { } profile
            || await sync.BuildAsync(profile) is not { } built || built.Snapshot.ChainHead == _sentHead)
        {
            return;
        }

        if (!_whole)
        {
            try
            {
                await UploadNewAsync(built.Snapshot, built.History.Events);
                _sentHead = built.Snapshot.ChainHead;
                return;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _whole = true;
                logger.LogInformation("El servidor no tiene subir_eventos (falta el SQL 15): se sube la run entera");
            }
        }

        var body = new JsonObject
        {
            ["run_id"] = built.Snapshot.RunId,
            ["snapshot"] = JsonSerializer.SerializeToNode(built.Snapshot, Options),
            ["history"] = JsonSerializer.SerializeToNode(built.History, Options)
        };

        if (await discord.PostAsync("runs?on_conflict=run_id", body.ToJsonString(), "resolution=merge-duplicates"))
        {
            _sentHead = built.Snapshot.ChainHead;
            logger.LogInformation("Run subida al torneo: {Events} eventos, {Points} puntos",
                built.Snapshot.EventCount, built.Snapshot.Points);
        }
    }

    /// <summary>The summary, and the events after the ones the server already holds, a chunk at a time.</summary>
    private async Task UploadNewAsync(RunSnapshot snapshot, IReadOnlyList<GameEvent> events)
    {
        if (_serverRun != snapshot.RunId)
        {
            _serverRun = snapshot.RunId;
            _serverCount = null;
            _diverged = false;
        }

        var summary = JsonSerializer.SerializeToNode(snapshot, Options);

        // Sin saber por dónde va el servidor, o si no sigue nuestra cadena: solo el resumen, y la respuesta dice cuántos tiene.
        if (_serverCount is null || _diverged)
        {
            _serverCount = await SendAsync(summary, -1, []);
        }

        while (!_diverged && _serverCount < events.Count)
        {
            var from = _serverCount.Value;
            var now = await SendAsync(summary, from, events.Skip(from).Take(Chunk).ToList());

            if (now == from)
            {
                // No los ha añadido: lo que tiene no acaba donde empieza lo nuestro.
                _diverged = true;
                logger.LogWarning("El servidor tiene otra cadena para esta run ({Server} eventos): solo se sube el resumen", from);
                break;
            }

            _serverCount = now;
        }

        if (_serverCount > events.Count && !_diverged)
        {
            _diverged = true;
            logger.LogWarning("El servidor tiene más eventos ({Server}) que este PC ({Local}): solo se sube el resumen",
                _serverCount, events.Count);
        }

        logger.LogInformation("Run subida al torneo: {Events} eventos ({Server} en el servidor), {Points} puntos",
            snapshot.EventCount, _serverCount, snapshot.Points);
    }

    private async Task<int> SendAsync(JsonNode? summary, int from, IReadOnlyList<GameEvent> events)
    {
        var body = new JsonObject
        {
            ["p_run"] = _serverRun,
            ["p_snapshot"] = summary?.DeepClone(),
            ["p_desde"] = from,
            ["p_eventos"] = JsonSerializer.SerializeToNode(events, Options)
        };

        var answer = await discord.CallAsync("subir_eventos", body.ToJsonString())
                     ?? throw new InvalidOperationException("Sin sesión de Discord.");
        return int.Parse(answer.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
