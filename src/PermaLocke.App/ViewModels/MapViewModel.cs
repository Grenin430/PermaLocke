using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One zone on the map: free, spent, or spent by somebody who died.</summary>
public sealed partial class MapZoneViewModel(string name, string island) : ObservableObject
{
    public string Name { get; } = name;

    public string Island { get; } = island;

    [ObservableProperty]
    private string _caughtWhat = string.Empty;

    [ObservableProperty]
    private bool _isSpent;

    [ObservableProperty]
    private bool _isDead;

    /// <summary>
    /// "free" / "spent" / "dead", so the template picks a look without three triggers of its own.
    /// A zone whose Pokemon died stays spent -- that is the whole point of a Nuzlocke -- but it
    /// reads differently, and a player wants to see the graveyard at a glance.
    /// </summary>
    public string State => !IsSpent ? "free" : IsDead ? "dead" : "spent";

    partial void OnIsSpentChanged(bool value) => OnPropertyChanged(nameof(State));

    partial void OnIsDeadChanged(bool value) => OnPropertyChanged(nameof(State));
}

/// <summary>A capture that has not yet said which zone it spent.</summary>
public sealed record PendingCaptureViewModel(Guid Id, string Label, string Where);

/// <summary>One island and its zones, which is how the map is laid out.</summary>
public sealed class IslandViewModel(string name, IReadOnlyList<MapZoneViewModel> zones)
{
    public string Name { get; } = name;

    public IReadOnlyList<MapZoneViewModel> Zones { get; } = zones;

    public int Count => Zones.Count;
}

/// <summary>
/// The map of Alola: which zones the run has spent and which are still free.
/// </summary>
/// <remarks>
/// <para>
/// It exists because the first-encounter rule had never fired once. The seventh generation stores
/// no field saying an encounter was wild -- measured on the real save, where a gift and a wild
/// capture are identical down to the ball -- so the watcher registers every automatic capture as
/// <see cref="EncounterType.Unknown"/>, which spends no zone, and the rule spent the whole run
/// with an empty list to compare against.
/// </para>
/// <para>
/// So somebody has to say it, and this is the pleasant way of saying it: instead of answering
/// "was it wild?" in a dialog, the player clicks the zone and watches Alola fill up. The claim
/// recorded is the same either way, and it is recorded as a claim -- ZoneConfirmed, by the player
/// -- never as something the application worked out.
/// </para>
/// </remarks>
public sealed partial class MapViewModel : SectionViewModel
{
    private readonly EncounterService _encounters;
    private readonly IPokemonRepository _pokemon;
    private readonly IRunContext _runContext;
    private readonly JsonIslandMap _map;
    private readonly ILogger<MapViewModel> _logger;

    public MapViewModel(EncounterService encounters, IPokemonRepository pokemon,
        IRunContext runContext, JsonIslandMap map, ILogger<MapViewModel> logger)
        : base("MAPA", "Las zonas de Alola: cuál gastó cada captura y cuáles quedan libres")
    {
        _encounters = encounters;
        _pokemon = pokemon;
        _runContext = runContext;
        _map = map;
        _logger = logger;

        foreach (var island in JsonIslandMap.Order)
        {
            var zones = map.Of(island);

            if (zones.Count == 0)
            {
                continue;
            }

            Islands.Add(new IslandViewModel(island,
                [.. zones.Select(zone => new MapZoneViewModel(zone.Name, island))]));
        }

        ZoneCount = Islands.Sum(island => island.Zones.Count);
    }

    /// <summary>Se relee al entrar: el vigilante puede haber registrado capturas por su cuenta.</summary>
    public override Task ActivateAsync() => RefreshAsync();

    public override string IconKey => "IconGrid";

    /// <summary>Nothing here touches the game: it is the run's own bookkeeping.</summary>
    public override GameNeed Needs => GameNeed.None;

    public ObservableCollection<IslandViewModel> Islands { get; } = [];

    public ObservableCollection<PendingCaptureViewModel> Pending { get; } = [];

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

    /// <summary>False when the file is missing, so the screen says so instead of drawing nothing.</summary>
    public bool HasMap => _map.Zones.Count > 0;

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

            foreach (var zone in Islands.SelectMany(island => island.Zones))
            {
                var here = spent.GetValueOrDefault(EncounterService.NormaliseLocationId(zone.Name));

                zone.IsSpent = here is not null;
                zone.IsDead = here?.Status == PokemonStatus.Dead;
                zone.CaughtWhat = here is null ? string.Empty : Label(here);

                if (here is not null)
                {
                    count++;
                }
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo refrescar el mapa");
            Say("No se pudo leer la run.", bad: true);
        }
    }

    /// <summary>Clicking a zone: spends it on the selected capture, or frees it if it was spent.</summary>
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
        var id = EncounterService.NormaliseLocationId(zone.Name);
        var all = await _pokemon.GetAllAsync(run.Id);
        var owner = all.FirstOrDefault(entry => entry.ConsumedZoneEncounter && entry.LocationId == id);

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
