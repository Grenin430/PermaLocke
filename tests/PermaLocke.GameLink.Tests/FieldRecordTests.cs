using System.Buffers.Binary;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Field;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The player's position records, checked with the bytes read from the running game on 2026-09-13 and 14 (§117).
/// </summary>
public sealed class FieldRecordTests
{
    private static readonly MapTable Maps = new(
    [
        new MapInfo(0, 0, "ruta-1", "Ruta 1"),
        new MapInfo(7, 0, "ruta-2", "Ruta 2"),
        new MapInfo(13, 0, "ciudad-hauoli-zona-comercial", "Ciudad Hauoli (Zona Comercial)"),
        new MapInfo(28, 15, "cementerio-de-hauoli", "Cementerio de Hauoli"),
        new MapInfo(89, 58, "pueblo-ohana", "Pueblo Ohana"),
        new MapInfo(171, 117, "ruta-16", "Ruta 16")
    ]);

    private static byte[] Hex(string text) => Convert.FromHexString(text.Replace("-", string.Empty).Replace(" ", string.Empty));

    /// <summary>Where the player came into Ciudad Hauoli: identity rotation. Read at 0x33F68EC4 - 4.</summary>
    private static readonly byte[] Landing = Hex(
        "00-00-0D-00 45-CD-EB-45 00-00-7A-C4 33-E5-7E-46 00-00-00-00 00-00-00-00 00-00-00-00 00-00-80-3F FF-FF-FF-FF");

    /// <summary>The record that follows every step, with the player turned. Read at 0x33F6E44C - 4.</summary>
    private static readonly byte[] Walking = Hex(
        "00-00-0D-00 67-F5-EE-45 00-D0-78-C4 CC-D5-80-46 00-00-00-00 E8-AF-89-3E 00-00-00-00 D1-91-76-3F FF-FF-FF-FF");

    [Fact]
    public void A_landing_record_reads_as_its_map()
    {
        var zone = FieldRecord.Parse(Landing, Maps);

        Assert.NotNull(zone);
        Assert.Equal(13, zone.Map);
        Assert.Equal("ciudad-hauoli-zona-comercial", zone.LocationId);
    }

    [Fact]
    public void A_turned_player_still_reads_because_the_rotation_is_a_unit_quaternion()
    {
        Assert.Equal(13, FieldRecord.Parse(Walking, Maps)?.Map);
    }

    [Fact]
    public void The_loading_screen_of_a_flight_is_not_a_zone()
    {
        // «mundo 600, mapa 12288» y todo a cero, leído mientras se volaba.
        var flying = Hex("58-02-00-30 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00");

        Assert.Null(FieldRecord.Parse(flying, Maps));
    }

    [Fact]
    public void A_battle_leaves_a_real_map_of_the_wrong_world()
    {
        // Durante un combate en la Ruta 16 una copia leyó «mundo 35, mapa 89»: el 89 es Pueblo Ohana, del mundo 58.
        var battle = (byte[])Landing.Clone();
        battle[0] = 35;
        battle[2] = 89;

        Assert.Null(FieldRecord.Parse(battle, Maps));
    }

    [Fact]
    public void A_dead_record_of_zeros_is_not_ruta_1()
    {
        var dead = new byte[FieldRecord.Length];
        dead[0x1E] = 0x80;
        dead[0x1F] = 0x3F;
        dead.AsSpan(0x20).Fill(0xFF);

        Assert.Null(FieldRecord.Parse(dead, Maps));
    }

    [Fact]
    public void The_scale_vectors_the_shape_search_also_finds_are_rejected()
    {
        var scale = Hex("00-00-00-00 00-00-80-3F 00-00-80-3F 00-00-80-3F 00-00-00-00 00-00-00-00 00-00-00-00 00-00-80-3F FF-FF-FF-FF");

        Assert.Null(FieldRecord.Parse(scale, Maps));
    }

    /// <summary>The (1, 0, 0) transforms: world 0 and map 0 because their first bytes are zero, so «Afueras de Hauoli».</summary>
    private static readonly byte[] Placeholder = Hex(
        "00-00-00-00 00-00-80-3F 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-00-00 00-00-80-3F FF-FF-FF-FF");

    [Fact]
    public void The_placeholder_transforms_at_one_zero_zero_are_rejected()
    {
        Assert.Null(FieldRecord.Parse(Placeholder, Maps));
    }

    /// <summary>
    /// What placed every Ruta 1 battle in the outskirts on 2026-09-21: as the battle starts the live records hold the
    /// battle's map and stop validating, and the placeholders were all that was left to vote.
    /// </summary>
    [Fact]
    public void At_the_start_of_a_battle_the_placeholders_no_longer_decide_the_zone()
    {
        var battle = (byte[])Landing.Clone();
        battle[0] = 35;
        battle[2] = 89;

        var readings = new[] { battle, battle, Placeholder, Placeholder, Placeholder }
            .Select(record => FieldRecord.Parse(record, Maps));

        Assert.Null(FieldRecord.Resolve(readings));
    }

