using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>
/// Where each zone's marker sits on its island, which is the one part of the map nobody can
/// automate: the cartridge does not say where Ruta 3 is on the picture.
/// </summary>
public sealed class ZoneMarkerTests
{
    private static string Scratch([System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        var folder = Path.Combine(Path.GetTempPath(), "permalocke-marcadores", name);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "marcadores.json");
    }

    [Fact]
    public async Task A_marker_survives_being_written_and_read_back()
    {
        var path = Scratch();
        var markers = JsonZoneMarkers.Empty.With("ruta-1", new ZoneMarker("Melemele", 0.25, 0.5));

        await markers.SaveAsync(path);
        var back = JsonZoneMarkers.Load(path);

        var marker = back.For("ruta-1");
        Assert.NotNull(marker);
        Assert.Equal("Melemele", marker!.Island);
        Assert.Equal(0.25, marker.X, 6);
        Assert.Equal(0.5, marker.Y, 6);
    }

    /// <summary>
    /// The names in the file are Spanish, and that has to survive a round trip.
    /// </summary>
    /// <remarks>
    /// Left to the serializer's defaults, «isla» does not match <c>Island</c>: every marker would
    /// load with a null island and be thrown away by the range check, giving an empty map with
    /// nothing in the log and a file that looks perfectly fine when opened.
    /// </remarks>
    [Fact]
    public async Task The_file_is_written_with_Spanish_names()
    {
        var path = Scratch();
        await JsonZoneMarkers.Empty.With("ruta-2", new ZoneMarker("Akala", 0.1, 0.2)).SaveAsync(path);

        var json = await File.ReadAllTextAsync(path);

        Assert.Contains("\"isla\"", json);
        Assert.Contains("\"marcadores\"", json);
        Assert.Contains("Akala", json);
    }

    /// <summary>A position outside the picture is dropped, not clamped.</summary>
    /// <remarks>
    /// Clamping would move the marker to the border and keep it, which reads as a real answer.
    /// A zone with no marker is honest; a zone pinned to the edge because somebody edited the file
    /// by hand is a lie about where that zone is.
    /// </remarks>
    [Fact]
    public void A_position_outside_the_picture_is_dropped()
    {
        var path = Scratch();
        File.WriteAllText(path, """
            {
              "marcadores": {
                "buena":  { "isla": "Poni", "x": 0.5, "y": 0.5 },
                "fuera":  { "isla": "Poni", "x": 1.4, "y": 0.5 },
                "sinisla":{ "isla": "",     "x": 0.5, "y": 0.5 }
              }
            }
            """);

        var markers = JsonZoneMarkers.Load(path);

        Assert.Equal(1, markers.Count);
        Assert.NotNull(markers.For("buena"));
        Assert.Null(markers.For("fuera"));
        Assert.Null(markers.For("sinisla"));
    }

    [Fact]
    public async Task Placing_the_same_zone_again_moves_it_instead_of_duplicating()
    {
        var path = Scratch();
        var markers = JsonZoneMarkers.Empty
            .With("ruta-3", new ZoneMarker("Melemele", 0.1, 0.1))
            .With("ruta-3", new ZoneMarker("Melemele", 0.9, 0.9));

        await markers.SaveAsync(path);
        var back = JsonZoneMarkers.Load(path);

        Assert.Equal(1, back.Count);
        Assert.Equal(0.9, back.For("ruta-3")!.X, 6);
    }

    [Fact]
    public async Task A_forgotten_marker_is_gone_from_the_file()
    {
        var path = Scratch();
        var markers = JsonZoneMarkers.Empty
            .With("a", new ZoneMarker("Poni", 0.2, 0.2))
            .With("b", new ZoneMarker("Poni", 0.3, 0.3));

        await markers.Without("a").SaveAsync(path);
        var back = JsonZoneMarkers.Load(path);

        Assert.Null(back.For("a"));
        Assert.NotNull(back.For("b"));
    }

