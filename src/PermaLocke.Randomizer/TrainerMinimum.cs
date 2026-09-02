namespace PermaLocke.Randomizer;

/// <param name="Class">The trainer class id this applies to.</param>
/// <param name="Name">
/// What that class is called in the cartridge, checked before anything is written.
/// </param>
/// <param name="MinimumBaseStatTotal">The floor its Pokémon are drawn above.</param>
/// <param name="Note">Which battle this is, carried into the report.</param>
/// <remarks>
/// The name travels with the id and is verified, the same rule the shops follow (§52): an id typed
/// from memory that lands on another class would quietly make the wrong battle harder and never
/// fail. A mismatch stops the randomization instead.
/// </remarks>
public sealed record TrainerMinimum(int Class, string Name, int MinimumBaseStatTotal, string Note = "");