    /// <summary>A real record of world 0, map 0 still reads: the check is about the position, not the map.</summary>
    [Fact]
    public void A_real_position_in_map_zero_still_reads()
    {
        // Leído el 2026-09-21 en 0x33F6E510: mundo 0 mapa 0 en (28063.24, -907.86, 18102.31).
        var outskirts = (byte[])Placeholder.Clone();
        BinaryPrimitives.WriteSingleLittleEndian(outskirts.AsSpan(4), 28063.24f);
        BinaryPrimitives.WriteSingleLittleEndian(outskirts.AsSpan(8), -907.86f);
        BinaryPrimitives.WriteSingleLittleEndian(outskirts.AsSpan(0xC), 18102.31f);

        Assert.Equal(0, FieldRecord.Parse(outskirts, Maps)?.Map);
    }

    [Fact]
    public void A_rotation_that_is_not_a_rotation_is_rejected()
    {
        var bent = (byte[])Landing.Clone();
        bent[0x1F] = 0x40; // qw = 2

        Assert.Null(FieldRecord.Parse(bent, Maps));
    }

    [Fact]
    public void Without_the_trailing_marker_it_is_something_else()
    {
        var other = (byte[])Landing.Clone();
        other[0x20] = 0;

        Assert.Null(FieldRecord.Parse(other, Maps));
    }

    [Fact]
    public void An_empty_map_table_validates_nothing()
    {
        Assert.Null(FieldRecord.Parse(Landing, MapTable.Empty));
    }

    [Fact]
    public void Two_records_agreeing_give_the_zone()
    {
        var zone = FieldRecord.Resolve([FieldRecord.Parse(Landing, Maps), FieldRecord.Parse(Walking, Maps), null]);

        Assert.Equal(13, zone?.Map);
    }

    [Fact]
    public void One_record_alone_is_not_enough()
    {
        Assert.Null(FieldRecord.Resolve([FieldRecord.Parse(Landing, Maps), null, null]));
    }

    private static readonly FieldZone Route2 = new(7, 0, "ruta-2", "Ruta 2");
    private static readonly FieldZone Hauoli = new(13, 0, "ciudad-hauoli-zona-comercial", "Ciudad Hauoli (Zona Comercial)");

    /// <summary>
    /// What was read on 2026-09-14 with the player in Ruta 2: six live records, and 0x33F6E490 holding the previous
    /// map. Under «all must agree» this was unknown, and the battle went to the zone before.
    /// </summary>
    [Fact]
    public void A_record_of_the_previous_map_is_outvoted_by_the_live_ones()
    {
        Assert.Equal(Route2, FieldRecord.Resolve([Route2, Hauoli, Route2, Route2, Route2, Route2, Route2]));
    }

    /// <summary>What the connection had kept: one live record and the previous-map one. A tie is not a zone.</summary>
    [Fact]
    public void One_against_one_means_nobody_knows()
    {
        Assert.Null(FieldRecord.Resolve([Route2, Hauoli]));
    }

    private static readonly FieldZone School = new(46, 27, "ruta-1-afueras-de-hauoli", "Ruta 1 (Afueras de Hauoli)");
    private static readonly FieldZone Outskirts = new(0, 0, "ruta-1-afueras-de-hauoli", "Ruta 1 (Afueras de Hauoli)");

    /// <summary>
    /// The Trainers' School on 2026-09-21: six records of the school's map and six of the map outside, all twelve in the
    /// same zone. Six against six by map decides nothing, but the zone — what the rules use — is unanimous.
    /// </summary>
    [Fact]
    public void Two_maps_of_the_same_zone_still_give_the_zone()
    {
        var zone = FieldRecord.Resolve([School, School, School, Outskirts, Outskirts, Outskirts]);

        Assert.Equal("ruta-1-afueras-de-hauoli", zone?.LocationId);
    }

    /// <summary>The zone needs the same majority the map does: two maps of one zone against one of another are not enough.</summary>
    [Fact]
    public void A_zone_split_in_two_maps_does_not_outvote_a_bigger_rival()
    {
        Assert.Null(FieldRecord.Resolve([School, Outskirts, Route2, Route2]));
        Assert.Equal("ruta-2", FieldRecord.Resolve([School, Route2, Route2])?.LocationId);
    }

    [Fact]
    public void Half_is_not_a_majority()
    {
        var cemetery = new FieldZone(28, 15, "cementerio-de-hauoli", "Cementerio de Hauoli");

        Assert.Null(FieldRecord.Resolve([Route2, Route2, Hauoli, cemetery]));
    }

