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
    private readonly Dictionary<int, string> _taught = [];

    public MachineItemLookup(IItemLookup inner, AzaharInstallation azahar,
        ILogger<MachineItemLookup> logger, string language = "es")
    {
        _inner = inner;

        try
        {
            Build(azahar, language);
        }
        catch (Exception ex)
        {
            // Nunca impide arrancar: sin esto las MT salen por su numero, que es como salian antes.
            logger.LogWarning(ex, "No se ha podido leer qué enseña cada MT del mundo instalado");
        }
    }

    public string GetName(int itemId) =>
        _taught.TryGetValue(itemId, out var move)
            ? $"{_inner.GetName(itemId)} · {move}"
            : _inner.GetName(itemId);

    private void Build(AzaharInstallation azahar, string language)
    {
        var location = azahar.Locate(AppContext.BaseDirectory);
        var mod = AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId);
        var code = Path.Combine(mod, "exefs", "code.bin");

        if (!File.Exists(code))
        {
            return;
        }

        var bytes = File.ReadAllBytes(code);
        var at = MachineTable.Find(bytes, MachineRandomizer.Hidden);

        if (at < 0)
        {
            return;
        }

        var moves = MachineTable.Read(bytes, at);
        var strings = GameInfo.GetStrings(language);
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
                _taught[machines[number]] = strings.movelist[move];
            }
        }
    }
}
