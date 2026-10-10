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
                var t = scenario.Moment ?? (still ? 0 : scenario.Steps.Count > 1 ? scenario.Steps[1].At + 0.2 : 0);
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

    /// <summary>The moments of each scenario worth a frame, in seconds after its first change, and the part of the panel to look at: the whole of it when none is given.</summary>
    private static readonly Dictionary<string, (double[] Offsets, CapRect? Region)> Moments = new()
    {
        ["caida"] = ([-0.3, 0.3, 1.2, 2.5, 4.0, 6.0], new CapRect(0, 40, 178, 281)),
        ["cambio-cap"] = ([-0.3, 0.4, 1.0, 1.6, 2.4, 3.6], null),
        ["subida"] = ([-0.2, 0.15, 0.5, 0.9, 1.4], new CapRect(0, 55, 178, 70)),
        ["golpe"] = ([-0.2, 0.1, 0.3, 0.8, 1.5, 3.2], new CapRect(0, 55, 178, 100)),
        ["curacion"] = ([-0.2, 0.2, 0.5, 0.9, 1.4], new CapRect(0, 55, 178, 130)),
        ["eclosion"] = ([-0.2, 0.15, 0.4, 0.8, 1.3], new CapRect(0, 85, 178, 60))
    };

    /// <summary>A strip of frames of a scenario, side by side: the moments before and after each change.</summary>
    public static void Strip(string dir, CapScenario scenario, CapMode mode, int cols, int rows)
    {
        var (offsets, region) = Moments.TryGetValue(scenario.Name, out var m) ? m : ([-0.2, 0.1, 0.4, 1.0, 2.0, 3.2], null);
        var change = scenario.Steps.Count > 1 ? scenario.Steps[1].At : 0;
        var times = offsets.Select(o => change + o).ToList();
        var frames = times.Select(t => CapSnap.Bgra(CapPlaque.Replay(scenario, mode, cols, rows, t), 2, 0x000000, 1, region)).ToList();
        var width = (region?.Width ?? cols) * 2;
        var height = (region?.Height ?? rows) * 2;
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

    /// <summary>Key frames of a scenario at the times given (absolute, in seconds), side by side, over a region of the panel, each frame with its time written under it.</summary>
    public static void TimedStrip(string file, CapScenario scenario, CapMode mode, int cols, int rows, int scale, CapRect region, double[] times)
    {
        var frames = times.Select(t => CapSnap.Bgra(CapPlaque.Replay(scenario, mode, cols, rows, t), scale, 0x000000, 1, region)).ToList();
        var width = region.Width * scale;
        var height = region.Height * scale;
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

        CapSnap.Save(file, sheet, total, height);
    }

    private static string Safe(string name) => new([.. name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_')]);
}
