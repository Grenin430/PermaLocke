namespace System.Windows.Media;

/// <summary>
/// Stand-in for WPF's <c>Color</c>, the only piece of WPF the album's drawing uses, so it builds where WPF does not
/// exist. Same members the drawing touches, same equality: two colours are equal when their four channels are.
/// </summary>
public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
}

/// <summary>Stand-in for WPF's <c>Colors</c>: only what the drawing uses.</summary>
public static class Colors
{
    public static Color Transparent => Color.FromArgb(0, 255, 255, 255);
}
