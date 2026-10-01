using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Shells;

/// <summary>
/// What every alternative shell does the same way: hosts the section on screen with its small entrance, and tells the
/// window what to animate when the window comes back from the tab. The look of each shell is its own XAML.
/// </summary>
public abstract class ShellBase : UserControl
{
    private MainViewModel? _main;
    private readonly TranslateTransform _slide = new();

    protected ShellBase()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) =>
        {
            Detach();
            Attach();
        };
    }

    /// <summary>The element that holds the section on screen: it gets the entrance.</summary>
    protected abstract UIElement SectionHost { get; }

    protected MainViewModel? Main => _main;

    private void Attach()
    {
        if (_main is not null || DataContext is not MainViewModel main) return;

        _main = main;
        SectionHost.RenderTransform = _slide;
        main.PropertyChanged += OnMainChanged;
        OnAttached(main);
    }

    private void Detach()
    {
        if (_main is null) return;

        _main.PropertyChanged -= OnMainChanged;
        _main = null;
    }

    /// <summary>A page picked in a list of the group's pages: the group takes it. Only what really is one of its pages.</summary>
    protected void ChoosePage(SelectionChangedEventArgs e)
    {
        if (_main?.SelectedSection is GroupSectionViewModel group
            && e.AddedItems.Count == 1
            && e.AddedItems[0] is SectionViewModel page
            && group.Pages.Contains(page)
            && !ReferenceEquals(group.SelectedPage, page))
        {
            group.SelectedPage = page;
        }
    }

    protected virtual void OnAttached(MainViewModel main)
    {
    }

    /// <summary>Any other property of the window'"'"'s data that this shell reacts to (the rail flashes the points).</summary>
    protected virtual void OnMainPropertyChanged(string? property)
    {
    }

    protected virtual void OnSectionChanged()
    {
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnMainPropertyChanged(e.PropertyName);

        if (e.PropertyName == nameof(MainViewModel.SelectedSection))
        {
            ShellSupport.Enter(SectionHost, _slide);
            OnSectionChanged();
        }
    }
}
