#!/usr/bin/env python3
"""Sounds of the cats' activities in the hideout (2026-10-16), mixed from the CC0 samples in ArtSource/Audio and
synthesised noise, written as Assets/_Project/Audio/Sfx/<key>_NN.ogg (three variants each):
  hid_rope       the skipping rope slapping the ground (played when the rope passes under the feet)
  hid_bench      the tin cans of the barbell rattling (every press up)
  hid_canpunch   a paw hitting the hanging tin can: a dull knock and a clang (another tin than the barbell's)
  hid_rummage    rummaging in the dumpster: rustling rubbish, bags and a few clinks
  hid_food       food got: a tin can set down with a rattle of leftovers and a short cheerful pluck
Needs ffmpeg and numpy. Usage: Tools/make_activity_sfx.py (run with a python that has numpy)
"""
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from import_audio import IMPACT, RPG, UI, ffmpeg, write_wav  # noqa: E402

RATE = 44100
OUT = "Assets/_Project/Audio/Sfx"


def load(path, speed=1.0):
    """Mono samples of a sound file; speed > 1 plays it faster and higher (like a smaller object)."""
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        name = tmp.name
    rate = int(RATE * speed)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-ac", "1", "-ar", str(rate), name], check=True)
    with wave.open(name) as w:
        data = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768
    os.remove(name)
    # the samples are read as if recorded at RATE: speed > 1 makes them shorter and higher
    return data


def mix(length, *parts):
    """parts: (samples, start seconds, gain)."""
    out = np.zeros(int(RATE * length))
    for samples, at, gain in parts:
        i = int(RATE * at)
        n = min(len(samples), len(out) - i)
        if n > 0:
            out[i:i + n] += samples[:n] * gain
    return out


def trim(x, fade=0.03):
    """Cuts the silent tail and fades the end out."""
    loud = np.nonzero(np.abs(x) > 0.004)[0]
    x = x[:loud[-1] + 1] if len(loud) else x
    n = min(len(x), int(RATE * fade))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def normalise(x, peak=0.85):
    return x / max(1e-6, np.max(np.abs(x))) * peak


def bandpass(x, low, high):
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    spectrum[(f < low) | (f > high)] = 0
    return np.fft.irfft(spectrum, len(x))


def rope(rng):
    """A thin rope slapping concrete: a sharp crack of bright noise, a short smack and a soft scuff."""
    n = int(RATE * 0.16)
    t = np.arange(n) / RATE
    crack = bandpass(rng.standard_normal(n), 1800, 9000) * np.exp(-t * 140)
    smack = bandpass(rng.standard_normal(n), 300, 2200) * np.exp(-t * 60) * 0.7
    thump = np.sin(2 * np.pi * rng.uniform(95, 125) * t) * np.exp(-t * 45) * 0.35
    step = load(f"{IMPACT}/footstep_concrete_00{rng.integers(0, 5)}.ogg", speed=1.25)
    return trim(normalise(mix(0.25, (crack + smack + thump, 0, 1.0), (step, 0.004, 1.2)), 0.8))


def bench(rng, i):
    """Two or three small tin cans knocking together on the bar ends: quick high clinks."""
    hits = []
    at = 0.0
    for k in range(int(rng.integers(3, 5))):
        src = f"{IMPACT}/impactTin_medium_00{(i + k) % 5}.ogg" if k % 2 == 0 else f"{IMPACT}/impactPlate_light_00{(i + k) % 5}.ogg"
        hits.append((load(src, speed=rng.uniform(1.35, 1.7)), at, 0.9 * 0.72 ** k))
        at += rng.uniform(0.035, 0.07)
    return trim(normalise(mix(0.6, *hits), 0.8))


