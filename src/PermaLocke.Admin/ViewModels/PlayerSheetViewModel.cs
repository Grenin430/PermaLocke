using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Admin.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.ViewModels;

/// <summary>One Pokémon of the player's run.</summary>
public sealed record SheetPokemon(Guid Id, string Name, string Species, int Level, string Status, string Origin,
    string Obtained, string Died, bool Alive, bool Dead, bool Shiny)
{
    public string ShinyMark => Shiny ? "  ★" : string.Empty;
}

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

/// <summary>One figure of the sheet's header: what it is and its value, already as text.</summary>
public sealed record SheetFigure(string Label, string Value);

/// <summary>Something about this player that wants the organiser's eyes, with the tab where it is.</summary>
/// <param name="Severe">Red rather than gold: a rewind, an order refused, an account in trouble.</param>
public sealed record Attention(string Text, string Tab, bool Severe);

/// <summary>
/// The organiser's sheet of one player (2026-09-26, redone 2026-10-08): everything their last upload says, what was sent to
/// them and what became of it, the internal notes and the log, and every action Admin can take on them.
/// </summary>
/// <remarks>
/// <para>
/// Read-only from the server: the snapshot and the whole history the player's application uploaded (every two minutes
/// while it is open). Nothing here edits the run. The game actions send an <see cref="AdminOrder"/>, which the player's
/// application applies on its own with its event (rule 4); MANDADO shows when it closed it and what it said.
/// </para>
/// <para>
/// Every action goes through <see cref="Confirm"/>, which says what will happen and asks the reason there. Each part of the
/// sheet loads on its own, so a missing piece (no run, no 21-admin.sql) leaves the rest working.
/// </para>
/// </remarks>
public sealed partial class PlayerSheetViewModel : ObservableObject
{
    private readonly GiftDesk _desk;
    private readonly ILogger _logger;
    private readonly string _from;
    private readonly Func<Task> _changed;
    private IReadOnlyList<SheetEvent> _allEvents = [];
    private IReadOnlyList<GameEvent> _history = [];
    private DateTimeOffset? _runStart;
    private bool _hasRun;

    public PlayerSheetViewModel(GiftDesk desk, PlayerLine player, string from, IReadOnlyList<Pick> species,
        IReadOnlyList<Pick> items, ILogger logger, Func<Task>? changed = null)
    {
        _desk = desk;
        _logger = logger;
        _from = from;
        _changed = changed ?? (() => Task.CompletedTask);
        Player = player;
        SpeciesChoices = species;
        ItemChoices = items;
    }

    public PlayerLine Player { get; }

    public IReadOnlyList<Pick> SpeciesChoices { get; }

    public IReadOnlyList<Pick> ItemChoices { get; }

    public ObservableCollection<SheetFigure> Figures { get; } = [];

    public ObservableCollection<Attention> Attentions { get; } = [];

    public ObservableCollection<SheetPokemon> Pokemon { get; } = [];

    public ObservableCollection<SheetEvent> Events { get; } = [];

    public ObservableCollection<SheetEvent> RecentEvents { get; } = [];

    public ObservableCollection<SheetZone> Zones { get; } = [];

    public ObservableCollection<SheetWipe> Wipes { get; } = [];

    public ObservableCollection<SheetEvent> Flags { get; } = [];

    public ObservableCollection<string> Achievements { get; } = [];

    public ObservableCollection<OrderLine> Orders { get; } = [];

    public ObservableCollection<NoteLine> Notes { get; } = [];

    public ObservableCollection<LogLine> Log { get; } = [];

    /// <summary>The copies of their run and save on the server (§198), newest first.</summary>
    public ObservableCollection<StoredFile> Copies { get; } = [];

    public ObservableCollection<string> EventTypes { get; } = ["TODOS"];

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Why the run could not be read, or why there is none; empty when it was.</summary>
    [ObservableProperty]
    private string _runProblem = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    [ObservableProperty]
    private string _uploaded = string.Empty;

    [ObservableProperty]
    private string _eventSearch = string.Empty;

    [ObservableProperty]
    private string _eventType = "TODOS";

    /// <summary>The tab on screen, so «ver» in RESUMEN can jump to one.</summary>
    [ObservableProperty]
    private int _tab;

    public bool HasRunProblem => RunProblem.Length > 0;
    
