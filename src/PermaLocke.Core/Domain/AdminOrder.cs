namespace PermaLocke.Core.Domain;

/// <summary>
/// Something the organiser orders a player's application to do to their run, from Admin (2026-09-26).
/// </summary>
/// <remarks>
/// <para>
/// It travels inside an <see cref="AdminGift"/> of schema <see cref="AdminGift.OrderSchema"/>, through the same server
/// table, so an older application never sees it (it skips newer schemas) instead of half understanding it. The player's
/// application applies it on its own, like a points adjustment, through the same domain services the player's own
/// buttons use, and every change leaves its event with the organiser as its source (rule 4). Once applied, or refused
/// for good, an <see cref="GameEventType.AdminGiftClaimed"/> with its id closes it, so it never runs twice.
/// </para>
/// <para>
/// Some wait for the game: items need it open, a Pokémon needs it closed. Those stay pending and are tried again.
/// </para>
/// </remarks>
/// <param name="Kind">One of <see cref="AdminOrderKinds"/>.</param>
/// <param name="Args">What it acts on, as text: ids, numbers, names.</param>
/// <param name="Summary">One line in the organiser's words, for both screens.</param>
public sealed record AdminOrder(string Kind, IReadOnlyDictionary<string, string> Args, string Summary)
{
    public string Arg(string key) => Args.TryGetValue(key, out var value) ? value : string.Empty;
}

/// <summary>What an <see cref="AdminOrder"/> can ask for.</summary>
public static class AdminOrderKinds
{
    /// <summary>A recorded death was wrong: <c>pokemon</c> (entry id) is alive again and its penalty paid back.</summary>
    public const string Revive = "revivir";

    /// <summary>A death PermaLocke missed: <c>pokemon</c> (entry id) is marked fallen, with its penalty.</summary>
    public const string Kill = "matar";

    /// <summary>A charged wipe was wrong: <c>wipe</c> (event id) is paid back.</summary>
    public const string RevokeWipe = "revocarWipe";

    /// <summary>A spent route is free again: <c>zona</c> (location id) and <c>nombre</c>.</summary>
    public const string FreeZone = "liberarRuta";

    /// <summary>The stages marked by hand become <c>etapas</c>: moves the level cap with them.</summary>
    public const string SetStages = "etapas";

    /// <summary><c>cantidad</c> of item <c>objeto</c> (id) called <c>nombre</c>, into the bag. Needs the game open.</summary>
    public const string GiveItem = "objeto";

    /// <summary>A Pokémon: <c>especie</c>, <c>nivel</c>, <c>variocolor</c>, <c>forma</c>. Needs the game closed.</summary>
    public const string GivePokemon = "pokemon";

    /// <summary>A private message, <c>texto</c>, shown over the game and in the inbox.</summary>
    public const string Message = "mensaje";

    /// <summary>JUGAR closed (<c>cerrado</c> = true) or open again for this player, with <c>motivo</c>.</summary>
    public const string PlayLock = "bloqueo";
}
