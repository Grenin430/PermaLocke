namespace PermaLocke.App.Services;

/// <summary>Positions the notice stack in screen pixels, keeping it inside the selected monitor.</summary>
internal static class ToastPlacement
{
    internal static (int Left, int Top, int Width, int Height) Calculate(
        (int Left, int Top, int Width, int Height) target,
        (int Left, int Top, int Width, int Height) work,
        double scaleX, double scaleY)
    {
        var marginX = Math.Min((int)Math.Ceiling(16 * scaleX), (work.Width - 1) / 2);
        var marginY = Math.Min((int)Math.Ceiling(16 * scaleY), (work.Height - 1) / 2);
        var width = Math.Min((int)Math.Ceiling(470 * scaleX), work.Width - 2 * marginX);
        var height = Math.Min((int)Math.Ceiling(760 * scaleY), work.Height - 2 * marginY);
        var left = Math.Clamp(target.Left + target.Width - width - marginX,
            work.Left + marginX, work.Left + work.Width - width - marginX);
        var top = Math.Clamp(target.Top + target.Height - height - marginY,
            work.Top + marginY, work.Top + work.Height - height - marginY);
        return (left, top, width, height);
    }
}
