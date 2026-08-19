using CommunityToolkit.Mvvm.ComponentModel;

namespace PermaLocke.App.ViewModels;

/// <summary>Base for everything the sidebar can navigate to.</summary>
public abstract partial class SectionViewModel : ObservableObject
{
    protected SectionViewModel(string title)
    {
        Title = title;
    }

    /// <summary>Label shown in the sidebar and as the big section heading.</summary>
    public string Title { get; }

    /// <summary>Called every time the section becomes visible, so it can refresh itself.</summary>
    public virtual Task ActivateAsync() => Task.CompletedTask;

    public override string ToString() => Title;
}

/// <summary>
/// Stand-in for a section that is not built yet. It states plainly that it does nothing,
/// which phase will build it and what it will need, rather than showing a fake screen.
/// </summary>
public sealed class PendingSectionViewModel(string title, string phase, string plan)
    : SectionViewModel(title)
{
    /// <summary>Phase from docs/ARCHITECTURE.md §12 that delivers this section.</summary>
    public string Phase { get; } = phase;

    /// <summary>What the section will do once implemented.</summary>
    public string Plan { get; } = plan;
}
