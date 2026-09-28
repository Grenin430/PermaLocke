using System.Globalization;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// Says which map, and so which zone of the run, the player is on, read live from the game.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the anchor of §23, which died because it pointed at memory the game later used for something else
/// (§55). This one does not trust an address: the records are found by what they hold, every reading has to
/// pass <see cref="FieldRecord.Parse"/>, and a strict majority of at least two has to agree (§117, §118).
/// </para>
/// <para>
/// Searching is the part that could hurt the emulator, and it is kept rare and small: at most six single searches
/// over the 64 MB of linear heap — measured at 16 ms each — never in a loop, and no more than once every
/// <see cref="SearchEvery"/>, or <see cref="SearchEveryWhileDisagreeing"/> when the records hold maps that make no
/// majority. After that each reading is a few dozen bytes per record.
/// </para>
/// <para>
/// The addresses of the last session are tried before searching, as the party and the bag already do (§118): the
/// three measured records survived flights and a restart of the emulator. Not trusted for that — they are read and
/// parsed like any other, and used only while two of them agree.
/// </para>
/// </remarks>
public sealed class FieldZoneReader(AzaharRpcClient client, SavedGameCache saved, MapTable maps,
    string knownAddressesPath, ILogger<FieldZoneReader> logger, TimeProvider? timeProvider = null) : IZoneProvider
{
    private const uint LinearHeap = 0x30000000, LinearHeapSize = 0x04000000;
    private const int SearchCap = 255;
    private const int MaxRecords = 16;
    private const int SearchBudget = 6;
    private const uint PageSize = 4096;
    private readonly Dictionary<(int World, int Map), uint> _siblingCursors = [];

    /// <summary>The least time between two searches.</summary>
    public static readonly TimeSpan SearchEvery = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long nothing has to validate before searching again. Longer than a flight, whose loading leaves every
    /// record unreadable for about eight seconds.
    /// </summary>
    public static readonly TimeSpan UnknownBeforeSearch = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The least time between two searches while the records hold valid maps that do not make a majority. That is not
    /// a loading screen that will pass: it is a stale record in the set, and it does not fix itself (§118).
    /// </summary>
    public static readonly TimeSpan SearchEveryWhileDisagreeing = TimeSpan.FromSeconds(20);

    private readonly object _gate = new();
    private List<uint> _records = [];
    private List<uint>? _remembered;
    private DateTimeOffset _lastSearch = DateTimeOffset.MinValue;
    private DateTimeOffset? _unknownSince;
    private int? _lastMap;
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private PKHeX.Core.SAV7USUM? _searchedSave;
    private DateTimeOffset _lastSaveProbe = DateTimeOffset.MinValue;
    private static readonly TimeSpan SaveProbeEvery = TimeSpan.FromSeconds(5);

    public (FieldZone Zone, DateTimeOffset At)? LastConfirmed { get; private set; }

    public FieldZone? CurrentZone() => CurrentZone(allowSearch: true);

    /// <param name="allowSearch">
    /// False during a battle: the records are empty until it ends, so a search then would find nothing and
    /// spend the allowance.
    /// </param>
    public FieldZone? CurrentZone(bool allowSearch)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();

            if (maps.Count == 0)
            {
                return null;
            }

            var tracked = ReadTracked(_records, now);
            var readings = tracked.Select(record => record.Zone).ToList();
            var (zone, byMotion) = Candidate(tracked, now);
            var basis = tracked;

            // Las de la última sesión antes que buscar: leer unas pocas direcciones no le cuesta nada al emulador.
            if (zone is null && Remembered() is { Count: > 0 } remembered && !remembered.SequenceEqual(_records))
            {
                var again = ReadTracked(remembered, now);

                if (FieldRecord.Resolve(again.Select(record => record.Zone)) is { } revalidated)
                {
                    _records = remembered;
                    zone = revalidated;
                    basis = again;
                    logger.LogInformation("Registros de posición de la última vez, revalidados sin buscar: {Addresses}",
                        Addresses(remembered));
                }
            }

            var held = zone is not null && !Believable(zone, basis, now);
            var disagreeing = zone is null && readings.Count(reading => reading is not null) >= 2;

            if (zone is null || held)
            {
                _doubtSince ??= now;
            }

            // Mantener una zona también es una duda: se busca con la cadencia de cuando no hay acuerdo, por si el que
            // sigue al jugador no está entre los registros que se leen. Y no en mitad de una carga (SettleBeforeSearch).
            if ((zone is null || held) && allowSearch && Settled(now)
                && (ShouldSearch(now, disagreeing || held) || SavedSinceLastSearch(now)))
            {
                if (disagreeing)
                {
                    logger.LogInformation("Los registros de posición no se ponen de acuerdo ({Maps}); se buscan más",
                        string.Join(", ", readings.OfType<FieldZone>().Select(reading => reading.LocationName).Distinct()));

                    foreach (var line in Describe(_records))
                    {
                        logger.LogInformation("  {Record}", line);
                    }
                }

                Search(now, basis.Select(record => record.Zone).OfType<FieldZone>());
                var found = ReadTracked(_records, now);
                (zone, byMotion) = Candidate(found, now);
                basis = found;
                held = zone is not null && !Believable(zone, basis, now);
            }

            if (zone is null)
            {
                _unknownSince ??= now;
                return null;
            }

            if (held)
            {
                if (!_holding)
                {
                    logger.LogInformation(
                        "La mayoría dice {Other}, pero con registros que no han cambiado ni se han movido desde que " +
                        "llegaste a {Current}: se mantiene {Current}", zone.LocationName, _current!.LocationName,
                        _current.LocationName);

                    foreach (var line in Describe(_records))
                    {
                        logger.LogInformation("  {Record}", line);
                    }
                }

                _holding = true;
                zone = _current!;
                byMotion = false;
            }
            else
            {
                _holding = false;
                _doubtSince = null;
                Adopt(zone, basis);
            }

            _unknownSince = null;
            LastConfirmed = (zone, now);

            if (zone.Map != _lastMap)
            {
                logger.LogInformation("Zona: {Zone} (mapa {Map}, mundo {World}){Motion}", zone.LocationName, zone.Map,
                    zone.World, byMotion ? " — por el registro que se mueve" : "");

                // Cuando el que anda le lleva la contraria a una mayoría, se apuntan los registros: es la decisión
                // nueva del §164 y la que conviene poder revisar en el log si algún día sale mal.
                if (byMotion && FieldRecord.Resolve(basis.Select(record => record.Zone)) is { } outvoted)
                {
                    logger.LogInformation("  contra una mayoría quieta que decía {Outvoted}:", outvoted.LocationName);

                    foreach (var line in Describe(_records))
                    {
                        logger.LogInformation("  {Record}", line);
                    }
                }

                _lastMap = zone.Map;
            }

            return zone;
        }
    }

    /// <summary>
    /// Whether the player has walked since a moment (2026-09-28): the post-capture menus (name, party or PC, summary) and
    /// the battle never move the player, so a step means they are all closed. Reads only the known records, never searches.
    /// </summary>
    public bool MovedSince(DateTimeOffset since)
    {
        lock (_gate)
        {
            ReadTracked(_records, _time.GetUtcNow());
            return _records.Any(address => _motion.TryGetValue(address, out var seen) && seen.LastMove > since);
        }
    }

    /// <summary>How recently a record must have moved twice to count as the one following the player.</summary>
    public static readonly TimeSpan WalkingWindow = TimeSpan.FromSeconds(5);

    private readonly Dictionary<uint, (float X, float Y, float Z, DateTimeOffset LastMove, DateTimeOffset PreviousMove)> _motion = [];

    /// <summary>Reads the records and notes which of them moved since the last read.</summary>
    private List<(uint Address, FieldZone? Zone)> ReadTracked(IEnumerable<uint> addresses, DateTimeOffset now)
    {
        var result = new List<(uint Address, FieldZone? Zone)>();

        foreach (var address in addresses)
        {
            if (!client.TryReadMemory(address, FieldRecord.Length, out var bytes)
                || FieldRecord.Parse(bytes, maps) is not { } zone)
            {
                result.Add((address, null));
                continue;
            }

            var (x, y, z) = FieldRecord.Position(bytes);

            if (!_motion.TryGetValue(address, out var seen))
            {
                _motion[address] = (x, y, z, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            }
            else if (seen.X != x || seen.Y != y || seen.Z != z)
            {
                _motion[address] = (x, y, z, now, seen.LastMove);
            }

            result.Add((address, zone));
        }

        return result;
    }

    /// <summary>
    /// The zone of the records that keep moving, when they all agree on it: those follow the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on 2026-09-21 on Ruta 1. The §117 found that every record changes map at the same instant, and that
    /// holds across a warp — a door, a flight, a loading screen. It does <b>not</b> hold across a seamless edge: walking
    /// from the outskirts into Ruta 1, 0x33F6E4C8 changed position at every read and switched to map 3, while
    /// 0x33F6E510 kept map 0 and one position, the place the player came in. One against one, <see
    /// cref="FieldRecord.Resolve"/> rightly decides nothing, and a route where that happens is never placed.
    /// </para>
    /// <para>
    /// Moving is what tells them apart, and it has to be moving, not having moved: the record of where the player
    /// came in also changes, once, when they come in. So a record counts only when it has moved at least twice in the
    /// last <see cref="WalkingWindow"/>, and only when every such record agrees on the map. A wild battle starts while
    /// walking in grass, which is exactly when this is true. Standing still it decides nothing, as before, and a
    /// single reading can never be enough, which keeps the §55 lesson: a record is not believed alone until it proves
    /// it is alive.
    /// </para>
    /// </remarks>
    private FieldZone? Walking(List<(uint Address, FieldZone? Zone)> tracked, DateTimeOffset now)
    {
        var walking = tracked
            .Where(record => record.Zone is not null && IsWalking(record.Address, now))
            .Select(record => record.Zone!)
            .GroupBy(zone => (zone.World, zone.Map))
            .ToList();

        return walking.Count == 1 ? walking[0].First() : null;
    }

    /// <summary>The record has moved at least twice in the last <see cref="WalkingWindow"/>.</summary>
    private bool IsWalking(uint address, DateTimeOffset now) =>
        _motion.TryGetValue(address, out var motion)
        && motion.PreviousMove != DateTimeOffset.MinValue
        && now - motion.PreviousMove <= WalkingWindow;

    /// <summary>
    /// What the readings say: the zone of the record that keeps moving when it is on another map than the majority, or
    /// else the majority.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 2026-09-22 the majority came first and the walking record only broke ties. Found playing, twice in forty
    /// minutes, the same way: from Ciudad Hauoli into Ruta 2 and from the outskirts into the Escuela, both edges with no
    /// door. The first battle of the new route was counted in the one behind, already spent, so without balls; and when
    /// it ended the zone changed and the balls came back. The game keeps copies of the player's position that it only
    /// refreshes now and then — at a door, when a battle starts or ends —: in the same log two of them go from garbage to
    /// the player's exact position twenty seconds later. Across an edge they stay on the map behind, frozen, and three
    /// of them outvote the one record that follows the player, until a battle brings them up to date.
    /// </para>
    /// <para>
    /// A record that has moved twice in the last seconds is where the player is, and one that has not moved does not
    /// know better. So on another map, the walking record wins. It is still the same bar as before — two moves, and
    /// every walking record on one map —, and it is never needed standing still: then the majority, and the hysteresis
    /// of <see cref="Believable"/>, which keeps the new zone once the walking stops.
    /// </para>
    /// </remarks>
    private (FieldZone? Zone, bool ByMotion) Candidate(List<(uint Address, FieldZone? Zone)> readings, DateTimeOffset now)
    {
        var majority = FieldRecord.Resolve(readings.Select(record => record.Zone));

        if (Walking(readings, now) is { } walking
            && (majority is null || (walking.World, walking.Map) != (majority.World, majority.Map)))
        {
            return (walking, true);
        }

        return (majority, false);
    }

    /// <summary>The zone last adopted, and the records that said it while it was the zone.</summary>
    private FieldZone? _current;
    private HashSet<uint> _support = [];
    private bool _holding;

    /// <summary>
    /// Whether the readings are enough to leave the zone the player was in for <paramref name="candidate"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found playing on 2026-09-21, twice in half an hour: opening the bag in the Escuela de Entrenadores took the balls
    /// away «because you are in Ruta 1 (Afueras de Hauoli)», and gave them back on closing it; the same in Ciudad Hauoli
    /// with the Paseo Marítimo and the Zona Comercial. The log has the records at those moments. The game keeps
    /// records of <b>where the player was</b> — the map they came from, the spot a battle started — next to the ones
    /// that follow them, and outnumbered they are harmless. Opening the bag blanks the ones that follow the player
    /// for as long as it is open, and the stale ones are left voting alone: four of Zona Comercial against none.
    /// </para>
    /// <para>
    /// So a zone is left when the records that said it change their mind, not when they fall silent. Moving to another
    /// place needs one of two things among the records that now say it: a record that said the current place while it
    /// was the current place — the ones following the player, which change map with them at a door, in a flight or
    /// across an edge — or a record that is walking. A record that already said the other place before and has not
    /// moved since is what the player left behind. Only for a change of place: two maps of the same place change
    /// freely, because no rule tells them apart. And it only ever keeps a zone, never makes one up: when nothing
    /// reaches a majority the answer is still «I do not know».
    /// </para>
    /// </remarks>
    private bool Believable(FieldZone candidate, List<(uint Address, FieldZone? Zone)> basis, DateTimeOffset now) =>
        _current is not { } current
        || candidate.LocationId == current.LocationId
        || basis.Any(record => record.Zone is { } zone && zone.LocationId == candidate.LocationId
            && (_support.Contains(record.Address) || IsWalking(record.Address, now)));

    private void Adopt(FieldZone zone, List<(uint Address, FieldZone? Zone)> basis)
    {
        var voters = basis.Where(record => record.Zone?.LocationId == zone.LocationId).Select(record => record.Address);

        if (_current is null || _current.LocationId != zone.LocationId)
        {
            _support = [.. voters];
        }
        else
        {
            _support.UnionWith(voters);
        }

        _current = zone;
    }

    private IEnumerable<FieldZone?> Read(IEnumerable<uint> addresses) => addresses.Select(address =>
        client.TryReadMemory(address, FieldRecord.Length, out var bytes) ? FieldRecord.Parse(bytes, maps) : null);

    /// <summary>How long the reader has to have been in doubt before it searches memory, save the first search of all.</summary>
    /// <remarks>
    /// <para>
    /// Found on 2026-09-21 in the emulator's own log. Walking out of a building in Ciudad Hauoli, the game unloaded and
    /// reloaded the field — «Unloading CRO FieldEffectCommon», loaded again 0.24 s later — and within 70 ms PermaLocke
    /// ran three searches of the 64 MB of linear heap and a burst of reads to check the hits. Azahar died in the middle
    /// of the burst. A change of map is exactly when the records disagree, so it was exactly when the reader searched:
    /// while the game was rearranging the memory it was searching.
    /// </para>
    /// <para>
    /// A change of map sorts itself out in a second or two, when the new records come up agreeing. A doubt that
    /// outlasts this is a stale record or a lost one, which is what searching is for. The very first search, at
    /// connection, does not wait: without it there is nothing to read at all, and it is the one that has always run.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan SettleBeforeSearch = TimeSpan.FromSeconds(5);

    private DateTimeOffset? _doubtSince;

    private bool Settled(DateTimeOffset now) =>
        _lastSearch == DateTimeOffset.MinValue || (_doubtSince is { } since && now - since >= SettleBeforeSearch);

    private bool ShouldSearch(DateTimeOffset now, bool disagreeing) =>
        _lastSearch == DateTimeOffset.MinValue
        || (disagreeing && now - _lastSearch >= SearchEveryWhileDisagreeing)
        || (now - _lastSearch >= SearchEvery
            && (_records.Count < 2 || (_unknownSince is { } since && now - since >= UnknownBeforeSearch)));

    // A fresh game may have no save when the first search runs. A new save supplies a useful
    // position pattern; don't leave that new evidence idle for the normal two-minute cooldown.
    private bool SavedSinceLastSearch(DateTimeOffset now)
    {
        if (now - _lastSearch < SearchEveryWhileDisagreeing || now - _lastSaveProbe < SaveProbeEvery)
            return false;

        _lastSaveProbe = now;
        if (saved.Load() is not { } game || ReferenceEquals(game.Save, _searchedSave))
            return false;

        logger.LogInformation("Nuevo guardado disponible: se vuelve a localizar la zona sin esperar dos minutos");
        return true;
    }
    /// <param name="current">What the records held just now, valid or not: the maps worth looking for siblings of.</param>
    private void Search(DateTimeOffset now, IEnumerable<FieldZone> current)
    {
        _lastSearch = now;
        var budget = SearchBudget;
        var found = new List<uint>();
        (int World, int Map)? savedMap = null;

        // Primero donde estaba al guardar: justo después de cargar es lo único que casa.
        var savedGame = saved.Load();
        _searchedSave = savedGame?.Save;
        _lastSaveProbe = now;
        if (savedGame is { } game)
        {
            var situation = game.Save.Data.Slice(game.Save.AllBlocks[1].Offset, 0x14);
            var pattern = FieldRecord.SavedPattern(situation);
            found.AddRange(client.SearchMemory(LinearHeap, LinearHeapSize, pattern, Mask(pattern.Length)));
            budget--;
            savedMap = (BitConverter.ToUInt16(situation[..2]), BitConverter.ToUInt16(situation.Slice(2, 2)));
        }

        // Y la firma del aterrizaje, que casa en cuanto se ha cambiado de mapa una vez. Paginada, pero con tope:
        // un patrón más común de lo medido no puede convertirse en una ráfaga (§114 ter).
        var from = LinearHeap;

        for (var page = 0; page < 3 && from < LinearHeap + LinearHeapSize; page++)
        {
            var hits = client.SearchMemory(from, LinearHeap + LinearHeapSize - from, FieldRecord.LandingPattern,
                Mask(FieldRecord.LandingPattern.Length));
            budget--;

            found.AddRange(hits.Select(hit => hit - (uint)FieldRecord.LandingPatternOffset));

            if (hits.Count < SearchCap)
            {
                break;
            }

            from = hits[^1] + 4;
        }

        var valid = Valid(found);
        // The first save can point at an old room. Live records nearby may already hold another
        // map and a non-identity rotation, matching neither the save nor the landing pattern.
        // Inspect only two already mapped pages around validated records (8 KB, never a heap sweep).
        valid = [.. valid.Concat(Nearby(valid)).DistinctBy(pair => pair.Address)];

        // Sin mayoría no se decide nada, y es lo normal si se conecta después de dar unos pasos -ni la partida
        // guardada ni el aterrizaje casan ya- o si en el grupo se ha colado el registro del mapa anterior. Se buscan
        // las hermanas de cada mapa que se ve ahora, como mucho dos, o del de la partida si no se ve ninguno (§118).
        if (FieldRecord.Resolve(valid.Select(pair => (FieldZone?)pair.Zone)) is null)
        {
            List<(int World, int Map)> targets = [.. valid.Select(pair => pair.Zone).Concat(current)
                .GroupBy(zone => (zone.World, zone.Map))
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .Take(2)];

            if (targets.Count == 0 && savedMap is { } fromSave)
            {
                targets.Add(fromSave);
            }

            var pending = new Queue<(int World, int Map)>(targets);
            while (budget > 0 && pending.TryDequeue(out var target))
            {
                var (world, map) = target;
                var (pattern, mask) = FieldRecord.SiblingPattern(world, map);
                var cursor = _siblingCursors.GetValueOrDefault(target, LinearHeap);
                var hits = client.SearchMemory(cursor, LinearHeap + LinearHeapSize - cursor, pattern, mask);
                budget--;
                logger.LogInformation("Buscando más registros del mapa {Map} (mundo {World}) desde 0x{From:X8}: {Count} candidatos",
                    map, world, cursor, hits.Count);
                valid = [.. valid.Concat(Valid(hits)).DistinctBy(pair => pair.Address)];

                var next = hits.Count == SearchCap ? hits.Max() + 4 : LinearHeap + LinearHeapSize;
                if (next > cursor && next < LinearHeap + LinearHeapSize)
                {
                    _siblingCursors[target] = next;
                    pending.Enqueue(target);
                }
                else
                    _siblingCursors.Remove(target);
            }
            if (pending.Count > 0)
                logger.LogInformation("Búsqueda de posición limitada a {Budget} consultas; las páginas pendientes se retoman en la próxima búsqueda", SearchBudget);
        }

        // Lo que ya se tenía no se tira: una búsqueda justo al salir de un combate o de un vuelo no encuentra nada
        // válido porque el campo aún se está cargando, y vaciar la lista dejaba sin zona dos minutos.
        _records = [.. valid.Select(pair => pair.Address).Concat(_records).Distinct().Take(MaxRecords)];

        logger.LogInformation("Registros de posición localizados: {Count} ({Addresses})", _records.Count,
            Addresses(_records));

        if (FieldRecord.Resolve(Read(_records)) is not null)
        {
            Remember(_records);
        }
    }

    /// <summary>Every record written out, address included, for when they disagree.</summary>
    private IEnumerable<string> Describe(IEnumerable<uint> addresses) =>
        addresses.Select(address => client.TryReadMemory(address, FieldRecord.Length, out var bytes)
            ? string.Create(CultureInfo.InvariantCulture, $"0x{address:X8}  {FieldRecord.Describe(bytes, maps)}")
            : string.Create(CultureInfo.InvariantCulture, $"0x{address:X8}  no se puede leer"));

    private List<(uint Address, FieldZone Zone)> Valid(IEnumerable<uint> addresses) =>
        [.. addresses
            .Distinct()
            .Select(address => (Address: address,
                Zone: client.TryReadMemory(address, FieldRecord.Length, out var bytes) ? FieldRecord.Parse(bytes, maps) : null))
            .Where(pair => pair.Zone is not null)
            .Select(pair => (pair.Address, pair.Zone!))];

    private List<(uint Address, FieldZone Zone)> Nearby(List<(uint Address, FieldZone Zone)> anchors)
    {
        var result = new List<(uint Address, FieldZone Zone)>();
        foreach (var page in anchors.Select(pair => pair.Address & ~(PageSize - 1)).Distinct().Take(2))
        {
            if (page < LinearHeap || page >= LinearHeap + LinearHeapSize
                || !client.TryReadMemory(page, (int)PageSize, out var bytes)) continue;
            for (var offset = 0; offset + FieldRecord.Length <= bytes.Length; offset += 4)
                if (FieldRecord.Parse(bytes.AsSpan(offset, FieldRecord.Length), maps) is { } zone)
                    result.Add((page + (uint)offset, zone));
        }
        return result;
    }

    /// <summary>The addresses that last made a zone, read once from disk. Never used without two agreeing.</summary>
    private List<uint> Remembered()
    {
        if (_remembered is not null)
        {
            return _remembered;
        }

        try
        {
            _remembered = File.Exists(knownAddressesPath)
                ? [.. File.ReadAllLines(knownAddressesPath)
                    .Select(line => uint.TryParse(line.Trim(), NumberStyles.HexNumber, null, out var address) ? address : 0)
                    .Where(address => address >= LinearHeap && address < LinearHeap + LinearHeapSize)
                    .Take(MaxRecords)]
                : [];
        }
        catch (IOException)
        {
            _remembered = [];
        }

        return _remembered;
    }

    private void Remember(List<uint> addresses)
    {
        _remembered = [.. addresses];

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(knownAddressesPath)!);
            File.WriteAllLines(knownAddressesPath, addresses.Select(address => address.ToString("X8")));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "No se pudieron guardar las direcciones de los registros de posición");
        }
    }

    private static string Addresses(IEnumerable<uint> addresses) =>
        string.Join(", ", addresses.Select(address => $"0x{address:X8}"));

    private static byte[] Mask(int length) => Enumerable.Repeat((byte)0xFF, length).ToArray();
}
