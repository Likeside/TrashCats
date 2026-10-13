#!/usr/bin/env python3
"""Removes a drawn ground line / shadow strip under the feet (thin, wide, dark blobs at the bottom of a frame).
Usage: Tools/remove_ground_line.py <clip folder>...
"""
import glob, sys
import numpy as np
from PIL import Image
from scipy import ndimage

for folder in sys.argv[1:]:
    removed = 0
    for f in sorted(glob.glob(folder + "/*.png")):
        a = np.array(Image.open(f).convert("RGBA"))
        solid = a[..., 3] > 30
        h = a.shape[0]
        lum = a[..., :3].astype(np.float32) @ np.array([0.299, 0.587, 0.114], np.float32)
        dark = solid & (lum < 90)
        # rows near the bottom whose dark pixels form a wide thin band
        labels, n = ndimage.label(dark)
        for i, sl in enumerate(ndimage.find_objects(labels), 1):
            bh, bw = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
            if sl[0].start > h * 0.85 and bh <= 10 and bw >= 8 * bh:
                band = labels == i
                band = ndimage.binary_dilation(band, iterations=2) & solid & (lum < 140)
                a[band, 3] = 0
                removed += 1
        # a line touching the feet: dark pixels in the bottom strip, wider than any foot outline
        rows = np.nonzero(solid.any(1))[0]
        if len(rows):
            low = rows.max()
            strip = slice(max(0, low - 9), low + 1)
            band = dark[strip]
            if band.sum(1).max() > a.shape[1] * 0.25:
                a[strip][..., 3][band] = 0
                sub = a[strip]; sub[..., 3][band] = 0; a[strip] = sub
                removed += 1
        Image.fromarray(a).save(f)
    print(folder, "lines removed:", removed)
