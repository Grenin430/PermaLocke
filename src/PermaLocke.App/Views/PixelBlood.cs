using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// The blood of the death ceremony, simulated on the Pokémon's own pixel grid and drawn into a small
/// bitmap that is shown scaled up with no smoothing.
/// </summary>
/// <remarks>
/// <para>
/// Pixels and not vector shapes, because the only real thing on screen is a pixel-art sprite from
/// the cartridge: round, anti-aliased drops next to it read as a stock effect laid on top. Here one
/// cell of blood is exactly one pixel of the Pokémon, in three reds and no gradients.
/// </para>
/// <para>
/// And behaviour rather than decoration: drops leave the sprite <b>from its own opaque pixels</b>,
/// fly with gravity and a one-cell trail, and <b>stay where they land</b> — the pool on the floor is
/// built by them plus what flows out while the Pokémon sinks into it. The drips from the top bar
/// grow in fits and starts and let go of drops. When a card goes, its blood crumbles away cell by
/// cell. The randomness is cosmetic and decides nothing.
/// </para>
/// <para>
/// Everything is in cells and milliseconds of the window's own clock. Presentation only.
/// </para>
/// </remarks>
internal sealed class PixelBlood
{
    // Tres rojos y ninguno más: oscuro para bordes y estelas, medio para el cuerpo, vivo para lo que
    // brilla. Y un brillo casi blanco, raro, en la superficie del charco.
    private static readonly Color Dark = Color.FromRgb(0x4A, 0x06, 0x10);
    private static readonly Color Mid = Color.FromRgb(0x80, 0x0C, 0x16);
    private static readonly Color Bright = Color.FromRgb(0xB4, 0x16, 0x20);
    private static readonly Color Shine = Color.FromRgb(0xE2, 0x5A, 0x5A);

    /// <summary>Cells per second squared.</summary>
    private const double Gravity = 150;

    private readonly Random _random = new();
    private readonly byte[] _pixels;
    private readonly List<Drop> _drops = [];
    private readonly List<Drip> _drips = [];
    private readonly Dictionary<(int X, int Y), Color> _stains = [];

    private Card? _card;
    private double _last;

