using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// A sidebar entry that holds several sections as tabs, so the sidebar stays short.
/// </summary>
/// <remarks>
/// Each page keeps its own view model, view and game requirement: the group only forwards. What
/// the header badge shows is the requirement of the tab on screen, because the pages of one group
/// do not all need the same thing (the viewer wants the game shut, POKE PASTE does not care).
/// </remarks>
public sealed partial class GroupSectionViewModel : SectionViewModel
{
    private readonly string _iconKey;
    private readonly ILogger _logger;

    public GroupSectionViewModel(string title, string iconKey, ILogger logger, params SectionViewModel[] pages)
        : base(title)
    {
        _iconKey = iconKey;
        _logger = logger;
        Pages = [.. pages];
        _selectedPage = pages[0];
    }

    /// <summary>The tabs. It can change: PUNTOS only has RULETA while the run plays with the wheel.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SectionViewModel> Pages { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Needs), nameof(NeedsLabel), nameof(NeedsState), nameof(ShowsNeed), nameof(HeaderTitle))]
    private SectionViewModel _selectedPage;

    public override string IconKey => _iconKey;

    public override GameNeed Needs => SelectedPage.Needs;

    public override string HeaderTitle => SelectedPage.Title;

    partial void OnSelectedPageChanged(SectionViewModel? oldValue, SectionViewModel newValue)
    {
        try
        {
            oldValue?.ResetState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al cerrar la pestaña {Page}", oldValue?.Title);
        }

        _ = ActivatePageAsync(newValue);
    }

    public override Task ActivateAsync() => ActivatePageAsync(SelectedPage);

    public override void ResetState() => SelectedPage.ResetState();

    /// <summary>A tab that fails to load must not take the window down with it.</summary>
    private async Task ActivatePageAsync(SectionViewModel page)
    {
        try
        {
            await page.ActivateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al activar la pestaña {Page}", page.Title);
        }
    }
}
