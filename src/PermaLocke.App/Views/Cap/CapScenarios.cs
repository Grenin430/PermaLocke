using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>One moment of a scenario: what the panel says from <see cref="At"/> seconds on.</summary>
public sealed record CapStep(double At, CapPlaqueData Data);

/// <summary>A situation of the panel to look at without the game: a list of moments the lab, the tests and <c>--ensayar-cap</c> all play the same way.</summary>
public sealed record CapScenario(string Name, string Title, IReadOnlyList<CapStep> Steps)
{
    /// <summary>The moment that says most about it, in seconds, when it is not the one just after its first change: where the lab takes its picture.</summary>
    public double? Moment { get; init; }

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

    private static readonly string[] Abilities = ["Intimidación", "Ensañamiento", "Sebo", "Cuerpo Maldito", "Nado Rápido", "Compensación"];

    private static readonly string[] Names = ["Gyarados", "Toxapex", "Snorlax", "Gengar", "Kingdra", "Dragonite"];

    private static readonly int[][] Types = [[10, 2], [3, 10], [0], [7, 3], [10, 15], [15, 2]];

    private static readonly string?[] Items = ["Restos", "Baya Zidra", null, "Cinta Focus", "Escama Marina", "Gafas Elegidas"];

    private static readonly int[][] Moves = [[10, 2, 15, 9], [3, 10, 17, 0], [0, 0, 1, 4], [7, 3, 13, 16], [10, 15, 0, 14], [15, 2, 0, 9]];

    private static readonly string[][] MoveNames =
    [
        ["Cascada", "Ataque Ala", "Dragodanza", "Colmillo Igneo"], ["Toxico", "Surf", "Rayo Hielo", "Placaje"], ["Golpe Cuerpo", "Descanso", "Bola Sombra", "Terremoto"],
        ["Bola Sombra", "Bomba Lodo", "Psiquico", "Pulso Umbrio"], ["Hidrobomba", "Dragoaliento", "Hiperrayo", "Rayo Hielo"], ["Dragoaliento", "Ataque Ala", "Hiperrayo", "Lanzallamas"]
    ];

    private static CapPlaqueMember Member(int i, Func<int, BitmapSource?> icon, int level, int hp, int max, CapStatus status = CapStatus.None,
        double exp = 0.4, bool egg = false)
    {
        var moves = Moves[i].Select((type, k) => new CapMove(type, i == 3 && k == 1 && hp < max / 2, MoveNames[i][k])).ToList();
        return new CapPlaqueMember(icon(Species[i]), Names[i], level, hp, max, egg, (uint)(1000 + i), Types[i], Items[i], status, exp, moves, null, Abilities[i]);
    }

    private static IReadOnlyList<CapGrave> Graves(Func<int, BitmapSource?> icon, int count)
    {
        var names = new[] { "Ratatta", "Pidgey", "Zubat", "Rowlet", "Magikarp", "Caterpie", "Weedle", "Geodude", "Machop", "Abra" };
        var killers = new[] { "Kahili", "Hala", "Olivia", "Salvaje", "Plumeria", "Totem", "Guzma", "Nanu", "Ilima", "Lana" };
        var species = new[] { 19, 16, 41, 722, 129, 10, 13, 74, 66, 63 };
        return [.. Enumerable.Range(0, count).Select(i => new CapGrave(icon(species[i % species.Length]), names[i % names.Length], killers[i % killers.Length], 8 + (i * 3), 1 + ((count - 1 - i) * 4)))];
    }

    private static CapPlaqueData Base(Func<int, BitmapSource?> icon, IReadOnlyList<CapPlaqueMember> party, int graves = 3, bool battle = false,
        int cap = 42, int? highest = null, int cleared = 7, double hour = 15.5, int seed = 7421) =>
        new("PRUEBA 8 DE 12", cap, 54, highest ?? party.Where(m => !m.Egg).Select(m => m.Level).DefaultIfEmpty(0).Max(), party, battle,
            Graves(icon, graves), cleared, 12, hour, seed);

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

        AddProposalScenarios(scenarios, icon);

