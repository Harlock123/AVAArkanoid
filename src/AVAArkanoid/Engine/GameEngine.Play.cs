using AVAArkanoid.Models;

namespace AVAArkanoid.Engine;

public sealed partial class GameEngine
{
    public const double PaddleSpeed = 210;
    public const double AimSpeed = 520;
    public const double MaxSpeed = 260;
    public const double CapsuleSpeed = 52;
    public const double BeamSpeed = 320;
    public const double ReadySeconds = 2.0;
    public const double DyingSeconds = 2.0;
    public const double ClearSeconds = 2.6;
    public const double AutoLaunch = 3.5;
    public const int CapsulePoints = 1000;
    public const int DrifterPoints = 100;
    public const int BreakPoints = 10000;
    public const int MonolithPoints = 30000;

    /// <summary>Which round (0-based), and how many times round all of them.</summary>
    public int RoundIndex { get; private set; }
    public int Lap { get; private set; }
    public int RoundNumber => Lap * Rounds.All.Length + RoundIndex + 1;
    public RoundMap Round => Rounds.All[RoundIndex];
    public Brick?[,] Bricks { get; } = new Brick?[Field.Columns, Field.Rows];
    public Paddle Paddle { get; } = new();
    public List<Ball> Balls { get; } = [];
    public List<Capsule> Capsules { get; } = [];
    public List<Beam> Beams { get; } = [];
    public List<Drifter> Drifters { get; } = [];
    public List<Fireball> Fireballs { get; } = [];
    public Monolith? Monolith { get; private set; }
    /// <summary>The way out at the side is open (a Break capsule).</summary>
    public bool WarpOpen { get; private set; }
    /// <summary>A hatch in the top opening, for the drawing: which one, and how open (0 to 1).</summary>
    public (int Index, double Open) Hatch { get; private set; } = (-1, 0);
    public string? LastLoss { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _drifterClock = 6, _beamClock, _hatchTime;
    private bool _firePrev;
    private bool _calm;
    /// <summary>The round had bricks to break (so breaking them all clears it).</summary>
    private bool _hadBricks;

    /// <summary>How fast a ball starts: quicker each round and each time round.</summary>
    public double BaseSpeed => Math.Min(185, 128 + 3 * RoundIndex + 18 * Lap);

    public static int Points(BrickKind k) => k switch
    {
        BrickKind.White => 50, BrickKind.Orange => 60, BrickKind.Cyan => 70, BrickKind.Green => 80,
        BrickKind.Red => 90, BrickKind.Blue => 100, BrickKind.Pink => 110, BrickKind.Yellow => 120, _ => 0,
    };

    public int SilverPoints => 50 * RoundNumber;
    public int SilverHits => 2 + RoundNumber / 8;

    public int BricksLeft
    {
        get
        {
            int n = 0;
            foreach (var b in Bricks) if (b is { Kind: not BrickKind.Gold }) n++;
            return n;
        }
    }

    // ----- rounds ---------------------------------------------------------------------------------

    private void StartRound(int index)
    {
        RoundIndex = index;
        var map = Round;
        for (int x = 0; x < Field.Columns; x++)
            for (int y = 0; y < Field.Rows; y++)
                Bricks[x, y] = map.Bricks[x, y] is { } k && !_calm ? new Brick { Kind = k, Hits = k == BrickKind.Silver ? SilverHits : 1 } : null;
        _hadBricks = BricksLeft > 0;
        Monolith = map.Boss ? new Monolith { Hits = 16 + 4 * Lap, MaxHits = 16 + 4 * Lap } : null;
        WarpOpen = false;
        NewLife();
    }

    /// <summary>A fresh paddle and a ball on it; the bricks stay as they are.</summary>
    private void NewLife()
    {
        Paddle.X = Field.Width / 2.0;
        Paddle.Width = Paddle.Normal;
        Paddle.Power = Power.None;
        Balls.Clear();
        Balls.Add(new Ball { Stuck = true, StuckOffset = 6, Speed = BaseSpeed });
        PlaceStuck(Balls[0]);
        Capsules.Clear();
        Beams.Clear();
        Drifters.Clear();
        Fireballs.Clear();
        _drifterClock = 6;
        SetPhase(Phase.Ready);
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        bool firePressed = c.Fire && !_firePrev;
        _firePrev = c.Fire;
        switch (Phase)
        {
            case Phase.Ready:
                MovePaddle(dt, c);
                foreach (var b in Balls) PlaceStuck(b);
                if (PhaseTime >= ReadySeconds) SetPhase(Phase.Playing);
                return;
            case Phase.Dying:
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) { GameOver(); return; }
                NewLife();
                return;
            case Phase.RoundClear:
                if (PhaseTime < ClearSeconds) return;
                if (RoundIndex + 1 < Rounds.All.Length) StartRound(RoundIndex + 1);
                else { Lap++; StartRound(0); }
                return;
        }

        MovePaddle(dt, c);
        if (Phase != Phase.Playing) return;
        Fire(dt, c, firePressed);
        foreach (var b in Balls.ToList()) MoveBall(b, dt);
        if (Phase != Phase.Playing) return;
        Balls.RemoveAll(b => b.Lost);
        if (Balls.Count == 0) { LoseLife("lost the ball"); return; }
        MoveCapsules(dt);
        MoveBeams(dt);
        MoveDrifters(dt);
        MoveMonolith(dt);
        foreach (var br in Bricks) if (br is { Shine: > 0 }) br.Shine -= dt;
        if (Phase == Phase.Playing && _hadBricks && BricksLeft == 0) ClearRound();
    }

