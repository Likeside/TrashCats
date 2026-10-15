#!/usr/bin/env python3
"""Puts every battle frame on one ground line: the lowest point of the silhouette (feet, or the body of a knocked-out
fighter) is moved down to GROUND empty rows above the frame's bottom edge, the line the idle frames stand on. The
sprites' pivot is the bottom centre, so a frame with more empty rows under the feet looked like the fighter hopping.
Originals are copied to ArtSource/Battle/before_ground/<character>/<clip>/ first (never overwritten).
Reads TestResults/clips.json (TrashCats/Art/Rebuild Character Art Registry). Usage: python3 Tools/ground_battle_frames.py
"""
import json
import os
import shutil

import numpy as np
from PIL import Image

CLIPS = "TestResults/clips.json"
ROLES = {"idle", "attack", "heavy", "hit", "ko"}
GROUND = 16
ALPHA = 60
ARCHIVE = "ArtSource/Battle/before_ground"


def main():
    done = set()
    for clip in json.load(open(CLIPS)):
        if clip["role"] not in ROLES or clip["character"] == "wheel":
            continue
        for path in clip["frames"]:
            if path in done:
                continue
            done.add(path)
            im = Image.open(path).convert("RGBA")
            a = np.array(im)
            solid = np.nonzero((a[..., 3] > ALPHA).any(1))[0]
            if len(solid) == 0:
                continue
            gap = a.shape[0] - 1 - solid.max()
            if gap == GROUND:
                continue
            keep = os.path.join(ARCHIVE, clip["character"], clip["role"], os.path.basename(path))
            os.makedirs(os.path.dirname(keep), exist_ok=True)
            if not os.path.exists(keep):
                shutil.copy2(path, keep)
            shift = gap - GROUND
            out = np.zeros_like(a)
            if shift > 0:
                out[shift:] = a[:-shift]
            else:
                out[:shift] = a[-shift:]
            Image.fromarray(out, "RGBA").save(path)
            print(f"{path}: {shift:+d}")


if __name__ == "__main__":
    main()


# Frames drawn in the air keep their height (restored from the archive after grounding, 2026-10-16): the leaping heavy
# attacks, and hit / knock-out frames where the fighter flies back or falls (archived gap under the feet >= AIRBORNE).
LEAPS = {("shnyr", "heavy"), ("ponchik", "heavy")}
AIRBORNE = 45


def restore_airborne():
    import glob
    for path in sorted(glob.glob(os.path.join(ARCHIVE, "*", "*", "*.png"))):
        character, role = path.split(os.sep)[-3:-1]
        a = np.array(Image.open(path).convert("RGBA"))[..., 3] > ALPHA
        gap = a.shape[0] - 1 - np.nonzero(a.any(1))[0].max()
        if (character, role) in LEAPS or (role in ("hit", "ko") and gap >= AIRBORNE):
            target = next(c for c in glob.glob(f"Assets/_Project/Art/Characters/*/*/{os.path.basename(path)}"))
            shutil.copy2(path, target)
            print(f"restored {target} (gap {gap})")
