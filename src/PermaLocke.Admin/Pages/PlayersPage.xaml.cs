using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PermaLocke.Admin.Services;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>JUGADORES: the table and the sheet. The filters and the selection are passed to <see cref="AdminViewModel"/>.</summary>
public partial class PlayersPage : UserControl
{
    private AdminViewModel? _admin;

    public PlayersPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as AdminViewModel);
        Unloaded += (_, _) => Attach(null);
    }

    private void Attach(AdminViewModel? admin)
    {
        if (_admin is not null)
        {
            _admin.VisiblePlayers.CollectionChanged -= OnListChanged;
            _admin.PropertyChanged -= OnAdminChanged;
        }

        _admin = admin;
        if (admin is null) return;

        admin.VisiblePlayers.CollectionChanged += OnListChanged;
        admin.PropertyChanged += OnAdminChanged;
        ShowFilter(admin.Filter);
        ShowEmpty();
    }

    private void OnAdminChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminViewModel.Filter) && _admin is not null) ShowFilter(_admin.Filter);
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowEmpty();

    /// <summary>Marks the chip of the filter in use (it can come from INICIO).</summary>
    private void ShowFilter(PlayerFilter filter)
    {
        var chip = filter switch
        {
            PlayerFilter.Conectados => FilterConectados,
            PlayerFilter.ARevisar => FilterARevisar,
            PlayerFilter.Suspendidos => FilterSuspendidos,
            PlayerFilter.SinRun => FilterSinRun,
            _ => FilterTodos
        };
        if (chip.IsChecked != true) chip.IsChecked = true;
    }

    private void ShowEmpty()
    {
        if (_admin is null) return;

        Empty.Visibility = _admin.VisiblePlayers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _admin.Players.Count == 0
            ? _admin.IsSignedIn ? "Aún no hay nadie: da de alta su Discord en LISTA." : "Entra con Discord (abajo a la izquierda) para ver a los jugadores."
            : "Nadie con este filtro o esta búsqueda.";
    }

    private void OnFilter(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string filter }) _admin?.SetFilterCommand.Execute(filter);
    }

    /// <summary>One marked opens the sheet; several are the group for the bar of actions.</summary>
    private void OnSelection(object sender, SelectionChangedEventArgs e)
    {
        if (_admin is null) return;

        var marked = List.SelectedItems.Cast<PlayerLine>().ToList();
        _admin.Choose(marked);
        if (marked.Count == 1) _admin.OpenSheetCommand.Execute(marked[0]);
    }
}
