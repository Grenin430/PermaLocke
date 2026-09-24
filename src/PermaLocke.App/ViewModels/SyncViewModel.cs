using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One line of the tournament's top.</summary>
/// <param name="Picture">The Discord photo (a URL), or the lead Pokémon when there is none.</param>
public sealed record TopRow(int Position, string Player, int Points, bool IsMine, object? Picture);

/// <summary>
/// COMPETICIÓN: the tournament's top by points, read from the server's <c>clasificacion</c> view.
/// </summary>
/// <remarks>
/// Rebuilt on 2026-09-24 at the player's request: for now only the points and a top. The shared folder, the audit chip
/// and the link-battle notes are gone from this screen; checking runs is the organiser's job, in Admin. The points are
/// those of each player's last uploaded run (<see cref="TournamentUpload"/>), and only accounts on the whitelist can
/// read them.
/// </remarks>
public sealed partial class SyncViewModel : SectionViewModel
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly DiscordLogin _discord;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<SyncViewModel> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };

    public SyncViewModel(DiscordLogin discord, PokemonSpriteService sprites, ILogger<SyncViewModel> logger)
        : base("COMPETICIÓN")
    {
        _discord = discord;
        _sprites = sprites;
        _logger = logger;
        _timer.Tick += (_, _) => _ = RefreshAsync();
    }

    public override string IconKey => "IconPeople";

    public override GameNeed Needs => GameNeed.Either;

    /// <summary>The top three, in the order a podium is drawn: second, first, third.</summary>
    public ObservableCollection<TopRow> Podium { get; } = [];

    /// <summary>Fourth place and below.</summary>
    public ObservableCollection<TopRow> Rest { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();
        _timer.Start();
        await RefreshAsync();
    }

    private sealed record Row(string? Jugador, int? Puntos, int? Avatar, bool Es_mio, string? Avatar_url);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await _discord.GetAsync("clasificacion?select=jugador,puntos,avatar,es_mio,avatar_url") is not { } json)
            {
                Status = "Entra con Discord para ver la clasificación.";
                return;
            }

            var rows = JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [];
            var top = new List<TopRow>();

            // Empates, mismo puesto: 1, 2, 2, 4.
            for (var i = 0; i < rows.Count; i++)
            {
                var points = rows[i].Puntos ?? 0;
                var position = i > 0 && points == top[i - 1].Points ? top[i - 1].Position : i + 1;
                top.Add(new TopRow(position, rows[i].Jugador ?? "Jugador", points, rows[i].Es_mio,
                    (object?)rows[i].Avatar_url ?? (rows[i].Avatar is { } species ? _sprites.Get(species) : null)));
            }

            Podium.Clear();
            foreach (var index in new[] { 1, 0, 2 }.Where(i => i < top.Count)) Podium.Add(top[index]);

            Rest.Clear();
            foreach (var row in top.Skip(3)) Rest.Add(row);

            Status = top.Count == 0 ? "Todavía no ha subido nadie su run." : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la clasificación del torneo");
            Status = "Sin conexión con el servidor del torneo.";
        }
    }
}
