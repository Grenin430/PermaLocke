using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>One moment of a scenario: what the panel says from <see cref="At"/> seconds on.</summary>
public sealed record CapStep(double At, CapPlaqueData Data);

/// <summary>A situation of the panel to look at without the game: a list of moments the lab, the tests and <c>--ensayar-cap</c> all play the same way.</summary>
public sealed record CapScenario(string Name, string Title, IReadOnlyList<CapStep> Steps)
{
    /// <summary>How long it lasts: until a little after its last step.</summary>
    public double Length => Steps.Count == 0 ? 0 : Steps[^1].At + 4;

    /// <summary>What the panel says at a moment: the last step whose time has come.</summary>
    public CapPlaqueData At(double t)
    {
        var data = Steps[0].Data;
        foreach (var step in Steps)
        {
            if (step.At <= t) data = step.Data;
        }

        return data;
    }
}

/// <summary>
/// The scenarios (2026-10-10): the full party, the fallen, the eggs, a battle with low HP, the cap reached, a run that has just
/// begun, and the moments that animate (a level, a heal, a hit, a fall, a new cap, a hatching). Made from what a player's run
/// really has, with invented Pokémon only because there is no run: nothing here reaches the game.
/// </summary>
public static class CapScenarios
{
    /// <summary>The species of the sample party, in order: a stand-in for each cartridge icon is asked for by number.</summary>
    public static readonly int[] Species = [130, 748, 143, 94, 230, 149];

    private static readonly string[] Names = ["Gyarados", "Toxapex", "Snorlax", "Gengar", "Kingdra", "Dragonite"];

    private static readonly int[][] Types = [[10, 2], [3, 10], [0], [7, 3], [10, 15], [15, 2]];

    private static readonly string?[] Items = ["Restos", "Baya Zidra", null, "Cinta Focus", "Escama Marina", "Gafas Elegidas"];

    private static readonly int[][] Moves = [[10, 2, 15, 9], [3, 10, 17, 0], [0, 0, 1, 4], [7, 3, 13, 16], [10, 15, 0, 14], [15, 2, 0, 9]];

    private static CapPlaqueMember Member(int i, Func<int, BitmapSource?> icon, int level, int hp, int max, CapStatus status = CapStatus.None,
        double exp = 0.4, bool egg = false)
    {
        var moves = Moves[i].Select((type, k) => new CapMove(type, i == 3 && k == 1 && hp < max / 2)).ToList();
        return new CapPlaqueMember(icon(Species[i]), Names[i], level, hp, max, egg, (uint)(1000 + i), Types[i], Items[i], status, exp, moves);
    }

    private static IReadOnlyList<CapGrave> Graves(Func<int, BitmapSource?> icon, int count)
    {
        var names = new[] { "Ratatta", "Pidgey", "Zubat", "Rowlet", "Magikarp", "Caterpie", "Weedle", "Geodude", "Machop", "Abra" };
        var killers = new[] { "Kahili", "Hala", "Olivia", "Salvaje", "Plumeria", "Totem", "Guzma", "Nanu", "Ilima", "Lana" };
        var species = new[] { 19, 16, 41, 722, 129, 10, 13, 74, 66, 63 };
        return [.. Enumerable.Range(0, count).Select(i => new CapGrave(icon(species[i % species.Length]), names[i % names.Length], killers[i % killers.Length], 8 + (i * 3)))];
    }

    private static CapPlaqueData Base(Func<int, BitmapSource?> icon, IReadOnlyList<CapPlaqueMember> party, int graves = 3, bool battle = false,
        int cap = 42, int? highest = null, int cleared = 7) =>
        new("PRUEBA 8 DE 12", cap, 54, highest ?? party.Where(m => !m.Egg).Select(m => m.Level).DefaultIfEmpty(0).Max(), party, battle,
            Graves(icon, graves), cleared, 12);

