using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The run's fallen, one grave each, and the story of the one picked: how it arrived, what it fell to
/// and, when there is one, the replay of its death.
/// </summary>
public sealed partial class CemeteryViewModel : SectionViewModel
{
    private readonly CemeteryService _cemetery;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<CemeteryViewModel> _logger;
    private readonly FallenShare _share;
    private readonly CommunityService _community;

    public CemeteryViewModel(CemeteryService cemetery, PokemonSpriteService sprites, IRunContext runContext, FallenShare share,
        CommunityService community,
        GameLinkMonitor monitor, IUiDispatcher ui, ILogger<CemeteryViewModel> logger)
        : base("CEMENTERIO", "Los que no volvieron, y cómo cayeron")
    {
        _cemetery = cemetery;
        _sprites = sprites;
        _logger = logger;
        _share = share;
        _community = community;
        community.PropertyChanged += (_, _) => _ = ui.InvokeAsync(() => { ShowOwners(); return Task.CompletedTask; });

        runContext.CurrentChanged += (_, _) => _ = ui.InvokeAsync(RefreshAsync);

        // Una muerte nueva aparece sin salir y volver a entrar.
        monitor.RunDataChanged += (_, _) => _ = ui.InvokeAsync(RefreshAsync);
    }

    public override string IconKey => "IconGrave";

    /// <summary>Only the run's own database: the emulator has nothing to do with it.</summary>
    public override GameNeed Needs => GameNeed.None;

    public ObservableCollection<GraveViewModel> Graves { get; } = [];