    [Fact]
    public void Two_against_one_is_the_two()
    {
        Assert.Equal(Route2, FieldRecord.Resolve([Route2, null, Hauoli, Route2]));
    }

    [Fact]
    public void The_saved_pattern_is_world_map_and_position_as_a_live_record_holds_them()
    {
        // Situation de la partida guardada en la Ruta 16: M=117, +2=171, FFFFFFFF, X, Y, Z. La búsqueda con este
        // patrón encontró cinco registros tras reiniciar el emulador.
        var situation = Hex("75-00-AB-00 FF-FF-FF-FF 3C-65-F8-45 A4-00-76-43 D9-0E-C6-46");

        Assert.Equal("7500AB003C65F845A4007643D90EC646", Convert.ToHexString(FieldRecord.SavedPattern(situation)));
    }

    /// <summary>Read at 0x33F6E448 on 2026-09-14 in the Jardines de Ula-Ula, facing sideways: no other pattern finds it.</summary>
    private static readonly byte[] Sideways = Hex(
        "79-00-B9-00 BE-D9-7D-45 34-33-13-C0 91-2B-84-45 00-00-00-00 F1-04-35-BF 00-00-00-00 F5-04-35-3F FF-FF-FF-FF");

    [Fact]
    public void A_record_facing_sideways_matches_neither_the_landing_nor_the_saved_pattern_but_its_sibling_pattern()
    {
        var maps = new MapTable([new MapInfo(185, 121, "jardines-de-ula-ula", "Jardines de Ula-Ula")]);
        Assert.Equal("jardines-de-ula-ula", FieldRecord.Parse(Sideways, maps)?.LocationId);

        Assert.False(Sideways.AsSpan(FieldRecord.LandingPatternOffset).SequenceEqual(FieldRecord.LandingPattern));

        var (pattern, mask) = FieldRecord.SiblingPattern(121, 185);

        Assert.Equal(FieldRecord.Length, pattern.Length);
        Assert.True(Matches(Sideways, pattern, mask));
        Assert.True(Matches(Landing, FieldRecord.SiblingPattern(0, 13).Pattern, FieldRecord.SiblingPattern(0, 13).Mask));
        Assert.False(Matches(Landing, pattern, mask));
    }

    [Fact]
    public void The_sibling_pattern_fixes_only_world_map_and_the_marker()
    {
        var (_, mask) = FieldRecord.SiblingPattern(121, 185);

        Assert.Equal(8, mask.Count(b => b == 0xFF));
        Assert.All(mask.AsSpan(4, 0x1C).ToArray(), b => Assert.Equal(0, b));
    }

    private static bool Matches(byte[] data, byte[] pattern, byte[] mask) =>
        Enumerable.Range(0, pattern.Length).All(i => (data[i] & mask[i]) == (pattern[i] & mask[i]));

    [Fact]
    public void The_landing_pattern_is_the_tail_of_a_landing_record()
    {
        Assert.True(Landing.AsSpan(FieldRecord.LandingPatternOffset).SequenceEqual(FieldRecord.LandingPattern));
    }

    /// <summary>
    /// Lo que tiene que distinguir: dos registros del MISMO mapa en sitios distintos. Con solo el nombre de la zona
    /// en el log, un registro vivo, uno rancio y uno del mapa de al lado se leen igual, y eso es lo que dejó sin
    /// resolver el caso del 2026-09-21.
    /// </summary>
    [Fact]
    public void A_record_is_described_with_its_map_and_its_position()
    {
        var maps = new MapTable([new MapInfo(185, 121, "jardines-de-ula-ula", "Jardines de Ula-Ula")]);
        var line = FieldRecord.Describe(Sideways, maps);

        Assert.Contains("mundo 121 mapa 185", line, StringComparison.Ordinal);
        Assert.Contains("Jardines de Ula-Ula", line, StringComparison.Ordinal);
        Assert.DoesNotContain("rechazado", line, StringComparison.Ordinal);

        var moved = (byte[])Sideways.Clone();
        BinaryPrimitives.WriteSingleLittleEndian(moved.AsSpan(4), 1234.5f);

        Assert.NotEqual(line, FieldRecord.Describe(moved, maps));
        Assert.Contains("1234.5", FieldRecord.Describe(moved, maps), StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_that_parse_rejects_says_so_when_described()
    {
        var maps = new MapTable([new MapInfo(185, 121, "jardines-de-ula-ula", "Jardines de Ula-Ula")]);
        var blank = new byte[FieldRecord.Length];

        Assert.Null(FieldRecord.Parse(blank, maps));
        Assert.Contains("rechazado", FieldRecord.Describe(blank, maps), StringComparison.Ordinal);
    }
}
