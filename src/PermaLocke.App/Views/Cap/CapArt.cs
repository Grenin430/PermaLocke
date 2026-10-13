using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// The small shared things of the cap panel (2026-10-10): the colour and the three letters of each type and status, and the numbers that look
/// random and are not (two hashes of what they are asked for, so a frame is the same wherever and whenever it is painted).
/// </summary>
public static class CapArt
{
    /// <summary>The tints of a creature with no type to give it one.</summary>
    public static readonly uint[] Hues = [0x4A6A8A, 0x8A4A3A, 0x4A7A5A, 0x7A5A8A, 0x8A7A3A, 0x3A7A7A];

    /// <summary>The three letters of each type, in Spanish, by the game's type number.</summary>
    private static readonly string[] TypeNames =
        ["NORMAL", "LUCHA", "VOLADOR", "VENENO", "TIERRA", "ROCA", "BICHO", "FANTASMA", "ACERO", "FUEGO", "AGUA", "PLANTA", "ELÉCTRICO", "PSÍQUICO", "HIELO",
         "DRAGÓN", "SINIESTRO", "HADA"];

    public static string TypeName(int type) => type >= 0 && type < TypeNames.Length ? TypeNames[type] : "???";

    public static uint TypeColour(int type)
    {
        var c = TypeColours.Of(type);
        return CapSheet.Rgb(c.R, c.G, c.B);
    }

    public static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text) hash = unchecked((hash * 31) + ch);
        return hash & 0x7FFFFFFF;
    }

    /// <summary>A number out of two, the same every time: what the decoration that looks random is made of.</summary>
    public static int Hash(int a, int b)
    {
        var h = unchecked((uint)((a * 374761393) + (b * 668265263)));
        h = (h ^ (h >> 13)) * 1274126177;
        return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
    }

    /// <summary>The three letters and the colour of a status.</summary>
    public static (string Text, uint Colour) StatusLook(CapStatus status) => status switch
    {
        CapStatus.Poison => ("ENVENENADO", 0xA040A0u),
        CapStatus.Burn => ("QUEMADO", 0xF08030u),
        CapStatus.Paralysis => ("PARALIZADO", 0xF8D030u),
        CapStatus.Sleep => ("DORMIDO", 0x8C888Cu),
        CapStatus.Freeze => ("CONGELADO", 0x98D8D8u),
        _ => ("", 0u)
    };
}
