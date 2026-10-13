#!/usr/bin/env python3
"""Preview: Ryzhy bench press frames (ArtSource/Bench/ryzhy_v2) on the mat with brick stands. Usage: <cat scale> <dx> <dy> <out.gif> [mat scale] [frames dir]  (v2: 1.35 0 -90 out.gif 0.95; v3: 1.35 -95 -140 out.gif 0.81; v4: 1.35 -90 -189 out.gif 0.729; ryzhy v5 and ponchik: 1.35 -90 -195 0.729; shnyr v3: 1.15 -67 -136 0.729)"""
import sys, glob
from PIL import Image
sx, ox, oy = float(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
ms = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0
mat = Image.open("ArtSource/Bench/mat/mat_empty.png").convert("RGBA")
mat = mat.crop(mat.getchannel("A").getbbox())
mat = mat.resize((int(mat.width * ms), int(mat.height * ms)), Image.LANCZOS)
frames_dir = sys.argv[6] if len(sys.argv) > 6 else "ArtSource/Bench/ryzhy_v2"
fs = sorted(glob.glob(frames_dir + "/*.png"), key=lambda p: int(p.rsplit("_", 1)[1][:-4]))
cats = [Image.open(f).convert("RGBA") for f in fs]
cats = [c.resize((int(c.width * sx), int(c.height * sx)), Image.LANCZOS) for c in cats]
W, H = mat.width + 200, mat.height + 260
frames = []
for c in cats:
    bg = Image.new("RGBA", (W, H), (70, 60, 80, 255))
    bg.alpha_composite(mat, (100, 230))
    bg.alpha_composite(c, (100 + ox, 230 + oy))
    frames.append(bg.convert("RGB"))
out = sys.argv[4]
frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / len(frames)), loop=0)  # one cycle per second (8 frames at 8 fps, or 16 at 16)
s = Image.new("RGB", (W * 4, H)); [s.paste(frames[i], (j * W, 0)) for j, i in enumerate([0, 2, 3, 6])]
s.save(out.replace(".gif", "_strip.png"))
print(mat.size, cats[0].size)
