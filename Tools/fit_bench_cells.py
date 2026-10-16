#!/usr/bin/env python3
"""Cells of a regenerated bench-press sheet placed over the old frames (2026-10-16, Ponchik's tilted barbell): the
lower body (rows from lock_y down, under the arms) of the first cell is fitted to the old frame's by scale and shift,
every other cell keeps that scale and is shifted onto the first one by its lower body, so the cat lies still on the mat
and keeps its size (HideoutLayout.BenchFit stays valid).
Usage: fit_bench_cells.py <sheet> <old frame png> <out dir> <stem> <lock_y> [cells, e.g. 0,2,4,6,7] [--feet]
--feet: the other cells are placed by their feet (lowest row, middle of the feet) instead of the lower-body overlap.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import compose_pose_frames as C  # noqa: E402


def place(a, k, W, H, ox, oy):
    out = np.zeros((H, W, 4), np.uint8)
    C.paste(out, C.scaled(a, k), ox, oy)
    return out


def score(ref, cand, lock_y):
    return np.logical_xor(ref[lock_y:], cand[lock_y:]).sum()


def fit_shift(ref, a, k, W, H, lock_y, guess, step, reach):
    small = C.scaled(a, k)
    best, arg = None, guess
    for dy in range(-reach, reach + 1, step):
        for dx in range(-reach, reach + 1, step):
            ox, oy = guess[0] + dx, guess[1] + dy
            out = np.zeros((H, W, 4), np.uint8)
            C.paste(out, small, ox, oy)
            s = score(ref, C.mask(out), lock_y)
            if best is None or s < best:
                best, arg = s, (ox, oy)
    return best, arg


def fit_wide(ref, a, k, W, H, lock_y, guess):
    """Coarse then fine: cells of the second sheet row are often drawn at another spot of their cell."""
    _, arg = fit_shift(ref, a, k, W, H, lock_y, guess, 4, 60)
    return fit_shift(ref, a, k, W, H, lock_y, arg, 1, 4)


def feet(m, band=30):
    """Middle x of the pixels in the lowest rows (both feet) and the lowest row."""
    ys = np.nonzero(m.any(1))[0]
    bottom = ys.max()
    xs = np.nonzero(m[bottom - band:bottom + 1])[1]
    return (xs.min() + xs.max()) / 2, bottom


def main():
    sheet, old_png, out, stem, lock_y = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], int(sys.argv[5])
    old = np.array(Image.open(old_png).convert("RGBA"))
    H, W = old.shape[:2]
    ref = C.mask(old)
    cells = [a for _, a in C.cells(sheet)]
    pick = [int(v) for v in sys.argv[6].split(",")] if len(sys.argv) > 6 and sys.argv[6][0].isdigit() else list(range(len(cells)))
    first = cells[pick[0]]
    # scale: the feet-to-knee part of the old frame and of the cell, by the width of everything under lock_y
    rx0, _, rx1, ry1 = C.bbox(ref[lock_y:])
    best = None
    for k in np.arange(0.6, 1.6, 0.01):
        m = C.mask(C.scaled(first, k))
        bx0, by0, bx1, by1 = C.bbox(m)
        # feet on the old feet line, centred like the old lower body
        oy = lock_y + ry1 - by1
        rows = m[max(0, lock_y - oy):]
        if not rows.any():
            continue
        lx0, _, lx1, _ = C.bbox(rows)
        guess = ((rx0 + rx1) // 2 - (lx0 + lx1) // 2, oy)
        s, arg = fit_shift(ref, first, k, W, H, lock_y, guess, 3, 9)
        if best is None or s < best[0]:
            best = (s, k, arg)
    _, k, shift0 = best
    _, shift0 = fit_shift(ref, first, k, W, H, lock_y, shift0, 1, 3)
    base = C.mask(place(first, k, W, H, *shift0))
    os.makedirs(out, exist_ok=True)
    feet0 = feet(C.mask(C.scaled(first, k)))
    for n, i in enumerate(pick):
        if n == 0:
            shift = shift0
        elif "--feet" in sys.argv:
            # the feet stay planted: the lowest row and the middle of the feet as in the first cell
            f = feet(C.mask(C.scaled(cells[i], k)))
            shift = (shift0[0] + round(feet0[0] - f[0]), shift0[1] + feet0[1] - f[1])
        else:
            shift = fit_wide(base, cells[i], k, W, H, lock_y, shift0)[1]
        Image.fromarray(place(cells[i], k, W, H, *shift)).save(f"{out}/{stem}_{n:02d}.png")
        print(i, "->", n, "shift", shift)
    print("scale", round(float(k), 3))


if __name__ == "__main__":
    main()
