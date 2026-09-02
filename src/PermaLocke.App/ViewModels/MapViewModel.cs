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

/// <summary>One zone: free, spent, or spent by somebody who died.</summary>
public sealed partial class MapZoneViewModel(int number, string name, string island) : ObservableObject
{
    /// <summary>Its place in its island, which is what the marker shows.</summary>
    /// <remarks>
    /// A hundred and thirteen names do not fit on a map, and shrinking them to fit makes a wall of
    /// text nobody reads. A number fits, the name is one hover away, and the whole point of the
    /// screen — how much of Alola is spent — survives being looked at from across the room.
    /// </remarks>
    public int Number { get; } = number;

    public string Name { get; } = name;

    public string Island { get; } = island;

    /// <summary>The id the run stores against a capture, so a marker and a Pokémon match up.</summary>
    public string ZoneId { get; } = EncounterService.NormaliseLocationId(name);

    [ObservableProperty]
    private string _caughtWhat = string.Empty;

    [ObservableProperty]
    private bool _isSpent;

    [ObservableProperty]
    private bool _isDead;

    /// <summary>Where it sits on its island.s picture, in pixels.</summary>
    /// <remarks>
    /// Plain doubles with a flag beside them, and not nullables: Canvas.Left does not take a null,
    /// so a hundred and thirteen unplaced zones would each log a binding failure on every refresh.
    /// </remarks>
    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private bool _isPlaced;

    /// <summary>
    /// "free" / "spent" / "dead", so the template picks a look without three triggers of its own.
    /// A zone whose Pokémon died stays spent — that is the whole point of a Nuzlocke — but it reads
    /// differently, and a player wants to see the graveyard at a glance.
    /// </summary>
    public string State => !IsSpent ? "free" : IsDead ? "dead" : "spent";

    /// <summary>The line under the name when hovering: who spent it, or that it is still free.</summary>
    public string Tooltip => !IsSpent
        ? "Libre. Todavía no has gastado su encuentro."
        : IsDead ? $"{CaughtWhat} — murió" : CaughtWhat;

    partial void OnIsSpentChanged(bool value)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Tooltip));
    }

    partial void OnIsDeadChanged(bool value)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Tooltip));
    }

    partial void OnCaughtWhatChanged(string value) => OnPropertyChanged(nameof(Tooltip));

}

/// <summary>A capture that has not yet said which zone it spent.</summary>
public sealed record PendingCaptureViewModel(Guid Id, string Label, string Where);

/// <summary>One island and its zones.</summary>
public sealed partial class IslandViewModel(string name, IReadOnlyList<MapZoneViewModel> zones)
    : ObservableObject
{
    public string Name { get; } = name;

    public IReadOnlyList<MapZoneViewModel> Zones { get; } = zones;

    public int Count => Zones.Count;

    /// <summary>How many of this island's zones are spent, so each one carries its own score.</summary>
    [ObservableProperty]
    private int _spentCount;
}

/// <summary>
/// The map of Alola: which zones the run has spent and which are still free.
/// </summary>
/// <remarks>
/// <para>
/// It exists because the first-encounter rule had never fired once. The seventh generation stores
/// no field saying an encounter was wild — measured on the real save, where a gift and a wild
/// capture are identical down to the ball — so the watcher registers every automatic capture as
/// <see cref="EncounterType.Unknown"/>, which spends no zone, and the rule spent the whole run with
/// an empty list to compare against.
/// </para>
/// <para>
/// So somebody has to say it, and this is the pleasant way of saying it: the player clicks the zone
/// on the island and watches Alola fill up. The claim recorded is the same as answering a dialog,
/// and it is recorded as a claim — <c>ZoneConfirmed</c>, by the player — never as a deduction.
/// </para>
/// <para>
/// The island art comes out of the player's own cartridge. Where each marker goes does not: nothing
/// in the RomFS says where Ruta 3 is on the picture, so the positions are placed by hand here and
/// saved to <c>Data/marcadores.json</c>, which unlike the pictures is PermaLocke's own work and
/// travels with it.
/// </para>
/// </remarks>
public sealed partial class MapViewModel : SectionViewModel
{
    private readonly EncounterService _encounters;
    private readonly IPokemonRepository _pokemon;
    private readonly IRunContext _runContext;
    private readonly JsonIslandMap _map;
    private readonly IslandMapService _art;
    private readonly AppPaths _paths;
    private readonly ILogger<MapViewModel> _logger;

    private JsonZoneMarkers _markers;

    public MapViewModel(EncounterService encounters, IPokemonRepository pokemon,
        IRunContext runContext, JsonIslandMap map, IslandMapService art, AppPaths paths,
        ILogger<MapViewModel> logger)
        : base("MAPA", "Las zonas de Alola: cuál gastó cada captura y cuáles quedan libres")
    {
        _encounters = encounters;
        _pokemon = pokemon;
        _runContext = runContext;
        _map = map;
        _art = art;
        _paths = paths;
        _logger = logger;
        _markers = JsonZoneMarkers.Load(MarkerPath);

        foreach (var island in JsonIslandMap.Order)
        {
            var zones = map.Of(island);

            if (zones.Count == 0)
            {
                continue;
            }

            Islands.Add(new IslandViewModel(island,
                [.. zones.Select((zone, at) => new MapZoneViewModel(at + 1, zone.Name, island))]));
        }

        ZoneCount = Islands.Sum(island => island.Zones.Count);
        _selectedIsland = Islands.FirstOrDefault();
    }

