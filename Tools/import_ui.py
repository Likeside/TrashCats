#!/usr/bin/env python3
"""Cuts generated UI sheets (Docs/ArtDirection/Generated/raw_ui) into single sprites in Assets/_Project/Art/UI.

Green sheets are chroma-keyed (sprite_process.key_green); sheets the model drew on a light grey background are cut by
flood-filling the background from the image border (the black outlines stop the fill). Items are found as blobs
(nearby parts such as the moon's stars are merged), ordered in rows top to bottom, left to right.
Also writes button_white.png (the yellow button turned neutral so it can be tinted) and slices.json (9-slice borders
in pixels: left, bottom, right, top) for the Unity skin builder. Needs numpy, scipy, Pillow.
Usage: Tools/import_ui.py
"""
import glob, json, os, shutil, sys
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

sys.path.insert(0, os.path.dirname(__file__))
from sprite_process import key_green

RAW = "Docs/ArtDirection/Generated/raw_ui"
OUT = "Assets/_Project/Art/UI"

# sheet -> names of its items in reading order (None = skip)
SHEETS = {
    "ui_buttons": ["button_yellow", "button_blue", None, None, None, None],
    "ui_panels": ["panel_paper", None, None],
    "ui_bars": ["bar_frame", "bar_energy", "bar_satiety", "bar_hp", "bar_grit"],
    # the attribute icons of this sheet were replaced (paw, tail, heart: ArtSource/UI/*_v1_*.png); strength and technique
    # now come from Docs/ArtDirection/Generated/raw_icons_attr, endurance from the icon_endurance sheet
    "ui_icons_a": ["icon_food", "icon_junk", None, None, None, "icon_xp", "icon_lock", "icon_clock"],
    "ui_icons_b": ["icon_home", "icon_map", "icon_sleep", "icon_upgrade", "icon_speed", "icon_skip", "icon_settings", "icon_crown"],
    "ui_fx": ["ribbon", "burst", "bubble", "tape", "pointer"],
    "ui_extra": ["panel_tin"],
    # round 2: style of the chosen concept hideout_A (rounded, ink outline, no tape)
    "ui_a_buttons": ["button_a_yellow", "button_a_blue", "button_a_grey"],
    "ui_a_parts": ["hud_a"],
    "ui_a_sheet": ["panel_a"],
    "icon_endurance": ["icon_endurance"],
    "ui_event_icons": ["icon_victory", "icon_defeat", "icon_newcat", "icon_skill", "icon_flag", "icon_levelup", "icon_final", "icon_tools"],
    "cat_busts": ["bust_ryzhy", "bust_shnyr", "bust_ponchik"],
    "fx_combat": ["fx_star", "fx_dust", "fx_scratch", "fx_sparkle"],
    "ui_map_parts": ["map_ring", None, None, None],
    # satiety is a fish now (raw_icon_satiety, Tools/cut_icon.py); the bowl is kept as ArtSource/UI/icon_satiety_v1_bowl.png
    "ui_a_icons": ["icon_food", "icon_junk", "icon_hp", "icon_paw", "icon_home", "icon_map", "icon_energy", None],
}

ENEMIES = ["rat", "gull", "shopcat", "crow", "raccoon", "pug", "admiral"]

# cat card portraits cut straight from the concept (left, top, right, bottom)
CONCEPT = "Docs/ArtDirection/Concepts/round1/blind/hideout_A.png"
PORTRAITS = {"Ryzhy": (44, 1080, 323, 1250), "shnyr": (380, 1080, 641, 1238), "ponchik": (699, 1080, 983, 1238)}

# how far apart parts of one item may be (px); small where items sit close together
MERGE = {"ui_event_icons": 18, "cat_busts": 8, "ui_icons_a": 18, "ui_icons_b": 18, "ui_bars": 0, "fx_combat": 14}

# items cut by a fixed box (left, top, right, bottom) because they consist of scattered parts
MANUAL = {"fx_sparkle": ("fx_combat", (1230, 330, 1490, 650))}

