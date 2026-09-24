using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Rules.Services;

namespace PermaLocke.Rules.Tests;

/// <summary>
/// The wild battles of a trial are not the route's first encounter until the trial is passed (§160).
/// </summary>
/// <remarks>
/// Found in the Cueva Sotobosque on 2026-09-21: the first of the three den battles of Ilima's trial, where the game does
/// not let a ball be thrown, spent the route and marked it on the map.
/// </remarks>
public sealed class TrialZoneTests
{
    private const int NormaliumZ = 807;
    private const string Dens = "cueva-sotobosque-sala-de-la-prueba";

    private sealed class Bag(IReadOnlyDictionary<int, int> carried) : IItemDelivery
    {
        public Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
            Task.FromResult(carried.TryGetValue(itemId, out var count) ? count : -1);

        public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
            Task.FromResult(carried);
    }

    private sealed class Catalog(params Achievement[] all) : IAchievementCatalog
    {
        public IReadOnlyList<Achievement> All { get; } = all;
    }

    private static TrialZoneService Service(IReadOnlyDictionary<int, int> carried) =>
        new(new RulesConfiguration
        {
            BallControl = new BallControlSettings
            {
                TrialZones = [new TrialZone("prueba de Liam, las madrigueras", Dens, "prueba-01")]
            }
        },
        new Catalog(new Achievement("prueba-01", "Primera prueba", "", null, "", 1, 200, Item: NormaliumZ)),
        new Bag(carried),
        NullLogger<TrialZoneService>.Instance);

    [Fact]
    public async Task Before_the_crystal_the_dens_belong_to_the_trial()
    {
        var pending = await Service(new Dictionary<int, int> { [NormaliumZ] = 0 }).PendingAsync(Dens);

        Assert.Equal("prueba de Liam, las madrigueras", pending);
    }

    [Fact]
    public async Task With_the_crystal_the_dens_are_a_route_like_any_other()
    {
        Assert.Null(await Service(new Dictionary<int, int> { [NormaliumZ] = 1 }).PendingAsync(Dens));
    }

    [Fact]
    public async Task Any_other_zone_is_untouched()
    {
        Assert.Null(await Service(new Dictionary<int, int> { [NormaliumZ] = 0 }).PendingAsync("ruta-1"));
    }

    /// <summary>A bag that cannot be read is «not known», and the battle counts as it always did (§68).</summary>
    [Fact]
    public async Task An_unreadable_bag_leaves_the_rule_as_it_was()
    {
        Assert.Null(await Service(new Dictionary<int, int>()).PendingAsync(Dens));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// The shipped list names zones that exist and trials with a crystal: a typo in either would make the rule silently
    /// do nothing, which is the one way it must not fail.
    /// </summary>
    [Fact]
    public void Every_shipped_trial_zone_exists_and_its_trial_has_a_crystal()
    {
        var rules = RulesConfigurationLoader.Load(Path.Combine(Root(), "Data", "rules.json"));
        Assert.NotEmpty(rules.BallControl.TrialZones);

        using var maps = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Data", "mapas.json")));
        var zones = maps.RootElement.GetProperty("mapas").EnumerateArray()
            .Select(map => map.GetProperty("zona").GetString()).ToHashSet();

        using var achievements = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "Data", "achievements.json")));
        var crystals = achievements.RootElement.GetProperty("achievements").EnumerateArray()
            .Where(achievement => achievement.TryGetProperty("item", out _))
            .ToDictionary(achievement => achievement.GetProperty("id").GetString()!,
                achievement => achievement.GetProperty("item").GetInt32());

        foreach (var trial in rules.BallControl.TrialZones)
        {
            Assert.Contains(trial.Zone, zones);
            Assert.True(crystals.TryGetValue(trial.Achievement, out var crystal) && crystal is >= 807 and <= 824,
                $"{trial.Zone}: el logro {trial.Achievement} no tiene un cristal Z de tipo");
        }
    }
}
