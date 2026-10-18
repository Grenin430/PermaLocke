using CommunityToolkit.Mvvm.ComponentModel;

namespace PermaLocke.Admin.ViewModels;

/// <summary>
/// One entry of Admin's side menu (2026-09-28): its group heading, its name, its pixel icon and the page it opens. The page
/// is a view model; the window's templates pick the view.
/// </summary>
/// <remarks>
/// <see cref="Badge"/> (2026-10-08) is how many things on that page want the organiser now, counted from what was read
/// from the server; zero hides it.
/// </remarks>
public sealed partial class AdminSection(string group, string title, string icon, object page, string help) : ObservableObject
{
    public string Group { get; } = group;

    public string Title { get; } = title;

    public string Icon { get; } = icon;

    public object Page { get; } = page;

    public string Help { get; } = help;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    private int _badge;

    public bool HasBadge => Badge > 0;
}

/// <summary>The pages that are parts of the main view model, told apart by type so each gets its own view.</summary>
public sealed record HomeSection(AdminViewModel Admin);

public sealed record PlayersSection(AdminViewModel Admin);

public sealed record GiftsSection(AdminViewModel Admin);

public sealed record TournamentSection(AdminViewModel Admin);
