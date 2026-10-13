using Avalonia;
using Avalonia.Media;
using AVAArkanoid.Engine;
using AVAArkanoid.Models;

namespace AVAArkanoid.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: scores and spare paddles on the left; the round, the power and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "PLAYER 1", new Point(lx, 60), 30, Orange, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 160), 30, Orange, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            int spare = g.Lives - (g.Phase == Phase.Dying ? 0 : 1);
            Text(dc, "PADDLES", new Point(lx, 270), 28, Dim, center: true);
            for (int i = 0; i < Math.Min(spare, 8); i++)
            {
                double px = (lx - 48 + (i % 2) * 52 - FieldLeft) / Scale, py = (296 + i / 2 * 26 - FieldTop) / Scale;
                DrawPaddle(dc, px, Paddle.Normal, Power.None, py, 0.55);
            }
            Text(dc, "ROUND", new Point(rx, 60), 30, Orange, center: true);
            Text(dc, g.RoundNumber.ToString(), new Point(rx, 92), 34, White, center: true);
            Text(dc, g.Round.Name.ToUpperInvariant(), new Point(rx, 122), 24, Dim, center: true);
            Text(dc, "BRICKS", new Point(rx, 170), 28, Orange, center: true);
            Text(dc, g.Round.Boss ? "-" : g.BricksLeft.ToString(), new Point(rx, 200), 32, White, center: true);
            string power = g.Paddle.Power switch
            {
                Power.Catch => "CATCH", Power.Laser => "LASER", Power.Enlarge => "ENLARGED", _ => g.Balls.Count > 1 ? "DISRUPTION" : "",
            };
            if (power.Length > 0) Text(dc, power, new Point(rx, 260), 30, Cyan, center: true);
            if (g.WarpOpen && (int)(_clock * 4) % 2 == 0) Text(dc, "BREAK: EXIT RIGHT!", new Point(rx, 290), 24, Yellow, center: true);
        }
        Text(dc, "AVA ARKANOID", new Point(rx, 490), 24, Dim, center: true);
        Text(dc, "Arrows or mouse", new Point(rx, 520), 20, Dim, center: true);
        Text(dc, "Space, Z or click", new Point(rx, 540), 20, Dim, center: true);
        Text(dc, "to launch or fire", new Point(rx, 560), 20, Dim, center: true);
        Text(dc, "Enter starts", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 620), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 640), 20, Dim, center: true);
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xD8000010), new Rect(P(14, top), P(Field.Width - 14, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        // A row of bricks along the top, for the look of it.
        for (int col = 0; col < Field.Columns; col++) DrawBrick(dc, Field.BrickX(col), 24, (BrickKind)(col % 8), (int)(_clock * 2) % Field.Columns == col ? 0.2 : 0);
        Panel(dc, 38, 250);
        Text(dc, "AVA", P(cx, 50), 56, Orange, center: true);
        Text(dc, "ARKANOID", P(cx, 66), 90, Orange, center: true);
        Text(dc, "AFTER THE 1986 ARCADE CLASSIC", P(cx, 82), 26, Dim, center: true);
        Text(dc, "BREAK EVERY BRICK. DON'T LET THE BALL PAST.", P(cx, 93), 26, White, center: true);

        double y = 108;
        foreach (CapsuleKind k in Enum.GetValues<CapsuleKind>())
        {
            DrawCapsule(dc, k, new Vec(36, y), _clock);
            string what = k switch
            {
                CapsuleKind.Slow => "SLOW: CALMS THE BALL",
                CapsuleKind.Catch => "CATCH: HOLD IT, AIM, FIRE",
                CapsuleKind.Laser => "LASER: FIRE AT THE BRICKS",
                CapsuleKind.Enlarge => "ENLARGE: A LONGER PADDLE",
                CapsuleKind.Disrupt => "DISRUPTION: THREE BALLS",
                CapsuleKind.Break => "BREAK: A WAY OUT, 10000",
                _ => "PLAYER: AN EXTRA PADDLE",
            };
            Text(dc, what, P(52, y), 25, White);
            y += 11;
        }
        DrawBrick(dc, 28, y - 4, BrickKind.Silver);
        Text(dc, "SILVER TAKES SEVERAL HITS", P(52, y), 25, White);
        y += 11;
        DrawBrick(dc, 28, y - 4, BrickKind.Gold);
        Text(dc, "GOLD NEVER BREAKS", P(52, y), 25, White);
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 212), 30, Yellow, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 224), 22, Dim, center: true);
        DrawPaddle(dc, cx + Math.Sin(_clock * 1.5) * 60, Paddle.Normal, Power.None);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 22, 236);
        Text(dc, "TOP PLAYERS", P(cx, 40), 60, Orange, center: true);
        Text(dc, "RANK    SCORE   NAME  ROUND", P(cx, 58), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Orange : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {e.Stage,3}", P(cx, 74 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 222), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
