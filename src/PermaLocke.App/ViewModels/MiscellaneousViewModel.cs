using PermaLocke.App.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;

namespace PermaLocke.App.ViewModels;

/// <summary>One thing a prize hands over, with the drawing the cartridge has for it.</summary>
/// <remarks>
/// The icon and not just the words. The application already pulls the 769 item icons out of the
/// player.s own ROM for the shop, and the prizes were describing themselves in text next to a
/// screen full of those same drawings.
/// </remarks>
public sealed record RewardGiftViewModel(int Amount, string Name,
    System.Windows.Media.Imaging.BitmapSource? Icon);

/// <summary>One of the competition.s one-off prizes, as the screen shows it.</summary>
/// <param name="Detail">What it hands over, spelled out, so nobody has to press to find out.</param>
/// <param name="Share">0 to 1, for the bar. A «8/12» in a small chip is a figure to decode; a bar
/// is how far you are, which is what the question actually was.</param>
public sealed record RewardRowViewModel(
    string Id, string Name, string Description, string Detail,
    string Progress, bool CanClaim, bool Claimed, bool Automatic = false,
    double Share = 0, IReadOnlyList<RewardGiftViewModel>? Gifts = null)
{
    public IReadOnlyList<RewardGiftViewModel> Gives { get; } = Gifts ?? [];

    /// <summary>
    /// What the button says.
    /// </summary>
    /// <remarks>
    /// A prize already taken says so instead of looking pressable. An automatic one that is still
    /// waiting says it is coming on its own, because a button next to "it arrives by itself" reads
    /// as a contradiction -- it still works if pressed, and it is the same code path, but nobody
    /// should feel they have to.
    /// </remarks>
    public string Action => Claimed
        ? "YA RECOGIDO"
        : Automatic && CanClaim ? "LLEGA SOLO" : "RECOGER";
}

/// <summary>
/// Testing tools that write straight into the running game, and the competition's one-off prizes.
/// </summary>
/// <remarks>
/// The tools exist to exercise rules that are otherwise slow to reach — the level cap needs Rare
/// Candies — and every one of them leaves an event behind, because nothing may change the game
/// silently. The prizes are not tools: they are earned, they are given once, and what makes "once"
/// true is the event, not a flag on this screen.
/// </remarks>
public sealed partial class MiscellaneousViewModel : SectionViewModel
{
    /// <summary>How many Rare Candies one press hands over.</summary>
    private const int CandiesPerPress = 10;

    /// <summary>How many Heart Scales one press hands over.</summary>
    private const int ScalesPerPress = 10;

    private readonly BagService _bag;
    private readonly IItemDelivery _delivery;
    private readonly IItemLookup _items;
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IClock _clock;
    private readonly RewardService _rewards;
    private readonly PokemonSpriteService _sprites;
    private readonly Notifier _notifier;
    private readonly EdgeTab _tab;
    private readonly ILogger<MiscellaneousViewModel> _logger;

    public MiscellaneousViewModel(BagService bag, IItemDelivery delivery, IItemLookup items,
        IRunContext runContext, IEventStore events, IClock clock, RewardService rewards,
        PokemonSpriteService sprites, Notifier notifier, EdgeTab tab,
        ILogger<MiscellaneousViewModel> logger)
        : base("MISCELÁNEA", "Herramientas sueltas y diagnóstico del enlace con el juego")
    {
        _bag = bag;
        _delivery = delivery;
        _items = items;
        _runContext = runContext;
        _events = events;
        _clock = clock;
        _rewards = rewards;
        _sprites = sprites;
        _notifier = notifier;
        _tab = tab;
        _logger = logger;
        _notificationsOn = notifier.Enabled;
        _stepAside = tab.StepAside;
    }

    /// <summary>The competition's one-off prizes, with how far off each one is.</summary>
    public ObservableCollection<RewardRowViewModel> Rewards { get; } = [];

    [ObservableProperty]
    private bool _hasRewards;

    public override string IconKey => "IconTools";

    public override GameNeed Needs => GameNeed.Running;

    public override Task ActivateAsync() => RefreshRewardsAsync();

