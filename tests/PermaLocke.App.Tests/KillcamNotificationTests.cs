using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Tests;

public sealed class KillcamNotificationTests
{
    [Fact]
    public async Task Cemetery_refreshes_after_the_clip_is_ready()
    {
        var recorder = new PendingRecorder();
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        using var monitor = Monitor(recorder, paths);
        var refreshed = 0;
        monitor.RunDataChanged += (_, _) => refreshed++;
        var run = Guid.NewGuid();
        var pokemon = Guid.NewGuid();
        var save = monitor.SaveKillcamAsync(run, pokemon, 1234);
        Assert.Equal(0, refreshed);
        Assert.False(save.IsCompleted);
        Assert.Equal(KillcamClip.PathFor(paths.Saves, run, pokemon), recorder.Path);
        Assert.Equal(1234, recorder.MarkAt);
        recorder.Complete.SetResult(recorder.Path);
        await save;
        Assert.Equal(1, refreshed);
    }

    [Fact]
    public async Task A_missing_recording_does_not_announce_a_ready_clip()
    {
        var recorder = new PendingRecorder();
        using var monitor = Monitor(recorder, new AppPaths(Path.GetTempPath()));
        var refreshed = false;
        monitor.RunDataChanged += (_, _) => refreshed = true;
        var save = monitor.SaveKillcamAsync(Guid.NewGuid(), Guid.NewGuid(), 0);
        recorder.Complete.SetResult(null);
        await save;
        Assert.False(refreshed);
    }

    [Fact]
    public async Task A_failed_clip_write_is_observed_without_interrupting_death_recording()
    {
        var recorder = new PendingRecorder();
        using var monitor = Monitor(recorder, new AppPaths(Path.GetTempPath()));
        var save = monitor.SaveKillcamAsync(Guid.NewGuid(), Guid.NewGuid(), 0);
        recorder.Complete.SetException(new IOException("Disk unavailable"));
        await save;
    }

    private static GameLinkMonitor Monitor(IKillcamRecorder recorder, AppPaths paths) =>
        new(provider: null!, runContext: null!, watcher: null!, writer: null!, progress: null!, caps: null!,
            encounterGuard: null!, encounters: null!, rewards: null!, maintenance: null!, events: null!,
            clock: null!, battleTables: null!, killcam: recorder, paths: paths,
            logger: NullLogger<GameLinkMonitor>.Instance, balls: null!, integrity: null!, rules: null!, berries: null!);

    private sealed class PendingRecorder : IKillcamRecorder
    {
        public bool Recording { get; set; }
        public string? Path { get; private set; }
        public double MarkAt { get; private set; }
        public TaskCompletionSource<string?> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public double Mark() => 0;
        public Task<string?> SaveAsync(string path, double mark, CancellationToken ct = default)
        {
            Path = path;
            MarkAt = mark;
            return Complete.Task;
        }
    }
}
