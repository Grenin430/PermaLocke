namespace PermaLocke.App.Views;

/// <summary>
/// A colour of the pixel art as four bytes, and nothing else (§188).
/// </summary>
/// <remarks>
/// <para>
/// The album's drawing used WPF's <c>Color</c>, and every <c>Color.FromRgb</c> converts the colour to scRGB and back
/// with six <c>Math.Pow</c>. The card in the hand made several per screen pixel — a quarter of a million pixels a frame —
/// and ran at about ten frames a second on the player's PC. This one is only its bytes: making it costs nothing.
/// </para>
/// <para>
/// The same members as WPF's, so the drawing reads the same: <c>using Color = PermaLocke.App.Views.PixelColour;</c>.
/// Pure, like the rest of the album's drawing, so <c>tools/PermaLocke.PixelCheck</c> measures what the app will run.
/// </para>
/// </remarks>
public readonly record struct PixelColour(byte A, byte R, byte G, byte B)
{
    public static PixelColour FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public static PixelColour FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    public static PixelColour Transparent => new(0, 255, 255, 255);
}
