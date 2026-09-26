using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One crash report on the server, with whose it is.</summary>
public sealed record ReportLine(string Player, StoredFile File)
{
    public string When => File.When;

    public string Size => File.Size;

    public string Name => File.FileName;
}

/// <summary>
/// INFORMES (§199): the crash reports every player's application sent when Azahar fell over, newest first, to download
/// and open without asking anybody for a file.
/// </summary>
/// <remarks>
/// Listed by <c>informes_lista()</c> (17-informes.sql), which only an organiser may call; downloaded through Storage.
/// Read-only: the LIMPIEZA removes the ones older than 30 days.
/// </remarks>
public sealed partial class ReportsViewModel(GiftDesk desk, DiscordLogin discord, ILogger<ReportsViewModel> logger) : ObservableObject
{
    /// <summary>The bucket of the reports (17-informes.sql); the same name the player's CrashReportUpload sends to.</summary>
    public const string CrashReportBucket = "informes";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Row(string Nombre, Guid Jugador, long Bytes, DateTimeOffset Creado);

    public ObservableCollection<ReportLine> Reports { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.CallAsync("informes_lista", "{}") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            var names = (await desk.PlayersAsync()).ToDictionary(player => player.Id, player => player.Name);
            Reports.Clear();
            foreach (var row in JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [])
            {
                Reports.Add(new ReportLine(names.GetValueOrDefault(row.Jugador, row.Jugador.ToString()[..8]),
                    new StoredFile(CrashReportBucket, row.Nombre, row.Creado, row.Bytes)));
            }

            Status = Reports.Count == 0
                ? "Ningún informe: nadie ha tenido un cierre de Azahar (o sus apps aún no los mandan)."
                : $"{Reports.Count} informes. Cada uno lleva los dos logs, las últimas peticiones al emulador y su equipo.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los informes");
            Status = "No se ha podido. ¿Has ejecutado 17-informes.sql?";
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(ReportLine? report)
    {
        if (report is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{report.Player} {report.Name}",
            Filter = "Zip|*.zip",
            Title = "Guardar el informe"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (await desk.DownloadAsync(report.File) is not { } bytes)
            {
                Status = "Entra con Discord.";
                return;
            }

            await System.IO.File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status = $"Guardado en {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo descargar {File}", report.File.Path);
            Status = "No se ha podido descargar.";
        }
    }
}
