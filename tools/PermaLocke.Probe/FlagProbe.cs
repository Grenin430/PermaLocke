using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Finds which event flag or counter of the save means "trial cleared", "sticker found" and the
/// rest, by comparing two snapshots of the same run.
/// </summary>
/// <remarks>
/// <para>
/// Ultra Moon keeps <b>4960 event flags and 1000 counters</b>, and nothing in them is labelled.
/// Guessing which one is which would be exactly the kind of nearly-right mapping that rule 3
/// forbids: it would award points for the wrong thing and look perfectly fine doing it.
/// </para>
/// <para>
/// So it is not guessed, it is <b>measured</b>. Dump before, play until the thing happens, dump
/// after, and the difference is short enough to read: the flags that turned on and the counters
/// that moved. That pins the number, and from then on the achievement counts itself.
/// </para>
/// <para>
/// The dump is a plain text file on purpose. It is evidence: it can be kept, compared later and
/// shown to somebody else.
/// </para>
/// </remarks>
public static class FlagProbe
{
    public static int Dump(string destination)
    {
        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(), AppContext.BaseDirectory);

        var path = save.Find();
        if (path is null)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine($"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            return 1;
        }

        if (save.IsGameLoaded())
        {
            Console.WriteLine("AVISO: el juego está abierto. Esto es lo último que guardaste, no lo de ahora.");
            Console.WriteLine("       Guarda dentro del juego antes de volcar, o el antes y el después serán iguales.");
        }

        var work = game.Blocks.EventWork;
        var text = new StringBuilder();

        text.AppendLine($"# volcado de banderas de PermaLocke");
        text.AppendLine($"# partida: {path}");
        text.AppendLine($"# fecha:   {DateTimeOffset.Now:O}");
        text.AppendLine($"# jugado:  {game.PlayTimeString}");
        text.AppendLine();
        text.AppendLine($"stamps={game.Blocks.Misc.Stamps}");
        text.AppendLine($"dinero={game.Blocks.Misc.Money}");
        text.AppendLine($"fama={game.Blocks.EventWork.Fame.First1}");
        text.AppendLine($"celulasZygarde={work.TotalZygardeCellCount}");

        foreach (var record in SaveRecordReader.Wanted)
        {
            text.AppendLine($"record[{record}]={game.Records.GetRecord(record)}");
        }

        text.AppendLine();
        var on = 0;
        for (var flag = 0; flag < work.EventFlagCount; flag++)
        {
            if (!work.GetEventFlag(flag))
            {
                continue;
            }

            text.AppendLine($"flag[{flag}]=1");
            on++;
        }

        text.AppendLine();
        var set = 0;
        for (var counter = 0; counter < work.EventWorkCount; counter++)
        {
            var value = work.GetWork(counter);
            if (value == 0)
            {
                continue;
            }

            text.AppendLine($"work[{counter}]={value}");
            set++;
        }

        File.WriteAllText(destination, text.ToString());

        Console.WriteLine($"Volcado en {destination}");
        Console.WriteLine($"  {on} banderas encendidas de {work.EventFlagCount}");
        Console.WriteLine($"  {set} contadores distintos de cero de {work.EventWorkCount}");

