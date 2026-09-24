using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The tournament audit. Everything it does lives in <see cref="AuditViewModel"/>.</summary>
public partial class AuditWindow : Window
{
    public AuditWindow(AuditViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