def canpunch(rng, i):
    """A paw knocking the big hanging can: a dull punch and the tin's deeper, longer ring."""
    knock = load(f"{IMPACT}/impactPunch_medium_00{i % 5}.ogg", speed=1.1)
    clang = load(f"{RPG}/metalPot{i % 3 + 1}.ogg", speed=rng.uniform(0.85, 0.95))
    ring = load(f"{IMPACT}/impactMetal_medium_00{(i + 2) % 5}.ogg", speed=0.9)
    return trim(normalise(mix(0.8, (knock, 0, 0.6), (clang, 0.005, 1.0), (ring, 0.01, 0.45)), 0.85))


def rummage(rng, i):
    """About a second of digging in rubbish: rustling bags and paper with a few light clinks and clunks."""
    n = int(RATE * 1.1)
    t = np.arange(n) / RATE
    rustle = np.zeros(n)
    for _ in range(420):
        at = int(rng.uniform(0, n - 600))
        length = int(rng.integers(60, 500))
        grain = np.diff(rng.standard_normal(length + 1)) * np.hanning(length) * rng.uniform(0.1, 1.0)
        rustle[at:at + length] += grain
    swish = np.convolve(rng.standard_normal(n), np.ones(60) / 60, mode="same") * 3
    # two or three scoops: the rustle swells and fades with each
    scoops = np.zeros(n)
    for c in np.sort(rng.uniform(0.1, 0.9, 3)):
        scoops += np.exp(-((t - c) / 0.12) ** 2)
    noise = (bandpass(rustle, 500, 7000) * 0.6 + swish * 0.4) * np.clip(scoops, 0, 1.2)
    noise = normalise(noise, 0.55)
    parts = [(noise, 0, 1.0)]
    for k in range(int(rng.integers(2, 4))):
        src = [f"{IMPACT}/impactTin_medium_00{rng.integers(0, 5)}.ogg", f"{IMPACT}/impactPlate_light_00{rng.integers(0, 5)}.ogg",
               f"{IMPACT}/impactGeneric_light_00{rng.integers(0, 5)}.ogg", f"{RPG}/cloth{rng.integers(1, 5)}.ogg"][(i + k) % 4]
        parts.append((load(src, speed=rng.uniform(1.0, 1.4)), rng.uniform(0.05, 0.85), rng.uniform(0.25, 0.45)))
    out = mix(1.3, *parts)
    fade = int(RATE * 0.08)
    out[:fade] *= np.linspace(0, 1, fade)
    return trim(normalise(out, 0.75), 0.15)


def food(rng, i):
    """A tin of food set down on the ground, leftovers rattling in it, and a short bright pluck: something good."""
    can = load(f"{IMPACT}/impactTin_medium_00{(i + 1) % 5}.ogg", speed=1.0)
    soft = load(f"{IMPACT}/impactSoft_medium_00{i % 5}.ogg", speed=1.1)
    rattle = [(load(f"{IMPACT}/impactGeneric_light_00{(i + k) % 5}.ogg", speed=rng.uniform(1.6, 2.0)), 0.06 + 0.04 * k, 0.3) for k in range(3)]
    pluck = load(f"{UI}/pluck_00{i % 2 + 1}.ogg", speed=1.0 + 0.06 * i)
    return trim(normalise(mix(0.9, (soft, 0, 0.7), (can, 0.01, 0.9), *rattle, (pluck, 0.16, 0.8)), 0.85))


def main():
    rng = np.random.default_rng(16)
    sounds = {
        "hid_rope": [rope(rng) for _ in range(3)],
        "hid_bench": [bench(rng, i) for i in range(3)],
        "hid_canpunch": [canpunch(rng, i) for i in range(3)],
        "hid_rummage": [rummage(rng, i) for i in range(3)],
        "hid_food": [food(rng, i) for i in range(3)],
    }
    tmp = tempfile.mkdtemp()
    for key, variants in sounds.items():
        for i, samples in enumerate(variants):
            wav = os.path.join(tmp, f"{key}_{i:02d}.wav")
            write_wav(wav, samples)
            ffmpeg(wav, f"{OUT}/{key}_{i:02d}.ogg")
            os.remove(wav)
            print(key, i, f"{len(samples) / RATE:.2f}s")
    os.rmdir(tmp)


if __name__ == "__main__":
    main()
