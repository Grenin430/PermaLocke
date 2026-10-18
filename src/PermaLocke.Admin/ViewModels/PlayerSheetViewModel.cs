using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One Pokémon of the player's run.</summary>
public sealed record SheetPokemon(Guid Id, string Name, string Species, int Level, string Status, string Origin,
    string Obtained, string Died, bool Alive, bool Dead, bool Shiny);

/// <summary>One event of the player's history.</summary>
public sealed record SheetEvent(DateTimeOffset At, string When, string Type, string Source, string Description, string Points);

/// <summary>A route whose encounter is spent.</summary>
public sealed record SheetZone(string Id, string Name, string When);

/// <summary>A wipe that was charged and not revoked.</summary>
public sealed record SheetWipe(Guid EventId, string When, string Description);

/// <summary>A thing the organiser can give from the list, with its id.</summary>
public sealed record Pick(int Id, string Name)
{
    public override string ToString() => $"{Name} ({Id})";
}

/// <summary>
/// The organiser's sheet of one player (2026-09-26): everything their last upload says, and every order Admin can send.
/// </summary>
/// <remarks>
/// <para>
/// Read-only from the server: the snapshot and the whole history the player's application uploaded (every two minutes
/// while it is open). Nothing here edits the run. The buttons send an <see cref="AdminOrder"/>, which the player's
/// application applies on its own with its event; the sheet shows it once their next upload arrives.
/// </para>
/// <para>
/// Every order needs <see cref="Reason"/>: it goes into the player's history and they read it.
/// </para>
/// </remarks>
public sealed partial class PlayerSheetViewModel : ObservableObject
{
    private readonly GiftDesk _desk;
    private readonly ILogger _logger;
    private readonly string _from;
    private IReadOnlyList<SheetEvent> _allEvents = [];

    public PlayerSheetViewModel(GiftDesk desk, PlayerLine player, string from, IReadOnlyList<Pick> species,
        IReadOnlyList<Pick> items, ILogger logger)
    {
        _desk = desk;
        _logger = logger;
        _from = from;
        Player = player;
        SpeciesChoices = species;
        ItemChoices = items;
    }

    public PlayerLine Player { get; }

    public IReadOnlyList<Pick> SpeciesChoices { get; }

    public IReadOnlyList<Pick> ItemChoices { get; }

    public ObservableCollection<SheetPokemon> Pokemon { get; } = [];

    public ObservableCollection<SheetEvent> Events { get; } = [];

    public ObservableCollection<SheetZone> Zones { get; } = [];

    public ObservableCollection<SheetWipe> Wipes { get; } = [];

    public ObservableCollection<SheetEvent> Flags { get; } = [];

    public ObservableCollection<string> Achievements { get; } = [];

    /// <summary>The copies of their run and save on the server (§198), newest first.</summary>
    public ObservableCollection<StoredFile> Copies { get; } = [];

    public ObservableCollection<string> EventTypes { get; } = ["TODOS"];

    [ObservableProperty]
    private string _summary = "Leyendo...";

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _eventSearch = string.Empty;

    [ObservableProperty]
    private string _eventType = "TODOS";

    partial void OnEventSearchChanged(string value) => FilterEvents();

    partial void OnEventTypeChanged(string value) => FilterEvents();

    // Lo que se da o se corrige desde ACCIONES.
    [ObservableProperty]
    private int _stages;

    [ObservableProperty]
    private Pick? _item;

    [ObservableProperty]
    private int _itemAmount = 1;

    /// <summary>Any item by its id, for one that is not in the shop list (Probe --objeto-find gives the id).</summary>
    [ObservableProperty]
    private int _manualItemId;

    [ObservableProperty]
    private Pick? _giftSpecies;

    [ObservableProperty]
    private int _giftLevel = 5;

    [ObservableProperty]
    private bool _giftShiny;

    [ObservableProperty]
    private int _giftForm;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private int _adjustPoints;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await LoadFilesAsync();