    public static IReadOnlyList<CapScenario> All(Func<int, BitmapSource?> icon)
    {
        var full = new[]
        {
            Member(0, icon, 42, 143, 143, exp: 0.62), Member(1, icon, 42, 122, 122, exp: 0.3),
            Member(2, icon, 41, 170, 170, exp: 0.9), Member(3, icon, 40, 120, 124, CapStatus.Poison, 0.15),
            Member(4, icon, 42, 124, 124, exp: 0.5), Member(5, icon, 39, 124, 124, exp: 0.7)
        };

        var fallen = new[]
        {
            Member(0, icon, 42, 143, 143), Member(1, icon, 42, 0, 122), Member(2, icon, 41, 0, 170),
            Member(3, icon, 40, 0, 124), Member(4, icon, 42, 60, 124), Member(5, icon, 39, 124, 124, CapStatus.Burn)
        };

        var eggs = new[]
        {
            Member(0, icon, 42, 143, 143), Member(1, icon, 5, 20, 20, egg: true), Member(2, icon, 5, 20, 20, egg: true)
        };

        var combat = new[]
        {
            Member(0, icon, 42, 31, 143), Member(1, icon, 42, 12, 122, CapStatus.Paralysis), Member(2, icon, 41, 170, 170),
            Member(3, icon, 40, 0, 124), Member(4, icon, 42, 124, 124), Member(5, icon, 39, 20, 124)
        };

        var atCap = new[]
        {
            Member(0, icon, 42, 143, 143), Member(1, icon, 42, 122, 122), Member(2, icon, 42, 170, 170),
            Member(3, icon, 42, 124, 124), Member(4, icon, 42, 124, 124), Member(5, icon, 42, 124, 124, CapStatus.Sleep)
        };

        var begin = new[] { Member(0, icon, 7, 24, 24), Member(1, icon, 6, 20, 20) };

        var scenarios = new List<CapScenario>
        {
            Still("completo", "Equipo completo y tres caídos", Base(icon, full)),
            Still("caidos", "Tres del equipo caídos, siete en el cementerio", Base(icon, fallen, 7)),
            Still("huevos", "Dos huevos y un Pokémon", Base(icon, eggs, 0)),
            Still("combate", "Combate con PS bajos", Base(icon, combat, 3, battle: true)),
            Still("tope", "Todo el equipo en el cap", Base(icon, atCap, 5, highest: 42)),
            Still("inicio", "Inicio de la run: dos Pokémon, sin caídos", Base(icon, begin, 0, cap: 15, cleared: 0) with { Trial = "PRUEBA 1 DE 12", Next = 22 }),
            Still("liga", "La Liga, sin siguiente cap", Base(icon, full, 9, cap: 90, cleared: 12) with { Trial = "CAMPEONATO DE LA LIGA", Next = null }),
        };

        // The ones that move: each is a few moments in order, and the panel is told them the way the game would.
        var calm = Base(icon, full);
        var fight = Base(icon, full, battle: true);

        scenarios.Add(new CapScenario("subida", "Gyarados sube del 41 al 42", [
            new(0, Base(icon, Replace(full, 0, m => m with { Level = 41, Exp = 0.96, MaxHp = 139, Hp = 139 }), highest: 42)),
            new(2, Base(icon, Replace(full, 0, m => m with { Level = 42, Exp = 0.04 })))]));

        scenarios.Add(new CapScenario("golpe", "Un combate: Gyarados baja de 143 a 90 y a 31", [
            new(0, fight),
            new(2, Base(icon, Replace(full, 0, m => m with { Hp = 90 }), battle: true)),
            new(5, Base(icon, Replace(full, 0, m => m with { Hp = 31 }), battle: true))]));

        scenarios.Add(new CapScenario("caida", "Gyarados cae en combate y su tumba aparece", [
            new(0, Base(icon, Replace(full, 0, m => m with { Hp = 31 }), battle: true)),
            new(2, Base(icon, Replace(full, 0, m => m with { Hp = 0 }), battle: true)),
            new(7, Base(icon, Replace(full, 0, m => m with { Hp = 0 }), 4))]));

        scenarios.Add(new CapScenario("curacion", "Un Centro Pokémon cura al equipo", [
            new(0, Base(icon, combat, 3)),
            new(2, Base(icon, [.. full.Select(m => m with { Hp = m.MaxHp, Status = CapStatus.None })], 3))]));

        scenarios.Add(new CapScenario("cambio-cap", "Se supera la prueba: el cap pasa de 42 a 54", [
            new(0, Base(icon, atCap, 5, highest: 42)),
            new(2, Base(icon, atCap, 5, cap: 54, highest: 42, cleared: 8) with { Trial = "PRUEBA 9 DE 12", Next = 60 })]));

        scenarios.Add(new CapScenario("eclosion", "Un huevo eclosiona", [
            new(0, Base(icon, eggs, 0)),
            new(2, Base(icon, Replace(eggs, 1, m => m with { Egg = false, Level = 1, Hp = 11, MaxHp = 11 }), 0))]));

        var states = new[]
        {
            Member(0, icon, 42, 143, 143, CapStatus.Poison), Member(1, icon, 42, 122, 122, CapStatus.Burn),
            Member(2, icon, 41, 170, 170, CapStatus.Sleep), Member(3, icon, 40, 124, 124, CapStatus.Paralysis),
            Member(4, icon, 42, 124, 124, CapStatus.Freeze), Member(5, icon, 39, 124, 124)
        };
        scenarios.Add(Still("estados", "Los cinco estados alterados", Base(icon, states)));
        scenarios.Add(Still("casi-tope", "Dos Pokémon a punto de llegar al cap", Base(icon,
            [Member(0, icon, 41, 143, 143, exp: 0.93), Member(1, icon, 41, 122, 122, exp: 0.85), Member(2, icon, 38, 170, 170, exp: 0.2),
             Member(3, icon, 40, 124, 124, exp: 0.5), Member(4, icon, 42, 124, 124), Member(5, icon, 39, 124, 124, exp: 0.7)])));
        _ = calm;

        return scenarios;
    }

    private static CapPlaqueMember[] Replace(IReadOnlyList<CapPlaqueMember> party, int index, Func<CapPlaqueMember, CapPlaqueMember> change) =>
        [.. party.Select((m, i) => i == index ? change(m) : m)];

    private static CapScenario Still(string name, string title, CapPlaqueData data) => new(name, title, [new CapStep(0, data)]);
}
