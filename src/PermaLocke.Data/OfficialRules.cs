using System.Security.Cryptography;

namespace PermaLocke.Data;

/// <summary>When a rules file takes effect once it is adopted.</summary>
public enum RuleFileKind
{
    /// <summary>Read when PermaLocke starts: prices, achievements, penalties, rewards...</summary>
    OnRestart,

    /// <summary>Baked into the generated mod: it only reaches the game after generating and installing again.</summary>
    NeedsRegeneration,
}

public enum RuleFileState
{
    /// <summary>This machine already has exactly the official file.</summary>
    Same,

    /// <summary>This machine has a different version.</summary>
    Different,

    /// <summary>This machine does not have the file at all.</summary>
    New,

    /// <summary>The official file does not load, so it is never adopted.</summary>
    Unreadable,
}

/// <param name="Name">File name, as it sits in both folders.</param>
/// <param name="Detail">What the difference is, or why it cannot be adopted.</param>
public sealed record RuleFileStatus(string Name, RuleFileKind Kind, RuleFileState State, string Detail = "")
{
    public bool CanAdopt => State is RuleFileState.Different or RuleFileState.New;
}

/// <param name="Name">File adopted.</param>
/// <param name="Before">SHA-256 of this machine's file before, or empty when there was none.</param>
/// <param name="After">SHA-256 of the file now, read back from disk.</param>
public sealed record AdoptedRule(string Name, RuleFileKind Kind, string Before, string After);

/// <param name="Adopted">Files that were copied and read back identical.</param>
/// <param name="Backup">Where this machine's previous files were copied, or null when none were replaced.</param>
/// <param name="Problem">Why nothing was adopted, or empty.</param>
public sealed record RulesAdoption(IReadOnlyList<AdoptedRule> Adopted, string? Backup, string Problem = "");

/// <summary>
/// The competition's official rules: the files the admin leaves in <c>reglas/</c>, compared with
/// this machine's <c>Data/</c> and adopted from there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only a fixed list of files</b>, each with what adopting it means. Everything else in
/// <c>Data/</c> is either read off the player's own cartridge (<c>species.json</c>, <c>mapas.json</c>)
/// or belongs to the application's own layout, and overwriting it from a shared folder would be
/// somebody else's cartridge or somebody else's screen.
/// </para>
/// <para>
/// <b>Nothing that does not load is adopted.</b> Every official file goes through the same loader
/// the application uses at start-up, supplied by the caller, before it is offered. A rules file with
/// a typo in it would otherwise be copied in and stop PermaLocke from starting on five machines at
/// once.
/// </para>
/// <para>
/// And the files it replaces are copied aside first, with the rule the save writers follow: without
/// a copy, nothing is overwritten (§67, §123).
/// </para>
/// </remarks>
public static class OfficialRules
{
    public static readonly IReadOnlyDictionary<string, RuleFileKind> Files =
        new Dictionary<string, RuleFileKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["achievements.json"] = RuleFileKind.OnRestart,
            ["gacha.json"] = RuleFileKind.OnRestart,
            ["grants.json"] = RuleFileKind.OnRestart,
            ["levelcaps.json"] = RuleFileKind.OnRestart,
            ["penalties.json"] = RuleFileKind.OnRestart,
            ["rewards.json"] = RuleFileKind.OnRestart,
            ["roulette.json"] = RuleFileKind.OnRestart,
            ["rules.json"] = RuleFileKind.OnRestart,
            ["shop.json"] = RuleFileKind.OnRestart,
            ["wondertrade.json"] = RuleFileKind.OnRestart,

