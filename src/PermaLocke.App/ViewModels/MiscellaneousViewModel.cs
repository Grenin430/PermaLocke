using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Testing tools that write straight into the running game. They exist to exercise rules that
/// are otherwise slow to reach — the level cap needs Rare Candies — and every one of them
/// leaves an event behind, because nothing may change the game silently.
/// </summary>
public sealed partial class MiscellaneousViewModel : SectionViewModel
{
    /// <summary>How many Rare Candies one press hands over.</summary>
    private const int CandiesPerPress = 10;

    private readonly BagService _bag;
    private readonly IItemDelivery _delivery;
    private readonly IItemLookup _items;
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IClock _clock;
    private readonly ILogger<MiscellaneousViewModel> _logger;

    public MiscellaneousViewModel(BagService bag, IItemDelivery delivery, IItemLookup items,
        IRunContext runContext, IEventStore events, IClock clock,
        ILogger<MiscellaneousViewModel> logger)
        : base("MISCELÁNEA", "Herramientas sueltas y diagnóstico del enlace con el juego")
    {
        _bag = bag;
        _delivery = delivery;
        _items = items;
        _runContext = runContext;
        _events = events;
        _clock = clock;
        _logger = logger;
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
    }
}