    /// <summary>The graves of the page on screen: the scene holds thirty.</summary>
    public ObservableCollection<GraveViewModel> PageGraves { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    private int _page;

    public int PageCount => Math.Max(1, (int)Math.Ceiling(Graves.Count / (double)Views.CemeteryScene.PerPage));

    public bool HasPages => PageCount > 1;

    public string PageText => $"{Page + 1} / {PageCount}";

    public string CountText => Graves.Count == 1 ? "1 caído" : $"{Graves.Count} caídos";

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousPage() => ShowPage(Page - 1);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void NextPage() => ShowPage(Page + 1);

    private bool CanGoBack() => Page > 0;

    private bool CanGoForward() => Page < PageCount - 1;

    private void ShowPage(int page)
    {
        Page = Math.Clamp(page, 0, PageCount - 1);
        PageGraves.Clear();

        foreach (var grave in Graves.Skip(Page * Views.CemeteryScene.PerPage).Take(Views.CemeteryScene.PerPage))
        {
            PageGraves.Add(grave);
        }

        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(CountText));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(OpenTheaterCommand))]
    [NotifyPropertyChangedFor(nameof(TheaterKillcamPath))]
    private GraveViewModel? _selected;

    /// <summary>Whether the killcam is shown big, over the whole section.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TheaterKillcamPath))]
    private bool _isTheaterOpen;

    /// <summary>The clip for the big player, only while it is open: a hidden player would keep playing.</summary>
    public string? TheaterKillcamPath => IsTheaterOpen ? Selected?.KillcamPath : null;

    [RelayCommand(CanExecute = nameof(CanOpenTheater))]
    private void OpenTheater() => IsTheaterOpen = true;

    private bool CanOpenTheater() => Selected?.HasKillcam == true;

    [RelayCommand]
    private void CloseTheater() => IsTheaterOpen = false;

    // Otra tumba es otra muerte: la sala no se queda enseñando la anterior.
    partial void OnSelectedChanged(GraveViewModel? value)
    {
        if (value?.HasKillcam != true)
        {
            IsTheaterOpen = false;
        }
    }

    [ObservableProperty]
    private bool _isEmpty = true;

    public bool HasSelection => Selected is not null;

    public override Task ActivateAsync() => RefreshAsync();

    [RelayCommand]
    private void Pick(GraveViewModel? grave)
    {
        if (grave is null)
        {
            return;
        }

        foreach (var other in Graves)
        {
            other.IsSelected = ReferenceEquals(other, grave);
        }

        Selected = grave;
        _ = FetchKillcamAsync(grave);
    }

    /// <summary>Whose cemetery is on screen (1.0.5.5): null for this player's own, or a friend's id.</summary>
    private FriendStatus? _owner;

    public string EmptyText => _owner is null ? "Aún no ha caído nadie en esta run." : "Aún no ha caído nadie (o no lo ha compartido).";

    /// <summary>Whether there are friends to switch to.</summary>
    public bool HasOwners => _community.Friends.Count > 0;

    /// <summary>Every player as a tab above the scene (1.0.5.7): you first, then each friend.</summary>
    public ObservableCollection<OwnerTab> Owners { get; } = [];

    private void ShowOwners()
    {
        var tabs = new List<OwnerTab> { new(null, "TÚ", _owner is null) };
        tabs.AddRange(_community.Friends.Select(f => new OwnerTab(f, f.Name.ToUpperInvariant(), f.PlayerId == _owner?.PlayerId)));

        // Solo si ha cambiado: la lista de amigos avisa a menudo y rehacer las pestañas parpadea.
        static (Guid?, string, bool) Key(OwnerTab t) => (t.Friend?.PlayerId, t.Name, t.IsSelected);
        if (tabs.Select(Key).SequenceEqual(Owners.Select(Key))) return;
        Owners.Clear();
        foreach (var tab in tabs) Owners.Add(tab);
        OnPropertyChanged(nameof(HasOwners));
    }

    [RelayCommand]
    private Task SelectOwner(OwnerTab? tab)
    {
        if (tab is null || tab.Friend?.PlayerId == _owner?.PlayerId) return Task.CompletedTask;

        _owner = tab.Friend;
        ShowOwners();
        OnPropertyChanged(nameof(EmptyText));
        IsTheaterOpen = false;
        return RefreshAsync();
    }

    private async Task<IReadOnlyList<GraveViewModel>> LoadAsync()
    {
        if (_owner is not { } friend)
        {
            return [.. (await _cemetery.GravesAsync()).Select(grave => new GraveViewModel(grave, _sprites.Get(grave.Species, grave.Form, grave.IsShiny)))];
        }

        return [.. (await _share.FriendGravesAsync(friend.PlayerId)).Select(pair =>
            new GraveViewModel(pair.Grave, _sprites.Get(pair.Grave.Species, pair.Grave.Form, pair.Grave.IsShiny), friend.PlayerId, pair.Killcam))];
    }

    private async Task FetchKillcamAsync(GraveViewModel grave)
    {
        if (grave.Owner is not { } player || !grave.RemoteKillcam || grave.KillcamPath is not null || grave.IsFetching) return;

        grave.IsFetching = true;
        grave.KillcamPath = await _share.FriendKillcamAsync(player, grave.PokemonId);
        grave.IsFetching = false;
        OpenTheaterCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _sprites.PrepareAsync();

            var graves = await LoadAsync();
            var picked = Selected?.PokemonId;

            Graves.Clear();
            ShowOwners();

            foreach (var grave in graves)
            {
                Graves.Add(grave);
            }

            IsEmpty = Graves.Count == 0;

            // Se conserva el elegido si sigue ahí; si no, el último en caer, que es por el que se viene.
            var chosen = Graves.FirstOrDefault(g => g.PokemonId == picked) ?? Graves.LastOrDefault();
            ShowPage(chosen is null ? 0 : Graves.IndexOf(chosen) / Views.CemeteryScene.PerPage);
            Pick(chosen);

            if (Graves.Count == 0)
            {
                Selected = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se ha podido cargar el cementerio");
        }
    }

    public override void ResetState()
    {
        // El elegido no es un panel abierto: es por dónde iba el jugador. Se queda. La sala sí se cierra.
        IsTheaterOpen = false;
    }
}

