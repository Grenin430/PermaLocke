using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

public sealed class NotificationTests
{
    [Theory]
    [InlineData(1.0, 470, 760, 16)]
    [InlineData(1.25, 588, 950, 20)]
    [InlineData(1.5, 705, 1140, 24)]
    [InlineData(2.0, 940, 1520, 32)]
    public void Scaling_changes_size_without_scaling_the_screen_anchor(double scale, int width, int height, int margin)
    {
        var area = (Left: 0, Top: 0, Width: 3840, Height: 2160);
        var box = ToastPlacement.Calculate(area, area, scale, scale);
        Assert.Equal(width, box.Width);
        Assert.Equal(height, box.Height);
        Assert.Equal(area.Width - margin, box.Left + box.Width);
        Assert.Equal(area.Height - margin, box.Top + box.Height);
    }

    [Theory]
    [InlineData(-1920, -200)]
    [InlineData(3840, 300)]
    public void A_secondary_monitor_keeps_its_native_origin(int left, int top)
    {
        var area = (Left: left, Top: top, Width: 1920, Height: 1040);
        var box = ToastPlacement.Calculate(area, area, 1.5, 1.5);
        Assert.Equal(left + 1920 - 24, box.Left + box.Width);
        Assert.Equal(top + 1040 - 24, box.Top + box.Height);
        Assert.True(box.Left >= left && box.Top >= top);
    }

    [Fact]
    public void A_small_screen_limits_the_whole_stack_to_visible_space()
    {
        var area = (Left: 0, Top: 0, Width: 800, Height: 560);
        var box = ToastPlacement.Calculate(area, area, 2, 2);
        Assert.Equal((32, 32, 736, 496), box);
    }

    [Theory]
    [InlineData(-32000, -32000)]
    [InlineData(2000, 2000)]
    public void An_offscreen_window_cannot_move_the_notices_offscreen(int left, int top)
    {
        var box = ToastPlacement.Calculate((left, top, 1000, 700), (0, 0, 1920, 1040), 1, 1);
        Assert.InRange(box.Left, 16, 1920 - box.Width - 16);
        Assert.InRange(box.Top, 16, 1040 - box.Height - 16);
    }

    [Fact]
    public void A_windowed_game_anchors_the_bottom_card_in_the_game()
    {
        var box = ToastPlacement.Calculate((200, 100, 1000, 700), (0, 0, 1920, 1040), 1, 1);
        Assert.Equal(1184, box.Left + box.Width);
        Assert.Equal(784, box.Top + box.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_synchronous_and_asynchronous_dispatch_failures_are_observed(bool asyncFailure)
    {
        var failure = new InvalidOperationException("UI unavailable");
        var dispatcher = new StubDispatcher(_ => asyncFailure ? Task.FromException(failure) : throw failure);
        var logger = new CaptureLogger();
        var notifier = new Notifier(dispatcher, logger);
        await notifier.SayAsync(ToastKind.Reward, "Premio", "Prueba");
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Error == failure);
        Assert.Empty(notifier.Showing);
    }

    [Fact]
    public async Task Disabled_notices_are_reported_without_dispatch_or_events()
    {
        var dispatcher = new StubDispatcher(_ => throw new Exception("Should not dispatch"));
        var logger = new CaptureLogger();
        var notifier = new Notifier(dispatcher, logger) { Enabled = false };
        var said = false;
        notifier.Said += (_, _) => said = true;
        await notifier.SayAsync(ToastKind.Info, "Prueba", "");
        Assert.False(said);
        Assert.Equal(0, dispatcher.Calls);
        Assert.Contains(logger.Entries, entry => entry.Text.Contains("desactivadas"));
    }

    [Fact]
    public async Task A_failed_listener_does_not_prevent_the_notice_from_reaching_the_dispatcher()
    {
        var dispatcher = new StubDispatcher(_ => Task.CompletedTask);
        var logger = new CaptureLogger();
        var notifier = new Notifier(dispatcher, logger);
        notifier.Said += (_, _) => throw new InvalidOperationException("Listener failed");
        await notifier.SayAsync(ToastKind.Info, "Prueba", "");
        Assert.Equal(1, dispatcher.Calls);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private sealed class StubDispatcher(Func<Func<Task>, Task> dispatch) : IUiDispatcher
    {
        public int Calls { get; private set; }
        public Task InvokeAsync(Func<Task> action)
        {
            Calls++;
            return dispatch(action);
        }
    }

    private sealed class CaptureLogger : ILogger<Notifier>
    {
        public List<(LogLevel Level, Exception? Error, string Text)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((level, exception, formatter(state, exception)));
    }
}