        // El tiempo jugado es la manera de ver de un vistazo hasta dónde llega el volcado. Si no
        // ha subido desde el anterior, es que la partida no se guardó y lo que se busca no está.
        Console.WriteLine($"  la partida lleva {game.PlayTimeString} jugados");
        Console.WriteLine($"  {game.Records.GetRecord(5)} combates contra entrenadores, " +
                          $"{game.Records.GetRecord(4)} contra salvajes, " +
                          $"{game.Records.GetRecord(6)} capturas");
        return 0;
    }

    /// <summary>
    /// Reads two dumps and prints what changed. Short by construction: between one trial and the
    /// next only a handful of things move.
    /// </summary>
    public static int Diff(string before, string after)
    {
        if (!File.Exists(before) || !File.Exists(after))
        {
            Console.WriteLine("Faltan uno o los dos volcados.");
            return 1;
        }

        var a = Read(before);
        var b = Read(after);

        // Las banderas y los contadores solo se escriben cuando no valen cero, así que una clave
        // que aparece o desaparece es un cambio de verdad. Los récords y los escalares se escriben
        // siempre, de modo que ahí una clave nueva significa que ha cambiado la LISTA que pide la
        // herramienta, no la partida. Mezclar las dos cosas hace leer un cambio donde no lo hay.
        var appeared = b.Where(kv => !a.ContainsKey(kv.Key) && IsOmittedWhenZero(kv.Key)).ToList();
        var gone = a.Where(kv => !b.ContainsKey(kv.Key) && IsOmittedWhenZero(kv.Key)).ToList();
        var moved = b.Where(kv => a.TryGetValue(kv.Key, out var old) && old != kv.Value).ToList();
        var toolOnly = b.Where(kv => !a.ContainsKey(kv.Key) && !IsOmittedWhenZero(kv.Key))
            .Concat(a.Where(kv => !b.ContainsKey(kv.Key) && !IsOmittedWhenZero(kv.Key)))
            .ToList();

        Console.WriteLine($"ANTES:   {before}   ({a.PlayTime} jugados)");
        Console.WriteLine($"DESPUÉS: {after}   ({b.PlayTime} jugados)");
        Console.WriteLine();

        if (a.PlayTime == b.PlayTime)
        {
            Console.WriteLine("AVISO: los dos volcados tienen el mismo tiempo jugado, así que son la");
            Console.WriteLine("       misma partida guardada. Guarda DENTRO del juego y vuelve a volcar.");
            Console.WriteLine();
        }

        Report("SE HAN ENCENDIDO", appeared);
        Report("SE HAN APAGADO", gone);
        Report("HAN CAMBIADO DE VALOR", moved.Select(kv =>
            new KeyValuePair<string, string>(kv.Key, $"{a[kv.Key]} -> {kv.Value}")).ToList());
        Report("CLAVES QUE SOLO ESTÁN EN UNO DE LOS DOS (cambió la herramienta, no el juego)", toolOnly);

        if (appeared.Count + gone.Count + moved.Count == 0)
        {
            Console.WriteLine("No ha cambiado nada. ¿Guardaste dentro del juego entre los dos volcados?");
        }

        return 0;
    }

    /// <summary>
    /// True for the keys the dump leaves out when they are zero, which are the only ones where
    /// "appears" and "disappears" mean something happened in the game.
    /// </summary>
    private static bool IsOmittedWhenZero(string key) =>
        key.StartsWith("flag[", StringComparison.Ordinal) ||
        key.StartsWith("work[", StringComparison.Ordinal);

    private static void Report(string title, IReadOnlyList<KeyValuePair<string, string>> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        Console.WriteLine($"== {title} ({items.Count}) ==");
        foreach (var (key, value) in items.Take(200))
        {
            Console.WriteLine($"  {key} = {value}");
        }

        if (items.Count > 200)
        {
            Console.WriteLine($"  ... y {items.Count - 200} más");
        }

        Console.WriteLine();
    }

    private static Snapshot Read(string path)
    {
        var lines = File.ReadAllLines(path);

        var playTime = lines
            .FirstOrDefault(line => line.StartsWith("# jugado:", StringComparison.Ordinal))
            ?.Split(':', 2)[1].Trim() ?? "?";

        var values = lines
            .Where(line => line.Contains('=') && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        return new Snapshot(playTime, values);
    }

    /// <summary>One dump read back: its values plus the play time that says how far it reaches.</summary>
    private sealed record Snapshot(string PlayTime, Dictionary<string, string> Values)
    {
        public string this[string key] => Values[key];

        public bool ContainsKey(string key) => Values.ContainsKey(key);

        public bool TryGetValue(string key, out string value) => Values.TryGetValue(key, out value!);

        public IEnumerable<KeyValuePair<string, string>> Where(
            Func<KeyValuePair<string, string>, bool> predicate) => Values.Where(predicate);
    }
}
