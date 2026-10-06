namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// Stand-in for the app's <c>PixelTheme</c> (2026-10-01), which needs WPF: here every scene is drawn in its own colours,
/// as the original look does. <c>PixelScene</c> asks it to recolour each frame for the Game Boy look.
/// </summary>
public sealed class PixelTheme
{
    public static PixelTheme Current { get; } = new();

    public void MapPixels(byte[] bgra)
    {
    }
}
