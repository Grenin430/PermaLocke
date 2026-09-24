namespace PermaLocke.App.Services;

/// <summary>A bounded ring that reuses pixel arrays; snapshots own their pixels.</summary>
internal sealed class KillcamFrameBuffer(int capacity, int frameBytes)
{
    private readonly byte[]?[] _pixels = new byte[capacity][];
    private readonly double[] _times = new double[capacity];
    private int _head;
    public int Count { get; private set; }

    public void Add(double at, ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != frameBytes) throw new ArgumentException("Unexpected frame size.", nameof(pixels));
        var index = (_head + Count) % capacity;
        if (Count == capacity) _head = (_head + 1) % capacity;
        else Count++;
        pixels.CopyTo(_pixels[index] ??= new byte[frameBytes]);
        _times[index] = at;
    }

    public void ForgetBefore(double at)
    {
        while (Count > 0 && _times[_head] < at)
        {
            _head = (_head + 1) % capacity;
            Count--;
        }
    }

    public void RemoveAfter(double at)
    {
        while (Count > 0 && _times[(_head + Count - 1) % capacity] > at) Count--;
    }

    public List<(int Milliseconds, byte[] Bgra)> Snapshot(double mark, double before, double after)
    {
        var result = new List<(int, byte[])>();
        for (var i = 0; i < Count; i++)
        {
            var index = (_head + i) % capacity;
            var at = _times[index];
            if (at >= mark - before && at <= mark + after)
                result.Add(((int)Math.Round(at - mark), (byte[])_pixels[index]!.Clone()));
        }
        return result;
    }

    public void Clear(bool release = false)
    {
        _head = Count = 0;
        if (release) Array.Clear(_pixels);
    }
}
