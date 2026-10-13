#!/usr/bin/env python3
"""Keeps a character standing on one spot: shifts every frame horizontally so its legs (the lower third of the
figure) overlap the first frame's legs best. Fixes clips whose cut drifted (e.g. a tail tip taken for a foot).
Usage: Tools/align_frames.py [--max 80] <clip folder>...
"""
import glob, sys
import numpy as np
from PIL import Image

args = sys.argv[1:]
max_px = 80
if "--max" in args:
    i = args.index("--max"); max_px = int(args[i + 1]); del args[i:i + 2]


def legs(a):
    rows = np.nonzero(a.any(1))[0]
    top = rows.min() + int((rows.max() - rows.min()) * 0.67)
    out = np.zeros_like(a); out[top:] = a[top:]
    return out


for folder in args:
    files = sorted(glob.glob(folder + "/*.png"))
    images = [Image.open(f).convert("RGBA") for f in files]
    ref = legs(np.array(images[0])[..., 3] > 128)
    shifts = []
    for f, im in zip(files, images):
        a = legs(np.array(im)[..., 3] > 128)
        best, dx = -1, 0
        for s in range(-max_px, max_px + 1):
            score = (ref & np.roll(a, s, axis=1)).sum()
            if score > best:
                best, dx = score, s
        shifts.append(dx)
        if dx:
            moved = Image.new("RGBA", im.size, (0, 0, 0, 0))
            moved.paste(im, (dx, 0))
            moved.save(f)
    print(folder, shifts)
