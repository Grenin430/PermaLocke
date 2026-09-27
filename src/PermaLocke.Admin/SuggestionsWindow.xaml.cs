using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The players' suggestions. Everything it does lives in <see cref="SuggestionsViewModel"/>.</summary>
public partial class SuggestionsWindow : Window
{
    public SuggestionsWindow(SuggestionsViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
