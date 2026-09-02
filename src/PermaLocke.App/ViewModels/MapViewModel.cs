using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.Infrastructure;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One zone on the map, and what the player marked happened there.</summary>
public sealed partial class MapZoneViewModel(int number, string name, string island, double left,
    double top, System.Windows.Media.Imaging.BitmapSource? photo) : ObservableObject
{
    /// <summary>Its place in its island, which is what the marker shows.</summary>
    /// <remarks>
    /// A name does not fit on a marker, and shrinking one to fit makes a wall of text nobody reads.
    /// A number fits, the name is one hover away, and the whole point of the screen — how much of
    /// Alola is spent — survives being looked at from across the room.
    /// </remarks>
    public int Number { get; } = number;

    public string Name { get; } = name;

    public string Island { get; } = island;

    /// <summary>The zone.s stable id, which is what a mark is stored against.</summary>
    public string ZoneId { get; } = EncounterService.NormaliseLocationId(name);

    /// <summary>Where it sits on its island's picture, in that picture's own pixels.</summary>
    public double Left { get; } = left;

    public double Top { get; } = top;

    /// <summary>A picture of the place, for the card that appears on hover.</summary>
    /// <remarks>
    /// Null when there is none, and the card then shows just the name. A missing picture is a
    /// smaller card, never an empty frame.
    /// </remarks>
    public System.Windows.Media.Imaging.BitmapSource? Photo { get; } = photo;

    public bool HasPhoto => Photo is not null;

    /// <summary>What the player has marked happened here. Each click moves it on one.</summary>
    [ObservableProperty]
    private ZoneOutcome _outcome;

    /// <summary>
    /// The outcome's own name, so the template picks a look without four triggers of its own.
    /// </summary>
    public string State => Outcome.ToString();

    /// <summary>The line under the name when hovering.</summary>
    public string Tooltip => ZoneOutcomeService.Label(Outcome);

    /// <summary>An unmarked zone is drawn hollow, so the map reads as what is left to do.</summary>
    public bool IsMarked => Outcome != ZoneOutcome.Free;

    partial void OnOutcomeChanged(ZoneOutcome value)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(IsMarked));
    }
}

/// <summary>One island and its catchable zones.</summary>
public sealed partial class IslandViewModel(string name, IReadOnlyList<MapZoneViewModel> zones)
    : ObservableObject
{
    public string Name { get; } = name;

    public IReadOnlyList<MapZoneViewModel> Zones { get; } = zones;

    public int Count => Zones.Count;

    /// <summary>How many of this island.s zones are marked, so each one carries its own score.</summary>
    [ObservableProperty]
    private int _spentCount;

    /// <summary>Which island the map is showing, so its button can say so.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Every zone on it accounted for, which is the thing worth seeing from the sidebar.</summary>
    public bool IsComplete => Count > 0 && SpentCount >= Count;

    partial void OnSpentCountChanged(int value) => OnPropertyChanged(nameof(IsComplete));
}

/// <summary>
/// The map of Alola: a board of what happened at each zone.
/// </summary>
/// <remarks>
/// <para>
/// Clicking a marker walks it round four states — sin marcar, atrapado, muerto, huida — and that is
/// all it does. It arbitrates nothing: no rule reads these marks and no points move, because the
/// player asked for something to look at rather than a referee. The first-encounter rule is
/// switched off in <c>Data/rules.json</c> for the same reason, rather than left enabled with
/// nothing feeding it, which is exactly the §81 fault.
/// </para>
/// <para>
/// The island art comes out of the player's own cartridge. Where each marker goes does not: nothing
/// in the RomFS says where Ruta 3 is on the picture, so somebody placed the sixty-one markers by
/// hand once, and <c>Data/marcadores.json</c> is what that produced. Unlike the pictures, that file
/// <b>ships with PermaLocke</b>: one person places them and everybody in the competition has them.
/// </para>
/// <para>
/// Which is why there is no placing mode any more. The map is finished and travels finished, and a
/// screen that lets five people each drag Alola around would end with five different Alolas. The
/// file stays the single source of truth, so correcting it later is a small change here rather than
/// something anyone can do by accident.
/// </para>
/// <para>
/// Zones the file marks as having no encounters are <b>not built at all</b>. Drawing them greyed
/// out was worse than useless: they can never be spent, so they are not part of the map, and
/// leaving them in made the count something that could never reach its own top.
/// </para>
/// </remarks>
public sealed partial class MapViewModel : SectionViewModel
{
    private readonly ZoneOutcomeService _outcomes;
    private readonly IRunContext _runContext;
    private readonly JsonIslandMap _map;
    private readonly IslandMapService _art;
    private readonly JsonZoneMarkers _markers;
    private readonly ZonePhotoService _photos;
    private readonly ILogger<MapViewModel> _logger;

    public MapViewModel(ZoneOutcomeService outcomes, IRunContext runContext, JsonIslandMap map,
        IslandMapService art, ZonePhotoService photos, AppPaths paths, ILogger<MapViewModel> logger)
        : base("MAPA", "Las zonas de Alola: marca lo que pasó en cada una")
    {
        _outcomes = outcomes;
        _runContext = runContext;
        _map = map;
        _art = art;
        _photos = photos;
        _logger = logger;
        _markers = JsonZoneMarkers.Load(Path.Combine(paths.Data, "marcadores.json"));
    }

