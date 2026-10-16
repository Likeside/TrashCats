#!/usr/bin/env python3
"""Moves the upper part of a frame (head, arms, a held barbell) as one piece, the part below stays (2026-10-16,
Ryzhy's bench: the head jumped in the in-betweens). Everything above y_full moves by (dx, dy), the shift fades out
smoothly to nothing at y_zero, so the torso between stretches and the feet stay planted.
Usage: band_shift.py <in png> <out png> <dx> <dy> <y_full> <y_zero>
"""
import sys

import numpy as np
from PIL import Image
from scipy import ndimage


def main():
    src, dst = sys.argv[1], sys.argv[2]
    dx, dy, y_full, y_zero = (float(v) for v in sys.argv[3:7])
    a = np.array(Image.open(src).convert("RGBA")).astype(np.float32)
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.clip((y_zero - yy) / (y_zero - y_full), 0, 1)
    t = t * t * (3 - 2 * t)
    # premultiplied, so the edges do not pick up colour from transparent pixels
    pm = a.copy()
    pm[..., :3] *= pm[..., 3:4] / 255
    sx, sy = xx - t * dx, yy - t * dy
    out = np.stack([ndimage.map_coordinates(pm[..., c], [sy, sx], order=1, mode="constant") for c in range(4)], -1)
    alpha = np.clip(out[..., 3:4], 1e-3, 255)
    out[..., :3] = np.where(out[..., 3:4] > 0.5, out[..., :3] * 255 / alpha, 0)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst)


if __name__ == "__main__":
    main()
