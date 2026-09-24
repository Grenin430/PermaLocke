using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;
using GameConfig = pk3DS.Core.GameConfig;
using Pk3dsVersion = pk3DS.Core.GameVersion;
using TextFile = pk3DS.Core.TextFile;

namespace PermaLocke.Randomizer.Modules;

/// <summary>One move of a world's move table, with its category already translated.</summary>
/// <param name="Category">
/// 0 status, 1 physical, 2 special, or -1 when the table's own numbering could not be anchored.
/// </param>
/// <param name="Accuracy">Zero for a move that never misses.</param>
public sealed record WorldMoveEntry(int Type, int Category, int Power, int Accuracy, int PP);

/// <summary>
/// The level-up learnsets, the move table, the move names and their descriptions of a world on disk, each null when
/// that world does not carry the file — the game then reads the cartridge's, and so should whoever asks.
/// </summary>
public sealed record WorldMoveTables(
    IReadOnlyList<IReadOnlyList<LevelUpMove>>? Learnsets,
    IReadOnlyList<WorldMoveEntry>? Moves,
    IReadOnlyList<string>? Names,
    IReadOnlyList<string>? Descriptions = null)
{
    /// <summary>Text file of the move names in each language's text container, measured in §133.</summary>
    private const int MoveNamesFile = 118;

    /// <summary>The move descriptions, the file before the names in Ultra Sun and Ultra Moon (pk3DS <c>TextReference</c>).</summary>
    private const int MoveDescriptionsFile = 117;

    /// <summary>The game's text container for Spanish, which is the player's language.</summary>
    private const int Spanish = 6;

    /// <summary>What closes a learnset entry: a move id no move has.</summary>
    private const ushort Terminator = 0xFFFF;

    /// <summary>Reads the three from a romfs folder. Never writes; a file that is not there is a null.</summary>
    public static WorldMoveTables Read(string romfs)
    {
        ArgumentNullException.ThrowIfNull(romfs);

        var learnsets = Path.Combine(romfs, GameFiles.Learnset.Replace('/', Path.DirectorySeparatorChar));
        var moves = Path.Combine(romfs, GameFiles.Move.Replace('/', Path.DirectorySeparatorChar));
        var text = Path.Combine(romfs, GameFiles.GameText(Spanish).Replace('/', Path.DirectorySeparatorChar));

        var config = new GameConfig(Pk3dsVersion.UM);
        var names = File.Exists(text) ? TextFile.GetStrings(config, GarcPatcher.ReadOnly(text, MoveNamesFile)) : null;
        var descriptions = File.Exists(text)
            ? TextFile.GetStrings(config, GarcPatcher.ReadOnly(text, MoveDescriptionsFile)).Select(Flatten).ToArray()
            : null;

        return new WorldMoveTables(
            File.Exists(learnsets) ? [.. GarcPatcher.ReadAllReadOnly(learnsets).Select(Parse)] : null,
            File.Exists(moves) ? ReadMoves(moves, names) : null,
            names,
            descriptions);
    }

    /// <summary>
    /// One learnset entry: pairs of (move, level) as two little-endian halfwords, closed by a move of 0xFFFF.
    /// </summary>
    /// <remarks>
    /// Stops at the terminator rather than at the end of the bytes, because the entry is padded, and at an odd
    /// trailing halfword rather than reading past it: a malformed entry yields what it clearly says and no more.
    /// </remarks>
    public static IReadOnlyList<LevelUpMove> Parse(byte[] entry)
    {
        var moves = new List<LevelUpMove>(entry.Length / 4);

        for (var at = 0; at + 4 <= entry.Length; at += 4)
        {
            var move = BitConverter.ToUInt16(entry, at);

            if (move == Terminator)
            {
                break;
            }

            moves.Add(new LevelUpMove(move, BitConverter.ToUInt16(entry, at + 2)));
        }

        return moves;
    }

    /// <summary>
    /// A description as one paragraph: the game breaks it into lines for the width of the handheld's screen, written
    /// as <c>\n</c>, and here the text wraps to whatever width it is given.
    /// </summary>
    public static string Flatten(string text) =>
        string.Join(' ', (text ?? string.Empty)
            .Replace("\\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<WorldMoveEntry> ReadMoves(string path, IReadOnlyList<string>? names)
    {
        var table = Mini.UnpackMini(GarcPatcher.ReadOnly(path, 0), "WD").Select(data => new Move7(data)).ToArray();

        // La numeración de la categoría y el valor de «no falla nunca» se anclan en movimientos con nombre, como en
        // el randomizador (MoveCatalog). Sin nombres, o si las anclas no cuadran, se dice «no se sabe».
        (int Physical, int Special)? categories = null;
        int? perfect = null;

        if (names is not null)
        {
            try
            {
                categories = MoveCatalog.DetectCategories([.. table.Select(move => move.Category)], names);
                perfect = MoveCatalog.DetectPerfectAccuracy([.. table.Select(move => move.Accuracy)], names);
            }
            catch (InvalidDataException)
            {
                categories = null;
                perfect = null;
            }
        }

        return
        [
            .. table.Select(move => new WorldMoveEntry(
                move.Type,
                categories is not { } known ? MoveSheet.Unknown
                : move.Category == known.Physical ? MoveSheet.Physical
                : move.Category == known.Special ? MoveSheet.Special
                : MoveSheet.Status,
                move.Power,
                perfect is { } never && move.Accuracy == never ? 0 : move.Accuracy,
                move.PP))
        ];
    }
}
