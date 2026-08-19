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
    private readonly BagService _bag;
    private readonly IItemLookup _items;
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IClock _clock;
    private readonly ILogger<MiscellaneousViewModel> _logger;

    public MiscellaneousViewModel(BagService bag, IItemLookup items, IRunContext runContext,
        IEventStore events, IClock clock, ILogger<MiscellaneousViewModel> logger)
        : base("MISCELÁNEA")
    {
        _bag = bag;
        _items = items;
        _runContext = runContext;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    [ObservableProperty]
    private int _targetCandies = 999;

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

    /// <summary>
    /// Writes Rare Candies into the bag. Works whether or not the player already carries any:
    /// if the pocket has no entry for them, one is added in the first free slot.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseTools))]
    private async Task GrantCandiesAsync()
    {
        var run = _runContext.Current;

        if (run is null)
        {
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = "Buscando la mochila en la memoria del juego...";

        try
        {
            var target = Math.Clamp(TargetCandies, 1, BagService.MaxItemCount);
            var result = await Task.Run(() => _bag.SetCount(BagService.RareCandyItemId, target));

            Status = Describe(result, target);

            if (result.Outcome != BagWriteOutcome.Ok)
            {
                return;
            }

            BagAddress = _bag.Block is { } block ? $"Bloque en 0x{block.BaseAddress:X8}" : BagAddress;

            await _events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = _clock.Now,
                Type = GameEventType.TestItemGranted,
                Source = EventSource.Player,
                Actor = run.PlayerName,
                Description = $"Herramienta de pruebas: {result.Applied} Caramelos Raros escritos en la mochila.",
                Reason = "Probar el cap de nivel",
                Data = new Dictionary<string, string>
                {
                    ["itemId"] = BagService.RareCandyItemId.ToString(),
                    ["previous"] = result.Previous.ToString(),
                    ["count"] = result.Applied.ToString(),
                    ["address"] = $"0x{result.Address:X8}",
                }
            });

            _logger.LogInformation("Herramienta de pruebas: {Count} Caramelos Raros en 0x{Address:X8}",
                result.Applied, result.Address);

            await ReadBagAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la entrega de Caramelos Raros");
            Status = "Ha fallado. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    /// <summary>
    /// Says what actually happened. The write is read back before this reports success, so a
    /// button never claims to have changed the game when the emulator dropped the write.
    /// </summary>
    private static string Describe(BagWriteResult result, int target) => result.Outcome switch
    {
        BagWriteOutcome.Ok when result.Previous == 0 =>
            $"Añadidos {result.Applied} Caramelos Raros en 0x{result.Address:X8}. "
            + "Escrito y releído: la memoria del juego ya lo tiene.",
        BagWriteOutcome.Ok =>
            $"Caramelos Raros: de {result.Previous} a {result.Applied} en 0x{result.Address:X8}. "
            + "Escrito y releído: la memoria del juego ya lo tiene.",
        BagWriteOutcome.BagNotFound =>
            "No se ha encontrado la mochila en la memoria. Azahar tiene que estar abierto con el juego cargado.",
        BagWriteOutcome.NotApplied =>
            "La escritura no ha cuajado: se releyó la memoria y no había cambiado. "
            + "Eso pasa con el Azahar oficial, que acepta la escritura y no la aplica. Hace falta el fork.",
        BagWriteOutcome.PocketFull =>
            "El bolsillo de medicinas está lleno; no queda hueco donde meter los caramelos.",
        BagWriteOutcome.UnknownPocket =>
            "Ningún bolsillo de la mochila admite ese objeto.",
        _ => $"No había nada que hacer con {target} caramelos."
    };

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
    }
}
