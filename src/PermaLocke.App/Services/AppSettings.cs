using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>What the player chose in CONFIGURACIÓN, as it is written to disk.</summary>
public sealed record AppSettingsData(
    bool Notifications = true,
    bool StepAside = true,
    bool DeathScene = true,
    bool Killcam = true,
    bool Follower = true,
    bool Ghosts = true,
    bool CatchCard = true);

/// <summary>
/// The player's preferences, kept in <c>Config/ajustes.json</c> and pushed into the services that obey them.
/// </summary>
/// <remarks>
/// Before this, «Avisarme mientras juego» and «Minimizar al abrir el juego» lived only in memory and came back on at
/// every start. Presentation preferences only: nothing here changes a rule, a point or the run, so none of it is an
/// event. The window size keeps its own file (<see cref="WindowSizeService"/>), because it already had one.
/// A missing or broken file means the defaults, never a failed start.
/// </remarks>
public sealed class AppSettings(
    AppPaths paths,
    Notifier notifier,
    EdgeTab tab,
    DeathCeremony ceremony,
    KillcamRecorder killcam,
    CatchCeremony catches,
    ILogger<AppSettings> logger)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private string FilePath => Path.Combine(paths.Config, "ajustes.json");

    public AppSettingsData Current { get; private set; } = new();

    /// <summary>Reads the file and applies it. Called once at start-up.</summary>
    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                Current = JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(FilePath)) ?? new();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron leer los ajustes; se usan los de siempre");
            Current = new();
        }

        Apply();
    }

    /// <summary>Applies a change at once and writes it down.</summary>
    public void Update(AppSettingsData settings)
    {
        Current = settings;
        Apply();

        try
        {
            Directory.CreateDirectory(paths.Config);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron guardar los ajustes");
        }
    }

    private void Apply()
    {
        notifier.Enabled = Current.Notifications;
        tab.StepAside = Current.StepAside;
        ceremony.Enabled = Current.DeathScene;
        ceremony.Ghosts = Current.Ghosts;
        killcam.Enabled = Current.Killcam;
        catches.Enabled = Current.CatchCard;
    }
}
