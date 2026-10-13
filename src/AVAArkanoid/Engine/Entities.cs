using AVAArkanoid.Models;

namespace AVAArkanoid.Engine;

/// <summary>The bat at the bottom: its middle, its width, and the power it has.</summary>
public sealed class Paddle
{
    public const double Normal = 32;
    public const double Long = 48;
    public double X { get; set; } = Field.Width / 2.0;
    public double Width { get; set; } = Normal;
    public Power Power { get; set; }
    public double Left => X - Width / 2;
    public double Right => X + Width / 2;
}

/// <summary>The paddle's power from the last capsule caught.</summary>
public enum Power { None, Catch, Laser, Enlarge }

public sealed class Ball
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public double Speed { get; set; }
    /// <summary>Held on the paddle (at the start, or caught): how far from its middle, and for how long.</summary>
    public bool Stuck { get; set; }
    public double StuckOffset { get; set; }
    public double StuckTime { get; set; }
    public bool Lost { get; set; }
}

public sealed class Brick
{
    public required BrickKind Kind { get; init; }
    public int Hits { get; set; }
    /// <summary>Seconds of shimmer left after a hit (silver and gold).</summary>
    public double Shine { get; set; }
}

/// <summary>What a capsule gives.</summary>
public enum CapsuleKind
{
    /// <summary>Slows the ball.</summary>
    Slow,
    /// <summary>The ball sticks to the paddle until fired off.</summary>
    Catch,
    /// <summary>The paddle fires lasers.</summary>
    Laser,
    /// <summary>A longer paddle.</summary>
    Enlarge,
    /// <summary>Three balls.</summary>
    Disrupt,
    /// <summary>Opens the way out at the side: on to the next round.</summary>
    Break,
    /// <summary>An extra paddle.</summary>
    Player,
}

public sealed class Capsule
{
    public required CapsuleKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
}

public sealed class Beam
{
    public Vec Pos { get; set; }
    public bool Dead { get; set; }
}

/// <summary>Something drifting down from the hatches in the top: the ball or the paddle breaks it.</summary>
public sealed class Drifter
{
    public required int Look { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Phase { get; init; }
    public bool Dead { get; set; }
}

/// <summary>The last round's guardian: a great stone face that takes many hits and spits fire.</summary>
public sealed class Monolith
{
    public const double Width = 56;
    public const double Height = 64;
    public Vec Centre { get; set; } = new(Field.Width / 2.0, 84);
    public int Hits { get; set; }
    public int MaxHits { get; init; }
    public double Hurt { get; set; }
    public double Reload { get; set; } = 2;
    /// <summary>Seconds since it was beaten (it crumbles), or −1.</summary>
    public double Beaten { get; set; } = -1;
    public double Left => Centre.X - Width / 2;
    public double Right => Centre.X + Width / 2;
    public double TopY => Centre.Y - Height / 2;
    public double Bottom => Centre.Y + Height / 2;
}

/// <summary>A fireball from the Monolith.</summary>
public sealed class Fireball
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public bool Dead { get; set; }
}

public enum EffectKind { Score, Blast, Shatter }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public uint Colour { get; init; }
    public bool Done => Age >= Life;
}

/// <summary>
/// The controls for one step: the stick (−1 to 1), the fire button (launch, release, lasers), and
/// where the mouse wants the paddle, if it's being used.
/// </summary>
public record struct Controls(double Dx, bool Fire = false, double? AimX = null);
