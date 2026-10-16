#!/usr/bin/env python3
"""Moves the head of every key frame to one place with a smooth local warp (no cut seams) - Ryzhy bench, 2026-10-16.
The face template box (x0..y1) is in the frame of the bench canvas. Usage: head_lock.py <dir with key_N.png> <n> <ref key> <head cx> <head cy> <target dx> <target dy>
"""
import sys, numpy as np
from PIL import Image
from scipy import ndimage
d, n, ref, cx, cy = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
tx, ty = float(sys.argv[6]), float(sys.argv[7])  # target offset of the head relative to the ref key
keys=[np.array(Image.open(f"{d}/key_{i}.png")).astype(np.float32) for i in range(n)]
x0,y0,x1,y1 = 280,128,335,165
T = keys[ref][y0:y1, x0:x1]
def find(k):
    best=None
    for dy in range(-25,26):
        for dx in range(-25,26):
            e = np.abs(k[y0+dy:y1+dy, x0+dx:x1+dx]-T).mean()
            if best is None or e<best[0]: best=(e,dx,dy)
    return best[1], best[2]
H, W = keys[0].shape[:2]
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
for i,k in enumerate(keys):
    ox, oy = find(k)
    Dx, Dy = tx-ox, ty-oy
    r = np.hypot(xx-(cx+ox), yy-(cy+oy))
    w = np.clip((75-r)/(75-32), 0, 1); w = w*w*(3-2*w)
    sx, sy = xx - w*Dx, yy - w*Dy
    out = np.stack([ndimage.map_coordinates(k[...,c], [sy, sx], order=1, mode='constant') for c in range(4)], -1)
    # premultiplied-ish fix: keep colours of transparent pixels from bleeding
    Image.fromarray(np.clip(out,0,255).astype(np.uint8)).save(f"{d}/warped_{i}.png")
    print(i, (ox,oy), "moved by", (Dx,Dy))
