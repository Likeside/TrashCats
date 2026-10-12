#!/usr/bin/env python3
"""Spritesheet pilot: generates short animation strips of a character from a reference image via the
OpenAI Images edit endpoint (gpt-image-2). Reads OPENAI_API_KEY from the environment.

Usage: Tools/art_spritesheet.py <reference.png> <out_dir> <sheet_name> [<sheet_name> ...] [--dry-run]
"""
import base64, json, os, subprocess, sys, tempfile

MODEL = "gpt-image-2"
SUFFIX = os.environ.get("SHEET_SUFFIX", "")

STYLE = (
    "Comic book style matching the reference exactly: bold black ink outlines, flat orange fur with darker stripes, "
    "cream chest and muzzle, torn left ear, scar across the nose, yellow eyes. Same character, same proportions, same "
    "colors and line weight in every frame. No clothes."
)

LAYOUT = (
    "Game sprite sheet: exactly {n} frames in ONE horizontal row, equal-width cells, the character centered in each cell "
    "at the same scale, feet on the same baseline, full body visible, side view facing right. "
    "Solid flat pure green (#00FF00) chroma-key background everywhere, no shadows on the background, no scenery, no text, no frame numbers, no borders between cells."
)

SHEETS = {
    "idle": (4, "Animation: idle fighting stance, breathing loop - chest rises and falls, tail sways, ears twitch slightly."),
    "punch": (4, "Keep the exact stance, proportions and size of the reference: standing on hind legs like a boxer. "
                 "Animation: punching a hanging tin can with a front paw - 1) wind-up pulling the paw back, "
                 "2) lunge forward, 3) paw hits (impact pose), 4) recovery back to stance. The tin can is NOT drawn."),
    "bite": (4, "Animation: lunging bite attack - 1) crouch and anticipate, 2) leap forward with mouth wide open, "
                "3) bite at full extension, 4) land back in stance with a grin."),
}


def generate(ref, out_dir, name, key, background):
    frames, action = SHEETS[name]
    prompt = f"{LAYOUT.format(n=frames)}\n\n{action}\n\n{STYLE}"
    out = os.path.join(out_dir, f"ryzhy_{name}{SUFFIX}.png")
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as tmp:
        resp_path = tmp.name
    cmd = ["curl", "-sS", "https://api.openai.com/v1/images/edits",
           "-H", f"Authorization: Bearer {key}",
           "-F", f"model={MODEL}", "-F", f"image[]=@{ref}", "-F", f"prompt={prompt}",
           "-F", "size=1536x1024", "-F", "quality=high", "-F", f"background={background}", "-o", resp_path]
    subprocess.run(cmd, check=True)
    data = json.load(open(resp_path))
    os.remove(resp_path)
    if "error" in data:
        raise RuntimeError(f"{name}: {data['error'].get('message')}")
    with open(out, "wb") as f:
        f.write(base64.b64decode(data["data"][0]["b64_json"]))
    with open(os.path.join(out_dir, "prompts.md"), "a") as f:
        f.write(f"## ryzhy_{name} (edit of {os.path.basename(ref)}, background={background})\n\n```\n{prompt}\n```\n\n")
    return out, data.get("usage")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    ref, out_dir, names = args[0], args[1], args[2:]
    background = "opaque"
    for a in sys.argv[1:]:
        if a.startswith("--background="):
            background = a.split("=", 1)[1]
    if "--dry-run" in sys.argv:
        print(names, background); return
    key = os.environ["OPENAI_API_KEY"]
    for name in names:
        try:
            path, usage = generate(ref, out_dir, name, key, background)
            print("OK", os.path.basename(path), json.dumps(usage))
        except Exception as e:
            print("FAIL", e)


if __name__ == "__main__":
    main()
