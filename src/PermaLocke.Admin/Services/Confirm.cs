using System.Windows;

namespace PermaLocke.Admin.Services;

/// <summary>What a confirmation asks: the action, what it will do, and whether it needs a reason or typing a word.</summary>
/// <param name="Consequences">What will happen, in plain words, one per line: the dialog lists them.</param>
/// <param name="NeedsReason">The reason goes to the player's history and to the organiser's log; empty is refused.</param>
/// <param name="TypeToConfirm">For the actions that cannot be undone from here: the button stays off until this is typed.</param>
public sealed record ConfirmRequest(string Title, string Target, IReadOnlyList<string> Consequences, string ConfirmText,
    bool NeedsReason = true, bool Danger = false, string? TypeToConfirm = null, string? Reason = null);

/// <param name="Reason">What the organiser wrote, trimmed; empty when none was asked for.</param>
public sealed record ConfirmAnswer(string Reason);

/// <summary>
/// Every action of Admin that changes something goes through here (2026-10-08): one dialog that says what will happen and
/// asks the reason there, instead of a reason field shared by the whole page.
/// </summary>
/// <remarks>Tests replace <see cref="Handler"/>; the application shows <c>ConfirmDialog</c>.</remarks>
public static class Confirm
{
    public static Func<ConfirmRequest, ConfirmAnswer?> Handler { get; set; } = Show;

    /// <summary>The answer, or null when the organiser said no.</summary>
    public static ConfirmAnswer? Ask(ConfirmRequest request) => Handler(request);

    private static ConfirmAnswer? Show(ConfirmRequest request)
    {
        var dialog = new ConfirmDialog(request) { Owner = Application.Current?.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }
}
