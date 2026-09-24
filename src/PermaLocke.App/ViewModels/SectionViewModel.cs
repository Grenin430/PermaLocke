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

    /// <summary>
    /// Called when the section is left, so that coming back to it opens it as it opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What this closes is what is <b>open</b> or <b>primed</b>: a panel unfolded over the screen,
    /// a two-step button already armed, a confirmation waiting to be pressed. Leaving the gacha
    /// with a tier's list unfolded and finding it still unfolded ten minutes later reads as the
    /// screen having failed to close it, not as the screen having remembered.
    /// </para>
    /// <para>
    /// It deliberately does <b>not</b> mean "throw everything away". Where a section is holding
    /// something the player made -- an edit not yet saved, work in flight -- it says so and keeps
    /// it: dropping that on a tab change would be a silent state change, which is the one thing
    /// this project does not do. Each section decides, because only the section knows which of its
    /// state is a panel and which is somebody's work.
    /// </para>
    /// </remarks>
    public virtual void ResetState()
    {
    }

    /// <summary>This entry as a group of tabs, or null. The sidebar hangs the tabs under it.</summary>
    public GroupSectionViewModel? AsGroup => this as GroupSectionViewModel;

    /// <summary>The big heading: the section, or the tab on screen when the section is a group.</summary>
    public virtual string HeaderTitle => Title;

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
