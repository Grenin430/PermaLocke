using System.IO;
using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Output;

namespace PermaLocke.App.Services;

/// <summary>
/// Item names, with a TM saying which move it teaches <b>in the world being played</b>.
/// </summary>
/// <remarks>
/// <para>
/// «MT56» tells a player nothing at all here, because the machines are randomized: MT56 is not
/// Fling any more, it is whatever this world put there. The name has to be read from the installed
/// mod's own <c>code.bin</c>, and reading it from PKHeX's table instead would print a move that
/// confidently is not the one the machine teaches — worse than the number alone.
/// </para>
/// <para>
/// It wraps the plain lookup instead of replacing it, so every screen that names an item gains
/// this at once: the roulette that hands out and takes away machines, the shop, the prizes. And it
/// degrades to the bare name whenever the world cannot be read — no mod installed, a table that
/// moved, a game that is not ours — because a TM with no move named is a small loss and a TM with
/// the wrong move named is a lie.
/// </para>
/// <para>
/// Which items are machines is asked of the cartridge by name, never by a range: the hundred TMs
/// are <b>not</b> contiguous (§62), and a range written down here would silently miss the last
/// eight, which is a bug this project has already paid for once.
/// </para>
/// </remarks>
public sealed class MachineItemLookup : IItemLookup
{
    private readonly IItemLookup _inner;
    private readonly AzaharInstallation _azahar;
    private readonly ILogger<MachineItemLookup> _logger;
    private readonly string _language;
    private Dictionary<int, string> _taught = [];

    /// <summary>The mod's <c>code.bin</c> the names were read from, as its size and write time, and when it was last looked at.</summary>
    private (long Length, DateTime Written) _source;
    private DateTime _checked = DateTime.MinValue;
    private bool _built;

    /// <summary>
    /// How often the file is looked at again. Looked at again at all because the player installs a new world while PermaLocke is
    /// open: names read at start-up were the old world's, and an item's animation said «MT01 · Hiperrayo» over a machine that
    /// teaches Atracción (2026-10-07).
    /// </summary>
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(5);

    public MachineItemLookup(IItemLookup inner, AzaharInstallation azahar,
        ILogger<MachineItemLookup> logger, string language = "es")
    {
        _inner = inner;
        _azahar = azahar;
        _logger = logger;
        _language = language;
        Refresh();
    }

    public string GetName(int itemId)
    {
        Refresh();

        return _taught.TryGetValue(itemId, out var move)
            ? $"{_inner.GetName(itemId)} · {move}"
            : _inner.GetName(itemId);
    }

    private void Refresh()
    {
        if (DateTime.UtcNow - _checked < Recheck) return;
        _checked = DateTime.UtcNow;

        try
        {
            var location = _azahar.Locate(AppContext.BaseDirectory);
            var code = Path.Combine(AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId), "exefs", "code.bin");
            var info = new FileInfo(code);
            var stamp = info.Exists ? (info.Length, info.LastWriteTimeUtc) : (0L, DateTime.MinValue);

            if (stamp == _source && _built) return;
            _source = stamp;
            _taught = Build(code);
            _built = true;
        }
        catch (Exception ex)
        {
            // Nunca impide arrancar: sin esto las MT salen por su numero, que es como salian antes.
            _logger.LogWarning(ex, "No se ha podido leer qué enseña cada MT del mundo instalado");
        }
    }

    private Dictionary<int, string> Build(string code)
    {
        var taught = new Dictionary<int, string>();

        if (!File.Exists(code))
        {
            return taught;
        }

        var bytes = File.ReadAllBytes(code);
        var at = MachineTable.Find(bytes, MachineRandomizer.Hidden);

        if (at < 0)
        {
            return taught;
        }

        var moves = MachineTable.Read(bytes, at);
        var strings = GameInfo.GetStrings(_language);
        var machines = ShopTable.TechnicalMachines(strings.itemlist);

        // Las MT van en orden por su numero, y ese orden es el de la tabla: la enesima MT del
        // cartucho ensena el enesimo movimiento de la lista. Emparejarlas por id seria suponer que
        // los ids son seguidos, y no lo son.
        for (var number = 0; number < machines.Length && number < moves.Length; number++)
        {
            var move = moves[number];

            if (move > 0 && move < strings.movelist.Length
                && !string.IsNullOrWhiteSpace(strings.movelist[move]))
            {
                taught[machines[number]] = strings.movelist[move];
            }
        }

        return taught;
    }
}
