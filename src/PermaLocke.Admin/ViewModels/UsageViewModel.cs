using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One table of the tournament and what it takes.</summary>
public sealed record UsageTable(string Nombre, long Filas, long Bytes)
{
    public string Size => UsageViewModel.Format(Bytes);
}

/// <summary>One player's run on the server.</summary>
public sealed record UsageRun(string? Jugador, bool Activa, int Eventos, long Bytes)
{
    public string Size => UsageViewModel.Format(Bytes);

    public string Label => $"{Jugador ?? "?"}{(Activa ? string.Empty : " (archivada)")}";
}

/// <summary>
/// CONSUMO: how much of the free plan's 500 MB the tournament uses, table by table and run by run (2026-09-24).
/// </summary>
/// <remarks>
/// Read through <c>consumo()</c> (11-consumo.sql), which only an organiser may call. The month's downloads (5 GB) are
/// not here on purpose: Supabase only gives them to its dashboard or to an account-wide key, and that key does not
/// belong in an application.
/// </remarks>
public sealed partial class UsageViewModel(DiscordLogin discord, ILogger<UsageViewModel> logger) : ObservableObject
{
    /// <summary>The free plan's database limit.</summary>
    public const long Limit = 500L * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Reply(long BaseDeDatos, List<UsageTable> Tablas, List<UsageRun> Runs);

    public ObservableCollection<UsageTable> Tables { get; } = [];

    public ObservableCollection<UsageRun> Runs { get; } = [];

    [ObservableProperty]
    private string _total = "—";

    /// <summary>0 to 1, for the bar.</summary>
    [ObservableProperty]
    private double _share;

    [ObservableProperty]
    private string _status = string.Empty;

    public static string Format(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.CallAsync("consumo") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            var reply = JsonSerializer.Deserialize<Reply>(json, Json)!;
            Tables.Clear();
            foreach (var table in reply.Tablas) Tables.Add(table);
            Runs.Clear();
            foreach (var run in reply.Runs) Runs.Add(run);

            Share = Math.Min(1, (double)reply.BaseDeDatos / Limit);
            Total = $"{Format(reply.BaseDeDatos)} de 500 MB ({Share:P0})";
            Status = "Lo descargado en el mes (5 GB) solo lo da el panel de Supabase: Project Settings > Usage.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer el consumo");
            Status = "No se ha podido leer. ¿Has ejecutado 11-consumo.sql?";
        }
    }
}
