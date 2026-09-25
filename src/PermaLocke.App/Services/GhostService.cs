using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.Services;

/// <summary>
/// The ghosts (§183): this player's deaths go to the tournament server at once, and the other players' come over
/// this player's emulator as a notice and a ghost crossing the screen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only inside the emulator</b>, as the player asked: while Azahar is closed nothing is shown, and the pointer
/// forgets where it was, so opening the game later does not replay the deaths of the whole evening.
/// </para>
/// <para>
/// <b>One after another</b>: several deaths arrive as a queue, and each ghost waits for the previous one to be gone.
/// </para>
/// <para>
/// This decides and records nothing. The death is already in the dying player's run; the server table is only the
/// news of it (<c>tools/supabase/12-fantasmas.sql</c>). A failure here costs the ghost and nothing else.
/// </para>
/// </remarks>
public sealed class GhostService
{
    /// <summary>How often the others' deaths are asked for while the game is open: a ghost should feel live.</summary>
    private static readonly TimeSpan ReadEvery = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly DiscordLogin _discord;
    private readonly EmulatorLauncher _launcher;
    private readonly IZoneProvider _zones;
    private readonly PokemonSpriteService _sprites;
    private readonly DeathCeremony _ceremony;
    private readonly ILogger<GhostService> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = ReadEvery };
    private readonly Queue<GhostRow> _waiting = new();

    /// <summary>Last ghost seen, by server id; negative until the first read sets it to «now».</summary>
    private long _cursor = -1;
    private bool _reading;
    private bool _playing;
    private GhostWindow? _window;
    private string? _lastProblem;

    public GhostService(DiscordLogin discord, EmulatorLauncher launcher, IZoneProvider zones,
        PokemonSpriteService sprites, DeathCeremony ceremony, ILogger<GhostService> logger)
    {
        _ceremony = ceremony;
        _discord = discord;
        _launcher = launcher;
        _zones = zones;
        _sprites = sprites;
        _logger = logger;
        _timer.Tick += (_, _) => _ = ReadAsync();
    }

    /// <summary>
    /// Off in CONFIGURACIÓN, one switch with the ghost of the death scene (<see cref="DeathCeremony.Ghosts"/>): the
    /// others' ghosts are not shown. This player's deaths are still sent; the others decide what they see.
    /// </summary>
    private bool Enabled => _ceremony.Ghosts;

    private bool _writes;

    /// <param name="writes">False for the read-only copy (<c>--sin-juego</c>): it never sends a death.</param>
    public void Start(bool writes)
    {
        _writes = writes;
        _timer.Start();
    }

    /// <summary>Sends this player's death to the others. Fire and forget: the run has it already.</summary>
    public void Send(DeathNotice notice, string player)
    {
        if (!_writes || _discord.Saved is null)
        {
            return;
        }

        _ = SendAsync(notice, player);
    }

    private async Task SendAsync(DeathNotice notice, string player)
    {
        try
        {
            // La zona ya confirmada, sin pedir otra: buscarla ahora podría barrer memoria en mitad del combate.
            var zone = _zones.LastConfirmed?.Zone.LocationName;

            await _discord.PostAsync("fantasmas", JsonSerializer.Serialize(new
            {
                nombre = _discord.Saved?.Name ?? player,
                pokemon = notice.Name,
                especie = notice.Species,
                forma = notice.Form,
                shiny = notice.Shiny,
                nivel = notice.Level > 0 ? notice.Level : (int?)null,
                zona = zone
            }));

            _logger.LogInformation("Fantasma enviado: {Pokemon}", notice.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el fantasma de {Pokemon}", notice.Name);
        }
    }

    private sealed record GhostRow(long Id, Guid Jugador, string Nombre, string Pokemon, int Especie, int Forma,
        bool Shiny, int? Nivel, string? Zona);

    private async Task ReadAsync()
    {
        if (_reading)
        {
            return;
        }

        // Solo dentro del emulador. Con el juego cerrado se olvida el puntero: al abrir no se repite lo de antes.
        if (!_launcher.IsRunning || !Enabled || _discord.Saved is null)
        {
            _cursor = -1;
            return;
        }

        _reading = true;

        try
        {
            if (_cursor < 0)
            {
                var latest = await _discord.GetAsync("fantasmas?select=id&order=id.desc&limit=1");
                _cursor = latest is null ? -1
                    : (JsonSerializer.Deserialize<List<GhostRow>>(latest, Json) ?? []).FirstOrDefault()?.Id ?? 0;
                return;
            }

            var json = await _discord.GetAsync(
                $"fantasmas?select=id,jugador,nombre,pokemon,especie,forma,shiny,nivel,zona&id=gt.{_cursor}&order=id.asc&limit=20");

            if (json is null)
            {
                return;
            }

            var me = _discord.Saved?.UserId ?? Guid.Empty;

            foreach (var row in JsonSerializer.Deserialize<List<GhostRow>>(json, Json) ?? [])
            {
                _cursor = Math.Max(_cursor, row.Id);

                if (row.Jugador != me)
                {
                    _waiting.Enqueue(row);
                }
            }

            _lastProblem = null;

            if (!_playing && _waiting.Count > 0)
            {
                await PlayAllAsync();
            }
        }
        catch (Exception ex)
        {
            // Se pregunta cada 10 s: el mismo fallo se apunta una vez, no cada vez (§167).
            if (ex.Message != _lastProblem)
            {
                _lastProblem = ex.Message;
                _logger.LogWarning(ex, "No se pudieron leer los fantasmas del servidor");
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// Shows one friend's ghost without the server and without the game, for <c>--ensayar-fantasma</c>: a death that
    /// did happen in this run, shown the way the others would see it. Nothing is sent or recorded.
    /// </summary>
    public Task RehearseAsync(string player, DeathNotice fallen)
    {
        _waiting.Enqueue(new GhostRow(0, Guid.Empty, player, fallen.Name, fallen.Species, fallen.Form, fallen.Shiny,
            fallen.Level > 0 ? fallen.Level : null, "Ruta 1"));
        return _playing ? Task.CompletedTask : PlayAllAsync(rehearsal: true);
    }

    private async Task PlayAllAsync(bool rehearsal = false)
    {
        _playing = true;

        try
        {
            await _sprites.PrepareAsync();

            while (_waiting.Count > 0 && (rehearsal || (_launcher.IsRunning && Enabled)))
            {
                var row = _waiting.Dequeue();
                var sprite = _sprites.Get(row.Especie, row.Forma, row.Shiny);
                var where = string.Join(" · ", new[] { row.Nivel is { } level ? $"Nv. {level}" : null, row.Zona }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

                var notice = new Toast(ToastKind.Ghost, $"{row.Nombre} ha perdido a {row.Pokemon}", where, sprite,
                    DateTime.UtcNow, Notifier.Linger);

                _logger.LogInformation("Fantasma de {Pokemon}, de {Player}", row.Pokemon, row.Nombre);
                _window ??= new GhostWindow();
                await _window.PlayAsync(notice, GhostArt.Make(sprite));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la animación de un fantasma");
            _waiting.Clear();
        }
        finally
        {
            _window?.Done();
            _playing = false;
        }
    }
}
