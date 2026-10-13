import sys
import numpy as np
from PIL import Image
src, dst, specs = sys.argv[1], sys.argv[2], sys.argv[3]
im = Image.open(src).convert("RGBA"); im = im.crop(im.getchannel("A").getbbox())
brick = Image.open("ArtSource/Bench/mat/brick_flat.png").convert("RGBA"); brick = brick.crop(brick.getchannel("A").getbbox())
a = np.array(im)
for spec in specs.split(";"):
    x0, x1, cut, bottom = map(int, spec.split(","))
    a[:cut, x0:x1, 3] = 0           # remove the notched top brick
    w = x1 - x0
    b = brick.resize((w, int(brick.height * w / brick.width)), Image.LANCZOS)
    ba = np.array(b).astype(np.float32); ba[..., :3] *= 0.9; b = Image.fromarray(ba.astype(np.uint8))
    tmp = Image.fromarray(a); tmp.alpha_composite(b, (x0, bottom - b.height)); a = np.array(tmp)
Image.fromarray(a).save(dst)
