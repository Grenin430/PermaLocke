using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One run of the tournament, checked.</summary>
/// <param name="Verdict">What <see cref="SnapshotAudit"/> says of the last upload.</param>
/// <param name="Rewinds">Uploads where the history went backwards or was rewritten, from the server's log.</param>
public sealed record AuditRow(Guid RunId, bool Active, Guid UserId, string Player, int Points, int Events, string Verdict, string Detail, bool Ok,
    int Uploads, string Rewinds, string LastUpload, string Flags = "")
{
    public string Action => Active ? "REINICIAR" : "REACTIVAR";

    public string Run => Active ? "ACTIVA" : "ARCHIVADA";
}

/// <summary>
/// AUDITORÍA DEL TORNEO: every uploaded run, checked the same way the shared folder was (§123), plus the server's upload
/// log, which the players cannot touch.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SnapshotAudit.Check"/> verifies the hash chain and that the points, event count and head come out of it.
/// The upload log (<c>subidas</c>, readable only by an organiser, <c>tools/supabase/05-organizador.sql</c>) catches what
/// one upload alone cannot: the number of events going down (a run restored from a copy) or the chain head changing
/// without it going up (history rewritten).
/// </para>
/// <para>
/// What it is not: proof against someone who rebuilds a whole valid chain with tools. It is evidence to look at.
/// </para>
/// </remarks>
public sealed partial class AuditViewModel(DiscordLogin discord, ILogger<AuditViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ObservableCollection<AuditRow> Rows { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(ActCommand))]
    private bool _busy;

    private bool CanAct => !Busy;

    /// <summary>Also the runs archived by a reset, to compare somebody's old run with the new one.</summary>
    [ObservableProperty]
    private bool _showArchived;

    partial void OnShowArchivedChanged(bool value) => _ = RefreshAsync();

    private sealed record RunRow(Guid Run_id, Guid User_id, bool Activa, JsonElement Snapshot, JsonElement History, DateTimeOffset Subida);

    private sealed record UploadRow(Guid Run_id, int? Eventos, string? Huella, int? Puntos, DateTimeOffset Llegada);

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task RefreshAsync()
    {
        Busy = true;
        Status = "Leyendo el servidor...";

        try
        {
            var runsJson = await discord.GetAsync(ShowArchived
                ? "runs?select=run_id,user_id,activa,snapshot,history,subida&order=subida.desc"
                : "runs?select=run_id,user_id,activa,snapshot,history,subida&activa=eq.true");
            var uploadsJson = await discord.GetAsync("subidas?select=run_id,eventos,huella,puntos,llegada&order=llegada");

            if (runsJson is null || uploadsJson is null)
            {
                Status = "Entra con Discord en la ventana principal (la cuenta del organizador).";
                return;
            }

            var uploads = (JsonSerializer.Deserialize<List<UploadRow>>(uploadsJson, Json) ?? []).ToLookup(u => u.Run_id);
            var rows = new List<AuditRow>();

            foreach (var run in JsonSerializer.Deserialize<List<RunRow>>(runsJson, Json) ?? [])
            {
                var snapshot = run.Snapshot.Deserialize<RunSnapshot>(Json)!;
                var history = await Services.ServerHistory.CompleteAsync(discord, run.History.Deserialize<RunHistory>(Json), snapshot, Json);
                var result = SnapshotAudit.Check(snapshot, history);
                var log = uploads[run.Run_id].ToList();

                rows.Add(new AuditRow(run.Run_id, run.Activa, run.User_id, snapshot.PlayerName, snapshot.Points, snapshot.EventCount,
                    Say(result.Verdict), result.Detail, result.Verdict == AuditVerdict.Consistent,
                    log.Count, Rewinds(log), run.Subida.LocalDateTime.ToString("dd/MM HH:mm"), Flags(history)));
            }

            Rows.Clear();
            foreach (var row in rows.OrderByDescending(r => r.Active).ThenBy(r => r.Ok).ThenByDescending(r => r.Points)) Rows.Add(row);

            Status = rows.Count == 0
                ? "Todavía no ha subido nadie su run."
                : $"{rows.Count} runs · {rows.Count(r => !r.Ok || r.Rewinds.Length > 0 || r.Flags.Length > 0)} con algo que mirar.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo auditar el torneo");
            Status = "No se ha podido leer el servidor. ¿Está tu ID en la tabla organizadores?";
        }
        finally
        {
            Busy = false;
        }
    }


    /// <summary>
    /// Archives a player's run so they can start again from zero. Nothing is deleted: the run stays on the server for
    /// the audit, and the reset is logged. Only an organiser can do it (<c>reiniciar_run</c>, 07-una-run.sql).
    /// </summary>
    private async Task ResetAsync(AuditRow? row)
    {
        if (row is null || System.Windows.MessageBox.Show(
                $"¿Reiniciar la run de {row.Player}?\n\nSu run actual ({row.Points} puntos, {row.Events} eventos) deja de contar " +
                "en el torneo y podrá crear una nueva desde cero. La run no se borra: se queda archivada para auditar.",
                "Reiniciar run", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.CallAsync("reiniciar_run", JsonSerializer.Serialize(new { jugador = row.UserId }));
            logger.LogInformation("Run de {Player} reiniciada por el organizador", row.Player);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo reiniciar la run de {Player}", row.Player);
            Status = "No se ha podido reiniciar. ¿Está tu ID en la tabla organizadores?";
            return;
        }

        await RefreshAsync();
        Status = $"Run de {row.Player} reiniciada. Ya puede crear una nueva.";
    }

    /// <summary>The row's button: resets an active run, or brings an archived one back.</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task ActAsync(AuditRow? row) => row is { Active: false } ? ReactivateAsync(row) : ResetAsync(row);

    /// <summary>
    /// Undoes a reset: the archived run becomes the player's active run again. Refused by the server if they have
    /// already started another one; reset that first (<c>reactivar_run</c>, 10-control.sql).
    /// </summary>
    private async Task ReactivateAsync(AuditRow row)
    {
        if (System.Windows.MessageBox.Show(
                $"¿Reactivar esta run de {row.Player} ({row.Points} puntos, {row.Events} eventos)?\n\n" +
                "Vuelve a contar en el torneo. Si ya tiene otra run activa, reiníciala antes.",
                "Reactivar run", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.CallAsync("reactivar_run", JsonSerializer.Serialize(new { run = row.RunId }));
            logger.LogInformation("Run {Run} de {Player} reactivada", row.RunId, row.Player);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo reactivar la run {Run}", row.RunId);
            Status = $"No se ha podido reactivar. Si {row.Player} ya tiene otra run activa, reiníciala antes.";
            return;
        }

        await RefreshAsync();
        Status = $"Run de {row.Player} reactivada.";
    }
    /// <summary>Uploads where the run went backwards (fewer events) or was rewritten (same count, other head).</summary>
    private static string Rewinds(List<UploadRow> log) =>
        string.Join(" · ", SnapshotAudit.Rewinds([.. log.Select(u => new SeenMark(u.Eventos ?? 0, u.Huella ?? ""))])
            .Select(i => log[i].Eventos < log[i - 1].Eventos
                ? $"{log[i].Llegada.LocalDateTime:dd/MM HH:mm}: de {log[i - 1].Eventos} a {log[i].Eventos} eventos"
                : $"{log[i].Llegada.LocalDateTime:dd/MM HH:mm}: historial reescrito"));

    /// <summary>
    /// The reload tricks PermaLocke saw in this run (2026-09-26): save states, playing or restoring without PermaLocke,
    /// closing the emulator mid-battle. Only the organiser sees them; the player's screens leave them out.
    /// </summary>
    private static string Flags(RunHistory? history) =>
        string.Join("\n", (history?.Events ?? [])
            .Where(e => e.Type == GameEventType.IntegrityFlag)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => $"{e.Timestamp.LocalDateTime:dd/MM HH:mm}: {e.Description}"));

    private static string Say(AuditVerdict verdict) => verdict switch
    {
        AuditVerdict.Consistent => "CUADRA",
        AuditVerdict.NoHistory => "SIN HISTORIAL",
        AuditVerdict.ChainBroken => "CADENA ROTA",
        AuditVerdict.DoesNotMatch => "NO CUADRA",
        AuditVerdict.Rewound => "RETROCEDIDA",
        _ => verdict.ToString()
    };
}
