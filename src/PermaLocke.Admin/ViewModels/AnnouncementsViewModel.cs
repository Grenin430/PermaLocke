using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One announcement to every player.</summary>
public sealed record Announcement(long Id, string Texto, DateTimeOffset Creado)
{
    public string When => Creado.LocalDateTime.ToString("dd/MM HH:mm");
}

/// <summary>
/// ANUNCIOS: a message for every player of the tournament. The newest one shows in JUGAR, and each player gets a
/// notice the first time they see it. Only an organiser can publish or withdraw (<c>10-control.sql</c>).
/// </summary>
public sealed partial class AnnouncementsViewModel(DiscordLogin discord, ILogger<AnnouncementsViewModel> logger)
    : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ObservableCollection<Announcement> Published { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    private bool CanPublish => Text.Trim().Length > 0;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.GetAsync("anuncios?select=id,texto,creado&order=creado.desc") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            Published.Clear();
            foreach (var item in JsonSerializer.Deserialize<List<Announcement>>(json, Json) ?? []) Published.Add(item);
            Status = Published.Count == 0 ? "No hay anuncios." : "El más reciente es el que ven los jugadores.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los anuncios");
            Status = "No se han podido leer. ¿Has ejecutado 10-control.sql?";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        try
        {
            await discord.PostAsync("anuncios", JsonSerializer.Serialize(new { texto = Text.Trim() }));
            logger.LogInformation("Anuncio publicado: {Text}", Text.Trim());
            Text = string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo publicar el anuncio");
            Status = "No se ha podido publicar.";
            return;
        }

        await RefreshAsync();
        Status = "Publicado. Lo verán en JUGAR en como mucho un minuto.";
    }

    [RelayCommand]
    private async Task WithdrawAsync(Announcement? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await discord.DeleteAsync($"anuncios?id=eq.{item.Id}");
            logger.LogInformation("Anuncio {Id} retirado", item.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo retirar el anuncio {Id}", item.Id);
            Status = "No se ha podido retirar.";
            return;
        }

        await RefreshAsync();
    }
}
