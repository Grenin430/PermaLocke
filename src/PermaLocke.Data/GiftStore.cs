using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>
/// The admin's gifts inside the shared folder: one file each, written by the admin and read by everybody (§129).
/// </summary>
/// <remarks>
/// <para>
/// One file per gift rather than one list for all of them. Two programs write in this folder at the same time —
/// Drive being the third — and a single list is a file two of them would overwrite each other's copy of; separate
/// files cannot collide, and one that arrives half-synchronised is one gift that does not load instead of all of them.
/// </para>
/// <para>
/// Nothing here says whether a gift was collected. That lives in the player's own history, published with the rest,
/// which is the only place that can say it truthfully: this folder is the postbox, not the ledger.
/// </para>
/// </remarks>
public sealed class GiftStore(ILogger<GiftStore>? logger = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Where the gifts live inside the shared folder.</summary>
    public static string Folder(string root) =>
        Path.Combine(root, CompetitionLayout.AdminFolder, CompetitionLayout.GiftsFolder);

    /// <summary>Writes one gift, and returns the file it wrote.</summary>
    public string Write(string root, AdminGift gift)
    {
        ArgumentNullException.ThrowIfNull(gift);

        var folder = Folder(root);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"{gift.Id:N}.json");
        var temporary = path + ".tmp";

        // A un temporal y luego se mueve: la carpeta la sincroniza otro programa, y un fichero a medio escribir
        // que Drive suba es un regalo que el otro no puede leer.
        File.WriteAllText(temporary, JsonSerializer.Serialize(gift, Options));
        File.Move(temporary, path, overwrite: true);

        logger?.LogInformation("Regalo {Id} escrito para {To}", gift.Id, gift.To);
        return path;
    }

    /// <summary>Every gift in the folder, newest first. One that cannot be read is skipped and said in the log.</summary>
    public IReadOnlyList<AdminGift> ReadAll(string root)
    {
        var folder = Folder(root);

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(folder))
        {
            return [];
        }

        var gifts = new List<AdminGift>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var gift = JsonSerializer.Deserialize<AdminGift>(File.ReadAllText(file), Options);

                // Un formato más nuevo no se adivina: lo escribió una versión posterior y se deja estar.
                if (gift is not null && gift.Id != Guid.Empty && gift.Schema <= AdminGift.CurrentSchema)
                {
                    gifts.Add(gift);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "No se ha podido leer el regalo {File}", file);
            }
        }

        return [.. gifts.OrderByDescending(gift => gift.CreatedAt)];
    }

    /// <summary>Removes a gift from the folder, for an admin who sent one by mistake.</summary>
    /// <remarks>
    /// Only stops what nobody has collected yet: a gift already collected lives in that player's history and no
    /// deletion here reaches it.
    /// </remarks>
    public bool Remove(string root, Guid id)
    {
        var path = Path.Combine(Folder(root), $"{id:N}.json");

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        logger?.LogInformation("Regalo {Id} retirado", id);
        return true;
    }
}
