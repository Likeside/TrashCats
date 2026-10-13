#!/usr/bin/env python3
"""Imports generated sheets (Docs/ArtDirection/Generated/raw) into Unity art folders.

Character sheets (2x4) are cut with sprite_process.py into two 4-frame clips; hideout pose sheets, props and icons are cut
and trimmed to their outlines; backgrounds are copied. Re-runnable; only reads raw sheets. Needs numpy + Pillow.
Usage: Tools/import_art.py [python-with-numpy]
"""
import glob, os, shutil, subprocess, sys, tempfile
import numpy as np
from PIL import Image

RAW = os.environ.get("ART_RAW", "Docs/ArtDirection/Generated/raw")  # later rounds: ART_RAW=.../raw_v2
CHARS = "Assets/_Project/Art/Characters"
ENV = "Assets/_Project/Art/Environment"
PY = sys.argv[1] if len(sys.argv) > 1 else sys.executable
GUARD_HEIGHT = 369  # Ryzhy's stance height: every character clip is scaled so its first frame stands this tall

SHEETS = {  # sheet suffix -> (first row clip, second row clip)
    "idle_attack": ("Idle", "Attack"),
    "heavy_hit": ("Heavy", "Hit"),
    "ko_win": ("KO", "Victory"),
    "hit_ko": ("Hit", "KO"),
    "bite_hit": ("Heavy", "Hit"),
}
KEY_FOLDERS = {"ryzhy": "Ryzhy"}  # existing folder names in Assets (case matters on Linux)
PROPS = ["prop_upg_can", "prop_upg_mouse", "prop_upg_wheel", "prop_upg_dumpster", "prop_upg_boxes"]
ICONS = ["icon_food", "icon_junk", "icon_energy", "icon_satiety", "icon_strength", "icon_map"]
BACKGROUNDS = {"bg_hideout": "bg_hideout", "bg_map": "bg_map", "bg_backyard": "arena_backyard", "bg_fishshop": "arena_fishshop",
               "bg_alley": "arena_alley", "bg_park": "arena_park", "bg_rooftop": "arena_rooftop"}


def process(sheet, frames, rows, extra):
    out = tempfile.mkdtemp()
    cmd = [PY, "Tools/sprite_process.py", sheet, out, "--frames", str(frames), "--rows", str(rows)] + extra
    result = subprocess.run(cmd, capture_output=True, text=True)
    print("   ", result.stdout.strip().replace("\n", " | "), result.stderr.strip()[-200:])
    name = os.path.splitext(os.path.basename(sheet))[0]
    return out, name


def trim(path, pad=6):
    im = Image.open(path)
    box = im.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
    if box:
        im = im.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(im.width, box[2] + pad), min(im.height, box[3] + pad)))
    im.save(path)


def put(src, dst):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(src, dst)


def main():
    for path in sorted(glob.glob(f"{RAW}/*.png")):
        name = os.path.splitext(os.path.basename(path))[0]
        suffix = next((s for s in SHEETS if name.endswith("_" + s)), None)
        if suffix:
            key = name[: -len(suffix) - 1]
            key = KEY_FOLDERS.get(key, key)
            first, second = SHEETS[suffix]
            print(f"{name} -> {key}: {first}, {second}")
            # scale by a frame where the character stands in its stance (heavy attacks start crouched)
            reference = "4" if suffix in ("heavy_hit", "bite_hit") else "0"
            out, base = process(path, 8, 2, ["--match-height", str(GUARD_HEIGHT), "--match-frame", reference])
            frames = sorted(glob.glob(f"{out}/{base}_[0-9].png"), key=lambda p: int(p.rsplit("_", 1)[1][:-4]))
            for i, f in enumerate(frames):
                clip = first if i < 4 else second
                put(f, f"{CHARS}/{key}/{clip}/{key}_{clip.lower()}_{i % 4:02d}.png")
        elif name.startswith("hideout_"):
            key = KEY_FOLDERS.get(name[len("hideout_"):], name[len("hideout_"):])
            print(f"{name} -> {key} hideout poses")
            out, base = process(path, 4, 1, ["--anchor", "center", "--match-height", "300"])
            for i, f in enumerate(sorted(glob.glob(f"{out}/{base}_[0-9].png"))):
                trim(f)
                put(f, f"{CHARS}/{key}/Hideout/{key}_hideout_{i:02d}.png")
        elif name == "props" or name == "icons":
            keys = PROPS if name == "props" else ICONS
            print(f"{name} -> {', '.join(keys)}")
            out, base = process(path, len(keys), 1, ["--anchor", "center"])
            for key, f in zip(keys, sorted(glob.glob(f"{out}/{base}_[0-9].png"))):
                trim(f)
                put(f, f"{ENV}/{key}.png")
        elif name in BACKGROUNDS:
            print(f"{name} -> {BACKGROUNDS[name]}")
            put(path, f"{ENV}/{BACKGROUNDS[name]}.png")
        elif name == "wheel_spin" or name.endswith(("_run", "_canpunch", "_attack8")):
            # round 3: one 8-frame clip per sheet (2x4)
            if name == "wheel_spin":
                key, clip, extra = "wheel", "Idle", ["--anchor", "center"]
            else:
                key, kind = name.rsplit("_", 1)
                key = KEY_FOLDERS.get(key, key)
                clip, extra = {
                    "run": ("Run", ["--anchor", "center", "--match-height", "210"]),
                    "canpunch": ("CanPunch", ["--match-height", str(GUARD_HEIGHT), "--match-frame", "0"]),
                    "attack8": ("Attack", ["--match-height", str(GUARD_HEIGHT), "--match-frame", "0"]),
                }[kind]
            print(f"{name} -> {key}/{clip}")
            out, base = process(path, 8, 2, extra)
            folder = f"{CHARS}/{key}/{clip}"
            for old in glob.glob(f"{folder}/*.png*"):
                os.remove(old)
            frames = sorted(glob.glob(f"{out}/{base}_[0-9].png"), key=lambda p: int(p.rsplit("_", 1)[1][:-4]))
            for i, f in enumerate(frames):
                put(f, f"{folder}/{key.lower()}_{clip.lower()}_{i:02d}.png")
        elif name.startswith("prop_"):
            # single prop on green (round 3): key out and trim
            key = {"prop_wheel": "prop_upg_wheel", "prop_can": "prop_upg_can", "prop_dumpster": "prop_upg_dumpster"}.get(name, name)
            print(f"{name} -> {key}")
            out, base = process(path, 1, 1, ["--anchor", "center"])
            f = sorted(glob.glob(f"{out}/{base}_[0-9].png"))[0]
            trim(f)
            put(f, f"{ENV}/{key}.png")
        elif name.startswith("guard_"):
            continue
        else:
            print(f"skip {name}")


if __name__ == "__main__":
    main()
