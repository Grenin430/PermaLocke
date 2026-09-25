using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.ViewModels;

/// <summary>One changed evolution, ready to draw: who, into whom, and how now.</summary>
/// <param name="Item">The item it needs now, drawn, or null.</param>
/// <param name="LevelsUp">It evolves on a level up, so the card shows a green up arrow.</param>
public sealed record EvolutionCard(BitmapSource? From, string FromName, BitmapSource? To, string ToName,
    string Now, BitmapSource? Item, bool LevelsUp)
{
    public bool HasItem => Item is not null;
}

/// <summary>A group of evolutions that change the same way.</summary>
public sealed record EvolutionGroup(string Title, IReadOnlyList<EvolutionCard> Cards);

/// <summary>One thing a counter sells.</summary>
public sealed record ShopItemCard(BitmapSource? Icon, string Name, string Price);

/// <summary>A counter of the game and what it sells.</summary>
public sealed record GameShop(string Place, IReadOnlyList<ShopItemCard> Items);

/// <summary>
/// INFORMACIÓN: the evolutions PermaLocke changes and what the game's special counters sell, with sprites.
/// </summary>
/// <remarks>
/// Asked for on 2026-09-24 so players do not need the two .txt files. Everything comes from <c>Data/informacion.json</c>,
/// which RomTool «informacion» writes from the cartridge (with the gen 8-9 mod) and <c>Data/randomizer.json</c>, through
/// the same code the randomizer uses: nothing on this screen is typed by hand. Sprites come from the player's own ROM.
/// Only the counters: the TIENDA section of the app is a different thing.
/// </remarks>
public sealed partial class InformationViewModel(AppPaths paths, PokemonSpriteService sprites,
    ILogger<InformationViewModel> logger) : SectionViewModel("INFORMACIÓN")
{
    public override string IconKey => "IconDocument";

    public override GameNeed Needs => GameNeed.None;

    [ObservableProperty]
    private IReadOnlyList<EvolutionGroup> _evolutions = [];

    [ObservableProperty]
    private IReadOnlyList<GameShop> _shops = [];

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _evolutionSearch = string.Empty;

    [ObservableProperty]
    private string _shopSearch = string.Empty;

    private IReadOnlyList<EvolutionGroup> _allEvolutions = [];

    private IReadOnlyList<GameShop> _allShops = [];

    private sealed record Evolution(int Especie, int Forma, string Nombre, int Destino, int DestinoForma,
        string DestinoNombre, string Como, int Objeto, string Ahora, string Antes, bool SubeNivel);

    private sealed record ShopItem(int Id, string Nombre, int Precio);

    private sealed record Shop(string Lugar, List<ShopItem> Objetos);

    private sealed record Data(List<Evolution> Evoluciones, List<Shop> Tiendas);

    private static readonly (string Como, string Title)[] Groups =
    [
        ("nivel", "POR NIVEL"),
        ("objetoDeDia", "SUBIENDO DE NIVEL DE DÍA CON SU OBJETO"),
        ("objeto", "USANDO UN OBJETO"),
        ("compañero", "CON SU PAREJA EN EL EQUIPO"),
        ("otro", "OTRAS FORMAS")
    ];

    partial void OnEvolutionSearchChanged(string value) =>
        Evolutions = [.. _allEvolutions
            .Select(g => g with { Cards = [.. g.Cards.Where(c => Matches(value, c.FromName, c.ToName, c.Now))] })
            .Where(g => g.Cards.Count > 0)];

    partial void OnShopSearchChanged(string value) =>
        Shops = [.. _allShops
            .Select(s => Matches(value, s.Place) ? s : s with { Items = [.. s.Items.Where(i => Matches(value, i.Name))] })
            .Where(s => s.Items.Count > 0)];

    /// <summary>Ignores case and accents, so «pocion» finds «Poción».</summary>
    private static bool Matches(string search, params string[] texts) =>
        string.IsNullOrWhiteSpace(search) || texts.Any(t => CultureInfo.InvariantCulture.CompareInfo
            .IndexOf(t, search.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    public override async Task ActivateAsync()
    {
        if (_allEvolutions.Count > 0)
        {
            return;
        }

        await sprites.PrepareAsync();

        try
        {
            var data = JsonSerializer.Deserialize<Data>(
                await File.ReadAllTextAsync(Path.Combine(paths.Data, "informacion.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            _allEvolutions = [.. Groups
                .Select(g => new EvolutionGroup(g.Title, [.. data.Evoluciones
                    .Where(e => e.Como == g.Como)
                    .Select(e => new EvolutionCard(
                        sprites.Get(e.Especie, e.Forma), e.Nombre,
                        sprites.Get(e.Destino, e.DestinoForma), e.DestinoNombre,
                        e.Ahora, e.Objeto > 0 ? sprites.GetItem(e.Objeto) : null, e.SubeNivel))]))
                .Where(g => g.Cards.Count > 0)];

            _allShops = [.. data.Tiendas.Select(s => new GameShop(s.Lugar,
                [.. s.Objetos.Select(i => new ShopItemCard(sprites.GetItem(i.Id), i.Nombre, i.Precio.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))))]))];

            OnEvolutionSearchChanged(EvolutionSearch);
            OnShopSearchChanged(ShopSearch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer Data/informacion.json");
            Status = "No se ha podido leer la información (falta Data/informacion.json).";
        }
    }
}

/// <summary>One page of INFORMACIÓN in the sidebar. Both share the data of <see cref="InformationViewModel"/>.</summary>
public sealed class InformationPageViewModel(string title, bool shops, InformationViewModel info) : SectionViewModel(title)
{
    public InformationViewModel Info { get; } = info;

    /// <summary>True for TIENDAS, false for EVOLUCIONES.</summary>
    public bool Shops { get; } = shops;

    public override Task ActivateAsync() => Info.ActivateAsync();
}
