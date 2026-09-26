using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// Sends Azahar's crash reports (§168) straight to the organiser (§199, plan del próximo torneo, paso 6): to the
/// tournament's Storage, <c>informes/&lt;id del jugador&gt;/&lt;nombre&gt;.zip</c>, which Admin lists in INFORMES.
/// </summary>
/// <remarks>
/// <para>
/// The same zip that stays in <c>Diagnosticos\</c>: two logs, the last requests to the emulator and the computer; no ROM,
/// save or run. Sent the moment it is written and, for any that could not go (offline, not signed in, no bucket yet —
/// <c>17-informes.sql</c>), on the next start (<see cref="CrashReportQueue"/>). Never throws; a failure is logged.
/// </para>
/// </remarks>
public sealed class CrashReportUpload(AppPaths paths, DiscordLogin discord, ILogger<CrashReportUpload> logger)
{
    public const string Bucket = "informes";

    private readonly SemaphoreSlim _one = new(1, 1);

    private string Folder => Path.Combine(paths.Root, "Diagnosticos");

    /// <summary>Sends every report still pending. Called at start and after a crash writes a new one.</summary>
    public async Task SendPendingAsync()
    {
        await _one.WaitAsync();
        try
        {
            if (discord.Saved is not { UserId: var user } || user == Guid.Empty)
            {
                return;
            }

            foreach (var report in CrashReportQueue.Pending(Folder, paths.Config, DateTimeOffset.UtcNow))
            {
                var bytes = await File.ReadAllBytesAsync(report);
                if (!await discord.UploadAsync(Bucket, $"{user}/{Path.GetFileName(report)}", bytes, "application/zip"))
                {
                    return;
                }

                CrashReportQueue.MarkSent(paths.Config, report);
                logger.LogInformation("Informe de fallo enviado al organizador: {Report}", Path.GetFileName(report));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el informe de fallo al organizador; se reintenta al abrir");
        }
        finally
        {
            _one.Release();
        }
    }
}
