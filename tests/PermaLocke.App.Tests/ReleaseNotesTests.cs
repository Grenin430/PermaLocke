using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

/// <summary>The Steam-style NOVEDADES of JUGAR read <c>Novedades.txt</c>; a misread line would put notes under the wrong version.</summary>
public sealed class ReleaseNotesTests
{
    [Fact]
    public void Versions_dates_headings_and_lines_come_out_in_order()
    {
        var notes = ReleaseNotes.Parse("1.0.9 2026-10-06\nREGLAS\nUna.\nDos.\n\n1.0.7.9\nARREGLOS\nTres.\n");

        Assert.Equal(["1.0.9", "1.0.7.9"], notes.Select(n => n.Version));
        Assert.Equal(new DateOnly(2026, 10, 6), notes[0].Date);
        Assert.Null(notes[1].Date);
        Assert.Equal("REGLAS", notes[0].Title);
        Assert.Equal(["Una.", "Dos."], notes[0].Sections[0].Lines);
        Assert.Equal("Una. Dos.", notes[0].Summary);
        Assert.Equal("ACTUALIZACIÓN", notes[0].Kind);
        Assert.Equal("PARCHE", notes[1].Kind);
    }

    [Fact]
    public void The_age_reads_like_steam()
    {
        var note = new ReleaseNote("1.0.9", new DateOnly(2026, 10, 6), []);

        Assert.Equal("HOY", note.Ago(new DateOnly(2026, 10, 6)));
        Assert.Equal("AYER", note.Ago(new DateOnly(2026, 10, 7)));
        Assert.Equal("HACE 5 DÍAS", note.Ago(new DateOnly(2026, 10, 11)));
    }

    [Fact]
    public void The_embedded_file_reads_and_every_version_has_a_date_and_notes()
    {
        var notes = ReleaseNotes.Embedded();

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.NotNull(n.Date));
        Assert.All(notes, n => Assert.NotEmpty(n.Sections.SelectMany(s => s.Lines)));
    }
}
