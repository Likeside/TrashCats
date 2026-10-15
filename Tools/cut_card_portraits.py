"""Cat card pictures and the card itself, cut straight from the hideout concept.

Picture: the concept's window in one piece (portrait, coloured halftone backdrop and the window's own rounded ink
frame) plus the ear tips that stick out above the frame onto the card's paper. The outline is the drawing's own:
a flood fill through the paper around the piece, stopped by the ink, and the rest is the picture. The windows differ in height, so the taller ones lose rows above the bottom
edge (the chin) and get the concept's own bottom frame back: every picture has the same frame aspect.

Card: Shnyr's card frame from the concept (rounded cardboard edge with an ink line), the inside painted over with
paper, as a 9-sliced sprite.

v1 (ArtSource/UI/cards_v1_concept): window inside its frame, the whole cat in it.
v2 (ArtSource/UI/cards_v2_ears): cut-out cat over a drawn frame (rejected: the cat and the card cut separately).
v3 (ArtSource/UI/cards_v3_window): this one.
Needs numpy and Pillow.  Usage: python3 Tools/cut_card_portraits.py
"""
import os
from collections import deque

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

CONCEPT = "Docs/ArtDirection/Concepts/round1/blind/hideout_A.png"
# window of each card with its ink frame (left, top, right, bottom)
WINDOWS = {"Ryzhy": (39, 1077, 326, 1260), "shnyr": (376, 1076, 645, 1236), "ponchik": (694, 1077, 985, 1237)}
EARS_TOP = 1066  # just under the card's inner ink line: the paper band the ears stick out into
ASPECT = 1.84  # frame width / height, the same for every cat (the game lays the cards out alike)
BOTTOM_FRAME = 7  # rows of the concept's bottom frame put back under a shortened window
MARGIN = 3  # paper taken around the window so the fill can go round it
PAPER_LUMA = 150
SCALE = 2
SS = 4  # supersampling of the panel mask
ARCHIVE = "ArtSource/UI/cards_v3_window"

# the card itself: Shnyr's card from the concept (outer edge of its ink outline), the inside painted over with paper
PANEL = (353, 1052, 667, 1362)
PANEL_RADIUS = 19
PANEL_INSIDE = (367, 1067, 654, 1347)  # everything inside the inner ink line but a pixel of paper
PAPER_SAMPLE = (366, 1150, 374, 1300)  # clean paper between the frame and the bars
PANEL_INNER_RADIUS = 10
PANEL_SLICE = 26  # 9-slice border on every side (covers the rounded corners), concept px
PANEL_SCALE = 2
PANEL_OUT = "Assets/_Project/Art/UI/card_cat.png"


def flood(passable, seeds):
    h, w = passable.shape
    seen = np.zeros((h, w), bool)
    queue = deque()
    for y, x in seeds:
        if passable[y, x] and not seen[y, x]:
            seen[y, x] = True
            queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < h and 0 <= xx < w and not seen[yy, xx] and passable[yy, xx]:
                seen[yy, xx] = True
                queue.append((yy, xx))
    return seen


def outline_mask(piece):
    """Everything inside the ink outline: the piece minus the paper around it, found by a flood fill from the
    edges through light warm pixels that the ink stops; specks drawn on the paper are dropped."""
    h, w, _ = piece.shape
    lum = piece.mean(2)
    paper = (lum > PAPER_LUMA) & (piece[..., 0] > piece[..., 2] + 20)
    edges = [(0, x) for x in range(w)] + [(h - 1, x) for x in range(w)] + [(y, 0) for y in range(h)] + [(y, w - 1) for y in range(h)]
    inside = flood(~flood(paper, edges), [(h // 2, w // 2)])
    # the antialiased paper rim around the ink goes: one pixel of erosion
    eroded = inside.copy()
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        eroded &= np.roll(np.roll(inside, dy, 0), dx, 1)
    return eroded


def card(src, key):
    left, top, right, bottom = WINDOWS[key]
    width = right - left
    height = min(bottom - top, round(width / ASPECT))
    m = MARGIN
    # the paper band with the ear tips, the window's top part and the concept's own bottom frame, with paper around
    piece = np.concatenate([src[EARS_TOP: top + height - BOTTOM_FRAME, left - m: right + m],
                            src[bottom - BOTTOM_FRAME: bottom + m, left - m: right + m]])
    mask = outline_mask(piece.astype(int))
    ys, xs = np.nonzero(mask)
    box = (xs.min(), 0, xs.max() + 1, ys.max() + 1)  # the frame touches the sides and the bottom
    size = ((box[2] - box[0]) * SCALE, (box[3] - box[1]) * SCALE)
    image = Image.fromarray(piece.astype(np.uint8)).crop(box).resize(size, Image.LANCZOS)
    alpha = Image.fromarray((mask * 255).astype(np.uint8), "L").crop(box).resize(size, Image.BILINEAR)
    image.putalpha(alpha.filter(ImageFilter.GaussianBlur(0.5)))
    return image


def panel(src):
    """9-sliced card: the concept's rounded cardboard frame with an ink line and a paper inside."""
    left, top, right, bottom = PANEL
    card = src[top:bottom, left:right].astype(float)
    il, it, ir, ib = (PANEL_INSIDE[0] - left, PANEL_INSIDE[1] - top, PANEL_INSIDE[2] - left, PANEL_INSIDE[3] - top)
    sl, st, sr, sb = PAPER_SAMPLE
    margin = src[st:sb, sl:sr].reshape(-1, 3).astype(float)
    paper = np.median(margin, 0)
    rng = np.random.default_rng(7)
    grain = rng.normal(0, margin.std(0).mean() * 0.35, (ib - it, ir - il, 1))
    fill = Image.new("L", (right - left, bottom - top), 0)
    ImageDraw.Draw(fill).rounded_rectangle((il, it, ir - 1, ib - 1), PANEL_INNER_RADIUS, fill=255)
    fill = np.array(fill.filter(ImageFilter.GaussianBlur(0.6)))[it:ib, il:ir, None] / 255.0
    card[it:ib, il:ir] = card[it:ib, il:ir] * (1 - fill) + (paper + grain) * fill
    w, h = right - left, bottom - top
    image = Image.fromarray(np.clip(card, 0, 255).astype(np.uint8)).resize((w * PANEL_SCALE, h * PANEL_SCALE), Image.LANCZOS)
    k = PANEL_SCALE * SS
    mask = Image.new("L", (w * k, h * k), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w * k - 1, h * k - 1), PANEL_RADIUS * k, fill=255)
    image.putalpha(mask.resize(image.size, Image.LANCZOS))
    image.save(PANEL_OUT)
    image.save(f"{ARCHIVE}/card_cat.png")
    # 9-slice borders (left, bottom, right, top) for Tools/import_ui.py SLICES / Art/UI/slices.json
    print("card_cat", image.size, "slices", PANEL_SLICE * PANEL_SCALE)


def main():
    src = np.array(Image.open(CONCEPT).convert("RGB"))
    os.makedirs(ARCHIVE, exist_ok=True)
    for key in WINDOWS:
        image = card(src, key)
        image.save(f"{ARCHIVE}/{key.lower()}_card.png")
        folder = f"Assets/_Project/Art/Characters/{key}/Card"
        os.makedirs(folder, exist_ok=True)
        image.save(f"{folder}/{key.lower()}_card.png")
        print(key, image.size)
    panel(src)


if __name__ == "__main__":
    main()
