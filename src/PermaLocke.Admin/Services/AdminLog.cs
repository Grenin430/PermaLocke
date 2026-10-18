using System.Text.Json;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Services;

/// <summary>
/// The words for the organiser's log (<c>admin_registro</c>, 21-admin.sql): the server writes short codes such as
/// «orden:revivir» or «lista:suspender», and these say them as a sentence.
/// </summary>
public static class AdminLog
{
    private static readonly Dictionary<string, string> Orders = new()
    {
        [AdminOrderKinds.Revive] = "revivir",
        [AdminOrderKinds.Kill] = "marcar caído",
        [AdminOrderKinds.RevokeWipe] = "revocar equipo caído",
        [AdminOrderKinds.FreeZone] = "liberar ruta",
        [AdminOrderKinds.SetStages] = "pruebas superadas",
        [AdminOrderKinds.GiveItem] = "dar objeto",
        [AdminOrderKinds.GivePokemon] = "dar Pokémon",
        [AdminOrderKinds.Message] = "mensaje privado",
        [AdminOrderKinds.PlayLock] = "abrir o cerrar el juego"
    };

    public static string Describe(string action)
    {
        var retired = action.StartsWith("retirar:", StringComparison.Ordinal);
        var code = retired ? action["retirar:".Length..] : action;

        var text = code switch
        {
            "regalo" => "Regalo",
            "ajuste" => "Ajuste de puntos",
            _ when code.StartsWith("orden:", StringComparison.Ordinal) =>
                $"Orden: {Orders.GetValueOrDefault(code["orden:".Length..], code["orden:".Length..])}",
            "lista:alta" => "Alta en la lista",
            "lista:baja" => "Baja de la lista",
            "lista:suspender" => "Suspendido",
            "lista:reactivar" => "Reactivado",
            "lista:cambio" => "Cambio en la lista",
            "anuncio:publicar" => "Anuncio publicado",
            "anuncio:retirar" => "Anuncio retirado",
            "reglas:publicar" => "Reglas publicadas",
            "reglas:cambiar" => "Reglas cambiadas",
            "reglas:quitar" => "Reglas quitadas",
            "mote:anular" => "Mote anulado",
            "buzon:borrar" => "Sugerencia borrada",
            "run:reinicio" => "Run reiniciada",
            "run:reactivada" => "Run reactivada",
            "nota:escribir" => "Nota escrita",
            "nota:borrar" => "Nota borrada",
            "servidor:limpieza" => "Limpieza del servidor",
            _ => code
        };

        return retired ? $"Retirado · {text}" : text;
    }

    /// <summary>The one line of detail that matters for each kind, from what the server kept.</summary>
    public static string Summarise(LogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var detail = line.Detail;
        if (detail.ValueKind != JsonValueKind.Object) return line.Target ?? string.Empty;

        string? Text(string key) => detail.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        int? Number(string key) => detail.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

        var code = line.Action.StartsWith("retirar:", StringComparison.Ordinal) ? line.Action["retirar:".Length..] : line.Action;

        return code switch
        {
            _ when code.StartsWith("orden:", StringComparison.Ordinal) => Text("resumen") ?? string.Empty,
            "regalo" or "ajuste" => GiftText(detail, Number("puntos"), Number("wonderTrades")),
            "lista:suspender" or "lista:reactivar" or "lista:alta" or "lista:baja" or "lista:cambio" =>
                $"Discord {line.Target}{Change(detail)}",
            "anuncio:publicar" or "anuncio:retirar" or "nota:escribir" or "nota:borrar" or "buzon:borrar" => Clip(Text("texto")),
            "reglas:publicar" or "reglas:cambiar" or "reglas:quitar" =>
                $"{line.Target} · {Bytes(Number("bytesAntes"))} → {Bytes(Number("bytesDespues"))}",
            "mote:anular" => $"{Text("pokemon")} de {Text("jugadorNombre")}",
            "run:reinicio" or "run:reactivada" => Number("puntos") is { } points ? $"{points} puntos, {Number("eventos")} eventos" : string.Empty,
            "servidor:limpieza" => detail.TryGetProperty("resultado", out var result) && result.TryGetProperty("bytes", out var bytes)
                ? $"{Bytes(bytes.GetInt32())} liberados" : string.Empty,
            _ => line.Target ?? string.Empty
        };
    }

    private static string GiftText(JsonElement detail, int? points, int? trades)
    {
        var parts = new List<string>();
        if (points is { } p) parts.Add($"{p:+#;-#} puntos");
        if (detail.TryGetProperty("tiradas", out var rolls) && rolls.ValueKind == JsonValueKind.Object)
        {
            var total = rolls.EnumerateObject().Sum(r => r.Value.TryGetInt32(out var n) ? n : 0);
            if (total > 0) parts.Add($"{total} tiradas");
        }

        if (trades is { } t) parts.Add($"{t} wonder trades");
        if (detail.TryGetProperty("para", out var to) && to.GetString() == AdminGift.Everybody) parts.Add("a todos");
        return string.Join(" · ", parts);
    }

    private static string Change(JsonElement detail)
    {
        if (!detail.TryGetProperty("antes", out var before) || !detail.TryGetProperty("despues", out var after)) return string.Empty;

        var oldName = before.TryGetProperty("nombre", out var a) ? a.GetString() : null;
        var newName = after.TryGetProperty("nombre", out var b) ? b.GetString() : null;
        return oldName != newName ? $" · {oldName} → {newName}" : string.Empty;
    }

    private static string Clip(string? text) => text is null ? string.Empty : text.Length > 120 ? text[..117] + "..." : text;

    private static string Bytes(int? bytes) => bytes switch
    {
        null => "—",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB"
    };
}

/// <summary>
/// Something sent to one player and what became of it: open until their app closes it with an
/// <see cref="GameEventType.AdminGiftClaimed"/>, whose <c>resultado</c> says what it did or why it could not.
/// </summary>
/// <param name="Earlier">Sent before their active run began (or they have none): its closing would be in a run that is no longer
/// uploaded, so it is not counted as waiting.</param>
public sealed record OrderLine(Guid Id, DateTimeOffset Sent, string Kind, string What, string Reason, string? By, bool Closed,
    DateTimeOffset? ClosedAt, string Result, bool Earlier = false)
{
    public string SentText => Sent.LocalDateTime.ToString("dd/MM HH:mm");

    public string State => Closed ? "CERRADA" : Earlier ? "ANTERIOR A SU RUN" : "PENDIENTE";

    public string ClosedText => ClosedAt?.LocalDateTime.ToString("dd/MM HH:mm") ?? "—";

    /// <summary>Open for more than half an hour: their app has not been opened, or cannot apply it.</summary>
    public bool Stuck(DateTimeOffset now) => !Closed && !Earlier && now - Sent > TimeSpan.FromMinutes(30);

    public static OrderLine From(AdminGift gift, GameEvent? closing, DateTimeOffset? runStart = null, bool hasRun = true)
    {
        ArgumentNullException.ThrowIfNull(gift);

        var kind = gift.Order is { } order ? $"Orden: {AdminLog.Describe("orden:" + order.Kind)["Orden: ".Length..]}"
            : gift.Adjustment ? "Ajuste" : "Regalo";

        return new OrderLine(gift.Id, gift.CreatedAt, kind, gift.Order?.Summary ?? gift.Say(), gift.Reason, gift.From,
            closing is not null, closing?.Timestamp,
            closing is null ? string.Empty : closing.Data.GetValueOrDefault("resultado") ?? "Recogido.",
            closing is null && (!hasRun || (runStart is { } start && gift.CreatedAt < start)));
    }
}
