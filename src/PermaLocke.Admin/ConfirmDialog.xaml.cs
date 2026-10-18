using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.Admin.Services;

namespace PermaLocke.Admin;

/// <summary>The confirmation of every action of Admin. Everything it shows comes from its <see cref="ConfirmRequest"/>.</summary>
public partial class ConfirmDialog : Window
{
    private readonly ConfirmRequest _request;

    public ConfirmDialog(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InitializeComponent();
        _request = request;

        Title = request.Title;
        Heading.Text = request.Title.ToUpperInvariant();
        Target.Text = request.Target;
        Consequences.ItemsSource = request.Consequences;
        Yes.Content = request.ConfirmText;
        ReasonBox.Visibility = request.NeedsReason ? Visibility.Visible : Visibility.Collapsed;
        Reason.Text = request.Reason ?? string.Empty;
        TypeBox.Visibility = request.TypeToConfirm is null ? Visibility.Collapsed : Visibility.Visible;
        TypeHint.Text = $"No se puede deshacer desde aquí. Escribe {request.TypeToConfirm} para seguir.";

        if (request.Danger)
        {
            Stripe.Background = (Brush)FindResource("Bad");
            Yes.Style = (Style)FindResource(typeof(Button));
            Yes.BorderBrush = (Brush)FindResource("Bad");
        }

        Loaded += (_, _) => (request.NeedsReason ? (Control)Reason : request.TypeToConfirm is null ? Yes : Typed).Focus();
        Refresh();
    }

    public ConfirmAnswer? Answer { get; private set; }

    private void OnTyped(object sender, TextChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var missingReason = _request.NeedsReason && Reason.Text.Trim().Length == 0;
        var missingWord = _request.TypeToConfirm is { } word
                          && !string.Equals(Typed.Text.Trim(), word, StringComparison.CurrentCultureIgnoreCase);

        Yes.IsEnabled = !missingReason && !missingWord;
        Why.Text = missingReason ? "Falta el motivo." : missingWord ? $"Escribe {_request.TypeToConfirm}." : string.Empty;
    }

    private void OnYes(object sender, RoutedEventArgs e)
    {
        if (!Yes.IsEnabled) return;

        Answer = new ConfirmAnswer(_request.NeedsReason ? Reason.Text.Trim() : string.Empty);
        DialogResult = true;
    }
}