    private string MarkerPath => Path.Combine(_paths.Data, "marcadores.json");

    public override string IconKey => "IconGrid";

    /// <summary>Nothing here touches the game: it is the run's own bookkeeping.</summary>
    public override GameNeed Needs => GameNeed.None;

    public override async Task ActivateAsync()
    {
        await _art.PrepareAsync();
        OnPropertyChanged(nameof(IslandImage));
        OnPropertyChanged(nameof(HasArt));
        await RefreshAsync();
    }

    public ObservableCollection<IslandViewModel> Islands { get; } = [];

    public ObservableCollection<PendingCaptureViewModel> Pending { get; } = [];

    [ObservableProperty]
    private IslandViewModel? _selectedIsland;

    [ObservableProperty]
    private PendingCaptureViewModel? _selected;

    /// <summary>The zone whose marker the next click on the map will place.</summary>
    [ObservableProperty]
    private MapZoneViewModel? _placing;

    /// <summary>Whether clicking the map drops a marker instead of spending a zone.</summary>
    /// <remarks>
    /// An explicit switch and not a guess from what is selected: the same click would otherwise
    /// mean two different things, and one of them writes to the run's history.
    /// </remarks>
    [ObservableProperty]
    private bool _isPlacingMode;

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

    public bool HasPending => Pending.Count > 0;

    public BitmapSource? IslandImage =>
        SelectedIsland is null ? null : _art.Map(SelectedIsland.Name);

    /// <summary>This island.s zones, the ones still to place first.</summary>
    /// <remarks>
    /// Todas y no solo las que faltan, que es como estaba y era un callejon: una colocada por
    /// error no aparecia en ninguna lista, asi que no habia forma de elegirla para quitarla.
    /// </remarks>
    public ObservableCollection<MapZoneViewModel> Placeable { get; } = [];

    public string MarkerCount => $"{_markers.Count} de {ZoneCount} colocadas";

    /// <summary>Whether the chosen zone has a pin to take off.</summary>
    public bool CanForget => Placing is { IsPlaced: true };

    partial void OnSelectedIslandChanged(IslandViewModel? value)
    {
        OnPropertyChanged(nameof(IslandImage));
        OnPropertyChanged(nameof(HasArt));
        Placing = null;
        RefreshPlaceable();
    }

    partial void OnPlacingChanged(MapZoneViewModel? value) => OnPropertyChanged(nameof(CanForget));

