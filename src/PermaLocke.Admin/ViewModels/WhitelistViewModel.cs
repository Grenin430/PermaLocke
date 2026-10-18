using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One Discord account allowed into the tournament.</summary>
public sealed record Allowed(string Discord_id, string? Nombre, DateTimeOffset Alta, bool Suspendido = false)
{
    public string Toggle => Suspendido ? "REACTIVAR" : "SUSPENDER";

    public string State => Suspendido ? "SUSPENDIDO" : string.Empty;

    public string Name => string.IsNullOrWhiteSpace(Nombre) ? "(sin nombre)" : Nombre;

    public string Since => Alta.LocalDateTime.ToString("dd/MM/yyyy");
}

/// <summary>
/// LISTA DEL TORNEO: who may open PermaLocke. Adding or removing here is the same as doing it in Supabase's panel,
/// and only an organiser can (<c>tools/supabase/09-whitelist-admin.sql</c>).
/// </summary>
/// <remarks>
/// Removing somebody locks them out the next time they open the application, and the server stops accepting what
/// their copy sends. Their run is not touched: to start them again from zero, use REINICIAR in the audit.
/// </remarks>
public sealed partial class WhitelistViewModel(DiscordLogin discord, ILogger<WhitelistViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ObservableCollection<Allowed> People { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newId = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>A Discord ID is a number of 17 to 20 digits; anything else is a typo that would let nobody in.</summary>
    internal static bool IsDiscordId(string text) =>
        text.Trim().Length is >= 17 and <= 20 && text.Trim().All(char.IsAsciiDigit);

    private bool CanAdd => IsDiscordId(NewId);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.GetAsync("whitelist?select=discord_id,nombre,alta,suspendido&order=alta.desc") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            People.Clear();
            foreach (var person in JsonSerializer.Deserialize<List<Allowed>>(json, Json) ?? []) People.Add(person);
            Status = $"{People.Count} en la lista.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer la whitelist");
            Status = "No se ha podido leer la lista. ¿Has ejecutado 09-whitelist-admin.sql?";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var id = NewId.Trim();

        if (People.Any(p => p.Discord_id == id))
        {
            Status = "Ese ID ya está en la lista.";
            return;
        }

        try
        {
            await discord.PostAsync("whitelist",
                JsonSerializer.Serialize(new { discord_id = id, nombre = NewName.Trim() }));
            logger.LogInformation("Añadido a la whitelist: {Id} ({Name})", id, NewName.Trim());
            Status = $"{(NewName.Trim().Length > 0 ? NewName.Trim() : id)} ya puede entrar.";
            NewId = string.Empty;
            NewName = string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo añadir {Id} a la whitelist", id);
            Status = "No se ha podido añadir.";
            return;
        }

        await RefreshAsync();
    }

    /// <summary>
    /// Suspends somebody without taking them off the list, or lets them back in. A suspended player cannot open
    /// PermaLocke or upload anything, and keeps everything they had (<c>10-control.sql</c>).
    /// </summary>
    [RelayCommand]
    private async Task ToggleAsync(Allowed? person)
    {
        if (person is null)
        {
            return;
        }

        try
        {
            await discord.PatchAsync($"whitelist?discord_id=eq.{Uri.EscapeDataString(person.Discord_id)}",
                JsonSerializer.Serialize(new { suspendido = !person.Suspendido }));
            logger.LogInformation("{Id} ({Name}) {What}", person.Discord_id, person.Name,
                person.Suspendido ? "reactivado" : "suspendido");
            Status = person.Suspendido ? $"{person.Name} puede volver a entrar." : $"{person.Name} queda suspendido.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo cambiar la suspensión de {Id}", person.Discord_id);
            Status = "No se ha podido cambiar. ¿Has ejecutado 10-control.sql?";
            return;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveAsync(Allowed? person)
    {
        if (person is null || System.Windows.MessageBox.Show(
                $"¿Quitar a {person.Name} de la lista del torneo?\n\nNo podrá volver a abrir PermaLocke ni subir nada. " +
                "Su run no se toca.",
                "Quitar de la lista", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.DeleteAsync($"whitelist?discord_id=eq.{Uri.EscapeDataString(person.Discord_id)}");
            logger.LogInformation("Quitado de la whitelist: {Id} ({Name})", person.Discord_id, person.Name);
            Status = $"{person.Name} ya no puede entrar.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo quitar {Id} de la whitelist", person.Discord_id);
            Status = "No se ha podido quitar.";
            return;
        }

        await RefreshAsync();
    }
}
