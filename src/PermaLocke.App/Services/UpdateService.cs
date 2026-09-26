using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using PermaLocke.App.ViewModels;
using PermaLocke.Core.Domain;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// The automatic update (§196, plan del próximo torneo, paso 3): at start, asks GitHub for the latest release of the
/// repository named in <c>Data/torneo.json</c> (<c>actualizaciones</c>) and, if it is newer and has an update package,
/// offers it; with a yes it downloads, checks and installs it (<see cref="AppUpdate"/>) and restarts.
/// </summary>
/// <remarks>
/// <para>
/// Only in a distributed folder (<c>PermaLocke.local</c>): a copy running from the repository is updated with git. Nothing
/// goes to the tournament's database: the versions live in GitHub Releases.
/// </para>
/// <para>
/// Never with the emulator open (the program is replaced under a running game's watcher). The official rules the
/// organiser published (<see cref="RulesSync"/>) stay: the Data files the server has a rule for are not overwritten, and
/// if the server cannot be asked, none of the rule files is.
/// </para>
/// </remarks>
public sealed class UpdateService(AppPaths paths, DiscordLogin discord, EmulatorLauncher emulator, IAppDialogs dialogs,
    ILogger<UpdateService> logger)
{
    /// <summary>The argument of the restart after installing: it waits for this instance to close.</summary>
    public const string RestartArgument = "--tras-actualizar";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private sealed record Settings(string? Actualizaciones);

    private sealed record RuleRow(string Fichero);

    /// <summary>The version running.</summary>
    public static Version Current => typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);

    private string Executable => Path.GetFileName(Environment.ProcessPath ?? "PermaLocke.exe");

    /// <summary>Removes what the last update left: the old program and the downloaded package.</summary>
    public void CleanUp()
    {
        if (paths.LocalOnly)
        {
            AppUpdate.CleanUp(paths.Root, Executable);
        }
    }

    /// <summary>Looks for a newer version and, if the player says yes, installs it and restarts. Never throws.</summary>
    public async Task CheckAsync()
    {
        if (!paths.LocalOnly || Repository() is not { } repository)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"PermaLocke/{Current.ToString(3)}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await Http.SendAsync(request, cancel.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Sin comprobar actualizaciones: GitHub responde {Status}", (int)response.StatusCode);
                return;
            }

            if (AppUpdate.Pick(await response.Content.ReadAsStringAsync(cancel.Token), Current) is not { } asset)
            {
                logger.LogInformation("PermaLocke {Version} está al día", Current.ToString(3));
                return;
            }

            logger.LogInformation("Versión nueva disponible: {Version} ({Bytes} bytes)", asset.Version, asset.Size);
            await OfferAsync(asset);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo comprobar si hay una versión nueva");
        }
    }

    private async Task OfferAsync(UpdateAsset asset)
    {
        if (emulator.IsRunning)
        {
            dialogs.Tell("Versión nueva", $"Hay una versión nueva de PermaLocke ({asset.Version}). Cierra el emulador y vuelve a abrir " +
                                          "PermaLocke para actualizar.");
            return;
        }

        var notes = asset.Notes.Length > 600 ? asset.Notes[..600] + "…" : asset.Notes;
        if (!dialogs.Confirm("Versión nueva",
                $"Hay una versión nueva de PermaLocke: {asset.Version} (tienes la {Current.ToString(3)}).\n\n{notes}\n\n" +
                $"Se descargan {asset.Size / (1024.0 * 1024):0.0} MB y PermaLocke se reinicia. Tu run, tu partida y tus ajustes " +
                "no se tocan. ¿Actualizar ahora?"))
        {
            return;
        }

        var folder = Path.Combine(paths.Root, AppUpdate.Staging);
        Directory.CreateDirectory(folder);
        var package = Path.Combine(folder, asset.Name);

        // La ventana de la descarga (§201): la barra, cuánto va, la velocidad y lo que queda; luego cada paso.
        var progress = new UpdateProgressViewModel(asset.Version);
        using (dialogs.ShowUpdateProgress(progress))
        {
            try
            {
                await DownloadAsync(asset, package, progress);
            }
            catch (OperationCanceledException)
            {
                Discard(package);
                logger.LogInformation("Actualización a {Version} cancelada por el jugador", asset.Version);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                Discard(package);
                logger.LogWarning(ex, "Falló la descarga de {Name}", asset.Name);
                dialogs.Tell("Versión nueva", "No se ha podido descargar. Se intentará la próxima vez que abras PermaLocke.");
                return;
            }

            progress.Step("COMPROBANDO LA DESCARGA");
            if (!await Task.Run(() => AppUpdate.Matches(package, asset)))
            {
                logger.LogWarning("La descarga de {Name} no coincide con la de GitHub", asset.Name);
                dialogs.Tell("Versión nueva", "La descarga ha llegado mal. Se intentará la próxima vez que abras PermaLocke.");
                return;
            }

            progress.Step("INSTALANDO");
            var keep = await OfficialRulesAsync();
            var result = await Task.Run(() => AppUpdate.Apply(package, paths.Root, Executable, keep, logger));
            if (!result.Done)
            {
                dialogs.Tell("Versión nueva", "No se ha podido instalar:\n\n" + string.Join("\n", result.Log));
                return;
            }

            progress.Step("REINICIANDO");
        }

        logger.LogInformation("PermaLocke actualizado a {Version}; se reinicia", asset.Version);
        Process.Start(new ProcessStartInfo(Path.Combine(paths.Root, Executable), RestartArgument) { UseShellExecute = false });
        Application.Current.Shutdown();
    }

    /// <summary>Downloads the package a piece at a time, telling the window how it goes about ten times a second.</summary>
    private static async Task DownloadAsync(UpdateAsset asset, string package, UpdateProgressViewModel progress)
    {
        var cancel = progress.Token;
        using var response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        var meter = new DownloadMeter(response.Content.Headers.ContentLength ?? asset.Size);
        var clock = Stopwatch.StartNew();
        var shown = TimeSpan.MinValue;
        var buffer = new byte[128 * 1024];
        long received = 0;

        await using var download = await response.Content.ReadAsStreamAsync(cancel);
        await using var file = File.Create(package);

        progress.Show(meter.Sample(0, TimeSpan.Zero));
        int read;
        while ((read = await download.ReadAsync(buffer, cancel)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancel);
            received += read;

            if (clock.Elapsed - shown >= TimeSpan.FromMilliseconds(100))
            {
                shown = clock.Elapsed;
                progress.Show(meter.Sample(received, shown));
            }
        }

        progress.Show(meter.Sample(received, clock.Elapsed));
    }

    /// <summary>A half-downloaded package is ours and useless: it goes.</summary>
    private void Discard(string package)
    {
        try
        {
            if (File.Exists(package)) File.Delete(package);
        }
        catch (IOException ex)
        {
            logger.LogInformation(ex, "No se pudo quitar la descarga a medias; se quita en el próximo arranque");
        }
    }

    /// <summary>The Data files the organiser has an official rule for; all the rule files if the server cannot say.</summary>
    private async Task<IReadOnlySet<string>> OfficialRulesAsync()
    {
        try
        {
            if (await discord.GetAsync("reglas?select=fichero") is { } json)
            {
                return (JsonSerializer.Deserialize<List<RuleRow>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
                    .Select(row => row.Fichero)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Sin la lista de reglas oficiales: se conservan todos los ficheros de reglas");
        }

        return TournamentRules.Files.Select(file => file.File).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>«dueño/repositorio» from <c>Data/torneo.json</c>, or null when it does not say.</summary>
    private string? Repository()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(paths.Data, "torneo.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return string.IsNullOrWhiteSpace(settings?.Actualizaciones) ? null : settings.Actualizaciones.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer Data/torneo.json para las actualizaciones");
            return null;
        }
    }
}
