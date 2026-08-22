using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Reads the effort values of everything in the player's save, and can prove the write works by
/// doing it <b>on a copy</b>.
/// </summary>
/// <remarks>
/// <para>
/// The unit tests build a save in memory, which covers the edit but not the round trip through a
/// file: PKHeX does not recognise a blank save written back to disk, so the only file that would
/// exercise <see cref="SaveEvTrainer.ApplyIn"/> is a real one. This is how that gets checked
/// without gambling with the player's partida — the copy is written, re-read and then deleted, and
/// the original is opened read-only.
/// </para>
/// <para>
/// Listing and testing are separate commands for the same reason the name repair separates them:
/// nothing that touches a save should happen as a side effect of looking.
/// </para>
/// </remarks>
public static class EvProbe
{
    private static readonly string[] StatNames = ["PS", "Atk", "Def", "SpA", "SpD", "Vel"];

    public static int Run(bool test)
    {
        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida: {path}");

        var reader = new SaveBoxReader(save, new PkhexLocationLookup(), "es",
            NullLogger<SaveBoxReader>.Instance);

        var snapshot = reader.ReadFrom(path);

        if (!snapshot.Available)
        {
            Console.WriteLine(snapshot.Problem);
            return 1;
        }

        Report(snapshot);

        return test ? Test(path, snapshot) : 0;
    }

    private static void Report(BoxSnapshot snapshot)
    {
        var everyone = snapshot.Boxes.SelectMany(box => box.Pokemon).ToList();
        var trained = everyone.Where(p => p.Evs.Sum() > 0).ToList();

        Console.WriteLine();
        Console.WriteLine($"{everyone.Count} Pokémon · {snapshot.Party?.Count ?? 0} en el equipo "
                          + $"· {snapshot.Stored} en el PC");
        Console.WriteLine($"Con EV repartidos: {trained.Count}");

        if (snapshot.Party is { Count: > 0 } party)
        {
            Console.WriteLine();
            Console.WriteLine("EQUIPO");

            foreach (var member in party.Pokemon)
            {
                Console.WriteLine($"  {member.Slot + 1}. {Line(member)}");
            }
        }

        if (trained.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("CON EV (los diez primeros)");

        foreach (var pokemon in trained.Take(10))
        {
            var where = pokemon.IsInParty ? "equipo" : $"caja {pokemon.Box + 1}";
            Console.WriteLine($"  {where}, hueco {pokemon.Slot + 1}: {Line(pokemon)}");
        }
    }

    private static string Line(BoxedPokemon pokemon)
    {
        var evs = EvSpread.Of(pokemon.Evs);
        var detail = string.Join(' ', Enumerable.Range(0, EvSpread.StatCount)
            .Where(index => evs[index] > 0)
            .Select(index => $"{StatNames[index]} {evs[index]}"));

        return $"{pokemon.DisplayName} Nv.{pokemon.Level} · EV {evs.Total}/510"
               + (detail.Length > 0 ? $" ({detail})" : " (sin repartir)");
    }

    /// <summary>
    /// Writes a spread onto a copy of the save and reads it back, so the file round trip is proved
    /// on a real partida without the real partida being at risk.
    /// </summary>
    private static int Test(string path, BoxSnapshot snapshot)
    {
        if (snapshot.Boxes.SelectMany(box => box.Pokemon).FirstOrDefault() is not { } victim)
        {
            Console.WriteLine();
            Console.WriteLine("No hay ningún Pokémon con el que probar.");
            return 1;
        }

        var copy = Path.Combine(Path.GetTempPath(), $"permalocke-ev-{Guid.NewGuid():N}.main");
        File.Copy(path, copy);

        try
        {
            var trainer = new SaveEvTrainer(save: null!, Path.Combine(Path.GetTempPath(), "permalocke-ev-backup"),
                NullLogger<SaveEvTrainer>.Instance);

            // Un reparto que no es el que ya tiene, para que un "funcionó" no pueda ser un empate.
            var before = EvSpread.Of(victim.Evs);
            var wanted = before.Total == 510
                ? EvSpread.Empty.With(0, 4)
                : EvSpread.Empty.With(0, 252).With(5, 252).With(1, 6);

            Console.WriteLine();
            Console.WriteLine($"Prueba sobre una COPIA: {victim.DisplayName} en "
                              + (victim.IsInParty ? $"el equipo, puesto {victim.Slot + 1}"
                                                  : $"la caja {victim.Box + 1}, hueco {victim.Slot + 1}"));
            Console.WriteLine($"  antes:  {before}");
            Console.WriteLine($"  pedido: {wanted}");

            var change = new EvChange(victim.Box, victim.Slot, victim.Pid, victim.DisplayName, wanted.Values);
            var result = trainer.ApplyIn(copy, change);

            Console.WriteLine($"  {result.Outcome}: {result.Message}");

            if (!result.Delivered)
            {
                return 1;
            }

            // Y se relee con el lector de verdad, no con el verificador interno: si los dos
            // coinciden es que lo escrito es lo que la aplicación va a enseñar.
            var again = new SaveBoxReader(save: null!, new PkhexLocationLookup(), "es",
                NullLogger<SaveBoxReader>.Instance).ReadFrom(copy);

            var found = again.Boxes.SelectMany(box => box.Pokemon)
                .FirstOrDefault(p => p.Pid == victim.Pid && p.Slot == victim.Slot);

            if (found is null)
            {
                Console.WriteLine("  RELECTURA: no se encuentra el Pokémon. MAL.");
                return 1;
            }

            var after = EvSpread.Of(found.Evs);
            Console.WriteLine($"  releído: {after}");

            var ok = after.Equals(wanted);
            Console.WriteLine(ok
                ? "  BIEN: lo escrito es lo que se lee. La partida original no se ha tocado."
                : "  MAL: lo releído no es lo pedido.");

            // Las estadísticas tienen que haberse movido con los EV; si no, se habría escrito un
            // número que el juego ignora.
            Console.WriteLine($"  estadísticas antes:   {string.Join('/', victim.Stats)}");
            Console.WriteLine($"  estadísticas después: {string.Join('/', found.Stats)}");

            return ok ? 0 : 1;
        }
        finally
        {
            File.Delete(copy);
        }
    }
}
