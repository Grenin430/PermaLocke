using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Services;

/// <summary>A player of the tournament, as the organiser's list shows them.</summary>
/// <param name="Id">Their account on the tournament server: what a gift is addressed to.</param>
public sealed record PlayerLine(Guid Id, string Name, int Points, int Alive, int Dead, PresenceState State = PresenceState.Offline)
{
    public string Presence => State switch
    {
        PresenceState.Playing => "JUGANDO",
        PresenceState.InApp => "EN LA APP",
        _ => "DESCONECTADO"
    };
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
public sealed class GiftDesk(DiscordLogin discord, ILogger<GiftDesk> logger)
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

    public async Task<IReadOnlyList<PlayerLine>> PlayersAsync()
    {
        var json = await discord.GetAsync("clasificacion?select=user_id,jugador,puntos,vivos,caidos,estado")
                   ?? throw new InvalidOperationException("Entra con Discord.");

        return [.. (JsonSerializer.Deserialize<List<PlayerRow>>(json, Json) ?? [])
            .Select(p => new PlayerLine(p.User_id, p.Jugador ?? "Jugador", p.Puntos ?? 0, p.Vivos ?? 0, p.Caidos ?? 0,
                (PresenceState)(p.Estado ?? 0)))
            .OrderByDescending(line => line.State)
            .ThenBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task SendAsync(AdminGift gift)
    {
        await discord.PostAsync("regalos", JsonSerializer.Serialize(new { id = gift.Id, para = gift.To, regalo = gift }, Json));
        logger.LogInformation("Regalo enviado a {To}: {What}", gift.To, gift.Say());
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
