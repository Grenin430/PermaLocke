using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// Whether a figure is something good, something bad, or neither.
/// </summary>
/// <remarks>
/// It lives with the data and not in the view because the view cannot tell: «Penalización» and
/// «Puntos ganados» are both just a label and a number, and until now both were drawn in the same
/// accent violet — four panels where the bars only said lengths. The rule the rest of the
/// application already follows is that a colour has to mean something, and the only place that
/// knows what a line means is whoever built it.
///
/// <see cref="Neutral"/> is not a cop-out: where a Pokémon came from is neither good nor bad, and
/// painting it green or red would invent a judgement that nobody made.
/// </remarks>
public enum StatTone
{
    Neutral,
    Good,
    Bad
}

/// <param name="Label">Already in Spanish: the enum never reaches the screen.</param>
/// <param name="Total">Net points this line is responsible for.</param>
/// <param name="Count">How many events made it up.</param>
/// <param name="Share">0 to 1 against the biggest line, for the bar the view draws.</param>
public sealed record LedgerLine(string Label, int Total, int Count, double Share,
    StatTone Tone = StatTone.Neutral);

/// <param name="Label">What the tally counts.</param>
/// <param name="Count">How many.</param>
/// <param name="Share">0 to 1 against the biggest tally in its group.</param>
public sealed record Tally(string Label, int Count, double Share,
    StatTone Tone = StatTone.Neutral);

/// <param name="Label">What the record is.</param>
/// <param name="Value">The answer, preformatted.</param>
/// <param name="Detail">Where it came from, or empty.</param>
public sealed record RecordLine(string Label, string Value, string Detail = "",
    StatTone Tone = StatTone.Neutral);

/// <param name="Earned">Everything the run has ever gained.</param>
/// <param name="Spent">Everything it has ever lost or spent, as a positive number.</param>
/// <param name="Curve">The running balance, one point per event that moved it.</param>
public sealed record StatisticsReport(
    bool HasRun,
    int Balance,
    int Earned,
    int Spent,
    IReadOnlyList<LedgerLine> Income,
    IReadOnlyList<LedgerLine> Outgoings,
    IReadOnlyList<double> Curve,
    double? CurveZero,
    IReadOnlyList<Tally> Origins,
    IReadOnlyList<Tally> Fates,
    IReadOnlyList<RecordLine> Records);

