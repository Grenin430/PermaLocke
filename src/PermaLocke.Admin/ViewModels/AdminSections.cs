namespace PermaLocke.Admin.ViewModels;

/// <summary>
/// One entry of Admin's side menu (2026-09-28): its group heading, its name, its pixel icon and the page it opens. The page
/// is a view model; the window's templates pick the view.
/// </summary>
public sealed record AdminSection(string Group, string Title, string Icon, object Page, string Help);

/// <summary>The pages that are parts of the main view model, told apart by type so each gets its own view.</summary>
public sealed record HomeSection(AdminViewModel Admin);

public sealed record PlayersSection(AdminViewModel Admin);

public sealed record GiftsSection(AdminViewModel Admin);

public sealed record TournamentSection(AdminViewModel Admin);
