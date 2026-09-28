using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One suggestion from a player's suggestion box.</summary>
public sealed record SuggestionLine(long Id, string Player, string Text, string Version, DateTimeOffset Created)
{
    public string When => Created.ToLocalTime().ToString("dd/MM HH:mm");
}

/// <summary>
/// SUGERENCIAS (1.0.5.5): what the players wrote in the suggestion box of their app, newest first. Only the organiser
/// can read or delete them (18-sugerencias.sql).
/// </summary>
public sealed partial class SuggestionsViewModel(DiscordLogin discord, ILogger<SuggestionsViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Row(long Id, string Nombre, string Texto, string? Version, DateTimeOffset Creado);

    public ObservableCollection<SuggestionLine> Suggestions { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.GetAsync("sugerencias?select=id,nombre,texto,version,creado&order=id.desc&limit=300") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            Suggestions.Clear();
            foreach (var row in JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [])
            {
                Suggestions.Add(new SuggestionLine(row.Id, row.Nombre, row.Texto, row.Version ?? "", row.Creado));
            }

            Status = Suggestions.Count == 0 ? "El buzón está vacío." : $"{Suggestions.Count} sugerencias.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer las sugerencias");
            Status = "No se ha podido. ¿Has ejecutado 18-sugerencias.sql?";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(SuggestionLine? line)
    {
        if (line is null)
        {
            return;
        }

        if (System.Windows.MessageBox.Show("¿Borrar esta sugerencia? No se puede recuperar.", "Borrar",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.DeleteAsync($"sugerencias?id=eq.{line.Id}");
            Suggestions.Remove(line);
            Status = "Borrada.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo borrar la sugerencia {Id}", line.Id);
            Status = "No se ha podido borrar.";
        }
    }
}
