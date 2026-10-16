#!/usr/bin/env python3
"""Bench variants (2026-10-16): cuts a generated pose sheet (rows split at the empty band between them), scales the
cells to the current clip by the width of the legs and aligns every cell by the lower body onto the current clip's
canvas, padded (wider for a drawn barbell, taller for raised fists). Prints for every cell the head offset (face
template of one cell, see --face) and the top of the fists / barbell, to pick and order the frames.
Usage: compose_variant.py <sheet> <ref frame png> <out_dir> [--cols 4] [--rows 2] [--pad-x 0] [--pad-top 0]
       [--face x0,y0,x1,y1 (in the ref canvas)] [--face-cell 1] [--fit legs|height] (height: cell 0 as tall as the ref,
       for a redrawn body whose legs differ)
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import compose_pose_frames as C  # noqa: E402


def opt(name, default):
    if name in sys.argv:
        i = sys.argv.index(name)
        v = sys.argv[i + 1]
        del sys.argv[i:i + 2]
        return v
    return default


def leg_width(m, band):
    ys = np.nonzero(m.any(1))[0]
    rows = m[max(0, ys.max() - band):ys.max() + 1]
    xs = np.nonzero(rows.any(0))[0]
    return xs.max() - xs.min()


def main():
    cols, rows = int(opt("--cols", 4)), int(opt("--rows", 2))
    pad_x, pad_top = int(opt("--pad-x", 0)), int(opt("--pad-top", 0))
    face = opt("--face", None)
    face_cell = int(opt("--face-cell", 1))
    fit = opt("--fit", "legs")
    sheet, ref_png, out = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out, exist_ok=True)
    rgb = np.array(Image.open(sheet).convert("RGB"))
    clean, alpha = C.key_green(rgb)
    rgba = np.dstack([clean, alpha]).astype(np.uint8)
    h, w = alpha.shape
    # row edges: the middle of the widest empty band near each row boundary
    filled = (alpha > 60).sum(1) > 3
    edges = [0]
    for r in range(1, rows):
        guess = r * h // rows
        lo, hi = guess - h // (rows * 3), guess + h // (rows * 3)
        empty = [y for y in range(lo, hi) if not filled[y]]
        edges.append((empty[0] + empty[-1]) // 2 if empty else guess)
    edges.append(h)
    cells = [C.despeckle(rgba[edges[r]:edges[r + 1], c * w // cols:(c + 1) * w // cols].copy())
             for r in range(rows) for c in range(cols)]

    ref = np.array(Image.open(ref_png).convert("RGBA"))
    rm = C.mask(ref)
    H, W = rm.shape[0] + pad_top, rm.shape[1] + 2 * pad_x
    big_ref = np.zeros((H, W), bool)
    C.paste_mask(big_ref, rm, pad_x, pad_top)
    band_ref = 60
    k = 1.0
    for _ in range(3):  # the band is measured in the cell's own pixels
        k = leg_width(rm, band_ref) / np.median([leg_width(C.mask(a), round(band_ref / k)) for a in cells])
    if fit == "height":
        def tall(m):
            ys = np.nonzero(m.any(1))[0]
            return ys.max() - ys.min()
        k = tall(rm) / tall(C.mask(cells[0]))
    print("scale", round(k, 3), "canvas", W, H)
    rx0, ry0, rx1, ry1 = C.bbox(big_ref)
    placed = []
    for i, a in enumerate(cells):
        a = C.scaled(a, k)
        m = C.mask(a)
        x0, y0, x1, y1 = C.bbox(m)
        ox, oy = rx1 - x1, ry1 - y1
        cand = np.zeros((H, W), bool)
        C.paste_mask(cand, m, ox, oy)
        dx, dy = C.best_shift(big_ref, cand, (0.6, 1.0))
        o = np.zeros((H, W, 4), np.uint8)
        C.paste(o, a, ox + dx, oy + dy)
        Image.fromarray(o).save(f"{out}/cell_{i}.png")
        placed.append(o)
    if face:
        fx0, fy0, fx1, fy1 = [int(v) for v in face.split(",")]
        fx0, fx1, fy0, fy1 = fx0 + pad_x, fx1 + pad_x, fy0 + pad_top, fy1 + pad_top
        T = placed[face_cell][fy0:fy1, fx0:fx1].astype(np.float32)
    for i, o in enumerate(placed):
        m = C.mask(o)
        top = int(np.nonzero(m.any(1))[0].min())
        line = f"cell {i}: top {top - pad_top}"
        if face:
            best = None
            f = o.astype(np.float32)
            for dy in range(-30, 31):
                for dx in range(-30, 31):
                    e = np.abs(f[fy0 + dy:fy1 + dy, fx0 + dx:fx1 + dx] - T).mean()
                    if best is None or e < best[0]:
                        best = (e, dx, dy)
            line += f", head ({best[1]}, {best[2]}) err {best[0]:.0f}"
        print(line)


if __name__ == "__main__":
    main()
