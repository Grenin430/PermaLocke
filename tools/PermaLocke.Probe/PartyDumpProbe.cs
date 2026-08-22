using PKHeX.Core;

namespace PermaLocke.Probe;

/// <summary>
/// Reads a 260-byte party entry saved by <c>AzaharGameWriter</c> before a write, and prints the
/// two things that decide a Pokémon's level.
/// </summary>
/// <remarks>
/// <para>
/// A party Pokémon carries the level <b>twice</b>: as experience inside the encrypted block, and
/// as <c>Stat_Level</c> in the party stats that follow it. PKHeX reports <c>CurrentLevel</c> from
/// the experience, so a tool that writes the experience and reads it back agrees with itself while
/// the game, which shows <c>Stat_Level</c>, disagrees with both.
/// </para>
/// <para>
/// That is exactly the trap the level cap fell into, so this exists to show the two numbers side
/// by side rather than trusting either.
/// </para>
/// </remarks>
public static class PartyDumpProbe
{
    public static int Run(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"No existe: {path}");
            return 1;
        }

        var bytes = File.ReadAllBytes(path);
        var expected = new PK7().SIZE_PARTY;

        if (bytes.Length != expected)
        {
            Console.WriteLine($"El fichero mide {bytes.Length} bytes y una entrada de equipo mide {expected}.");
            return 1;
        }

        var pokemon = new PK7(bytes);
        var names = GameInfo.GetStrings("es").specieslist;

        Console.WriteLine(Path.GetFileName(path));
        Console.WriteLine($"  especie        {pokemon.Species} " +
                          (pokemon.Species < names.Length ? names[pokemon.Species] : "?"));
        Console.WriteLine($"  checksum       {(pokemon.ChecksumValid ? "válido" : "INVÁLIDO")}");
        Console.WriteLine($"  PID            {pokemon.PID:X8}");
        Console.WriteLine();
        Console.WriteLine($"  EXP            {pokemon.EXP}");
        Console.WriteLine($"  nivel por EXP  {pokemon.CurrentLevel}   <- lo que lee PKHeX y PermaLocke");
        Console.WriteLine($"  Stat_Level     {pokemon.Stat_Level}   <- lo que ensena el juego");
        Console.WriteLine();
        Console.WriteLine($"  PS             {pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax}");
        Console.WriteLine($"  estadisticas   {pokemon.Stat_ATK}/{pokemon.Stat_DEF}/"
                          + $"{pokemon.Stat_SPA}/{pokemon.Stat_SPD}/{pokemon.Stat_SPE}");

        if (pokemon.Stat_Level != pokemon.CurrentLevel)
        {
            Console.WriteLine();
            Console.WriteLine("  LOS DOS NIVELES NO COINCIDEN. El juego ensena el Stat_Level.");
        }

        return 0;
    }

    /// <summary>
    /// Applies the cap the way the writer does and reports which bytes would change, so the
    /// question "does this write touch the level the game shows?" gets a measured answer.
    /// </summary>
    public static int WhatWouldChange(string path, int cap)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"No existe: {path}");
            return 1;
        }

        var original = File.ReadAllBytes(path);
        var size = new PK7().SIZE_PARTY;

        var reference = new byte[size];
        new PK7((byte[])original.Clone()).WriteEncryptedDataParty(reference);

        var working = new PK7((byte[])original.Clone());
        Console.WriteLine($"antes:  EXP {working.EXP} (nivel {working.CurrentLevel}), Stat_Level {working.Stat_Level}");

        working.CurrentLevel = (byte)cap;
        working.RefreshChecksum();

        Console.WriteLine($"tras poner CurrentLevel = {cap}:");
        Console.WriteLine($"        EXP {working.EXP} (nivel {working.CurrentLevel}), Stat_Level {working.Stat_Level}");

        var modified = new byte[size];
        working.WriteEncryptedDataParty(modified);

        var changed = Enumerable.Range(0, size).Where(i => modified[i] != reference[i]).ToList();
        Console.WriteLine($"        {changed.Count} bytes cambiarian: "
                          + string.Join(", ", changed.Select(i => $"0x{i:X2}")));

        return 0;
    }
}
