using AVAArkanoid.Models;

namespace AVAArkanoid.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a round.</summary>
public enum Phase
{
    /// <summary>The round's name, before the ball can be launched.</summary>
    Ready,
    Playing,
    /// <summary>The last ball lost: the paddle breaks up.</summary>
    Dying,
    /// <summary>Every brick that can be broken is broken (or the way out was taken, or the Monolith fell).</summary>
    RoundClear,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Paddle, Brick, Metal, Gold, Wall, Laser, Capsule, Enlarge, Split, Drifter, Lost, Warp, MonolithHit, MonolithFire, MonolithDown, ExtraLife,
    TitleTune, StartTune, RoundClear, GameOver,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 3;
    public const int FirstExtraLife = 20000;
    public const int ExtraLifeEvery = 60000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private int _nextExtraLife;

    public GameEngine(int seed = 1986, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        StartRound(0);
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Ready;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }

    public List<Effect> Effects { get; } = [];

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                if (startPressed) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, RoundNumber);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing, 0);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract, _rng.Next(Rounds.BrickRounds));
    }

    private void Begin(GameState state, int round)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _nextExtraLife = FirstExtraLife;
        NewEntryIndex = -1;
        Effects.Clear();
        Lap = 0;
        StartRound(round);
        Play(Sfx.StartTune);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        Effects.Clear();
        Lap = 0;
        StartRound(0);
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
    }

    public void Pause()
    {
        if (State == GameState.Playing) State = GameState.Paused;
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraLife)
        {
            _nextExtraLife += ExtraLifeEvery;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    private void Popup(Vec at, string text, double life = 1) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = life, Text = text });

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }
}
