using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Admin.Services;

/// <param name="Name">The player, as they call themselves.</param>
/// <param name="Points">What their last published snapshot says.</param>
/// <param name="PublishedAt">When they last published, or null if never.</param>
/// <param name="Verdict">Whether their numbers add up against their own published history.</param>
public sealed record PlayerLine(
    Guid Id, string Name, int Points, int Alive, int Dead, DateTimeOffset? PublishedAt, string Verdict)
{
    public string Folder { get; init; } = string.Empty;

    public string Ago => PublishedAt is { } at ? Playtime.Ago(at, DateTimeOffset.Now) : "nunca";
}

/// <param name="Gift">What was sent.</param>
/// <param name="Collected">Who has collected it, by name.</param>
/// <param name="Waiting">Who has not, by name.</param>
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
/// The admin's side of the shared folder: who is in it, and the gifts left for them (§129).
/// </summary>
/// <remarks>
/// Reads and writes files and nothing else. Whether a gift was collected is <b>not</b> stored here: it is read from
/// each player's own published history, which is the only place that can say it — their application is what applies a
/// gift, and it says so in the run's own chain. A player who collected something and has not published yet reads as
/// waiting, and that is the truth from here.
/// </remarks>
public sealed class GiftDesk(SnapshotStore snapshots, GiftStore gifts, ILogger<GiftDesk> logger)
{
    public IReadOnlyList<PlayerLine> Players(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var lines = new List<PlayerLine>();

        foreach (var player in snapshots.ReadPlayers(root))
        {
            var snapshot = player.Snapshot;
            var history = snapshots.ReadHistory(player.HistoryPath);

            var verdict = snapshot is null
                ? "Sin publicar"
                : history is null
                    ? "Sin historial"
                    : Core.Services.SnapshotAudit.Check(snapshot, history).Verdict switch
                    {
                        Core.Services.AuditVerdict.Consistent => "Cuadra",
                        Core.Services.AuditVerdict.Rewound => "Ha retrocedido",
                        Core.Services.AuditVerdict.NoHistory => "Sin historial",
                        _ => "NO CUADRA"
                    };

            lines.Add(new PlayerLine(player.Profile.Id, player.Profile.Name,
                snapshot?.Points ?? 0, snapshot?.Alive ?? 0, snapshot?.Dead ?? 0,
                snapshot?.PublishedAt, verdict)
            {
                Folder = player.Folder
            });
        }

        return [.. lines.OrderBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Writes a gift into the folder.</summary>
    public void Send(string root, AdminGift gift)
    {
        gifts.Write(root, gift);
        logger.LogInformation("Regalo enviado a {To}: {What}", gift.To, gift.Say());
    }

    public bool Withdraw(string root, Guid id) => gifts.Remove(root, id);

    /// <summary>Every gift sent, with who has collected it according to what each player has published.</summary>
    public IReadOnlyList<SentGift> Sent(string root, IReadOnlyList<PlayerLine> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        // Un historial por jugador, leído una vez: son el fichero grande de la carpeta.
        var collected = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var player in snapshots.ReadPlayers(root))
        {
            var history = snapshots.ReadHistory(player.HistoryPath);

            collected[player.Profile.Id] = history is null
                ? []
                : [.. history.Events
                    .Where(e => e.Type == GameEventType.AdminGiftClaimed)
                    .Select(e => e.Data.TryGetValue("regalo", out var id) && Guid.TryParse(id, out var gift)
                        ? gift
                        : Guid.Empty)
                    .Where(id => id != Guid.Empty)];
        }

        var sent = new List<SentGift>();

        foreach (var gift in gifts.ReadAll(root))
        {
            var addressed = players.Where(player => gift.IsFor(player.Id)).ToList();

            sent.Add(new SentGift(gift,
                [.. addressed.Where(p => collected.GetValueOrDefault(p.Id)?.Contains(gift.Id) == true).Select(p => p.Name)],
                [.. addressed.Where(p => collected.GetValueOrDefault(p.Id)?.Contains(gift.Id) != true).Select(p => p.Name)]));
        }

        return sent;
    }
}