# 9-slice borders (left, bottom, right, top) in source pixels; items not listed are drawn whole
SLICES = {
    "button_yellow": (60, 62, 150, 118),
    "button_blue": (60, 62, 150, 118),
    "button_white": (60, 62, 150, 118),
    "panel_paper": (120, 70, 120, 118),
    "panel_tin": (110, 110, 110, 110),
    "card_cat": (52, 52, 52, 52),  # Tools/cut_card_portraits.py
    "card_plain": (80, 80, 80, 80),  # Tools/make_plain_card.py
    "bar_frame": (48, 44, 48, 44),
    "bar_energy": (178, 52, 48, 52),
    "bar_satiety": (178, 52, 48, 52),
    "bar_hp": (178, 52, 48, 52),
    "bar_grit": (178, 52, 48, 52),
    "tape": (34, 26, 34, 26),
    # cut by hand from raw_ui_marker/marker_loop_c.png (2026-10-16): the flick at the top right stays in its corner
    "tutorial_marker": (60, 60, 200, 70),
    "button_a_yellow": (50, 50, 50, 50),
    "button_a_blue": (50, 50, 50, 50),
    "button_a_grey": (50, 50, 50, 50),
    "button_a_white": (50, 50, 50, 50),
    "hud_a": (60, 60, 60, 60),
    "panel_a": (90, 90, 90, 90),
}


