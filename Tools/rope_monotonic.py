#!/usr/bin/env python3
"""Skipping rope without the shake (2026-10-16): the cat's height (y of the top of the silhouette, the legs bend
under it) must only go one way between its extremes: from the highest frame down to the lowest, then up again. A frame
that moves against that way is dropped. keys.json keeps one entry per drawn key (a dropped key points at the next
kept frame, so the tempo stays), dropped.json records the original frames for Tools/pose_handles.py.
Usage: rope_monotonic.py [--dry] <clip folder>...
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image


def height(f):
    return int(np.nonzero((np.array(Image.open(f))[..., 3] > 60).any(1))[0].min())


def plan(ys, tol):
    n = len(ys)
    top, bottom = int(np.argmin(ys)), int(np.argmax(ys))
    keep = {top}
    last = ys[top]
    i = top
    goal = bottom
    down = True
    for _ in range(n - 1):
        i = (i + 1) % n
        y = ys[i]
        if (down and y >= last - tol) or (not down and y <= last + tol):
            keep.add(i)
            last = y
        if i == goal:
            down, goal = not down, top
    return sorted(keep)


def main():
    args = sys.argv[1:]
    dry = "--dry" in args
    folders = [a for a in args if a != "--dry"]
    for d in folders:
        fs = sorted(glob.glob(d + "/*.png"))
        ys = [height(f) for f in fs]
        keep = plan(ys, 1)
        drop = [i for i in range(len(fs)) if i not in keep]
        print(os.path.basename(os.path.dirname(d)), ys, "drop", drop)
        if dry or not drop:
            continue
        keys = json.load(open(d + "/keys.json"))["keys"]
        json.dump({"frames": len(fs), "keys": keys, "dropped": drop}, open(d + "/dropped.json", "w"))
        new_keys = [sum(1 for j in keep if j < k) if k in keep else min(sum(1 for j in keep if j < k), len(keep) - 1) for k in keys]
        stem = os.path.basename(fs[0]).rsplit("_", 1)[0]
        for i in drop:
            os.remove(fs[i])
            if os.path.exists(fs[i] + ".meta"):
                os.remove(fs[i] + ".meta")
        for j, i in enumerate(keep):
            dst = f"{d}/{stem}_{j:02d}.png"
            if fs[i] != dst:
                os.rename(fs[i], dst)
                if os.path.exists(fs[i] + ".meta"):
                    os.rename(fs[i] + ".meta", dst + ".meta")
        json.dump({"keys": new_keys}, open(d + "/keys.json", "w"))


if __name__ == "__main__":
    main()
