using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// «Jugando a PermaLocke» in the player's Discord profile, with the app's icon, for as long as PermaLocke is open (§185).
/// </summary>
/// <remarks>
/// <para>
/// Only that, as the player asked: no route, no points, no team. Discord writes «Jugando a» and the name of the Discord
/// application whose id is <c>discordApp</c> in <c>Data/torneo.json</c>; the name and the icon are set in the Discord
/// Developer Portal, not here. Without the id this does nothing and says so once in the log.
/// </para>
/// <para>
/// Talks to the Discord app running on this PC through its local pipe (<c>discord-ipc-0</c> to <c>9</c>): nothing goes
/// through the tournament server and no library is shipped for it. The activity lasts while the pipe is open, so
/// closing PermaLocke clears it. If Discord is closed or restarted, it is tried again every 20 s.
/// </para>
/// <para>
/// The emulator's own presence («Jugando a Azahar») is switched off in its settings when the game is launched
/// (<c>AzaharInstallation.DisableDiscordPresence</c>), so PermaLocke's is the one in front.
/// </para>
/// </remarks>
public sealed class DiscordPresence(AppPaths paths, ILogger<DiscordPresence> logger)
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(20);

    private const int Handshake = 0;
    private const int Frame = 1;
    private const int Close = 2;
    private const int Ping = 3;
    private const int Pong = 4;

    private CancellationTokenSource _stopping = new();
    private string? _lastProblem;

    private sealed record PresenceConfig(string? DiscordApp, string? DiscordImagen);

    public void Start()
    {
        // Otra vez al volver del segundo plano (1.0.5.4), con un token nuevo si el anterior ya se canceló.
        if (_stopping.IsCancellationRequested) _stopping = new CancellationTokenSource();
        _ = RunAsync(_stopping.Token);
    }

    /// <summary>Closes the pipe, and with it the activity.</summary>
    public void Stop() => _stopping.Cancel();

    private async Task RunAsync(CancellationToken stop)
    {
        var config = Read();

        if (config is null || string.IsNullOrWhiteSpace(config.DiscordApp))
        {
            logger.LogInformation("Sin estado de Discord: falta discordApp en Data/torneo.json");
            return;
        }

        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = await ConnectAsync(stop);

                if (pipe is not null)
                {
                    await ShowAsync(pipe, config, stop);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Se reintenta cada 20 s: el mismo fallo se apunta una vez (§167).
                if (ex.Message != _lastProblem)
                {
                    _lastProblem = ex.Message;
                    logger.LogWarning(ex, "El estado de Discord falló; se reintenta");
                }
            }

            try
            {
                await Task.Delay(RetryEvery, stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private PresenceConfig? Read()
    {
        try
        {
            return JsonSerializer.Deserialize<PresenceConfig>(File.ReadAllText(Path.Combine(paths.Data, "torneo.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer Data/torneo.json para el estado de Discord");
            return null;
        }
    }

    /// <summary>The first Discord pipe that answers, or null when Discord is not running.</summary>
    private static async Task<NamedPipeClientStream?> ConnectAsync(CancellationToken stop)
    {
        for (var i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(200, stop);
                return pipe;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                await pipe.DisposeAsync();
            }
        }

        return null;
    }

    /// <summary>Says who we are, sets the activity and keeps the pipe open until Discord or PermaLocke closes it.</summary>
    private async Task ShowAsync(Stream pipe, PresenceConfig config, CancellationToken stop)
    {
        await WriteAsync(pipe, Handshake, new { v = 1, client_id = config.DiscordApp }, stop);

        var ready = await ReadAsync(pipe, stop);

        if (ready is null || ready.Value.Op == Close)
        {
            throw new IOException($"Discord no aceptó la aplicación {config.DiscordApp}: {ready?.Json}");
        }

        // Solo el nombre de la aplicación: sin detalles, sin estado, sin tiempo.
        object activity = string.IsNullOrWhiteSpace(config.DiscordImagen)
            ? new { instance = false }
            : new { instance = false, assets = new { large_image = config.DiscordImagen, large_text = "PermaLocke" } };

        await WriteAsync(pipe, Frame, new
        {
            cmd = "SET_ACTIVITY",
            args = new { pid = Environment.ProcessId, activity },
            nonce = Guid.NewGuid().ToString()
        }, stop);

        _lastProblem = null;
        logger.LogInformation("Estado de Discord puesto con la aplicación {App}", config.DiscordApp);

        // Mientras la tubería siga abierta, el estado sigue. Discord pregunta de vez en cuando si seguimos aquí.
        while (await ReadAsync(pipe, stop) is { } frame)
        {
            if (frame.Op == Ping)
            {
                await WriteRawAsync(pipe, Pong, frame.Json, stop);
            }
            else if (frame.Op == Close)
            {
                return;
            }
            else if (frame.Json.Contains("\"evt\":\"ERROR\"", StringComparison.Ordinal))
            {
                logger.LogWarning("Discord rechazó el estado: {Answer}", frame.Json);
            }
        }
    }

    private static Task WriteAsync(Stream pipe, int op, object payload, CancellationToken stop) =>
        WriteRawAsync(pipe, op, JsonSerializer.Serialize(payload), stop);

    private static async Task WriteRawAsync(Stream pipe, int op, string json, CancellationToken stop)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var frame = new byte[8 + body.Length];

        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame, 8);

        await pipe.WriteAsync(frame, stop);
        await pipe.FlushAsync(stop);
    }

    /// <summary>One frame from Discord, or null when the pipe has closed.</summary>
    private static async Task<(int Op, string Json)?> ReadAsync(Stream pipe, CancellationToken stop)
    {
        var header = new byte[8];

        if (!await FillAsync(pipe, header, stop))
        {
            return null;
        }

        var op = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

        if (length is < 0 or > 64 * 1024)
        {
            throw new IOException($"Discord mandó un mensaje de {length} bytes.");
        }

        var body = new byte[length];

        return await FillAsync(pipe, body, stop) ? (op, Encoding.UTF8.GetString(body)) : null;
    }

    private static async Task<bool> FillAsync(Stream pipe, byte[] buffer, CancellationToken stop)
    {
        var read = 0;

        while (read < buffer.Length)
        {
            var got = await pipe.ReadAsync(buffer.AsMemory(read), stop);

            if (got == 0)
            {
                return false;
            }

            read += got;
        }

        return true;
    }
}