    public bool HasFlags => Flags.Count > 0;

    partial void OnRunProblemChanged(string value) => OnPropertyChanged(nameof(HasRunProblem));

    partial void OnEventSearchChanged(string value) => FilterEvents();

    partial void OnEventTypeChanged(string value) => FilterEvents();

    /// <summary>Notes, log and the account need 21-admin.sql on the server.</summary>
    public bool ControlCentre => _desk.ControlCentre;

    public bool CanOrder => Player.HasAccount;

    public string SuspendText => Player.Suspended ? "REACTIVAR" : "SUSPENDER";

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

    [ObservableProperty]
    private string _newNote = string.Empty;

    // ============================================================ LECTURA

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Status = string.Empty;

        try
        {
            await LoadRunAsync();
            await Task.WhenAll(LoadFilesAsync(), LoadOrdersAsync(), LoadNotesAsync(), LoadLogAsync());
            BuildAttention();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRunAsync()
    {
        Figures.Clear();
        Pokemon.Clear();
        Zones.Clear();
        Wipes.Clear();
        Flags.Clear();
        Achievements.Clear();
        _history = [];
        _allEvents = [];
        _runStart = null;
        _hasRun = false;

        if (!Player.HasAccount)
        {
            RunProblem = "No ha entrado nunca en PermaLocke: no hay run ni se le puede mandar nada hasta que entre con su Discord.";
            FilterEvents();
            return;
        }

        try
        {
            if (await _desk.RunOfAsync(Player.Id) is not { } run)
            {
                RunProblem = Player.Archived > 0
                    ? $"No tiene run activa ({Player.Archived} archivada(s)). Puede crear una al abrir PermaLocke; las archivadas están en AUDITORÍA."
                    : "No tiene ninguna run en el servidor todavía.";
                FilterEvents();
                return;
            }

            RunProblem = string.Empty;
            var (snapshot, history, uploaded) = run;
            _history = history?.Events ?? [];
            _runStart = snapshot.RunCreatedAt == default ? null : snapshot.RunCreatedAt;
            _hasRun = true;

            foreach (var p in (snapshot.Pokemon ?? []).OrderBy(p => p.Status).ThenByDescending(p => p.Level))
            {
                Pokemon.Add(new SheetPokemon(p.Id, p.Nickname ?? p.SpeciesName, p.SpeciesName, p.Level, Fate(p.Status),
                    p.Origin.ToString(), p.ObtainedAt.LocalDateTime.ToString("dd/MM HH:mm"),
                    p.DiedAt?.LocalDateTime.ToString("dd/MM HH:mm") ?? string.Empty,
                    p.Status == PokemonStatus.Alive, p.Status == PokemonStatus.Dead, p.IsShiny));
            }

            _allEvents = [.. _history.OrderByDescending(e => e.Timestamp).Select(Row)];
            EventTypes.Clear();
            EventTypes.Add("TODOS");
            foreach (var type in _allEvents.Select(e => e.Type).Distinct().Order()) EventTypes.Add(type);
            FilterEvents();

            foreach (var zone in SpentZones(_history)) Zones.Add(zone);

            var revoked = _history.Where(e => e.Type == GameEventType.WipeRevoked)
                .Select(e => e.Data.GetValueOrDefault("equipoCaido") ?? string.Empty).ToHashSet();
            foreach (var wipe in _history.Where(e => e.Type == GameEventType.TeamWiped && !revoked.Contains(e.Id.ToString())))
            {
                Wipes.Add(new SheetWipe(wipe.Id, wipe.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"), wipe.Description));
            }

            foreach (var flag in _allEvents.Where(e => e.Type == DisplayName(GameEventType.IntegrityFlag))) Flags.Add(flag);

            foreach (var achievement in _history.Where(e => e.Type == GameEventType.AchievementUnlocked).OrderByDescending(e => e.Timestamp))
            {
                Achievements.Add($"{achievement.Timestamp.LocalDateTime:dd/MM HH:mm}  {achievement.Description}");
            }

            Stages = snapshot.StagesCleared;
            Uploaded = $"Última subida {uploaded.LocalDateTime:dd/MM HH:mm} ({PlayerLine.Ago(uploaded)})";
            var es = CultureInfo.GetCultureInfo("es-ES");
            Figures.Add(new SheetFigure("PUNTOS", snapshot.Points.ToString("N0", es)));
            Figures.Add(new SheetFigure("VIVOS", snapshot.Alive.ToString(es)));
            Figures.Add(new SheetFigure("CAÍDOS", snapshot.Dead.ToString(es)));
            Figures.Add(new SheetFigure("PRUEBAS", snapshot.StagesCleared.ToString(es)));
            Figures.Add(new SheetFigure("CAP", snapshot.LevelCap?.ToString(es) ?? "—"));
            Figures.Add(new SheetFigure("LOGROS", $"{snapshot.AchievementsUnlocked}/{snapshot.AchievementsTotal}"));
            Figures.Add(new SheetFigure("HORAS", snapshot.PlayedHours?.ToString("0.#", es) ?? "—"));
            Figures.Add(new SheetFigure("EVENTOS", snapshot.EventCount.ToString("N0", es)));

            if (snapshot.Pokemon is null)
            {
                Status = "Su PermaLocke es anterior a la ficha: los Pokémon aparecerán cuando actualice.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer la run de {Player}", Player.Name);
            RunProblem = $"No se ha podido leer su run del servidor: {ex.Message}";
            FilterEvents();
        }
    }

    /// <summary>Their files in Storage. Never throws: without the bucket, the tab is just empty.</summary>
    private async Task LoadFilesAsync()
    {
        Copies.Clear();
        if (!Player.HasAccount) return;
        foreach (var file in await _desk.FilesAsync(GiftDesk.CopiesBucket, Player.Id)) Copies.Add(file);
    }

    private async Task LoadOrdersAsync()
    {
        Orders.Clear();
        if (!Player.HasAccount) return;

        try
        {
            foreach (var order in await _desk.OrdersOfAsync(Player.Id, _history, _runStart, _hasRun)) Orders.Add(order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer lo mandado a {Player}", Player.Name);
        }
    }

    private async Task LoadNotesAsync()
    {
        Notes.Clear();
        if (!_desk.ControlCentre || Player.DiscordId.Length == 0) return;

        try
        {
            foreach (var note in await _desk.NotesAsync(Player.DiscordId)) Notes.Add(note);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron leer las notas de {Player}", Player.Name);
        }
    }

    private async Task LoadLogAsync()
    {
        Log.Clear();
        if (!_desk.ControlCentre || !Player.HasAccount) return;

        try
        {
            foreach (var line in await _desk.LogAsync(Player.Id, 200)) Log.Add(line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer el registro de {Player}", Player.Name);
        }
    }

    /// <summary>RESUMEN's list: only things read from the server, each with the tab that shows it.</summary>
    private void BuildAttention()
    {
        Attentions.Clear();        OnPropertyChanged(nameof(HasFlags));
        var now = DateTimeOffset.Now;

        if (Player.Suspended) Attentions.Add(new Attention("Cuenta suspendida: no entra ni sube nada.", "ACCIONES", true));
        if (!Player.InList) Attentions.Add(new Attention("Tiene run pero ya no está en la lista.", "ACCIONES", true));
        if (Player.Rewinds > 0)
        {
            Attentions.Add(new Attention($"{Player.Rewinds} subida(s) en las que su historial retrocedió o se reescribió: compáralas en AUDITORÍA.", "", true));
        }

        if (Flags.Count > 0) Attentions.Add(new Attention($"{Flags.Count} aviso(s) de recarga o de juego sin PermaLocke.", "", true));

        var stuck = Orders.Count(o => o.Stuck(now));
        if (stuck > 0) Attentions.Add(new Attention($"{stuck} envío(s) sin aplicar desde hace más de 30 min.", "MANDADO", false));

        if (Player.HasRun && Player.LastUpload is { } upload && now - upload > TimeSpan.FromDays(2))
        {
            Attentions.Add(new Attention($"No sube su run desde {PlayerLine.Ago(upload)}.", "", false));
        }

        if (Notes.Count > 0) Attentions.Add(new Attention($"{Notes.Count} nota(s) tuya(s) sobre este jugador.", "NOTAS", false));
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

        RecentEvents.Clear();
        foreach (var e in _allEvents.Take(8)) RecentEvents.Add(e);
    }

    /// <summary>Jumps to a tab by its name (from RESUMEN).</summary>
    [RelayCommand]
    private void Show(string tab) => Tab = Math.Max(0, Array.IndexOf(Tabs, tab));

    /// <summary>The tabs of the sheet, in the order the view shows them.</summary>
    public static readonly string[] Tabs = ["RESUMEN", "POKÉMON", "HISTORIAL", "RUTAS", "MANDADO", "COPIAS", "NOTAS", "ACCIONES"];

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
                Say("Entra con Discord.", true);
                return;
            }

            await System.IO.File.WriteAllBytesAsync(dialog.FileName, bytes);
            Say($"Guardado en {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo descargar {File}", file.Path);
            Say($"No se ha podido descargar: {ex.Message}", true);
        }
    }

    private void Say(string text, bool error = false)
    {
        Status = text;
        StatusIsError = error;
    }

    // ============================================================ ÓRDENES

    /// <summary>The note every order adds about how it arrives.</summary>
    private const string Arrival = "Su PermaLocke lo aplica solo en unos segundos si está abierto, o al abrirlo, y deja el evento en su historial.";

    /// <returns>True when it was sent.</returns>
    private async Task<bool> SendAsync(string kind, Dictionary<string, string> args, string summary, params string[] consequences)
    {
        if (!CanOrder)
        {
            Say("No ha entrado nunca: no se le puede mandar nada.", true);
            return false;
        }

        if (Confirm.Ask(new ConfirmRequest(summary, Player.Name, [.. consequences, Arrival], "MANDAR")) is not { } answer)
        {
            return false;
        }

        try
        {
            await _desk.SendAsync(new AdminGift
            {
                Schema = AdminGift.OrderSchema,
                Id = Guid.NewGuid(),
                From = _from,
                To = Player.Id.ToString(),
                Reason = answer.Reason,
                CreatedAt = DateTimeOffset.Now,
                Order = new AdminOrder(kind, args, summary)
            });
            Say($"Mandado: {summary}. Míralo en MANDADO.");
            await LoadOrdersAsync();
            BuildAttention();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo mandar la orden {Kind} a {Player}", kind, Player.Name);
            Say($"No se ha podido mandar: {ex.Message}", true);
            return false;
        }
    }

    [RelayCommand]
    private Task ReviveAsync(SheetPokemon? p) => p is { Dead: true }
        ? SendAsync(AdminOrderKinds.Revive, new() { ["pokemon"] = p.Id.ToString() }, $"Revivir a {p.Name}",
            "Vuelve a estar vivo en su run y se le devuelve la penalización de su muerte.")
        : Task.CompletedTask;

    [RelayCommand]
    private Task KillAsync(SheetPokemon? p) => p is { Alive: true }
        ? SendAsync(AdminOrderKinds.Kill, new() { ["pokemon"] = p.Id.ToString() }, $"Marcar como caído a {p.Name}",
            "Cuenta como muerte, con su penalización, como si PermaLocke la hubiera visto.",
            "Se deshace con REVIVIR.")
        : Task.CompletedTask;

    [RelayCommand]
    private Task FreeZoneAsync(SheetZone? zone) => zone is null
        ? Task.CompletedTask
        : SendAsync(AdminOrderKinds.FreeZone, new() { ["zona"] = zone.Id, ["nombre"] = zone.Name }, $"Liberar {zone.Name}",
            "Podrá volver a capturar su primer encuentro en esa ruta.");

    [RelayCommand]
    private Task RevokeWipeAsync(SheetWipe? wipe) => wipe is null
        ? Task.CompletedTask
        : SendAsync(AdminOrderKinds.RevokeWipe, new() { ["wipe"] = wipe.EventId.ToString() }, $"Revocar el equipo caído del {wipe.When}",
            "Se le devuelve la penalización de ese equipo caído.");

    [RelayCommand]
    private Task SetStagesAsync()
    {
        if (Stages is >= 0 and <= 20)
        {
            return SendAsync(AdminOrderKinds.SetStages, new() { ["etapas"] = Stages.ToString(CultureInfo.InvariantCulture) },
            $"Poner sus pruebas superadas en {Stages}", "Mueve también su cap de nivel.");
        }

        Say("Las pruebas van de 0 a 20.", true);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task GiveItemAsync()
    {
        var item = ManualItemId > 0 ? new Pick(ManualItemId, $"objeto {ManualItemId}") : Item;

        if (item is null || ItemAmount is <= 0 or > 999)
        {
            Say("Elige el objeto y una cantidad de 1 a 999.", true);
            return Task.CompletedTask;
        }

        return SendAsync(AdminOrderKinds.GiveItem,
            new() { ["objeto"] = item.Id.ToString(CultureInfo.InvariantCulture), ["cantidad"] = ItemAmount.ToString(CultureInfo.InvariantCulture), ["nombre"] = item.Name },
            $"Dar {ItemAmount} x {item.Name}", "Llega a su mochila la próxima vez que tenga el juego abierto con la partida cargada.");
    }

    [RelayCommand]
    private Task GivePokemonAsync()
    {
        if (GiftSpecies is null)
        {
            Say("Elige la especie.", true);
            return Task.CompletedTask;
        }

        var level = Math.Clamp(GiftLevel, 1, 100);
        return SendAsync(AdminOrderKinds.GivePokemon, new()
        {
            ["especie"] = GiftSpecies.Id.ToString(CultureInfo.InvariantCulture),
            ["nivel"] = level.ToString(CultureInfo.InvariantCulture),
            ["variocolor"] = GiftShiny ? "true" : "false",
            ["forma"] = Math.Max(0, GiftForm).ToString(CultureInfo.InvariantCulture)
        }, $"Dar {GiftSpecies.Name} Nv. {level}{(GiftShiny ? " variocolor" : "")}",
            "Entra en sus cajas con el juego cerrado, como el gacha: habilidad y naturaleza al azar.",
            "Queda registrado como dado por ti.");
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (Message.Trim().Length == 0)
        {
            Say("Escribe el mensaje.", true);
            return;
        }

        if (await SendAsync(AdminOrderKinds.Message, new() { ["texto"] = Message.Trim() }, "Mensaje privado",
            $"«{Message.Trim()}»", "Le sale encima del juego y en su buzón. El motivo va de asunto."))
        {
            Message = string.Empty;
        }
    }

    [RelayCommand]
    private Task LockAsync() =>
        SendAsync(AdminOrderKinds.PlayLock, new() { ["cerrado"] = "true" }, "Cerrar el juego",
            "No podrá pulsar JUGAR hasta que lo vuelvas a abrir.", "Si está jugando, se le avisa y vale para la próxima vez.");

    [RelayCommand]
    private Task UnlockAsync() =>
        SendAsync(AdminOrderKinds.PlayLock, new() { ["cerrado"] = "false" }, "Volver a abrir el juego", "Vuelve a poder pulsar JUGAR.");

    /// <summary>Points on the organiser's say: the adjustment of REGALOS, from here and with the same shape.</summary>
    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (AdjustPoints == 0 || Math.Abs(AdjustPoints) > 100_000)
        {
            Say("Pon los puntos (en negativo para quitar, como mucho 100.000).", true);
            return;
        }

        if (!CanOrder || Confirm.Ask(new ConfirmRequest($"{AdjustPoints:+#;-#} puntos", Player.Name,
                ["Se aplica solo, sin que lo recoja, y queda en su historial con el motivo.", "Se deshace con otro ajuste del signo contrario."],
                "AJUSTAR", Danger: AdjustPoints < 0)) is not { } answer)
        {
            return;
        }

        try
        {
            await _desk.SendAsync(new AdminGift
            {
                Schema = AdminGift.AdjustmentSchema,
                Id = Guid.NewGuid(),
                From = _from,
                To = Player.Id.ToString(),
                Reason = answer.Reason,
                CreatedAt = DateTimeOffset.Now,
                Points = AdjustPoints,
                Adjustment = true
            });

            Say($"Ajuste de {AdjustPoints:+#;-#} puntos mandado.");
            AdjustPoints = 0;
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo mandar el ajuste a {Player}", Player.Name);
            Say($"No se ha podido mandar: {ex.Message}", true);
        }
    }

    // ============================================================ LA CUENTA (21-admin.sql la deja en el REGISTRO)

    [RelayCommand]
    private async Task ToggleSuspendAsync()
    {
        if (Player.DiscordId.Length == 0 || !Player.InList)
        {
            Say("Solo se suspende a alguien que está en la LISTA.", true);
            return;
        }

        var suspend = !Player.Suspended;
        var request = suspend
            ? new ConfirmRequest("Suspender", Player.Name,
                ["Sigue en la lista con todo lo suyo, pero no puede entrar ni subir nada.", "Su run no se toca.", "Se deshace con REACTIVAR."],
                "SUSPENDER", NeedsReason: false, Danger: true)
            : new ConfirmRequest("Reactivar", Player.Name, ["Vuelve a poder entrar y subir su run."], "REACTIVAR", NeedsReason: false);

        if (Confirm.Ask(request) is null) return;

        try
        {
            await _desk.SetSuspendedAsync(Player.DiscordId, suspend);
            Say(suspend ? "Suspendido." : "Reactivado.");
            await _changed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cambiar la suspensión de {Player}", Player.Name);
            Say($"No se ha podido: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private async Task ResetRunAsync()
    {
        if (!Player.HasRun)
        {
            Say("No tiene run activa que reiniciar.", true);
            return;
        }

        if (Confirm.Ask(new ConfirmRequest("Reiniciar su run", Player.Name,
            [
                "Su run activa pasa a archivada: deja de contar en la clasificación y no puede subir más.",
                "Al abrir PermaLocke podrá empezar una nueva desde cero.",
                "No se borra nada. AUDITORÍA puede reactivarla si no ha empezado otra."
            ], "REINICIAR", NeedsReason: false, Danger: true, TypeToConfirm: "REINICIAR")) is null)
        {
            return;
        }

        try
        {
            await _desk.ResetRunAsync(Player.Id);
            Say("Run archivada. Puede empezar otra al abrir PermaLocke.");
            await _changed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo reiniciar la run de {Player}", Player.Name);
            Say($"No se ha podido: {ex.Message}", true);
        }
    }

    // ============================================================ NOTAS

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        var text = NewNote.Trim();
        if (text.Length == 0 || text.Length > 2000)
        {
            Say("La nota va de 1 a 2000 letras.", true);
            return;
        }

        try
        {
            await _desk.AddNoteAsync(Player.DiscordId, text, _from);
            NewNote = string.Empty;
            await LoadNotesAsync();
            BuildAttention();
            Say("Nota guardada. Solo la ven los organizadores.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar la nota de {Player}", Player.Name);
            Say($"No se ha podido guardar: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteLine? note)
    {
        if (note is null || Confirm.Ask(new ConfirmRequest("Borrar la nota", Player.Name, [$"«{note.Text}»", "Su texto se queda en el REGISTRO."],
                "BORRAR", NeedsReason: false, Danger: true)) is null)
        {
            return;
        }

        try
        {
            await _desk.DeleteNoteAsync(note.Id);
            await LoadNotesAsync();
            BuildAttention();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo borrar la nota {Id}", note.Id);
            Say($"No se ha podido borrar: {ex.Message}", true);
        }
    }

    // ============================================================ LECTURA

    private static SheetEvent Row(GameEvent e) => new(e.Timestamp, e.Timestamp.LocalDateTime.ToString("dd/MM HH:mm"),
        DisplayName(e.Type), e.Source switch
        {
            EventSource.Player => "Jugador",
            EventSource.AutoDetect => "Detectado",
            EventSource.Admin => "Organizador",
            _ => "PermaLocke"
        }, e.Description, e.PointsDelta == 0 ? string.Empty : e.PointsDelta.ToString("+#;-#", CultureInfo.InvariantCulture));

    private static string DisplayName(GameEventType type) => PermaLocke.App.Services.DisplayNames.Of(type);

    /// <summary>Routes spent and not freed since, newest first: the same reading as the ball rule (§118).</summary>
    public static IReadOnlyList<SheetZone> SpentZones(IEnumerable<GameEvent> events)
    {
        var spent = new Dictionary<string, (SheetZone Zone, DateTimeOffset At)>();

        foreach (var e in events.OrderBy(e => e.Timestamp))
        {
            if (e.LocationId is not { } id)
            {
                continue;
            }

            if (e.Type is GameEventType.ZoneEncounterSpent or GameEventType.ZoneOutcomeSet)
            {
                spent[id] = (new SheetZone(id, e.Data.GetValueOrDefault("zona") ?? id, e.Timestamp.LocalDateTime.ToString("dd/MM HH:mm")), e.Timestamp);
            }
            else if (e.Type == GameEventType.ZoneCleared)
            {
                spent.Remove(id);
            }
        }

        return [.. spent.Values.OrderByDescending(z => z.At).Select(z => z.Zone)];
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