/// <summary>
/// Everything the run has recorded about itself, read back as figures.
/// </summary>
/// <remarks>
/// <para>
/// It computes nothing new and decides nothing: it is a <b>projection over the event chain</b>,
/// which already holds every point that moved and why. Until now the application kept that chain
/// carefully, showed the last twenty lines on HOME, and threw the rest away.
/// </para>
/// <para>
/// The centre of it is the ledger, because it answers the one question the run cannot otherwise
/// answer: <i>why do I have this many points</i>. Every event carries its own
/// <see cref="GameEvent.PointsDelta"/>, so income and outgoings are the same field split by sign —
/// nothing is reconstructed from today's prices, which is what keeps it honest when the
/// configuration changes under a run that is already going.
/// </para>
/// </remarks>
public sealed class StatisticsService(
    IRunContext runContext,
    IEventStore events,
    IPokemonRepository pokemon)
{
    /// <summary>How many samples the balance curve is reduced to before it is drawn.</summary>
    /// <remarks>
    /// A run ends up with thousands of events and a line of a thousand segments across 700 pixels
    /// is a smear. Sampling keeps the shape, which is the only thing the curve is for.
    /// </remarks>
    private const int CurvePoints = 120;

    public async Task<StatisticsReport> BuildAsync(CancellationToken ct = default)
    {
        if (runContext.Current is not { } run)
        {
            return new StatisticsReport(false, 0, 0, 0, [], [], [], null, [], [], []);
        }

        var history = (await events.GetAllAsync(run.Id, ct))
            .OrderBy(e => e.Timestamp)
            .ToList();

        var team = await pokemon.GetAllAsync(run.Id, ct);

        var moved = history.Where(e => e.PointsDelta != 0).ToList();

        var curve = Curve(moved);

        var earned = moved.Where(e => e.PointsDelta > 0).Sum(e => e.PointsDelta);
        var spent = -moved.Where(e => e.PointsDelta < 0).Sum(e => e.PointsDelta);

        return new StatisticsReport(
            HasRun: true,
            Balance: moved.Sum(e => e.PointsDelta),
            Earned: earned,
            Spent: spent,
            Income: Ledger(moved.Where(e => e.PointsDelta > 0), StatTone.Good),
            Outgoings: Ledger(moved.Where(e => e.PointsDelta < 0), StatTone.Bad),
            Curve: curve.Points,
            CurveZero: curve.Zero,
            Origins: Group(team.Select(p => (DisplayNames.Of(p.Origin), StatTone.Neutral))),
            Fates: Group(team.Select(p => (DisplayNames.Of(p.Status), Of(p.Status)))),
            Records: Records(history, team, run));
    }

    /// <summary>Groups the movements by what caused them, biggest first.</summary>
    private static IReadOnlyList<LedgerLine> Ledger(IEnumerable<GameEvent> moved, StatTone tone)
    {
        var groups = moved
            .GroupBy(e => e.Type)
            .Select(g => new
            {
                Label = DisplayNames.Of(g.Key),
                Total = Math.Abs(g.Sum(e => e.PointsDelta)),
                Count = g.Count()
            })
            .OrderByDescending(g => g.Total)
            .ToList();

        var biggest = groups.Count > 0 ? groups.Max(g => g.Total) : 0;

        return groups
            .Select(g => new LedgerLine(g.Label, g.Total, g.Count,
                biggest == 0 ? 0 : (double)g.Total / biggest, tone))
            .ToList();
    }

    /// <summary>
    /// The running balance, normalised to 0..1 and sampled down.
    /// </summary>
    /// <remarks>
    /// Normalised here and not in the view because the floor matters: penalties can take the
    /// balance <b>below zero</b> — that is deliberate, a penalty is not a purchase and does not ask
    /// whether you can afford it — so the curve is scaled between its own minimum and maximum. A
    /// curve anchored at zero would draw a run that went negative as if it had flatlined.
    /// </remarks>
    /// <returns>The samples, and where a balance of zero falls in them, if it falls inside.</returns>
    private static (IReadOnlyList<double> Points, double? Zero) Curve(IReadOnlyList<GameEvent> moved)
    {
        if (moved.Count < 2)
        {
            return ([], null);
        }

        var running = new List<double>(moved.Count);
        var balance = 0;

        foreach (var e in moved)
        {
            balance += e.PointsDelta;
            running.Add(balance);
        }

        var low = running.Min();
        var high = running.Max();
        var span = high - low;

        var step = Math.Max(1, running.Count / CurvePoints);

        var points = running
            .Where((_, i) => i % step == 0 || i == running.Count - 1)
            .Select(v => span == 0 ? 0.5 : (v - low) / span)
            .ToList();

        // Donde cae el cero. La curva se escala entre su propio minimo y su maximo -y tiene que
        // ser asi, porque el saldo puede quedarse en negativo-, y el precio de eso es que mirando
        // el dibujo no se sabe si estas por encima o por debajo de cero. Esta es esa referencia, y
        // vale null cuando la run entera se mantuvo de un solo lado.
        var zero = span > 0 && low <= 0 && high >= 0 ? -low / span : (double?)null;

        return (points, zero);
    }

    /// <param name="items">Each one already carries whether it is a good thing or a bad one.</param>
    private static IReadOnlyList<Tally> Group(IEnumerable<(string Label, StatTone Tone)> items)
    {
        var groups = items
            .GroupBy(i => i.Label)
            .Select(g => new { Label = g.Key, Count = g.Count(), Tone = g.First().Tone })
            .OrderByDescending(g => g.Count)
            .ToList();

        var biggest = groups.Count > 0 ? groups.Max(g => g.Count) : 0;

        return groups
            .Select(g => new Tally(g.Label, g.Count,
                biggest == 0 ? 0 : (double)g.Count / biggest, g.Tone))
            .ToList();
    }

    /// <summary>What a fate means for the run. Released counts as bad: it is gone either way.</summary>
    private static StatTone Of(PokemonStatus status) => status switch
    {
        PokemonStatus.Alive => StatTone.Good,
        PokemonStatus.Dead => StatTone.Bad,
        _ => StatTone.Neutral
    };

    /// <summary>
    /// The handful of figures a player would actually tell somebody about.
    /// </summary>
    /// <remarks>
    /// The stretch without a death is measured in <b>events</b> and not in days, and the label says
    /// so: PermaLocke only sees what happens while it is open, so counting days would turn a week
    /// of not playing into a week of surviving.
    /// </remarks>
    private static IReadOnlyList<RecordLine> Records(
        IReadOnlyList<GameEvent> history, IReadOnlyList<PokemonEntry> team, Run run)
    {
        if (history.Count == 0)
        {
            return [];
        }

        var lines = new List<RecordLine>();

        var best = history.MaxBy(e => e.PointsDelta);
        if (best is { PointsDelta: > 0 })
        {
            lines.Add(new RecordLine("Mayor ganancia", $"+{best.PointsDelta}", best.Description,
                StatTone.Good));
        }

        var worst = history.MinBy(e => e.PointsDelta);
        if (worst is { PointsDelta: < 0 })
        {
            lines.Add(new RecordLine("Mayor pérdida", worst.PointsDelta.ToString(), worst.Description,
                StatTone.Bad));
        }

        lines.Add(new RecordLine("Racha sin bajas",
            $"{LongestCleanRun(history)} eventos",
            "Contado en eventos y no en días: PermaLocke solo ve lo que pasa con la aplicación abierta.",
            StatTone.Good));

        var days = history
            .GroupBy(e => e.Timestamp.LocalDateTime.Date)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (days is not null)
        {
            lines.Add(new RecordLine("Día más movido",
                days.Key.ToString("dd/MM/yyyy"), $"{days.Count()} eventos"));
        }

        var span = history[^1].Timestamp - run.CreatedAt;
        lines.Add(new RecordLine("La run lleva viva",
            $"{Math.Max(1, (int)span.TotalDays)} día(s)",
            $"Desde el {run.CreatedAt.LocalDateTime:dd/MM/yyyy}"));

        var shinies = team.Count(p => p.IsShiny);
        if (shinies > 0)
        {
            lines.Add(new RecordLine("Variocolor registrados", shinies.ToString()));
        }

        return lines;
    }

    private static int LongestCleanRun(IReadOnlyList<GameEvent> history)
    {
        var longest = 0;
        var current = 0;

        // Una muerte revocada no fue una muerte: no corta la racha. Se mira por el id que la
        // revocación guarda, que es lo único que no confunde dos muertes del mismo Pokémon.
        var revoked = history
            .Where(e => e.Type == GameEventType.DeathRevoked && e.Data.ContainsKey("muerte"))
            .Select(e => e.Data["muerte"])
            .ToHashSet();

        foreach (var e in history)
        {
            if (e.Type == GameEventType.PokemonDied && !revoked.Contains(e.Id.ToString()))
            {
                current = 0;
                continue;
            }

            longest = Math.Max(longest, ++current);
        }

        return longest;
    }
}
