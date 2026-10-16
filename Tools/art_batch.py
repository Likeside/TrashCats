#!/usr/bin/env python3
"""Batch art generation via the OpenAI Images edit endpoint (gpt-image-2), with reference images and stages.

Usage: Tools/art_batch.py <jobs.json> <out_dir> [--only name1,name2] [--dry-run]
jobs.json: [{"name": "...", "refs": ["path.png", ...], "prompt": "...", "size": "1536x1024", "mask": "optional.png"}, ...]
A job is skipped if <out_dir>/<name>.png already exists (re-runs only generate what is missing).
Reads OPENAI_API_KEY from the environment. Appends every prompt to <out_dir>/prompts.md.
"""
import base64, concurrent.futures, json, os, subprocess, sys, tempfile, threading

MODEL = "gpt-image-2"
lock = threading.Lock()


def run(job, out_dir, key):
    out = os.path.join(out_dir, job["name"] + ".png")
    if os.path.exists(out):
        return job["name"], "exists", None
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as tmp:
        resp = tmp.name
    # the key goes through a private header file, never through the command line or error messages
    with tempfile.NamedTemporaryFile("w", suffix=".hdr", delete=False) as hdr:
        hdr.write(f"Authorization: Bearer {key}\n")
        header_file = hdr.name
    os.chmod(header_file, 0o600)
    cmd = ["curl", "-sS", "--max-time", "400", "https://api.openai.com/v1/images/edits", "-H", f"@{header_file}",
           "-F", f"model={MODEL}", "-F", f"prompt={job['prompt']}", "-F", f"size={job.get('size', '1536x1024')}",
           "-F", "quality=high", "-F", "background=opaque", "-o", resp]
    for ref in job["refs"]:
        cmd += ["-F", f"image[]=@{ref}"]
    if job.get("mask"):  # PNG the size of the first image, transparent where the model may draw
        cmd += ["-F", f"mask=@{job['mask']}"]
    try:
        code = subprocess.run(cmd, capture_output=True, text=True).returncode
    finally:
        os.remove(header_file)
    if code != 0:
        return job["name"], f"curl failed with exit code {code}", None
    data = json.load(open(resp))
    os.remove(resp)
    if "error" in data:
        return job["name"], "error: " + str(data["error"].get("message"))[:200], None
    with open(out, "wb") as f:
        f.write(base64.b64decode(data["data"][0]["b64_json"]))
    with lock, open(os.path.join(out_dir, "prompts.md"), "a") as f:
        f.write(f"## {job['name']} ({job.get('size')}, refs: {', '.join(os.path.basename(r) for r in job['refs'])})\n\n```\n{job['prompt']}\n```\n\n")
    return job["name"], "ok", data.get("usage", {}).get("output_tokens")


def main():
    jobs = json.load(open(sys.argv[1]))
    out_dir = sys.argv[2]
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
        jobs = [j for j in jobs if j["name"] in only]
    os.makedirs(out_dir, exist_ok=True)
    if "--dry-run" in sys.argv:
        for j in jobs:
            print(j["name"], j.get("size"), j["refs"])
        return
    key = os.environ["OPENAI_API_KEY"]
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        for name, status, tokens in pool.map(lambda j: run(j, out_dir, key), jobs):
            print(f"{name}: {status}" + (f" ({tokens} tokens)" if tokens else ""), flush=True)


if __name__ == "__main__":
    main()
