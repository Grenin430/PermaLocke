using CommunityToolkit.Mvvm.ComponentModel;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Whether a section needs Azahar running, needs it shut, or does not care.
/// </summary>
/// <remarks>
/// It is not a detail of each screen: it is the single question that decides whether pressing
/// anything here will work at all, and the answer changes from section to section because the two
/// ways into the game are different. Memory is live and needs the emulator up (§22); the save file
/// is a file and PKHeX will not write it under a running game. A player who has to find that out by
/// pressing a button and reading a failure is being told something the app knew all along.
/// </remarks>
public enum GameNeed
{
    /// <summary>Touches nothing of the game.</summary>
    None,

    /// <summary>Reads or writes the running game's memory. Azahar open with the game loaded.</summary>
    Running,

    /// <summary>Writes the save file or the mod folder. The game has to be closed.</summary>
    Closed,

    /// <summary>Only reads the save file. Works either way, but shows the last thing saved.</summary>
    Either
}

/// <summary>Base for everything the sidebar can navigate to.</summary>
public abstract partial class SectionViewModel : ObservableObject
{
    protected SectionViewModel(string title, string subtitle = "")
    {
        Title = title;
        Subtitle = subtitle;
    }

    /// <summary>Label shown in the sidebar and as the big section heading.</summary>
    public string Title { get; }

    /// <summary>
    /// One line under the heading saying what the section is for. Presentation only: a header
    /// carrying a single word tells a newcomer nothing about what they are looking at.
    /// </summary>
    public string Subtitle { get; }

    /// <summary>
    /// Key of the navigation icon, resolved against <c>Themes/Icons.xaml</c>.
    /// </summary>
    /// <remarks>
    /// A key and not a geometry: the view models must not reference WPF drawing types, and the
    /// shape of an icon is the theme's business. A section that forgets to answer gets a dot,
    /// which is visibly a placeholder rather than an empty gap in the menu.
    /// </remarks>
    public virtual string IconKey => "IconDot";

    /// <summary>
    /// What this section needs of the emulator. Each screen answers for itself, because the answer
    /// comes from which door it writes through and only the screen knows that.
    /// </summary>
    public virtual GameNeed Needs => GameNeed.None;

    /// <summary>The badge text, in Spanish: the enum never reaches the screen.</summary>
    public string NeedsLabel => Needs switch
    {
        GameNeed.Running => "JUEGO ABIERTO",
        GameNeed.Closed => "JUEGO CERRADO",
        GameNeed.Either => "ABIERTO O CERRADO",
        _ => string.Empty
    };

    /// <summary>
    /// Colour band, decided here rather than by three triggers copied into XAML -- same reason the
    /// party rows carry their own "ok"/"low"/"critical".
    /// </summary>
    public string NeedsState => Needs switch
    {
        GameNeed.Running => "running",
        GameNeed.Closed => "closed",
        GameNeed.Either => "either",
        _ => "none"
    };

    /// <summary>False for a section that does not touch the game, so the badge stays off.</summary>
    public bool ShowsNeed => Needs != GameNeed.None;

    /// <summary>Called every time the section becomes visible, so it can refresh itself.</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;

    public override string ToString() => Title;
}

/// <summary>
/// Stand-in for a section that is not built yet. It states plainly that it does nothing,
/// which phase will build it and what it will need, rather than showing a fake screen.
/// </summary>
public sealed class PendingSectionViewModel(string title, string phase, string plan)
    : SectionViewModel(title, "Sin construir todavía")
{
    /// <summary>Phase from docs/ARCHITECTURE.md §12 that delivers this section.</summary>
    public string Phase { get; } = phase;

    /// <summary>What the section will do once implemented.</summary>
    public string Plan { get; } = plan;
}
