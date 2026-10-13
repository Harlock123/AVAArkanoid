using AVAArkanoid.Models;

namespace AVAArkanoid.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It works out where each falling ball will
/// come down (bouncing off the side walls on the way) and meets the soonest, catching it a little off
/// the middle of the paddle so the angle changes from one hit to the next. With time to spare it goes
/// for a falling capsule, it keeps out from under the Monolith's fire, and it fires off a held ball
/// and keeps the lasers going.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private double _offset;
    private Ball? _tracking;
    private bool _fire;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        var p = g.Paddle;
        double target = p.X;

        // The ball due down soonest, and where.
        Ball? next = null;
        double soonest = double.MaxValue, landing = p.X;
        foreach (var b in g.Balls)
        {
            if (b.Stuck || b.Vel.Y <= 0) continue;
            double t = (Field.PaddleY - Field.BallRadius - b.Pos.Y) / b.Vel.Y;
            if (t < 0 || t >= soonest) continue;
            soonest = t;
            next = b;
            landing = Fold(b.Pos.X + b.Vel.X * t);
        }
        if (next != _tracking)
        {
            _tracking = next;
            // Somewhere along the paddle other than the dead middle, for a change of angle.
            _offset = (_rng.NextDouble() * 2 - 1) * p.Width * 0.38;
        }
        // Rising balls: how long until one comes back down, roughly.
        double free = g.Balls.Where(b => !b.Stuck && b.Vel.Y < 0)
            .Select(b => (b.Pos.Y - Field.Top + Field.PaddleY - Field.Top) / Math.Max(1, b.Speed))
            .DefaultIfEmpty(9).Min();

        var capsule = g.Capsules.OrderByDescending(c => c.Pos.Y).FirstOrDefault();
        if (next is not null && (capsule is null || soonest < 0.9)) target = landing - _offset;
        else if (capsule is not null && (next is null ? free : soonest) > (Field.PaddleY - capsule.Pos.Y) / GameEngine.CapsuleSpeed * 0.6)
            target = capsule.Pos.X;
        else if (next is not null) target = landing - _offset;
        else if (g.Balls.FirstOrDefault(b => !b.Stuck) is { } up) target = up.Pos.X;

        // Out of the way of the Monolith's fire: the nearest place to where it wants to be that no
        // fireball due down soon will land on, without crossing one about to land on the way there.
        var danger = new List<(double Lo, double Hi, double T)>();
        foreach (var f in g.Fireballs)
        {
            if (f.Vel.Y <= 0) continue;
            double t = (Field.PaddleY - f.Pos.Y) / f.Vel.Y;
            // Until it has fallen clear past the paddle, not just reached its top.
            if (f.Pos.Y - 3 > Field.PaddleY + 6 || t > 1.4) continue;
            double fx = f.Pos.X + f.Vel.X * Math.Max(0, t);
            danger.Add((fx - p.Width / 2 - 6, fx + p.Width / 2 + 6, t));
        }
        if (danger.Count > 0)
        {
            // Crossing one about to land is out, unless the paddle is under it already and getting out.
            bool Safe(double x) =>
                danger.All(d => x < d.Lo || x > d.Hi)
                && danger.Where(d => d.T < 0.25 && !(p.X >= d.Lo && p.X <= d.Hi))
                    .All(d => Math.Max(x, p.X) < d.Lo || Math.Min(x, p.X) > d.Hi);
            var options = new List<double> { target, p.X };
            foreach (var d in danger) { options.Add(d.Lo - 1); options.Add(d.Hi + 1); }
            // Ways to still meet the ball: on either end of the paddle.
            if (next is not null)
                for (double e = -p.Width / 2 + 3; e <= p.Width / 2 - 3; e += 4) options.Add(landing - e);
            var safe = options.Where(x => x >= Field.Left + p.Width / 2 && x <= Field.Right - p.Width / 2 && Safe(x)).ToList();
            if (safe.Count > 0)
            {
                // Meeting the ball comes first, then keeping near where it wanted to be.
                bool Meets(double x) => next is not null && Math.Abs(landing - x) <= p.Width / 2 - 2;
                target = safe.OrderBy(x => Meets(x) ? 0 : 1).ThenBy(x => Math.Abs(x - target)).First();
            }
        }

        // Fire: a held ball once the paddle is set, and the lasers all the time.
        bool held = g.Balls.Any(b => b.Stuck);
        _fire = !_fire && (held && Math.Abs(target - p.X) < 4 || p.Power == Power.Laser);
        if (held && g.Balls.All(b => b.Stuck) && next is null) target = Field.Width / 2.0 + Math.Sin(g.PhaseTime * 2) * 40;
        return new Controls(0, _fire, Math.Clamp(target, Field.Left, Field.Right));
    }

    /// <summary>Folds an x back inside the walls, as the ball bounces off them.</summary>
    private static double Fold(double x)
    {
        double lo = Field.Left + Field.BallRadius, hi = Field.Right - Field.BallRadius, span = hi - lo;
        double t = (x - lo) % (2 * span);
        if (t < 0) t += 2 * span;
        return lo + (t <= span ? t : 2 * span - t);
    }
}
