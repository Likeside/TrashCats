#!/usr/bin/env python3
"""Glowing outline of a prop picture: a warm ring along its silhouette, nothing inside (a station in use).

Usage: python3 Tools/make_outline.py <prop.png> <out.png> [width_px=7]
The output has the same size as the prop, so it lays over it exactly.
"""
import sys

from PIL import Image, ImageChops, ImageFilter

COLOR = (255, 214, 92)


def main():
    src, out = sys.argv[1], sys.argv[2]
    width = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    prop = Image.open(src).convert("RGBA")
    solid = prop.getchannel("A").point(lambda v: 255 if v > 60 else 0)
    outer = solid.filter(ImageFilter.MaxFilter(width * 2 + 1))
    inner = solid.filter(ImageFilter.MaxFilter(3))
    ring = ImageChops.subtract(outer, inner).filter(ImageFilter.GaussianBlur(width * 0.35))
    image = Image.new("RGBA", prop.size, COLOR + (0,))
    image.putalpha(ring)
    image.save(out)
    print(out, image.size)


if __name__ == "__main__":
    main()
