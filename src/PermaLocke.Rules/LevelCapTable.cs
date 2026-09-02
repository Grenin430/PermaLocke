using System.Text.Json;

namespace PermaLocke.Rules;

/// <param name="Order">1-based position in the tour.</param>
/// <param name="Achievement">
/// The achievement that clears this stage, or null when nothing detects it.
/// <para>
/// This is what turns the cap from something the player remembers to press into something the run
/// works out on its own: the twelve trials count themselves off the Z-Crystal that lands in the
/// bag, so the cap moves the moment the trial is really cleared.
/// </para>
/// </param>
public sealed record LevelCapStage(string Id, int Order, string Name, int Level, string? Achievement = null);

/// <summary>
/// The level ceiling for each stage of the island tour, read from Data/levelcaps.json.
/// </summary>
/// <remarks>
/// The cap in force is the one of the stage the player is <em>about to face</em>: with no
/// trials cleared it is the first entry, after clearing one it is the second, and so on. That
/// is how the user stated it ("1ª prueba: nivel 14").
/// </remarks>
public sealed class LevelCapTable(IReadOnlyList<LevelCapStage> stages, bool correctInMemory = false)
{
    public IReadOnlyList<LevelCapStage> Stages { get; } = stages;

    /// <summary>
    /// Whether PermaLocke should force an over-levelled Pokémon back down in the running game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off, and that is the competition's own answer rather than a retreat. The reference the
    /// competition is played with does <b>no emulator memory access at all</b> — measured on its
    /// own binaries: no WriteMemory, no RPC, nothing on port 45987, and exactly one level-cap
    /// symbol, a getter for the list. There the cap is a rule the player keeps and the app shows.
    /// </para>
    /// <para>
    /// PermaLocke tried to enforce it by rewriting the running game, and that is a fight it cannot
    /// reliably win: the party lives in twenty-five places in memory at once, the game restores it
    /// from whichever it likes, and a write that lands and re-reads correctly still loses. Worse,
    /// a correction that goes slightly wrong <b>changes somebody's Pokémon</b> — §53 evolved a
    /// Ledyba that way. Warning always works and can never damage a save.
    /// </para>
    /// <para>
    /// The switch stays because the machinery is written, measured and tested. It is off by default
    /// because the safe behaviour has to be the one you get without thinking about it.
    /// </para>
    /// </remarks>
    public bool CorrectInMemory { get; } = correctInMemory;

    /// <param name="clearedStages">How many milestones the player has already completed.</param>
    public LevelCapStage? Current(int clearedStages) =>
        Stages.Count == 0 ? null : Stages[Math.Clamp(clearedStages, 0, Stages.Count - 1)];

    public int? CapFor(int clearedStages) => Current(clearedStages)?.Level;

    public static LevelCapTable Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LevelCapTable([]);
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<CapFile>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new LevelCapTable(
        [
            .. (file?.Caps ?? [])
                .OrderBy(c => c.Order)
                .Select(c => new LevelCapStage(c.Id, c.Order, c.Name, c.Level, c.Logro))
        ], file?.CorregirEnMemoria ?? false);
    }

    private sealed record CapEntry(string Id, int Order, string Name, int Level, string? Logro);

    private sealed record CapFile(List<CapEntry>? Caps, bool? CorregirEnMemoria);
}
