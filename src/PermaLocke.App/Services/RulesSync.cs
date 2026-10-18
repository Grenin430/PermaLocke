using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// Brings the organiser's official rules into this PermaLocke's <c>Data/</c> (2026-09-26, <c>14-reglas.sql</c>).
/// </summary>
/// <remarks>
/// <para>
/// Once when the application opens, after signing in. A file is written only when the server's differs from the local
/// one, only if it is valid JSON, and only among <see cref="TournamentRules.Files"/>; the local one is kept first in
/// <c>Data/reglas-anteriores</c>. Everything reads <c>Data/</c> when it starts, so the new rules apply from the next start
/// of PermaLocke, and the player is told so.
/// </para>
/// <para>
/// A file with no row on the server is left alone: each player keeps the one their PermaLocke came with. Off with
/// <c>--sin-juego</c>, which in the repository would overwrite the files under version control.
/// </para>
/// </remarks>
public sealed class RulesSync(DiscordLogin discord, AppPaths paths, Notifier notifier, ILogger<RulesSync> logger)
{
    private sealed record Row(string Fichero, string Contenido);

    public async Task SyncAsync()
    {
        try
        {
            if (await discord.GetAsync("reglas?select=fichero,contenido") is not { } json)
            {
                return;
            }

            var changed = new List<string>();

            foreach (var row in JsonSerializer.Deserialize<List<Row>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
            {
                if (!TournamentRules.IsRuleFile(row.Fichero) || !IsJson(row.Contenido))
                {
                    logger.LogWarning("Regla del servidor ignorada: {File}", row.Fichero);
                    continue;
                }

                var local = Path.Combine(paths.Data, row.Fichero);

                if (File.Exists(local) && Same(await File.ReadAllTextAsync(local), row.Contenido))
                {
                    continue;
                }

                if (File.Exists(local))
                {
                    var backup = Path.Combine(paths.Data, "reglas-anteriores");
                    Directory.CreateDirectory(backup);
                    File.Copy(local, Path.Combine(backup, $"{DateTime.Now:yyyyMMdd-HHmmss}-{row.Fichero}"));
                }

                await File.WriteAllTextAsync(local, row.Contenido);
                changed.Add(TournamentRules.Files.First(f => string.Equals(f.File, row.Fichero, StringComparison.OrdinalIgnoreCase)).What);
                logger.LogInformation("Regla oficial {File} descargada del servidor", row.Fichero);
            }

            if (changed.Count > 0)
            {
                notifier.Say(ToastKind.Warning, "Reglas nuevas del organizador",
                    $"{string.Join(" · ", changed)}. Se aplican al reiniciar PermaLocke.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se han podido leer las reglas oficiales del servidor");
        }
    }

    private static bool IsJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Equal apart from line endings, which an editor on another machine may change.</summary>
    private static bool Same(string a, string b) =>
        string.Equals(a.ReplaceLineEndings("\n").Trim(), b.ReplaceLineEndings("\n").Trim(), StringComparison.Ordinal);
}
