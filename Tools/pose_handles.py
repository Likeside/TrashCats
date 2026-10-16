#!/usr/bin/env python3
"""Hand points of the prop clips drawn by the game (2026-10-16): the rope's handle ends and the fists on the barbell,
marked by hand on the composed key frames (pixels, y down) and written as handles.json next to the frames, one
(left x, left y, right x, right y) per played frame in 0..1 of the frame with y up. In-betweens made by
Tools/interpolate_adaptive.py (keys.json) get points interpolated between their keys.
Usage: python3 Tools/pose_handles.py   (after interpolation)
"""
import glob
import json
import os

from PIL import Image

A = "Assets/_Project/Art/Characters"
KEYS = {
    # rope: the points of the frames kept by Tools/rope_monotonic.py (2026-10-16) are in the clips' handles.json
    # bench: every cat holds a drawn barbell since 2026-10-16 (raw_v14/v15), no points
}


def main():
    for (cat, clip), points in KEYS.items():
        folder = f"{A}/{cat}/{clip}"
        frames = sorted(glob.glob(f"{folder}/*.png"))
        w, h = Image.open(frames[0]).size
        norm = [(lx / w, 1 - ly / h, rx / w, 1 - ry / h) for lx, ly, rx, ry in points]
        keys_file = f"{folder}/keys.json"
        keys = json.load(open(keys_file))["keys"] if os.path.exists(keys_file) else list(range(len(frames)))
        n = len(frames)
        out = []
        for i in range(n):
            k = max(j for j in range(len(keys)) if keys[j] <= i)
            nxt = (k + 1) % len(keys)
            at, bt = keys[k], (keys[nxt] if nxt > 0 else n)
            t = (i - at) / (bt - at) if bt > at else 0.0
            a, b = norm[k], norm[nxt]
            out.append([round(a[j] + (b[j] - a[j]) * t, 4) for j in range(4)])
        json.dump({"handles": out}, open(f"{folder}/handles.json", "w"))
        print(folder, n, "frames")


if __name__ == "__main__":
    main()
