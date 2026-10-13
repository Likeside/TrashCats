#!/usr/bin/env python3
"""Doubles the frame count of sprite clips with RIFE frame interpolation (in-between frames at t = 0.5).

RGBA is handled by interpolating premultiplied colour and alpha separately. Looping clips also get a frame between
the last and the first (8 -> 16); one-shot clips get n -> 2n-1. Originals are copied to ArtSource/Frames8/<path>
before the folder is rewritten (art versions are never lost). Frame files keep the <name>_NN.png pattern.
Usage: Tools/interpolate_frames.py <rife_dir> [--out DIR] [--once] <clip folder>...
  rife_dir: a Practical-RIFE checkout with train_log/ (v4.25 weights); needs torch + torchvision (CPU is fine).
  --out DIR: write the doubled frames to DIR/<clip folder name> instead of replacing the clip (for trials).
"""
import glob, os, shutil, sys
import numpy as np
import torch
from PIL import Image

args = sys.argv[1:]
rife_dir = args.pop(0)
out_dir = None
if "--out" in args:
    i = args.index("--out"); out_dir = args[i + 1]; del args[i:i + 2]
once = "--once" in args
args = [a for a in args if a != "--once"]

sys.path.insert(0, rife_dir)
os.chdir(rife_dir)  # the model code imports model.* relative to its checkout
from train_log.RIFE_HDv3 import Model  # noqa: E402

torch.set_grad_enabled(False)
model = Model()
model.load_model(os.path.join(rife_dir, "train_log"), -1)
model.eval()


def tensor(x):
    h, w = x.shape[:2]
    H, W = (h + 63) // 64 * 64, (w + 63) // 64 * 64
    padded = np.zeros((H, W, 3), np.float32)
    padded[:h, :w] = x
    return torch.from_numpy(padded).permute(2, 0, 1)[None]


def middle(a, b):
    h, w = a.shape[:2]
    rgb = model.inference(tensor(a[..., :3] * a[..., 3:]), tensor(b[..., :3] * b[..., 3:]), 0.5)[0].permute(1, 2, 0).numpy()[:h, :w]
    alpha = model.inference(tensor(np.repeat(a[..., 3:], 3, 2)), tensor(np.repeat(b[..., 3:], 3, 2)), 0.5)[0, 0].numpy()[:h, :w].clip(0, 1)
    alpha[alpha < 0.03] = 0
    colour = np.where(alpha[..., None] > 1e-3, rgb / np.maximum(alpha[..., None], 1e-3), 0).clip(0, 1)
    return np.dstack([colour, alpha])


def double(folder):
    files = sorted(f for f in glob.glob(os.path.join(folder, "*.png")))
    if len(files) < 2:
        return
    frames = [np.array(Image.open(f).convert("RGBA")).astype(np.float32) / 255 for f in files]
    stem = os.path.basename(files[0]).rsplit("_", 1)[0]
    result = []
    for i, frame in enumerate(frames):
        result.append(frame)
        if i + 1 < len(frames):
            result.append(middle(frame, frames[i + 1]))
        elif not once:
            result.append(middle(frame, frames[0]))
    target = os.path.join(out_dir, os.path.basename(os.path.normpath(folder))) if out_dir else folder
    if not out_dir:
        backup = os.path.join(project, "ArtSource", "Frames8", os.path.relpath(folder, os.path.join(project, "Assets/_Project/Art")))
        if not os.path.exists(backup):
            os.makedirs(backup)
            for f in files:
                shutil.copy(f, backup)
        for f in glob.glob(os.path.join(folder, "*.png*")):
            os.remove(f)
    os.makedirs(target, exist_ok=True)
    for i, frame in enumerate(result):
        Image.fromarray((frame * 255).round().astype(np.uint8), "RGBA").save(os.path.join(target, f"{stem}_{i:02d}.png"))
    print(f"{folder}: {len(frames)} -> {len(result)}")


project = os.environ.get("PROJECT_DIR", os.getcwd())
for folder in args:
    double(folder if os.path.isabs(folder) else os.path.join(project, folder))
