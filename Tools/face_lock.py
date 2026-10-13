#!/usr/bin/env python3
"""Keeps one facial expression through a clip: finds the head in every frame (template match on the head of the
reference frame, translation + small scale) and pastes the reference face (an ellipse over eyes, muzzle and mouth)
with a soft edge. Pixels of the target that look like the barbell (brown wood, grey cans) are kept on top.
Usage: Tools/face_lock.py <folder> <ref index> <head box x0,y0,x1,y1> <face ellipse cx,cy,rx,ry> [--frames 0-15]
Writes in place; originals go to <folder>_faces_before/.
"""
import glob, os, shutil, sys
import numpy as np
from PIL import Image
from scipy import ndimage

folder, ref_i = sys.argv[1], int(sys.argv[2])
hx0, hy0, hx1, hy1 = map(int, sys.argv[3].split(","))
cx, cy, rx, ry = map(int, sys.argv[4].split(","))
rng = None
if "--frames" in sys.argv:
    a, b = sys.argv[sys.argv.index("--frames") + 1].split("-"); rng = range(int(a), int(b) + 1)

files = sorted(glob.glob(folder + "/*.png"))
backup = folder + "_faces_before"
if not os.path.exists(backup):
    shutil.copytree(folder, backup)
ref = np.array(Image.open(files[ref_i]).convert("RGBA")).astype(np.float32)

def gray(a):  # luminance where opaque, a neutral grey elsewhere
    g = a[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
    return np.where(a[..., 3] > 128, g, 128.0)

tmpl = gray(ref[hy0:hy1, hx0:hx1])
tmpl = (tmpl - tmpl.mean()) / (tmpl.std() + 1e-3)
H, W = ref.shape[:2]
yy, xx = np.mgrid[0:H, 0:W]
for i, f in enumerate(files):
    if i == ref_i or (rng and i not in rng):
        continue
    img = Image.open(f).convert("RGBA"); a = np.array(img).astype(np.float32)
    best = (-1e9, 0, 0, 1.0)
    for s in (0.94, 0.97, 1.0, 1.03, 1.06):
        t = tmpl if s == 1.0 else np.array(Image.fromarray(tmpl).resize((max(4, int(tmpl.shape[1] * s)), max(4, int(tmpl.shape[0] * s)))))
        g = gray(a)
        th, tw = t.shape
        for dy in range(-60, 61, 2):
            for dx in range(-40, 41, 2):
                y0 = int(hy0 + dy - (th - (hy1 - hy0)) / 2); x0 = int(hx0 + dx - (tw - (hx1 - hx0)) / 2)
                if y0 < 0 or x0 < 0 or y0 + th > H or x0 + tw > W: continue
                p = g[y0:y0 + th, x0:x0 + tw]; p = (p - p.mean()) / (p.std() + 1e-3)
                sc = float((p * t).mean())
                if sc > best[0]: best = (sc, dx, dy, s)
    sc, dx, dy, s = best
    # reference face, scaled about the head-box centre and moved by (dx, dy)
    hcx, hcy = (hx0 + hx1) / 2, (hy0 + hy1) / 2
    src = Image.fromarray(ref.astype(np.uint8), "RGBA")
    big = src.resize((int(W * s), int(H * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    canvas.alpha_composite(big, (int(round(hcx + dx - hcx * s)), int(round(hcy + dy - hcy * s))))
    face = np.array(canvas).astype(np.float32)
    ecx, ecy = hcx + dx + (cx - hcx) * s, hcy + dy + (cy - hcy) * s
    e = ((xx - ecx) / (rx * s)) ** 2 + ((yy - ecy) / (ry * s)) ** 2
    m = np.clip((1.0 - e) / 0.35, 0, 1)                   # soft edge
    m *= (face[..., 3] > 128) & (a[..., 3] > 128)
    r, g_, b = a[..., 0], a[..., 1], a[..., 2]
    bar = ((r > 70) & (r < 170) & (g_ > 35) & (g_ < 110) & (b < 70) & (r - b > 50) & (r < 1.9 * g_)) | ((np.abs(r - g_) < 14) & (np.abs(g_ - b) < 14) & (r > 120))
    bar = ndimage.binary_dilation(bar, iterations=1)
    m *= ~bar
    out = a.copy()
    out[..., :3] = a[..., :3] * (1 - m[..., None]) + face[..., :3] * m[..., None]
    Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA").save(f)
    print(os.path.basename(f), "match", round(sc, 2), "shift", dx, dy, "scale", s)
