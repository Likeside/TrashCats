#!/usr/bin/env python3
"""Cuts generated UI sheets (Docs/ArtDirection/Generated/raw_ui) into single sprites in Assets/_Project/Art/UI.

Green sheets are chroma-keyed (sprite_process.key_green); sheets the model drew on a light grey background are cut by
flood-filling the background from the image border (the black outlines stop the fill). Items are found as blobs
(nearby parts such as the moon's stars are merged), ordered in rows top to bottom, left to right.
Also writes button_white.png (the yellow button turned neutral so it can be tinted) and slices.json (9-slice borders
in pixels: left, bottom, right, top) for the Unity skin builder. Needs numpy, scipy, Pillow.
Usage: Tools/import_ui.py
"""
import json, os, sys
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
    "ui_icons_a": ["icon_food", "icon_junk", "icon_strength", "icon_agility", "icon_toughness", "icon_xp", "icon_lock", "icon_clock"],
    "ui_icons_b": ["icon_home", "icon_map", "icon_sleep", "icon_upgrade", "icon_speed", "icon_skip", "icon_settings", "icon_crown"],
    "ui_fx": ["ribbon", "burst", "bubble", "tape", "pointer"],
    "ui_extra": ["panel_tin"],
}

# how far apart parts of one item may be (px); small where items sit close together
MERGE = {"ui_icons_a": 18, "ui_icons_b": 18, "ui_bars": 0}

# 9-slice borders (left, bottom, right, top) in source pixels; items not listed are drawn whole
SLICES = {
    "button_yellow": (60, 62, 150, 118),
    "button_blue": (60, 62, 150, 118),
    "button_white": (60, 62, 150, 118),
    "panel_paper": (120, 70, 120, 118),
    "panel_tin": (110, 110, 110, 110),
    "bar_frame": (48, 44, 48, 44),
    "bar_energy": (178, 52, 48, 52),
    "bar_satiety": (178, 52, 48, 52),
    "bar_hp": (178, 52, 48, 52),
    "bar_grit": (178, 52, 48, 52),
    "tape": (34, 26, 34, 26),
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
            pad.save(f"{OUT}/{name}.png")
            print(f"   {name}: {pad.width}x{pad.height}")
            if name == "button_yellow":
                neutral(pad).save(f"{OUT}/button_white.png")
    with open(f"{OUT}/slices.json", "w") as f:
        json.dump({k: list(v) for k, v in SLICES.items()}, f, indent=1)


if __name__ == "__main__":
    main()
