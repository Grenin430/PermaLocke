using System.Globalization;
using System.Text.Json;

namespace PermaLocke.GameLink;

/// <summary>
/// The ability PermaLocke gave each Pokémon it wrote into a save, by PID (2026-10-08).
/// </summary>
/// <remarks>
/// <para>
/// The gacha, the wonder trade, the nursery and the roulette's ability faces write an ability that is not the one the
/// species' table gives. The game keeps it as it finds it, but <c>CoreParam::ChangeFormNo</c> (Mega Evolution and every
/// other form that reverts after a battle) and <c>ChangeMonsNo</c> (evolution) <b>recompute it from the table</b> by the
/// ability slot: a Heracross from the gacha with Espada Indómita came out of a Mega Evolution with Gran Encanto (§237).
/// There is no room in <c>code.bin</c> to teach the game otherwise, so PermaLocke remembers what it wrote and puts it back
/// (<see cref="SaveAbilityKeeper"/>).
/// </para>
/// <para>
/// One small JSON file for the whole installation, keyed by the 32 bit PID: a save belongs to one run at a time, and a PID
/// seen in another run's save is another Pokémon only by a one in four thousand million chance. Static and unconfigured
/// by default, so tests that build Pokémon write nowhere.
/// </para>
/// </remarks>
public static class AbilityLedger
{
    private static readonly object Gate = new();
    private static string? _path;
    private static Dictionary<uint, int>? _map;

    /// <summary>Where the ledger lives, or null for none (nothing is recorded).</summary>
    public static void Configure(string? path)
    {
        lock (Gate)
        {
            _path = path;
            _map = null;
        }
    }

    /// <summary>Notes that this Pokémon was given this ability.</summary>
    public static void Record(uint pid, int ability)
    {
        if (pid == 0 || ability <= 0) return;

        lock (Gate)
        {
            if (_path is null) return;

            var map = Load();
            if (_readFailed) return;
            if (map.TryGetValue(pid, out var known) && known == ability) return;

            map[pid] = ability;
            Save(map);
        }
    }

    /// <summary>Adds what is not yet known; what was recorded when the ability was written is never replaced.</summary>
    public static int Merge(IReadOnlyDictionary<uint, int> found)
    {
        lock (Gate)
        {
            if (_path is null) return 0;

            var map = Load();
            if (_readFailed) return 0;
            var added = 0;

            foreach (var (pid, ability) in found)
            {
                if (pid != 0 && ability > 0 && map.TryAdd(pid, ability)) added++;
            }

            if (added > 0) Save(map);
            return added;
        }
    }

    /// <summary>A copy of what is recorded.</summary>
    public static IReadOnlyDictionary<uint, int> Snapshot()
    {
        lock (Gate)
        {
            return _path is null ? new Dictionary<uint, int>() : new Dictionary<uint, int>(Load());
        }
    }

    private static Dictionary<uint, int> Load()
    {
        if (_map is not null) return _map;

        var map = new Dictionary<uint, int>();
        _readFailed = false;

        try
        {
            if (File.Exists(_path))
            {
                foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_path)) ?? [])
                {
                    if (uint.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid)) map[pid] = value;
                }
            }
        }
        catch (JsonException)
        {
            // Un fichero roto es un libro vacío: se guarda aparte y se vuelve a llenar con lo que se escriba y con lo que dicen los eventos.
            try { File.Copy(_path!, _path + ".roto", overwrite: true); } catch (IOException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se ha podido leer (un antivirus, un bloqueo): no se da por vacío ni se escribe encima; se reintenta en la próxima.
            _readFailed = true;
            return map;
        }

        return _map = map;
    }

    private static bool _readFailed;

    private static void Save(Dictionary<uint, int> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var text = JsonSerializer.Serialize(map.ToDictionary(pair => pair.Key.ToString("X8", CultureInfo.InvariantCulture), pair => pair.Value));
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, text);
            File.Move(temporary, _path!, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin libro, la habilidad sigue escrita en la partida; solo no se podrá restaurar.
        }
    }
}
