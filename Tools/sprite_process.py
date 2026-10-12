#!/usr/bin/env python3
"""Turns a generated sprite strip on a green chroma background into clean animation frames.

- Keys out the green background (with soft edges) and removes green spill from the outline.
- Finds frames as connected blobs (ignores small specks), left to right.
- Places every frame into equal cells around a shared anchor x; vertical offsets from the row's ground line are kept.

Usage: Tools/sprite_process.py <sheet.png> <out_dir> [--frames N] [--rows R] [--cell-height H] [--anchor backfoot|center|base-br]
       [--match-height H [--match-frame K]]

Anchors: "base-br" puts the bottom-right extreme point (e.g. the cut base of a detached tail) at the sprite pivot.
"backfoot" (default) keeps the planted back foot (leftmost foot touching the ground; characters face right) at the
same x in every frame, so idle loops do not slide when the tail swings. "center" centres the bounding box (for jumps).
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


def find_frames(alpha, expected=None, scale=4, rows=1):
    """
    Frames are the large blobs; small detached bits (sparks, whisker tips) join the nearest frame.
    Returns (bounding slices, full-resolution ownership mask) per frame, so overlapping boxes do not mix frames.
    """
    small = alpha[::scale, ::scale] > 32
    blobs, labels = label_blobs(small)
    total = small.sum()
    row_height = small.shape[0] / rows

    def order(i):
        y0, y1, x0, x1, _ = blobs[i]
        return int(((y0 + y1) / 2) // row_height), x0

    big = sorted([i for i, b in enumerate(blobs) if b[4] > total * 0.015], key=order)
    owner = {i + 1: n for n, i in enumerate(big)}
    for i, b in enumerate(blobs):
        if i in big:
            continue
        cx, cy = (b[2] + b[3]) / 2, (b[0] + b[1]) / 2

        def gap(n):  # distance from the fragment centre to frame n's box (0 inside)
            y0, y1, x0, x1, _ = blobs[big[n]]
            return max(x0 - cx, 0, cx - x1) + max(y0 - cy, 0, cy - y1)

        nearest = min(range(len(big)), key=gap)
        if gap(nearest) <= 12:  # ~48 px at scale 4: sparks and whisker tips next to a frame; farther specks are dropped
            owner[i + 1] = nearest

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
        frames.append(((slice(ys.min(), ys.max() + 1), slice(xs.min(), xs.max() + 1)), own, order(big[n])[0]))
    return frames


def back_foot_x(alpha):
    """x of the leftmost foot that touches the ground (bottom rows of the figure), in the crop's coordinates."""
    mask = alpha > 64
    ys, xs = np.nonzero(mask)
    bottom, top = ys.max(), ys.min()
    band_top = bottom - max(4, int((bottom - top) * 0.07))
    band = mask[band_top: bottom + 1]
    cols = np.flatnonzero(band.any(axis=0))
    runs, start = [], cols[0]
    for prev, col in zip(cols, cols[1:]):
        if col != prev + 1:
            runs.append((start, prev))
            start = col
    runs.append((start, cols[-1]))
    # a foot reaches the ground; a dangling tail tip in the band does not
    grounded = [r for r in runs if r[1] - r[0] >= 6 and np.nonzero(band[:, r[0]: r[1] + 1].any(axis=1))[0].max() >= band.shape[0] - 4]
    left = (grounded or runs)[0]
    return (left[0] + left[1]) / 2


def bottom_right_point(alpha):
    """(x, y) of the opaque pixel furthest to the bottom right (max x + y), in the crop's coordinates."""
    ys, xs = np.nonzero(alpha > 64)
    i = np.argmax(xs + ys)
    return float(xs[i]), float(ys[i])


def main():
    src, out_dir = sys.argv[1], sys.argv[2]
    expected = int(sys.argv[sys.argv.index("--frames") + 1]) if "--frames" in sys.argv else None
    cell_h = int(sys.argv[sys.argv.index("--cell-height") + 1]) if "--cell-height" in sys.argv else None
    rows = int(sys.argv[sys.argv.index("--rows") + 1]) if "--rows" in sys.argv else 1
    anchor_mode = sys.argv[sys.argv.index("--anchor") + 1] if "--anchor" in sys.argv else "backfoot"
    match_height = int(sys.argv[sys.argv.index("--match-height") + 1]) if "--match-height" in sys.argv else None
    match_frame = int(sys.argv[sys.argv.index("--match-frame") + 1]) if "--match-frame" in sys.argv else 0
    name = os.path.splitext(os.path.basename(src))[0]
    rgb = np.array(Image.open(src).convert("RGB"))
    color, alpha = key_green(rgb)
    rgba = np.dstack([color, alpha])
    frames = find_frames(alpha, expected, rows=rows)
    if expected and len(frames) != expected:
        print(f"WARNING {name}: found {len(frames)} frames, expected {expected}")

    pad = 16
    ground = {}                                         # lowest point of each row = its ground line
    for (ys, _), _, row in frames:
        ground[row] = max(ground.get(row, 0), ys.stop)

    crops, anchors = [], []
    for (ys, xs), own, row in frames:
        pixels = rgba.copy()
        pixels[..., 3] = np.where(own, pixels[..., 3], 0)
        crop = pixels[ys, xs]
        crops.append((crop, ground[row] - ys.stop))      # lift keeps jumps in the air

    if match_height:
        # uniform scale so the reference frame's figure is match_height px tall (keeps clips of one character consistent)
        factor = match_height / crops[match_frame][0].shape[0]
        scaled = []
        for crop, lift in crops:
            image = Image.fromarray(crop, "RGBA")
            size = (max(1, round(image.width * factor)), max(1, round(image.height * factor)))
            scaled.append((np.array(image.resize(size, Image.LANCZOS)), round(lift * factor)))
        crops = scaled
        print(f"{name}: scaled by {factor:.3f}")

    os.makedirs(out_dir, exist_ok=True)
    cells = []
    if anchor_mode == "base-br":
        # pivot (cell bottom centre) = base point; cell symmetric in x around it, base on the bottom padding line
        points = [bottom_right_point(c[..., 3]) for c, _ in crops]
        half = max(max(px, c.shape[1] - px) for (c, _), (px, _) in zip(crops, points)) + pad
        width = int(np.ceil(half * 2))
        height = int(np.ceil(max(py for _, py in points))) + pad * 2
        for i, ((crop, _), (px, py)) in enumerate(zip(crops, points)):
            cell = Image.new("RGBA", (width, height), (0, 0, 0, 0))
            image = Image.fromarray(crop, "RGBA")
            cell.alpha_composite(image, (int(round(width / 2 - px)), int(round(height - pad - py))))
            cell.save(os.path.join(out_dir, f"{name}_{i}.png"))
            cells.append(cell)
    else:
        for crop, _ in crops:
            anchors.append(back_foot_x(crop[..., 3]) if anchor_mode == "backfoot" else crop.shape[1] / 2)

        # symmetric cell around the anchor so the sprite pivot (bottom centre) is the anchor
        half = max(max(a, c.shape[1] - a) for (c, _), a in zip(crops, anchors)) + pad
        width = int(np.ceil(half * 2))
        height = max(c.shape[0] + lift for c, lift in crops) + pad * 2
        for i, ((crop, lift), anchor) in enumerate(zip(crops, anchors)):
            cell = Image.new("RGBA", (width, height), (0, 0, 0, 0))
            image = Image.fromarray(crop, "RGBA")
            cell.paste(image, (int(round(width / 2 - anchor)), height - pad - lift - image.height))
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
