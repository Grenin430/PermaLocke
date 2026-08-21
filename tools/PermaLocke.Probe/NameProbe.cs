using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Puts the species name back on the Pokémon PermaLocke delivered before it learned to write one.
/// </summary>
/// <remarks>
/// Listing and fixing are two separate commands on purpose: this writes to the player's own save,
/// so they get to read what is about to change before anything does.
/// </remarks>
public static class NameProbe
{
    public static int Run(bool write)
    {
        var root = FindRoot();
        var repair = new SaveNameRepair(
            new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
                new AzaharRpcClient(), AppContext.BaseDirectory),
            Path.Combine(root, "Saves", "backup"),
            NullLogger<SaveNameRepair>.Instance);

        var report = write ? repair.Repair() : repair.Inspect();

        foreach (var line in report.Named.Take(200))
        {
            Console.WriteLine($"  {line}");
        }

        if (report.Named.Count > 200)
        {
            Console.WriteLine($"  ... y {report.Named.Count - 200} más");
        }

        Console.WriteLine();
        Console.WriteLine(report.Message);

        if (!write && report.Nameless > 0)
        {
            Console.WriteLine("Para arreglarlo, cierra el juego y ejecuta: Probe --nombres --arreglar");
        }

        return 0;
    }

    /// <summary>Walks up to the repository root, which is where Saves/ lives.</summary>
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
