using PermaLocke.Infrastructure;
namespace PermaLocke.Core.Tests;

public sealed class LocalDistributionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-local-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void A_local_distribution_inside_a_repository_keeps_its_own_root()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "PermaLocke.slnx"), "<Solution />");
        var app = Directory.CreateDirectory(Path.Combine(_root, "package")).FullName;
        File.WriteAllText(Path.Combine(app, AppPaths.LocalMarker), "local");
        Assert.Equal(app, AppPaths.ResolveRoot(app));
        Assert.True(new AppPaths(app).LocalOnly);
    }

    [Fact]
    public void A_local_distribution_never_borrows_a_neighbours_expansion()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Expansion", "romfs"));
        var app = Directory.CreateDirectory(Path.Combine(_root, "package")).FullName;
        File.WriteAllText(Path.Combine(app, AppPaths.LocalMarker), "local");
        Assert.Equal(Path.Combine(app, "Expansion"), new AppPaths(app).Expansion);
    }
}
