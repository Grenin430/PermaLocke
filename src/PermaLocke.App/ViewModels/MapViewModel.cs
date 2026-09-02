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

/// <summary>One zone on the map: free, spent, or spent by somebody who died.</summary>
public sealed partial class MapZoneViewModel(int number, string name, string island, double left, double top)
    : ObservableObject
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

    /// <summary>The id the run stores against a capture, so a marker and a Pokémon match up.</summary>
    public string ZoneId { get; } = EncounterService.NormaliseLocationId(name);

    /// <summary>Where it sits on its island's picture, in that picture's own pixels.</summary>
    public double Left { get; } = left;

    public double Top { get; } = top;

    [ObservableProperty]
    private string _caughtWhat = string.Empty;

    [ObservableProperty]
    private bool _isSpent;

    [ObservableProperty]
    private bool _isDead;

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

/// <summary>One island and its catchable zones.</summary>
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
/// an empty list to compare against. Clicking a zone here is what says otherwise, and it is
/// recorded as a claim by the player — <c>ZoneConfirmed</c> — never as a deduction.
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
    private readonly EncounterService _encounters;
    private readonly IPokemonRepository _pokemon;
    private readonly IRunContext _runContext;
    private readonly JsonIslandMap _map;
    private readonly IslandMapService _art;
    private readonly JsonZoneMarkers _markers;
    private readonly ILogger<MapViewModel> _logger;

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

    public ObservableCollection<PendingCaptureViewModel> Pending { get; } = [];

    [ObservableProperty]
    private IslandViewModel? _selectedIsland;

    [ObservableProperty]
    private PendingCaptureViewModel? _selected;

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

    partial void OnSelectedIslandChanged(IslandViewModel? value)
    {
        OnPropertyChanged(nameof(IslandImage));
        OnPropertyChanged(nameof(HasArt));
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
                    marker.X * picture.PixelWidth, marker.Y * picture.PixelHeight));
            }

            if (zones.Count > 0)
            {
                Islands.Add(new IslandViewModel(island, zones));
            }
        }

        ZoneCount = Islands.Sum(island => island.Zones.Count);
        SelectedIsland ??= Islands.FirstOrDefault();
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
            var all = await _pokemon.GetAllAsync(run.Id);

            var spent = all
                .Where(entry => entry is { ConsumedZoneEncounter: true, LocationId: not null })
                .GroupBy(entry => entry.LocationId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var count = 0;
            var drawn = new HashSet<string>(StringComparer.Ordinal);

            foreach (var island in Islands)
            {
                var here = 0;

                foreach (var zone in island.Zones)
                {
                    drawn.Add(zone.ZoneId);
                    var owner = spent.GetValueOrDefault(zone.ZoneId);

                    zone.IsSpent = owner is not null;
                    zone.IsDead = owner?.Status == PokemonStatus.Dead;
                    zone.CaughtWhat = owner is null ? string.Empty : Label(owner);

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

            foreach (var entry in all
                .Where(entry => entry is { ConsumedZoneEncounter: false, Origin: PokemonOrigin.Capture })
                .OrderBy(entry => entry.ObtainedAt))
            {
                Pending.Add(new PendingCaptureViewModel(entry.Id, Label(entry),
                    entry.LocationId ?? "sin lugar"));
            }

            Selected ??= Pending.FirstOrDefault();
            OnPropertyChanged(nameof(HasPending));

            // Una zona gastada que no está en el mapa no se puede ver ni liberar, así que se dice.
            // Pasaría si alguien recorta marcadores.json con una run ya empezada, y callarlo
            // dejaría un encuentro gastado que la pantalla jura que sigue libre.
            var orphans = spent.Keys.Where(id => !drawn.Contains(id)).ToArray();

            if (orphans.Length > 0)
            {
                Say($"{orphans.Length} zonas gastadas no están en el mapa: "
                    + string.Join(", ", orphans.Take(5)), bad: true);
            }
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
