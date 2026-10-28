# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A brick-breaking game in the style of Taito's 1986 arcade game Arkanoid, in C# on .NET 10 and Avalonia 12, drawn
through Avalonia's `DrawingContext`. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). Everything in
it is original: the rounds are its own designs, the graphics are drawn in code, and the sounds and tunes are
synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAArkanoid.slnx                     # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAArkanoid.slnx                      # every test runs headless: no window opens
dotnet run --project src/AVAArkanoid              # play it; --no-audio for silence
ARKANOID_SHOTS=$PWD/Images dotnet test tests/AVAArkanoid.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVAArkanoid/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAArkanoid/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, demonstration, play, high scores), scoring and bonus paddles.
  - `GameEngine.Play.cs` steps a round: the paddle, the ball and what it hits, capsules and their powers,
    lasers, drifters and the Monolith.
  - `Entities.cs` has the paddle, balls, bricks, capsules, beams, drifters and the `Controls` record.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys and mouse) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVAArkanoid/Models/Field.cs`: the field's measures (224 by 256 game pixels), the fixed step, the brick
  kinds, and the rounds, written as text maps parsed by `RoundMap.Parse`.
- `src/AVAArkanoid/Views/`: `GameCanvas` (split over partial files) draws the frame and runs the 60 Hz timer.
- `src/AVAArkanoid/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVAArkanoid/Sounds/`: the WAV files that `tools/make_sounds.py` writes.
- `tests/AVAArkanoid.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: `GameplayTests` and `ScreenshotTests`.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances in fixed 1/60 s steps (`Field.Step`), independent of the frame
  rate, so a seed always plays the same game and tests can play whole rounds deterministically. Draw random
  numbers from the engine's seeded `_rng`, never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it without
  a window. `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** Keys, stick and mouse boil down to `Controls(Dx, Fire, AimX)`:
  negative `Dx` is left, and `AimX` is the mouse's x in game pixels once the mouse moves. Test new controls
  through `InputHandler`, not by setting `Controls` directly.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVAArkanoid`); tests
  give the table a temporary folder. They make no sound: the engine only raises `Sound` events, and the
  screenshot tests use `SoundManager(silent: true)`.
- **Line endings.** Keep each file's existing line endings (`app.manifest` is CRLF) so diffs show only real
  changes.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list, the project structure) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