    public PixelBlood(int columns, int rows)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        Bitmap = new WriteableBitmap(Columns, Rows, 96, 96, PixelFormats.Bgra32, null);
        _pixels = new byte[Columns * Rows * 4];
    }

    public int Columns { get; }

    public int Rows { get; }

    public WriteableBitmap Bitmap { get; }

    /// <summary>The scene opens: drips start somewhere in the next couple of seconds.</summary>
    /// <param name="deepest">The lowest row a drip may reach, so it stays above the floor and the name.</param>
    public void OpenScene(double now, int deepest)
    {
        _drops.Clear();
        _drips.Clear();
        _stains.Clear();
        _card = null;
        _last = now;

        var taken = new List<int>();

        for (var i = 0; i < 16; i++)
        {
            var column = _random.Next(1, Columns - 2);

            // Separados: dos hilos pegados se leen como uno gordo.
            if (taken.Any(other => Math.Abs(other - column) < 3))
            {
                continue;
            }

            taken.Add(column);

            _drips.Add(new Drip
            {
                Column = column,
                Width = _random.NextDouble() < 0.3 ? 2 : 1,
                Starts = now + 350 + _random.Next(0, 1400),
                Longest = Math.Max(3, Math.Min(deepest, 3 + (Math.Sqrt(_random.NextDouble()) * _random.NextDouble() * deepest))),
                NextLetGo = 4 + _random.Next(0, 6)
            });
        }
    }

    /// <summary>A death starts: its blood comes out of the sprite at <paramref name="hit"/>.</summary>
    /// <param name="body">The sprite's opaque pixels, already in cells.</param>
    /// <param name="floor">The row under the sprite's feet, where the pool forms.</param>
    public void StartCard(double now, IReadOnlyList<(int X, int Y)> body, int floor, int hit, int sinks,
        int sunk, int leaves)
    {
        // La sangre de la tarjeta anterior se va con ella; la de los hilos es de la escena y se queda.
        _drops.RemoveAll(drop => drop.OfCard);
        _stains.Clear();

        if (body.Count == 0)
        {
            _card = null;
            return;
        }

        var left = body.Min(cell => cell.X);
        var right = body.Max(cell => cell.X);

        _card = new Card
        {
            Starts = now,
            Body = body,
            Floor = floor,
            Centre = (left + right) / 2,
            Reach = Math.Max(5, (int)((right - left + 1) * 0.8)),
            Hit = hit,
            Sinks = sinks,
            Sunk = sunk,
            Leaves = leaves,
            Ragged = Enumerable.Range(0, Columns).Select(_ => _random.Next(0, 3)).ToArray()
        };
    }

    /// <summary>Advances the simulation to <paramref name="now"/> and redraws the bitmap.</summary>
    public void Tick(double now)
    {
        var dt = Math.Clamp((now - _last) / 1000, 0, 0.05);
        _last = now;

        Spawn(now);
        Move(dt);
        Grow(now, dt);
        Draw(now);
    }

    private void Spawn(double now)
    {
        if (_card is not { } card)
        {
            return;
        }

        var t = now - card.Starts;

        // EL GOLPE: sale de píxeles de su propio cuerpo, sobre todo de la mitad de arriba, hacia fuera
        // y hacia arriba, cada gota alejándose del centro por su lado.
        if (!card.Burst && t >= card.Hit)
        {
            card.Burst = true;

            var top = card.Body.Min(cell => cell.Y);
            var bottom = card.Body.Max(cell => cell.Y);
            var upper = card.Body.Where(cell => cell.Y <= top + ((bottom - top) * 0.7)).ToList();

            for (var i = 0; i < 46; i++)
            {
                var (x, y) = upper[_random.Next(upper.Count)];
                var side = x == card.Centre ? (_random.NextDouble() < 0.5 ? -1 : 1) : Math.Sign(x - card.Centre);

                _drops.Add(new Drop
                {
                    X = x,
                    Y = y,
                    PreviousX = x,
                    PreviousY = y,
                    Vx = (side * (6 + (_random.NextDouble() * 22))) + ((_random.NextDouble() - 0.5) * 10),
                    Vy = -(12 + (_random.NextDouble() * 44)),
                    Colour = _random.NextDouble() < 0.5 ? Bright : Mid,
                    OfCard = true,
                    Floor = card.Floor,

                    // Una de cada cinco es salpicadura fina: se deshace en el aire sin llegar al suelo.
                    Life = _random.NextDouble() < 0.2 ? 0.25 + (_random.NextDouble() * 0.3) : double.MaxValue
                });
            }
        }

        // AL HUNDIRSE: el charco salta un poco por los lados, como un líquido que se aparta.
        if (t >= card.Sinks && t < card.Sunk && _random.NextDouble() < 0.35)
        {
            var side = _random.NextDouble() < 0.5 ? -1 : 1;
            var edge = card.Centre + (side * _random.Next(1, Math.Max(2, card.Reach / 2)));

            _drops.Add(new Drop
            {
                X = edge,
                Y = card.Floor - 1,
                PreviousX = edge,
                PreviousY = card.Floor - 1,
                Vx = side * (3 + (_random.NextDouble() * 10)),
                Vy = -(8 + (_random.NextDouble() * 18)),
                Colour = Mid,
                OfCard = true,
                Floor = card.Floor,
                Life = double.MaxValue
            });
        }
    }

    private void Move(double dt)
    {
        foreach (var drop in _drops)
        {
            drop.PreviousX = drop.X;
            drop.PreviousY = drop.Y;
            drop.Vy += Gravity * dt;
            drop.X += drop.Vx * dt;
            drop.Y += drop.Vy * dt;
            drop.Life -= dt;

            if (drop.Life <= 0)
            {
                drop.Gone = true;
                continue;
            }

            // Toca el suelo: se queda. A veces se abre en dos celdas, como una gota que revienta.
            if (drop.Floor is { } floor && drop.Vy > 0 && drop.Y >= floor)
            {
                var x = (int)Math.Round(drop.X);

                Stain(x, floor, _random.NextDouble() < 0.6 ? Mid : Dark);

                if (_random.NextDouble() < 0.4)
                {
                    Stain(x + (drop.Vx >= 0 ? 1 : -1), floor, Dark);
                }

                drop.Gone = true;
                continue;
            }

            if (drop.Y >= Rows || drop.X < -1 || drop.X > Columns)
            {
                drop.Gone = true;
            }
        }

        _drops.RemoveAll(drop => drop.Gone);
    }

    private void Stain(int x, int y, Color colour)
    {
        if (x >= 0 && x < Columns && y >= 0 && y < Rows)
        {
            // Lo oscuro no pisa lo vivo: una gota nueva sobre una mancha la reaviva, no la apaga.
            if (!_stains.TryGetValue((x, y), out var already) || already == Dark)
            {
                _stains[(x, y)] = colour;
            }
        }
    }

    private void Grow(double now, double dt)
    {
        foreach (var drip in _drips.Where(drip => now >= drip.Starts))
        {
            // A trompicones: la velocidad da tumbos, a veces se para, y cuanto más largo más le cuesta.
            drip.Speed = Math.Clamp(drip.Speed + ((_random.NextDouble() - 0.4) * 90 * dt), 0, 16);

            if (_random.NextDouble() < 0.9 * dt)
            {
                drip.Speed = 0;
            }

            var room = 1 - (drip.Length / drip.Longest);
            drip.Length = Math.Min(drip.Longest, drip.Length + (drip.Speed * Math.Max(0.3, room) * dt));

            // Suelta una gota: la punta se va y el hilo se queda un par de celdas más corto.
            if (drip.Length >= drip.NextLetGo)
            {
                _drops.Add(new Drop
                {
                    X = drip.Column,
                    Y = drip.Length,
                    PreviousX = drip.Column,
                    PreviousY = drip.Length,
                    Vx = 0,
                    Vy = 2,
                    Colour = Bright,
                    OfCard = false,
                    Floor = null,
                    Life = double.MaxValue
                });

                drip.Length = Math.Max(1, drip.Length - 2);
                drip.NextLetGo = drip.Length + 3 + _random.Next(0, 8);
            }
        }
    }

    private void Draw(double now)
    {
        Array.Clear(_pixels);

        foreach (var drip in _drips)
        {
            var length = (int)drip.Length;

            for (var row = 0; row < length; row++)
            {
                for (var w = 0; w < drip.Width; w++)
                {
                    Put(drip.Column + w, row, row < 2 ? Dark : Mid);
                }
            }

            if (length > 0)
            {
                // La punta, más viva que el hilo. En los de dos celdas, redondeada: la fila entera y
                // una celda más colgando. Un píxel pegado a un lado se leía como un palo de hockey.
                for (var w = 0; w < drip.Width; w++)
                {
                    Put(drip.Column + w, length, Bright);
                }

                if (drip.Width == 2 && length > 3)
                {
                    Put(drip.Column + (drip.Column % 2), length + 1, Mid);
                }
            }
        }

        var crumbling = _card is { } going ? Math.Clamp((now - going.Starts - going.Leaves) / 500, 0, 1) : 0;

        if (_card is { } card)
        {
            DrawPool(card, now - card.Starts, crumbling);
        }

        foreach (var ((x, y), colour) in _stains)
        {
            if (Stays(x, y, crumbling))
            {
                Put(x, y, colour);
            }
        }

        foreach (var drop in _drops)
        {
            var x = (int)Math.Round(drop.X);
            var y = (int)Math.Round(drop.Y);
            var px = (int)Math.Round(drop.PreviousX);
            var py = (int)Math.Round(drop.PreviousY);

            if (drop.OfCard && !Stays(x, y, crumbling))
            {
                continue;
            }

            // Estela de una celda si se ha movido; y cayendo rápido se estira hacia arriba.
            if (px != x || py != y)
            {
                Put(px, py, Dark);
            }

            if (drop.Vy > 45)
            {
                Put(x, y - 1, Mid);
            }

            Put(x, y, drop.Colour);
        }

        Bitmap.WritePixels(new Int32Rect(0, 0, Columns, Rows), _pixels, Columns * 4, 0);
    }

    /// <summary>
    /// The pool: two rows on the floor that widen from the hit, ragged at the edges, with the odd
    /// bump on its surface and a glint or two.
    /// </summary>
    private void DrawPool(Card card, double t, double crumbling)
    {
        var grow = Math.Clamp((t - card.Hit - 60) / 1900, 0, 1);

        if (grow <= 0)
        {
            return;
        }

        var eased = 1 - Math.Pow(1 - grow, 3);
        var half = (int)Math.Round(card.Reach * eased);

        for (var dx = -half; dx <= half; dx++)
        {
            var x = card.Centre + dx;

            if (x < 0 || x >= Columns)
            {
                continue;
            }

            // El borde se come como mucho una celda: irregular, pero sin huecos dentro.
            var ragged = card.Ragged[x] == 2 ? 1 : 0;
            var distance = Math.Abs(dx);

            // Tres filas en forma de lente: la de arriba es la más ancha y la de abajo solo el centro.
            // Superficie viva con algún brillo, cuerpo medio, bordes y fondo oscuros.
            if (distance <= half - ragged && Stays(x, card.Floor, crumbling))
            {
                Put(x, card.Floor, distance >= half - ragged ? Dark : Glint(x) ? Shine : Bright);
            }

            var second = (int)(half * 0.8) - ragged;

            if (distance <= second && Stays(x, card.Floor + 1, crumbling))
            {
                Put(x, card.Floor + 1, distance >= second ? Dark : Mid);
            }

            var third = (int)(half * 0.45) - ragged;

            if (distance <= third && Stays(x, card.Floor + 2, crumbling))
            {
                Put(x, card.Floor + 2, Dark);
            }

            // Bultos en la superficie, hacia el centro y cuando ya ha crecido: lo que aún está cayendo.
            if (distance < half / 2 && card.Ragged[x] == 2 && eased > 0.5 && Stays(x, card.Floor - 1, crumbling))
            {
                Put(x, card.Floor - 1, Mid);
            }
        }
    }

    private static bool Glint(int x) => Hash(x, 7) < 0.12;

    /// <summary>Whether a cell survives the crumbling: each one goes at its own fixed moment.</summary>
    private static bool Stays(int x, int y, double crumbling) => crumbling <= 0 || Hash(x, y) >= crumbling;

    private static double Hash(int x, int y)
    {
        unchecked
        {
            var h = (uint)((x * 73856093) ^ (y * 19349663));
            h ^= h >> 13;
            h *= 0x5BD1E995;
            h ^= h >> 15;
            return (h & 0xFFFF) / 65536.0;
        }
    }

    private void Put(int x, int y, Color colour)
    {
        if (x < 0 || x >= Columns || y < 0 || y >= Rows)
        {
            return;
        }

        var i = ((y * Columns) + x) * 4;
        _pixels[i] = colour.B;
        _pixels[i + 1] = colour.G;
        _pixels[i + 2] = colour.R;
        _pixels[i + 3] = 255;
    }

    private sealed class Drop
    {
        public double X, Y, PreviousX, PreviousY, Vx, Vy, Life;
        public Color Colour;
        public bool OfCard, Gone;
        public int? Floor;
    }

    private sealed class Drip
    {
        public int Column, Width;
        public double Starts, Length, Longest, Speed, NextLetGo;
    }

    private sealed class Card
    {
        public required double Starts;
        public required IReadOnlyList<(int X, int Y)> Body;
        public required int Floor, Centre, Reach, Hit, Sinks, Sunk, Leaves;
        public required int[] Ragged;
        public bool Burst;
    }
}
