using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.ViewModels;

/// <summary>One gift as the inbox draws it.</summary>
public sealed partial class GiftRowViewModel(AdminGift gift, bool collected) : ObservableObject
{
    public AdminGift Gift { get; } = gift;

    public string From => Gift.From;

    public string What => Gift.Say();

    public string Reason => Gift.Reason;

    public string When => Playtime.Ago(Gift.CreatedAt, DateTimeOffset.Now);

    /// <summary>Said before pressing, because it is the one thing that can make it wait.</summary>
    public bool NeedsTheGame => Gift.NeedsTheGame;

    [ObservableProperty]
    private bool _isCollected = collected;

    [ObservableProperty]
    private bool _isBusy;

    public bool CanCollect => !IsCollected && !IsBusy;
}

/// <summary>
/// The gift in the corner of the window: what the admin has left for this player, and the button that collects it
/// (§129).
/// </summary>
/// <remarks>
/// It opens from the header on every screen because a gift arrives whenever it arrives, and it never applies anything
/// by itself: the list is what is waiting, and collecting is a press.
/// </remarks>
public sealed partial class GiftInboxViewModel : ObservableObject
{
    private readonly GiftInbox _inbox;
    private readonly IUiDispatcher _ui;

    public GiftInboxViewModel(GiftInbox inbox, IUiDispatcher ui)
    {
        Fleeting.Fade(this, nameof(Status));

        _inbox = inbox;
        _ui = ui;

        _inbox.PropertyChanged += (_, _) => _ = _ui.InvokeAsync(ShowAsync);
    }

    public ObservableCollection<GiftRowViewModel> Items { get; } = [];

    /// <summary>How many are waiting: the number on the gift.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGifts))]
    private int _count;

    public bool HasGifts => Count > 0;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _status = string.Empty;

    public bool IsEmpty => Items.Count == 0;

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;

        if (IsOpen)
        {
            Status = string.Empty;
            _ = _inbox.LookAsync();
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private async Task ClaimAsync(GiftRowViewModel? row)
    {
        if (row is null || !row.CanCollect)
        {
            return;
        }

        row.IsBusy = true;

        try
        {
            var result = await _inbox.ClaimAsync(row.Gift);
            Status = result.Message;
            row.IsCollected = result.Collected;
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private Task ShowAsync()
    {
        Items.Clear();

        foreach (var gift in _inbox.Pending)
        {
            Items.Add(new GiftRowViewModel(gift, collected: false));
        }

        foreach (var gift in _inbox.Collected)
        {
            Items.Add(new GiftRowViewModel(gift, collected: true));
        }

        Count = _inbox.Pending.Count;
        OnPropertyChanged(nameof(IsEmpty));

        // Nada que recoger y nadie mirando: la bandeja se cierra sola en vez de quedarse abierta y vacía.
        if (Items.Count == 0)
        {
            IsOpen = false;
        }

        return Task.CompletedTask;
    }
}
