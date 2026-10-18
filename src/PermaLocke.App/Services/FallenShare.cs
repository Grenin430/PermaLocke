using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// The cemeteries of everybody (1.0.5.5): this player's graves and killcams go to the tournament server, and the
/// friends' ones come back for the CEMENTERIO (<c>tools/supabase/19-caidos.sql</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every minute it compares the run's graves with what it already sent (<c>Config/caidos-compartidos.json</c>), so the
/// deaths from before this version go up too, with their killcams: the local files are still there. A grave is sent as
/// soon as it exists; its killcam, as a small video (<see cref="KillcamVideo"/>), when the recorder has written it.
/// </para>
/// <para>
/// A friend's killcam is downloaded when its grave is picked, once, into <c>Saves/killcam-amigos/</c>. Nothing here
/// changes the run: the cemetery is already in it, the server only shows it to the others.
/// </para>
/// </remarks>
public sealed class FallenShare
{
    public const string Bucket = "killcams";
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly CemeteryService _cemetery;
    private readonly DiscordLogin _discord;
    private readonly IRunContext _runs;
    private readonly AppPaths _paths;
    private readonly ILogger<FallenShare> _logger;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly DispatcherTimer _timer = new() { Interval = Every };
    private string? _lastProblem;

    public FallenShare(CemeteryService cemetery, DiscordLogin discord, IRunContext runs, AppPaths paths, ILogger<FallenShare> logger)
    {
        _cemetery = cemetery;
        _discord = discord;
        _runs = runs;
        _paths = paths;
        _logger = logger;
        _timer.Tick += (_, _) => _ = ShareAsync();
    }

    private string SentPath => Path.Combine(_paths.Config, "caidos-compartidos.json");

    /// <summary>Starts sending this player's graves. Not called for the read-only copy (<c>--sin-juego</c>).</summary>
    public void Start()
    {
        _timer.Start();
        _ = ShareAsync();
    }

    private Dictionary<Guid, bool> ReadSent()
    {
        try
        {
            return File.Exists(SentPath)
                ? JsonSerializer.Deserialize<Dictionary<Guid, bool>>(File.ReadAllText(SentPath)) ?? []
                : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private async Task ShareAsync()
    {
        if (!await _one.WaitAsync(0)) return;

        try
        {
            if (_discord.Saved is not { UserId: var user } account || user == Guid.Empty || _runs.Current is not { } run)
            {
                return;
            }

            // Valor: si su killcam ya está subida.
            var sent = ReadSent();

            foreach (var grave in await _cemetery.GravesAsync())
            {
                var hasKillcam = grave.KillcamPath is not null;
                if (sent.TryGetValue(grave.PokemonId, out var withKillcam) && (withKillcam || !hasKillcam))
                {
                    continue;
                }

                var killcamSent = false;
                if (hasKillcam && await Task.Run(() => KillcamVideo.Encode(grave.KillcamPath!)) is { } video)
                {
                    try
                    {
                        killcamSent = await _discord.UploadAsync(Bucket, $"{user}/{grave.PokemonId:N}.plkv", video, "application/octet-stream");
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest)
                    {
                        // Storage no reemplaza: si una vuelta anterior la subió sin llegar a apuntarlo, ya está. Se mira,
                        // porque el mismo error sale si falta el bucket (sin el SQL 19).
                        var listed = await _discord.ListAsync(Bucket, user.ToString());
                        killcamSent = listed?.Contains($"{grave.PokemonId:N}.plkv", StringComparison.Ordinal) == true;
                        if (!killcamSent) throw;
                    }

                    _logger.LogInformation("Killcam de {Pokemon} compartida: {Bytes} bytes", grave.Name, video.Length);
                }

                var row = JsonSerializer.Serialize(new
                {
                    pokemon_id = grave.PokemonId,
                    nombre = account.Name,
                    run_id = run.Id,
                    datos = grave with { KillcamPath = null },
                    killcam = killcamSent
                });

                if (!await _discord.PostAsync("caidos?on_conflict=pokemon_id", row, "resolution=merge-duplicates"))
                {
                    return;
                }

                sent[grave.PokemonId] = killcamSent;
                Directory.CreateDirectory(_paths.Config);
                await File.WriteAllTextAsync(SentPath, JsonSerializer.Serialize(sent));
            }

            _lastProblem = null;
        }
        catch (Exception ex)
        {
            // Cada minuto: el mismo fallo se apunta una vez (§167).
            if (ex.Message != _lastProblem)
            {
                _lastProblem = ex.Message;
                _logger.LogWarning(ex, "No se pudo compartir el cementerio; se reintenta");
            }
        }
        finally
        {
            _one.Release();
        }
    }

    private sealed record Row(Guid Pokemon_Id, Guid Jugador, string Nombre, Guid Run_Id, Grave Datos, bool Killcam, DateTimeOffset Creado);

    /// <summary>A friend's cemetery: the graves of their latest run, with whether each has a killcam on the server.</summary>
    public async Task<IReadOnlyList<(Grave Grave, bool Killcam)>> FriendGravesAsync(Guid player)
    {
        var json = await _discord.GetAsync($"caidos?select=*&jugador=eq.{player}&order=creado.desc&limit=500");
        var rows = json is null ? [] : JsonSerializer.Deserialize<List<Row>>(json, Json) ?? [];
        if (rows.Count == 0) return [];

        // Solo la run de ahora: la de la muerte más reciente.
        var current = rows[0].Run_Id;
        return [.. rows.Where(row => row.Run_Id == current)
            .Select(row => (row.Datos with { KillcamPath = Cached(row.Pokemon_Id) }, row.Killcam))
            .OrderBy(pair => pair.Item1.DiedAt ?? DateTimeOffset.MaxValue)];
    }

    private string CachePath(Guid pokemon) => Path.Combine(_paths.Saves, "killcam-amigos", $"{pokemon:N}.killcam");

    private string? Cached(Guid pokemon) => File.Exists(CachePath(pokemon)) ? CachePath(pokemon) : null;

    /// <summary>A friend's killcam as a local file, downloading it the first time; null when it cannot be had.</summary>
    public async Task<string?> FriendKillcamAsync(Guid player, Guid pokemon)
    {
        if (Cached(pokemon) is { } cached) return cached;

        try
        {
            if (await _discord.DownloadAsync(Bucket, $"{player}/{pokemon:N}.plkv") is not { } video) return null;
            var path = CachePath(pokemon);
            return await Task.Run(() => KillcamVideo.Decode(video, path)) ? path : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo bajar la killcam {Pokemon} de {Player}", pokemon, player);
            return null;
        }
    }
}
