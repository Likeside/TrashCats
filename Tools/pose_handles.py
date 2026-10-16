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
    # bench: key order as played. Ryzhy's folded-arms keys and Shnyr's narrow-grip keys are dropped (2026-10-16);
    # Shnyr's mid key is played twice (up and down)
    ("Ryzhy", "Bench"): [(175, 130, 262, 135), (182, 88, 288, 100), (225, 42, 305, 46), (220, 56, 307, 61),
                         (180, 101, 268, 108), (178, 134, 258, 144)],
    ("shnyr", "Bench"): [(219, 152, 340, 175), (209, 128, 357, 155), (218, 42, 326, 50), (219, 38, 326, 43),
                         (209, 128, 357, 155), (204, 146, 344, 163)],
    ("ponchik", "Bench"): [(195, 162, 288, 172), (171, 128, 318, 140), (155, 90, 327, 118), (200, 17, 288, 22),
                           (195, 13, 288, 18), (159, 97, 329, 117), (165, 118, 319, 130), (196, 160, 288, 170)],
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
