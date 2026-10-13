namespace AVAArkanoid.Models;

/// <summary>A 2D point or vector in game pixels (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec Normal => Length < 1e-9 ? new Vec(0, 0) : this * (1 / Length);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's. Pipes make the walls at the
/// sides and the top; the bricks sit in a grid of 13 columns, 16 by 8 pixels each, below the top pipe.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const double Step = 1.0 / 60;
    public const double Left = 8;
    public const double Right = 216;
    public const double Top = 16;
    public const int Columns = 13;
    public const int Rows = 16;
    public const double BrickW = 16;
    public const double BrickH = 8;
    public const double BricksTop = 32;
    public const double PaddleY = 232;
    public const double BallRadius = 2.5;

    public static double BrickX(int col) => Left + col * BrickW;
    public static double BrickY(int row) => BricksTop + row * BrickH;
}

/// <summary>What a brick is: a colour (worth more further down the list), silver, or gold.</summary>
public enum BrickKind : byte { White, Orange, Cyan, Green, Red, Blue, Pink, Yellow, Silver, Gold }

/// <summary>
/// A round's bricks, written as text, a character a brick: <c>w o c g r b p y</c> for the colours,
/// <c>s</c> silver (takes more than one hit), <c>*</c> gold (can't be broken), <c>.</c> none.
/// </summary>
public sealed class RoundMap
{
    public required string Name { get; init; }
    public BrickKind?[,] Bricks { get; } = new BrickKind?[Field.Columns, Field.Rows];
    /// <summary>The last round: the Monolith, and no bricks.</summary>
    public bool Boss { get; init; }

    public static BrickKind? KindOf(char c) => c switch
    {
        'w' => BrickKind.White, 'o' => BrickKind.Orange, 'c' => BrickKind.Cyan, 'g' => BrickKind.Green,
        'r' => BrickKind.Red, 'b' => BrickKind.Blue, 'p' => BrickKind.Pink, 'y' => BrickKind.Yellow,
        's' => BrickKind.Silver, '*' => BrickKind.Gold, _ => null,
    };

    public static RoundMap Parse(string name, IReadOnlyList<string> rows)
    {
        var m = new RoundMap { Name = name };
        for (int y = 0; y < rows.Count && y < Field.Rows; y++)
            for (int x = 0; x < rows[y].Length && x < Field.Columns; x++)
                m.Bricks[x, y] = KindOf(rows[y][x]);
        return m;
    }

    public int Breakable
    {
        get
        {
            int n = 0;
            foreach (var b in Bricks) if (b is { } k && k != BrickKind.Gold) n++;
            return n;
        }
    }
}

/// <summary>
/// The game's own rounds, designed for it (none is copied from the arcade's). Every brick that can be
/// broken can be reached: the gold never walls one in.
/// </summary>
public static class Rounds
{
    public static readonly RoundMap[] All =
    [
        RoundMap.Parse("Rainbow",
        [
            ".............",
            "sssssssssssss",
            "rrrrrrrrrrrrr",
            "ooooooooooooo",
            "yyyyyyyyyyyyy",
            "ggggggggggggg",
            "ccccccccccccc",
            "bbbbbbbbbbbbb",
        ]),
        RoundMap.Parse("Diamond",
        [
            "......y......",
            ".....yry.....",
            "....yrory....",
            "...yrogory...",
            "..yrogcgory..",
            ".yrogcbcgory.",
            "..yrogcgory..",
            "...yrogory...",
            "....yrory....",
            ".....yry.....",
            "......y......",
        ]),
        RoundMap.Parse("Gates",
        [
            "ccccccccccccc",
            ".............",
            "*********....",
            ".............",
            "bbbbbbbbbbbbb",
            ".............",
            "....*********",
            ".............",
            "ppppppppppppp",
            ".............",
            "ggggggggggggg",
        ]),
        RoundMap.Parse("Pillars",
        [
            "s.s.s.s.s.s.s",
            "r.o.y.g.c.b.p",
            "r.o.y.g.c.b.p",
            "r.o.y.g.c.b.p",
            "r.o.y.g.c.b.p",
            "r.o.y.g.c.b.p",
            "r.o.y.g.c.b.p",
            "s.s.s.s.s.s.s",
        ]),
        RoundMap.Parse("Invader",
        [
            "...g.....g...",
            "....g...g....",
            "...ggggggg...",
            "..gg.ggg.gg..",
            ".ggggggggggg.",
            ".g.ggggggg.g.",
            ".g.g.....g.g.",
            "....gg.gg....",
            ".............",
            "wwwwwwwwwwwww",
        ]),
        RoundMap.Parse("Checker",
        [
            "r.r.r.r.r.r.r",
            ".o.o.o.o.o.o.",
            "y.y.y.y.y.y.y",
            ".g.g.g.g.g.g.",
            "c.c.c.c.c.c.c",
            ".b.b.b.b.b.b.",
            "p.p.p.p.p.p.p",
            ".s.s.s.s.s.s.",
            "w.w.w.w.w.w.w",
        ]),
        RoundMap.Parse("Fortress",
        [
            ".............",
            ".***********.",
            ".*ssssssss.*.",
            ".*yyyyyyyy.*.",
            ".*rrrrrrrr.*.",
            ".*ooooooo..*.",
            ".*.........*.",
            ".*****.*****.",
            ".............",
            "bbbbbbbbbbbbb",
        ]),
        RoundMap.Parse("Twin Hearts",
        [
            ".rr.rr.pp.pp.",
            "rrrrrrrpppppp",
            "rrrrrrrpppppp",
            ".rrrrr..pppp.",
            "..rrr....pp..",
            "...r......p..",
            ".............",
            "ssss.....ssss",
        ]),
        RoundMap.Parse("Waves",
        [
            "bb.......bb..",
            "..bb...bb..bb",
            "....bbb......",
            "cc.......cc..",
            "..cc...cc..cc",
            "....ccc......",
            "gg.......gg..",
            "..gg...gg..gg",
            "....ggg......",
            "*...........*",
        ]),
        RoundMap.Parse("Rings",
        [
            "...sssssss...",
            "..s.......s..",
            ".s.yyyyyyy.s.",
            ".s.y.....y.s.",
            ".s.y.rrr.y.s.",
            ".s.y.....y.s.",
            ".s.yyyyyyy.s.",
            "..s.......s..",
            "...sssssss...",
        ]),
        RoundMap.Parse("Arrow",
        [
            "......w......",
            ".....www.....",
            "....wwwww....",
            "...wwwwwww...",
            "..wwwwwwwww..",
            ".....ooo.....",
            ".....ooo.....",
            ".....ooo.....",
            ".....ooo.....",
            "**.........**",
        ]),
        RoundMap.Parse("Vault",
        [
            "sssssssssssss",
            "s***********s",
            "s*ppppppppp*s",
            "s*pyyyyyyyp*s",
            "s*pyrrrrryp*s",
            "s*pyyyyyyyp*s",
            "s*ppppppppp*s",
            "s*****.*****s",
            "sssssssssssss",
        ]),
        new RoundMap { Name = "The Monolith", Boss = true },
    ];

    /// <summary>Rounds with bricks, before the Monolith.</summary>
    public static int BrickRounds => All.Length - 1;
}
