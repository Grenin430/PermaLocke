using System.Collections.Concurrent;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The run is set by services that continue on the thread pool; the screens listening must hear it on their own thread
/// (logs of 2026-09-28: starting over and creating a run crashed listeners off the UI thread).
/// </summary>
public sealed class RunContextTests
{
    /// <summary>A one-thread context, like the UI dispatcher.</summary>
    private sealed class OneThread : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback, object?)> _work = [];
        public Thread Thread { get; }

        public OneThread()
        {
            Thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _work.GetConsumingEnumerable()) callback(state);
            }) { IsBackground = true };
            Thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state) => _work.Add((d, state));

        public T Run<T>(Func<T> func)
        {
            var done = new TaskCompletionSource<T>();
            Post(_ => done.SetResult(func()), null);
            return done.Task.GetAwaiter().GetResult();
        }

        public void Dispose() => _work.CompleteAdding();
    }

    [Fact]
    public async Task A_run_set_from_another_thread_is_announced_on_the_thread_that_made_the_context()
    {
        using var ui = new OneThread();
        var context = ui.Run(() => new RunContext());
        var heard = new TaskCompletionSource<Thread>();
        context.CurrentChanged += (_, _) => heard.TrySetResult(Thread.CurrentThread);

        await Task.Run(() => context.SetCurrent(Sample()));

        Assert.Same(ui.Thread, await heard.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(context.Current);
    }

    [Fact]
    public void On_its_own_thread_it_is_announced_at_once()
    {
        var context = new RunContext();
        var heard = false;
        context.CurrentChanged += (_, _) => heard = true;

        context.SetCurrent(Sample());

        Assert.True(heard);
    }

    private static PermaLocke.Core.Domain.Run Sample() => new()
    {
        Id = Guid.NewGuid(), Name = "x", Game = PermaLocke.Core.Domain.GameVersion.UltraMoon, SeedLabel = "x", Seed = 1,
        RoleId = "experto", PlayerName = "javi"
    };
}
