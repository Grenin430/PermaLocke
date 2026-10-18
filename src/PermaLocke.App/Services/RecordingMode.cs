using System.Windows;

namespace PermaLocke.App.Services;

/// <summary>
/// Started with <c>--grabacion</c> to film the trailer: hides the app's name in the caption and the other players'
/// tabs above the cemetery. Nothing else changes.
/// </summary>
public static class RecordingMode
{
    public static bool On { get; set; }

    public static Visibility BrandVisibility => On ? Visibility.Collapsed : Visibility.Visible;
}
