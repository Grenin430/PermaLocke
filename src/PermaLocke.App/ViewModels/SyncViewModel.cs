using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.ViewModels;

/// <summary>One line of the tournament's top.</summary>
/// <param name="Picture">The Discord photo (a URL), or the lead Pokémon when there is none.</param>
/// <param name="State">Whether they are connected now, for the dot next to the name.</param>
public sealed record TopRow(int Position, string Player, int Points, bool IsMine, object? Picture,
    PresenceState State, int Caught, int Alive, int Dead, int Stages, int Achievements, int AchievementsTotal)
{
    public string Line => $"ETAPA {Stages} · {Achievements}/{AchievementsTotal} LOGROS";

    public string Team => $"{Alive} VIVOS · {Dead} CAÍDOS";
}

/// <summary>A record of the tournament: who holds it and with how much.</summary>
public sealed record RecordCard(string Title, string Player, string Value, object? Picture, string Icon);

/// <summary>
/// COMPETICIÓN: the tournament's top by points and its records, read from the server's <c>clasificacion</c> view.
/// </summary>
/// <remarks>
/// Rebuilt on 2026-09-24 at the player's request, and filled out the same day with things that are in every uploaded
/// run anyway: how far each one is, who is alive and fallen, their achievements, whether they are connected, and the
/// records (most captures, most fallen, most achievements, furthest). Nothing here is invented: each number is a field of
/// the last run each player uploaded (<see cref="TournamentUpload"/>), and only accounts on the whitelist can read them.
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

    public ObservableCollection<RecordCard> Records { get; } = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();
        _timer.Start();
        await RefreshAsync();
    }

    private sealed record Row(string? Jugador, int? Puntos, int? Avatar, bool Es_mio, string? Avatar_url,
        int? Capturados, int? Vivos, int? Caidos, int? Etapas, int? Logros, int? Logros_total, int? Estado);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (await _discord.GetAsync("clasificacion?select=*") is not { } json)
            {
                Status = "Entra con Discord para ver la clasificación.";
                return;
            }

            var rows = JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [];
            var top = new List<TopRow>();

            // Empates, mismo puesto: 1, 2, 2, 4.
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var points = r.Puntos ?? 0;
                var position = i > 0 && points == top[i - 1].Points ? top[i - 1].Position : i + 1;
                top.Add(new TopRow(position, r.Jugador ?? "Jugador", points, r.Es_mio,
                    (object?)r.Avatar_url ?? (r.Avatar is { } species ? _sprites.Get(species) : null),
                    (PresenceState)(r.Estado ?? 0), r.Capturados ?? 0, r.Vivos ?? 0, r.Caidos ?? 0, r.Etapas ?? 0,
                    r.Logros ?? 0, r.Logros_total ?? 0));
            }

            Podium.Clear();
            foreach (var index in new[] { 1, 0, 2 }.Where(i => i < top.Count)) Podium.Add(top[index]);

            Rest.Clear();
            foreach (var row in top.Skip(3)) Rest.Add(row);

            Records.Clear();
            if (top.Count > 0)
            {
                Records.Add(Best("MÁS CAPTURAS", top, r => r.Caught, v => $"{v} Pokémon", "IconGacha"));
                Records.Add(Best("MÁS CAÍDOS", top, r => r.Dead, v => $"{v} en el cementerio", "IconGrave"));
                Records.Add(Best("MÁS LOGROS", top, r => r.Achievements, v => $"{v} logros", "IconTrophy"));
                Records.Add(Best("MÁS AVANZADO", top, r => r.Stages, v => $"etapa {v}", "IconChart"));
            }

            var online = top.Count(r => r.State != PresenceState.Offline);
            var playing = top.Count(r => r.State == PresenceState.Playing);
            Summary = top.Count == 0
                ? string.Empty
                : $"{top.Count} EN EL TORNEO · {online} CONECTADOS · {playing} JUGANDO · {top.Sum(r => r.Dead)} CAÍDOS EN TOTAL";

            Status = top.Count == 0 ? "Todavía no ha subido nadie su run." : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la clasificación del torneo");
            Status = "Sin conexión con el servidor del torneo.";
        }
    }

    /// <summary>Who holds a record; on a tie, the one higher in the top.</summary>
    private static RecordCard Best(string title, List<TopRow> top, Func<TopRow, int> value, Func<int, string> say,
        string icon)
    {
        var best = top.MaxBy(value)!;
        return value(best) == 0
            ? new RecordCard(title, "Nadie todavía", "-", null, icon)
            : new RecordCard(title, best.Player, say(value(best)), best.Picture, icon);
    }
}
