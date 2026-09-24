using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

public sealed class KillcamBufferTests
{
    [Fact]
    public void Long_recording_keeps_only_the_latest_frames_in_order()
    {
        var buffer = new KillcamFrameBuffer(3, 1);
        for (byte i = 0; i < 100; i++) buffer.Add(i * 50, [i]);
        var clip = buffer.Snapshot(4900, 1000, 1000);
        Assert.Equal(3, buffer.Count);
        Assert.Equal(new[] { -50, 0, 50 }, clip.Select(f => f.Milliseconds));
        Assert.Equal(new byte[] { 97, 98, 99 }, clip.Select(f => f.Bgra[0]));
    }

    [Fact]
    public void Saving_owns_a_stable_copy_while_recording_continues()
    {
        var buffer = new KillcamFrameBuffer(2, 1);
        byte[] input = [42];
        buffer.Add(0, input);
        input[0] = 99;
        var clip = buffer.Snapshot(0, 0, 0);
        for (var i = 1; i <= 20; i++) buffer.Add(i, input);
        buffer.Clear(release: true);
        Assert.Equal(42, clip[0].Bgra[0]);
    }

    [Fact]
    public void Cover_and_expiry_remove_frames_at_their_original_times()
    {
        var buffer = new KillcamFrameBuffer(5, 1);
        for (var i = 0; i < 5; i++) buffer.Add(i * 50, [1]);
        buffer.RemoveAfter(120);
        buffer.ForgetBefore(50);
        var clip = buffer.Snapshot(100, 100, 100);
        Assert.Equal(new[] { -50, 0 }, clip.Select(f => f.Milliseconds));
        buffer.ForgetBefore(101);
        Assert.Empty(buffer.Snapshot(100, 100, 100));
        buffer.Clear(release: true);
        buffer.Add(500, [2]);
        Assert.Equal(2, buffer.Snapshot(500, 0, 0)[0].Bgra[0]);
    }

    [Fact]
    public void A_new_battle_does_not_include_the_previous_one()
    {
        var buffer = new KillcamFrameBuffer(2, 1);
        buffer.Add(0, [1]);
        buffer.Add(50, [2]);
        buffer.Clear();
        buffer.Add(100, [3]);
        var frame = Assert.Single(buffer.Snapshot(100, 1000, 1000));
        Assert.Equal(3, frame.Bgra[0]);
    }

    [Fact]
    public void Warm_recording_does_not_allocate_new_pixel_arrays()
    {
        var buffer = new KillcamFrameBuffer(141, 400 * 240 * 4);
        var pixels = new byte[400 * 240 * 4];
        for (var i = 0; i < 141; i++) buffer.Add(i * 50, pixels);
        var before = GC.GetAllocatedBytesForCurrentThread();
        // One minute of combat, after the seven second buffer has filled.
        for (var i = 141; i < 1341; i++)
        {
            buffer.Add(i * 50, pixels);
            buffer.ForgetBefore(i * 50 - 7000);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 1024);
    }

    [Fact]
    public void Disposed_recorder_cannot_start_a_capture_thread()
    {
        var recorder = new KillcamRecorder(NullLogger<KillcamRecorder>.Instance);
        recorder.Recording = false;
        recorder.Dispose();
        recorder.Dispose();
        recorder.Recording = true;
        Assert.False(recorder.Recording);
    }
}
