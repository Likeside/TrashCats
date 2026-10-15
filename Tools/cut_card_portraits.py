"""Cat card pictures (portrait on its coloured halftone backdrop) cut straight from the hideout concept.

The windows are cut inside their ink border, so no paper pixels from the card frame get in; the corners are
rounded with an antialiased mask to match the concept's rounded windows. The game draws its own ink border.
Usage: python3 Tools/cut_card_portraits.py  -> ArtSource/UI/cards_v1_concept/ + Assets/_Project/Art/Characters/<Cat>/Card/
"""
import os
from PIL import Image, ImageDraw

CONCEPT = "Docs/ArtDirection/Concepts/round1/blind/hideout_A.png"
# inner window of each card (left, top, right, bottom), just inside the ink border
WINDOWS = {"Ryzhy": (43, 1081, 322, 1255), "shnyr": (380, 1080, 641, 1231), "ponchik": (699, 1080, 981, 1232)}
SCALE = 2  # upscaled so the card window is not magnified in game
RADIUS = 7  # concept px
ARCHIVE = "ArtSource/UI/cards_v1_concept"


def rounded(im, radius):
    big = 4
    mask = Image.new("L", (im.width * big, im.height * big), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, mask.width - 1, mask.height - 1), radius * big, fill=255)
    out = im.convert("RGBA")
    out.putalpha(mask.resize(im.size, Image.LANCZOS))
    return out


def main():
    src = Image.open(CONCEPT).convert("RGB")
    os.makedirs(ARCHIVE, exist_ok=True)
    for key, box in WINDOWS.items():
        crop = src.crop(box)
        crop = crop.resize((crop.width * SCALE, crop.height * SCALE), Image.LANCZOS)
        card = rounded(crop, RADIUS * SCALE)
        card.save(f"{ARCHIVE}/{key.lower()}_card.png")
        folder = f"Assets/_Project/Art/Characters/{key}/Card"
        os.makedirs(folder, exist_ok=True)
        card.save(f"{folder}/{key.lower()}_card.png")
        print(key, card.size)


if __name__ == "__main__":
    main()