        // The moods and the hours of the day, each as a scene of its own.
        scenarios.Add(Still("batalla", "Un combate sin peligro: el mar frío y la corriente", Base(icon, full, battle: true)));
        scenarios.Add(new CapScenario("luto", "Doce segundos después de una caída: el mar de luto", [
            new(0, Base(icon, Replace(full, 3, m => m with { Hp = 5 }), battle: true)),
            new(1, Base(icon, Replace(full, 3, m => m with { Hp = 0 }), 4))]) { Moment = 13 });
        scenarios.Add(Still("noche", "De noche: la luna, las estrellas y el brillo del mar", Base(icon, full, hour: 23.5)));
        scenarios.Add(Still("amanecer", "Al amanecer", Base(icon, full, hour: 6.7)));
        scenarios.Add(Still("atardecer", "Al atardecer", Base(icon, full, hour: 18.5)));

        // The rare events, at the moment each is half way across: the seed of the run and the time are found that make it happen then.
        void Rare(string name, string title, int kind, double period, double duration, int chance, double hour = 15.5)
        {
            for (var seed = 1; seed < 400; seed++)
            {
                for (var t = 1.0; t < period; t += 1)
                {
                    var e = CapFauna.EventAt(seed, kind, t, period, duration, chance);
                    if (!e.Active || !(e.Progress is >= 0.1 and <= 0.22 || e.Progress is >= 0.78 and <= 0.9)) continue;
                    scenarios.Add(new CapScenario(name, title, [new(0, Base(icon, full, hour: hour, seed: seed))]) { Moment = t });
                    return;
                }
            }
        }

        foreach (var (lane, name, title) in new[] { (0, "nadador", "Un Pokémon común pasa nadando"), (1, "nadador-raro", "Un Pokémon menos común pasa nadando"),
                     (2, "nadador-leyenda", "Un gran Pokémon cruza el mar"), (3, "volador", "Una bandada pasa volando"), (4, "volador-raro", "Un Pokémon menos común pasa volando"),
                     (5, "volador-leyenda", "Un legendario cruza el cielo") })
        {
            var l = CapVisitors.Lanes[lane];
            Rare(name, title, l.Kind, l.Period, l.Duration, l.Chance);
        }

        Rare("banco", "Un banco de peces", 3, 95, 26, 65);
        Rare("ola", "Una ola de luz recorre el mar", 4, 330, 9, 60);
        Rare("estrella", "Una estrella fugaz sobre el mar nocturno", 5, 50, 2.2, 80, 23.5);

