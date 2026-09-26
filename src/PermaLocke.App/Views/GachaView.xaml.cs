using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The gacha screen. The only code here feeds the capsule machine.
/// </summary>
/// <remarks>
/// Presentation, not logic: it decides nothing and touches no data. It turns what the view model already has — the
/// chosen banner and its odds, the strip of past rolls — into what the machine draws: which machine, how full of which
/// balls, and which figures stand on the shelf. The pull itself reaches the machine by binding, with the moment it
/// started, and the view model owns its timeline (§171).
/// </remarks>
public partial class GachaView : UserControl
{
    private GachaViewModel? _model;

    public GachaView()
    {
        InitializeComponent();
        Machine.RenderFailed += ex => (DataContext as GachaViewModel)?.AnimationFailed(ex);
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
        Loaded += (_, _) =>
        {
            Attach(DataContext as GachaViewModel);

            // El foco en el botón grande, para que ESPACIO tire nada más entrar.
            RollButton.Focus();
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// ESPACIO or INTRO is the big button (§189): TIRAR, or SALTAR while a pull is playing. For fifty pulls in a row
    /// without aiming the mouse fifty times.
    /// </summary>
    /// <remarks>
    /// Not while typing in the pool's search box, not on a key held down — a held key repeating would spend points —
    /// and not on the other buttons, which keep their own ESPACIO.
    /// </remarks>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter) || e.IsRepeat || _model is not { } model
            || e.OriginalSource is TextBoxBase or ButtonBase { Name: not "RollButton" })
        {
            return;
        }

        if (model.SkipCommand.CanExecute(null))
        {
            model.SkipCommand.Execute(null);
        }
        else if (model.RollCommand.CanExecute(null))
        {
            model.RollCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        Attach(e.NewValue as GachaViewModel);

    private void Attach(GachaViewModel? model)
    {
        if (ReferenceEquals(model, _model))
        {
            return;
        }

        Detach();

        if (model is null)
        {
            return;
        }

        _model = model;
        model.PropertyChanged += OnModelPropertyChanged;
        model.History.CollectionChanged += OnHistoryChanged;
        model.Portals.CollectionChanged += OnPortalsChanged;
        UpdateBanner();
        UpdateShelf();
    }

    private void Detach()
    {
        if (_model is null)
        {
            return;
        }

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.History.CollectionChanged -= OnHistoryChanged;
        _model.Portals.CollectionChanged -= OnPortalsChanged;
        _model = null;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GachaViewModel.SelectedBanner) or nameof(GachaViewModel.RollCost))
        {
            UpdateBanner();
        }
    }

    private void OnPortalsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateBanner();

    private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateShelf();

    /// <summary>
    /// Loads the machine with the chosen banner: painted in the tier it mostly hands out, its dome filled in its odds,
    /// its price on the coin plaque, gold trim on the one that can give a legendary.
    /// </summary>
    /// <remarks>
    /// The odds go by the portals' order, which is the tiers' order in <c>Data/gacha.json</c>, so a sixth tier or a
    /// renamed one needs no change here. The same banner loaded twice is left alone: reloading it would make the dome
    /// fill up again for nothing.
    /// </remarks>
    private void UpdateBanner()
    {
        if (_model is not { } model || (model.SelectedBanner ?? model.Banners.FirstOrDefault()) is not { } banner
            || model.Portals.Count == 0)
        {
            return;
        }

        var odds = model.Portals
            .Select(portal => banner.Banner.TierChances.TryGetValue(portal.TierId, out var chance) ? chance : 0)
            .ToArray();

        var skin = 0;
        for (var i = 1; i < odds.Length; i++)
        {
            if (odds[i] > odds[skin]) skin = i;
        }

        var loaded = new CapsuleBanner(banner.Name, skin, odds, banner.Banner.Cost.ToString(),
            Deluxe: odds.Length > 0 && odds[^1] > 0);

        if (Machine.Banner is { } current && current.Name == loaded.Name && current.Price == loaded.Price
            && current.Odds.SequenceEqual(loaded.Odds))
        {
            return;
        }

        Machine.Banner = loaded;
    }

    /// <summary>The strip of past rolls, as figures for the shelf: newest first, each with its tier.</summary>
    private void UpdateShelf()
    {
        if (_model is not { } model)
        {
            return;
        }

        Machine.Shelf = model.History
            .Select(entry => new CapsuleShelfItem(RoomSprite.From(entry.Sprite), TierOf(entry.BrushKey)))
            .ToList();
    }

    /// <summary>Tier index from a palette key: <c>Tier3Brush</c> is the third tier, index 2.</summary>
    private static int TierOf(string brushKey) =>
        brushKey.StartsWith("Tier", StringComparison.Ordinal)
        && int.TryParse(brushKey.AsSpan(4, brushKey.Length - 4 - (brushKey.EndsWith("Brush", StringComparison.Ordinal) ? 5 : 0)), out var position)
            ? Math.Clamp(position - 1, 0, 4)
            : 0;
}
