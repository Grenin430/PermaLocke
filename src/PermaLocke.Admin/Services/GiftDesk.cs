using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Services;

/// <summary>A player of the tournament, as the organiser's list shows them.</summary>
/// <param name="Id">
/// Their account on the tournament server: what a gift is addressed to. <see cref="Guid.Empty"/> for somebody on the list
/// who has never signed in, who can be looked at but not sent anything.
/// </param>
/// <remarks>
/// The fields after the constructor come from <c>admin_jugadores()</c> (<c>tools/supabase/21-admin.sql</c>). On a server
/// without it they stay at their defaults, and null means «not known», never zero.
/// </remarks>
public sealed record PlayerLine(Guid Id, string Name, int Points, int Alive, int Dead, PresenceState State = PresenceState.Offline)
{
    public string Presence => State switch
    {
        PresenceState.Playing => "JUGANDO",
        PresenceState.InApp => "EN LA APP",
        _ => "DESCONECTADO"
    };

    public string DiscordId { get; init; } = string.Empty;

    /// <summary>False for somebody with a run who has since been taken off the list.</summary>
    public bool InList { get; init; } = true;

    public bool Suspended { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>The PermaLocke version their app last said; null before 1.0.16 or if it never said.</summary>
    public string? Version { get; init; }

    public DateTimeOffset? LastSeen { get; init; }

    public DateTimeOffset? LastUpload { get; init; }

    public bool HasRun { get; init; } = true;

    public int? Stages { get; init; }

    public int? LevelCap { get; init; }

    public int? Captured { get; init; }

    public int? Achievements { get; init; }

    public int? AchievementsTotal { get; init; }

    public string? Role { get; init; }

    public double? Hours { get; init; }

    public int? Events { get; init; }

    public int Archived { get; init; }

    public int Notes { get; init; }

    /// <summary>Uploads of their active run where the history went backwards or was rewritten.</summary>
    public int Rewinds { get; init; }

    public bool HasAccount => Id != Guid.Empty;

    /// <summary>What the list shows under the name: the account's state when it is not the normal one.</summary>
    public string Standing => !InList ? "FUERA DE LA LISTA" : Suspended ? "SUSPENDIDO" : !HasAccount ? "NUNCA HA ENTRADO" : !HasRun ? "SIN RUN" : string.Empty;

    public string LastUploadText => Ago(LastUpload);

    public string LastSeenText => Ago(LastSeen);

    public string VersionText => Version ?? "—";

    public string RoleText => Role ?? "—";

    public string StagesText => Stages?.ToString() ?? "—";

    public string CapText => LevelCap?.ToString() ?? "—";

    public string HoursText => Hours is { } h ? h.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) : "—";

    /// <summary>Something here wants the organiser's eyes: rewinds or an account in an odd state.</summary>
    public bool NeedsLook => Rewinds > 0 || Suspended || !InList;

    /// <summary>«hace 5 min», «hace 3 h», «12/10»; a dash when never.</summary>
    public static string Ago(DateTimeOffset? when, DateTimeOffset? now = null)
    {
        if (when is not { } at) return "—";

        var gone = (now ?? DateTimeOffset.Now) - at;
        return gone.TotalMinutes < 1 ? "ahora"
            : gone.TotalMinutes < 60 ? $"hace {(int)gone.TotalMinutes} min"
            : gone.TotalHours < 24 ? $"hace {(int)gone.TotalHours} h"
            : gone.TotalDays < 7 ? $"hace {(int)gone.TotalDays} d"
            : at.LocalDateTime.ToString("dd/MM/yy");
    }
}

/// <summary>One line of the organiser's log (<c>admin_registro</c>): who changed what, when, to whom.</summary>
public sealed record LogLine(long Id, DateTimeOffset When, string? By, string Action, Guid? Player, string? Target, string? Reason,
    JsonElement Detail)
{
    public string WhenText => When.LocalDateTime.ToString("dd/MM HH:mm");

    public string ByText => By ?? "—";

    /// <summary>The action in words, from its code («orden:revivir» → «Orden: revivir»).</summary>
    public string What => AdminLog.Describe(Action);

    public string Summary => AdminLog.Summarise(this);

    /// <summary>A rules change keeps the file's previous content, so it can be put back.</summary>
    public bool CanRevert => Action is "reglas:cambiar" or "reglas:quitar" && Detail.ValueKind == JsonValueKind.Object
                             && Detail.TryGetProperty("antes", out var before) && before.ValueKind == JsonValueKind.String;
}

