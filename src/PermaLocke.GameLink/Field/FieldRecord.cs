using System.Buffers.Binary;
using System.Globalization;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// One of the game's records of where the player is: world and map, then the position and the rotation.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-09-13 against the running game (§117), flying and walking through twelve places and
/// checking each one against the map number the cartridge gives it. The layout, from the start of the record:
/// </para>
/// <code>
/// +0x00  u16  world
/// +0x02  u16  map (index into zonedata)
/// +0x04  f32  X   +0x08  f32  Y   +0x0C  f32  Z
/// +0x10  f32  qx  +0x14  f32  qy  +0x18  f32  qz  +0x1C  f32  qw
/// +0x20  u32  FFFFFFFF
/// </code>
/// <para>
/// The game keeps several: one follows every step, others hold where the player came into the map, and all of
/// them change map in the same instant. They are heap objects, so their addresses are not trusted between
/// sessions even though they happened to survive a restart; a record is found by what it holds and believed
/// only when it checks out.
/// </para>
/// </remarks>
public static class FieldRecord
{
    public const int Length = 0x24;

    /// <summary>
    /// What a record that is not a record looks like, and why each check exists.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The map has to exist and belong to <b>that</b> world. This is the check that does the work: while
    /// flying the records held «world 600, map 12288» and «world 28964, map 100», and during a battle «world
    /// 35, map 89», which is a real map of world 58.</item>
    /// <item>The position is not zero. A dead record reads all zeros, and map 0 of world 0 is Ruta 1, so zeros
    /// would otherwise say Ruta 1 with total confidence.</item>
    /// <item>X, Y and Z are not all equal: the same shape search finds dozens of (1, 1, 1) scale vectors.</item>
    /// <item>The position is not inside the unit cube around the origin. Found on 2026-09-21: memory holds dozens of
    /// transforms at <b>(1, 0, 0)</b> with an identity rotation and <c>FFFFFFFF</c> behind, and since their first four
    /// bytes are zero they read as world 0, map 0 — Ruta 1 (Afueras de Hauoli). When a battle starts the live records
    /// switch to the battle's map and stop validating, so for those seconds <b>only</b> these voted, and every
    /// battle on Ruta 1 was placed in the outskirts: 45 of them in the logs, always exactly (1, 0, 0). The smallest
    /// real position ever logged is (2000, 3.94, 6299) in Senda Mahalo; nobody stands within one unit of a map's
    /// corner, so this cannot lose a real reading.</item>
    /// <item>The rotation is a unit quaternion and <c>FFFFFFFF</c> follows it.</item>
    /// </list>
    /// </remarks>
    public static FieldZone? Parse(ReadOnlySpan<byte> record, MapTable maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        if (record.Length < Length)
        {
            return null;
        }

        var world = BinaryPrimitives.ReadUInt16LittleEndian(record);
        var map = BinaryPrimitives.ReadUInt16LittleEndian(record[2..]);

        if (!maps.Matches(map, world) || maps.For(map) is not { } info)
        {
            return null;
        }

        var x = BinaryPrimitives.ReadSingleLittleEndian(record[4..]);
        var y = BinaryPrimitives.ReadSingleLittleEndian(record[8..]);
        var z = BinaryPrimitives.ReadSingleLittleEndian(record[0xC..]);

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)
            || (x == 0 && y == 0 && z == 0) || (x == y && y == z)
            || Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))) <= 1
            || Math.Abs(x) > 100000 || Math.Abs(y) > 100000 || Math.Abs(z) > 100000)
        {
            return null;
        }

        double norm = 0;

        for (var at = 0x10; at < 0x20; at += 4)
        {
            var component = BinaryPrimitives.ReadSingleLittleEndian(record[at..]);

            if (!float.IsFinite(component))
            {
                return null;
            }

            norm += component * (double)component;
        }

        if (Math.Abs(Math.Sqrt(norm) - 1) > 0.02 || BinaryPrimitives.ReadUInt32LittleEndian(record[0x20..]) != 0xFFFFFFFF)
        {
            return null;
        }

        return new FieldZone(map, world, info.LocationId, info.LocationName);
    }

    /// <summary>
    /// The zone that at least two records and more than half of the valid ones say, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was «all of them agree», and that was measured wrong in play on 2026-09-14 (§118): the game keeps a record
    /// that holds <b>the previous map</b>, a valid record with a real map of the right world. Seven records read
    /// together: six said Ruta 2 with the same position to the decimal, and <c>0x33F6E490</c> said Ciudad Hauoli
    /// somewhere else. The search at connection had kept exactly that one and one live one, so after every change
    /// of map the two disagreed, the zone read as unknown, and a battle was placed in the last zone confirmed — the
    /// previous one. The player saw it as PermaLocke always being one map behind.
    /// </para>
    /// <para>
    /// A strict majority keeps what §55 taught — one record alone is not believed, and a tie is «I do not know» —
    /// while a stale record among live ones is outvoted instead of vetoing them.
    /// </para>
    /// </remarks>
    public static FieldZone? Resolve(IEnumerable<FieldZone?> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var valid = readings.OfType<FieldZone>().ToList();

        if (valid.Count < 2)
        {
            return null;
        }

        var winner = valid
            .GroupBy(reading => (reading.World, reading.Map))
            .OrderByDescending(group => group.Count())
            .First();

        if (winner.Count() >= 2 && winner.Count() * 2 > valid.Count)
        {
            return winner.First();
        }

        // Sin mayoría de MAPA puede haberla de ZONA, que es lo que usan las reglas. Medido el 2026-09-21 en la Escuela de
        // Entrenadores: seis registros del mapa de la escuela (mundo 27, mapa 46) y seis del de fuera (mundo 0, mapa 0),
        // los doce de «Ruta 1 (Afueras de Hauoli)». Seis contra seis no decidía nada y la ruta se quedaba sin identificar
        // con todos los registros diciendo lo mismo. Misma exigencia que arriba: dos como poco y más de la mitad.
        var place = valid
            .GroupBy(reading => reading.LocationId)
            .OrderByDescending(group => group.Count())
            .First();

        return place.Count() >= 2 && place.Count() * 2 > valid.Count
            ? place.GroupBy(reading => (reading.World, reading.Map)).OrderByDescending(group => group.Count()).First().First()
            : null;
    }

    /// <summary>
    /// The last twenty bytes of a record whose rotation is the identity, which is what a record holds right after
    /// arriving on a map.
    /// </summary>
    /// <remarks>
    /// After loading a save the rotation is the saved one instead, so this finds nothing until the first change
    /// of map; <see cref="SavedPattern"/> covers that stretch.
    /// </remarks>
    public static byte[] LandingPattern { get; } =
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x00, 0x80, 0x3F, 0xFF, 0xFF, 0xFF, 0xFF];

    /// <summary>Where <see cref="LandingPattern"/> sits inside a record.</summary>
    public const int LandingPatternOffset = 0x10;

    /// <summary>
    /// World, map and position exactly as the last save holds them, laid out as a record holds them.
    /// </summary>
    /// <param name="situation">The save's <c>Situation</c> block: world at +0, map at +2, position at +8.</param>
    /// <remarks>
    /// Right after loading, before moving, the live records hold exactly this. Measured after restarting the
    /// emulator: five records found in 14 ms, while the landing pattern found none.
    /// </remarks>
    public static byte[] SavedPattern(ReadOnlySpan<byte> situation)
    {
        var pattern = new byte[16];
        situation[..4].CopyTo(pattern);
        situation.Slice(8, 12).CopyTo(pattern.AsSpan(4));
        return pattern;
    }

    /// <summary>
    /// Any record of this world and map, whatever its position and rotation: the world and map at the start and
    /// <c>FFFFFFFF</c> at the end, everything between a wildcard.
    /// </summary>
    /// <remarks>
    /// The other two patterns only match at two moments — right after loading, before moving, and right after
    /// landing — and a player who connects PermaLocke after walking a few steps is at neither. Measured on
    /// 2026-09-14 in the Jardines de Ula-Ula: the search at connection found <b>one</b> record, while the three
    /// measured addresses all held that map with the player facing sideways. So once one record, or the save, says
    /// which map it is, this finds the rest. Eight fixed bytes are loose on purpose and every hit still has to pass
    /// <see cref="Parse"/>.
    /// <para>
    /// <b>Known hazard, measured 2026-09-21 and not fixed.</b> All the signal is in those four bytes, and for
    /// <b>world 0, map 0</b> they are zeros, so the search matches blank memory. On Ruta 1 — map 3, whose neighbour
    /// Ruta 1 (Afueras de Hauoli) is map 0 — the same pass got 21 candidates for map 3 and <b>255, the cap, three
    /// times</b> for map 0. Ten of the sixteen surviving records sat in the region those pages had just swept
    /// (0x3010–0x301A) against six in the real cluster, so <see cref="Resolve"/> gave a strict majority to map 0 and
    /// the run spent its first route, marked it and confiscated the player's balls one zone away. The cartridge said
    /// otherwise on the spot: a Tepig caught eight seconds earlier carries <c>MetLocation</c> = Ruta 1.
    /// </para>
    /// <para>
    /// Not fixed by skipping the search: that was tried and it breaks the opposite case, which has its own tests —
    /// a player standing <b>in</b> map 0 is found by paging through exactly that flood. What is still unknown is
    /// whether those ten records are junk that got past <see cref="Parse"/> or real records of the loaded
    /// neighbouring map, and the two want opposite repairs. <see cref="Describe"/> exists to settle it the next time
    /// it happens.
    /// </para>
    /// </remarks>
    /// <summary>One record written out for the log: its map, its zone and where it puts the player.</summary>
    /// <remarks>
    /// The position is the part that matters and the part <see cref="FieldZone"/> does not carry. §118 was solved by
    /// seeing that six live records agreed on the player's position <b>to the decimal</b> while the odd one out sat
    /// somewhere else; with only the zone names in the log, a stale record, a record of a neighbouring map and a
    /// lucky piece of memory all read the same. Only used when the records disagree, which is when somebody is
    /// going to have to tell those three apart.
    /// </remarks>
    /// <summary>The X, Y and Z a record holds. Only meaningful for a record <see cref="Parse"/> accepts.</summary>
    public static (float X, float Y, float Z) Position(ReadOnlySpan<byte> record) =>
        (BinaryPrimitives.ReadSingleLittleEndian(record[4..]),
         BinaryPrimitives.ReadSingleLittleEndian(record[8..]),
         BinaryPrimitives.ReadSingleLittleEndian(record[0xC..]));

    public static string Describe(ReadOnlySpan<byte> record, MapTable maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        if (record.Length < Length)
        {
            return "corto";
        }

        var world = BinaryPrimitives.ReadUInt16LittleEndian(record);
        var map = BinaryPrimitives.ReadUInt16LittleEndian(record[2..]);
        var x = BinaryPrimitives.ReadSingleLittleEndian(record[4..]);
        var y = BinaryPrimitives.ReadSingleLittleEndian(record[8..]);
        var z = BinaryPrimitives.ReadSingleLittleEndian(record[0xC..]);
        var name = maps.Matches(map, world) && maps.For(map) is { } info ? info.LocationName : "no es un mapa de ese mundo";
        var valid = Parse(record, maps) is not null ? "" : " [rechazado]";

        return string.Create(CultureInfo.InvariantCulture,
            $"mundo {world} mapa {map} ({name}) en ({x:0.##}, {y:0.##}, {z:0.##}){valid}");
    }

    public static (byte[] Pattern, byte[] Mask) SiblingPattern(int world, int map)
    {
        var pattern = new byte[Length];
        var mask = new byte[Length];

        BinaryPrimitives.WriteUInt16LittleEndian(pattern, (ushort)world);
        BinaryPrimitives.WriteUInt16LittleEndian(pattern.AsSpan(2), (ushort)map);
        pattern.AsSpan(0x20).Fill(0xFF);

        mask.AsSpan(0, 4).Fill(0xFF);
        mask.AsSpan(0x20).Fill(0xFF);

        return (pattern, mask);
    }
}
