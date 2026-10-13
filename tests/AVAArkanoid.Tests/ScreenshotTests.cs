using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAArkanoid.Engine;
using AVAArkanoid.Models;
using AVAArkanoid.Views;
using Controls = AVAArkanoid.Engine.Controls;

namespace AVAArkanoid.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set ARKANOID_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arkanoid-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("ARKANOID_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAArkanoid-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_round_under_way()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.JumpTo(1);
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++)
        {
            g.Tick(pilot.Decide(g));
            if (i > 60 * 15 && g.Phase == Phase.Playing && g.Capsules.Count > 0 && g.Balls.Any(b => b.Pos.Y < 150)) break;
        }
        Shot(w, c, "play");
        w.Close();
    }

    [AvaloniaFact]
    public void Lasers_and_three_balls()
    {
        var (w, c, g) = Open(seed: 3);
        g.StartGame();
        g.JumpTo(6);
        g.Give(CapsuleKind.Laser);
        g.Tick(new Controls(0, Fire: true));
        g.PutBall(new Vec(150, 170), new Vec(60, -150));
        g.Give(CapsuleKind.Disrupt);
        for (int i = 0; i < 40; i++) g.Tick(new Controls(-0.3, Fire: true));
        g.PutDrifter(new Vec(60, 150));
        g.PutCapsule(CapsuleKind.Catch, new Vec(170, 200));
        g.Idle(2);
        Shot(w, c, "lasers");
        w.Close();
    }

    [AvaloniaFact]
    public void The_way_out()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        g.JumpTo(9);
        for (int x = 0; x < Field.Columns; x++)
            for (int y = 4; y < Field.Rows; y++) g.Bricks[x, y] = null;
        g.Give(CapsuleKind.Break);
        g.PutBall(new Vec(90, 140), new Vec(70, 140));
        for (int i = 0; i < 24; i++) g.Tick(new Controls(1));
        Shot(w, c, "break");
        w.Close();
    }

    [AvaloniaFact]
    public void The_monolith()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpTo(Rounds.All.Length - 1);
        g.Monolith!.Hits = 7;
        g.PutBall(new Vec(80, 200), new Vec(-60, -140));
        for (int i = 0; i < 70; i++) { g.Tick(new Controls(0, AimX: 140)); if (i == 40) g.Fireballs.Clear(); }
        Shot(w, c, "monolith");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.Go();
        while (g.State == GameState.Playing) g.Tick(new Controls(1, Fire: true));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(5); }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
