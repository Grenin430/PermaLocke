namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// The list of which move each TM teaches, which lives in <c>code.bin</c> and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// It is not in the RomFS. Not in <c>personal</c>, not in the move table, not in any GARC: the
/// hundred move ids are a plain array inside the executable, followed by the seven HMs. That is
/// why nothing PermaLocke did ever changed a TM — the randomizer moves TMs <em>around</em>, into
/// different Poké Balls and different shops, but TM54 stayed False Swipe because that fact is in
/// the binary.
/// </para>
/// <para>
/// The array is found by <b>searching for it</b> and never by a written-down offset. A mod ships
/// its own <c>code.bin</c> and the table sits somewhere else in it; hardcoding a number measured on
/// one build would silently patch whatever happened to live there on another. Searching is also
/// what makes the find verifiable: on the gen 8-9 expansion's executable the pattern matches
/// <b>exactly once</b> in 5,9 MB.
/// </para>
/// </remarks>
public static class MachineTable
{
    /// <summary>How many TMs the game has.</summary>
    public const int Count = 100;

    /// <summary>Highest move id Ultra Moon knows, used to sanity check a candidate table.</summary>
    public const int LastCartridgeMove = 729;

    /// <summary>
    /// The first four entries of the cartridge's list: Work Up, Dragon Claw, Psyshock, Calm Mind.
    /// </summary>
    /// <remarks>
    /// Four is enough to be unique — measured, one hit in the whole executable — and short enough
    /// that it keeps matching after the table has been randomized once, which it must: the module
    /// has to be able to find the table again to verify what it wrote.
    /// <para>
    /// It does not, of course, survive being overwritten. That is why <see cref="Find"/> also
    /// accepts a table it has already written, by shape rather than by content.
    /// </para>
    /// </remarks>
    public static readonly int[] Signature = [526, 337, 473, 347];

    /// <summary>
    /// Where the hundred move ids start inside <paramref name="code"/>, or -1.
    /// </summary>
    /// <remarks>
    /// Two passes. First the exact signature, which is what identifies an untouched table beyond
    /// doubt. If that fails — because this executable has already been randomized — it falls back
    /// to shape: a run of a hundred u16 that are all real move ids, all different, and followed by
    /// the seven HM ids, which the module never touches and which therefore stay put as a marker.
    /// </remarks>
    public static int Find(ReadOnlySpan<byte> code, ReadOnlySpan<int> hmMarker)
    {
        var exact = FindSignature(code);

        return exact >= 0 ? exact : FindByShape(code, hmMarker);
    }

    private static int FindSignature(ReadOnlySpan<byte> code)
    {
        for (var at = 0; at + (Signature.Length * 2) <= code.Length; at += 2)
        {
            var ok = true;

            for (var i = 0; i < Signature.Length && ok; i++)
            {
                ok = At(code, at + (i * 2)) == Signature[i];
            }

            if (ok)
            {
                return at;
            }
        }

        return -1;
    }

    private static int FindByShape(ReadOnlySpan<byte> code, ReadOnlySpan<int> hmMarker)
    {
        for (var at = 0; at + ((Count + hmMarker.Length) * 2) <= code.Length; at += 2)
        {
            if (!LooksLikeTable(code, at, hmMarker))
            {
                continue;
            }

            return at;
        }

        return -1;
    }

    private static bool LooksLikeTable(ReadOnlySpan<byte> code, int at, ReadOnlySpan<int> hmMarker)
    {
        var seen = new HashSet<int>(Count);

        for (var i = 0; i < Count; i++)
        {
            var move = At(code, at + (i * 2));

            if (move is < 1 or > LastCartridgeMove || !seen.Add(move))
            {
                return false;
            }
        }

        for (var i = 0; i < hmMarker.Length; i++)
        {
            if (At(code, at + ((Count + i) * 2)) != hmMarker[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The hundred move ids at <paramref name="at"/>.</summary>
    public static int[] Read(ReadOnlySpan<byte> code, int at)
    {
        var moves = new int[Count];
        for (var i = 0; i < Count; i++) moves[i] = At(code, at + (i * 2));
        return moves;
    }

    /// <summary>Writes the hundred move ids back, in place and without changing any length.</summary>
    public static void Write(Span<byte> code, int at, IReadOnlyList<int> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        if (moves.Count != Count)
        {
            throw new ArgumentException($"Son {Count} MT y se han dado {moves.Count}.", nameof(moves));
        }

        for (var i = 0; i < Count; i++)
        {
            BitConverter.TryWriteBytes(code[(at + (i * 2))..], (ushort)moves[i]);
        }
    }

    private static int At(ReadOnlySpan<byte> code, int at) => BitConverter.ToUInt16(code[at..]);
}
