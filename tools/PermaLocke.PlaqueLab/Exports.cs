using System.IO;
using PermaLocke.App.Views;
using PermaLocke.GameLink;

namespace PermaLocke.PlaqueLab;

/// <summary>The PNGs the lab leaves: a whole emulator per mode and scenario, and strips of frames for what moves (<c>--exportar</c>, the PNG button).</summary>
public static class Exports
{
    /// <summary>The windows each mode is seen in: the player's, a small one that gives the slim column, a tiny one for the compact plaque and the side by side.</summary>
    private static readonly string[] ModeWindows =
    [
        "1920×976 · apilado (el del jugador)", "1280×690 · apilado (ventana de 720p)", "960×520 · apilado", "640×360 · apilado (diminuta)",
        "1920×976 · lado a lado", "3840×2064 · apilado (4K)"
    ];

    public static int All(string dir, LabData data)
    {
        Directory.CreateDirectory(dir);
        var count = 0;
        var options = new CapOptions();

        foreach (var scenario in data.Scenarios)
        {
            var still = scenario.Steps.Count == 1;
            foreach (var name in ModeWindows)
            {
                var preset = LabScreen.Presets.First(p => p.Name == name);

                // A scenario that moves is shown at the moment that says most; the strips show the rest.
                var t = still ? 0 : scenario.Steps.Count > 1 ? scenario.Steps[1].At + 0.2 : 0;
                var (bgra, plan) = LabScreen.Compose(scenario, t, preset, options, game: true);
                CapSnap.Save(Path.Combine(dir, $"{Safe(scenario.Name)}__{Safe(preset.Name)}__{plan.Main?.Mode.ToString() ?? "oculto"}.png"), bgra, preset.Width, preset.Height);
                count++;
            }

            if (!still)
            {
                Strip(dir, scenario, CapMode.Column, 178, 321);
                count++;
            }
        }

        // Every mode on its own grid, the way the tests draw them, with the party and the memorial.
        foreach (var scenario in data.Scenarios.Where(s => s.Name is "completo" or "caidos" or "combate"))
        {
            foreach (var (mode, cols, rows) in new[] { (CapMode.Column, 178, 321), (CapMode.Slim, 100, 321), (CapMode.Compact, 70, 90), (CapMode.Strip, 636, 52) })
            {
                var scale = mode == CapMode.Strip ? 2 : 3;
                CapSnap.Save(Path.Combine(dir, $"modo-{scenario.Name}-{mode}.png"), CapPlaque.Replay(scenario, mode, cols, rows, 3), scale);
                CapSnap.Save(Path.Combine(dir, $"modo-{scenario.Name}-{mode}-cementerio.png"), CapPlaque.Replay(scenario, mode, cols, rows, 3, CapPanelKind.Deep), scale);
                count += 2;
            }
        }

        return count;
    }

    /// <summary>A strip of frames of a scenario, side by side: the moments before and after each change.</summary>
    public static void Strip(string dir, CapScenario scenario, CapMode mode, int cols, int rows)
    {
        var times = new List<double> { 0 };
        foreach (var step in scenario.Steps.Skip(1))
        {
            times.AddRange([step.At - 0.2, step.At + 0.1, step.At + 0.4, step.At + 1.0, step.At + 2.0, step.At + 3.2, step.At + 4.5, step.At + 6.0]);
        }

        times.Add(scenario.Length);
        var frames = times.Select(t => CapSnap.Bgra(CapPlaque.Replay(scenario, mode, cols, rows, t), 2, 0x000000)).ToList();
        var width = cols * 2;
        var height = rows * 2;
        var gap = 6;
        var total = (frames.Count * (width + gap)) - gap;
        var sheet = new byte[total * height * 4];
        for (var i = 3; i < sheet.Length; i += 4) sheet[i] = 255;

        for (var f = 0; f < frames.Count; f++)
        {
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(frames[f], y * width * 4, sheet, ((y * total) + (f * (width + gap))) * 4, width * 4);
            }
        }

        CapSnap.Save(Path.Combine(dir, $"tira-{scenario.Name}.png"), sheet, total, height);
    }

    private static string Safe(string name) => new([.. name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_')]);
}
