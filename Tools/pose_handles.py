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
    ("Ryzhy", "Rope"): [(70, 267, 325, 267), (70, 267, 325, 267), (65, 280, 325, 280), (67, 288, 327, 290),
                        (68, 258, 325, 258), (68, 265, 328, 267), (70, 265, 328, 267), (70, 268, 328, 268)],
    ("shnyr", "Rope"): [(90, 262, 272, 255), (90, 262, 272, 255), (93, 297, 273, 293), (90, 293, 277, 288),
                        (90, 258, 277, 255), (91, 280, 277, 273), (90, 258, 274, 258), (90, 262, 275, 260)],
    ("ponchik", "Rope"): [(55, 295, 345, 293), (62, 298, 338, 295), (64, 300, 340, 300), (60, 303, 338, 300),
                          (62, 268, 342, 268), (58, 305, 348, 300), (62, 290, 352, 290), (60, 298, 345, 298)],
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