    public override string IconKey => "IconGrid";

    /// <summary>Nothing here touches the game: it is the run's own bookkeeping.</summary>
    public override GameNeed Needs => GameNeed.None;

    public override async Task ActivateAsync()
    {
        // Las islas no se pueden construir hasta que las imagenes esten, porque la posicion de cada
        // marcador se guarda en fraccion y hay que multiplicarla por el tamaño de su dibujo.
        await _art.PrepareAsync();
        BuildIslands();
        await RefreshAsync();
    }

    public ObservableCollection<IslandViewModel> Islands { get; } = [];

    [ObservableProperty]
    private IslandViewModel? _selectedIsland;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _statusIsBad;

    [ObservableProperty]
    private int _spentCount;

    [ObservableProperty]
    private int _zoneCount;

    /// <summary>False when islas.json is missing, so the screen says so instead of drawing nothing.</summary>
    public bool HasMap => _map.Zones.Count > 0;

    /// <summary>Whether the island pictures could be extracted from the cartridge.</summary>
    public bool HasArt => _art.IsAvailable && IslandImage is not null;

    public BitmapSource? IslandImage =>
        SelectedIsland is null ? null : _art.Map(SelectedIsland.Name);

    partial void OnSelectedIslandChanged(IslandViewModel? value)
    {
        OnPropertyChanged(nameof(IslandImage));
        OnPropertyChanged(nameof(HasArt));

        // La marca vive en cada isla y no en un disparador sobre el elemento, porque los botones
        // no son una lista con seleccion: son cuatro botones sueltos y nadie mas sabe cual manda.
        foreach (var island in Islands)
        {
            island.IsSelected = ReferenceEquals(island, value);
        }
    }

    [RelayCommand]
    public void SelectIsland(IslandViewModel? island)
    {
        if (island is not null)
        {
            SelectedIsland = island;
        }
    }

    /// <summary>
    /// Builds the islands from the marker file, leaving out everything that cannot be caught in.
    /// </summary>
    private void BuildIslands()
    {
        Islands.Clear();

        foreach (var island in JsonIslandMap.Order)
        {
            var picture = _art.Map(island);
            var zones = new List<MapZoneViewModel>();
            var number = 1;

            foreach (var zone in _map.Of(island))
            {
                var id = EncounterService.NormaliseLocationId(zone.Name);
                var marker = _markers.For(id);

                // Sin marcador no hay sitio donde dibujarla, y sin dibujo no se puede pinchar.
                if (marker is null || picture is null || _markers.NoEncounters.Contains(id))
                {
                    continue;
                }

                zones.Add(new MapZoneViewModel(number++, zone.Name, island,
                    marker.X * picture.PixelWidth, marker.Y * picture.PixelHeight, _photos.Photo(id)));
            }

            if (zones.Count > 0)
            {
                Islands.Add(new IslandViewModel(island, zones));
            }
        }

        ZoneCount = Islands.Sum(island => island.Zones.Count);
        SelectedIsland ??= Islands.FirstOrDefault();

        // La primera vez, el ??= no dispara el cambio porque no lo hay: se marca a mano.
        foreach (var island in Islands)
        {
            island.IsSelected = ReferenceEquals(island, SelectedIsland);
        }
        OnPropertyChanged(nameof(IslandImage));
        OnPropertyChanged(nameof(HasArt));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var run = _runContext.Current;

        if (run is null)
        {
            return;
        }

        try
        {
            var outcomes = await _outcomes.GetAsync(run.Id);
            var count = 0;

            foreach (var island in Islands)
            {
                var here = 0;

                foreach (var zone in island.Zones)
                {
                    zone.Outcome = outcomes.GetValueOrDefault(zone.ZoneId);

                    if (zone.IsMarked)
                    {
                        here++;
                    }
                }

                island.SpentCount = here;
                count += here;
            }

            SpentCount = count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo refrescar el mapa");
            Say("No se pudo leer la run.", bad: true);
        }
    }

    /// <summary>
    /// Clicking a marker moves it on one: sin marcar, atrapado, muerto, huida, y vuelta a empezar.
    /// </summary>
    /// <remarks>
    /// A cycle and not four buttons because the map has sixty-one markers and a menu on each would
    /// bury the thing it is for. Going round rather than stopping at the last state is what makes a
    /// mis-click cost three clicks instead of a trip somewhere else to undo it.
    /// </remarks>
    [RelayCommand]
    public async Task ClickZoneAsync(MapZoneViewModel? zone)
    {
        var run = _runContext.Current;

        if (zone is null || run is null)
        {
            return;
        }

        var next = ZoneOutcomeService.Next(zone.Outcome);

        try
        {
            await _outcomes.SetAsync(run.Id, zone.ZoneId, zone.Name, next, run.PlayerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo marcar {Zone}", zone.Name);
            Say($"No se pudo marcar {zone.Name}.", bad: true);
            return;
        }

        zone.Outcome = next;
        Say($"{zone.Name}: {ZoneOutcomeService.Label(next).ToLowerInvariant()}.", bad: false);
        await RefreshAsync();
    }

    private void Say(string what, bool bad)
    {
        Status = what;
        StatusIsBad = bad;
    }
}