    /// <summary>
    /// Rebuilds the prize list from the achievements and the history.
    /// </summary>
    /// <remarks>
    /// Needs no emulator: whether a prize is earned comes from the achievements, which read the
    /// saved game, and whether it is taken comes from the run's own events. Only handing it over
    /// needs the game open.
    /// </remarks>
    private async Task RefreshRewardsAsync()
    {
        Rewards.Clear();

        if (_runContext.Current is not { } run)
        {
            HasRewards = false;
            return;
        }

        try
        {
            foreach (var status in await _rewards.GetStatusAsync(run))
            {
                Rewards.Add(new RewardRowViewModel(
                    status.Reward.Id,
                    status.Reward.Name,
                    status.Reward.Description,
                    string.Join(" · ", status.Reward.Items.Select(item => $"{item.Amount} {item.Name}")),
                    status.Progress,
                    status.CanClaim,
                    status.Claimed,
                    status.Reward.Automatic,
                    status.Required <= 0 ? 1 : Math.Clamp((double)status.Unlocked / status.Required, 0, 1),
                    [.. status.Reward.Items.Select(item =>
                        new RewardGiftViewModel(item.Amount, item.Name, _sprites.GetItem(item.Id)))]));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al leer los premios de la competición");
        }

        HasRewards = Rewards.Count > 0;
    }

    /// <summary>
    /// Hands over a prize, once.
    /// </summary>
    /// <remarks>
    /// Everything that decides whether it can be taken lives in <see cref="RewardService"/> and is
    /// checked there again: this button being enabled is a convenience for the player, never the
    /// guard. A screen left open while the history changes underneath would otherwise be enough to
    /// take a one-off prize twice.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private async Task ClaimRewardAsync(RewardRowViewModel? row)
    {
        if (row is null || _runContext.Current is not { } run)
        {
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = $"Entregando «{row.Name}»...";

        try
        {
            var result = await _rewards.ClaimAsync(run, row.Id);
            Status = result.Message;

            if (result.Succeeded)
            {
                _logger.LogInformation("Premio {Reward} recogido", row.Id);
                await ReadBagAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la entrega del premio {Reward}", row.Id);
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
            await RefreshRewardsAsync();
        }
    }

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _bagAddress = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>What the player is carrying, so the fix can be checked against the screen.</summary>
    public ObservableCollection<string> BagContents { get; } = [];

    private bool CanUseTools => !IsBusy && _runContext.Current is not null;

    /// <summary>
    /// Reads the bag and shows it. Touches nothing, and it is the honest way to confirm that
    /// PermaLocke has found the real bag: what it lists has to match what the game shows.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private async Task ReadBagAsync()
    {
        IsBusy = true;
        NotifyCommands();
        Status = "Buscando la mochila en la memoria del juego...";

        try
        {
            var contents = await Task.Run(() => _bag.Read());
            var block = _bag.Block;

            if (block is null)
            {
                BagAddress = string.Empty;
                BagContents.Clear();
                Status = "No se ha encontrado la mochila. Azahar tiene que estar abierto con el juego cargado.";
                return;
            }

            BagAddress = $"Bloque en 0x{block.BaseAddress:X8}";
            BagContents.Clear();

            foreach (var slot in contents)
            {
                BagContents.Add($"{PocketName(slot.Pocket.Type)} · {_items.GetName(slot.Entry.ItemId)} "
                                + $"x{slot.Entry.Count}  (hueco {slot.Index}, 0x{slot.Address:X8})");
            }

            Status = contents.Count == 0
                ? "La mochila está localizada y vacía."
                : $"{contents.Count} objetos leídos. Compáralos con la mochila del juego: si no cuadran, no escribas nada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la lectura de la mochila");
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    /// <summary>Ten more Rare Candies on top of whatever the player already carries.</summary>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private Task GrantCandiesAsync() =>
        GiveAsync(BagService.RareCandyItemId, CandiesPerPress, "Caramelo Raro", "Probar el cap de nivel");

    /// <summary>
    /// The Shiny Charm, which is a key item: the bag holds exactly one no matter how often this
    /// is pressed, so pressing it again says so instead of pretending to have given another.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private Task GrantShinyCharmAsync() =>
        GiveAsync(BagService.ShinyCharmItemId, 1, "Amuleto Iris", "Herramienta de pruebas");

    /// <summary>
    /// Ten more Heart Scales. Unlike the Shiny Charm this one stacks, so pressing it twice really
    /// does hand over twenty: the move relearner charges one per move, and a Nuzlocke wants a
    /// pile of them right before the league.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private Task GrantHeartScalesAsync() =>
        GiveAsync(BagService.HeartScaleItemId, ScalesPerPress, "Escama Corazón", "Recordar movimientos");

    /// <summary>
    /// Adds an item to the bag on top of what is already there, and records it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Goes through <see cref="IItemDelivery"/>, the same path the shop uses: it pings the
    /// emulator first, adds to the current count rather than replacing it, and <b>re-reads the bag
    /// before reporting success</b>, so a button never claims to have changed a game that dropped
    /// the write.
    /// </para>
    /// <para>
    /// The name is checked against the cartridge table before anything is written. An id typed from
    /// memory that lands on the wrong item would hand over the wrong thing and never fail, which is
    /// the worst kind of bug this file could have.
    /// </para>
    /// </remarks>
    private async Task GiveAsync(int itemId, int amount, string expectedName, string reason)
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        if (_items.GetName(itemId) is var actualName && !string.Equals(actualName, expectedName,
                StringComparison.OrdinalIgnoreCase))
        {
            Status = $"No se entrega nada: PermaLocke esperaba que el objeto {itemId} fuese "
                     + $"«{expectedName}» y la tabla del juego dice «{actualName}».";
            _logger.LogError("Objeto {Item} esperado {Expected} pero es {Actual}",
                itemId, expectedName, actualName);
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = "Buscando la mochila en la memoria del juego...";

        try
        {
            // Lo que ya lleva y lo que le cabe, antes de tocar nada: es lo que distingue "ya lo
            // tienes" de "la escritura no ha cuajado", que si no se parecen demasiado.
            //
            // CarriedAsync hace ping primero y devuelve -1 si el emulador no está; solo entonces
            // se pregunta la capacidad, porque preguntarla barre los 96 MB de memoria del juego y
            // con Azahar cerrado ese barrido no puede acabar más que en fallo. Es el mismo cuelgue
            // que tuvo la tienda cuando preguntaba dieciocho veces sin comprobar nada antes.
            var carried = await _delivery.CarriedAsync(itemId);

            if (carried >= 0)
            {
                var capacity = await Task.Run(() => _bag.CapacityFor(itemId));

                if (capacity > 0 && carried >= capacity)
                {
                    Status = capacity == 1
                        ? $"Ya llevas el {expectedName}. Es un objeto clave: la mochila solo admite uno."
                        : $"Ya llevas {carried} {expectedName}, que es el máximo que cabe.";
                    return;
                }
            }

            var result = await _delivery.GiveAsync(itemId, amount);

            if (!result.Delivered)
            {
                Status = result.Problem;
                return;
            }

            Status = carried > 0
                ? $"{expectedName}: de {carried} a {result.Carried}. "
                  + "Escrito y releído: la mochila del juego ya lo tiene."
                : $"{expectedName} entregado: ahora llevas {result.Carried}. "
                  + "Escrito y releído: la mochila del juego ya lo tiene.";

            BagAddress = _bag.Block is { } block ? $"Bloque en 0x{block.BaseAddress:X8}" : BagAddress;

            await _events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = _clock.Now,
                Type = GameEventType.TestItemGranted,
                Source = EventSource.Player,
                Actor = run.PlayerName,
                Description = $"Herramienta de pruebas: {expectedName} escrito en la mochila "
                              + $"({carried} → {result.Carried}).",
                Reason = reason,
                Data = new Dictionary<string, string>
                {
                    ["itemId"] = itemId.ToString(),
                    ["objeto"] = expectedName,
                    ["entregados"] = amount.ToString(),
                    ["previous"] = carried.ToString(),
                    ["count"] = result.Carried.ToString()
                }
            });

            _logger.LogInformation("Herramienta de pruebas: {Item} de {Before} a {After}",
                expectedName, carried, result.Carried);

            await ReadBagAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la entrega de {Item}", expectedName);
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private static string PocketName(PKHeX.Core.InventoryType pocket) => pocket switch
    {
        PKHeX.Core.InventoryType.Items => "Objetos",
        PKHeX.Core.InventoryType.KeyItems => "Objetos clave",
        PKHeX.Core.InventoryType.TMHMs => "MT",
        PKHeX.Core.InventoryType.Medicine => "Medicinas",
        PKHeX.Core.InventoryType.Berries => "Bayas",
        PKHeX.Core.InventoryType.ZCrystals => "Cristales Z",
        PKHeX.Core.InventoryType.BattleItems => "Combate",
        _ => pocket.ToString()
    };

    private void NotifyCommands()
    {
        ReadBagCommand.NotifyCanExecuteChanged();
        GrantCandiesCommand.NotifyCanExecuteChanged();
        GrantShinyCharmCommand.NotifyCanExecuteChanged();
        ClaimRewardCommand.NotifyCanExecuteChanged();
    }
    /// <summary>
    /// Whether the notices appear on top of the game.
    /// </summary>
    /// <remarks>
    /// There is a switch because a notice you cannot turn off is not a notice, it is an
    /// interruption. It is not remembered between runs of the application on purpose: it is a
    /// «ahora no» and not a setting, and the price of forgetting it is that PermaLocke starts up
    /// saying things, which is what it is for.
    /// </remarks>
    [ObservableProperty]
    private bool _notificationsOn;

    partial void OnNotificationsOnChanged(bool value) => _notifier.Enabled = value;


    /// <summary>
    /// Whether PermaLocke minimises itself the moment the emulator appears.
    /// </summary>
    /// <remarks>
    /// It saves the one click Windows charges for: with the application behind the emulator, the
    /// first click on its taskbar button brings it to the front and only the second minimises it.
    /// Getting out of the way on its own means never needing that click.
    /// </remarks>
    [ObservableProperty]
    private bool _stepAside;

    partial void OnStepAsideChanged(bool value) => _tab.StepAside = value;

    /// <summary>Shows one, so the player can see where they land before something real happens.</summary>
    [RelayCommand]
    private void TestNotification() => _notifier.Say(
        "Así se ven los avisos",
        "Salen encima del juego con PermaLocke minimizado, no te quitan el foco y no se pueden pulsar.",
        ToastTone.Good);

}
