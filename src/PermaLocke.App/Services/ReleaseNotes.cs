using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PermaLocke.App.Services;

/// <summary>One heading of a version's notes and its lines.</summary>
public sealed record ReleaseSection(string Heading, IReadOnlyList<string> Lines);

/// <summary>What one version brought, as <c>Novedades.txt</c> tells it.</summary>
public sealed record ReleaseNote(string Version, DateOnly? Date, IReadOnlyList<ReleaseSection> Sections)
{
    /// <summary>Three numbers is an update; a fourth is a patch on top of one.</summary>
    public bool IsMajor => Version.Count(c => c == '.') < 3;

    /// <summary>The label Steam puts over an event.</summary>
    public string Kind => IsMajor ? "ACTUALIZACIÓN" : "PARCHE";

    public string Title => Sections.FirstOrDefault(s => s.Heading.Length > 0)?.Heading ?? "ARREGLOS";

    /// <summary>The first lines, for a card; the whole note is in <see cref="Sections"/>.</summary>
    public string Summary => string.Join(" ", Sections.SelectMany(s => s.Lines).Take(2));

    public string DateText => Date is { } date ? date.ToString("d 'de' MMMM 'de' yyyy", Spanish) : string.Empty;

    /// <summary>«HOY», «AYER», «HACE 5 DÍAS», or the date once it is more than a month old.</summary>
    public string Ago(DateOnly today) => Date is not { } date ? string.Empty : (today.DayNumber - date.DayNumber) switch
    {
        <= 0 => "HOY",
        1 => "AYER",
        var days and < 31 => $"HACE {days} DÍAS",
        _ => date.ToString("d MMM yyyy", Spanish).ToUpper(Spanish)
    };

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
}

/// <summary>
/// The notes of every version, newest first, read from <c>Novedades.txt</c> (embedded in the exe, so an update brings its
/// own). A line that is a version, optionally followed by its date (<c>1.0.9 2026-10-06</c>), opens a version; a line in
/// capitals opens a heading; anything else belongs to the heading above it.
/// </summary>
public static class ReleaseNotes
{
    private static readonly Regex VersionLine = new(@"^(\d+(?:\.\d+)+)(?:\s+(\d{4}-\d{2}-\d{2}))?$", RegexOptions.Compiled);

    public static IReadOnlyList<ReleaseNote> Parse(string text)
    {
        var notes = new List<ReleaseNote>();
        string? version = null;
        DateOnly? date = null;
        var sections = new List<ReleaseSection>();
        string heading = string.Empty;
        var lines = new List<string>();

        void CloseSection()
        {
            if (lines.Count > 0 || heading.Length > 0) sections.Add(new ReleaseSection(heading, [.. lines]));
            heading = string.Empty;
            lines = [];
        }

        void CloseVersion()
        {
            CloseSection();
            if (version is not null) notes.Add(new ReleaseNote(version, date, [.. sections]));
            sections = [];
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (VersionLine.Match(line) is { Success: true } match)
            {
                CloseVersion();
                version = match.Groups[1].Value;
                date = match.Groups[2].Success ? DateOnly.ParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            }
            else if (version is null)
            {
                continue;
            }
            else if (line == line.ToUpperInvariant() && line.Any(char.IsLetter))
            {
                CloseSection();
                heading = line;
            }
            else
            {
                lines.Add(line);
            }
        }

        CloseVersion();
        return notes;
    }

    /// <summary>The notes that came with this exe.</summary>
    public static IReadOnlyList<ReleaseNote> Embedded()
    {
        using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream("PermaLocke.App.Novedades.txt");
        return stream is null ? [] : Parse(new StreamReader(stream).ReadToEnd());
    }
}
