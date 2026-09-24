using GameConfig = pk3DS.Core.GameConfig;
using TextFile = pk3DS.Core.TextFile;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// Replaces a few lines of one of the game's text files and leaves every other byte where it was.
/// </summary>
/// <remarks>
/// <para>
/// pk3DS can rebuild a text file, but not faithfully: measured on the Spanish story text, it writes each
/// line's length <b>with the padding word counted</b> and overwrites the <c>ushort</c> after it —
/// which the cartridge sets to 4 on some lines, for reasons nobody here has identified — with zero.
/// 1005 of the 1124 files come back different, and four even lose characters, because it trims every
/// line. The game may well not care, but a field whose meaning is unknown is exactly the kind of thing
/// this project does not rewrite on a hunch (§53).
/// </para>
/// <para>
/// So only the lines being replaced are encoded, with pk3DS's encoder, and they keep their original
/// flag; every other line is copied encrypted as it was, padding and all. With nothing to replace the
/// output is the input, byte for byte.
/// </para>
/// </remarks>
public static class GameTextPatch
{
    private const ushort KeyBase = 0x7C89;
    private const ushort KeyAdvance = 0x2983;

    /// <summary>The word that opens a variable; the next word says how many follow it.</summary>
    private const ushort VariableMarker = 0x0010;

    private const ushort Terminator = 0x0000;

    /// <summary>
    /// The file with the given lines replaced by already encoded words (see <see cref="Encode"/>).
    /// </summary>
    public static byte[] ReplaceLines(byte[] original, IReadOnlyDictionary<int, ushort[]> replacements)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacements);

        var sectionStart = BitConverter.ToInt32(original, 0x0C);
        var count = BitConverter.ToUInt16(original, 0x02);
        var sectionLength = BitConverter.ToInt32(original, sectionStart);

        if (BitConverter.ToUInt16(original, 0x00) != 1 || sectionStart + sectionLength != original.Length)
        {
            throw new InvalidDataException("No es un fichero de texto de una sola sección como los del juego.");
        }

        foreach (var index in replacements.Keys)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(replacements),
                    $"El fichero tiene {count} líneas y se pide cambiar la {index}.");
            }
        }

        var tableStart = sectionStart + 4;
        var data = new List<byte[]>(count);
        var lengths = new ushort[count];
        var flags = new ushort[count];

        for (var line = 0; line < count; line++)
        {
            var entry = tableStart + (line * 8);
            var offset = BitConverter.ToInt32(original, entry);
            var end = line + 1 < count ? BitConverter.ToInt32(original, entry + 8) : sectionLength;
            flags[line] = BitConverter.ToUInt16(original, entry + 6);

            if (replacements.TryGetValue(line, out var words))
            {
                // Cifrada con la clave de SU línea, y rellena hasta cuatro bytes como el resto.
                var encrypted = Crypt(words, KeyFor(line));
                data.Add(encrypted.Length % 4 == 0 ? encrypted : [.. encrypted, 0, 0]);
                lengths[line] = (ushort)words.Length;
            }
            else
            {
                data.Add(original[(sectionStart + offset)..(sectionStart + end)]);
                lengths[line] = BitConverter.ToUInt16(original, entry + 4);
            }
        }

        var body = 4 + (8 * count) + data.Sum(d => d.Length);
        var result = new byte[sectionStart + body];
        original.AsSpan(0, sectionStart).CopyTo(result);
        BitConverter.GetBytes(body).CopyTo(result, 0x04);
        BitConverter.GetBytes(body).CopyTo(result, sectionStart);

        var at = 4 + (8 * count);

        for (var line = 0; line < count; line++)
        {
            var entry = tableStart + (line * 8);
            BitConverter.GetBytes(at).CopyTo(result, entry);
            BitConverter.GetBytes(lengths[line]).CopyTo(result, entry + 4);
            BitConverter.GetBytes(flags[line]).CopyTo(result, entry + 6);
            data[line].CopyTo(result, sectionStart + at);
            at += data[line].Length;
        }

        return result;
    }

    /// <summary>
    /// One line of text as the words the game stores, terminator included and padding left out.
    /// </summary>
    /// <remarks>
    /// Encoded by pk3DS — the variables, <c>\n</c> and <c>\c</c> are its notation — and cut by walking
    /// the words: a variable says how many words follow it, and one of those can be a zero, so «up to
    /// the first zero» would cut <c>[VAR 0101(0000)]</c> in half.
    /// </remarks>
    public static ushort[] Encode(GameConfig config, string line)
    {
        var file = TextFile.GetBytes(config, [line]);
        var sectionStart = BitConverter.ToInt32(file, 0x0C);
        var entry = sectionStart + 4;
        var offset = sectionStart + BitConverter.ToInt32(file, entry);
        var length = BitConverter.ToUInt16(file, entry + 4);

        var words = Decrypt(file.AsSpan(offset, length * 2), KeyFor(0));
        return words[..LengthOf(words)];
    }

    /// <summary>How many words the line really takes: up to its terminator, skipping over variables.</summary>
    public static int LengthOf(ReadOnlySpan<ushort> words)
    {
        var i = 0;

        while (i < words.Length)
        {
            if (words[i] == Terminator)
            {
                return i + 1;
            }

            i += words[i] == VariableMarker && i + 1 < words.Length ? 2 + words[i + 1] : 1;
        }

        throw new InvalidDataException("La línea no tiene terminador.");
    }

    private static ushort KeyFor(int line)
    {
        var key = KeyBase;

        for (var i = 0; i < line; i++)
        {
            key += KeyAdvance;
        }

        return key;
    }

    private static byte[] Crypt(ushort[] words, ushort key)
    {
        var result = new byte[words.Length * 2];

        for (var i = 0; i < words.Length; i++)
        {
            BitConverter.GetBytes((ushort)(words[i] ^ key)).CopyTo(result, i * 2);
            key = (ushort)((key << 3) | (key >> 13));
        }

        return result;
    }

    private static ushort[] Decrypt(ReadOnlySpan<byte> data, ushort key)
    {
        var words = new ushort[data.Length / 2];

        for (var i = 0; i < words.Length; i++)
        {
            words[i] = (ushort)(BitConverter.ToUInt16(data[(i * 2)..]) ^ key);
            key = (ushort)((key << 3) | (key >> 13));
        }

        return words;
    }
}