    [RelayCommand]
    public void SelectIsland(IslandViewModel? island)
    {
        if (island is not null)
        {
            SelectedIsland = island;
        }
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
            var all = await _pokemon.GetAllAsync(run.Id);

            var spent = all
                .Where(entry => entry is { ConsumedZoneEncounter: true, LocationId: not null })
                .GroupBy(entry => entry.LocationId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var count = 0;

            foreach (var island in Islands)
            {
                var here = 0;
                var picture = _art.Map(island.Name);

                foreach (var zone in island.Zones)
                {
                    var owner = spent.GetValueOrDefault(zone.ZoneId);

                    zone.IsSpent = owner is not null;
                    zone.IsDead = owner?.Status == PokemonStatus.Dead;
                    zone.CaughtWhat = owner is null ? string.Empty : Label(owner);

                    var marker = _markers.For(zone.ZoneId);

                    // La posicion se guarda en fraccion y se pinta en pixeles, asi que sin la
                    // imagen no hay donde ponerla y el marcador se queda sin colocar.
                    zone.IsPlaced = marker is not null && picture is not null;
                    zone.Left = zone.IsPlaced ? marker!.X * picture!.PixelWidth : 0;
                    zone.Top = zone.IsPlaced ? marker!.Y * picture!.PixelHeight : 0;

                    if (owner is not null)
                    {
                        here++;
                    }
                }

                island.SpentCount = here;
                count += here;
            }

            SpentCount = count;
            Pending.Clear();

            // Lo que falta por decir: capturas que no han gastado zona. Las entregas del gacha y
            // los wonder trades no salen, porque nunca gastaron el encuentro de ningún sitio.
            foreach (var entry in all
                .Where(entry => entry is { ConsumedZoneEncounter: false, Origin: PokemonOrigin.Capture })
                .OrderBy(entry => entry.ObtainedAt))
            {
                Pending.Add(new PendingCaptureViewModel(entry.Id, Label(entry),
                    entry.LocationId ?? "sin lugar"));
            }

            Selected ??= Pending.FirstOrDefault();
            RefreshPlaceable();
            OnPropertyChanged(nameof(HasPending));
            OnPropertyChanged(nameof(MarkerCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo refrescar el mapa");
            Say("No se pudo leer la run.", bad: true);
        }
    }

    /// <summary>Clicking a marker: spends its zone on the selected capture, or frees it.</summary>
    [RelayCommand]
    public async Task ClickZoneAsync(MapZoneViewModel? zone)
    {
        var run = _runContext.Current;

        if (zone is null || run is null)
        {
            return;
        }

        // En modo colocar, pinchar un marcador es elegirlo para moverlo, no gastar su zona.
        if (IsPlacingMode)
        {
            Placing = zone;
            Say($"Pincha en el mapa dónde va {zone.Name}.", bad: false);
            return;
        }

        if (zone.IsSpent)
        {
            await FreeAsync(run, zone);
            return;
        }

        if (Selected is null)
        {
            Say("Elige primero qué captura gastó esta zona.", bad: true);
            return;
        }

        var chosen = Selected;
        var result = await _encounters.ConfirmZoneAsync(run.Id, chosen.Id, zone.Name, run.PlayerName);

        if (!result.Confirmed)
        {
            Say(result.Reason ?? "No se pudo.", bad: true);
            return;
        }

        Say($"{chosen.Label} gastó el encuentro de {zone.Name}.", bad: false);
        Selected = null;
        await RefreshAsync();
    }

    /// <summary>Drops the chosen zone's marker where the map was clicked.</summary>
    /// <param name="x">Across the picture, in its own pixels.</param>
    /// <param name="y">Down the picture, in its own pixels.</param>
    public async Task PlaceAtAsync(double x, double y)
    {
        var picture = IslandImage;
        var island = SelectedIsland;

        if (!IsPlacingMode || Placing is null || picture is null || island is null)
        {
            return;
        }

        var zone = Placing;

        // Se guarda en FRACCION de la imagen, no en pixeles: si el mapa se vuelve a extraer con
        // otro recorte, un marcador en pixeles apuntaria a otro sitio sin que nada fallara.
        _markers = _markers.With(zone.ZoneId,
            new ZoneMarker(island.Name,
                Math.Clamp(x / picture.PixelWidth, 0, 1),
                Math.Clamp(y / picture.PixelHeight, 0, 1)));

        try
        {
            await _markers.SaveAsync(MarkerPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar el marcador de {Zone}", zone.Name);
            Say("No se pudo guardar el marcador.", bad: true);
            return;
        }

        zone.Left = x;
        zone.Top = y;
        zone.IsPlaced = true;

        Say($"{zone.Name} colocada.", bad: false);
        OnPropertyChanged(nameof(CanForget));
        Placing = Placeable.FirstOrDefault(candidate => candidate != zone && !candidate.IsPlaced);
        RefreshPlaceable();
        OnPropertyChanged(nameof(MarkerCount));
    }

    /// <summary>Takes a marker off the map. The zone keeps whatever it had; only the pin goes.</summary>
    [RelayCommand]
    public async Task ForgetMarkerAsync()
    {
        var zone = Placing;

        if (zone is null || !zone.IsPlaced)
        {
            return;
        }

        _markers = _markers.Without(zone.ZoneId);

        try
        {
            await _markers.SaveAsync(MarkerPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar tras quitar el marcador de {Zone}", zone.Name);
            Say("No se pudo guardar.", bad: true);
            return;
        }

        zone.IsPlaced = false;
        Say($"{zone.Name} vuelve a estar sin colocar.", bad: false);
        RefreshPlaceable();
        OnPropertyChanged(nameof(MarkerCount));
        OnPropertyChanged(nameof(CanForget));
    }

    private void RefreshPlaceable()
    {
        Placeable.Clear();

        if (SelectedIsland is null)
        {
            return;
        }

        // Las que faltan primero: es lo que se esta haciendo, y las ya puestas solo se buscan
        // cuando hay que corregir una.
        foreach (var zone in SelectedIsland.Zones.OrderBy(zone => zone.IsPlaced).ThenBy(zone => zone.Number))
        {
            Placeable.Add(zone);
        }

        Placing ??= Placeable.FirstOrDefault(zone => !zone.IsPlaced);
    }

    private async Task FreeAsync(Run run, MapZoneViewModel zone)
    {
        var all = await _pokemon.GetAllAsync(run.Id);
        var owner = all.FirstOrDefault(entry =>
            entry.ConsumedZoneEncounter && entry.LocationId == zone.ZoneId);

        if (owner is null)
        {
            return;
        }

        var result = await _encounters.ClearZoneAsync(run.Id, owner.Id, run.PlayerName);

        Say(result.Confirmed ? $"{zone.Name} vuelve a estar libre." : result.Reason ?? "No se pudo.",
            bad: !result.Confirmed);

        await RefreshAsync();
    }

    private static string Label(PokemonEntry entry) => entry.Nickname is { Length: > 0 } nickname
        ? $"{nickname} ({entry.SpeciesName})"
        : entry.SpeciesName;

    private void Say(string what, bool bad)
    {
        Status = what;
        StatusIsBad = bad;
    }
}
