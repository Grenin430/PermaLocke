using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.App.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One kind of thing the cleanup would remove, and what it takes.</summary>
public sealed record CleanupLine(string Name, string Why, long Rows, long Bytes, string Unit = "filas")
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
/// <para>
/// Storage too (§198): the files a function of the server lists as left over — the copies beyond each player's five
/// latest (<c>copias_sobrantes</c>, 16-copias.sql) — are removed through the Storage API after the same yes, because
/// Supabase does not let SQL delete files. A server without that function simply has none to show.
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

    private sealed record StoredFile(string Nombre, long Bytes);

    /// <summary>The Storage buckets the cleanup looks at: the function that lists what is left over, and its arguments.</summary>
    private static readonly (string Bucket, string Function, object Arguments, string Name, string Why)[] Buckets =
    [
        (GiftDesk.CopiesBucket, "copias_sobrantes", new { p_guardar = 5 }, "Copias de seguridad",
            "de cada jugador, todas menos sus 5 últimas")
    ];

    /// <summary>The Storage files the last count found left over, by bucket: exactly what LIMPIAR removes.</summary>
    private readonly Dictionary<string, IReadOnlyList<StoredFile>> _files = [];

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

            await CountFilesAsync();
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
        var list = string.Join("\n", Lines.Select(line => $"· {line.Name}: {line.Rows} {line.Unit} ({line.Size})"));
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
            var files = await RemoveFilesAsync();
            Status = $"Limpio: {UsageViewModel.Format(done.Bytes + files)} liberados. Postgres reaprovecha lo suyo con su limpieza automática.";
        }
        finally
        {
            Busy = false;
        }

        var left = await CallAsync(execute: false);
        if (left is not null)
        {
            await CountFilesAsync();
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

    /// <summary>What each Storage bucket has left over. A bucket whose function is not on the server yet shows nothing.</summary>
    private async Task CountFilesAsync()
    {
        _files.Clear();
        foreach (var (bucket, function, arguments, _, _) in Buckets)
        {
            try
            {
                if (await discord.CallAsync(function, JsonSerializer.Serialize(arguments)) is { } json)
                {
                    _files[bucket] = JsonSerializer.Deserialize<List<StoredFile>>(json, Json) ?? [];
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo contar lo que sobra en Storage ({Bucket}); ¿falta su SQL?", bucket);
            }
        }
    }

    /// <summary>Removes the Storage files the last count listed, a hundred at a time; the bytes it removed.</summary>
    private async Task<long> RemoveFilesAsync()
    {
        long removed = 0;
        foreach (var (bucket, files) in _files)
        {
            foreach (var chunk in files.Chunk(100))
            {
                try
                {
                    if (await discord.RemoveAsync(bucket, chunk.Select(file => file.Nombre).ToList()))
                    {
                        removed += chunk.Sum(file => file.Bytes);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudieron retirar ficheros de Storage ({Bucket})", bucket);
                }
            }

            logger.LogWarning("Limpieza de Storage: {Bucket}, {Files} ficheros", bucket, files.Count);
        }

        return removed;
    }

    private void Show(Reply reply)
    {
        Lines.Clear();
        var lines = new List<CleanupLine>();
        foreach (var (key, count) in reply.Cosas)
        {
            var (name, why) = Kinds.TryGetValue(key, out var kind) ? kind : (key, string.Empty);
            lines.Add(new CleanupLine(name, why, count.Filas, count.Bytes));
        }

        foreach (var (bucket, _, _, name, why) in Buckets)
        {
            if (_files.TryGetValue(bucket, out var files) && files.Count > 0)
            {
                lines.Add(new CleanupLine(name, why, files.Count, files.Sum(file => file.Bytes), "ficheros"));
            }
        }

        foreach (var line in lines.OrderByDescending(line => line.Bytes))
        {
            Lines.Add(line);
        }

        Bytes = lines.Sum(line => line.Bytes);
        Total = $"{UsageViewModel.Format(Bytes)} que se pueden liberar";
    }
}
