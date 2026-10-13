#!/usr/bin/env python3
"""Keeps a character standing on one spot: shifts every frame horizontally so its legs (the lower third of the
figure) overlap the first frame's legs best. Fixes clips whose cut drifted (e.g. a tail tip taken for a foot).
Usage: Tools/align_frames.py [--max 80] [--cut] <clip folder>...
"""
import glob, sys
import numpy as np
from PIL import Image

args = sys.argv[1:]
max_px = 80
by_cut = "--cut" in args  # clips cut by a straight line at the bottom (in the dumpster): align the cut's centre
args = [a for a in args if a != "--cut"]
if "--max" in args:
    i = args.index("--max"); max_px = int(args[i + 1]); del args[i:i + 2]


def cut_centre(a):
    rows = np.nonzero(a.any(1))[0]
    band = a[rows.max() - 4:rows.max() + 1]
    return float(np.nonzero(band.any(0))[0].mean())


def legs(a):
    rows = np.nonzero(a.any(1))[0]
    top = rows.min() + int((rows.max() - rows.min()) * 0.67)
    out = np.zeros_like(a); out[top:] = a[top:]
    return out


for folder in args:
    files = sorted(glob.glob(folder + "/*.png"))
    images = [Image.open(f).convert("RGBA") for f in files]
    ref = legs(np.array(images[0])[..., 3] > 128)
    ref_cut = cut_centre(np.array(images[0])[..., 3] > 128)
    shifts = []
    for f, im in zip(files, images):
        if by_cut:
            dx = int(round(ref_cut - cut_centre(np.array(im)[..., 3] > 128)))
        else:
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