            // Los dos que se cuecen en el mod. roles.json lleva tambien los multiplicadores de
            // puntos, que si se leen al arrancar, pero los niveles y el Pokemon extra van dentro del
            // mundo generado: se cuenta como lo mas exigente de lo que lleva.
            ["randomizer.json"] = RuleFileKind.NeedsRegeneration,
            ["roles.json"] = RuleFileKind.NeedsRegeneration,
        };

    /// <summary>
    /// Compares each official file with this machine's copy.
    /// </summary>
    /// <param name="validate">Loads a file the way the application would; returns why it failed, or null.</param>
    public static IReadOnlyList<RuleFileStatus> Compare(string rulesFolder, string dataFolder,
        Func<string, string, string?> validate)
    {
        if (!Directory.Exists(rulesFolder))
        {
            return [];
        }

        var statuses = new List<RuleFileStatus>();

        foreach (var official in Directory.EnumerateFiles(rulesFolder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(official);

            if (!Files.TryGetValue(name, out var kind))
            {
                continue;
            }

            if (validate(name, official) is { } problem)
            {
                statuses.Add(new RuleFileStatus(name, kind, RuleFileState.Unreadable, problem));
                continue;
            }

            var local = Path.Combine(dataFolder, name);

            statuses.Add(!File.Exists(local)
                ? new RuleFileStatus(name, kind, RuleFileState.New, "No lo tienes.")
                : Hash(local) == Hash(official)
                    ? new RuleFileStatus(name, kind, RuleFileState.Same)
                    : new RuleFileStatus(name, kind, RuleFileState.Different, "Tu versión es distinta."));
        }

        return statuses;
    }

    /// <summary>
    /// Copies every adoptable official file over this machine's, after copying the old ones aside.
    /// </summary>
    /// <remarks>
    /// All or nothing at the start: the comparison is done again here, and if any file that was going
    /// to be adopted has since become unreadable, nothing is touched. A half-adopted set of rules —
    /// the new achievements with the old rewards — is worse than either set whole.
    /// </remarks>
    public static RulesAdoption Adopt(string rulesFolder, string dataFolder, string backupRoot,
        Func<string, string, string?> validate, DateTimeOffset now)
    {
        var statuses = Compare(rulesFolder, dataFolder, validate);

        if (statuses.FirstOrDefault(s => s.State == RuleFileState.Unreadable) is { } broken)
        {
            return new RulesAdoption([], null,
                $"{broken.Name} no se puede cargar ({broken.Detail}). No se ha adoptado nada.");
        }

        var toAdopt = statuses.Where(s => s.CanAdopt).ToList();

        if (toAdopt.Count == 0)
        {
            return new RulesAdoption([], null, "Ya tienes las reglas oficiales.");
        }

        string? backup = null;
        var replaced = toAdopt.Where(s => File.Exists(Path.Combine(dataFolder, s.Name))).ToList();

        if (replaced.Count > 0)
        {
            backup = Path.Combine(backupRoot, $"reglas-{now.LocalDateTime:yyyyMMdd-HHmmss}");

            for (var n = 2; Directory.Exists(backup); n++)
            {
                backup = Path.Combine(backupRoot, $"reglas-{now.LocalDateTime:yyyyMMdd-HHmmss}-{n}");
            }

            Directory.CreateDirectory(backup);

            foreach (var status in replaced)
            {
                var source = Path.Combine(dataFolder, status.Name);
                var copy = Path.Combine(backup, status.Name);
                File.Copy(source, copy);

                if (Hash(copy) != Hash(source))
                {
                    return new RulesAdoption([], backup,
                        $"La copia de {status.Name} no se lee igual que el original. No se ha adoptado nada.");
                }
            }
        }

        var adopted = new List<AdoptedRule>();

        foreach (var status in toAdopt)
        {
            var target = Path.Combine(dataFolder, status.Name);
            var before = File.Exists(target) ? Hash(target) : string.Empty;
            var official = Path.Combine(rulesFolder, status.Name);

            File.Copy(official, target, overwrite: true);
            adopted.Add(new AdoptedRule(status.Name, status.Kind, before, Hash(target)));

            if (adopted[^1].After != Hash(official))
            {
                return new RulesAdoption(adopted, backup,
                    $"{status.Name} no se lee igual que el oficial después de copiarlo. Tus ficheros "
                    + $"anteriores están en {backup}.");
            }
        }

        return new RulesAdoption(adopted, backup);
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
