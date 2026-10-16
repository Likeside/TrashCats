#!/usr/bin/env python3
"""Extra poses for an existing clip (2026-10-16): cuts a 2x2 sheet (optional row split for poses crossing the middle),
scales it so frame 1 (a copy of the reference key) matches the reference by leg width and aligns every frame by the
lower body. Usage: compose_extra_poses.py <sheet> <reference key png> <out_dir> [split row]
"""
import os, sys, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from PIL import Image
import compose_pose_frames as C
from scipy import ndimage
sheet, ref_png, out = sys.argv[1], sys.argv[2], sys.argv[3]
rgb = np.array(Image.open(sheet).convert("RGB")); clean, alpha = C.key_green(rgb)
rgba = np.dstack([clean, alpha]).astype(np.uint8); h, w = alpha.shape
split = int(sys.argv[4]) if len(sys.argv) > 4 else h // 2
edges = [0, split, h]
cells = [C.despeckle(rgba[edges[r]:edges[r+1], c*w//2:(c+1)*w//2].copy()) for r in range(2) for c in range(2)]
ref = np.array(Image.open(ref_png)); rm = C.mask(ref); H, W = rm.shape
rx0, ry0, rx1, ry1 = C.bbox(rm)
# scale: legs (lower 45%) width of the copy frame vs the reference
def lowwidth(m):
    x0,y0,x1,y1 = C.bbox(m); band = m[y0+int((y1-y0)*0.6):y1+1]; xs = np.nonzero(band.any(0))[0]; return xs.max()-xs.min()
k = lowwidth(rm) / lowwidth(C.mask(cells[0]))
print("scale", k)
for i, a in enumerate(cells):
    a = C.scaled(a, k); m = C.mask(a); x0,y0,x1,y1 = C.bbox(m)
    ox, oy = rx1 - x1, ry1 - y1
    cand = np.zeros((H, W), bool); C.paste_mask(cand, m, ox, oy)
    dx, dy = C.best_shift(rm, cand, (0.55, 1.0))
    o = np.zeros((H, W, 4), np.uint8); C.paste(o, a, ox+dx, oy+dy)
    Image.fromarray(o).save(f"{out}/deep_{i}.png")
