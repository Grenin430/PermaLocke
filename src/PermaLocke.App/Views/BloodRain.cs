namespace PermaLocke.App.Views;

/// <summary>
/// The blood rain of a team wipe (§184), cell by cell: red drops falling over the emulator for twelve seconds,
/// splashing where they land and pooling at the bottom, then gone.
/// </summary>
/// <remarks>
/// <para>
/// Pure drawing, without WPF, so it can be tested: <see cref="Advance"/> moves it one step and <see cref="Draw"/>
/// leaves the picture in <see cref="Pixels"/>, one cell per pixel, for <see cref="GhostWindow"/> to blow up.
/// </para>
/// <para>
/// No smoothing and no gradients: three flat reds, a flat wash that comes and goes in quarters, and the drops move
/// whole cells at a time. Decoration only; the seed decides nothing that matters.
/// </para>
/// </remarks>
public sealed class BloodRain
{
    /// <summary>How long it rains, start to last drop.</summary>
    public static readonly TimeSpan Length = TimeSpan.FromSeconds(12);

    /// <summary>One step of the rain: the same beat as the ghost's crossing, so both move like a game sprite.</summary>
    public static readonly TimeSpan Step = TimeSpan.FromMilliseconds(45);

    /// <summary>Every step of the rain; <see cref="Done"/> after the last.</summary>
    public static readonly int Steps = (int)(Length / Step);

    // Colores en 0xAARRGGBB, que en memoria es el orden B, G, R, A de Bgra32.
    private const uint Deep = 0xFF5E0710;
    private const uint Blood = 0xFF9E0F19;
    private const uint Bright = 0xFFD42A33;
    private const uint Wash = 0x00500008;

    /// <summary>The wash at its thickest: enough to redden the game, not to hide it.</summary>
    private const int WashAlpha = 0x34;

    private static readonly int RampSteps = (int)(TimeSpan.FromSeconds(1.5) / Step);
    private static readonly int StopRaining = (int)(TimeSpan.FromSeconds(9) / Step);
    private static readonly int FadeFrom = (int)(TimeSpan.FromSeconds(10) / Step);

    private readonly Random _random;
    private readonly List<Drop> _drops = [];
    private readonly List<Splash> _splashes = [];
    private readonly double[] _pool;
    private readonly int _poolMax;
    private double _owed;

    public BloodRain(int columns, int rows, int seed)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        Pixels = new uint[Columns * Rows];
        _pool = new double[Columns];
        _poolMax = Math.Max(1, Rows / 18);
        _random = new Random(seed);
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>The picture after the last <see cref="Draw"/>, row by row, one cell per pixel.</summary>
    public uint[] Pixels { get; }

    /// <summary>Steps taken so far.</summary>
    public int Frame { get; private set; }

    public bool Done => Frame >= Steps;

    /// <summary>How hard it rains now, from 0 to 1: it starts light, pours, and stops before the end.</summary>
    private double Intensity => Frame >= StopRaining ? 0 : Math.Min(1, (double)Frame / RampSteps);

    /// <summary>What is left of everything, in quarters: 4 while it rains, down to 0 in the last two seconds.</summary>
    private int Quarters => Frame < FadeFrom
        ? 4
        : Math.Max(0, 4 - ((Frame - FadeFrom) * 4 / Math.Max(1, Steps - FadeFrom)));

    /// <summary>One step: new drops, every drop down, splashes where they land.</summary>
    public void Advance()
    {
        if (Done)
        {
            return;
        }

        Frame++;

        _owed += Intensity * Columns / 26.0;

        for (; _owed >= 1; _owed--)
        {
            var length = _random.Next(2, 6);
            _drops.Add(new Drop(_random.Next(Columns), -_random.Next(0, Rows / 3 + 1) - length, _random.Next(2, 5), length));
        }

        for (var i = _splashes.Count - 1; i >= 0; i--)
        {
            _splashes[i] = _splashes[i] with { Age = _splashes[i].Age + 1 };

            if (_splashes[i].Age > 2)
            {
                _splashes.RemoveAt(i);
            }
        }

        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i] with { Y = _drops[i].Y + _drops[i].Speed };
            var surface = Surface(drop.X);

            if (drop.Y < surface)
            {
                _drops[i] = drop;
                continue;
            }

            _drops.RemoveAt(i);
            _splashes.Add(new Splash(drop.X, surface, 0));

            // El charco crece donde cae, y un poco a los lados para que no salgan agujas.
            Pool(drop.X, 0.6);
            Pool(drop.X - 1, 0.2);
            Pool(drop.X + 1, 0.2);
        }
    }

    /// <summary>Leaves the current picture in <see cref="Pixels"/>.</summary>
    public void Draw()
    {
        var quarters = Quarters;
        var wash = Math.Min(4, Frame * 4 / Math.Max(1, RampSteps));
        var background = Wash | ((uint)(WashAlpha * Math.Min(wash, quarters) / 4) << 24);

        Array.Fill(Pixels, background);

        if (quarters == 0)
        {
            return;
        }

        for (var x = 0; x < Columns; x++)
        {
            var height = (int)_pool[x];

            for (var y = Rows - height; y < Rows; y++)
            {
                Put(x, y, y == Rows - height ? Blood : Deep, quarters);
            }
        }

        foreach (var drop in _drops)
        {
            for (var i = 0; i < drop.Length; i++)
            {
                Put(drop.X, drop.Y - i, i == 0 ? Bright : i <= drop.Length / 2 ? Blood : Deep, quarters);
            }
        }

        foreach (var splash in _splashes)
        {
            var reach = splash.Age + 1;
            var rise = splash.Age == 2 ? 1 : splash.Age + 1;
            var colour = splash.Age == 0 ? Bright : Blood;

            Put(splash.X - reach, splash.Y - rise, colour, quarters);
            Put(splash.X + reach, splash.Y - rise, colour, quarters);
        }
    }

    /// <summary>The first row a drop in this column lands on: the top of the pool, or the bottom.</summary>
    private int Surface(int x) => Rows - 1 - (int)_pool[x];

    private void Pool(int x, double amount)
    {
        if (x >= 0 && x < Columns)
        {
            _pool[x] = Math.Min(_poolMax, _pool[x] + amount);
        }
    }

    private void Put(int x, int y, uint colour, int quarters)
    {
        if (x >= 0 && x < Columns && y >= 0 && y < Rows)
        {
            Pixels[(y * Columns) + x] = Fade(colour, quarters);
        }
    }

    private static uint Fade(uint colour, int quarters) =>
        (colour & 0x00FFFFFF) | ((colour >> 24) * (uint)quarters / 4 << 24);

    private readonly record struct Drop(int X, int Y, int Speed, int Length);

    private readonly record struct Splash(int X, int Y, int Age);
}
