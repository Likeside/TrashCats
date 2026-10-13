#!/usr/bin/env python3
"""Animation QA: checks every clip the way the game plays it and draws onion skins for a visual pass.

Reads TestResults/clips.json (written by TrashCats/Art/Rebuild Character Art Registry): frame files, fps, loop,
ping-pong, drawn key frames. For each clip, in playback order:
  size    — character size per frame (sqrt of silhouette area vs the clip median); flags drift > 8%
  drift   — horizontal shift of the silhouette against the first frame (best overlap); for clips that stand in place
  seam    — "saw" in loops: change at the wrap (last played frame -> first) vs the median step; flags > 1.8x
  spike   — single steps much bigger than the median (pops); flags > 3x (informational for strikes)
  smear   — dark blobs that neither neighbour has (bad in-betweens)
Writes TestResults/anim-qa.md (table, worst first) and TestResults/anim-qa/<character>_<role>.png: an onion skin
(all frames tinted from blue to red over each other, feet line marked) above a contact strip with frame numbers.
Usage: Tools/anim_qa.py [--only substring] [--no-images]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw

MANIFEST = "TestResults/clips.json"
OUT_MD = "TestResults/anim-qa.md"
OUT_DIR = "TestResults/anim-qa"

# clips that should stand on one spot (drift is a defect); strikes and hits move on purpose
IN_PLACE = {"idle", "hideoutIdle", "run", "canPunch", "rope", "dig"}
# clips the game does not animate (the wheel spins procedurally over its first frame)
SKIP = {"wheel/idle"}
LIMITS = {"size": 0.08, "drift": 0.04, "seam": 1.8, "spike": 3.0, "smear": 0.03}


def load(path):
    return np.array(Image.open(path).convert("RGBA")).astype(np.float32) / 255


def common(frames):
    h = max(f.shape[0] for f in frames); w = max(f.shape[1] for f in frames)
    out = []
    for f in frames:  # pivot is bottom centre: align frames on it
        c = np.zeros((h, w, 4), np.float32)
        y0 = h - f.shape[0]; x0 = (w - f.shape[1]) // 2
        c[y0:, x0:x0 + f.shape[1]] = f
        out.append(c)
    return out


def step(a, b):
    return float(np.abs(a[..., 3] - b[..., 3]).mean())


def shift_x(ref, f, max_px, upper=False):
    """Horizontal shift (px) that best overlays f on ref: by the legs (lower third), or by the upper body for clips
    whose legs move on purpose (run)."""
    k = 4
    rows = np.nonzero((ref[..., 3] > 0.5).any(1))[0]
    lo, hi = (rows.min(), rows.min() + int((rows.max() - rows.min()) * 0.5)) if upper else (rows.min() + int((rows.max() - rows.min()) * 0.67), rows.max() + 1)
    r = ref[lo:hi:k, ::k, 3] > 0.5; g = f[lo:hi:k, ::k, 3] > 0.5
    best, best_dx = -1, 0
    for dx in range(-max_px // k, max_px // k + 1):
        rolled = np.roll(g, dx, axis=1)
        score = (r & rolled).sum()
        if score > best:
            best, best_dx = score, dx
    return best_dx * k


def dark(f):
    lum = f[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
    return float(((lum < 0.14) & (f[..., 3] > 0.5)).sum())


def order(clip, n):
    if clip["pingPong"] and n > 1:
        return list(range(n)) + list(range(n - 2, 0, -1))
    return list(range(n))


def check(clip):
    frames = common([load(p) for p in clip["frames"]])
    n = len(frames)
    seq = order(clip, n)
    h = frames[0].shape[0]
    areas = [max(1.0, (f[..., 3] > 0.5).sum()) for f in frames]
    med = float(np.median(areas))
    sizes = [(a / med) ** 0.5 for a in areas]
    res = {"frames": n, "issues": []}
    worst = max(sizes, key=lambda s: abs(s - 1))
    res["size"] = worst
    if abs(worst - 1) > LIMITS["size"] and clip["role"] not in ("ko",):
        res["issues"].append(f"size {worst:.2f} (frames {[i for i, s in enumerate(sizes) if abs(s - 1) > LIMITS['size']]})")
    if clip["role"] in IN_PLACE and n > 1:
        shifts = [shift_x(frames[0], f, 80, upper=clip["role"] == "run") for f in frames]
        worst = max(shifts, key=abs)
        res["drift"] = worst / h
        if abs(worst) / h > LIMITS["drift"]:
            res["issues"].append(f"drift {worst:+d}px (frames {[i for i, s in enumerate(shifts) if abs(s) / h > LIMITS['drift']]})")
    if n > 2:
        steps = [step(frames[seq[i]], frames[seq[i + 1]]) for i in range(len(seq) - 1)]
        med_step = max(1e-4, float(np.median(steps)))
        if clip["loop"]:
            seam = step(frames[seq[-1]], frames[seq[0]]) / med_step
            res["seam"] = seam
            if seam > LIMITS["seam"]:
                res["issues"].append(f"saw: loop seam {seam:.1f}x the usual step")
        spikes = [(i, s / med_step) for i, s in enumerate(steps) if s / med_step > LIMITS["spike"]]
        if spikes and clip["role"] in IN_PLACE:
            res["issues"].append("pops at " + ", ".join(f"{seq[i]}->{seq[i + 1]} ({r:.1f}x)" for i, r in spikes))
        body = max(1.0, med)
        # only in-between frames can be smeared: frames not in keys (adaptive) or the odd ones of a plain doubling
        drawn = set(clip["keys"]) if clip["keys"] else (set(range(0, n, 2)) if n >= 15 else set(range(n)))
        smears = [i for i in range(1, n - 1) if i not in drawn
                  and (dark(frames[i]) - max(dark(frames[i - 1]), dark(frames[i + 1]))) / body > LIMITS["smear"]]
        if smears:
            res["issues"].append(f"smear frames {smears}")
    return res, frames


def draw(clip, frames, path):
    h, w = frames[0].shape[:2]
    n = len(frames)
    onion = np.zeros((h, w, 3), np.float32)
    weight = np.zeros((h, w, 1), np.float32)
    for i, f in enumerate(frames):
        t = i / max(1, n - 1)
        tint = np.array([t, 0.25, 1 - t], np.float32)
        a = f[..., 3:4] * 0.5
        onion += tint * a; weight += a
    onion = np.where(weight > 0, onion / np.maximum(weight, 1e-3), 0.12)
    img = Image.fromarray((onion * 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    d.line([(0, h - 2), (w, h - 2)], fill=(255, 255, 0))
    thumb_h = 140
    thumbs = []
    for f in frames:
        t = Image.fromarray((f * 255).astype(np.uint8), "RGBA")
        t.thumbnail((int(thumb_h * w / h), thumb_h))
        bg = Image.new("RGBA", t.size, (70, 55, 80, 255)); bg.alpha_composite(t)
        thumbs.append(bg.convert("RGB"))
    cols = min(n, 8)
    tw = max(t.width for t in thumbs)
    strip = Image.new("RGB", (cols * (tw + 4), ((n + cols - 1) // cols) * (thumb_h + 18)), (30, 30, 30))
    sd = ImageDraw.Draw(strip)
    for i, t in enumerate(thumbs):
        x = (i % cols) * (tw + 4); y = (i // cols) * (thumb_h + 18)
        strip.paste(t, (x, y + 16)); sd.text((x + 2, y + 2), str(i), fill=(255, 255, 255))
    scale = min(1.0, 400 / h)
    img = img.resize((int(w * scale), int(h * scale)))
    sheet = Image.new("RGB", (max(img.width, strip.width), img.height + strip.height + 6), (20, 20, 20))
    sheet.paste(img, (0, 0)); sheet.paste(strip, (0, img.height + 6))
    sheet.save(path)


def main():
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else ""
    images = "--no-images" not in sys.argv
    clips = [c for c in json.load(open(MANIFEST)) if only in f"{c['character']}/{c['role']}" and f"{c['character']}/{c['role']}" not in SKIP]
    os.makedirs(OUT_DIR, exist_ok=True)
    rows = []
    for clip in clips:
        res, frames = check(clip)
        name = f"{clip['character']}/{clip['role']}"
        if images:
            draw(clip, frames, os.path.join(OUT_DIR, f"{clip['character']}_{clip['role']}.png"))
        rows.append((len(res["issues"]), name, clip, res))
        print(f"{name}: {'; '.join(res['issues']) or 'ok'}")
    rows.sort(key=lambda r: -r[0])
    with open(OUT_MD, "w") as f:
        f.write("# Animation QA\n\nLimits: size ±8%, drift 4% of height (in-place clips), loop seam 1.8x, pops 3x, smear 3% dark area.\n"
                "Onion skins: TestResults/anim-qa/<character>_<role>.png (blue = first frame, red = last).\n\n")
        f.write("| Clip | Frames | fps | Loop | Size | Drift | Seam | Issues |\n|---|---|---|---|---|---|---|---|\n")
        for _, name, clip, res in rows:
            loop = "ping-pong" if clip["pingPong"] else "loop" if clip["loop"] else "once"
            f.write(f"| {name} | {res['frames']} | {clip['fps']:g} | {loop} | {res.get('size', 1):.2f} | "
                    f"{res.get('drift', 0):+.3f} | {res.get('seam', 0):.1f} | {'; '.join(res['issues']) or 'ok'} |\n")
    print(f"{sum(1 for r in rows if r[0])} of {len(rows)} clips with issues -> {OUT_MD}")


if __name__ == "__main__":
    main()
