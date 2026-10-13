using AVAArkanoid.Engine;
using AVAArkanoid.Models;

namespace AVAArkanoid.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arkanoid-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A round under way with no bricks unless put there, no capsules and no drifters.</summary>
    private GameEngine Calm(int round = 0, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(round);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Tick(c);
    }

    /// <summary>A brick up in the corner, out of the way, so breaking the others doesn't end the round.</summary>
    private static void Keep(GameEngine g) => g.PutBrick(Field.Columns - 1, 0, BrickKind.White);

    private static Vec BrickCentre(int col, int row) => new(Field.BrickX(col) + Field.BrickW / 2, Field.BrickY(row) + Field.BrickH / 2);

    [Fact]
    public void The_rounds_are_all_there_and_every_one_has_bricks_to_break()
    {
        Assert.Equal(13, Rounds.All.Length);
        Assert.True(Rounds.All[^1].Boss);
        foreach (var r in Rounds.All.SkipLast(1)) Assert.True(r.Breakable > 30, r.Name);
    }

    [Fact]
    public void The_ball_waits_on_the_paddle_and_is_launched_by_fire_or_by_itself()
    {
        var g = Calm();
        var b = g.Balls.Single();
        Assert.True(b.Stuck);
        Steps(g, 30, new Controls(1));
        Assert.True(b.Stuck);
        Assert.Equal(g.Paddle.X + b.StuckOffset, b.Pos.X, 3);
        g.Tick(new Controls(0, Fire: true));
        Assert.False(b.Stuck);
        Assert.True(b.Vel.Y < 0);

        g = Calm();
        Steps(g, (int)(60 * GameEngine.AutoLaunch) + 5);
        Assert.False(g.Balls[0].Stuck);
    }

    [Fact]
    public void The_paddle_stays_between_the_walls_and_follows_the_mouse()
    {
        var g = Calm();
        Steps(g, 120, new Controls(-1));
        Assert.Equal(Field.Left + Paddle.Normal / 2, g.Paddle.X, 3);
        Steps(g, 120, new Controls(1));
        Assert.Equal(Field.Right - Paddle.Normal / 2, g.Paddle.X, 3);
        Steps(g, 60, new Controls(0, AimX: 80));
        Assert.Equal(80, g.Paddle.X, 3);
    }

    [Fact]
    public void The_ball_comes_off_the_paddle_at_an_angle_set_by_where_it_lands()
    {
        foreach (double offset in new[] { 0.0, 12.0, -12.0 })
        {
            var g = Calm();
            g.Tick(default);
            var b = g.PutBall(new Vec(g.Paddle.X + offset, Field.PaddleY - 20), new Vec(0, 150));
            for (int i = 0; i < 30 && b.Vel.Y > 0; i++) g.Tick(default);
            Assert.True(b.Vel.Y < 0);
            Assert.Equal(Math.Sign(offset), Math.Sign(Math.Round(b.Vel.X, 6)));
        }
    }

    [Fact]
    public void The_walls_turn_the_ball_back_and_the_top_speeds_it_up()
    {
        var g = Calm();
        g.Tick(default);
        var b = g.PutBall(new Vec(20, 100), new Vec(-120, -60));
        Steps(g, 20);
        Assert.True(b.Vel.X > 0);
        b = g.PutBall(new Vec(112, 30), new Vec(0, -140));
        double speed = b.Speed;
        Steps(g, 10);
        Assert.True(b.Vel.Y > 0);
        Assert.True(b.Speed > speed);
        // Never too flat a path.
        b = g.PutBall(new Vec(112, 100), new Vec(200, 1));
        Steps(g, 2);
        Assert.True(Math.Abs(b.Vel.Y) >= 0.3 * b.Speed - 1e-6);
    }

    [Fact]
    public void Bricks_break_for_their_points_silver_takes_several_hits_and_gold_none()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        Keep(g);
        g.PutBrick(6, 5, BrickKind.Blue);
        var b = g.PutBall(BrickCentre(6, 5) + new Vec(0, 20), new Vec(0, -150));
        for (int i = 0; i < 30 && g.Bricks[6, 5] is not null; i++) g.Tick(default);
        Assert.Null(g.Bricks[6, 5]);
        Assert.Equal(GameEngine.Points(BrickKind.Blue), g.Score);
        Assert.True(b.Vel.Y > 0, "and it came back down");
        Assert.Contains(Sfx.Brick, heard);

        g.PutBrick(3, 5, BrickKind.Silver);
        Assert.Equal(2, g.Bricks[3, 5]!.Hits);
        g.PutBall(BrickCentre(3, 5) + new Vec(0, 20), new Vec(0, -150));
        for (int i = 0; i < 30 && g.Balls[0].Vel.Y < 0; i++) g.Tick(default);
        Assert.NotNull(g.Bricks[3, 5]);
        Assert.Contains(Sfx.Metal, heard);
        g.PutBall(BrickCentre(3, 5) + new Vec(0, 20), new Vec(0, -150));
        for (int i = 0; i < 30 && g.Balls[0].Vel.Y < 0; i++) g.Tick(default);
        Assert.Null(g.Bricks[3, 5]);
        Assert.Equal(GameEngine.Points(BrickKind.Blue) + g.SilverPoints, g.Score);

        g.PutBrick(9, 5, BrickKind.Gold);
        for (int n = 0; n < 4; n++)
        {
            g.PutBall(BrickCentre(9, 5) + new Vec(0, 20), new Vec(0, -150));
            for (int i = 0; i < 30 && g.Balls[0].Vel.Y < 0; i++) g.Tick(default);
        }
        Assert.NotNull(g.Bricks[9, 5]);
        Assert.Contains(Sfx.Gold, heard);
    }

    [Fact]
    public void A_ball_past_the_paddle_costs_a_paddle_and_the_bricks_stay()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        g.PutBrick(0, 0, BrickKind.Red);
        g.PutBall(new Vec(30, 200), new Vec(0, 160));
        Steps(g, 60, new Controls(1));
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains(Sfx.Lost, heard);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
        Assert.NotNull(g.Bricks[0, 0]);
        Assert.True(g.Balls.Single().Stuck);
    }

    [Fact]
    public void Breaking_the_last_brick_clears_the_round()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        g.PutBrick(6, 3, BrickKind.Green);
        g.PutBrick(8, 3, BrickKind.Gold);
        g.PutBall(BrickCentre(6, 3) + new Vec(0, 20), new Vec(0, -150));
        Steps(g, 30);
        Assert.Equal(Phase.RoundClear, g.Phase);
        Assert.Contains(Sfx.RoundClear, heard);
        Steps(g, (int)(60 * GameEngine.ClearSeconds) + 2);
        Assert.Equal(2, g.RoundNumber);
    }

    [Fact]
    public void Capsules_fall_and_give_their_powers_when_caught()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        g.PutCapsule(CapsuleKind.Enlarge, new Vec(g.Paddle.X, 180));
        Steps(g, 90);
        Assert.Empty(g.Capsules);
        Assert.Equal(Power.Enlarge, g.Paddle.Power);
        Assert.Equal(GameEngine.CapsulePoints, g.Score);
        Assert.Equal(Paddle.Long, g.Paddle.Width, 1);

        // Missed: it falls away.
        g.PutCapsule(CapsuleKind.Player, new Vec(30, 200));
        Steps(g, 30, new Controls(1));
        Steps(g, 90);
        Assert.Empty(g.Capsules);
        Assert.Equal(GameEngine.StartingLives, g.Lives);

        g.Give(CapsuleKind.Player);
        Assert.Equal(GameEngine.StartingLives + 1, g.Lives);
        Assert.Contains(Sfx.Capsule, heard);
    }

    [Fact]
    public void Lasers_break_bricks()
    {
        var g = Calm();
        g.Tick(default);
        g.Give(CapsuleKind.Laser);
        Keep(g);
        g.PutBall(new Vec(20, 120), new Vec(0, -150));
        double left = g.Paddle.Left + 3;
        int col = (int)((left - Field.Left) / Field.BrickW);
        g.PutBrick(col, 6, BrickKind.Pink);
        for (int i = 0; i < 60 && g.Bricks[col, 6] is not null; i++) { g.Tick(new Controls(0, Fire: true)); g.Balls[0].Pos = new Vec(20, 120); g.Balls[0].Vel = new Vec(0, -150); }
        Assert.Null(g.Bricks[col, 6]);
    }

    [Fact]
    public void Catch_holds_the_ball_until_fired()
    {
        var g = Calm();
        g.Tick(default);
        g.Give(CapsuleKind.Catch);
        var b = g.PutBall(new Vec(g.Paddle.X + 5, Field.PaddleY - 20), new Vec(0, 150));
        Steps(g, 20);
        Assert.True(b.Stuck);
        Steps(g, 20, new Controls(-1));
        Assert.Equal(g.Paddle.X + b.StuckOffset, b.Pos.X, 3);
        g.Tick(new Controls(0, Fire: true));
        Assert.False(b.Stuck);
    }

    [Fact]
    public void Disruption_makes_three_balls_and_it_takes_losing_them_all_to_lose_a_paddle()
    {
        var g = Calm();
        g.Tick(default);
        g.PutBall(new Vec(112, 120), new Vec(40, -150));
        g.Give(CapsuleKind.Disrupt);
        Assert.Equal(3, g.Balls.Count);
        g.Balls[0].Pos = new Vec(30, 250);
        g.Balls[0].Vel = new Vec(0, 150);
        Steps(g, 10);
        Assert.Equal(2, g.Balls.Count);
        Assert.Equal(Phase.Playing, g.Phase);
    }

    [Fact]
    public void Slow_calms_the_ball()
    {
        var g = Calm();
        g.Tick(default);
        var b = g.PutBall(new Vec(112, 120), new Vec(0, -240));
        g.Give(CapsuleKind.Slow);
        Assert.True(b.Speed < g.BaseSpeed);
        Assert.Equal(b.Speed, b.Vel.Length, 3);
    }

    [Fact]
    public void Break_opens_the_way_out_and_going_through_it_ends_the_round_for_10000()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        g.PutBrick(0, 0, BrickKind.Red);
        g.PutBall(new Vec(30, 60), new Vec(30, -150));
        g.Give(CapsuleKind.Break);
        Assert.True(g.WarpOpen);
        for (int i = 0; i < 120 && g.Phase == Phase.Playing; i++)
        {
            g.Tick(new Controls(1));
            if (g.Balls.Count > 0) { g.Balls[0].Pos = new Vec(30, 60); g.Balls[0].Vel = new Vec(30, -150); }
        }
        Assert.Equal(Phase.RoundClear, g.Phase);
        Assert.Equal(GameEngine.CapsulePoints + GameEngine.BreakPoints, g.Score);
        Assert.Contains(Sfx.Warp, heard);
    }

    [Fact]
    public void Drifters_come_out_of_the_hatches_and_break_on_the_ball_or_the_paddle()
    {
        var g = new GameEngine(2, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(0);
        for (int i = 0; i < 60 * 15 && g.Drifters.Count == 0; i++) { g.Tick(default); foreach (var b in g.Balls) b.Pos = b.Pos with { Y = Math.Min(b.Pos.Y, 200) }; }
        Assert.NotEmpty(g.Drifters);

        var calm = Calm();
        calm.Tick(default);
        var d = calm.PutDrifter(new Vec(100, 100));
        calm.PutBall(new Vec(100, 120), new Vec(0, -150));
        Steps(calm, 10);
        Assert.True(d.Dead);
        Assert.Equal(GameEngine.DrifterPoints, calm.Score);
        var d2 = calm.PutDrifter(new Vec(calm.Paddle.X, Field.PaddleY - 4));
        calm.Balls[0].Pos = new Vec(20, 60);
        Steps(calm, 2);
        Assert.True(d2.Dead);
    }

    [Fact]
    public void The_monolith_takes_its_hits_spits_fire_and_falls()
    {
        var g = new GameEngine(3, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.StartGame();
        g.JumpTo(Rounds.All.Length - 1);
        var m = g.Monolith!;
        Assert.Equal(16, m.Hits);
        for (int i = 0; i < 60 * 4 && g.Fireballs.Count == 0; i++) { g.Tick(default); g.Balls[0].Pos = new Vec(20, 200); g.Balls[0].Vel = new Vec(0, -150); }
        Assert.NotEmpty(g.Fireballs);
        Assert.Contains(Sfx.MonolithFire, heard);
        g.Fireballs.Clear();
        for (int n = 0; n < 40 && m.Beaten < 0; n++)
        {
            g.PutBall(new Vec(m.Centre.X, m.Bottom + 20), new Vec(0, -150));
            for (int i = 0; i < 40 && g.Balls.Count > 0 && g.Balls[0].Vel.Y < 0; i++) { g.Tick(default); g.Fireballs.Clear(); }
            // Let it get over the knock before the next one.
            for (int i = 0; i < 20 && m.Beaten < 0; i++) { g.Tick(default); g.Fireballs.Clear(); if (g.Balls.Count > 0) g.Balls[0].Pos = new Vec(20, 200); }
        }
        Assert.True(m.Beaten >= 0);
        Assert.Contains(Sfx.MonolithDown, heard);
        Assert.True(g.Score >= GameEngine.MonolithPoints);
        for (int i = 0; i < 60 * 8 && g.RoundNumber == Rounds.All.Length; i++) g.Tick(default);
        Assert.Equal(1, g.Lap);
        Assert.Equal(0, g.RoundIndex);
    }

    [Fact]
    public void Bonus_paddles_at_20000_and_every_60000_after()
    {
        var g = Calm();
        Assert.Equal(3, g.Lives);
        g.AddScore(19999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(60000);
        Assert.Equal(5, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_clears_a_round_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 60 * 6 && g.State == GameState.Playing && g.RoundNumber < 2; i++) g.Tick(pilot.Decide(g));
        Assert.True(g.RoundNumber >= 2, $"{g.BricksLeft} bricks left; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Tick(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        if (g.HighScores.Qualifies(g.Score))
        {
            Assert.Equal(GameState.EnterInitials, g.State);
            g.Initials!.Finish();
            g.Idle(2);
        }
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Tick(pilot.Decide(g));
            return $"{g.RoundNumber} {g.Score} {g.Lives} {g.BricksLeft} {g.Paddle.X:0.000} {string.Join(",", g.Balls.Select(b => b.Pos.ToString()))}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 20)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Paddle, heard);
        Assert.Contains(Sfx.Brick, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 18)));
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_holds_everything()
    {
        var g = Calm();
        g.Tick(default);
        var b = g.PutBall(new Vec(112, 120), new Vec(0, -150));
        g.Pause();
        Steps(g, 30, new Controls(1));
        Assert.Equal(new Vec(112, 120), b.Pos);
        g.Resume();
        Steps(g, 30);
        Assert.NotEqual(new Vec(112, 120), b.Pos);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        Assert.All(Enum.GetValues<Sfx>().Select(s => s.ToString()), f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }
}
