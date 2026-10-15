#!/usr/bin/env python3
"""Cuts one generated icon off its green chroma background, trims it and scales it to the UI icon size.

Usage: python3 Tools/cut_icon.py <raw.png> <out.png> [height=300]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from sprite_process import key_green  # noqa: E402


def main():
    src, out = sys.argv[1], sys.argv[2]
    height = int(sys.argv[3]) if len(sys.argv) > 3 else 300
    rgb = np.array(Image.open(src).convert("RGB"))
    clean, alpha = key_green(rgb)
    image = Image.fromarray(np.dstack([clean, alpha]).astype(np.uint8), "RGBA")
    image = image.crop(image.getchannel("A").point(lambda v: 255 if v > 16 else 0).getbbox())
    image = image.resize((round(image.width * height / image.height), height), Image.LANCZOS)
    pad = Image.new("RGBA", (image.width + 8, image.height + 8), (0, 0, 0, 0))
    pad.paste(image, (4, 4))
    pad.save(out)
    print(out, pad.size)


if __name__ == "__main__":
    main()
