using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Finding the move tutors' list inside an executable, without being told where it is.
/// </summary>
/// <remarks>
/// The offset measured on the expansion mod is <c>0x4E6860</c>, and it is deliberately not written
/// into the code: the cartridge and the mod carry different executables and the list will not sit
/// in the same place in both. Same lesson as the TM table.
/// </remarks>
public sealed class TutorTableTests
{
    /// <summary>An executable with the list buried in it, at a known offset.</summary>
    private static byte[] Fake(int at, IEnumerable<int> moves, int size = 4096)
    {
        var code = new byte[size];

        // Ruido que NO puede confundirse con la lista: valores fuera del rango de movimientos.
        for (var i = 0; i < size; i += 2)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                code.AsSpan(i), (ushort)(60000 + (i % 500)));
        }

        var i2 = at;
        foreach (var move in moves)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(code.AsSpan(i2), (ushort)move);
            i2 += 2;
        }

        return code;
    }

    /// <summary>The real list, as measured: the anchors plus enough others to reach length.</summary>
    private static int[] Measured()
    {
        var moves = TutorTable.Anchors.ToList();

        for (var move = 600; moves.Count < 67; move++)
        {
            if (!moves.Contains(move))
            {
                moves.Add(move);
            }
        }

        return [.. moves];
    }

    [Fact]
    public void It_finds_the_list_without_being_told_where()
    {
        var moves = Measured();
        var code = Fake(0x400, moves);

        Assert.Equal(0x400, TutorTable.Find(code));
        Assert.Equal(moves, TutorTable.Read(code, 0x400));
    }

    /// <summary>
    /// A run of plausible ids that does not hold the anchors is not the list.
    /// </summary>
    /// <remarks>
    /// This is the test that matters. An executable is full of runs of small distinct numbers, and
    /// without the anchors this would happily point at one of them and patch it — writing move ids
    /// into the middle of some other table, which is exactly the class of failure §52 exists for.
    /// </remarks>
    [Fact]
    public void A_run_of_plain_numbers_is_not_the_list()
    {
        var decoys = Enumerable.Range(300, 67).ToArray();

        Assert.Equal(-1, TutorTable.Find(Fake(0x400, decoys)));
    }

    /// <summary>Too short to be it, even holding every anchor.</summary>
    [Fact]
    public void A_handful_of_the_right_moves_is_not_the_list_either()
    {
        Assert.Equal(-1, TutorTable.Find(Fake(0x400, TutorTable.Anchors)));
    }

    /// <summary>Writing keeps the length, because the code behind it must not move.</summary>
    [Fact]
    public void It_refuses_to_change_the_length()
    {
        var moves = Measured();
        var code = Fake(0x400, moves);

        Assert.Throws<ArgumentException>(() => TutorTable.Write(code, 0x400, moves.Take(66).ToList()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TutorTable.Write(code, 0x400, [.. moves.Take(66), 5000]));

        // Y lo escrito se relee igual.
        var shuffled = moves.Reverse().ToList();
        TutorTable.Write(code, 0x400, shuffled);
        Assert.Equal(shuffled, TutorTable.Read(code, 0x400));
    }
}