        try
        {
            if (await _desk.RunOfAsync(Player.Id) is not { } run)
            {
                Summary = $"{Player.Name} no tiene ninguna run activa en el servidor.";
                return;
            }

            var (snapshot, history, uploaded) = run;
            var events = history?.Events ?? [];

            Pokemon.Clear();
            foreach (var p in (snapshot.Pokemon ?? []).OrderBy(p => p.Status).ThenByDescending(p => p.Level))
            {
                Pokemon.Add(new SheetPokemon(p.Id, p.Nickname ?? p.SpeciesName, p.SpeciesName, p.Level, Fate(p.Status),
                    p.Origin.ToString(), p.ObtainedAt.LocalDateTime.ToString("dd/MM HH:mm"),
                    p.DiedAt?.LocalDateTime.ToString("dd/MM HH:mm") ?? string.Empty,
                    p.Status == PokemonStatus.Alive, p.Status == PokemonStatus.Dead, p.IsShiny));
            }

            _allEvents = [.. events.OrderByDescending(e => e.Timestamp).Select(Row)];
            EventTypes.Clear();
            EventTypes.Add("TODOS");
            foreach (var type in _allEvents.Select(e => e.Type).Distinct().Order()) EventTypes.Add(type);
            FilterEvents();

            Zones.Clear();
            foreach (var zone in SpentZones(events)) Zones.Add(zone);

            var revoked = events.Where(e => e.Type == GameEventType.WipeRevoked)
                .Select(e => e.Data.GetValueOrDefault("equipoCaido") ?? string.Empty).ToHashSet();
            Wipes.Clear();
            foreach (var wipe in events.Where(e => e.Type == GameEventType.TeamWiped && !revoked.Contains(e.Id.ToString())))
            {
                Wipes.Add(new SheetWipe(wipe.Id, wipe.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"), wipe.Description));
            }

            Flags.Clear();
            foreach (var flag in _allEvents.Where(e => e.Type == DisplayName(GameEventType.IntegrityFlag))) Flags.Add(flag);

            Achievements.Clear();
            foreach (var achievement in events.Where(e => e.Type == GameEventType.AchievementUnlocked))
            {
                Achievements.Add($"{achievement.Timestamp.LocalDateTime:dd/MM HH:mm}  {achievement.Description}");
            }

            Stages = snapshot.StagesCleared;
            Summary = string.Join("   ·   ", new[]
            {
                $"{snapshot.Points} puntos",
                $"rol {snapshot.RoleName}",
                $"{snapshot.StagesCleared} pruebas",
                snapshot.LevelCap is { } cap ? $"cap {cap}" : "sin cap",
                $"{snapshot.Alive} vivos, {snapshot.Dead} caídos",
                $"{snapshot.AchievementsUnlocked}/{snapshot.AchievementsTotal} logros",
                snapshot.PlayedHours is { } hours ? $"{hours.ToString("0.#", CultureInfo.GetCultureInfo("es-ES"))} h jugadas" : "horas sin dato",
                $"{Flags.Count} avisos",
                $"subida {uploaded.LocalDateTime:dd/MM HH:mm}"
            });

            if (snapshot.Pokemon is null)
            {
                Status = "Su PermaLocke es anterior a la ficha: los Pokémon aparecerán cuando actualice.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer la run de {Player}", Player.Name);
            Summary = "No se ha podido leer su run del servidor.";
        }
    }

    /// <summary>Their files in Storage. Never throws: without the bucket, the tab is just empty.</summary>
    private async Task LoadFilesAsync()
    {
        Copies.Clear();
        foreach (var file in await _desk.FilesAsync(GiftDesk.CopiesBucket, Player.Id)) Copies.Add(file);
    }

    /// <summary>
    /// Saves one of their files where the organiser says. Only reads the server: giving a copy back to the player is
    /// done by hand, following the LEEME inside it.
    /// </summary>
    [RelayCommand]
    private async Task DownloadAsync(StoredFile? file)
    {
        if (file is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{Player.Name} {file.FileName}",
            Filter = "Zip|*.zip",
            Title = "Guardar la copia"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (await _desk.DownloadAsync(file) is not { } bytes)
            {
                Status = "Entra con Discord.";
                return;
            }

            await System.IO.File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status = $"Guardado en {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo descargar {File}", file.Path);
            Status = "No se ha podido descargar.";
        }
    }