        return scenarios;
    }

    // ---------------------------------------------------------------- PROPOSALS (branch cap-propuestas)

    private static IReadOnlyList<CapGrave> Fallen12(Func<int, BitmapSource?> icon)
    {
        var names = new[] { "Kompecono", "Marshtomp", "Panchito", "Prenador", "Crawdaunt", "Lapras", "Pidgeotto", "Golbat", "Mudsdale", "Lombre", "Zubat", "Pikipek" };
        var killers = new[] { "Kahili", "Hala", "Olivia", "Salvaje", "Plumeria", "Totem", "Guzma", "Nanu", "Ilima", "Lana", "Mallow", "Acerola" };
        var species = new[] { 784, 259, 19, 17, 342, 131, 17, 42, 750, 271, 41, 731 };
        var levels = new[] { 6, 14, 17, 21, 24, 27, 29, 31, 33, 35, 36, 38 };
        return [.. Enumerable.Range(0, 12).Select(i => new CapGrave(icon(species[i]), names[i], killers[i], levels[i], 1 + ((11 - i) * 3)))];
    }

    private static void AddProposalScenarios(List<CapScenario> scenarios, Func<int, BitmapSource?> icon)
    {
        CapPlaqueMember[] Team(Func<int, CapPlaqueMember, CapPlaqueMember>? change = null) =>
        [
            .. new[]
            {
                Member(0, icon, 42, 143, 143, exp: 0.62), Member(1, icon, 42, 122, 122, exp: 0.3), Member(2, icon, 42, 170, 170, exp: 0.9),
                Member(3, icon, 42, 124, 124, exp: 0.15), Member(4, icon, 42, 124, 124, exp: 0.5), Member(5, icon, 42, 124, 124, exp: 0.7)
            }.Select((m, i) => change is null ? m : change(i, m))
        ];

        CapPlaqueData Data(CapPlaqueMember[] party, bool battle = false) => Base(icon, party, 12, battle) with { Graves = Fallen12(icon) };
        CapPlaqueMember[] With(CapPlaqueMember[] party, int index, Func<CapPlaqueMember, CapPlaqueMember> change) => Replace(party, index, change);

        scenarios.Add(Still("captura", "Captura del jugador: seis de nivel 42, doce caídos, prueba 8 de 12", Data(Team())));

        var fight = Team();
        scenarios.Add(new CapScenario("ev-golpe", "Golpes de 37 %, 41 % y un rasguño del 3 % sobre Gyarados", [
            new(0, Data(fight, true)),
            new(2, Data(With(fight, 0, m => m with { Hp = 90 }), true)),
            new(6, Data(With(fight, 0, m => m with { Hp = 31 }), true)),
            new(10, Data(With(fight, 0, m => m with { Hp = 27 }), true))]));

        var hurt = Team((i, m) => i == 0 ? m with { Hp = 31 } : i == 1 ? m with { Hp = 12 } : m);
        scenarios.Add(new CapScenario("ev-cura", "Una cura pequeña (+29, 20 %) y una grande (+110, 90 %)", [
            new(0, Data(hurt)),
            new(2, Data(With(hurt, 0, m => m with { Hp = 60 }))),
            new(6, Data(With(With(hurt, 0, m => m with { Hp = 60 }), 1, m => m with { Hp = 122 })))]));

        var poisoned = Team((i, m) => i == 3 ? m with { Hp = 120, Status = CapStatus.Poison } : m);
        scenarios.Add(new CapScenario("ev-antidoto", "Un Antídoto quita el veneno a Gengar", [
            new(0, Data(poisoned)),
            new(2, Data(With(poisoned, 3, m => m with { Status = CapStatus.None })))]));

        var ailing = Team((i, m) => i switch
        {
            0 => m with { Status = CapStatus.Poison }, 1 => m with { Status = CapStatus.Burn }, 2 => m with { Status = CapStatus.Sleep },
            4 => m with { Status = CapStatus.Paralysis }, 5 => m with { Status = CapStatus.Freeze }, _ => m
        });
        // The statuses are cured one after another: poison, burn, sleep, paralysis, freeze.
        CapPlaqueData Cured(params int[] cured) => Data([.. ailing.Select((m, i) => cured.Contains(i) ? m with { Status = CapStatus.None } : m)]);
        scenarios.Add(new CapScenario("ev-curas", "Cada estado se cura a su manera, uno cada tres segundos", [
            new(0, Cured()), new(2, Cured(0)), new(5, Cured(0, 1)), new(8, Cured(0, 1, 2)), new(11, Cured(0, 1, 2, 4)), new(14, Cured(0, 1, 2, 4, 5))]));

        var down = Team((i, m) => i == 1 ? m with { Hp = 0 } : m);
        scenarios.Add(new CapScenario("ev-revivir", "Un Revivir levanta a Toxapex con la mitad de sus PS", [
            new(0, Data(down)),
            new(2, Data(With(down, 1, m => m with { Hp = 61 })))]));

        var held = Team();
        scenarios.Add(new CapScenario("ev-objeto", "Gyarados cambia los Restos por una Baya y Kingdra suelta su objeto", [
            new(0, Data(held)),
            new(2, Data(With(held, 0, m => m with { Item = "Baya Zidra" }))),
            new(6, Data(With(With(held, 0, m => m with { Item = "Baya Zidra" }), 4, m => m with { Item = null })))]));

        scenarios.Add(new CapScenario("ev-movimiento", "Snorlax aprende Hiperrayo y Dragonite, Danza Dragon", [
            new(0, Data(held)),
            new(2, Data(With(held, 2, m => m with { Moves = [.. m.Moves!.Take(3), new CapMove(0, false, "Hiperrayo")] }))),
            new(6, Data(With(With(held, 2, m => m with { Moves = [.. m.Moves!.Take(3), new CapMove(0, false, "Hiperrayo")] }), 5, m => m with { Moves = [new CapMove(15, false, "Danza Dragon"), .. m.Moves!.Skip(1)] })))]));

        var clean = Team();
        scenarios.Add(new CapScenario("ev-estado", "Entra el estado: Dragonite se duerme y Kingdra queda paralizado", [
            new(0, Data(clean)),
            new(2, Data(With(clean, 5, m => m with { Status = CapStatus.Sleep }))),
            new(6, Data(With(With(clean, 5, m => m with { Status = CapStatus.Sleep }), 4, m => m with { Status = CapStatus.Paralysis })))]));
    }

    private static CapPlaqueMember[] Replace(IReadOnlyList<CapPlaqueMember> party, int index, Func<CapPlaqueMember, CapPlaqueMember> change) =>
        [.. party.Select((m, i) => i == index ? change(m) : m)];

    private static CapScenario Still(string name, string title, CapPlaqueData data) => new(name, title, [new CapStep(0, data)]);
}