/// <summary>One grave, already turned into what the screen shows.</summary>
public sealed partial class GraveViewModel(Grave grave, BitmapSource? sprite, Guid? owner = null, bool remoteKillcam = false)
    : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public Guid PokemonId => grave.PokemonId;

    public string Name => grave.Name.ToUpperInvariant();

    public string Species => grave.SpeciesName;

    /// <summary>The species under the nickname, only when there is a nickname.</summary>
    public string SpeciesLine => string.Equals(grave.Name, grave.SpeciesName, StringComparison.OrdinalIgnoreCase)
        ? string.Empty
        : grave.SpeciesName;

    public BitmapSource? Sprite => sprite;

    /// <summary>How long it lasted in the run, when the run knows when it fell.</summary>
    private TimeSpan? Lasted => grave.DiedAt is { } died ? died - grave.ObtainedAt : null;

    /// <summary>
    /// The size of its monument, from how long it lasted: 0 a wooden cross (under a day), 1 a small stone
    /// (under four days), 2 a headstone (under ten), 3 an obelisk. A grave without a date of death gets the
    /// small stone, the one that claims nothing.
    /// </summary>
    public int MonumentSize => Lasted switch
    {
        null => 1,
        { TotalDays: < 1 } => 0,
        { TotalDays: < 4 } => 1,
        { TotalDays: < 10 } => 2,
        _ => 3
    };

    /// <summary>Which of the two looks of its monument, fixed per Pokémon so a grave does not change between visits.</summary>
    public int MonumentVariant => Math.Abs(grave.PokemonId.GetHashCode()) % 2;

    /// <summary>A per-grave phase for the ghost's float, so they do not bob in step.</summary>
    public double FloatPhase => (Math.Abs(grave.PokemonId.GetHashCode()) % 1000) / 1000.0;

    public bool IsShiny => grave.IsShiny;

    /// <summary>Fallen in the last day: the earth on its grave is still turned.</summary>
    public bool IsFresh => grave.DiedAt is { } died && DateTimeOffset.Now - died < TimeSpan.FromDays(1);

    /// <summary>What the pointer says over the grave.</summary>
    public string HoverText => string.IsNullOrEmpty(SpeciesLine)
        ? $"{Name} · {LastedShort}"
        : $"{Name} ({SpeciesLine}) · {LastedShort}";

    /// <summary>How long it lasted, short, for the big figure.</summary>
    public string LastedShort => Lasted switch
    {
        null => "¿?",
        { TotalDays: >= 2 } lasted => $"{(int)lasted.TotalDays} días",
        { TotalDays: >= 1 } => "1 día",
        { TotalHours: >= 1 } lasted => $"{(int)lasted.TotalHours} h",
        _ => "< 1 h"
    };

    /// <summary>What it cost, short, for the big figure.</summary>
    public string PenaltyShort => grave.Penalty > 0 ? $"−{grave.Penalty}" : "0";

    [ObservableProperty]
    private bool _isSelected;

    public string FellToText => grave.FellTo is { } rival
        ? $"Cayó ante {rival}"
        : grave.BattleAgainst.Count > 1
            ? $"Cayó en un combate contra {string.Join(", ", grave.BattleAgainst)}"
            : "No se sabe ante quién cayó";

    public string DiedText => grave.DiedAt is { } died
        ? $"Cayó el {died.ToLocalTime().ToString("d 'de' MMMM, HH:mm", Spanish)}"
        : "Sin fecha de la muerte";

    public string ArrivalText => grave.Origin switch
    {
        PokemonOrigin.Capture => $"Llegó por captura · {DisplayNames.Of(grave.Encounter)}",
        _ => $"Llegó por {DisplayNames.Of(grave.Origin).ToLowerInvariant()}"
    } + $" el {grave.ObtainedAt.ToLocalTime().ToString("d 'de' MMMM", Spanish)}";

    public string SeenText => grave.HowItWasSeen;

    /// <summary>The friend this grave belongs to (1.0.5.5), or null for this player's own.</summary>
    public Guid? Owner { get; } = owner;

    /// <summary>A friend's grave whose killcam is on the server, downloaded when it is picked.</summary>
    public bool RemoteKillcam { get; } = remoteKillcam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKillcam))]
    private string? _killcamPath = grave.KillcamPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoKillcamText))]
    private bool _isFetching;

    public bool HasKillcam => KillcamPath is not null;

    /// <summary>Why there is no replay, said plainly.</summary>
    public string NoKillcamText => IsFetching
        ? "Cargando la killcam..."
        : RemoteKillcam
            ? "No se ha podido bajar su killcam."
            : grave.HowItWasSeen == "Marcada a mano"
                ? "Sin repetición."
                : grave.HasBattleRecord
                    ? "Su killcam no llegó a guardarse."
                    : "Sin repetición.";
}

/// <summary>One player's tab above the cemetery (1.0.5.7); <paramref name="Friend"/> is null for this player.</summary>
public sealed record OwnerTab(FriendStatus? Friend, string Name, bool IsSelected);
