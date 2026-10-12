#!/usr/bin/env python3
"""Turns a generated sprite strip on a green chroma background into clean animation frames.

- Keys out the green background (with soft edges) and removes green spill from the outline.
- Finds frames as connected blobs (ignores small specks), left to right.
- Places every frame into equal cells, centred horizontally; vertical offsets from the sheet's ground line are kept.

Usage: Tools/sprite_process.py <sheet.png> <out_dir> [--frames N] [--cell-height H]
Writes <name>_<i>.png frames and <name>_strip.png (frames side by side on a checkerboard) for review.
"""
import os, sys
import numpy as np
from PIL import Image
from collections import deque


def key_green(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    greenness = g - np.maximum(r, b)              # how much green dominates
    alpha = np.clip(1.0 - (greenness - 40.0) / 80.0, 0.0, 1.0)  # >120: background, <40: opaque
    spill = np.clip(g - np.maximum(r, b), 0, None)
    g2 = g - spill * (1.0 - alpha * 0.0)           # clamp green to max(r, b) where it dominates
    out = np.stack([r, np.minimum(g2, np.maximum(r, b) + 0), b], axis=-1)
    out = np.where((greenness > 0)[..., None], out, rgb.astype(np.float32))
    return out.clip(0, 255).astype(np.uint8), (alpha * 255).astype(np.uint8)


def label_blobs(mask):
    """4-connected components; returns list of (y0, y1, x0, x1, pixel_count)."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    labels = np.zeros(mask.shape, dtype=np.int32)
    blobs = []
    for y0 in range(h):
        for x0 in np.flatnonzero(mask[y0] & ~seen[y0]):
            if seen[y0, x0]:
                continue
            queue = deque([(y0, x0)])
            seen[y0, x0] = True
            label = len(blobs) + 1
            ys0, ys1, xs0, xs1, count = y0, y0, x0, x0, 0
            while queue:
                y, x = queue.popleft()
                labels[y, x] = label
                count += 1
                ys0, ys1, xs0, xs1 = min(ys0, y), max(ys1, y), min(xs0, x), max(xs1, x)
                for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
            blobs.append((ys0, ys1 + 1, xs0, xs1 + 1, count))
    return blobs, labels


def find_frames(alpha, expected=None, scale=4):
    """
    Frames are the large blobs; small detached bits (sparks, whisker tips) join the nearest frame.
    Returns (bounding slices, full-resolution ownership mask) per frame, so overlapping boxes do not mix frames.
    """
    small = alpha[::scale, ::scale] > 32
    blobs, labels = label_blobs(small)
    total = small.sum()
    big = sorted([i for i, b in enumerate(blobs) if b[4] > total * 0.03], key=lambda i: blobs[i][2])
    owner = {i + 1: n for n, i in enumerate(big)}
    for i, b in enumerate(blobs):
        if i in big:
            continue
        cx = (b[2] + b[3]) / 2
        owner[i + 1] = min(range(len(big)), key=lambda n: abs((blobs[big[n]][2] + blobs[big[n]][3]) / 2 - cx))

    frame_map = np.zeros(labels.shape, dtype=np.int32) - 1
    for label, n in owner.items():
        frame_map[labels == label] = n
    # grow ownership by one cell so anti-aliased edges lost by downsampling stay with their frame
    grown = frame_map.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            shifted = np.roll(np.roll(frame_map, dy, axis=0), dx, axis=1)
            grown = np.where(grown < 0, shifted, grown)
    full = np.kron(grown, np.ones((scale, scale), dtype=np.int32))[: alpha.shape[0], : alpha.shape[1]]

    frames = []
    for n in range(len(big)):
        own = full == n
        ys, xs = np.nonzero(own & (alpha > 0))
        frames.append(((slice(ys.min(), ys.max() + 1), slice(xs.min(), xs.max() + 1)), own))
    return frames


def main():
    src, out_dir = sys.argv[1], sys.argv[2]
    expected = int(sys.argv[sys.argv.index("--frames") + 1]) if "--frames" in sys.argv else None
    cell_h = int(sys.argv[sys.argv.index("--cell-height") + 1]) if "--cell-height" in sys.argv else None
    name = os.path.splitext(os.path.basename(src))[0]
    rgb = np.array(Image.open(src).convert("RGB"))
    color, alpha = key_green(rgb)
    rgba = np.dstack([color, alpha])
    frames = find_frames(alpha, expected)
    if expected and len(frames) != expected:
        print(f"WARNING {name}: found {len(frames)} frames, expected {expected}")

    pad = 16
    ground = max(ys.stop for (ys, _), _ in frames)       # lowest point of the sheet = ground line
    width = max(xs.stop - xs.start for (_, xs), _ in frames) + pad * 2
    height = max(ground - ys.start for (ys, _), _ in frames) + pad * 2
    os.makedirs(out_dir, exist_ok=True)
    cells = []
    for i, ((ys, xs), own) in enumerate(frames):
        pixels = rgba.copy()
        pixels[..., 3] = np.where(own, pixels[..., 3], 0)
        crop = Image.fromarray(pixels[ys, xs], "RGBA")
        cell = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        lift = ground - ys.stop                            # keeps jumps in the air
        cell.paste(crop, ((width - crop.width) // 2, height - pad - lift - crop.height))
        if cell_h:
            cell = cell.resize((round(width * cell_h / height), cell_h), Image.LANCZOS)
        cell.save(os.path.join(out_dir, f"{name}_{i}.png"))
        cells.append(cell)

    # review strip on a checkerboard so edges and fringes are visible
    cw, ch = cells[0].size
    strip = Image.new("RGBA", (cw * len(cells), ch), (0, 0, 0, 255))
    checker = np.indices((ch, cw * len(cells))).sum(axis=0) // 16 % 2
    strip = Image.fromarray(np.where(checker[..., None] == 0, 200, 150).astype(np.uint8).repeat(3, axis=2)).convert("RGBA")
    for i, cell in enumerate(cells):
        strip.alpha_composite(cell, (i * cw, 0))
    strip.convert("RGB").save(os.path.join(out_dir, f"{name}_strip.png"))
    print(f"{name}: {len(cells)} frames, cell {cw}x{ch}")


if __name__ == "__main__":
    main()