    private void ClearRound()
    {
        Balls.Clear();
        Capsules.Clear();
        Fireballs.Clear();
        Play(Sfx.RoundClear);
        SetPhase(Phase.RoundClear);
    }

    private void LoseLife(string why)
    {
        LastLoss = why;
        LossLog.Add($"round {RoundNumber} ({BricksLeft} bricks left): {why}");
        Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = new Vec(Paddle.X, Field.PaddleY + 3), Life = 1.4 });
        Balls.Clear();
        Play(Sfx.Lost);
        SetPhase(Phase.Dying);
    }

    // ----- the paddle -----------------------------------------------------------------------------

    private void MovePaddle(double dt, Controls c)
    {
        var p = Paddle;
        // Growing or shrinking to its size.
        double want = p.Power == Power.Enlarge ? Paddle.Long : Paddle.Normal;
        p.Width += Math.Clamp(want - p.Width, -60 * dt, 60 * dt);
        double x = p.X;
        if (c.AimX is { } aim) x += Math.Clamp(aim - x, -AimSpeed * dt, AimSpeed * dt);
        else x += Math.Clamp(c.Dx, -1, 1) * PaddleSpeed * dt;
        double max = Field.Right - p.Width / 2;
        // Through the way out at the side, if it's open.
        if (WarpOpen && x > max)
        {
            if (x - p.Width / 2 > Field.Right - 4)
            {
                AddScore(BreakPoints);
                Popup(new Vec(Field.Right - 20, Field.PaddleY - 10), BreakPoints.ToString(), 2);
                Play(Sfx.Warp);
                ClearRound();
                return;
            }
        }
        else x = Math.Min(x, max);
        p.X = Math.Max(Field.Left + p.Width / 2, x);
    }

    private void PlaceStuck(Ball b)
    {
        if (!b.Stuck) return;
        b.Pos = new Vec(Paddle.X + b.StuckOffset, Field.PaddleY - Field.BallRadius - 0.5);
    }

    private void Fire(double dt, Controls c, bool pressed)
    {
        foreach (var b in Balls)
        {
            if (!b.Stuck) continue;
            b.StuckTime += dt;
            PlaceStuck(b);
            if (pressed || b.StuckTime > AutoLaunch) Launch(b);
        }
        _beamClock -= dt;
        if (Paddle.Power == Power.Laser && c.Fire && _beamClock <= 0 && Beams.Count < 4)
        {
            _beamClock = 0.28;
            Beams.Add(new Beam { Pos = new Vec(Paddle.Left + 3, Field.PaddleY - 2) });
            Beams.Add(new Beam { Pos = new Vec(Paddle.Right - 3, Field.PaddleY - 2) });
            Play(Sfx.Laser);
        }
    }

    /// <summary>Off the paddle at the angle its place on the paddle gives.</summary>
    private void Launch(Ball b)
    {
        b.Stuck = false;
        b.StuckTime = 0;
        Bounce(b, Math.Clamp(b.StuckOffset / (Paddle.Width / 2), -1, 1));
        Play(Sfx.Paddle);
    }

    /// <summary>Up off the paddle: straight up from its middle, angled more towards its ends.</summary>
    private static void Bounce(Ball b, double along)
    {
        double a = along * 60 * Math.PI / 180;
        b.Vel = new Vec(Math.Sin(a), -Math.Cos(a)) * b.Speed;
    }

    // ----- the ball -------------------------------------------------------------------------------

    /// <summary>
    /// Moves a ball in small steps, across then down, so it can't pass through a brick: each wall or
    /// brick it runs into turns it back along that axis.
    /// </summary>
    private void MoveBall(Ball b, double dt)
    {
        if (b.Stuck) return;
        double move = b.Speed * dt;
        int steps = Math.Max(1, (int)Math.Ceiling(move / 2));
        double h = dt / steps;
        const double r = Field.BallRadius;
        for (int i = 0; i < steps; i++)
        {
            var v = b.Vel;
            // Across.
            var p = b.Pos with { X = b.Pos.X + v.X * h };
            if (p.X - r < Field.Left) { p = p with { X = Field.Left + r }; v = v with { X = Math.Abs(v.X) }; Play(Sfx.Wall); }
            else if (p.X + r > Field.Right) { p = p with { X = Field.Right - r }; v = v with { X = -Math.Abs(v.X) }; Play(Sfx.Wall); }
            else if (HitBrickAt(p) || HitMonolith(p, horizontal: true))
            {
                p = b.Pos;
                v = v with { X = -v.X };
            }
            b.Pos = p;
            // Down (or up).
            p = b.Pos with { Y = b.Pos.Y + v.Y * h };
            if (p.Y - r < Field.Top)
            {
                p = p with { Y = Field.Top + r };
                v = v with { Y = Math.Abs(v.Y) };
                // Hitting the top speeds the ball up, as on the arcade.
                b.Speed = Math.Min(MaxSpeed, Math.Max(b.Speed, BaseSpeed + 45));
                Play(Sfx.Wall);
            }
            else if (HitBrickAt(p) || HitMonolith(p, horizontal: false))
            {
                p = b.Pos;
                v = v with { Y = -v.Y };
            }
            b.Pos = p;
            b.Vel = v.Normal * b.Speed;
            KeepSteep(b);

            // The paddle.
            if (b.Vel.Y > 0 && b.Pos.Y + r >= Field.PaddleY && b.Pos.Y + r <= Field.PaddleY + 5
                && b.Pos.X >= Paddle.Left - r && b.Pos.X <= Paddle.Right + r)
            {
                double along = Math.Clamp((b.Pos.X - Paddle.X) / (Paddle.Width / 2), -1, 1);
                b.Speed = Math.Min(MaxSpeed, b.Speed + 1.2);
                if (Paddle.Power == Power.Catch)
                {
                    b.Stuck = true;
                    b.StuckOffset = b.Pos.X - Paddle.X;
                    b.StuckTime = 0;
                    PlaceStuck(b);
                    Play(Sfx.Paddle);
                    return;
                }
                Bounce(b, along);
                b.Pos = b.Pos with { Y = Field.PaddleY - r };
                Play(Sfx.Paddle);
            }
            // Drifters break on it.
            foreach (var d in Drifters)
            {
                if (d.Dead || (d.Pos - b.Pos).Length > r + 6) continue;
                BreakDrifter(d);
                b.Vel = b.Vel with { Y = -b.Vel.Y };
            }
            if (b.Pos.Y > Field.Height + 8) { b.Lost = true; return; }
        }
    }

    /// <summary>Never too flat a path, or it would go back and forth for ever.</summary>
    private static void KeepSteep(Ball b)
    {
        double min = 0.3 * b.Speed;
        if (Math.Abs(b.Vel.Y) >= min) return;
        double vy = b.Vel.Y >= 0 ? min : -min;
        double vx = Math.Sqrt(Math.Max(0, b.Speed * b.Speed - vy * vy)) * (b.Vel.X >= 0 ? 1 : -1);
        b.Vel = new Vec(vx, vy);
    }

    /// <summary>The ball at p overlaps a brick: it's hit (the first one found) and the ball turns back.</summary>
    private bool HitBrickAt(Vec p)
    {
        const double r = Field.BallRadius;
        int c0 = (int)Math.Floor((p.X - r - Field.Left) / Field.BrickW), c1 = (int)Math.Floor((p.X + r - Field.Left) / Field.BrickW);
        int r0 = (int)Math.Floor((p.Y - r - Field.BricksTop) / Field.BrickH), r1 = (int)Math.Floor((p.Y + r - Field.BricksTop) / Field.BrickH);
        for (int row = Math.Max(0, r0); row <= Math.Min(Field.Rows - 1, r1); row++)
            for (int col = Math.Max(0, c0); col <= Math.Min(Field.Columns - 1, c1); col++)
                if (Bricks[col, row] is not null)
                {
                    HitBrick(col, row);
                    return true;
                }
        return false;
    }

    private void HitBrick(int col, int row)
    {
        var br = Bricks[col, row]!;
        var at = new Vec(Field.BrickX(col) + Field.BrickW / 2, Field.BrickY(row) + Field.BrickH / 2);
        foreach (var ball in Balls) ball.Speed = Math.Min(MaxSpeed, ball.Speed + 0.6);
        if (br.Kind == BrickKind.Gold)
        {
            br.Shine = 0.4;
            Play(Sfx.Gold);
            return;
        }
        br.Hits--;
        if (br.Hits > 0)
        {
            br.Shine = 0.4;
            Play(Sfx.Metal);
            return;
        }
        Bricks[col, row] = null;
        AddScore(br.Kind == BrickKind.Silver ? SilverPoints : Points(br.Kind));
        Effects.Add(new Effect { Kind = EffectKind.Shatter, Pos = at, Life = 0.35, Colour = (uint)br.Kind });
        Play(br.Kind == BrickKind.Silver ? Sfx.Metal : Sfx.Brick);
        if (br.Kind != BrickKind.Silver) MaybeCapsule(at);
    }

    /// <summary>Now and then a broken brick lets a capsule fall, one at a time, and none while there are several balls.</summary>
    private void MaybeCapsule(Vec at)
    {
        if (Capsules.Count > 0 || Balls.Count > 1 || _calm || _rng.NextDouble() > 1 / 6.0) return;
        double roll = _rng.NextDouble() * 100;
        var kind = roll < 19 ? CapsuleKind.Slow
            : roll < 36 ? CapsuleKind.Catch
            : roll < 54 ? CapsuleKind.Laser
            : roll < 72 ? CapsuleKind.Enlarge
            : roll < 90 ? CapsuleKind.Disrupt
            : roll < 95 ? CapsuleKind.Break
            : CapsuleKind.Player;
        Capsules.Add(new Capsule { Kind = kind, Pos = at });
    }

    // ----- capsules, beams, drifters ----------------------------------------------------------------

    private void MoveCapsules(double dt)
    {
        foreach (var c in Capsules.ToList())
        {
            c.Age += dt;
            c.Pos += new Vec(0, CapsuleSpeed * dt);
            if (c.Pos.Y > Field.Height + 8) { Capsules.Remove(c); continue; }
            if (c.Pos.Y + 3.5 >= Field.PaddleY && c.Pos.Y - 3.5 <= Field.PaddleY + 6 && c.Pos.X + 8 >= Paddle.Left && c.Pos.X - 8 <= Paddle.Right)
            {
                Capsules.Remove(c);
                Collect(c.Kind);
            }
        }
    }

    private void Collect(CapsuleKind kind)
    {
        AddScore(CapsulePoints);
        Play(Sfx.Capsule);
        // A new power takes over from the old one; a held ball is let go.
        if (Paddle.Power == Power.Catch && kind != CapsuleKind.Catch)
            foreach (var b in Balls.Where(b => b.Stuck)) Launch(b);
        switch (kind)
        {
            case CapsuleKind.Slow:
                foreach (var b in Balls) { b.Speed = BaseSpeed * 0.85; b.Vel = b.Vel.Normal * b.Speed; }
                break;
            case CapsuleKind.Catch: Paddle.Power = Power.Catch; break;
            case CapsuleKind.Laser: Paddle.Power = Power.Laser; break;
            case CapsuleKind.Enlarge:
                Paddle.Power = Power.Enlarge;
                Play(Sfx.Enlarge);
                break;
            case CapsuleKind.Disrupt:
            {
                var b = Balls.FirstOrDefault(x => !x.Stuck) ?? Balls[0];
                if (b.Stuck) Launch(b);
                double a = Math.Atan2(b.Vel.Y, b.Vel.X);
                foreach (double turn in new[] { -0.35, 0.35 })
                {
                    var nb = new Ball { Pos = b.Pos, Speed = b.Speed, Vel = new Vec(Math.Cos(a + turn), Math.Sin(a + turn)) * b.Speed };
                    KeepSteep(nb);
                    Balls.Add(nb);
                }
                if (Paddle.Power == Power.Catch) Paddle.Power = Power.None;
                Play(Sfx.Split);
                break;
            }
            case CapsuleKind.Break:
                WarpOpen = true;
                Play(Sfx.Warp);
                break;
            case CapsuleKind.Player:
                Lives++;
                Play(Sfx.ExtraLife);
                break;
        }
    }

    private void MoveBeams(double dt)
    {
        foreach (var beam in Beams)
        {
            beam.Pos += new Vec(0, -BeamSpeed * dt);
            if (beam.Pos.Y < Field.Top) { beam.Dead = true; continue; }
            int col = (int)Math.Floor((beam.Pos.X - Field.Left) / Field.BrickW), row = (int)Math.Floor((beam.Pos.Y - Field.BricksTop) / Field.BrickH);
            if (col is >= 0 and < Field.Columns && row is >= 0 and < Field.Rows && Bricks[col, row] is not null)
            {
                HitBrick(col, row);
                beam.Dead = true;
                continue;
            }
            foreach (var d in Drifters)
                if (!d.Dead && (d.Pos - beam.Pos).Length < 7) { BreakDrifter(d); beam.Dead = true; break; }
            if (Monolith is { Beaten: < 0 } m && beam.Pos.X > m.Left && beam.Pos.X < m.Right && beam.Pos.Y < m.Bottom && beam.Pos.Y > m.TopY)
            {
                beam.Dead = true;
                HurtMonolith(m);
            }
        }
        Beams.RemoveAll(b => b.Dead);
    }

    private void MoveDrifters(double dt)
    {
        if (!_calm && !Round.Boss)
        {
            _drifterClock -= dt;
            if (_drifterClock <= 0 && Drifters.Count < 3)
            {
                _drifterClock = 7 + _rng.NextDouble() * 5;
                int hatch = _rng.Next(2);
                Hatch = (hatch, 0);
                _hatchTime = 0.6;
                Drifters.Add(new Drifter { Look = RoundIndex % 4, Pos = new Vec(hatch == 0 ? 64 : 160, Field.Top + 2), Phase = _rng.NextDouble() * 6 });
            }
        }
        if (_hatchTime > 0)
        {
            _hatchTime -= dt;
            Hatch = (Hatch.Index, Math.Clamp(1 - Math.Abs(_hatchTime - 0.3) / 0.3, 0, 1));
            if (_hatchTime <= 0) Hatch = (-1, 0);
        }
        foreach (var d in Drifters)
        {
            d.Age += dt;
            // Down slowly, wandering from side to side.
            double vx = Math.Sin(d.Age * 1.3 + d.Phase) * 34;
            d.Pos = new Vec(Math.Clamp(d.Pos.X + vx * dt, Field.Left + 7, Field.Right - 7), d.Pos.Y + 18 * dt);
            if (d.Pos.Y > Field.Height + 10) d.Dead = true;
            else if (d.Pos.Y + 6 >= Field.PaddleY && d.Pos.Y - 6 <= Field.PaddleY + 6 && d.Pos.X + 6 >= Paddle.Left && d.Pos.X - 6 <= Paddle.Right)
                BreakDrifter(d);
        }
        Drifters.RemoveAll(d => d.Dead);
    }

    private void BreakDrifter(Drifter d)
    {
        if (d.Dead) return;
        d.Dead = true;
        AddScore(DrifterPoints);
        Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = d.Pos, Life = 0.5 });
        Play(Sfx.Drifter);
    }

    // ----- the Monolith ---------------------------------------------------------------------------

    private bool HitMonolith(Vec p, bool horizontal)
    {
        if (Monolith is not { Beaten: < 0 } m) return false;
        const double r = Field.BallRadius;
        if (p.X + r < m.Left || p.X - r > m.Right || p.Y + r < m.TopY || p.Y - r > m.Bottom) return false;
        HurtMonolith(m);
        return true;
    }

    private void HurtMonolith(Monolith m)
    {
        if (m.Hurt > 0.15) return;
        m.Hits--;
        m.Hurt = 0.4;
        Play(Sfx.MonolithHit);
        if (m.Hits > 0) return;
        m.Beaten = 0;
        AddScore(MonolithPoints);
        Popup(m.Centre, MonolithPoints.ToString(), 3);
        Fireballs.Clear();
        Balls.Clear();
        Balls.Add(new Ball { Stuck = true, StuckOffset = 6, Speed = BaseSpeed });
        Play(Sfx.MonolithDown);
    }

    private void MoveMonolith(double dt)
    {
        if (Monolith is not { } m) return;
        if (m.Hurt > 0) m.Hurt -= dt;
        if (m.Beaten >= 0)
        {
            m.Beaten += dt;
            foreach (var b in Balls) PlaceStuck(b);
            if (m.Beaten > 3.5) { Monolith = null; ClearRound(); }
            return;
        }
        // It spits fire at the paddle.
        m.Reload -= dt;
        if (m.Reload <= 0 && Fireballs.Count < 2)
        {
            m.Reload = Math.Max(1.3, 2.6 - 0.3 * Lap) * (0.7 + _rng.NextDouble() * 0.6);
            var from = new Vec(m.Centre.X, m.Bottom - 10);
            var to = new Vec(Paddle.X + (_rng.NextDouble() - 0.5) * 50, Field.PaddleY);
            Fireballs.Add(new Fireball { Pos = from, Vel = (to - from).Normal * (70 + 10 * Lap) });
            Play(Sfx.MonolithFire);
        }
        foreach (var f in Fireballs)
        {
            f.Pos += f.Vel * dt;
            if (f.Pos.Y > Field.Height + 8) f.Dead = true;
            else if (f.Pos.Y + 3 >= Field.PaddleY && f.Pos.Y - 3 <= Field.PaddleY + 6 && f.Pos.X + 3 >= Paddle.Left && f.Pos.X - 3 <= Paddle.Right)
            {
                LoseLife("burnt by the Monolith");
                return;
            }
        }
        Fireballs.RemoveAll(f => f.Dead);
    }

    // ----- for the tests and screenshots ----------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Tick(default);
    }

    internal void Go()
    {
        if (Phase == Phase.Ready) SetPhase(Phase.Playing);
    }

    internal void JumpTo(int index)
    {
        StartRound(index);
        Go();
    }

    /// <summary>No bricks unless put there, no capsules, no drifters.</summary>
    internal void Calm()
    {
        _calm = true;
        _hadBricks = false;
        for (int x = 0; x < Field.Columns; x++)
            for (int y = 0; y < Field.Rows; y++) Bricks[x, y] = null;
    }

    internal void PutBrick(int col, int row, BrickKind kind)
    {
        Bricks[col, row] = new Brick { Kind = kind, Hits = kind == BrickKind.Silver ? SilverHits : 1 };
        if (kind != BrickKind.Gold) _hadBricks = true;
    }

    /// <summary>A ball in flight from here at this velocity (the only one).</summary>
    internal Ball PutBall(Vec at, Vec vel)
    {
        Balls.Clear();
        var b = new Ball { Pos = at, Speed = vel.Length, Vel = vel };
        Balls.Add(b);
        return b;
    }

    internal Capsule PutCapsule(CapsuleKind kind, Vec at)
    {
        var c = new Capsule { Kind = kind, Pos = at };
        Capsules.Add(c);
        return c;
    }

    internal Drifter PutDrifter(Vec at)
    {
        var d = new Drifter { Look = 0, Pos = at };
        Drifters.Add(d);
        return d;
    }

    internal void Give(CapsuleKind kind) => Collect(kind);
}
