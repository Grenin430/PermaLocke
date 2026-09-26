using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One kind of thing the cleanup would remove, and what it takes.</summary>
public sealed record CleanupLine(string Name, string Why, long Rows, long Bytes)
{
    public string Size => UsageViewModel.Format(Bytes);
}

/// <summary>
/// LIMPIEZA (§194): what can go from the server without anything that reads it breaking, counted first and removed only
/// after a yes.
/// </summary>
/// <remarks>
/// <para>
/// All of it is decided by <c>limpieza()</c> (15-eventos-y-limpieza.sql), which only an organiser may call and which
/// lists exactly what it would delete before deleting it. This window only shows the count and asks. Never touched:
/// active runs, the whitelist, the organisers, the rules, the resets.
/// </para>
/// <para>
/// The histories of archived runs go only when ticked, with a second question: once gone, the audit of that run can
/// no longer check its chain — its summary stays.
/// </para>
/// </remarks>
public sealed partial class CleanupViewModel(DiscordLogin discord, ILogger<CleanupViewModel> logger) : ObservableObject
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record Count(long Filas, long Bytes);

    private sealed record Reply(Dictionary<string, Count> Cosas, long Bytes);

    /// <summary>What each kind is, in the words the organiser reads.</summary>
    private static readonly Dictionary<string, (string Name, string Why)> Kinds = new()
    {
        ["fantasmas"] = ("Fantasmas", "de más de un día; la app solo pinta los recientes"),
        ["lluvias"] = ("Lluvias de sangre", "de más de un día"),
        ["anuncios"] = ("Anuncios", "todos menos el último, que es el único que se lee"),
        ["regalos"] = ("Regalos", "a una persona, recogidos en su run activa y de hace más de 7 días"),
        ["subidas"] = ("Registro de subidas", "repetidas; quedan la primera, la última y todo retroceso"),
        ["historiales"] = ("Historial de runs archivadas", "el resumen se queda"),
        ["eventos"] = ("Eventos de runs archivadas", "los que subieron evento a evento")
    };

    public ObservableCollection<CleanupLine> Lines { get; } = [];

    [ObservableProperty]
    private string _total = "—";

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Also the histories of archived runs; their audit stops being able to check their chain.</summary>
    [ObservableProperty]
    private bool _histories;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private long _bytes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(CleanCommand))]
    private bool _busy;

    private bool CanCount => !Busy;

    private bool CanClean => !Busy && Bytes > 0;

    partial void OnHistoriesChanged(bool value) => _ = RefreshAsync();

    [RelayCommand(CanExecute = nameof(CanCount))]
    private async Task RefreshAsync()
    {
        Busy = true;
        try
        {
            if (await CallAsync(execute: false) is not { } reply)
            {
                return;
            }

            Show(reply);
            Status = reply.Bytes == 0
                ? "No hay nada que limpiar."
                : "Esto es lo que se quitaría. No se ha borrado nada todavía.";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var list = string.Join("\n", Lines.Select(line => $"· {line.Name}: {line.Rows} ({line.Size})"));
        if (System.Windows.MessageBox.Show(
                $"Se van a borrar del servidor, para siempre:\n\n{list}\n\nNada de esto lo vuelve a leer ninguna app. ¿Limpiar?",
                "Limpieza", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        if (Histories && System.Windows.MessageBox.Show(
                "También se vacía el historial de las runs archivadas. Su resumen se queda, pero la AUDITORÍA ya no podrá " +
                "comprobar su cadena. ¿Seguro?",
                "Limpieza", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        Busy = true;
        try
        {
            if (await CallAsync(execute: true) is not { } done)
            {
                return;
            }

            logger.LogWarning("Limpieza del servidor: {Bytes} bytes ({Histories})", done.Bytes,
                Histories ? "con historiales archivados" : "sin historiales");
            Status = $"Limpio: {UsageViewModel.Format(done.Bytes)} liberados. Postgres los reaprovecha con su limpieza automática.";
        }
        finally
        {
            Busy = false;
        }

        var left = await CallAsync(execute: false);
        if (left is not null)
        {
            Show(left);
        }
    }

    private async Task<Reply?> CallAsync(bool execute)
    {
        try
        {
            if (await discord.CallAsync("limpieza", JsonSerializer.Serialize(new { p_ejecutar = execute, p_historiales = Histories }))
                is not { } json)
            {
                Status = "Entra con Discord (la cuenta del organizador).";
                return null;
            }

            return JsonSerializer.Deserialize<Reply>(json, Json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo llamar a limpieza()");
            Status = "No se ha podido. ¿Has ejecutado 15-eventos-y-limpieza.sql?";
            return null;
        }
    }

    private void Show(Reply reply)
    {
        Lines.Clear();
        foreach (var (key, count) in reply.Cosas.OrderByDescending(pair => pair.Value.Bytes))
        {
            var (name, why) = Kinds.TryGetValue(key, out var kind) ? kind : (key, string.Empty);
            Lines.Add(new CleanupLine(name, why, count.Filas, count.Bytes));
        }

        Bytes = reply.Bytes;
        Total = $"{UsageViewModel.Format(reply.Bytes)} que se pueden liberar";
    }
}
