using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AVAArkanoid.Engine;
using AVAArkanoid.Models;

namespace AVAArkanoid.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), IPen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAArkanoid/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static IPen Pen(uint argb, double thickness)
    {
        if (!PenCache.TryGetValue((argb, thickness), out var p))
        {
            p = new ImmutablePen(argb, thickness);
            PenCache[(argb, thickness)] = p;
        }
        return p;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Orange = Brush(0xFFFF8A1E);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    /// <summary>Each brick colour: face, light edge, dark edge.</summary>
    public static (uint Face, uint Light, uint Dark) BrickColours(BrickKind k) => k switch
    {
        BrickKind.White => (0xFFE8E8EC, 0xFFFFFFFF, 0xFF9A9AA6),
        BrickKind.Orange => (0xFFFF8A1E, 0xFFFFC080, 0xFFA04E08),
        BrickKind.Cyan => (0xFF3CC8F0, 0xFFA0EEFF, 0xFF16708A),
        BrickKind.Green => (0xFF3CC84A, 0xFF9AF0A0, 0xFF16702A),
        BrickKind.Red => (0xFFE0382E, 0xFFFF9A90, 0xFF821810),
        BrickKind.Blue => (0xFF3A62E0, 0xFF9AB4FF, 0xFF162E82),
        BrickKind.Pink => (0xFFE84AC0, 0xFFFFA8E8, 0xFF821A66),
        BrickKind.Yellow => (0xFFF0D23A, 0xFFFFF4A0, 0xFF8A7210),
        BrickKind.Silver => (0xFFA8ACB8, 0xFFE8ECF4, 0xFF5A5E6A),
        _ => (0xFFD4A020, 0xFFFFE890, 0xFF7A5808),
    };

    /// <summary>Each round's background: two shades for its pattern.</summary>
    private static readonly (uint A, uint B)[] Backgrounds =
    [
        (0xFF0A1440, 0xFF10205C), (0xFF0A3020, 0xFF124430), (0xFF300A30, 0xFF441444), (0xFF0A2C40, 0xFF103E5A),
    ];

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private static void Polygon(DrawingContext dc, uint argb, params Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true);
            foreach (var pt in pts.Skip(1)) ctx.LineTo(pt);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Brush(argb), null, geo);
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawBackground(dc, g.State is GameState.Title or GameState.HighScores ? 0 : g.RoundIndex);
            DrawWalls(dc);
            switch (g.State)
            {
                case GameState.Title: DrawTitle(dc); break;
                case GameState.HighScores: DrawHighScores(dc); break;
                default: DrawPlay(dc); break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the playfield --------------------------------------------------------------------------

    /// <summary>A pattern of raised hexagons, in the round's colours.</summary>
    private static void DrawBackground(DrawingContext dc, int round)
    {
        var (a, b) = Backgrounds[round % Backgrounds.Length];
        Fill(dc, a, 0, 0, Field.Width, Field.Height);
        for (int row = 0; row < 18; row++)
            for (int col = 0; col < 9; col++)
            {
                double cx = 8 + col * 26 + (row % 2) * 13, cy = 16 + row * 15;
                var c = P(cx, cy);
                double s = Scale;
                Polygon(dc, b, c + new Point(-10 * s, 0), c + new Point(-5 * s, -7 * s), c + new Point(5 * s, -7 * s),
                    c + new Point(10 * s, 0), c + new Point(5 * s, 7 * s), c + new Point(-5 * s, 7 * s));
            }
    }

    /// <summary>Pipes down the sides and across the top, with two hatches in the top.</summary>
    private void DrawWalls(DrawingContext dc)
    {
        void Pipe(double x, double y, double w, double h, bool across)
        {
            Fill(dc, 0xFF6A6E7A, x, y, w, h);
            if (across) { Fill(dc, 0xFFC8CCD8, x, y + 1, w, 2); Fill(dc, 0xFF3A3E4A, x, y + h - 2, w, 2); }
            else { Fill(dc, 0xFFC8CCD8, x + 1, y, 2, h); Fill(dc, 0xFF3A3E4A, x + w - 2, y, 2, h); }
        }
        Pipe(0, 8, Field.Left, Field.Height - 8, false);
        Pipe(Field.Right, 8, Field.Width - Field.Right, Field.Height - 8, false);
        Pipe(0, 8, Field.Width, Field.Top - 8, true);
        // Joints along the pipes.
        for (double y = 40; y < Field.Height; y += 48)
        {
            Fill(dc, 0xFF9CA0AE, 0, y, Field.Left, 4);
            Fill(dc, 0xFF9CA0AE, Field.Right, y, Field.Width - Field.Right, 4);
        }
        var (index, open) = Engine.Hatch;
        for (int i = 0; i < 2; i++)
        {
            double hx = i == 0 ? 64 : 160;
            Fill(dc, 0xFF2A2E3A, hx - 12, 8, 24, 8);
            double o = i == index ? open : 0;
            Fill(dc, 0xFF8A8E9A, hx - 12, 9, 12 * (1 - o), 6);
            Fill(dc, 0xFF8A8E9A, hx + 12 * o, 9, 12 * (1 - o), 6);
        }
        // The way out, when it's open: a glowing gap in the right-hand pipe.
        if (Engine.WarpOpen && Engine.State is GameState.Playing or GameState.Attract or GameState.Paused)
        {
            bool lit = (int)(_clock * 8) % 2 == 0;
            Fill(dc, 0xFF000000, Field.Right, Field.PaddleY - 12, Field.Width - Field.Right, 22);
            Fill(dc, lit ? 0xFFFFE14A : 0xFFFF8A1E, Field.Right, Field.PaddleY - 12, 2, 22);
            Text(dc, "»", P(Field.Right + 4, Field.PaddleY - 1), 30, lit ? Yellow : Orange, center: true);
        }
    }

    private void DrawBrick(DrawingContext dc, double x, double y, BrickKind kind, double shine = 0)
    {
        var (face, light, dark) = BrickColours(kind);
        Fill(dc, 0xFF000000, x, y, Field.BrickW, Field.BrickH);
        Fill(dc, face, x, y, Field.BrickW - 1, Field.BrickH - 1);
        Fill(dc, light, x, y, Field.BrickW - 1, 1);
        Fill(dc, light, x, y, 1, Field.BrickH - 1);
        Fill(dc, dark, x + 1, y + Field.BrickH - 2, Field.BrickW - 2, 1);
        Fill(dc, dark, x + Field.BrickW - 2, y + 1, 1, Field.BrickH - 2);
        // Silver and gold shimmer when struck: a band of light runs across.
        if (shine > 0)
        {
            double sx = x + (1 - shine / 0.4) * (Field.BrickW - 4);
            Fill(dc, 0xFFFFFFFF, sx, y + 1, 3, Field.BrickH - 3);
        }
    }

    private static void DrawPaddle(DrawingContext dc, double cx, double width, Power power, double y = Field.PaddleY, double scale = 1)
    {
        double s = Scale * scale;
        var at = new Point(FieldLeft + cx * Scale - width / 2 * s, FieldTop + y * Scale);
        double w = width * s, h = 6 * s;
        // Red ends, a silver body, a light line along the top.
        uint ends = power == Power.Laser ? 0xFFFFE14A : 0xFFE0382E;
        dc.DrawRectangle(Brush(0xFFB8BCC8), null, new Rect(at.X + 5 * s, at.Y, w - 10 * s, h), 1.5 * s, 1.5 * s);
        dc.FillRectangle(Brush(0xFFF0F2F8), new Rect(at.X + 5 * s, at.Y + 1 * s, w - 10 * s, 1.2 * s));
        dc.FillRectangle(Brush(0xFF6A6E7A), new Rect(at.X + 5 * s, at.Y + h - 1.5 * s, w - 10 * s, 1.5 * s));
        dc.DrawRectangle(Brush(ends), null, new Rect(at.X, at.Y, 7 * s, h), 3 * s, 3 * s);
        dc.DrawRectangle(Brush(ends), null, new Rect(at.X + w - 7 * s, at.Y, 7 * s, h), 3 * s, 3 * s);
        dc.FillRectangle(Brush(0xFF2A2E3A), new Rect(at.X + 7 * s, at.Y + 1 * s, 1 * s, h - 2 * s));
        dc.FillRectangle(Brush(0xFF2A2E3A), new Rect(at.X + w - 8 * s, at.Y + 1 * s, 1 * s, h - 2 * s));
        if (power == Power.Laser)
        {
            dc.FillRectangle(Brush(0xFF8A8E9A), new Rect(at.X + 2 * s, at.Y - 3 * s, 2 * s, 3 * s));
            dc.FillRectangle(Brush(0xFF8A8E9A), new Rect(at.X + w - 4 * s, at.Y - 3 * s, 2 * s, 3 * s));
        }
        if (power == Power.Catch)
            dc.FillRectangle(Brush(0xFF3CC84A), new Rect(at.X + w / 2 - 4 * s, at.Y + 1 * s, 8 * s, 1.5 * s));
    }

    public static (uint Colour, string Letter) CapsuleLook(CapsuleKind k) => k switch
    {
        CapsuleKind.Slow => (0xFFFF8A1E, "S"),
        CapsuleKind.Catch => (0xFF3CC84A, "C"),
        CapsuleKind.Laser => (0xFFE0382E, "L"),
        CapsuleKind.Enlarge => (0xFF3A62E0, "E"),
        CapsuleKind.Disrupt => (0xFF3CC8F0, "D"),
        CapsuleKind.Break => (0xFFE84AC0, "B"),
        _ => (0xFF9CA0AE, "P"),
    };

    private void DrawCapsule(DrawingContext dc, CapsuleKind kind, Vec at, double age)
    {
        var (colour, letter) = CapsuleLook(kind);
        var r = new Rect(P(at.X - 8, at.Y - 3.5), P(at.X + 8, at.Y + 3.5));
        dc.DrawRectangle(Brush(colour), null, r, 3.5 * Scale, 3.5 * Scale);
        dc.DrawRectangle(Brush(0x60FFFFFF), null, new Rect(r.X + 2 * Scale, r.Y + 0.8 * Scale, r.Width - 4 * Scale, 1.6 * Scale), 1 * Scale, 1 * Scale);
        // The letter rolls round as it falls.
        double roll = Math.Sin(age * 8);
        if (roll > -0.3) Text(dc, letter, new Point(r.Center.X, r.Center.Y + roll * 2), 24, White, center: true);
    }

    private void DrawDrifter(DrawingContext dc, Drifter d)
    {
        var c = P(d.Pos);
        double s = Scale, t = d.Age * 3;
        switch (d.Look)
        {
            case 0:
                // A spinning cone.
                Polygon(dc, 0xFF3CC8F0, c + new Point(0, -6 * s), c + new Point(Math.Cos(t) * 6 * s, 5 * s), c + new Point(-Math.Cos(t) * 6 * s, 5 * s));
                dc.DrawEllipse(Brush(0xFF16708A), null, c + new Point(0, 5 * s), Math.Abs(Math.Cos(t)) * 6 * s + 1, 1.5 * s);
                break;
            case 1:
                // A turning pyramid.
                Polygon(dc, 0xFF3CC84A, c + new Point(0, -6 * s), c + new Point(6 * s, 5 * s), c + new Point(Math.Sin(t) * 3 * s, 6 * s));
                Polygon(dc, 0xFF16702A, c + new Point(0, -6 * s), c + new Point(-6 * s, 5 * s), c + new Point(Math.Sin(t) * 3 * s, 6 * s));
                break;
            case 2:
                // A molecule: three balls going round.
                for (int i = 0; i < 3; i++)
                {
                    double a = t + i * 2.09;
                    dc.DrawEllipse(Brush(i == 0 ? 0xFFE0382Eu : i == 1 ? 0xFFF0D23Au : 0xFF3A62E0u), null, c + new Point(Math.Cos(a) * 3.5 * s, Math.Sin(a) * 3.5 * s), 2.6 * s, 2.6 * s);
                }
                break;
            default:
                // A tumbling cube.
                dc.FillRectangle(Brush(0xFFE84AC0), new Rect(c.X - 5 * s, c.Y - 5 * s, 10 * s, 10 * s));
                dc.FillRectangle(Brush(0xFFFFA8E8), new Rect(c.X - 5 * s, c.Y - 5 * s, 10 * s * (0.5 + 0.5 * Math.Abs(Math.Sin(t))), 3 * s));
                dc.FillRectangle(Brush(0xFF821A66), new Rect(c.X + 2 * s, c.Y - 5 * s, 3 * s, 10 * s));
                break;
        }
    }

    /// <summary>The Monolith: a great stone face. Its eyes glow; it cracks as it's hit.</summary>
    private void DrawMonolith(DrawingContext dc, Monolith m)
    {
        double crumble = m.Beaten >= 0 ? m.Beaten / 3.5 : 0;
        bool hurt = m.Hurt > 0 && (int)(_clock * 20) % 2 == 0;
        var r = new Rect(P(m.Left, m.TopY + crumble * 20), P(m.Right, m.Bottom));
        if (crumble > 0.8) return;
        dc.FillRectangle(Brush(hurt ? 0xFFFFFFFFu : 0xFF7A7468u), r);
        dc.FillRectangle(Brush(0xFF9A9486), new Rect(r.X, r.Y, r.Width, 3 * Scale));
        dc.FillRectangle(Brush(0xFF4A463E), new Rect(r.Right - 4 * Scale, r.Y, 4 * Scale, r.Height));
        // Brow, eyes, nose, mouth.
        var c = P(m.Centre.X, m.Centre.Y + crumble * 20);
        double s = Scale;
        dc.FillRectangle(Brush(0xFF4A463E), new Rect(c.X - 22 * s, c.Y - 20 * s, 44 * s, 4 * s));
        uint eye = m.Beaten >= 0 ? 0xFF2A2620u : (int)(_clock * 3) % 2 == 0 ? 0xFFFF3D3Du : 0xFFFF8A1Eu;
        dc.FillRectangle(Brush(0xFF2A2620), new Rect(c.X - 18 * s, c.Y - 14 * s, 12 * s, 7 * s));
        dc.FillRectangle(Brush(0xFF2A2620), new Rect(c.X + 6 * s, c.Y - 14 * s, 12 * s, 7 * s));
        dc.FillRectangle(Brush(eye), new Rect(c.X - 15 * s, c.Y - 12 * s, 6 * s, 3 * s));
        dc.FillRectangle(Brush(eye), new Rect(c.X + 9 * s, c.Y - 12 * s, 6 * s, 3 * s));
        Polygon(dc, 0xFF5A5448, c + new Point(0, -6 * s), c + new Point(5 * s, 6 * s), c + new Point(-5 * s, 6 * s));
        double mouth = m.Reload < 0.3 ? 6 : 2;
        dc.FillRectangle(Brush(0xFF1A1610), new Rect(c.X - 12 * s, c.Y + 12 * s, 24 * s, mouth * s));
        // Cracks, more of them the more it's been hit.
        int cracks = (m.MaxHits - m.Hits) * 6 / Math.Max(1, m.MaxHits);
        var pen = Pen(0xFF2A2620, 1.5);
        for (int i = 0; i < cracks; i++)
        {
            double x0 = r.X + (8 + i * 9) * s, y0 = r.Y + (i % 2 == 0 ? 4 : 30) * s;
            dc.DrawLine(pen, new Point(x0, y0), new Point(x0 + 4 * s, y0 + 8 * s));
            dc.DrawLine(pen, new Point(x0 + 4 * s, y0 + 8 * s), new Point(x0 + 1 * s, y0 + 16 * s));
        }
        // Its strength.
        double frac = Math.Max(0, m.Hits) / (double)m.MaxHits;
        dc.FillRectangle(Brush(0xFF2A2620), new Rect(r.X, r.Y - 6 * s, r.Width, 3 * s));
        dc.FillRectangle(Brush(0xFFFF3D3D), new Rect(r.X, r.Y - 6 * s, r.Width * frac, 3 * s));
    }

    // ----- the play -------------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        for (int col = 0; col < Field.Columns; col++)
            for (int row = 0; row < Field.Rows; row++)
                if (g.Bricks[col, row] is { } b) DrawBrick(dc, Field.BrickX(col), Field.BrickY(row), b.Kind, b.Shine);
        if (g.Monolith is { } m) DrawMonolith(dc, m);
        foreach (var d in g.Drifters) DrawDrifter(dc, d);
        foreach (var c in g.Capsules) DrawCapsule(dc, c.Kind, c.Pos, c.Age);
        foreach (var beam in g.Beams) Fill(dc, (int)(_clock * 20) % 2 == 0 ? 0xFFFFE14Au : 0xFFFFFFFFu, beam.Pos.X - 0.5, beam.Pos.Y - 4, 1.2, 6);
        foreach (var f in g.Fireballs)
        {
            dc.DrawEllipse(Brush(0xFFFF6A1A), null, P(f.Pos), 3.5 * Scale, 3.5 * Scale);
            dc.DrawEllipse(Brush(0xFFFFE14A), null, P(f.Pos), 1.8 * Scale, 1.8 * Scale);
        }
        if (g.Phase != Phase.Dying) DrawPaddle(dc, g.Paddle.X, g.Paddle.Width, g.Paddle.Power);
        foreach (var b in g.Balls)
        {
            dc.DrawEllipse(Brush(0xFFF4F4F8), null, P(b.Pos), Field.BallRadius * Scale, Field.BallRadius * Scale);
            dc.DrawEllipse(Brush(0xFF9A9AA6), null, P(b.Pos) + new Point(1.5, 1.5), 1.2 * Scale, 1.2 * Scale);
            dc.DrawEllipse(Brush(0xFFFFFFFF), null, P(b.Pos) + new Point(-2, -2), 0.8 * Scale, 0.8 * Scale);
        }

        foreach (var fx in g.Effects)
        {
            double t = fx.Age / fx.Life;
            switch (fx.Kind)
            {
                case EffectKind.Shatter:
                {
                    var (face, _, _) = BrickColours((BrickKind)fx.Colour);
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * 1.05 + 0.4;
                        Fill(dc, face, fx.Pos.X + Math.Cos(a) * t * 14 - 1.5, fx.Pos.Y + Math.Sin(a) * t * 10 + t * t * 12 - 1.5, 3, 2);
                    }
                    break;
                }
                case EffectKind.Blast:
                    for (int i = 0; i < 10; i++)
                    {
                        double a = i * Math.PI / 5 + t;
                        double r0 = 2 + t * 10, r1 = 5 + t * 22;
                        uint c = ((i + (int)(_clock * 20)) % 3) switch { 0 => 0xFFFFFFFFu, 1 => 0xFFFFE14Au, _ => 0xFFFF6A1Au };
                        dc.DrawLine(Pen(c, 2 * Scale), P(fx.Pos.X + Math.Cos(a) * r0, fx.Pos.Y + Math.Sin(a) * r0), P(fx.Pos.X + Math.Cos(a) * r1, fx.Pos.Y + Math.Sin(a) * r1));
                    }
                    break;
                case EffectKind.Score:
                    Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - 6 - fx.Age * 8), 30, Yellow, center: true);
                    break;
            }
        }

        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready)
                    Banner(dc, $"ROUND {g.RoundNumber}", g.State == GameState.Attract ? "DEMONSTRATION" : g.Round.Name.ToUpperInvariant());
                else if (g.Phase == Phase.RoundClear)
                    Banner(dc, g.Round.Boss ? "THE MONOLITH FALLS!" : "ROUND CLEAR!", g.Round.Boss ? "AND ROUND AGAIN, FASTER" : null);
                else if (g.State == GameState.Attract && (int)(_clock * 2) % 2 == 0) Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 200), 32, Red, center: true);
                break;
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xC8000000, 16, 160, Field.Width - 32, sub is null ? 24 : 36);
        Text(dc, title, P(Field.Width / 2.0, 172), 48, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 187), 26, White, center: true);
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
