using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PermaLocke.GameLink;

/// <summary>
/// What the first-encounter rule has taken out of the bag and owes back, and <b>which run</b> it owes it to.
/// </summary>
/// <remarks>
/// <para>
/// Kept on disk and written before anything is touched, so a crash while the balls are away does not lose
/// them. That is also what made it dangerous: the file outlived the run. «Empezar de cero» deletes the run
/// and the save, and this file stayed; the new game connected on Route 1, the rule said «give back», and
/// the player found twelve kinds of ball from the previous game in a brand new bag — a Master Ball among
/// them — and the automatic reward that waits for the first Poké Ball fired on one of those (§147).
/// </para>
/// <para>
/// So the file now starts with the run it belongs to, <c>run=&lt;id&gt;</c>, and answers only that run.
/// A file of another run, or one written before this line existed, owes nothing to anybody asking now.
/// It is not deleted either: the first time the current run writes, it is moved aside with the date, so
/// if it turns out to have been owed after all the numbers are still there.
/// </para>
/// </remarks>
public sealed class WithheldLedger(string path, ILogger logger)
{
    private const string RunPrefix = "run=";
    private const string StampPrefix = "apuntado=";

    /// <summary>How much of <paramref name="itemId"/> <paramref name="run"/> is owed. Zero for anybody else's.</summary>
    public int Owed(Guid run, int itemId)
    {
        var (owner, amounts) = Read();

        return owner == run ? amounts.GetValueOrDefault(itemId) : 0;
    }

    /// <summary>Sets what <paramref name="run"/> is owed of one item; zero forgets it.</summary>
    public void Remember(Guid run, int itemId, int amount)
    {
        var (owner, amounts) = Read();

        if (owner != run)
        {
            if (amounts.Count > 0)
            {
                SetAside(owner);
            }

            amounts.Clear();
        }

        if (amount > 0)
        {
            amounts[itemId] = amount;
        }
        else
        {
            amounts.Remove(itemId);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path,
        [
            $"{RunPrefix}{run:D}",
            $"{StampPrefix}{DateTimeOffset.UtcNow:O}",
            .. amounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"),
        ]);
    }

    /// <summary>
    /// When this run's debt was last written down, or null when the file is somebody else's or has no stamp.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is in the file and not the file's own date because a folder that gets copied, restored or synchronised
    /// comes back with new dates — the same reason the run's backups are rotated by name and not by date (§76).
    /// </para>
    /// <para>
    /// What it is for: telling a debt that reached the saved game from one that did not. Balls are taken out of
    /// the <b>live</b> bag, so a withholding written after the last save is one the player can undo by reloading,
    /// and the debt that goes with it is to nobody.
    /// </para>
    /// </remarks>
    public DateTimeOffset? WrittenAt(Guid run)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var (owner, _) = Read();

        if (owner != run)
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith(StampPrefix, StringComparison.Ordinal)
                && DateTimeOffset.TryParse(line[StampPrefix.Length..], CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var stamp))
            {
                return stamp;
            }
        }

        return null;
    }

    /// <summary>The run the file belongs to — null when it predates this line — and what it owes.</summary>
    private (Guid? Owner, Dictionary<int, int> Amounts) Read()
    {
        var amounts = new Dictionary<int, int>();
        Guid? owner = null;

        if (!File.Exists(path))
        {
            return (owner, amounts);
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith(RunPrefix, StringComparison.Ordinal))
            {
                owner = Guid.TryParse(line[RunPrefix.Length..], out var id) ? id : null;
                continue;
            }

            var parts = line.Split('=');

            if (parts.Length == 2 && int.TryParse(parts[0], out var item) && int.TryParse(parts[1], out var count)
                && count > 0)
            {
                amounts[item] = count;
            }
        }

        return (owner, amounts);
    }

    /// <summary>Moves another run's ledger aside with the date, instead of paying it or throwing it away.</summary>
    private void SetAside(Guid? owner)
    {
        var aside = Path.Combine(Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}-de-otra-run-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(path)}");

        File.Move(path, aside, overwrite: true);
        logger.LogWarning(
            "Las Poké Balls retiradas eran de otra run ({Owner}): no se devuelven a esta. Apartadas en {Aside}",
            owner?.ToString() ?? "sin anotar", aside);
    }
}