def alpha_for(rgb):
    corners = np.array([rgb[2, 2], rgb[2, -3], rgb[-3, 2], rgb[-3, -3]], dtype=np.float32)
    if (corners[:, 1] - corners[:, [0, 2]].max(axis=1)).mean() > 80:  # green chroma background
        clean, alpha = key_green(rgb)
        return clean, alpha
    # light grey paper: flood from the border through low-saturation light pixels
    f = rgb.astype(np.int16)
    light = (f.max(axis=2) - f.min(axis=2) < 22) & (f.min(axis=2) > 170)
    labels, _ = ndimage.label(light)
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    background = np.isin(labels, border[border > 0])
    alpha = Image.fromarray(np.where(background, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
    return rgb, np.where(background, 0, np.array(alpha)).astype(np.uint8)


def items(alpha, merge=18, min_pixels=3000):
    solid = alpha > 128
    labels, count = ndimage.label(ndimage.binary_dilation(solid, iterations=merge) if merge else solid)
    boxes = []
    for i, sl in enumerate(ndimage.find_objects(labels), start=1):
        if (solid[sl] & (labels[sl] == i)).sum() >= min_pixels:
            boxes.append((sl, i))
    # rows: blobs whose vertical centres are close belong to one row
    boxes.sort(key=lambda b: (b[0][0].start + b[0][0].stop) / 2)
    rows, row = [], []
    for b in boxes:
        cy = (b[0][0].start + b[0][0].stop) / 2
        if row and cy - (row[0][0][0].start + row[0][0][0].stop) / 2 > 120:
            rows.append(row)
            row = []
        row.append(b)
    if row:
        rows.append(row)
    ordered = [b for r in rows for b in sorted(r, key=lambda b: b[0][1].start)]
    return ordered, labels


def defringe(img, passes=6):
    """Eats the light, unsaturated leftovers of a paper background along the cut edge, then softens the edge."""
    a = np.array(img).astype(np.int16)
    rgb, alpha = a[..., :3], a[..., 3]
    light = (rgb.min(axis=2) > 175) & (rgb.max(axis=2) - rgb.min(axis=2) < 45)
    for _ in range(passes):
        clear = alpha < 40
        edge = ndimage.binary_dilation(clear) & ~clear
        eat = edge & light
        if not eat.any():
            break
        alpha[eat] = 0
    alpha = ndimage.grey_erosion(alpha, size=(2, 2))
    a[..., 3] = alpha
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def neutral(img):
    """Yellow button -> white-ish button that keeps outline, texture and shading; tinted at runtime."""
    a = np.array(img).astype(np.float32)
    rgb, alpha = a[..., :3], a[..., 3:]
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    body = sat > 60
    base = np.median(lum[body])
    grey = np.clip(lum / base * 245.0, 0, 255)
    out = np.where(body[..., None], grey[..., None].repeat(3, axis=2), rgb)
    return Image.fromarray(np.concatenate([out, alpha], axis=2).astype(np.uint8), "RGBA")


def drawn():
    """Simple shapes drawn here: a circle mask for round pictures and a glowing golden ring for the map."""
    from PIL import ImageDraw
    size = 512
    mask = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(mask).ellipse((4, 4, size - 5, size - 5), fill=(255, 255, 255, 255))
    mask.save(f"{OUT}/circle_mask.png")
    ring = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(ring)
    d.ellipse((4, 4, size - 5, size - 5), fill=(20, 22, 30, 255))
    d.ellipse((14, 14, size - 15, size - 15), fill=(245, 189, 55, 255))
    d.ellipse((40, 40, size - 41, size - 41), fill=(20, 22, 30, 255))
    d.ellipse((48, 48, size - 49, size - 49), fill=(0, 0, 0, 0))
    ring.save(f"{OUT}/map_ring_gold.png")

    # "Ярость!" vignette: red halftone dots growing towards the edges (Art Bible §8), transparent in the middle
    w, h = 540, 960
    vignette = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(vignette)
    step = 18
    for y in range(0, h + step, step):
        for x in range((y // step % 2) * step // 2, w + step, step):
            dx, dy = (x - w / 2) / (w / 2), (y - h / 2) / (h / 2)
            t = max(0.0, (dx * dx + dy * dy) ** 0.5 - 0.55) / 0.6
            r = min(step * 0.62, step * 0.75 * t)
            if r > 0.8:
                d.ellipse((x - r, y - r, x + r, y + r), fill=(205, 40, 30, 235))
    vignette.save(f"{OUT}/rage_vignette.png")


def enemy_portraits():
    """Head-and-shoulders square cut from each enemy's first stance frame (characters face right, head on top)."""
    for key in ENEMIES:
        src = f"Assets/_Project/Art/Characters/{key}/Idle/{key}_idle_00.png"
        if not os.path.exists(src):
            continue
        im = Image.open(src).convert("RGBA")
        a = np.array(im)[..., 3] > 40
        ys, xs = np.nonzero(a)
        top, bottom = ys.min(), ys.max()
        side = int((bottom - top) * 0.5)
        band = a[top: top + max(4, side // 3)]
        cx = int(np.nonzero(band)[1].mean())
        x0 = max(0, cx - side // 2)
        box = (x0, max(0, top - side // 12), x0 + side, max(0, top - side // 12) + side)
        folder = f"Assets/_Project/Art/Characters/{key}/Portrait"
        os.makedirs(folder, exist_ok=True)
        im.crop(box).save(f"{folder}/{key}_portrait.png")


def main():
    os.makedirs(OUT, exist_ok=True)
    for sheet, names in SHEETS.items():
        path = f"{RAW}/{sheet}.png"
        if not os.path.exists(path):
            print("missing", path)
            continue
        rgb = np.array(Image.open(path).convert("RGB"))
        clean, alpha = alpha_for(rgb)
        found, labels = items(alpha, MERGE.get(sheet, 3))
        print(f"{sheet}: {len(found)} items for {len(names)} names")
        for (sl, label), name in zip(found, names):
            if name is None:
                continue
            mask = (labels[sl] == label)
            a = np.where(mask, alpha[sl], 0).astype(np.uint8)
            img = Image.fromarray(np.dstack([clean[sl], a]), "RGBA")
            img = img.crop(img.getchannel("A").point(lambda v: 255 if v > 16 else 0).getbbox())
            pad = Image.new("RGBA", (img.width + 8, img.height + 8), (0, 0, 0, 0))
            pad.paste(img, (4, 4))
            if name.startswith("bust_"):
                pad = defringe(pad)
            pad.save(f"{OUT}/{name}.png")
            print(f"   {name}: {pad.width}x{pad.height}")
            if name in MANUAL:
                left, top, right, bottom = MANUAL[name][1]
                img = Image.fromarray(np.dstack([clean, alpha]), "RGBA").crop((left, top, right, bottom))
                img.save(f"{OUT}/{name}.png")
            if name == "button_yellow":
                neutral(pad).save(f"{OUT}/button_white.png")
            if name == "button_a_yellow":
                neutral(pad).save(f"{OUT}/button_a_white.png")
    drawn()
    enemy_portraits()
    # card portraits: generated busts with transparent background (ears may stick out of the card)
    for key in PORTRAITS:
        bust = f"{OUT}/bust_{key.lower()}.png"
        folder = f"Assets/_Project/Art/Characters/{key}/Portrait"
        if os.path.exists(bust):
            os.makedirs(folder, exist_ok=True)
            for old in glob.glob(f"{folder}/*_portrait.png*"):
                os.remove(old)
            shutil.move(bust, f"{folder}/{key.lower()}_bust.png")
    with open(f"{OUT}/slices.json", "w") as f:
        json.dump({k: list(v) for k, v in SLICES.items()}, f, indent=1)


if __name__ == "__main__":
    main()
