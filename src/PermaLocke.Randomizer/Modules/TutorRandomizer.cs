using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;

namespace PermaLocke.Randomizer.Modules;

/// <param name="At">Where the list was found, or -1 when it was not looked for.</param>
public sealed record TutorResult(int Changed, int Total, int At);

/// <summary>
/// Shuffles what the move tutors teach.
/// </summary>
/// <remarks>
/// <para>
/// Like the TMs, and for the same reason: the list is not in the RomFS at all, it is sixty-seven
/// move ids inside <c>code.bin</c>. So it only works on a world whose executable PermaLocke has —
/// today that means a base layer with an <c>exefs</c>, which the expansion mod brings.
/// </para>
/// <para>
/// It <b>shuffles</b> rather than drawing fresh moves, which keeps a guarantee that matters here
/// more than variety: every move a tutor could teach is still taught by some tutor. Drawing at
/// random could lose one, and a tutor move that is gone is gone — unlike a TM, there is no second
/// copy lying on the ground.
/// </para>
/// <para>
/// It reads <c>code.bin</c> from the mod when the mod already has one, and only falls back to the
/// base layer otherwise. That is not a detail: the TM module writes the same file, and reading the
/// base layer unconditionally would throw its work away — §47, where <c>Stage()</c> copied vanilla
/// over what the previous module had just patched and the report cheerfully said it had worked.
/// </para>
/// </remarks>
public sealed class TutorRandomizer(RandomizerOptions options)
{
    public async Task<TutorResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        string? baseLayerExefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(mod);

        if (!options.RandomizeTutors || baseLayerExefs is null)
        {
            return new TutorResult(0, 0, -1);
        }

        var destination = Path.Combine(
            Path.GetDirectoryName(mod.RomFsDirectory)!, "exefs", "code.bin");

        // Lo ya parcheado manda sobre la capa base.
        var source = File.Exists(destination) ? destination : Path.Combine(baseLayerExefs, "code.bin");

        if (!File.Exists(source))
        {
            return new TutorResult(0, 0, -1);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var code = await File.ReadAllBytesAsync(source, ct);
        var at = TutorTable.Find(code);

        if (at < 0)
        {
            throw new InvalidDataException(
                "No encuentro la lista de tutores dentro de code.bin. No se toca el ejecutable.");
        }

        var moves = TutorTable.Read(code, at);
        var shuffled = moves.ToList();

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        TutorTable.Write(code, at, shuffled);
        await File.WriteAllBytesAsync(destination, code, ct);

        // Releido del fichero escrito, que es lo unico que convierte «escrito» en «hecho».
        var back = await File.ReadAllBytesAsync(destination, ct);
        var written = TutorTable.Read(back, at);

        if (!written.SequenceEqual(shuffled))
        {
            throw new InvalidDataException("La lista de tutores no quedó como se escribió.");
        }

        // Y la que importa: siguen siendo los MISMOS movimientos, solo que repartidos de otro modo.
        if (written.Length != moves.Length
            || !written.OrderBy(m => m).SequenceEqual(moves.OrderBy(m => m)))
        {
            throw new InvalidDataException(
                "Los tutores ya no enseñan el mismo conjunto: se ha perdido o repetido alguno.");
        }

        return new TutorResult(written.Where((move, i) => move != moves[i]).Count(), written.Length, at);
    }
}
