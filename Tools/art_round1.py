#!/usr/bin/env python3
"""Generates art-direction round 1 concepts (Docs/ArtDirection/ART_DIRECTION_BRIEF.md §4-5).

Reads OPENAI_API_KEY from the environment. Writes PNGs and prompts.md into Docs/ArtDirection/Concepts/round1.
Usage: Tools/art_round1.py [--dry-run] [--only scene_style ...]
"""
import base64, concurrent.futures, json, os, sys, time, urllib.error, urllib.request

MODEL = "gpt-image-2"
QUALITY = "high"
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Docs", "ArtDirection", "Concepts", "round1")

COMMON = (
    "Mobile game concept art. Humorous, warm, family-friendly tone. "
    "No blood, no realistic violence, no gore. No photorealism. "
    "Characters must read clearly at small size: strong silhouettes, clear value contrast against background. "
    "Minimal text; any UI labels may be placeholder shapes. No watermark, no signature, no logos."
)

CATS = (
    "RYZHY: a ginger tomcat, broad shoulders and chunky paws, torn left ear, small scar across the nose, cocky grin - the brawler. "
    "SHNYR: a lanky grey striped tabby, oversized ears, very long thin tail, narrow shifty eyes - the sneaky one. "
    "PONCHIK: a huge round black-and-white tuxedo cat, tiny head on a big body, sleepy half-closed eyes - the tank."
)

STYLES = {
    "S1": "Cartoon 2D style, thick clean dark outlines, flat colors with simple cel shading, bold saturated palette, exaggerated proportions, in the spirit of modern mobile cartoon games.",
    "S2": "Detailed pixel art, 16-bit era inspired, limited palette with careful dithering, crisp pixel edges, expressive chunky character sprites, cozy dumpster atmosphere.",
    "S3": "Stylized 2.5D look, soft volumetric lighting, rounded dimensional characters like a stylized 3D animated film, gentle ambient occlusion, warm evening light.",
    "S4": "Comic book style, bold ink lines, halftone shading, exaggerated facial expressions and dynamic poses, punchy limited color palette, speech-bubble energy.",
    "S5": "Hand-painted casual mobile game style, soft painterly brush textures, warm cozy palette, gentle lighting, characters with clean readable shapes over painted backgrounds.",
}

SCENES = {
    "lineup": ("1536x1024",
        "Character lineup sheet of three stray cats standing side by side on a plain light neutral background, "
        "full body, three-quarter view, same ground line, showing relative size: left RYZHY, center PONCHIK, right SHNYR. "
        + CATS + " Below each cat, a small solid black silhouette of the same pose."),
    "hideout": ("1024x1536",
        "Mobile game main screen mockup, portrait orientation. Scene: the back alley behind a small restaurant at dusk, "
        "warm light from the restaurant back door. Center-left: a big rusty dumpster with junk around it. "
        "Right: a stack of cardboard boxes where a cat sleeps. Upper-left: a tin can hanging on a string from a pipe - RYZHY punches it. "
        "Upper-center: a wind-up toy mouse - SHNYR chases it. Upper-right: an improvised exercise wheel made from an old tire - "
        "PONCHIK runs in it, looking exhausted. " + CATS + " "
        "UI overlay: thin top bar with a food icon and a scrap-metal icon with numbers; bottom area with three cat portrait cards, "
        "each with two small progress bars; two large tabs at the very bottom. The game scene occupies the middle 55% of the screen."),
}

def prompt_for(scene, style):
    return f"{STYLES[style]}\n\n{SCENES[scene][1]}\n\n{COMMON}"

def generate(scene, style, key):
    size = SCENES[scene][0]
    body = json.dumps({"model": MODEL, "prompt": prompt_for(scene, style), "size": size, "quality": QUALITY, "n": 1}).encode()
    req = urllib.request.Request("https://api.openai.com/v1/images/generations", data=body,
                                 headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                data = json.load(r)
            break
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors="replace")[:300]
            if e.code in (429, 500, 502, 503) and attempt < 2:
                time.sleep(10 * (attempt + 1)); continue
            raise RuntimeError(f"{scene}_{style}: HTTP {e.code}: {msg}")
    path = os.path.join(OUT_DIR, f"{scene}_{style}.png")
    with open(path, "wb") as f:
        f.write(base64.b64decode(data["data"][0]["b64_json"]))
    return path, data.get("usage")

def main():
    dry = "--dry-run" in sys.argv
    only = sys.argv[sys.argv.index("--only") + 1:] if "--only" in sys.argv else None
    jobs = [(sc, st) for sc in SCENES for st in STYLES if not only or f"{sc}_{st}" in only]
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "prompts.md"), "w") as f:
        f.write(f"# Round 1 prompts\n\nModel: `{MODEL}`, quality: `{QUALITY}`, n=1.\n\n")
        for sc, st in [(sc, st) for sc in SCENES for st in STYLES]:
            f.write(f"## {sc}_{st} ({SCENES[sc][0]})\n\n```\n{prompt_for(sc, st)}\n```\n\n")
    if dry:
        print("\n".join(f"{sc}_{st}" for sc, st in jobs)); return
    key = os.environ["OPENAI_API_KEY"]
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as ex:
        futures = {ex.submit(generate, sc, st, key): (sc, st) for sc, st in jobs}
        for fut in concurrent.futures.as_completed(futures):
            try:
                path, usage = fut.result()
                print("OK", os.path.basename(path), json.dumps(usage) if usage else "")
            except Exception as e:
                print("FAIL", e)

if __name__ == "__main__":
    main()