    /// <summary>Placing does not mutate the store it came from.</summary>
    [Fact]
    public void Placing_leaves_the_previous_store_alone()
    {
        var before = JsonZoneMarkers.Empty.With("a", new ZoneMarker("Akala", 0.5, 0.5));
        var after = before.With("b", new ZoneMarker("Akala", 0.6, 0.6));

        Assert.Equal(1, before.Count);
        Assert.Equal(2, after.Count);
    }

    /// <summary>A missing file is no markers, not a crash.</summary>
    [Fact]
    public void A_missing_file_gives_an_empty_store() =>
        Assert.Equal(0, JsonZoneMarkers.Load(
            Path.Combine(Path.GetTempPath(), "no-existe-marcadores.json")).Count);

    /// <summary>And a corrupt one too: the map draws without pins rather than not at all.</summary>
    [Fact]
    public void A_broken_file_gives_an_empty_store()
    {
        var path = Scratch();
        File.WriteAllText(path, "{ esto no es json");

        Assert.Equal(0, JsonZoneMarkers.Load(path).Count);
    }

    /// <summary>Zones with no encounters survive the round trip and keep out of the count.</summary>
    [Fact]
    public async Task Zones_with_no_encounters_are_kept()
    {
        var path = Scratch();
        var markers = JsonZoneMarkers.Empty
            .With("ruta-1", new ZoneMarker("Melemele", 0.4, 0.4))
            .WithNoEncounters(["pueblo-lilii", "senda-mahalo"]);

        await markers.SaveAsync(path);
        var back = JsonZoneMarkers.Load(path);

        Assert.Equal(1, back.Count);
        Assert.Contains("pueblo-lilii", back.NoEncounters);
        Assert.Contains("senda-mahalo", back.NoEncounters);
    }

    /// <summary>A zone with a marker cannot also be one where nothing can be caught.</summary>
    /// <remarks>
    /// Having placed a pin is itself the claim that the place is worth one, so the two statements
    /// contradict each other and the marker wins.
    /// </remarks>
    [Fact]
    public void A_placed_zone_is_not_written_off()
    {
        var markers = JsonZoneMarkers.Empty
            .With("ruta-1", new ZoneMarker("Melemele", 0.4, 0.4))
            .WithNoEncounters(["ruta-1", "pueblo-lilii"]);

        Assert.DoesNotContain("ruta-1", markers.NoEncounters);
        Assert.Contains("pueblo-lilii", markers.NoEncounters);
    }

    /// <summary>And placing one later takes it back out.</summary>
    [Fact]
    public void Placing_a_marker_undoes_writing_the_zone_off()
    {
        var markers = JsonZoneMarkers.Empty
            .WithNoEncounters(["huerto-de-bayas"])
            .With("huerto-de-bayas", new ZoneMarker("Melemele", 0.5, 0.5));

        Assert.Empty(markers.NoEncounters);
        Assert.NotNull(markers.For("huerto-de-bayas"));
    }

    [Fact]
    public async Task Writing_zones_off_can_be_undone()
    {
        var path = Scratch();
        var markers = JsonZoneMarkers.Empty.WithNoEncounters(["a", "b", "c"]);

        await markers.WithEncounters(["b"]).SaveAsync(path);
        var back = JsonZoneMarkers.Load(path);

        Assert.Equal(2, back.NoEncounters.Count);
        Assert.DoesNotContain("b", back.NoEncounters);
    }

    /// <summary>A file written before this existed still loads, with nothing written off.</summary>
    [Fact]
    public void A_file_without_the_section_loads_with_nothing_written_off()
    {
        var path = Scratch();
        File.WriteAllText(path, """
            { "marcadores": { "ruta-1": { "isla": "Melemele", "x": 0.5, "y": 0.5 } } }
            """);

        var markers = JsonZoneMarkers.Load(path);

        Assert.Equal(1, markers.Count);
        Assert.Empty(markers.NoEncounters);
    }
}
