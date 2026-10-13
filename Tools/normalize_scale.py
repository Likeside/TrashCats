#!/usr/bin/env python3
"""Evens out the character's size across the frames of a clip (generated sheets sometimes draw some frames smaller).

The size of a frame is estimated from the area of its silhouette (sqrt, relative to the first frame), which barely
depends on the pose. Frames that drift more than --tolerance are rescaled about their bottom centre (the feet), the
correction capped at --max. The canvas grows if needed; all frames keep a common size and the bottom-centre pivot.
Usage: Tools/normalize_scale.py [--tolerance 0.05] [--max 1.25] [--report] <clip folder>...
"""
import glob, sys
import numpy as np
from PIL import Image

args = sys.argv[1:]
def opt(name, default):
    if name in args:
        i = args.index(name); v = float(args[i + 1]); del args[i:i + 2]; return v
    return default
tolerance, cap = opt("--tolerance", 0.05), opt("--max", 1.25)
report = "--report" in args
args = [a for a in args if a != "--report"]

for folder in args:
    files = sorted(glob.glob(folder + "/*.png"))
    if len(files) < 2:
        continue
    images = [Image.open(f).convert("RGBA") for f in files]
    areas = [(np.array(im)[..., 3] > 40).sum() for im in images]
    scales = [min(cap, (areas[0] / a) ** 0.5) if a else 1.0 for a in areas]
    scales = [s if abs(s - 1) > tolerance else 1.0 for s in scales]
    print(folder, [round(s, 2) for s in scales])
    if report or all(s == 1.0 for s in scales):
        continue
    w, h = images[0].size
    W, H = int(w * max(scales)) + 2, int(h * max(scales)) + 2
    for f, im, s in zip(files, images, scales):
        big = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS) if s != 1.0 else im
        canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        canvas.paste(big, ((W - big.width) // 2, H - big.height))  # bottom centre stays the pivot
        canvas.save(f)
