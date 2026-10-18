using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One voted nickname, with what was proposed and voted.</summary>
public sealed record MoteLine(long Id, string Player, string Pokemon, string State, string Ballots, DateTimeOffset Created)
{
    public string When => Created.ToLocalTime().ToString("dd/MM HH:mm");
}

/// <summary>
/// MOTES (2026-09-28): every voted nickname, newest first, with its proposals and votes. The organiser can cancel one,
/// which deletes it: an open vote vanishes from every screen and a closed one not applied yet never gets applied.
/// </summary>
public sealed partial class MotesViewModel(DiscordLogin discord, ILogger<MotesViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Row(long Id, string Nombre, string Pokemon, DateTimeOffset Creado, DateTimeOffset Cierra, bool Aplicado, string? Ganador);

    private sealed record Ballot(long Mote, string Nombre, string? Propuesta, string? Voto, DateTimeOffset Creado);

    public ObservableCollection<MoteLine> Motes { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await discord.GetAsync("motes?select=id,nombre,pokemon,creado,cierra,aplicado,ganador&order=id.desc&limit=100") is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return;
            }

            var rows = JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [];
            var ballots = rows.Count == 0 ? [] : JsonSerializer.Deserialize<List<Ballot>>(
                await discord.GetAsync($"votos_mote?select=mote,nombre,propuesta,voto,creado&mote=in.({string.Join(",", rows.Select(r => r.Id))})") ?? "[]",
                Json) ?? [];

            Motes.Clear();
            foreach (var row in rows)
            {
                var mine = ballots.Where(b => b.Mote == row.Id).ToList();
                var state = row.Aplicado ? row.Ganador is null ? "Sin propuestas" : $"Puesto: {row.Ganador}"
                    : row.Cierra > DateTimeOffset.Now ? "Votando ahora"
                    : NicknameVote.Winner(mine.Select(b => new NicknameBallot(b.Nombre, b.Propuesta, b.Voto, b.Creado))) is { } winner
                        ? $"Gana {winner}; se pone cuando cierre el juego" : "Sin propuestas";
                var detail = string.Join(" · ", mine.Select(b => $"{b.Nombre}: {b.Propuesta ?? "-"}{(b.Voto is null ? "" : $" (vota {b.Voto})")}"));
                Motes.Add(new MoteLine(row.Id, row.Nombre, row.Pokemon, state, detail, row.Creado));
            }

            Status = Motes.Count == 0 ? "Nadie ha pedido todavía que le voten un mote." : $"{Motes.Count} votaciones.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los motes");
            Status = "No se ha podido. ¿Has ejecutado 20-motes.sql?";
        }
    }

    [RelayCommand]
    private async Task CancelAsync(MoteLine? line)
    {
        if (line is null || System.Windows.MessageBox.Show(
                $"¿Anular la votación del {line.Pokemon} de {line.Player}? Si no se ha puesto aún, ya no se pondrá.", "Anular",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.DeleteAsync($"motes?id=eq.{line.Id}");
            Motes.Remove(line);
            Status = "Anulada.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo anular la votación {Id}", line.Id);
            Status = "No se ha podido anular. Vuelve a ejecutar 20-motes.sql (tiene el permiso del organizador).";
        }
    }
}
