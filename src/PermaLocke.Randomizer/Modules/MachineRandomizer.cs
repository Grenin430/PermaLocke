using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Machines">TMs whose move changed.</param>
/// <param name="Offset">Where the table turned out to be, for the record.</param>
public sealed record MachineResult(int Machines, int Offset);

/// <summary>
/// Shuffles which move each TM teaches, inside the game's own executable.
/// </summary>
/// <remarks>
/// <para>
/// It exists because of a report from play: after the second trial Kukui hands over a TM and it was
/// False Swipe, exactly as the cartridge has it. PermaLocke moved TMs <em>around</em> — different
/// Poké Balls, different shops — but never changed what one teaches, because that list is not in
/// the RomFS at all. It is a hundred move ids inside <c>code.bin</c>.
/// </para>
/// <para>
/// The hundred are <b>shuffled among themselves</b> rather than drawn from the whole move list, the
/// same rule the ground items follow. Two reasons: every TM keeps teaching something worth a TM
/// slot, and the set stays exactly the hundred the game shipped, so no TM ever teaches a move the
/// player could not otherwise get, and none is lost.
/// </para>
/// <para>
/// It only works on a game whose <c>code.bin</c> PermaLocke has — which today means a base layer
/// that ships one. The cartridge keeps its executable in the ExeFS, which this project does not
/// extract, so on plain vanilla the module reports that it did nothing rather than pretending.
/// </para>
/// </remarks>
public sealed class MachineRandomizer(RandomizerOptions options)
{
    /// <summary>The seven HMs, which sit right after the TMs and are never touched.</summary>
    /// <remarks>
    /// They double as the marker that identifies the table once the TMs have been shuffled and the
    /// signature no longer matches. Randomizing them would be worse than pointless: Surf and Fly
    /// are how the player crosses the map, and a game that cannot be crossed is not a harder game.
    /// </remarks>
    private static readonly int[] Hidden = [15, 19, 57, 70, 127, 249, 291];

    public async Task<MachineResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        string? baseLayerExefs, CancellationToken ct = default)
    {
        if (!options.RandomizeMachines || baseLayerExefs is null)
        {
            return new MachineResult(0, -1);
        }

        var source = Path.Combine(baseLayerExefs, "code.bin");

        if (!File.Exists(source))
        {
            return new MachineResult(0, -1);
        }

        var destination = Path.Combine(
            Path.GetDirectoryName(mod.RomFsDirectory)!, "exefs", "code.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var code = await File.ReadAllBytesAsync(source, ct);
        var at = MachineTable.Find(code, Hidden);

        if (at < 0)
        {
            throw new InvalidDataException(
                "No encuentro la tabla de MT dentro de code.bin. No se toca el ejecutable.");
        }

        var moves = MachineTable.Read(code, at);
        var shuffled = moves.ToList();

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        MachineTable.Write(code, at, shuffled);
        await File.WriteAllBytesAsync(destination, code, ct);

        // Se relee del fichero escrito, que es lo unico que convierte «escrito» en «hecho».
        var back = await File.ReadAllBytesAsync(destination, ct);
        var written = MachineTable.Read(back, at);

        if (!written.SequenceEqual(shuffled))
        {
            throw new InvalidDataException("La tabla de MT no quedó como se escribió.");
        }

        // Y la comprobacion que importa: siguen siendo las MISMAS cien, solo que en otro orden. Si
        // alguna se hubiera perdido o duplicado, un movimiento dejaria de poder ensenarse a nadie.
        if (written.Length != moves.Length || written.OrderBy(m => m).SequenceEqual(moves.OrderBy(m => m)) is false)
        {
            throw new InvalidDataException(
                "Las MT ya no son el mismo conjunto que traía el juego: se ha perdido o repetido alguna.");
        }

        var changed = written.Where((move, i) => move != moves[i]).Count();
        return new MachineResult(changed, at);
    }
}
