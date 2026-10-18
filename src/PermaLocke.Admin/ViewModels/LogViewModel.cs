using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>
/// REGISTRO (2026-10-08): everything the organisers changed, from <c>admin_registro</c> (21-admin.sql). The server writes it,
/// with who from the session, so nothing in Admin can leave it out or change it.
/// </summary>
/// <remarks>
/// Rules changes keep the file as it was, and can be put back from here: the restore is one more change, logged too.
/// </remarks>
public sealed partial class LogViewModel(GiftDesk desk, ILogger<LogViewModel> logger) : ObservableObject
{
    private IReadOnlyList<LogLine> _all = [];

    public ObservableCollection<LogLine> Lines { get; } = [];

    public ObservableCollection<string> Kinds { get; } = ["TODO"];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _kind = "TODO";

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>False until 21-admin.sql is on the server: the page says so instead of an empty table.</summary>
    [ObservableProperty]
    private bool _available = true;

    partial void OnSearchChanged(string value) => Filter();

    partial void OnKindChanged(string value) => Filter();

    /// <summary>The newest lines, for INICIO.</summary>
    public ObservableCollection<LogLine> Latest { get; } = [];

    /// <summary>
    /// INICIO's eight lines, read on every refresh. The whole log (up to 1000 lines, with the rules' previous contents) is
    /// only read when REGISTRO opens, to keep the minute's refresh small on the free plan's transfer.
    /// </summary>
    public async Task LoadLatestAsync()
    {
        try
        {
            var lines = await desk.LogAsync(limit: 8);
            Latest.Clear();
            foreach (var line in lines) Latest.Add(line);
            Available = true;
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Available = false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron leer los últimos cambios");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;

        try
        {
            _all = await desk.LogAsync(limit: 1000);
            Available = true;
            var kinds = _all.Select(l => KindOf(l.Action)).Distinct().Order().ToList();
            var kept = Kind;
            Kinds.Clear();
            Kinds.Add("TODO");
            foreach (var kind in kinds) Kinds.Add(kind);
            Kind = Kinds.Contains(kept) ? kept : "TODO";
            Filter();
            Status = _all.Count == 0 ? "Aún no hay nada: se apunta desde que se ejecutó 21-admin.sql." : $"{_all.Count} cambio(s), los más nuevos arriba.";
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Available = false;
            Status = "El servidor aún no tiene el registro: ejecuta tools/supabase/21-admin.sql en Supabase.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer el registro");
            Status = $"No se ha podido leer el registro: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>The family of an action, for the filter: «Orden», «Lista», «Reglas»...</summary>
    public static string KindOf(string action)
    {
        var code = action.StartsWith("retirar:", StringComparison.Ordinal) ? action["retirar:".Length..] : action;
        var head = code.Split(':')[0];
        return head switch
        {
            "orden" => "Órdenes",
            "regalo" or "ajuste" => "Regalos y ajustes",
            "lista" => "Lista",
            "anuncio" => "Anuncios",
            "reglas" => "Reglas",
            "mote" or "buzon" => "Comunidad",
            "run" => "Runs",
            "nota" => "Notas",
            "servidor" => "Servidor",
            _ => head
        };
    }

    private void Filter()
    {
        Lines.Clear();
        foreach (var line in _all.Where(l =>
                     (Kind == "TODO" || KindOf(l.Action) == Kind)
                     && (string.IsNullOrWhiteSpace(Search) || Matches(l, Search.Trim()))))
        {
            Lines.Add(line);
        }
    }

    private static bool Matches(LogLine line, string text) =>
        new[] { line.What, line.Summary, line.ByText, line.Reason, line.Target }.Any(field =>
            field is not null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(field, text,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>Puts a rules file back as it was before that change.</summary>
    [RelayCommand]
    private async Task RevertAsync(LogLine? line)
    {
        if (line is not { CanRevert: true } || line.Target is null) return;

        var before = line.Detail.GetProperty("antes").GetString() ?? string.Empty;
        if (Confirm.Ask(new ConfirmRequest("Volver a la versión anterior", line.Target,
            [
                $"Se publica {line.Target} como estaba antes del cambio del {line.WhenText} ({before.Length:N0} letras).",
                "Todas las apps lo bajan al abrirse, como cualquier cambio de REGLAS.",
                "La versión de ahora queda guardada en este REGISTRO por si quieres volver."
            ], "RESTAURAR", NeedsReason: false, Danger: true)) is null)
        {
            return;
        }

        try
        {
            JsonDocument.Parse(before).Dispose();
        }
        catch (JsonException)
        {
            Status = "Lo que había antes no es un JSON válido: no se restaura.";
            return;
        }

        try
        {
            await desk.RestoreRulesAsync(line.Target, before);
            Status = $"{line.Target} restaurado.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo restaurar {File}", line.Target);
            Status = $"No se ha podido restaurar: {ex.Message}";
        }
    }
}
