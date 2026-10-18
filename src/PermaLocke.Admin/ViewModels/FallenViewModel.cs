using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One grave of the tournament.</summary>
public sealed record FallenLine(string Player, string Pokemon, string FellTo, string How, bool Killcam, DateTimeOffset? Died)
{
    public string When => Died?.ToLocalTime().ToString("dd/MM HH:mm") ?? "-";

    public string HasKillcam => Killcam ? "Sí" : "No";
}

/// <summary>
/// CEMENTERIO (2026-09-28): every player's graves in one list, from the table the apps share them to
/// (<c>19-caidos.sql</c>). Read only: deaths are changed from each player's sheet (revive, mark fallen).
/// </summary>
public sealed partial class FallenViewModel(DiscordLogin discord, ILogger<FallenViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Grave(string? Name, string? SpeciesName, DateTimeOffset? DiedAt, string? FellTo, string? HowItWasSeen);

    private sealed record Row(string Nombre, bool Killcam, Grave Datos);

    private List<FallenLine> _all = [];

    public ObservableCollection<FallenLine> Graves { get; } = [];

    public ObservableCollection<string> Players { get; } = [];

    /// <summary>Whose graves to show; empty for everybody's.</summary>
    [ObservableProperty]
    private string _player = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    partial void OnPlayerChanged(string value) => Show();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.GetAsync("caidos?select=nombre,killcam,datos&order=creado.desc&limit=1000") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            _all = [.. (JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [])
                .Select(r => new FallenLine(r.Nombre,
                    string.IsNullOrWhiteSpace(r.Datos.Name) || r.Datos.Name == r.Datos.SpeciesName
                        ? r.Datos.SpeciesName ?? "?" : $"{r.Datos.Name} ({r.Datos.SpeciesName})",
                    r.Datos.FellTo ?? "-", r.Datos.HowItWasSeen ?? "-", r.Killcam, r.Datos.DiedAt))];

            Players.Clear();
            Players.Add(string.Empty);
            foreach (var name in _all.Select(g => g.Player).Distinct().Order()) Players.Add(name);
            Show();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer el cementerio");
            Status = "No se ha podido. ¿Has ejecutado 19-caidos.sql?";
        }
    }

    private void Show()
    {
        Graves.Clear();
        foreach (var grave in _all.Where(g => Player.Length == 0 || g.Player == Player)) Graves.Add(grave);
        Status = $"{Graves.Count} caídos{(Player.Length == 0 ? " en todo el torneo" : $" de {Player}")}.";
    }
}
