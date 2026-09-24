namespace PermaLocke.App.Services;

/// <summary>Recording operations used by the game monitor.</summary>
public interface IKillcamRecorder
{
    bool Recording { get; set; }
    double Mark();
    Task<string?> SaveAsync(string path, double mark, CancellationToken ct = default);
}
