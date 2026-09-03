namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// The move tutors' list, which lives in <c>code.bin</c> and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// Found because a player asked why the Mantine Surf stall on Kantai Beach was not randomised. It
/// is not a shop: <c>Shop.cro</c> holds twenty-eight inventories and none of them is this. Four of
/// the sixteen things it sells — «Cede Paso», «Telequinesis», «Levitón», «Imitación» — are not TMs
/// in generation seven at all, which is what gave it away: it is a <b>tutor</b>.
/// </para>
/// <para>
/// Searching the mod's 2.5 GB of RomFS and its <c>code.bin</c> for those ids as a sequence found
/// nothing, in four different layouts. What found it was looking for a <b>cluster</b> instead: all
/// fifteen known ids inside one window of sixty-four. The screen order is not the storage order,
/// so any search that assumed it was could not have worked.
/// </para>
/// <para>
/// Measured on the expansion mod at <c>0x4E6860</c>: sixty-seven move ids, every one distinct,
/// with out-of-range values immediately before and after. Kantai's sixteen are a subset scattered
/// through it, so this is every tutor in the game and each stall shows its own slice.
/// </para>
/// </remarks>
public static class TutorTable
{
    /// <summary>Highest move id gen 7 can hold, so anything above ends the run.</summary>
    private const int LastMove = 919;

    /// <summary>How long a run has to be before it can be the tutor list.</summary>
    /// <remarks>
    /// A range and not the measured sixty-seven: the cartridge and the expansion mod need not
    /// agree on how many tutor moves there are, and pinning the number would make this work on
    /// exactly one world — the §MachineTable lesson, where a hardcoded offset did the same.
    /// </remarks>
    private const int Shortest = 40;

    private const int Longest = 160;

    /// <summary>
    /// Moves the list has to contain, read off the Kantai Beach stall in the running game.
    /// </summary>
    /// <remarks>
    /// The anchor, and it is the whole reason this is a measurement rather than a guess. A run of
    /// distinct plausible ids is not rare in an executable; a run of distinct plausible ids that
    /// happens to contain these fifteen is.
    /// </remarks>
    public static readonly int[] Anchors =
        [231, 180, 495, 202, 235, 502, 356, 446, 334, 477, 393, 340, 272, 7, 352];

    /// <summary>Where the list starts, or -1 when this executable does not carry a recognisable one.</summary>
    public static int Find(ReadOnlySpan<byte> code)
    {
        for (var at = 0; at + 2 <= code.Length; at += 2)
        {
            var length = RunLength(code, at);

            if (length < Shortest || length > Longest)
            {
                // Se salta el tramo entero: dentro de una tirada larga no empieza otra distinta.
                at += Math.Max(0, length - 1) * 2;
                continue;
            }

            if (StartsHere(code, at) && Holds(code, at, length))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>Reads the list at a known offset.</summary>
    public static int[] Read(ReadOnlySpan<byte> code, int at)
    {
        var length = RunLength(code, at);
        var moves = new int[length];

        for (var i = 0; i < length; i++)
        {
            moves[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(code[(at + (i * 2))..]);
        }

        return moves;
    }

    /// <summary>Writes the list back, refusing anything that would change its length.</summary>
    public static void Write(Span<byte> code, int at, IReadOnlyList<int> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var length = RunLength(code, at);

        if (moves.Count != length)
        {
            throw new ArgumentException(
                $"La lista de tutores tiene {length} entradas y se han dado {moves.Count}. "
                + "Cambiar su tamaño movería el código que hay detrás.", nameof(moves));
        }

        foreach (var move in moves)
        {
            if (move is < 1 or > LastMove)
            {
                throw new ArgumentOutOfRangeException(nameof(moves), move,
                    $"El movimiento {move} no cabe en la lista de tutores (1..{LastMove}).");
            }
        }

        for (var i = 0; i < moves.Count; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                code[(at + (i * 2))..], (ushort)moves[i]);
        }
    }

    /// <summary>How many plausible, distinct move ids run from here.</summary>
    private static int RunLength(ReadOnlySpan<byte> code, int at)
    {
        var seen = new HashSet<int>();
        var length = 0;

        while (at + (length * 2) + 2 <= code.Length && length <= Longest)
        {
            var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                code[(at + (length * 2))..]);

            // Todos distintos: un tutor que enseñara dos veces el mismo movimiento no es una lista
            // de tutores, y ese requisito es lo que descarta casi todo el ejecutable.
            if (value is < 1 or > LastMove || !seen.Add(value))
            {
                break;
            }

            length++;
        }

        return length;
    }

    /// <summary>True when the value before this one is not itself part of the run.</summary>
    private static bool StartsHere(ReadOnlySpan<byte> code, int at)
    {
        if (at < 2)
        {
            return true;
        }

        var before = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(code[(at - 2)..]);
        return before is < 1 or > LastMove;
    }

    private static bool Holds(ReadOnlySpan<byte> code, int at, int length)
    {
        var moves = new HashSet<int>();

        for (var i = 0; i < length; i++)
        {
            moves.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                code[(at + (i * 2))..]));
        }

        return Anchors.All(moves.Contains);
    }
}
