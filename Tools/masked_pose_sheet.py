#!/usr/bin/env python3
"""Base sheet and mask for a masked pose edit (2026-10-16): the same key frame 8 times in a 2x4 grid on chroma green;
the mask (transparent = the model may draw) opens the upper part of every cell (the arms and a barbell go there) and
keeps the head (an ellipse) and the body below lock_y. With art_batch.py "mask" the model redraws only the arms, so
the head, the face and the body stay the same in every frame.
Usage: masked_pose_sheet.py <key png> <out prefix> <head cx,cy,rx,ry> <lock_y>   (pixels of the key frame)
Writes <prefix>_base.png and <prefix>_mask.png (1536x1024).
"""
import sys

from PIL import Image, ImageDraw


def main():
    key_png, prefix = sys.argv[1], sys.argv[2]
    hx, hy, rx, ry = [float(v) for v in sys.argv[3].split(",")]
    lock_y = float(sys.argv[4])
    key = Image.open(key_png).convert("RGBA")
    k = min(384 / key.width, 480 / key.height)
    small = key.resize((round(key.width * k), round(key.height * k)), Image.LANCZOS)
    sheet = Image.new("RGBA", (1536, 1024), (0, 255, 0, 255))
    mask = Image.new("RGBA", (1536, 1024), (0, 0, 0, 255))
    draw = ImageDraw.Draw(mask)
    for r in range(2):
        for c in range(4):
            ox, oy = c * 384 + (384 - small.width) // 2, r * 512 + 512 - small.height - 20
            sheet.alpha_composite(small, (ox, oy))
            draw.rectangle([c * 384, r * 512, c * 384 + 383, oy + round(lock_y * k)], fill=(0, 0, 0, 0))
            cx, cy = ox + hx * k, oy + hy * k
            draw.ellipse([cx - rx * k, cy - ry * k, cx + rx * k, cy + ry * k], fill=(0, 0, 0, 255))
    sheet.convert("RGB").save(prefix + "_base.png")
    mask.save(prefix + "_mask.png")


if __name__ == "__main__":
    main()