/// <summary>An internal note of the organiser on a player (<c>admin_notas</c>); players never see them.</summary>
public sealed record NoteLine(long Id, string DiscordId, string Text, string? By, DateTimeOffset When)
{
    public string WhenText => When.LocalDateTime.ToString("dd/MM/yy HH:mm");
}

/// <summary>A file of a player in the tournament's Storage: a copy of their run (§198) or a crash report.</summary>
/// <param name="Path">Its full name in the bucket, «&lt;id del jugador&gt;/&lt;fecha&gt;.zip».</param>
public sealed record StoredFile(string Bucket, string Path, DateTimeOffset Created, long Bytes)
{
    public string When => Created.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

    public string Size => Bytes < 1024 * 1024 ? $"{Bytes / 1024.0:0} KB" : $"{Bytes / 1024.0 / 1024.0:0.0} MB";

    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed record SentGift(AdminGift Gift, IReadOnlyList<string> Collected, IReadOnlyList<string> Waiting)
{
    public string State => Waiting.Count == 0
        ? "Recogido"
        : Collected.Count == 0
            ? "Sin recoger"
            : $"Recogido por {Collected.Count} de {Collected.Count + Waiting.Count}";

    public string Detail => Waiting.Count == 0 ? string.Empty : $"Falta: {string.Join(", ", Waiting)}";
}

/// <summary>
/// The organiser's side of the gifts (§129), through the tournament server instead of the shared folder.
/// </summary>
/// <remarks>
/// A gift is a row of <c>regalos</c> (<c>tools/supabase/08-regalos.sql</c>): only an organiser can write or withdraw
/// one, and each player's application reads only its own and those for everybody. Whether a player has collected it
/// comes from their uploaded history (<c>AdminGiftClaimed</c>), never from a flag the player could set.
/// </remarks>
public sealed class GiftDesk(ITournamentServer discord, ILogger<GiftDesk> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record PlayerRow(Guid User_id, string? Jugador, int? Puntos, int? Vivos, int? Caidos, int? Estado);

    private sealed record GiftRow(AdminGift Regalo);

    private sealed record HistoryRow(Guid User_id, Guid Run_id, RunHistory? History);

    /// <summary>The bucket of the copies (16-copias.sql); the same name the player's ServerBackupService uploads to.</summary>
    public const string CopiesBucket = "copias";

    private sealed record ListedFile(string Name, string? Id, DateTimeOffset? Created_at, JsonElement? Metadata);

    /// <summary>
    /// A player's files in a Storage bucket, newest first. Empty when the bucket is not on the server yet (its SQL has not
    /// been run): Storage answers that with an error, and nothing else here depends on it.
    /// </summary>
    public async Task<IReadOnlyList<StoredFile>> FilesAsync(string bucket, Guid player)
    {
        string? json;
        try
        {
            json = await discord.ListAsync(bucket, player.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo listar {Bucket} de {Player}; ¿falta su SQL?", bucket, player);
            return [];
        }

        return [.. (JsonSerializer.Deserialize<List<ListedFile>>(json ?? "[]", Json) ?? [])
            .Where(file => file.Id is not null) // las carpetas vienen sin id
            .Select(file => new StoredFile(bucket, $"{player}/{file.Name}", file.Created_at ?? DateTimeOffset.MinValue,
                file.Metadata is { ValueKind: JsonValueKind.Object } meta && meta.TryGetProperty("size", out var size)
                    && size.TryGetInt64(out var bytes) ? bytes : 0))
            .OrderByDescending(file => file.Created)];
    }

    /// <summary>Downloads a file of Storage; null without a session.</summary>
    public Task<byte[]?> DownloadAsync(StoredFile file) => discord.DownloadAsync(file.Bucket, file.Path);

    private sealed record AdminPlayerRow(string Discord_id, bool En_lista, bool Suspendido, Guid? User_id, string? Nombre,
        string? Avatar_url, int? Estado, DateTimeOffset? Latido, string? Version, Guid? Run_id, DateTimeOffset? Subida,
        int? Puntos, int? Vivos, int? Caidos, int? Capturados, int? Etapas, int? Nivel_maximo, int? Logros, int? Logros_total,
        string? Rol, double? Horas, int? Eventos, int? Archivadas, int? Notas, int? Retrocesos);

    /// <summary>
    /// False when the server answered without the control centre's functions (21-admin.sql not run yet): the list comes
    /// from the leaderboard, and the log and notes are not there. Admin says so instead of failing.
    /// </summary>
    public bool ControlCentre { get; private set; } = true;

    /// <summary>
    /// Everybody: on the list (signed in or not) and with a run (even off the list), with their run and presence. Falls back
    /// to the leaderboard, which only has players with an active run, on a server without 21-admin.sql.
    /// </summary>
    public async Task<IReadOnlyList<PlayerLine>> PlayersAsync()
    {
        string? json;
        try
        {
            json = await discord.CallAsync("admin_jugadores");
            ControlCentre = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("El servidor no tiene admin_jugadores (falta 21-admin.sql): lista sacada de la clasificación");
            ControlCentre = false;
            return await LeaderboardAsync();
        }

        if (json is null) throw new InvalidOperationException("Entra con Discord.");

        return [.. (JsonSerializer.Deserialize<List<AdminPlayerRow>>(json, Json) ?? [])
            .Select(p => new PlayerLine(p.User_id ?? Guid.Empty, p.Nombre ?? p.Discord_id, p.Puntos ?? 0, p.Vivos ?? 0, p.Caidos ?? 0,
                (PresenceState)(p.Estado ?? 0))
            {
                DiscordId = p.Discord_id,
                InList = p.En_lista,
                Suspended = p.Suspendido,
                AvatarUrl = p.Avatar_url,
                Version = p.Version,
                LastSeen = p.Latido,
                LastUpload = p.Subida,
                HasRun = p.Run_id is not null,
                Stages = p.Etapas,
                LevelCap = p.Nivel_maximo,
                Captured = p.Capturados,
                Achievements = p.Logros,
                AchievementsTotal = p.Logros_total,
                Role = p.Rol,
                Hours = p.Horas,
                Events = p.Eventos,
                Archived = p.Archivadas ?? 0,
                Notes = p.Notas ?? 0,
                Rewinds = p.Retrocesos ?? 0
            })
            .OrderByDescending(line => line.State)
            .ThenByDescending(line => line.Points)
            .ThenBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private async Task<IReadOnlyList<PlayerLine>> LeaderboardAsync()
    {
        var json = await discord.GetAsync("clasificacion?select=user_id,jugador,puntos,vivos,caidos,estado")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        return [.. (JsonSerializer.Deserialize<List<PlayerRow>>(json, Json) ?? [])
            .Select(p => new PlayerLine(p.User_id, p.Jugador ?? "Jugador", p.Puntos ?? 0, p.Vivos ?? 0, p.Caidos ?? 0,
                (PresenceState)(p.Estado ?? 0)))
            .OrderByDescending(line => line.State)
            .ThenBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public Task SendAsync(AdminGift gift) => SendManyAsync([gift]);

    /// <summary>
    /// Sends gifts, adjustments or orders, one per player, all at once: on a server with 21-admin.sql they go in one
    /// transaction (all or none) and a repeat of the same ones adds nothing. Returns how many were new.
    /// </summary>
    /// <remarks>
    /// Without 21-admin.sql they go one by one as before; each keeps its id, so sending the same list again after a cut
    /// only adds the missing ones (the server refuses a repeated id).
    /// </remarks>
    public async Task<int> SendManyAsync(IReadOnlyList<AdminGift> gifts)
    {
        ArgumentNullException.ThrowIfNull(gifts);
        if (gifts.Count == 0) return 0;

        var rows = gifts.Select(gift => new { id = gift.Id, para = gift.To, regalo = gift }).ToList();
        int added;

        try
        {
            var answer = await discord.CallAsync("mandar_regalos", JsonSerializer.Serialize(new { p_regalos = rows }, Json))
                         ?? throw new InvalidOperationException("Entra con Discord.");
            added = int.Parse(answer, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            added = 0;
            foreach (var row in rows)
            {
                try
                {
                    if (!await discord.PostAsync("regalos", JsonSerializer.Serialize(row, Json))) throw new InvalidOperationException("Entra con Discord.");
                    added++;
                }
                catch (HttpRequestException conflict) when (conflict.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    // Ya estaba: un reintento.
                }
            }
        }

        foreach (var gift in gifts) logger.LogInformation("Regalo enviado a {To}: {What}", gift.To, gift.Say());
        return added;
    }

    // ============================================================ REGISTRO Y NOTAS (21-admin.sql)

    private sealed record LogRow(long Id, DateTimeOffset Cuando, string? Por_nombre, string Accion, Guid? Jugador, string? Objetivo,
        string? Motivo, JsonElement Detalle);

    /// <summary>The organiser's log, newest first: everything, or what touched one player.</summary>
    public async Task<IReadOnlyList<LogLine>> LogAsync(Guid? player = null, int limit = 500)
    {
        var filter = player is { } id ? $"&jugador=eq.{id}" : string.Empty;
        var json = await discord.GetAsync(
                       $"admin_registro?select=id,cuando,por_nombre,accion,jugador,objetivo,motivo,detalle&order=id.desc&limit={limit}{filter}")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        return [.. (JsonSerializer.Deserialize<List<LogRow>>(json, Json) ?? [])
            .Select(r => new LogLine(r.Id, r.Cuando, r.Por_nombre, r.Accion, r.Jugador, r.Objetivo, r.Motivo, r.Detalle))];
    }

    private sealed record NoteRow(long Id, string Discord_id, string Texto, string? Por_nombre, DateTimeOffset Creado);

    public async Task<IReadOnlyList<NoteLine>> NotesAsync(string discordId)
    {
        var json = await discord.GetAsync(
                       $"admin_notas?select=id,discord_id,texto,por_nombre,creado&discord_id=eq.{Uri.EscapeDataString(discordId)}&order=creado.desc")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        return [.. (JsonSerializer.Deserialize<List<NoteRow>>(json, Json) ?? [])
            .Select(r => new NoteLine(r.Id, r.Discord_id, r.Texto, r.Por_nombre, r.Creado))];
    }

    public async Task AddNoteAsync(string discordId, string text, string by)
    {
        if (!await discord.PostAsync("admin_notas", JsonSerializer.Serialize(new { discord_id = discordId, texto = text.Trim(), por_nombre = by })))
        {
            throw new InvalidOperationException("Entra con Discord.");
        }
    }

    public Task DeleteNoteAsync(long id) => discord.DeleteAsync($"admin_notas?id=eq.{id}");

    // ============================================================ LA CUENTA

    /// <summary>Suspends (or lets back in) a player of the list: they stay on it with everything theirs.</summary>
    public Task SetSuspendedAsync(string discordId, bool suspended) =>
        discord.PatchAsync($"whitelist?discord_id=eq.{Uri.EscapeDataString(discordId)}", JsonSerializer.Serialize(new { suspendido = suspended }));

    /// <summary>Archives their active run so they start from zero; nothing is deleted and AUDITORÍA can reactivate it.</summary>
    public Task ResetRunAsync(Guid player) => discord.CallAsync("reiniciar_run", JsonSerializer.Serialize(new { jugador = player }));

    /// <summary>Puts a rules file back as it was before a change in the log.</summary>
    public async Task RestoreRulesAsync(string file, string content)
    {
        if (!await discord.PostAsync("reglas?on_conflict=fichero",
                JsonSerializer.Serialize(new { fichero = file, contenido = content, actualizado = DateTimeOffset.UtcNow }), "resolution=merge-duplicates"))
        {
            throw new InvalidOperationException("Entra con Discord.");
        }
    }

    // ============================================================ LO MANDADO A UN JUGADOR

    /// <summary>
    /// What was sent to one player (gifts, adjustments, orders), newest first, each with whether their app closed it and
    /// what it answered. That comes from their uploaded history (<c>AdminGiftClaimed</c>), never from a flag they set.
    /// </summary>
    public async Task<IReadOnlyList<OrderLine>> OrdersOfAsync(Guid player, IEnumerable<GameEvent> history, DateTimeOffset? runStart = null, bool hasRun = true)
    {
        var json = await discord.GetAsync($"regalos?select=regalo&para=in.(todos,{player})&order=creado.desc&limit=300")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        var closed = history.Where(e => e.Type == GameEventType.AdminGiftClaimed && e.Data.ContainsKey("regalo"))
            .GroupBy(e => e.Data["regalo"])
            .ToDictionary(g => g.Key, g => g.First());

        return [.. (JsonSerializer.Deserialize<List<GiftRow>>(json, Json) ?? []).Select(row => row.Regalo)
            .Select(gift => OrderLine.From(gift, closed.GetValueOrDefault(gift.Id.ToString()), runStart, hasRun))];
    }

    /// <summary>Sends an order to one player's application (2026-09-26): it applies it on its own and leaves its event.</summary>
    public Task SendOrderAsync(Guid player, string from, string reason, AdminOrder order) =>
        SendAsync(new AdminGift
        {
            Schema = AdminGift.OrderSchema,
            Id = Guid.NewGuid(),
            From = from,
            To = player.ToString(),
            Reason = reason,
            CreatedAt = DateTimeOffset.Now,
            Order = order
        });

    private sealed record RunRow(Guid Run_id, RunSnapshot Snapshot, RunHistory? History, DateTimeOffset Subida);

    /// <summary>A player's active run as they last uploaded it, or null when they have none.</summary>
    public async Task<(RunSnapshot Snapshot, RunHistory? History, DateTimeOffset Uploaded)?> RunOfAsync(Guid player)
    {
        var json = await discord.GetAsync($"runs?select=run_id,snapshot,history,subida&user_id=eq.{player}&activa=eq.true")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        return (JsonSerializer.Deserialize<List<RunRow>>(json, Json) ?? []).FirstOrDefault() is { } row
            ? (row.Snapshot, await ServerHistory.CompleteAsync(discord, row.History, row.Snapshot, Json), row.Subida)
            : null;
    }

    public async Task WithdrawAsync(Guid id)
    {
        await discord.DeleteAsync($"regalos?id=eq.{id}");
        logger.LogInformation("Regalo {Id} retirado", id);
    }

    public async Task<IReadOnlyList<SentGift>> SentAsync(IReadOnlyList<PlayerLine> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        var giftsJson = await discord.GetAsync("regalos?select=regalo&order=creado.desc");
        var historiesJson = await discord.GetAsync("runs?select=user_id,run_id,history&activa=eq.true");

        if (giftsJson is null || historiesJson is null)
        {
            return [];
        }

        // Las runs que suben evento a evento tienen sus recogidas en la tabla eventos (§194).
        var claims = await ServerHistory.ClaimsAsync(discord, Json);
        var collected = (JsonSerializer.Deserialize<List<HistoryRow>>(historiesJson, Json) ?? [])
            .ToDictionary(row => row.User_id, row => (row.History?.Events ?? []).Concat(claims[row.Run_id])
                .Where(e => e.Type == GameEventType.AdminGiftClaimed)
                .Select(e => e.Data.TryGetValue("regalo", out var id) && Guid.TryParse(id, out var gift) ? gift : Guid.Empty)
                .ToHashSet());

        var sent = new List<SentGift>();

        foreach (var gift in (JsonSerializer.Deserialize<List<GiftRow>>(giftsJson, Json) ?? []).Select(row => row.Regalo))
        {
            var addressed = players.Where(player => gift.IsFor(player.Id)).ToList();

            sent.Add(new SentGift(gift,
                [.. addressed.Where(p => collected.GetValueOrDefault(p.Id)?.Contains(gift.Id) == true).Select(p => p.Name)],
                [.. addressed.Where(p => collected.GetValueOrDefault(p.Id)?.Contains(gift.Id) != true).Select(p => p.Name)]));
        }

        return sent;
    }
}