    private void FilterEvents()
    {
        Events.Clear();
        foreach (var e in _allEvents.Where(e =>
                     (EventType == "TODOS" || e.Type == EventType)
                     && (string.IsNullOrWhiteSpace(EventSearch)
                         || CultureInfo.InvariantCulture.CompareInfo.IndexOf(e.Description, EventSearch.Trim(),
                             CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)))
        {
            Events.Add(e);
        }
    }

    // ============================================================ ÓRDENES

    private async Task SendAsync(string kind, Dictionary<string, string> args, string summary, bool confirm = true)
    {
        if (Reason.Trim().Length == 0)
        {
            Status = "Escribe el MOTIVO arriba: lo lee el jugador y queda en su historial.";
            return;
        }

        if (confirm && System.Windows.MessageBox.Show($"{summary}\n\nPara: {Player.Name}\nMotivo: {Reason.Trim()}\n\n" +
                "Su PermaLocke lo aplica solo y queda en su historial.", "Orden al jugador",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _desk.SendOrderAsync(Player.Id, _from, Reason.Trim(), new AdminOrder(kind, args, summary));
            Status = $"Mandado: {summary}. Su PermaLocke lo aplica en unos segundos si está abierto, o al abrirlo.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo mandar la orden {Kind} a {Player}", kind, Player.Name);
            Status = "No se ha podido mandar.";
        }
    }

    [RelayCommand]
    private Task ReviveAsync(SheetPokemon? p) => p is { Dead: true }
        ? SendAsync(AdminOrderKinds.Revive, new() { ["pokemon"] = p.Id.ToString() }, $"Revivir a {p.Name}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task KillAsync(SheetPokemon? p) => p is { Alive: true }
        ? SendAsync(AdminOrderKinds.Kill, new() { ["pokemon"] = p.Id.ToString() }, $"Marcar como caído a {p.Name}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task FreeZoneAsync(SheetZone? zone) => zone is null
        ? Task.CompletedTask
        : SendAsync(AdminOrderKinds.FreeZone, new() { ["zona"] = zone.Id, ["nombre"] = zone.Name }, $"Liberar {zone.Name}");

    [RelayCommand]
    private Task RevokeWipeAsync(SheetWipe? wipe) => wipe is null
        ? Task.CompletedTask
        : SendAsync(AdminOrderKinds.RevokeWipe, new() { ["wipe"] = wipe.EventId.ToString() }, $"Revocar el equipo caído del {wipe.When}");

    [RelayCommand]
    private Task SetStagesAsync() =>
        SendAsync(AdminOrderKinds.SetStages, new() { ["etapas"] = Stages.ToString() }, $"Poner sus pruebas superadas en {Stages}");

    [RelayCommand]
    private Task GiveItemAsync()
    {
        var item = ManualItemId > 0 ? new Pick(ManualItemId, $"objeto {ManualItemId}") : Item;

        if (item is null || ItemAmount <= 0)
        {
            Status = "Elige el objeto y la cantidad.";
            return Task.CompletedTask;
        }

        return SendAsync(AdminOrderKinds.GiveItem,
            new() { ["objeto"] = item.Id.ToString(), ["cantidad"] = ItemAmount.ToString(), ["nombre"] = item.Name },
            $"Dar {ItemAmount} x {item.Name}");
    }

    [RelayCommand]
    private Task GivePokemonAsync()
    {
        if (GiftSpecies is null)
        {
            Status = "Elige la especie.";
            return Task.CompletedTask;
        }

        return SendAsync(AdminOrderKinds.GivePokemon, new()
        {
            ["especie"] = GiftSpecies.Id.ToString(),
            ["nivel"] = Math.Clamp(GiftLevel, 1, 100).ToString(),
            ["variocolor"] = GiftShiny ? "true" : "false",
            ["forma"] = GiftForm.ToString()
        }, $"Dar {GiftSpecies.Name} Nv. {Math.Clamp(GiftLevel, 1, 100)}{(GiftShiny ? " variocolor" : "")}");
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (Message.Trim().Length == 0)
        {
            Status = "Escribe el mensaje.";
            return;
        }

        await SendAsync(AdminOrderKinds.Message, new() { ["texto"] = Message.Trim() }, "Mensaje privado", confirm: false);
        Message = string.Empty;
    }

    [RelayCommand]
    private Task LockAsync() =>
        SendAsync(AdminOrderKinds.PlayLock, new() { ["cerrado"] = "true" }, "Cerrar el juego (no podrá pulsar JUGAR)");

    [RelayCommand]
    private Task UnlockAsync() =>
        SendAsync(AdminOrderKinds.PlayLock, new() { ["cerrado"] = "false" }, "Volver a abrir el juego");

    /// <summary>Points on the organiser's say: the adjustment of the main window, from here and with the same shape.</summary>
    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (AdjustPoints == 0 || Reason.Trim().Length == 0)
        {
            Status = "Pon los puntos y el MOTIVO.";
            return;
        }

        if (System.Windows.MessageBox.Show($"{AdjustPoints:+#;-#} puntos a {Player.Name}.\n\nMotivo: {Reason.Trim()}",
                "Ajustar puntos", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await _desk.SendAsync(new AdminGift
        {
            Schema = AdminGift.AdjustmentSchema,
            Id = Guid.NewGuid(),
            From = _from,
            To = Player.Id.ToString(),
            Reason = Reason.Trim(),
            CreatedAt = DateTimeOffset.Now,
            Points = AdjustPoints,
            Adjustment = true
        });

        Status = $"Ajuste de {AdjustPoints:+#;-#} puntos mandado.";
        AdjustPoints = 0;
    }

    // ============================================================ LECTURA

    private static SheetEvent Row(GameEvent e) => new(e.Timestamp, e.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"),
        DisplayName(e.Type), e.Source switch
        {
            EventSource.Player => "Jugador",
            EventSource.AutoDetect => "Detectado",
            EventSource.Admin => "Organizador",
            _ => "PermaLocke"
        }, e.Description, e.PointsDelta == 0 ? string.Empty : e.PointsDelta.ToString("+#;-#"));

    private static string DisplayName(GameEventType type) => PermaLocke.App.Services.DisplayNames.Of(type);

    /// <summary>Routes spent and not freed since, newest first: the same reading as the ball rule (§118).</summary>
    public static IReadOnlyList<SheetZone> SpentZones(IEnumerable<GameEvent> events)
    {
        var spent = new Dictionary<string, SheetZone>();

        foreach (var e in events.OrderBy(e => e.Timestamp))
        {
            if (e.LocationId is not { } id)
            {
                continue;
            }

            if (e.Type is GameEventType.ZoneEncounterSpent or GameEventType.ZoneOutcomeSet)
            {
                spent[id] = new SheetZone(id, e.Data.GetValueOrDefault("zona") ?? id, e.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"));
            }
            else if (e.Type == GameEventType.ZoneCleared)
            {
                spent.Remove(id);
            }
        }

        return [.. spent.Values.OrderByDescending(z => z.When)];
    }

    private static string Fate(PokemonStatus status) => status switch
    {
        PokemonStatus.Alive => "VIVO",
        PokemonStatus.Dead => "CAÍDO",
        PokemonStatus.Traded => "INTERCAMBIADO",
        PokemonStatus.Released => "LIBERADO",
        _ => status.ToString().ToUpperInvariant()
    };
}
