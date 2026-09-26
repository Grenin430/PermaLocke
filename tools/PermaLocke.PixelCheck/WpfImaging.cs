namespace System.Windows
{
    /// <summary>Stand-in for WPF's <c>Int32Rect</c>: only what the pixel scenes pass to <c>WritePixels</c>.</summary>
    public readonly record struct Int32Rect(int X, int Y, int Width, int Height);
}

namespace System.Windows.Media
{
    /// <summary>Stand-in for WPF's <c>PixelFormat</c>.</summary>
    public readonly record struct PixelFormat(int BitsPerPixel);

    /// <summary>Stand-in for WPF's <c>PixelFormats</c>: the two the scenes use.</summary>
    public static class PixelFormats
    {
        public static PixelFormat Bgra32 => new(32);

        public static PixelFormat Pbgra32 => new(32);
    }
}

namespace System.Windows.Media.Imaging
{
    /// <summary>Stand-in for WPF's <c>BitmapPalette</c>: never used, only passed as null.</summary>
    public sealed class BitmapPalette;

    /// <summary>
    /// Stand-in for WPF's <c>BitmapSource</c>, so the full pixel scenes (<c>PixelScene</c>, the capsule machine) build
    /// and draw where WPF does not exist. It holds the pixels it was given, BGRA.
    /// </summary>
    public class BitmapSource
    {
        protected BitmapSource(int width, int height)
        {
            PixelWidth = width;
            PixelHeight = height;
            Pixels = new byte[width * height * 4];
        }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        protected byte[] Pixels { get; }

        public void Freeze()
        {
        }

        public void CopyPixels(Array pixels, int stride, int offset) => Buffer.BlockCopy(Pixels, 0, pixels, offset, Pixels.Length);
    }

    /// <summary>Stand-in for WPF's <c>WriteableBitmap</c>: <c>WritePixels</c> copies into the stand-in's own pixels.</summary>
    public sealed class WriteableBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat format, BitmapPalette? palette)
        : BitmapSource(pixelWidth, pixelHeight)
    {
        public void WritePixels(Int32Rect rect, byte[] pixels, int stride, int offset)
        {
            for (var y = 0; y < rect.Height; y++)
            {
                Buffer.BlockCopy(pixels, offset + (y * stride), Pixels, (((rect.Y + y) * PixelWidth) + rect.X) * 4, rect.Width * 4);
            }
        }
    }
}
