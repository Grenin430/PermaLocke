using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>How long a run has been played (§125).</summary>
public sealed class PlaytimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-tiempo-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2));

    /// <summary>
    /// Keyed on the emulator's own start time: PermaLocke closing and opening again mid-game carries on with the
    /// same session instead of counting a second one.
    /// </summary>
    [Fact]
    public void The_same_game_is_one_session_however_many_heartbeats()
    {
        IReadOnlyList<PlaySession> sessions = [];

        sessions = Playtime.Record(sessions, Start, Start.AddMinutes(1));
        sessions = Playtime.Record(sessions, Start, Start.AddMinutes(30));
        sessions = Playtime.Record(sessions, Start, Start.AddMinutes(20));

        var only = Assert.Single(sessions);
        Assert.Equal(TimeSpan.FromMinutes(30), only.Length);
    }

    [Fact]
    public void Another_start_is_another_session_and_they_add_up()
    {
        IReadOnlyList<PlaySession> sessions = [];

        sessions = Playtime.Record(sessions, Start, Start.AddMinutes(45));
        sessions = Playtime.Record(sessions, Start.AddHours(3), Start.AddHours(4).AddMinutes(20));

        Assert.Equal(2, sessions.Count);
        Assert.Equal(TimeSpan.FromMinutes(125), Playtime.Total(sessions));
        Assert.Equal(Start.AddHours(4).AddMinutes(20), Playtime.LastPlayed(sessions));
    }

    [Theory]
    [InlineData(0.5, "menos de un minuto")]
    [InlineData(35, "35 min")]
    [InlineData(125, "2 h 05 min")]
    [InlineData(1500, "25 h 00 min")]
    public void Play_time_is_said_in_hours_and_minutes(double minutes, string expected) =>
        Assert.Equal(expected, Playtime.Say(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void The_session_clock_counts_past_a_day()
    {
        Assert.Equal("01:24:07", Playtime.Clock(new TimeSpan(1, 24, 7)));
        Assert.Equal("26:00:00", Playtime.Clock(TimeSpan.FromHours(26)));
    }

    [Fact]
    public void Last_played_is_said_in_days()
    {
        var now = new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal("nunca", Playtime.Ago(null, now));
        Assert.Equal("hoy", Playtime.Ago(now.AddHours(-3), now));
        Assert.Equal("ayer", Playtime.Ago(now.AddDays(-1), now));
        Assert.Equal("hace 4 días", Playtime.Ago(now.AddDays(-4), now));
    }

    [Fact]
    public async Task Sessions_live_beside_the_run_and_never_bring_a_deleted_one_back()
    {
        var store = new JsonPlaytimeStore(_root);
        var run = Guid.NewGuid();

        await store.SaveAsync(run, [new PlaySession(Start, Start.AddHours(1))]);
        Assert.False(Directory.Exists(Path.Combine(_root, run.ToString("N"))));
        Assert.Empty(await store.LoadAsync(run));

        Directory.CreateDirectory(Path.Combine(_root, run.ToString("N")));
        await store.SaveAsync(run, [new PlaySession(Start, Start.AddHours(1))]);

        var read = Assert.Single(await store.LoadAsync(run));
        Assert.Equal(TimeSpan.FromHours(1), read.Length);

        await File.WriteAllTextAsync(Path.Combine(_root, run.ToString("N"), JsonPlaytimeStore.FileName), "roto");
        Assert.Empty(await store.LoadAsync(run));
    }
}
