#!/usr/bin/env python3
"""Splits a character frame into body and tail along a cut polyline.

The polyline is drawn as a 2 px barrier on the alpha mask; the tail is the connected region that contains the given
tail point. Writes <out>_body.png (tail pixels removed) and <out>_tail.png (tail only, same canvas) and prints the
tail base point (middle of the cut segment that touches the tail) in canvas pixels.

Usage: Tools/split_tail.py <frame.png> <out_prefix> --tail-point X,Y --cut X1,Y1 X2,Y2 [X3,Y3 ...]
"""
import sys
from collections import deque
import numpy as np
from PIL import Image, ImageDraw


def components(mask):
    """Yields boolean masks of 4-connected components."""
    seen = np.zeros_like(mask)
    h, w = mask.shape
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        comp = np.zeros_like(mask)
        queue = deque([(y0, x0)])
        seen[y0, x0] = comp[y0, x0] = True
        while queue:
            y, x = queue.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = comp[ny, nx] = True
                    queue.append((ny, nx))
        yield comp


def leftover_specks(body, tail, max_size=400, reach=3):
    """Small body fragments left touching the removed tail (e.g. a tail tip that overlapped a paw, edge fringe).
    Only fragments within `reach` px of the tail are removed; separate details (speed lines, sparks) are kept."""
    near_tail = tail.copy()
    for _ in range(reach):
        grown = near_tail.copy()
        grown[1:] |= near_tail[:-1]; grown[:-1] |= near_tail[1:]
        grown[:, 1:] |= near_tail[:, :-1]; grown[:, :-1] |= near_tail[:, 1:]
        near_tail = grown
    remove = np.zeros_like(body)
    comps = sorted(components(body), key=lambda c: -c.sum())
    for comp in comps[1:]:
        if comp.sum() <= max_size and (comp & near_tail).any():
            remove |= comp
    return remove


def main():
    args = sys.argv[1:]
    src, out = args[0], args[1]
    tail_point = tuple(int(v) for v in args[args.index("--tail-point") + 1].split(","))
    cut = [tuple(int(v) for v in p.split(",")) for p in args[args.index("--cut") + 1:] if "," in p and not p.startswith("--")]

    image = Image.open(src).convert("RGBA")
    rgba = np.array(image)
    solid = rgba[..., 3] > 0

    barrier = Image.new("L", image.size, 0)
    ImageDraw.Draw(barrier).line(cut, fill=255, width=3)
    passable = solid & (np.array(barrier) == 0)

    h, w = passable.shape
    tail = np.zeros_like(passable)
    queue = deque([tail_point[::-1]])
    tail[tail_point[1], tail_point[0]] = True
    while queue:
        y, x = queue.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and passable[ny, nx] and not tail[ny, nx]:
                tail[ny, nx] = True
                queue.append((ny, nx))

    body = rgba.copy()
    body[..., 3] = np.where(tail, 0, body[..., 3])
    body[..., 3] = np.where(leftover_specks(body[..., 3] > 0, tail), 0, body[..., 3])
    tail_img = rgba.copy()
    tail_img[..., 3] = np.where(tail, tail_img[..., 3], 0)

    Image.fromarray(body).save(f"{out}_body.png")
    Image.fromarray(tail_img).save(f"{out}_tail.png")
    ys, xs = np.nonzero(tail)
    base = (np.array(cut[0]) + np.array(cut[-1])) / 2
    print(f"tail pixels {len(xs)}, bbox x {xs.min()}..{xs.max()} y {ys.min()}..{ys.max()}; cut middle {base}")


if __name__ == "__main__":
    main()
