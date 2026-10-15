#!/usr/bin/env python3
"""Turns a generated 2x4 pose sheet (raw_v10_poses: no rope / no barbell, the prop is drawn by the game) into frames.

Cells are cut from the sheet as drawn (no per-frame re-centering), keyed out and scaled. rope: frames aligned by the
torso, every frame's feet on one ground line except the hop (frame 5, HOP of the cat's height above it);
scaled so the standing cat is as tall as its hideout idle. bench: every frame aligned to frame 0 by the lower body
(it lies still, only the arms move), placed on the old bench canvas so it sits on the mat as before.
Usage: python3 Tools/compose_pose_frames.py <sheet> <rope|bench> <out_dir> <scale-target> [canvas WxH]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from sprite_process import key_green  # noqa: E402

GROUND = 16
HOP = 0.08  # height of the hop, share of the standing cat


def cells(sheet):
    rgb = np.array(Image.open(sheet).convert("RGB"))
    clean, alpha = key_green(rgb)
    rgba = np.dstack([clean, alpha]).astype(np.uint8)
    h, w = alpha.shape
    out = []
    for r in range(2):
        for c in range(4):
            out.append((r, despeckle(rgba[r * h // 2:(r + 1) * h // 2, c * w // 4:(c + 1) * w // 4].copy())))
    return out


def despeckle(a):
    """Only the character: blobs much smaller than the biggest one (key leftovers at the cell edges) are cleared."""
    from scipy import ndimage
    labels, n = ndimage.label(a[..., 3] > 60)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(labels), labels, range(1, n + 1))
        keep = np.isin(labels, [i + 1 for i, size in enumerate(sizes) if size >= sizes.max() * 0.02])
        grown = ndimage.binary_dilation(keep, iterations=3)
        a[~grown] = 0
    return a


def mask(a):
    return a[..., 3] > 60


def bbox(m):
    ys, xs = np.nonzero(m)
    return xs.min(), ys.min(), xs.max(), ys.max()


def scaled(a, k):
    im = Image.fromarray(a, "RGBA")
    return np.array(im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS))


def best_shift(ref, m, region):
    """Shift of m that best overlaps ref inside rows region (fractions of the height)."""
    h = ref.shape[0]
    ys = np.nonzero(ref.any(1))[0]
    top, bottom = ys.min(), ys.max()
    r0, r1 = int(top + (bottom - top) * region[0]), bottom + 1
    best, arg = None, (0, 0)
    for dy in range(-120, 121, 3):
        for dx in range(-80, 81, 3):
            s = np.roll(np.roll(m, dy, 0), dx, 1)
            score = np.logical_xor(ref[r0:r1], s[r0:r1]).sum()
            if best is None or score < best:
                best, arg = score, (dx, dy)
    return arg


def main():
    sheet, kind, out_dir, target = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4])
    canvas = tuple(int(v) for v in sys.argv[5].split("x")) if len(sys.argv) > 5 else None
    frames = cells(sheet)
    first = frames[0][1]
    x0, y0, x1, y1 = bbox(mask(first))
    k = target / (y1 - y0)
    frames = [(r, scaled(a, k)) for r, a in frames]
    os.makedirs(out_dir, exist_ok=True)
    stem = os.path.basename(out_dir.rstrip("/")).lower()
    placed = []
    if kind == "rope":
        feet = {}
        for r, a in frames:
            feet.setdefault(r, []).append(bbox(mask(a))[3])
        for i, (r, a) in enumerate(frames):
            m = mask(a)
            bx0, by0, bx1, by1 = bbox(m)
            band = m[by0 + (by1 - by0) * 3 // 10: by0 + (by1 - by0) * 6 // 10]
            cx = int(np.nonzero(band)[1].mean())
            # only frame 5 (index 4) is the hop; the sheet's other frames are put on the ground (the model drew some
            # crouching frames higher than their row)
            placed.append((a, cx, by1, round(target * HOP) if i == 4 else 0))
        hmax = max(p[2] - bbox(mask(p[0]))[1] for p in placed)
        W = max(max(p[1] - bbox(mask(p[0]))[0], bbox(mask(p[0]))[2] - p[1]) for p in placed) * 2 + 40
        H = hmax + max(p[3] for p in placed) + GROUND + 40
        for i, (a, cx, by1, up) in enumerate(placed):
            out = np.zeros((H, W, 4), np.uint8)
            ox, oy = W // 2 - cx, H - 1 - GROUND - up - by1
            paste(out, a, ox, oy)
            Image.fromarray(out).save(f"{out_dir}/{stem}_{i:02d}.png")
    else:
        W, H = canvas
        ref = None
        for i, (r, a) in enumerate(frames):
            m = mask(a)
            if ref is None:
                bx0, by0, bx1, by1 = bbox(m)
                ox, oy = (W - (bx1 - bx0)) // 2 - bx0, H - 1 - 19 - by1  # the old frames' feet line
                ref_big = np.zeros((H, W), bool)
                paste_mask(ref_big, m, ox, oy)
                ref = ref_big
                shift = (ox, oy)
            else:
                cand = np.zeros((H, W), bool)
                paste_mask(cand, m, shift0[0], shift0[1])
                dx, dy = best_shift(ref, cand, (0.55, 1.0))
                shift = (shift0[0] + dx, shift0[1] + dy)
            if i == 0:
                shift0 = shift
            out = np.zeros((H, W, 4), np.uint8)
            paste(out, a, *shift)
            Image.fromarray(out).save(f"{out_dir}/{stem}_{i:02d}.png")
    print(out_dir, "scale", round(k, 3))


def paste(out, a, ox, oy):
    H, W = out.shape[:2]
    h, w = a.shape[:2]
    sx0, sy0 = max(0, -ox), max(0, -oy)
    dx0, dy0 = max(0, ox), max(0, oy)
    ww, hh = min(w - sx0, W - dx0), min(h - sy0, H - dy0)
    if ww > 0 and hh > 0:
        out[dy0:dy0 + hh, dx0:dx0 + ww] = a[sy0:sy0 + hh, sx0:sx0 + ww]


def paste_mask(out, m, ox, oy):
    tmp = np.zeros(out.shape + (4,), np.uint8)
    paste(tmp, np.dstack([m.astype(np.uint8) * 255] * 4), ox, oy)
    out |= tmp[..., 3] > 0


if __name__ == "__main__":
    main()
