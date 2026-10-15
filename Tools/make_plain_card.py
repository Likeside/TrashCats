#!/usr/bin/env python3
"""panel_a without its thin inner line: the band from the inner ink line inwards is painted over with the paper next to
it, so a card has a single frame and its content is padded from that frame. -> Assets/_Project/Art/UI/card_plain.png
Usage: python3 Tools/make_plain_card.py   (numpy, Pillow)
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC = "Assets/_Project/Art/UI/panel_a.png"
OUT = "Assets/_Project/Art/UI/card_plain.png"
LINE_IN = 32   # first paper pixel inside the outer frame (the outer ink ends at 31)
LINE_OUT = 56  # first paper pixel inside the thin inner line
RADIUS = 40


def main():
    im = Image.open(SRC).convert("RGBA")
    a = np.array(im).astype(float)
    h, w = a.shape[:2]
    inner = a[LINE_OUT + 20: h - LINE_OUT - 20, LINE_OUT + 20: w - LINE_OUT - 20, :3]
    paper = np.median(inner.reshape(-1, 3), 0)
    rng = np.random.default_rng(3)
    grain = rng.normal(0, inner.std(axis=(0, 1)).mean() * 0.5, (h, w, 1))
    # paper over the old inner line (hard), the halftone fading back in softly towards the middle
    outer = Image.new("L", (w, h), 0)
    ImageDraw.Draw(outer).rounded_rectangle((LINE_IN, LINE_IN, w - 1 - LINE_IN, h - 1 - LINE_IN), RADIUS, fill=255)
    hard = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hard).rounded_rectangle((LINE_IN, LINE_IN, w - 1 - LINE_IN, h - 1 - LINE_IN), RADIUS, fill=255)
    ImageDraw.Draw(hard).rounded_rectangle((LINE_OUT + 8, LINE_OUT + 8, w - 1 - LINE_OUT - 8, h - 1 - LINE_OUT - 8), RADIUS - 10, fill=0)
    fade = Image.new("L", (w, h), 0)
    ImageDraw.Draw(fade).rounded_rectangle((LINE_IN, LINE_IN, w - 1 - LINE_IN, h - 1 - LINE_IN), RADIUS, fill=255)
    ImageDraw.Draw(fade).rounded_rectangle((LINE_OUT + 90, LINE_OUT + 90, w - 1 - LINE_OUT - 90, h - 1 - LINE_OUT - 90), RADIUS, fill=0)
    fade = fade.filter(ImageFilter.GaussianBlur(30))
    m = np.maximum(np.array(hard), np.array(fade)) * (np.array(outer) / 255.0)
    m = (m / 255.0)[..., None]
    a[..., :3] = a[..., :3] * (1 - m) + (paper + grain) * m
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA").save(OUT)
    print(OUT, (w, h))


if __name__ == "__main__":
    main()
