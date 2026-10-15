#!/usr/bin/env python3
""""New cat" icons per cat: the cat's bust (transparent, Characters/<Cat>/Portrait/*_bust.png) with the green plus of
icon_newcat.png in the bottom right corner -> Assets/_Project/Art/UI/icon_newcat_<viewkey>.png
Usage: python3 Tools/make_newcat_icons.py
"""
from collections import deque

from PIL import Image, ImageFilter

BASE = "Assets/_Project/Art/UI/icon_newcat.png"
PLUS = (188, 158, 346, 329)  # the plus in icon_newcat.png (left, top, right, bottom)
CATS = {"ryzhy": "Ryzhy", "shnyr": "shnyr", "ponchik": "ponchik"}
SIZE = 340


def cut_plus():
    """Only the plus: its white fill (connected to its centre) grown round by the frame's width."""
    crop = Image.open(BASE).convert("RGBA").crop(PLUS)
    w, h = crop.size
    px = crop.load()

    def fill(c):
        r, g, b, a = c
        white = r > 200 and g > 200 and b > 190
        olive = g >= r - 10 and g > b + 25 and r < 190
        return a > 200 and (white or olive)

    seen = set()
    queue = deque([(w // 2 + 4, h // 2 + 4)])
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h) or not fill(px[x, y]):
            continue
        seen.add((x, y))
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    mask = Image.new("L", (w, h), 0)
    for x, y in seen:
        mask.putpixel((x, y), 255)
    mask = mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))  # close specks inside
    # round dilation over the inner ink, the olive frame and the outer ink: blur the fill and keep what it reaches
    grown = mask.filter(ImageFilter.GaussianBlur(OUTLINE * 0.55)).point(lambda v: 255 if v > 6 else 0)
    mask = grown.filter(ImageFilter.GaussianBlur(0.8))
    alpha = Image.fromarray(__import__("numpy").minimum(__import__("numpy").array(mask), __import__("numpy").array(crop.getchannel("A"))))
    crop.putalpha(alpha)
    return crop


OUTLINE = 27  # px from the white fill to the outer edge of the ink (inner ink + olive frame + outer ink)


def main():
    plus = cut_plus()
    for key, folder in CATS.items():
        bust = Image.open(f"Assets/_Project/Art/Characters/{folder}/Portrait/{key}_bust.png").convert("RGBA")
        bust = bust.crop(bust.getchannel("A").getbbox())
        scale = SIZE * 0.92 / max(bust.size)
        bust = bust.resize((round(bust.width * scale), round(bust.height * scale)), Image.LANCZOS)
        icon = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        icon.alpha_composite(bust, ((SIZE - bust.width) // 2 - 14, SIZE - bust.height))
        icon.alpha_composite(plus, (SIZE - plus.width, SIZE - plus.height))
        out = f"Assets/_Project/Art/UI/icon_newcat_{key}.png"
        icon.save(out)
        print(out)


if __name__ == "__main__":
    main()
