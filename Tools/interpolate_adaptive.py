#!/usr/bin/env python3
"""Adaptive frame doubling for punches: in-betweens only where RIFE stays clean, extra ones in the calm phases.

For every gap between neighbouring frames a test in-between is computed and scored for smearing (dark blobs that
neither neighbour has, alpha mass that does not fit between them). Smeared gaps get no in-between (the strike stays
snappy); the frames are spent on the calmest clean gaps instead (up to 3 per gap, at t = k/(n+1)), so the clip still
reaches the target count. Writes keys.json next to the frames: the new index of every original frame (the game uses it
to time hits). Originals are backed up to ArtSource/Frames8/<path>.
Usage: Tools/interpolate_adaptive.py <rife_dir> [--target N] [--once] [--report] [--no-backup] <clip folder>...
"""
import glob, json, os, shutil, sys
import numpy as np
import torch
from PIL import Image

args = sys.argv[1:]
rife_dir = args.pop(0)
target = 16
if "--target" in args:
    i = args.index("--target"); target = int(args[i + 1]); del args[i:i + 2]
once = "--once" in args
report = "--report" in args
no_backup = "--no-backup" in args  # for folders outside Assets/_Project/Art (keep your own copy)
args = [a for a in args if a not in ("--once", "--report", "--no-backup")]
project = os.environ.get("PROJECT_DIR", os.getcwd())

sys.path.insert(0, rife_dir)
os.chdir(rife_dir)
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


def between(a, b, t):
    h, w = a.shape[:2]
    rgb = model.inference(tensor(a[..., :3] * a[..., 3:]), tensor(b[..., :3] * b[..., 3:]), t)[0].permute(1, 2, 0).numpy()[:h, :w]
    alpha = model.inference(tensor(np.repeat(a[..., 3:], 3, 2)), tensor(np.repeat(b[..., 3:], 3, 2)), t)[0, 0].numpy()[:h, :w].clip(0, 1)
    alpha[alpha < 0.03] = 0
    colour = np.where(alpha[..., None] > 1e-3, rgb / np.maximum(alpha[..., None], 1e-3), 0).clip(0, 1)
    return np.dstack([colour, alpha])


def dark_area(f):
    lum = f[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
    return float(((lum < 0.14) & (f[..., 3] > 0.5)).sum())


def smear_score(a, m, b):
    """How much the in-between looks like a smear: extra dark area and alpha mass versus its neighbours."""
    body = max(1.0, (a[..., 3] > 0.5).sum() * 0.5 + (b[..., 3] > 0.5).sum() * 0.5)
    dark = (dark_area(m) - max(dark_area(a), dark_area(b))) / body
    mass = abs((m[..., 3] > 0.5).sum() - 0.5 * ((a[..., 3] > 0.5).sum() + (b[..., 3] > 0.5).sum())) / body
    return max(0.0, dark) + 0.5 * mass


def motion(a, b):
    return float(np.abs(a[..., 3] - b[..., 3]).mean())


def process(folder):
    files = sorted(glob.glob(os.path.join(folder, "*.png")))
    frames = [np.array(Image.open(f).convert("RGBA")).astype(np.float32) / 255 for f in files]
    n = len(frames)
    gaps = list(range(n if not once else n - 1))
    pair = lambda g: (frames[g], frames[(g + 1) % n])
    scores = {g: smear_score(pair(g)[0], between(*pair(g), 0.5), pair(g)[1]) for g in gaps}
    moves = {g: motion(*pair(g)) for g in gaps}
    clean = [g for g in gaps if scores[g] < 0.02]
    extra = {g: 0 for g in gaps}
    need = target - n
    # one in-between per clean gap, then more in the calmest clean gaps
    for g in sorted(clean, key=lambda g: moves[g]):
        if need == 0: break
        extra[g] = 1; need -= 1
    for level in (2, 3):
        for g in sorted(clean, key=lambda g: moves[g]):
            if need == 0: break
            if extra[g] == level - 1:
                extra[g] = level; need -= 1
    name = os.path.relpath(folder, project)
    print(f"{name}: smear {[round(scores[g], 3) for g in gaps]} -> extra {[extra[g] for g in gaps]} = {n + sum(extra.values())} frames")
    if report:
        return
    out, keys = [], []
    for i, frame in enumerate(frames):
        keys.append(len(out)); out.append(frame)
        if i in extra:
            a, b = pair(i)
            k = extra[i]
            out += [between(a, b, j / (k + 1)) for j in range(1, k + 1)]
    backup = os.path.join(project, "ArtSource", "Frames8", os.path.relpath(folder, os.path.join(project, "Assets/_Project/Art")))
    if not no_backup and not os.path.exists(backup):
        os.makedirs(backup)
        for f in files:
            shutil.copy(f, backup)
    stem = os.path.basename(files[0]).rsplit("_", 1)[0]
    for f in glob.glob(os.path.join(folder, "*.png*")) + glob.glob(os.path.join(folder, "keys.json*")):
        os.remove(f)
    for i, frame in enumerate(out):
        Image.fromarray((frame * 255).round().astype(np.uint8), "RGBA").save(os.path.join(folder, f"{stem}_{i:02d}.png"))
    with open(os.path.join(folder, "keys.json"), "w") as f:
        json.dump({"keys": keys}, f)


for folder in args:
    process(folder if os.path.isabs(folder) else os.path.join(project, folder))
