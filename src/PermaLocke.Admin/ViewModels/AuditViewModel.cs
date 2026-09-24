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
public sealed record AuditRow(string Player, int Points, int Events, string Verdict, string Detail, bool Ok,
    int Uploads, string Rewinds, string LastUpload);

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
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(SignInCommand))]
    private bool _busy;

    private bool CanAct => !Busy;

    private sealed record RunRow(Guid Run_id, JsonElement Snapshot, JsonElement History, DateTimeOffset Subida);

    private sealed record UploadRow(Guid Run_id, int? Eventos, string? Huella, int? Puntos, DateTimeOffset Llegada);

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task SignInAsync()
    {
        Busy = true;
        Status = "Termina de entrar en el navegador...";

        try
        {
            var account = await discord.SignInAsync();
            Status = $"Dentro como {account.Name}.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el inicio de sesión con Discord");
            Status = "No se ha podido entrar con Discord.";
        }
        finally
        {
            Busy = false;
        }

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task RefreshAsync()
    {
        Busy = true;
        Status = "Leyendo el servidor...";

        try
        {
            var runsJson = await discord.GetAsync("runs?select=run_id,snapshot,history,subida");
            var uploadsJson = await discord.GetAsync("subidas?select=run_id,eventos,huella,puntos,llegada&order=llegada");

            if (runsJson is null || uploadsJson is null)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            var uploads = (JsonSerializer.Deserialize<List<UploadRow>>(uploadsJson, Json) ?? []).ToLookup(u => u.Run_id);
            var rows = new List<AuditRow>();

            foreach (var run in JsonSerializer.Deserialize<List<RunRow>>(runsJson, Json) ?? [])
            {
                var snapshot = run.Snapshot.Deserialize<RunSnapshot>(Json)!;
                var history = run.History.Deserialize<RunHistory>(Json);
                var result = SnapshotAudit.Check(snapshot, history);
                var log = uploads[run.Run_id].ToList();

                rows.Add(new AuditRow(snapshot.PlayerName, snapshot.Points, snapshot.EventCount,
                    Say(result.Verdict), result.Detail, result.Verdict == AuditVerdict.Consistent,
                    log.Count, Rewinds(log), run.Subida.LocalDateTime.ToString("dd/MM HH:mm")));
            }

            Rows.Clear();
            foreach (var row in rows.OrderBy(r => r.Ok).ThenByDescending(r => r.Points)) Rows.Add(row);

            Status = rows.Count == 0
                ? "Todavía no ha subido nadie su run."
                : $"{rows.Count} runs · {rows.Count(r => !r.Ok || r.Rewinds.Length > 0)} con algo que mirar.";
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

    /// <summary>Uploads where the run went backwards (fewer events) or was rewritten (same count, other head).</summary>
    private static string Rewinds(List<UploadRow> log) =>
        string.Join(" · ", SnapshotAudit.Rewinds([.. log.Select(u => new SeenMark(u.Eventos ?? 0, u.Huella ?? ""))])
            .Select(i => log[i].Eventos < log[i - 1].Eventos
                ? $"{log[i].Llegada.LocalDateTime:dd/MM HH:mm}: de {log[i - 1].Eventos} a {log[i].Eventos} eventos"
                : $"{log[i].Llegada.LocalDateTime:dd/MM HH:mm}: historial reescrito"));

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
