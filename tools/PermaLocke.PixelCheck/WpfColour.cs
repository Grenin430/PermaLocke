namespace System.Windows.Media;

/// <summary>
/// Stand-in for WPF's <c>Color</c>, so <c>RoomSprite.At</c> (used by the other scenes) builds where WPF does not exist.
/// The album itself draws with <c>PixelColour</c> (§188).
/// </summary>
public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
}
