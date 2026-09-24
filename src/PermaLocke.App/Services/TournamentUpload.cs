using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
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
/// When: once at start, then whenever an event has been stored, at most once a minute. Nothing is sent while the run
/// has not changed. A failed upload is logged and tried again on the next change; playing never waits for it.
/// </para>
/// </remarks>
public sealed class TournamentUpload(
    SyncService sync,
    DiscordLogin discord,
    RunActivity activity,
    ILogger<TournamentUpload> logger)
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

    // Los tipos de evento como texto ("AchievementUnlocked"), para que el servidor los lea sin saber el orden del enum.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private volatile bool _dirty = true;
    private string _sentHead = string.Empty;

    public void Start()
    {
        activity.Appended += (_, _) => _dirty = true;
        _ = LoopAsync();
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
}
