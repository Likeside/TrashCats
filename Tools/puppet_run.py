#!/usr/bin/env python3
"""Builds a correct run cycle from cut-out parts: the image models cannot swap which leg is in front, so the legs are
animated here. Parts sheet (green): left blob = body without hind legs (side view facing left), right blob = one hind
leg standing vertically. 16 frames: the legs swing about the hip in opposite phase (sin), the far leg darker and behind
the body, the swinging leg shortened a little (knee lift), the body bobs twice per cycle.
Usage: Tools/puppet_run.py <parts.png> <out_dir> <name> [--hip-dx F] [--hip-dy F] [--swing DEG] [--bob PX] [--leg-scale F]
"""
import math, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage
sys.path.insert(0, os.path.dirname(__file__))
from sprite_process import key_green

args = sys.argv[1:]
def opt(name, default):
    if name in args:
        i = args.index(name); v = float(args[i + 1]); del args[i:i + 2]; return v
    return default
hip_dx, hip_dy, swing, bob = opt("--hip-dx", 0.0), opt("--hip-dy", 0.0), opt("--swing", 30.0), opt("--bob", 6.0)
leg_scale = opt("--leg-scale", 1.0)
src, out_dir, name = args

rgb = np.array(Image.open(src).convert("RGB"))
clean, alpha = key_green(rgb)
labels, n = ndimage.label(ndimage.binary_dilation(alpha > 128, iterations=6))
blobs = sorted([(sl, i) for i, sl in enumerate(ndimage.find_objects(labels), 1) if (labels[sl] == i).sum() > 5000], key=lambda b: b[0][1].start)
def cut(sl, i):
    a = np.where(labels[sl] == i, alpha[sl], 0).astype(np.uint8)
    return Image.fromarray(np.dstack([clean[sl], a]), "RGBA")
body, leg = cut(*blobs[0]), cut(*blobs[-1])
leg = leg.crop(leg.getchannel("A").getbbox()); body = body.crop(body.getchannel("A").getbbox())
if leg_scale != 1.0:
    leg = leg.resize((max(1, int(leg.width * leg_scale)), max(1, int(leg.height * leg_scale))), Image.LANCZOS)

# hip: bottom of the torso (the left part of the body's lowest rows; the tail sticks out to the right)
ba = np.array(body)[..., 3] > 128
ys, xs = np.nonzero(ba)
low = ys.max()
rows = ba[low - int(0.12 * body.height):low + 1]
cols = np.nonzero(rows.any(0))[0]
torso = cols[cols < cols.min() + 0.45 * (cols.max() - cols.min()) + 1]
hip = (float(torso.mean()) + hip_dx * body.width, low - 0.06 * body.height + hip_dy * body.height)

la = np.array(leg)[..., 3] > 128
top_cols = np.nonzero(la[: max(2, int(0.1 * leg.height))].any(0))[0]
leg_pivot = (float(top_cols.mean()), 0.06 * leg.height)

def dark(im, k=0.62):
    a = np.array(im).astype(np.float32); a[..., :3] *= k
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")

def place(canvas, part, pivot, at, angle, squash):
    """Rotate part about pivot (positive angle = foot swings forward, i.e. to the left) and paste with pivot at `at`."""
    p = part.resize((part.width, max(1, int(part.height * squash))))
    pv = (pivot[0], pivot[1] * squash)
    big = Image.new("RGBA", (p.width * 3, p.height * 3), (0, 0, 0, 0))
    off = (int(p.width * 1.5 - pv[0]), int(p.height * 1.5 - pv[1]))
    big.paste(p, off)
    big = big.rotate(-angle, resample=Image.BICUBIC, center=(p.width * 1.5, p.height * 1.5))
    canvas.alpha_composite(big, (int(at[0] - p.width * 1.5), int(at[1] - p.height * 1.5)))

W = body.width + leg.width * 2; H = int(body.height + leg.height * 1.1)
origin = (leg.width, 10)
os.makedirs(out_dir, exist_ok=True)
for f in range(16):
    p = f / 16.0
    near = swing * math.sin(2 * math.pi * p)
    far = swing * math.sin(2 * math.pi * p + math.pi)
    # the leg swinging forward (angle rising) is lifted: shorter while it passes under the body
    lift = lambda ph: 1.0 - 0.14 * max(0.0, math.cos(2 * math.pi * ph))
    dy = bob * math.cos(4 * math.pi * p)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    hip_at = (origin[0] + hip[0], origin[1] + hip[1] + dy)
    place(canvas, dark(leg), leg_pivot, (hip_at[0] + 0.04 * body.width, hip_at[1]), -far, lift(p + 0.5 + 0.25))
    canvas.alpha_composite(body, (origin[0], int(origin[1] + dy)))
    place(canvas, leg, leg_pivot, hip_at, -near, lift(p + 0.25))
    canvas.save(os.path.join(out_dir, f"{name}_{f:02d}.png"))
print(name, "hip", [round(v) for v in hip], "body", body.size, "leg", leg.size)
