using System.Windows;

namespace PermaLocke.App.Views;

/// <summary>
/// The window the notices live in. It has no logic: where it goes and what it says is
/// <see cref="Services.Notifier"/>'s job.
/// </summary>
/// <remarks>
/// It refuses to close: closing it would leave the notifier holding a window that can never be
/// shown again, and there is no reason to close it — it hides itself when there is nothing to say.
/// <para>
/// It is closed like any other window and never refuses to: a window that cancels its own closing
/// keeps the application alive after the last real one is gone. Whoever owns it just makes another
/// if it ever needs one again.
/// </para>
/// </remarks>
public partial class ToastWindow : Window
{
    public ToastWindow() => InitializeComponent();
}
