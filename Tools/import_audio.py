#!/usr/bin/env python3
"""Builds Assets/_Project/Audio from the CC0 sources in ArtSource/Audio (Kenney packs, OpenGameArt) and synthesises the
few sounds no pack has (cat hiss, miss whoosh). Every sound gets a stable key name; variants are key_00, key_01...
Writes Assets/_Project/Audio/CREDITS.md. Needs ffmpeg and numpy.
Usage: Tools/import_audio.py [python-with-numpy is the interpreter running this]
"""
import os, shutil, subprocess, wave
import numpy as np

SRC = "ArtSource/Audio"
OUT = "Assets/_Project/Audio"
IMPACT = f"{SRC}/kenney_impact-sounds/Audio"
UI = f"{SRC}/kenney_interface-sounds/Audio"
RPG = f"{SRC}/kenney_rpg-audio/Audio"
JINGLES = f"{SRC}/kenney_music-jingles/Audio"
OGA = f"{SRC}/opengameart"

SFX = {
    # UI (CONTENT_SPEC §10)
    "ui_tap": [f"{UI}/click_002.ogg"],
    "ui_open": [f"{UI}/maximize_002.ogg"],
    "ui_close": [f"{UI}/minimize_002.ogg"],
    "ui_error": [f"{UI}/error_004.ogg"],
    "ui_buy": [f"{RPG}/handleCoins.ogg"],
    "ui_popup": [f"{UI}/confirmation_002.ogg"],
    "ui_reward": [f"{UI}/drop_002.ogg"],
    # hideout
    "hid_can": [f"{IMPACT}/impactTin_medium_00{i}.ogg" for i in range(3)],
    "hid_mouse": [f"{UI}/pluck_001.ogg", f"{UI}/pluck_002.ogg"],
    "hid_wheel": [f"{RPG}/creak1.ogg", f"{RPG}/creak2.ogg"],
    "hid_dumpster": [f"{IMPACT}/impactMetal_light_00{i}.ogg" for i in range(3)],
    "hid_eat": [f"{OGA}/cat_mewfood.wav"],
    "hid_levelup": [f"{OGA}/cat_mewpurr.wav"],
    "hid_meow": [f"{OGA}/Meow.ogg", f"{OGA}/cat_softmew.wav"],
    "hid_sleep": [f"{OGA}/cat_purrsleepy_loop.wav"],
    # battle
    "cmb_swing": [f"{RPG}/cloth1.ogg", f"{RPG}/cloth3.ogg"],
    "cmb_hit": [f"{IMPACT}/impactPunch_medium_00{i}.ogg" for i in range(5)],
    "cmb_hit_heavy": [f"{IMPACT}/impactPunch_heavy_00{i}.ogg" for i in range(5)],
    "cmb_ko": [f"{IMPACT}/impactSoft_heavy_000.ogg"],
    "cmb_bell": [f"{IMPACT}/impactBell_heavy_000.ogg"],
    "cmb_win": [f"{JINGLES}/Pizzicato jingles/jingles_PIZZI00.ogg"],
    "cmb_lose": [f"{JINGLES}/Sax jingles/jingles_SAX07.ogg"],
}
MUSIC = {"music_hideout": f"{OGA}/ChillLofi.ogg", "music_battle": f"{OGA}/jazznbrass_loop.mp3"}

CREDITS = """# Audio credits

All sounds are CC0 (public domain); credit is not required but given with thanks.

- **Kenney** (www.kenney.nl): Impact Sounds, Interface Sounds, RPG Audio, Music Jingles — CC0.
- **Kerzoven**, "Cat Purr & Meow" (opengameart.org/content/cat-purr-meow) — CC0.
- **IgnasD**, "Meow" (opengameart.org/content/meow) — CC0.
- **omfgdude**, "Chill lofi inspired" (opengameart.org/content/chill-lofi-inspired) — CC0. Hideout music.
- **Emma_MA**, "jazz n brass loop" (opengameart.org/content/jazz-n-brass-loop) — CC0. Battle music.
- `cmb_hiss`, `cmb_miss`: synthesised by Tools/import_audio.py.
"""


def ffmpeg(src, dst, mono=True, quality=4):
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", src]
    if mono:
        cmd += ["-ac", "1"]
    cmd += ["-c:a", "libvorbis", "-q:a", str(quality), dst]
    subprocess.run(cmd, check=True)


def write_wav(path, samples, rate=44100):
    data = (np.clip(samples, -1, 1) * 32000).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data.tobytes())


def synth(rate=44100):
    rng = np.random.default_rng(7)
    # hiss: bright noise with a fast attack and a long tail
    n = int(rate * 0.75)
    t = np.arange(n) / rate
    noise = rng.standard_normal(n)
    bright = np.diff(noise, prepend=0)  # high-pass
    env = np.minimum(1, t / 0.04) * np.exp(-t * 3.2)
    hiss = bright * env * 0.35
    # whoosh: band-limited noise sweeping up and down
    m = int(rate * 0.32)
    t2 = np.arange(m) / rate
    noise2 = rng.standard_normal(m)
    kernel = np.ones(24) / 24
    smooth = np.convolve(noise2, kernel, mode="same")
    env2 = np.sin(np.pi * t2 / t2[-1]) ** 2
    whoosh = (smooth * 3.0 + np.diff(noise2, prepend=0) * 0.15) * env2 * 0.6
    return {"cmb_hiss": hiss, "cmb_miss": whoosh}


def main():
    sfx_dir, music_dir = f"{OUT}/Sfx", f"{OUT}/Music"
    for d in (sfx_dir, music_dir):
        os.makedirs(d, exist_ok=True)
    for key, files in SFX.items():
        for i, src in enumerate(files):
            ffmpeg(src, f"{sfx_dir}/{key}_{i:02d}.ogg")
    tmp = "/tmp" if not os.environ.get("TMPDIR") else os.environ["TMPDIR"]
    for key, samples in synth().items():
        wav = os.path.join(tmp, key + ".wav")
        write_wav(wav, samples)
        ffmpeg(wav, f"{sfx_dir}/{key}_00.ogg")
        os.remove(wav)
    for key, src in MUSIC.items():
        ffmpeg(src, f"{music_dir}/{key}.ogg", mono=False, quality=3)
    with open(f"{OUT}/CREDITS.md", "w") as f:
        f.write(CREDITS)
    print(len(os.listdir(sfx_dir)), "sfx,", len(os.listdir(music_dir)), "music")


if __name__ == "__main__":
    main()
