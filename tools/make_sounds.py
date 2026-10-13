#!/usr/bin/env python3
"""Synthesises AVAArkanoid's sound effects and tunes into ../src/AVAArkanoid/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAArkanoid", "Sounds")
RNG = random.Random(1986)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Paddle.wav", tone(0.06, 440, kind="square", release=1.2), 0.32)
    write("Brick.wav", tone(0.05, 880, kind="square", release=1.2), 0.28)
    write("Metal.wav", mix(tone(0.12, 1760, kind="tri", release=1.4), tone(0.12, 2640, kind="square", release=1.8, gain=0.4)), 0.3)
    write("Gold.wav", mix(tone(0.18, 1320, kind="tri", release=1.0), tone(0.18, 1980, kind="tri", release=1.2), tone(0.18, 2970, kind="square", release=1.8, gain=0.3)), 0.32)
    write("Wall.wav", tone(0.03, 220, kind="tri", release=1.5), 0.25)
    write("Laser.wav", tone(0.1, 2200, 600, "pulse", release=1.0), 0.28)
    write("Capsule.wav", cat(*[tone(0.05, hz(n), kind="tri", release=0.4) for n in ["C6", "E6", "G6", "C7"]]), 0.4)
    write("Enlarge.wav", tone(0.35, 300, 900, "square", release=0.5, vibrato=0.04, vib_rate=30), 0.32)
    write("Split.wav", cat(*[tone(0.04, f, kind="pulse", release=0.5) for f in [1600, 2000, 1800, 2400, 2200]]), 0.32)
    write("Drifter.wav", mix(tone(0.2, 1800, 300, "noise", release=0.8), tone(0.15, 600, 200, "square", release=1.0)), 0.4)
    write("Lost.wav", cat(tone(0.7, 900, 90, "square", release=0.4, vibrato=0.06, vib_rate=16), tone(0.3, 300, 60, "noise", release=1.0)), 0.5)
    write("Warp.wav", tone(0.8, 200, 2400, "tri", release=0.3, vibrato=0.08, vib_rate=20), 0.4)
    write("MonolithHit.wav", mix(tone(0.25, 160, 80, "square", release=0.8), tone(0.2, 500, 120, "noise", release=1.0)), 0.5)
    write("MonolithFire.wav", tone(0.3, 700, 200, "saw", release=0.6, vibrato=0.1, vib_rate=25), 0.3)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["G5", "B5", "D6", "G6"] * 2]), 0.45)

    # Tunes, all new: a bright title theme, a short call to the round, a fanfare, a lament.
    write("TitleTune.wav", tune(126,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("B5", 1), ("G5", 1),
         ("A5", 0.5), ("F5", 0.5), ("D5", 0.5), ("F5", 0.5), ("E5", 1), ("C5", 1),
         ("D5", 0.5), ("F5", 0.5), ("A5", 0.5), ("D6", 0.5), ("C6", 1), ("A5", 1),
         ("B5", 0.5), ("G5", 0.5), ("D5", 0.5), ("B5", 0.5), ("C6", 2)],
        [(n, 1) for n in ["C3", "G3", "C3", "G3", "F2", "C3", "C3", "G2", "D3", "A2", "F2", "D3", "G2", "D3", "C3", "C3"]]), 0.6)
    write("StartTune.wav", tune(150,
        [("G4", 0.5), ("C5", 0.5), ("E5", 0.5), ("G5", 0.75), ("E5", 0.25), ("G5", 1), ("-", 0.25), ("A5", 0.5), ("G5", 0.5), ("C6", 1.5)],
        [("C3", 1), ("E3", 1), ("G2", 1), ("C3", 1), ("F2", 1), ("C3", 1)]), 0.6)
    write("RoundClear.wav", tune(160,
        [("E5", 0.25), ("G5", 0.25), ("C6", 0.25), ("E6", 0.25), ("D6", 0.5), ("B5", 0.5), ("C6", 1.5)],
        [("C3", 1), ("G2", 1), ("C3", 2)]), 0.6)
    write("MonolithDown.wav", tune(140,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("E6", 0.5), ("G6", 0.5), ("C7", 2), ("-", 0.5),
         ("G6", 0.5), ("E6", 0.5), ("C7", 2)],
        [("C3", 2), ("G2", 2), ("C3", 2), ("G2", 1), ("C3", 2)]), 0.6)
    write("GameOver.wav", tune(84,
        [("G4", 1), ("E4", 1), ("C4", 1), ("D4", 1), ("B3", 1), ("C4", 2), ("-", 1)],
        [("C3", 2), ("A2", 2), ("G2", 2), ("C3", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
