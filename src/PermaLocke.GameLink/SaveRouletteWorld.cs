using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <summary>
/// Everything the LUDÓPATA wheel does to the player's game, against the save file.
/// </summary>
/// <remarks>
/// <para>
/// The save and not the emulator's memory, and that is the whole design decision. The sixteen
/// faces between them touch the <b>party</b>, the <b>bag</b> and — through the free gacha rolls —
/// the <b>boxes</b>. Memory would mean three different mechanisms with three different failure
/// modes and, worse, a wheel that needed the game open for some faces and closed for others.
/// Through the save it is one door: one open, one backup, one write, one re-read.
/// </para>
/// <para>
/// A spin is applied <b>whole</b>. Every Pokémon it touches is checked by PID first, so a slot
/// that no longer holds who the wheel picked is skipped rather than overwritten, and the file is
/// only written once at the end.
/// </para>
/// <para>
/// Death is the same mark the rest of PermaLocke uses: the Pokémon is left at zero HP, still
/// itself. The lines it reports name exactly who died anyway — §59.s lesson, learned from a test
/// write nobody could identify afterwards — and now they are the only record that it was the wheel
/// and not a battle, because the Pokémon no longer looks any different from one the game knocked
/// out.
/// </para>
/// </remarks>
public sealed class SaveRouletteWorld(
    PlayerSave save,
    string backupFolder,
    ISpeciesLookup species,
    IItemLookup items,
    IAbilityLookup abilities,
    ILogger<SaveRouletteWorld> logger) : IRouletteWorldPort
{
    public bool CanActNow(out string reason)
    {
        if (save.IsGameLoaded())
        {
            reason = "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: "
                     + "la ruleta escribe en el save, y mientras esté cargado el emulador lo "
                     + "reescribiría por encima.";
            return false;
        }

        if (save.Find() is null)
        {
            reason = "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez "
                     + "con este emulador?";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Task<RouletteWorld> ReadAsync(CancellationToken ct = default) =>
        Task.Run(() => save.Find() is { } path ? ReadFrom(path) : RouletteWorld.Empty, ct);

    /// <summary>Reads one specific file, so the whole thing can be tried on a copy.</summary>
    public RouletteWorld ReadFrom(string path)
    {
        try
        {
            return !SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game
                ? RouletteWorld.Empty
                : Look(game);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer la partida para la ruleta");
            return RouletteWorld.Empty;
        }
    }

    /// <summary>What the wheel can see, from an already-open save.</summary>
    public RouletteWorld Look(SAV7USUM game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var party = new List<RoulettePokemon>();

        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is not PK7 { Species: > 0 } pokemon)
            {
                continue;
            }

            party.Add(new RoulettePokemon(slot, pokemon.PID, pokemon.Species,
                string.IsNullOrWhiteSpace(pokemon.Nickname)
                    ? species.GetName(pokemon.Species)
                    : pokemon.Nickname,
                pokemon.CurrentLevel));
        }

        var tms = Pouch(game, InventoryType.TMHMs);
        var medicine = Pouch(game, InventoryType.Medicine);

        return new RouletteWorld(
            party,

            // La lista buena de MT la da el propio bolsillo: en Ultra Luna no son un rango
            // -328-419, 618-620 y 690-694- y teclear los tramos a mano es pedir un error.
            [.. tms.GetAllItems()],
            [.. tms.Items.Where(item => item.Count > 0).Select(item => item.Index)],
            medicine.Items.Where(item => item.Count > 0)
                .ToDictionary(item => (int)item.Index, item => item.Count));
    }

    public Task<RouletteApplyResult> ApplyAsync(RouletteAction action, CancellationToken ct = default) =>
        Task.Run(() => Apply(action), ct);

    private RouletteApplyResult Apply(RouletteAction action)
    {
        if (!CanActNow(out var reason))
        {
            return RouletteApplyResult.Nothing(reason);
        }

        return ApplyIn(save.Find()!, action);
    }

    /// <summary>Applies to one specific file: opened once, backed up, written once, read back.</summary>
    public RouletteApplyResult ApplyIn(string path, RouletteAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return RouletteApplyResult.Nothing(
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            var lines = ApplyTo(game, action);

            if (lines.Count == 0)
            {
                return RouletteApplyResult.Nothing("No había nada sobre lo que aplicarlo.");
            }

            // La comprobación de los objetos no puede reconstruirse solo desde la acción: una
            // entrada podía existir ya, llegar al máximo del bolsillo o no caber porque estaba
            // lleno. Se captura el resultado exacto que dejó Move y se exige ese mismo número al
            // releer el fichero; de ese modo «hecho» nunca significa solo que Write no lanzó.
            var itemCounts = action.Items
                .Select(change => change.ItemId)
                .Distinct()
                .ToDictionary(itemId => itemId, itemId => Count(game, itemId));

            Backup(path);
            File.WriteAllBytes(path, game.Write().ToArray());

            // Se relee: no se da por hecho nada que no se haya vuelto a ver en el fichero.
            if (!Verify(path, action, itemCounts))
            {
                return new RouletteApplyResult(false,
                    "Se escribió la partida pero al releerla no estaba lo que la ruleta hizo. "
                    + $"La copia de seguridad está en {backupFolder}.", []);
            }

            logger.LogInformation("Ruleta {Effect}: {Lines}", action.Effect, string.Join(" | ", lines));

            return new RouletteApplyResult(true, "Hecho.", lines);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la ruleta al escribir la partida");
            return RouletteApplyResult.Nothing("No se pudo escribir en la partida. El detalle está en Logs.");
        }
    }

    /// <summary>
    /// Does the work on an already-open save, and says what it did.
    /// </summary>
    /// <remarks>
    /// Separate from the file so the whole thing can be tested against a save built in memory,
    /// the same split the EV trainer and the name repair use.
    /// </remarks>
    public List<string> ApplyTo(SAV7USUM game, RouletteAction action)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(action);

        var lines = new List<string>();

        for (var i = 0; i < action.Pokemon.Count; i++)
        {
            var target = action.Pokemon[i];

            if (game.GetPartySlotAtIndex(target.Slot) is not PK7 { Species: > 0 } pokemon
                || pokemon.PID != target.Pid)
            {
                // El hueco ya no tiene a quien la ruleta eligió: se salta en vez de escribirle a
                // quien esté ahora.
                lines.Add($"{target.Name} ya no está en el hueco {target.Slot + 1}: no se le ha tocado.");
                continue;
            }

            lines.Add(Touch(pokemon, action, i));
            pokemon.RefreshChecksum();
            game.SetPartySlotAtIndex(pokemon, target.Slot, PokemonBuilder.InPlace);
        }

        foreach (var change in action.Items)
        {
            lines.Add(Move(game, change));
        }

        return lines;
    }

    private string Touch(PK7 pokemon, RouletteAction action, int index)
    {
        switch (action.Effect)
        {
            case RouletteEffect.HabilidadBuena:
            case RouletteEffect.HabilidadMala:
            {
                var ability = action.Abilities[index];
                pokemon.Ability = ability;

                // El hueco de habilidad tiene que ser uno de los tres que el juego admite; el
                // primero vale siempre y evita que la ficha enseñe un hueco imposible.
                pokemon.AbilityNumber = 1;

                return $"{Name(pokemon)}: habilidad {abilities.GetName(ability)}.";
            }

            case RouletteEffect.IvPerfectos:
                SetIvs(pokemon, 31);
                return $"{Name(pokemon)}: los seis IV a 31.";

            case RouletteEffect.IvCero:
                SetIvs(pokemon, 0);
                return $"{Name(pokemon)}: los seis IV a 0.";

            case RouletteEffect.Muerte:
            {
                // Se dice quién era ANTES de borrarlo. Una prueba destructiva que no deja escrito
                // qué destruyó deja un Pokémon que nadie puede identificar después (§59).
                var who = $"{Name(pokemon)} (Nv.{pokemon.CurrentLevel}, {species.GetName(pokemon.Species)})";

                // La MISMA marca que usa el resto de la run. Una segunda copia de "qué le pasa a
                // un muerto" acabaría discrepando de esta.
                DeathMark.Apply(pokemon);

                return $"Muere {who}.";
            }

            default:
                return $"{Name(pokemon)}: nada que hacer.";
        }
    }

    /// <summary>
    /// Adds or takes items, never below zero and never past what the pouch allows.
    /// </summary>
    /// <remarks>
    /// The pouch is picked by <b>which one can hold the item</b>, straight from the cartridge's own
    /// lists, so a healing item lands in medicines and a TM among the TMs without anything here
    /// having to know that. Writing back is <c>SetPouch</c> over the save's own buffer, which is
    /// what <c>Write</c> then serialises; measured on a copy of the real partida before being
    /// trusted — Poción 8 → 13 and an MT02 that was not there.
    /// </remarks>
    private string Move(SAV7USUM game, RouletteItemChange change)
    {
        var name = items.GetName(change.ItemId);
        var pouch = game.Inventory.Pouches
            .FirstOrDefault(p => p.GetAllItems().Contains((ushort)change.ItemId));

        if (pouch is null)
        {
            return $"{name}: no cabe en ningún bolsillo de esta partida.";
        }

        var slots = pouch.Items;
        var at = Array.FindIndex(slots, item => item.Index == change.ItemId);

        if (at < 0)
        {
            if (change.Delta <= 0)
            {
                return $"{name}: no llevabas ninguno.";
            }

            at = Array.FindIndex(slots, item => item.Count == 0);

            if (at < 0)
            {
                return $"{name}: el bolsillo está lleno.";
            }

            slots[at].Index = (ushort)change.ItemId;
            slots[at].Count = 0;
        }

        var before = slots[at].Count;
        var after = Math.Clamp(before + change.Delta, 0, pouch.MaxCount);
        slots[at].Count = after;

        if (after == 0)
        {
            slots[at].Index = 0;
        }

        pouch.SetPouch(game.Data);

        return after == before
            ? $"{name}: sin cambios ({before})."
            : $"{name}: {before} → {after}.";
    }

    private static void SetIvs(PK7 pokemon, int value)
    {
        pokemon.IV_HP = pokemon.IV_ATK = pokemon.IV_DEF = value;
        pokemon.IV_SPA = pokemon.IV_SPD = pokemon.IV_SPE = value;
    }

    private string Name(PK7 pokemon) =>
        string.IsNullOrWhiteSpace(pokemon.Nickname) ? species.GetName(pokemon.Species) : pokemon.Nickname;

    /// <summary>
    /// Re-reads the file and checks the wheel's own work is in it.
    /// </summary>
    /// <remarks>
    /// Checks what the action <em>meant</em>, not byte equality: the Pokémon it touched carry what
    /// it wrote, and the items it moved hold the count it left. A slot it deliberately skipped is
    /// not checked, because nothing was promised about it.
    /// </remarks>
    private bool Verify(string path, RouletteAction action, IReadOnlyDictionary<int, int> itemCounts)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return false;
        }

        for (var i = 0; i < action.Pokemon.Count; i++)
        {
            var target = action.Pokemon[i];

            if (game.GetPartySlotAtIndex(target.Slot) is not PK7 pokemon)
            {
                continue;
            }

            // El PID tiene que seguir ahí. Aceptar que cambió convertiría una comprobación fallida
            // en éxito justo para el Pokémon cuya escritura había que vigilar.
            if (pokemon.PID != target.Pid)
            {
                return false;
            }

            var ok = action.Effect switch
            {
                RouletteEffect.HabilidadBuena or RouletteEffect.HabilidadMala =>
                    pokemon.Ability == action.Abilities[i],
                RouletteEffect.IvPerfectos => IvsAre(pokemon, 31),
                RouletteEffect.IvCero => IvsAre(pokemon, 0),
                RouletteEffect.Muerte => DeathMark.IsMarked(pokemon),
                _ => true
            };

            if (!ok)
            {
                return false;
            }
        }

        return itemCounts.All(pair => Count(game, pair.Key) == pair.Value);
    }

    private static bool IvsAre(PK7 pokemon, int value) =>
        pokemon.IV_HP == value && pokemon.IV_ATK == value && pokemon.IV_DEF == value
        && pokemon.IV_SPA == value && pokemon.IV_SPD == value && pokemon.IV_SPE == value;

    /// <summary>How many of one item the save really carries, zero when it has no entry.</summary>
    private static int Count(SAV7USUM game, int itemId)
    {
        var pouch = game.Inventory.Pouches
            .FirstOrDefault(p => p.GetAllItems().Contains((ushort)itemId));

        var entry = pouch?.Items.FirstOrDefault(item => item.Index == itemId);
        return entry?.Count ?? 0;
    }

    private static InventoryPouch7 Pouch(SAV7USUM game, InventoryType type) =>
        game.Inventory.Pouches.First(p => p.Type == type);

    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"{DateTime.Now:yyyyMMdd-HHmmss}-antes-de-la-ruleta.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: true);

        logger.LogInformation("Copia de la partida antes de la ruleta: {Name}", name);
    }
}
