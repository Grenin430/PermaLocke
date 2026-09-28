using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;

namespace PermaLocke.App.ViewModels;

/// <summary>One stretch of the game in a pillar's chart: the cartridge's figure and ours, as bar lengths and as text.</summary>
public sealed record DifficultyBar(string Stretch, double Before, double After, string BeforeText, string AfterText);

/// <summary>One of the four things the randomizer changes in the rivals (2026-09-28).</summary>
public sealed record DifficultyPillar(string Title, string Icon, string Before, string After, string Label, string Text,
    IReadOnlyList<DifficultyBar> Bars, string Unit);

public sealed record DifficultyRule(string Title, string Text);

/// <summary>One stage of the level caps, as a step of the climb.</summary>
public sealed record CapStep(string Name, int Level, double Share);

/// <summary>
/// INFORMACIÓN › DIFICULTAD (2026-09-28): the difficulty climb, told plainly and drawn. The figures are in
/// <c>Data/dificultad.json</c> (measured against the cartridge, see <c>subida_de_dificultad_explicada.txt</c>); the caps come
/// from <c>Data/levelcaps.json</c> through <see cref="LevelCapTable"/>. Read-only.
/// </summary>
public sealed partial class DifficultyViewModel(AppPaths paths, LevelCapTable caps, ILogger<DifficultyViewModel> logger)
    : SectionViewModel("DIFICULTAD")
{
    public override string IconKey => "IconChart";

    public override GameNeed Needs => GameNeed.None;

    [ObservableProperty]
    private string _heading = string.Empty;

    [ObservableProperty]
    private string _intro = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<DifficultyPillar> _pillars = [];

    [ObservableProperty]
    private IReadOnlyList<DifficultyRule> _rules = [];

    [ObservableProperty]
    private IReadOnlyList<CapStep> _caps = [];

    [ObservableProperty]
    private string _status = string.Empty;

    private sealed record BarRow(string Tramo, double Antes, double Ahora, double Maximo);

    private sealed record PillarRow(string Titulo, string Icono, string Antes, string Ahora, string Etiqueta, string Texto,
        List<BarRow> Barras, string Unidad);

    private sealed record RuleRow(string Titulo, string Texto);

    private sealed record FileRow(string Titulo, string Entradilla, List<PillarRow> Pilares, List<RuleRow> Reglas);

    public override async Task ActivateAsync()
    {
        if (Pillars.Count > 0)
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<FileRow>(await File.ReadAllTextAsync(Path.Combine(paths.Data, "dificultad.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            Heading = file.Titulo;
            Intro = file.Entradilla;
            Pillars = [.. file.Pilares.Select(p => new DifficultyPillar(p.Titulo, p.Icono, p.Antes, p.Ahora, p.Etiqueta, p.Texto,
                [.. p.Barras.Select(b => new DifficultyBar(b.Tramo, Share(b.Antes, b.Maximo), Share(b.Ahora, b.Maximo),
                    Number(b.Antes), Number(b.Ahora)))], p.Unidad))];
            Rules = [.. file.Reglas.Select(r => new DifficultyRule(r.Titulo, r.Texto))];

            var top = Math.Max(1, caps.Stages.Max(s => s.Level));
            Caps = [.. caps.Stages.Select(s => new CapStep(s.Name.ToUpperInvariant(), s.Level, s.Level / (double)top))];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer Data/dificultad.json");
            Status = "No se ha podido leer la dificultad (falta Data/dificultad.json).";
        }
    }

    private static double Share(double value, double max) => max <= 0 ? 0 : Math.Clamp(value / max, 0, 1);

    private static string Number(double value) => value.ToString(value % 1 == 0 ? "0" : "0.0", CultureInfo.GetCultureInfo("es-ES"));
}
