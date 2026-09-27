using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.App.Services;

/// <summary>
/// The ghosts (§183): this player's deaths go to the tournament server at once, and the other players' come over
/// this player's emulator as a notice and a ghost crossing the screen. A team wipe makes it rain blood over the
/// emulator of everyone, the one who wiped included (§184).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ghosts also without the emulator</b> (1.0.5.4): with only PermaLocke open, or in the background, over the screen.
/// The blood rain stays <b>only inside the emulator</b>, as the player asked: while Azahar is closed it is not shown, and the pointer
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

    /// <summary>The same for the wipes, which come from their own table (<c>tools/supabase/13-lluvias.sql</c>).</summary>
    private long _rainCursor = -1;
    private bool _reading;
    private bool _playing;
    private GhostWindow? _window;
    private string? _lastProblem;
    private string? _lastRainProblem;

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

    /// <summary>
    /// This player's team fell: it rains here at once, and the news goes to the others. Fire and forget, like
    /// <see cref="Send"/>: the wipe is already in the run.
    /// </summary>
    public void Rain(string player)
    {
        var name = _discord.Saved?.Name ?? player;

        if (_writes && _discord.Saved is not null)
        {
            _ = SendRainAsync(name);
        }

        // El vigilante avisa desde su hilo; la cola y la ventana son del de la pantalla.
        _ = _timer.Dispatcher.InvokeAsync(() =>
        {
            if (!Enabled)
            {
                return;
            }

            // Aquí sin esperar al servidor: quien ha caído lo ve ya, y sin aviso, que el suyo ya sale.
            _waiting.Enqueue(new GhostRow(0, Guid.Empty, name, string.Empty, 0, 0, false, null, null, Rain: true, Mine: true));

            if (!_playing)
            {
                _ = PlayAllAsync();
            }
        });
    }

    private async Task SendRainAsync(string name)
    {
        try
        {
            await _discord.PostAsync("lluvias", JsonSerializer.Serialize(new { nombre = name }));
            _logger.LogInformation("Lluvia de sangre enviada");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar la lluvia de sangre");
        }
    }

    private sealed record GhostRow(long Id, Guid Jugador, string Nombre, string Pokemon, int Especie, int Forma,
        bool Shiny, int? Nivel, string? Zona, bool Rain = false, bool Mine = false);

    private sealed record RainRow(long Id, Guid Jugador, string Nombre);

    private async Task ReadAsync()
    {
        if (_reading)
        {
            return;
        }

        // Los fantasmas salen también con solo la app abierta o en segundo plano (1.0.5.4); la lluvia, solo dentro del
        // emulador. Lo que se apaga olvida el puntero: al volver no se repite lo de antes.
        if (!Enabled || _discord.Saved is null)
        {
            _cursor = -1;
            _rainCursor = -1;
            return;
        }

        _reading = true;

        try
        {
            await ReadGhostsAsync();

            // Aparte: si la tabla de las lluvias todavía no existe en el servidor, los fantasmas siguen llegando.
            if (_launcher.IsRunning) await ReadRainAsync();
            else _rainCursor = -1;
        }
        finally
        {
            _reading = false;
        }

        if (!_playing && _waiting.Count > 0)
        {
            await PlayAllAsync();
        }
    }

    private async Task ReadGhostsAsync()
    {
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
    }

    /// <summary>The others' wipes, after their ghosts: the rain is the end of the deaths that made it.</summary>
    private async Task ReadRainAsync()
    {
        try
        {
            if (_rainCursor < 0)
            {
                var latest = await _discord.GetAsync("lluvias?select=id,jugador,nombre&order=id.desc&limit=1");
                _rainCursor = latest is null ? -1
                    : (JsonSerializer.Deserialize<List<RainRow>>(latest, Json) ?? []).FirstOrDefault()?.Id ?? 0;
                return;
            }

            var json = await _discord.GetAsync($"lluvias?select=id,jugador,nombre&id=gt.{_rainCursor}&order=id.asc&limit=5");

            if (json is null)
            {
                return;
            }

            var me = _discord.Saved?.UserId ?? Guid.Empty;

            foreach (var row in JsonSerializer.Deserialize<List<RainRow>>(json, Json) ?? [])
            {
                _rainCursor = Math.Max(_rainCursor, row.Id);

                // La propia ya llovió al momento, sin esperar al servidor.
                if (row.Jugador != me)
                {
                    _waiting.Enqueue(new GhostRow(row.Id, row.Jugador, row.Nombre, string.Empty, 0, 0, false, null, null,
                        Rain: true));
                }
            }

            _lastRainProblem = null;
        }
        catch (Exception ex)
        {
            if (ex.Message != _lastRainProblem)
            {
                _lastRainProblem = ex.Message;
                _logger.LogWarning(ex, "No se pudieron leer las lluvias de sangre del servidor");
            }
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

    /// <summary>
    /// The blood rain of a friend's wipe without the server and without the game, for <c>--ensayar-lluvia</c> (§184).
    /// Nothing is sent or recorded.
    /// </summary>
    public Task RehearseRainAsync(string player)
    {
        _waiting.Enqueue(new GhostRow(0, Guid.Empty, player, string.Empty, 0, 0, false, null, null, Rain: true));
        return _playing ? Task.CompletedTask : PlayAllAsync(rehearsal: true);
    }

    private async Task PlayAllAsync(bool rehearsal = false)
    {
        _playing = true;

        try
        {
            await _sprites.PrepareAsync();

            while (_waiting.Count > 0 && (rehearsal || Enabled))
            {
                var row = _waiting.Dequeue();
                _window ??= new GhostWindow();

                if (row.Rain)
                {
                    if (!rehearsal && !_launcher.IsRunning) continue;

                    _logger.LogInformation("Lluvia de sangre: {Player} ha perdido el equipo", row.Nombre);
                    await _window.RainAsync(row.Mine ? null : new Toast(ToastKind.TeamWipe,
                        $"{row.Nombre} ha perdido el equipo entero", "Llueve sangre.", null, DateTime.UtcNow, Notifier.Linger));
                    continue;
                }

                var sprite = _sprites.Get(row.Especie, row.Forma, row.Shiny);
                var where = string.Join(" · ", new[] { row.Nivel is { } level ? $"Nv. {level}" : null, row.Zona }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

                var notice = new Toast(ToastKind.Ghost, $"{row.Nombre} ha perdido a {row.Pokemon}", where, sprite,
                    DateTime.UtcNow, Notifier.Linger);

                _logger.LogInformation("Fantasma de {Pokemon}, de {Player}", row.Pokemon, row.Nombre);
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
