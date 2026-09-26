using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>A picture from the cartridge, as the scenes draw it: BGRA, one icon pixel per cell.</summary>
/// <remarks>
/// The cells alone here, without WPF; reading one from a bitmap (<c>From</c>) is in <c>TrainerRoomScene.cs</c>. Apart so
/// the album's cards can be drawn and tested without the rest of WPF (<c>tools/PermaLocke.PixelCheck</c>, §187).
/// </remarks>
public sealed partial record RoomSprite(byte[] Bgra, int Width, int Height)
{
    public bool Solid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Bgra[(((y * Width) + x) * 4) + 3] >= 128;

    public Color At(int x, int y)
    {
        var at = ((y * Width) + x) * 4;
        return Color.FromRgb(Bgra[at + 2], Bgra[at + 1], Bgra[at]);
    }
}
