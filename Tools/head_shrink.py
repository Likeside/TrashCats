#!/usr/bin/env python3
"""Makes a cat's head smaller in every frame of a clip, the rest of the drawing untouched (2026-10-16, Ponchik's
skipping rope: the head was 10-15% too big against his idle). The head is found by the eyes (their yellow pixels);
a smooth radial warp scales everything within `inner` px of the head centre by `scale`, easing back to 1 at `outer`
px, so the head shrinks towards its centre and the neck and shoulders blend in. Frames keep their size and place.
Usage: head_shrink.py <frames dir> <out dir> <scale> [inner outer up]
  up: the head centre is this many px above the eyes (default 20)
Needs numpy, scipy, Pillow.
"""
import glob
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage


def eyes(a):
    r, g, b, al = (a[..., i].astype(int) for i in range(4))
    m = (al > 200) & (r > 180) & (g > 120) & (b < 90) & (r - b > 120)
    # the eyes are in the top half of the cat
    ys = np.nonzero((a[..., 3] > 60).any(1))[0]
    m[(ys.min() + ys.max()) // 2:] = False
    yy, xx = np.nonzero(m)
    return xx.mean(), yy.mean()


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def warp(a, cx, cy, scale, inner, outer):
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = xx - cx, yy - cy
    d = np.hypot(dx, dy)
    s = scale + (1 - scale) * smoothstep(inner, outer, d)
    # inverse map: an output pixel at distance d shows the source at d / s (farther out: the head is drawn smaller)
    sx, sy = cx + dx / s, cy + dy / s
    # premultiplied, so the edges do not pick up colour from transparent pixels
    pm = a.astype(np.float32)
    pm[..., :3] *= pm[..., 3:4] / 255
    out = np.stack([ndimage.map_coordinates(pm[..., c], [sy, sx], order=1, mode="constant") for c in range(4)], -1)
    alpha = np.clip(out[..., 3:4], 1e-3, 255)
    out[..., :3] = np.where(out[..., 3:4] > 0.5, out[..., :3] * 255 / alpha, 0)
    return np.clip(out, 0, 255).astype(np.uint8)


def main():
    src, out, scale = sys.argv[1], sys.argv[2], float(sys.argv[3])
    inner, outer, up = (float(v) for v in sys.argv[4:7]) if len(sys.argv) > 6 else (70, 170, 20)
    os.makedirs(out, exist_ok=True)
    for f in sorted(glob.glob(os.path.join(src, "*.png"))):
        a = np.array(Image.open(f).convert("RGBA"))
        ex, ey = eyes(a)
        Image.fromarray(warp(a, ex, ey - up, scale, inner, outer)).save(os.path.join(out, os.path.basename(f)))
        print(os.path.basename(f), "eyes", round(ex), round(ey))


if __name__ == "__main__":
    main()
