using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// The copies on the server (§198, plan del próximo torneo, paso 5): every six hours the application is open, a zip with
/// the run and the Ultra Moon save (<see cref="ServerBackup"/>) goes to the tournament's Storage, to
/// <c>copias/&lt;id del jugador&gt;/&lt;fecha&gt;.zip</c>. If the player loses the PC or the folder, the organiser downloads
/// the last one from Admin.
/// </summary>
/// <remarks>
/// <para>
/// Only in a distributed folder (<c>PermaLocke.local</c>), signed in, with a run, and never with <c>--sin-juego</c>.
/// Nothing is sent when nothing changed since the last copy (same save, same run files): a PC left open for days
/// does not push its older copies out of the five the LIMPIEZA keeps.
/// </para>
/// <para>
/// The bucket comes with <c>tools/supabase/16-copias.sql</c>. Without it the upload fails, the failure is logged and it
/// is tried again six hours later; playing never waits for it. The player's own folder is only read.
/// </para>
/// </remarks>
public sealed class ServerBackupService(AppPaths paths, DiscordLogin discord, PlayerSave save, ILogger<ServerBackupService> logger)
{
    public const string Bucket = "copias";

    private static readonly TimeSpan Every = TimeSpan.FromHours(6);

    private sealed record State(DateTimeOffset Ultima, string Huella);

    private string StatePath => Path.Combine(paths.Config, "copia-servidor.json");

    public void Start()
    {
        if (paths.LocalOnly)
        {
            _ = LoopAsync();
        }
    }

    private async Task LoopAsync()
    {
        // Un rato después de abrir: el arranque ya tiene bastante con lo suyo.
        await Task.Delay(TimeSpan.FromMinutes(2));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        do
        {
            try
            {
                await BackUpIfDueAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo subir la copia al servidor; se reintenta en la próxima vuelta");
            }
        }
        while (await timer.WaitForNextTickAsync());
    }

    private async Task BackUpIfDueAsync()
    {
        var state = Read();
        if (state is not null && DateTimeOffset.Now - state.Ultima < Every)
        {
            return;
        }

        if (discord.Saved is not { UserId: var user } || user == Guid.Empty
            || !Directory.EnumerateFiles(paths.Saves, "run.json", SearchOption.AllDirectories).Any())
        {
            return;
        }

        var game = save.Find();
        var print = Fingerprint(game);
        if (state is not null && state.Huella == print)
        {
            Write(state with { Ultima = DateTimeOffset.Now });
            logger.LogInformation("Copia al servidor: nada nuevo desde la última");
            return;
        }

        var now = DateTimeOffset.Now;
        var bytes = await Task.Run(() => ServerBackup.Build(paths.Saves, game, now));
        var name = $"{user}/{now.UtcDateTime:yyyyMMdd-HHmmss}.zip";

        if (await discord.UploadAsync(Bucket, name, bytes, "application/zip"))
        {
            Write(new State(now, print));
            logger.LogInformation("Copia subida al servidor: {Name} ({Bytes} bytes)", name, bytes.Length);
        }
    }

    /// <summary>What the copy is made of, cheaply: the game save's hash and the run files' sizes and dates.</summary>
    private string Fingerprint(string? game)
    {
        var text = new StringBuilder();
        if (game is not null && File.Exists(game))
        {
            using var stream = new FileStream(game, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            text.Append(Convert.ToHexString(SHA256.HashData(stream)));
        }

        var database = new FileInfo(Path.Combine(paths.Saves, "permalocke.db"));
        var wal = new FileInfo(database.FullName + "-wal");
        foreach (var file in new[] { database, wal }.Concat(
                     Directory.EnumerateFiles(paths.Saves, "*.json", SearchOption.AllDirectories)
                         .Where(f => !f.StartsWith(paths.SaveBackups, StringComparison.OrdinalIgnoreCase))
                         .Order(StringComparer.Ordinal).Select(f => new FileInfo(f))))
        {
            if (file.Exists) text.Append($"|{file.Name}:{file.Length}:{file.LastWriteTimeUtc.Ticks}");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private State? Read()
    {
        try
        {
            return File.Exists(StatePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void Write(State state) => File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
}
