using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.Infrastructure;

namespace PermaLocke.Admin.ViewModels;

/// <summary>A rules file the organiser can change for everybody.</summary>
public sealed record RuleFile(string File, string What)
{
    public override string ToString() => $"{What}  ({File})";
}

/// <summary>
/// REGLAS (2026-09-26): the official rules of the tournament, edited here and downloaded by every player's PermaLocke.
/// </summary>
/// <remarks>
/// <para>
/// One row of <c>reglas</c> per file (<c>tools/supabase/14-reglas.sql</c>). With no row, every player keeps the file their
/// PermaLocke came with; the editor then starts from this computer's <c>Data/</c>. Players get a change the next time
/// they open PermaLocke and it applies when they open it again: everything reads <c>Data/</c> when it starts.
/// </para>
/// <para>
/// Nothing is uploaded that does not parse, and the files PermaLocke has a loader for are also loaded with it, so a
/// price written as text is caught here and not on six computers.
/// </para>
/// </remarks>
public sealed partial class RulesViewModel(DiscordLogin discord, AppPaths paths, ILogger<RulesViewModel> logger) : ObservableObject
{
    private sealed record Row(string Fichero, string Contenido, DateTimeOffset Actualizado);

    public ObservableCollection<RuleFile> Files { get; } = [.. TournamentRules.Files.Select(f => new RuleFile(f.File, f.What))];

    [ObservableProperty]
    private RuleFile? _selected;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _status = "Elige un fichero.";

    partial void OnSelectedChanged(RuleFile? value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Selected is not { } file)
        {
            return;
        }

        try
        {
            var json = await discord.GetAsync($"reglas?select=fichero,contenido,actualizado&fichero=eq.{Uri.EscapeDataString(file.File)}");
            var row = json is null ? null : (JsonSerializer.Deserialize<List<Row>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []).FirstOrDefault();

            if (row is not null)
            {
                Text = row.Contenido;
                Status = $"Versión oficial del servidor, del {row.Actualizado.LocalDateTime:dd/MM HH:mm}. Todos la tienen al abrir PermaLocke.";
            }
            else
            {
                var local = Path.Combine(paths.Data, file.File);
                Text = File.Exists(local) ? await File.ReadAllTextAsync(local) : string.Empty;
                Status = "Sin versión oficial: cada jugador usa la que venía con su PermaLocke. Esto es la de este PC.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer la regla {File}", file.File);
            Status = "No se ha podido leer el servidor. ¿Has ejecutado 14-reglas.sql?";
        }
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (Selected is not { } file)
        {
            return;
        }

        if (Problem(file.File, Text) is { } problem)
        {
            Status = $"No se sube: {problem}";
            return;
        }

        if (System.Windows.MessageBox.Show(
                $"Subir {file.File} como regla oficial.\n\nCada jugador la recibe al abrir PermaLocke y se le aplica al reiniciarlo. " +
                "Se guarda una copia de la suya.", "Reglas oficiales", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.PostAsync("reglas", JsonSerializer.Serialize(new { fichero = file.File, contenido = Text, actualizado = DateTimeOffset.UtcNow }),
                "resolution=merge-duplicates");
            logger.LogInformation("Regla oficial {File} subida", file.File);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo subir la regla {File}", file.File);
            Status = "No se ha podido subir. ¿Has ejecutado 14-reglas.sql y eres organizador?";
        }
    }

    /// <summary>Takes the official version off the server: every player goes back to the file of their PermaLocke.</summary>
    [RelayCommand]
    private async Task WithdrawAsync()
    {
        if (Selected is not { } file || System.Windows.MessageBox.Show(
                $"Quitar la versión oficial de {file.File} del servidor.\n\nLos jugadores que ya la descargaron la conservan hasta " +
                "que actualicen PermaLocke; los nuevos usarán la que traiga su versión.", "Reglas oficiales",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await discord.DeleteAsync($"reglas?fichero=eq.{Uri.EscapeDataString(file.File)}");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo quitar la regla {File}", file.File);
            Status = "No se ha podido quitar.";
        }
    }

    /// <summary>Why a file cannot go up, or null: it must parse, and load with PermaLocke's own loader where there is one.</summary>
    public static string? Problem(string file, string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            return $"no es JSON válido ({ex.Message}).";
        }

        var temporary = Path.Combine(Path.GetTempPath(), $"permalocke-regla-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(temporary, text);

            switch (file)
            {
                case "shop.json":
                    _ = JsonShopCatalog.Load(temporary).Items.Count;
                    break;
                case "gacha.json":
                    _ = JsonGachaCatalog.Load(temporary).Banners.Count;
                    break;
                case "wondertrade.json":
                    _ = JsonWonderTradeCatalog.Load(temporary);
                    break;
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"PermaLocke no lo puede cargar ({ex.Message}).";
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
