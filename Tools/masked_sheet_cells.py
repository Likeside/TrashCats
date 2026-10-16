#!/usr/bin/env python3
"""Cells of a masked pose edit made from Tools/masked_pose_sheet.py-like base sheets (2026-10-16, Ryzhy's bench from
ArtSource/Bench/ryzhy_v2 frame 0): every cell is cut where the base frame was placed (no re-alignment, the model drew
over the same spot), keyed out, scaled back to the key's pixels on a canvas taller by the room above, and the key's own
pixels are pasted back below lock_y (feathered), except the open box, so legs and torso are the same in every frame.
Usage: masked_sheet_cells.py <sheet> <key png> <out_dir> <lock_y> [open x0,y0,x1,y1]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from sprite_process import key_green  # noqa: E402
import compose_pose_frames as C  # noqa: E402

FEATHER = 10


def main():
    sheet, key_png, out = sys.argv[1], sys.argv[2], sys.argv[3]
    lock_y = int(sys.argv[4])
    box = [int(v) for v in sys.argv[5].split(",")] if len(sys.argv) > 5 else None
    os.makedirs(out, exist_ok=True)
    key = np.array(Image.open(key_png).convert("RGBA")).astype(np.float32)
    kh, kw = key.shape[:2]
    k = 384 / kw
    small_h = round(kh * k)
    room = round((512 - small_h - 20) / k)
    H = kh + room
    rgb = np.array(Image.open(sheet).convert("RGB"))
    clean, alpha = key_green(rgb)
    rgba = np.dstack([clean, alpha]).astype(np.uint8)
    # weight of the key's own pixels: 1 below lock_y (feathered), 0 in the open box
    yy, xx = np.mgrid[0:kh, 0:kw]
    w = np.clip((yy - lock_y) / FEATHER, 0, 1)
    if box:
        inside = (xx >= box[0]) & (xx <= box[2]) & (yy >= box[1]) & (yy <= box[3])
        w[inside] = 0
    w = w[..., None]
    for r in range(2):
        for c in range(4):
            cell = C.despeckle(rgba[r * 512:(r + 1) * 512, c * 384:(c + 1) * 384].copy())
            big = np.array(Image.fromarray(cell).resize((kw, round(512 / k)), Image.LANCZOS)).astype(np.float32)
            top = big.shape[0] - round(20 / k) - kh
            canvas = np.zeros((H, kw, 4), np.float32)
            src = big[max(0, top - room):top + kh]
            canvas[H - src.shape[0]:] = src
            lower = canvas[room:]
            canvas[room:] = lower * (1 - w) + key * w
            Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8)).save(f"{out}/cell_{r * 4 + c}.png")


if __name__ == "__main__":
    main()
