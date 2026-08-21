using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// Delivers a Pokémon by writing it into a box of the player's save file.
/// </summary>
/// <remarks>
/// <para>
/// The save, not memory. Locating the PC boxes in RAM would mean a fresh investigation and,
/// worse, writing a Pokémon into a slot that holds nothing — something never done before, on
/// ten other people's machines. The save is a file with a fixed format that PKHeX has been
/// writing for years, and it behaves identically everywhere.
/// </para>
/// <para>
/// The price is that the game has to be closed. The emulator keeps the save in memory and
/// writes its own copy on the next in-game save, which would erase whatever we put there. So a
/// delivery is refused, loudly, while the game is loaded.
/// </para>
/// <para>
/// Every write backs the whole save up first, with a timestamp, and reads the file back
/// afterwards to confirm the Pokémon is really in the box. Nothing is reported as delivered
/// that has not been verified on disk.
/// </para>
/// </remarks>
public sealed class SaveBoxDelivery(
    PlayerSave save,
    string backupFolder,
    ILogger<SaveBoxDelivery> logger) : IPokemonDelivery
{
    /// <summary>The save file, or null when it cannot be found.</summary>
    public string? FindSave() => save.Find();

    /// <summary>
    /// True when the game is not loaded in the emulator. A running game makes any write
    /// pointless, so it is checked before touching anything.
    /// </summary>
    public bool CanDeliverNow(out string reason)
    {
        if (IsGameLoaded())
        {
            reason = "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: "
                     + "mientras esté cargado, el emulador reescribiría el save y el Pokémon se perdería.";
            return false;
        }

        if (FindSave() is null)
        {
            reason = "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez "
                     + "con este emulador?";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool IsGameLoaded() => save.IsGameLoaded();

    public Task<DeliveryResult> DeliverAsync(GachaPull pull, Run run, CancellationToken ct = default) =>
        Task.Run(() => Deliver(pull, run), ct);

    private DeliveryResult Deliver(GachaPull pull, Run run)
    {
        if (!CanDeliverNow(out var reason))
        {
            return new DeliveryResult(
                IsGameLoaded() ? DeliveryOutcome.GameRunning : DeliveryOutcome.SaveNotFound, reason);
        }

        return DeliverTo(FindSave()!, pull);
    }

    /// <summary>
    /// Writes into one specific save file. Kept separate from the checks so the whole write can
    /// be exercised against a copy of a save, with no emulator anywhere near it.
    /// </summary>
    public DeliveryResult DeliverTo(string path, GachaPull pull)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM save)
            {
                return new DeliveryResult(DeliveryOutcome.SaveUnreadable,
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            var (box, slot) = FindFreeSlot(save);

            if (box < 0)
            {
                return new DeliveryResult(DeliveryOutcome.BoxesFull,
                    "Todas las cajas del PC están llenas.");
            }

            Backup(path);

            var pokemon = Build(pull, save);
            save.SetBoxSlotAtIndex(pokemon, box, slot);
            File.WriteAllBytes(path, save.Write().ToArray());

            // Se relee del disco: no se da por entregado lo que no se ha vuelto a ver.
            if (!Verify(path, box, slot, pull))
            {
                return new DeliveryResult(DeliveryOutcome.Failed,
                    "Se escribió la partida pero al releerla el Pokémon no estaba. "
                    + "La copia de seguridad está en Saves/backup.");
            }

            logger.LogInformation("{Species} entregado en la caja {Box}, hueco {Slot} de {Path}",
                pull.SpeciesName, box + 1, slot + 1, path);

            return new DeliveryResult(DeliveryOutcome.Delivered,
                $"{pull.SpeciesName} está en la caja {box + 1}, hueco {slot + 1}.", box + 1, slot + 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la entrega de {Species} en la partida", pull.SpeciesName);

            return new DeliveryResult(DeliveryOutcome.Failed,
                "No se pudo escribir en la partida. El detalle está en la carpeta Logs.");
        }
    }

    private static (int Box, int Slot) FindFreeSlot(SAV7USUM save)
    {
        for (var box = 0; box < save.BoxCount; box++)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                if (save.GetBoxSlotAtIndex(box, slot) is not { Species: > 0 })
                {
                    return (box, slot);
                }
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// Builds the Pokémon the roll describes, as the player's own: same trainer, same ids, same
    /// game. Otherwise the game would treat it as traded and it would not obey the player.
    /// </summary>
    private static PK7 Build(GachaPull pull, SAV7USUM save)
    {
        var pokemon = new PK7
        {
            Species = (ushort)pull.Species,
            Form = 0,
            CurrentLevel = (byte)pull.Level,
            Nature = (Nature)pull.Nature,
            Ability = pull.AbilityId,

            // El hueco de habilidad tiene que ser uno de los tres que la especie declara; el 0
            // vale siempre y evita que el juego muestre un hueco imposible.
            AbilityNumber = 1,

            OriginalTrainerName = save.OT,
            TID16 = save.TID16,
            SID16 = save.SID16,
            OriginalTrainerGender = save.Gender,
            Language = save.Language,
            Version = save.Version,
            Ball = (byte)PKHeX.Core.Ball.Poke,
            MetLevel = (byte)pull.Level,
            MetDate = DateOnly.FromDateTime(DateTime.Now),
            CurrentHandler = 1,
            HandlingTrainerName = save.OT,
            HandlingTrainerGender = save.Gender
        };

        pokemon.IV_HP = pull.Ivs[0];
        pokemon.IV_ATK = pull.Ivs[1];
        pokemon.IV_DEF = pull.Ivs[2];
        pokemon.IV_SPE = pull.Ivs[3];
        pokemon.IV_SPA = pull.Ivs[4];
        pokemon.IV_SPD = pull.Ivs[5];

        if (pull.IsShiny)
        {
            pokemon.SetShiny();
        }

        SetMovesFor(pokemon);
        pokemon.HealPP();
        pokemon.ResetPartyStats();
        pokemon.RefreshChecksum();

        return pokemon;
    }


    /// <summary>
    /// Gives the Pokémon the moves it would know at that level.
    /// </summary>
    /// <remarks>
    /// PKHeX works them out from a legality analysis, which can baulk at a Pokémon the gacha
    /// made impossible — a random ability its species cannot have, for instance. If it does,
    /// the Pokémon arrives with no moves rather than not arriving at all: the player can teach
    /// it or use the move reminder, and that is a far smaller problem than a failed delivery.
    /// </remarks>
    private static void SetMovesFor(PK7 pokemon)
    {
        try
        {
            Span<ushort> moves = stackalloc ushort[4];
            new LegalityAnalysis(pokemon).GetSuggestedCurrentMoves(moves);
            pokemon.SetMoves(moves);
        }
        catch (Exception)
        {
            // Sin movimientos, pero entregado.
        }
    }
    private bool Verify(string path, int box, int slot, GachaPull pull)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM save)
        {
            return false;
        }

        var written = save.GetBoxSlotAtIndex(box, slot);
        return written is { } found && found.Species == pull.Species && found.CurrentLevel == pull.Level;
    }

    /// <summary>
    /// Copies the whole save before touching it. If the backup cannot be written, the delivery
    /// does not happen: never modify a partida that cannot be put back.
    /// </summary>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss}.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);

        logger.LogInformation("Copia de la partida guardada en {Name}", name);
    }
}
