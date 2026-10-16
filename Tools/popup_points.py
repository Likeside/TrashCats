#!/usr/bin/env python3
"""Start points of the hideout's cycle result popups (2026-10-16): over each station, at the top of the head of a cat
working there, averaged over the cats and over the frames of their clip. Reads heads.txt of the "-autoplayHeads"
capture (screen rects of the picture and of every cat, the frames it plays, mirrored or not); the head top of a frame
is the middle of its topmost rows. Bench frames have the barbell over the head: their head tops are given by hand.
Prints HideoutLayout.PopupAt values (picture space, 0..1).
Usage: popup_points.py <heads.txt>
Needs numpy, Pillow.
"""
import glob
import sys

import numpy as np
from PIL import Image

# head top on the bench (first frame, as drawn facing right; x from the left, y from the bottom)
BENCH = {"ryzhy": (0.646, 0.564), "shnyr": (0.662, 0.637), "ponchik": (0.598, 0.565)}


def head_top(path, band=0.1):
    a = np.array(Image.open(path).convert("RGBA"))[..., 3] > 128
    ys = np.nonzero(a.any(1))[0]
    y0, y1 = ys.min(), ys.max()
    xs = np.nonzero(a[y0:y0 + max(3, int((y1 - y0) * band))].any(0))[0]
    return (xs.min() + xs.max()) / 2 / a.shape[1], 1 - y0 / a.shape[0]


def main():
    points = {}
    activity = None
    picture = None
    for line in open(sys.argv[1]):
        p = line.split()
        if p[0] == "#":
            who, activity = p[1], p[2]
        elif p[0] == "picture":
            picture = [float(v) for v in p[1:5]]
        elif p[0] == "cat" and p[1] == who:
            x0, y0, x1, y1 = (float(v) for v in p[3:7])
            flipped = p[7] == "1"
            key = who.replace("cat_", "")
            if p[2] == "Bench":
                tops = [BENCH[key]]
            else:
                tops = [head_top(glob.glob(f"Assets/_Project/Art/Characters/*/*/{f}.png")[0]) for f in p[8].split(",")]
            fx, fy = np.mean(tops, 0)
            if flipped:
                fx = 1 - fx
            sx, sy = x0 + fx * (x1 - x0), y0 + fy * (y1 - y0)
            px, py = (sx - picture[0]) / (picture[2] - picture[0]), (sy - picture[1]) / (picture[3] - picture[1])
            points.setdefault(activity, []).append((px, py))
            print(activity, key, round(px, 4), round(py, 4))
    for activity, ps in points.items():
        x, y = np.mean(ps, 0)
        print(f"{activity}: new Vector2({x:.4f}f, {y:.4f}f)")


if __name__ == "__main__":
    main()
