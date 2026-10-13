#!/usr/bin/env python3
"""Moves player-facing (Cyrillic) string literals of the Presentation layer into the localization table.

Every "..." or $"..." literal whose text contains Cyrillic becomes Loc.T("<file>.<nn>", args...), the text goes to
Assets/_Project/Resources/Localization/ru.json with interpolation holes turned into {0}, {1}... (format specifiers and
alignment kept). Literals that must stay compile-time constants (const, attributes, case labels) are reported and left.
Existing keys in ru.json are kept, so the tool can be re-run after new strings are added.
Usage: Tools/extract_strings.py [--dry-run]
"""
import glob, json, os, re, sys

ROOT = "Assets/_Project/Scripts/Presentation"
TABLE = "Assets/_Project/Resources/Localization/ru.json"
SKIP_FILES = {"UiSkin.cs", "Loc.cs"}
SKIP_DIRS = {"Dev"}
CYR = re.compile(r"[А-Яа-яЁё]")


def snake(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


class Literal:
    def __init__(self, start, end, interpolated, parts, holes):
        self.start, self.end = start, end
        self.interpolated = interpolated
        self.parts = parts  # text pieces (unescaped), len = len(holes) + 1
        self.holes = holes  # (expression source, alignment+format suffix)


def unescape(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            n = s[i + 1]
            out.append({"n": "\n", "t": "\t", "\"": "\"", "\\": "\\", "'": "'", "0": "\0"}.get(n, "\\" + n))
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def scan_string(src, i):
    """src[i] is the opening quote of a regular string; returns index after the closing quote."""
    i += 1
    while src[i] != '"':
        i += 2 if src[i] == "\\" else 1
    return i + 1


def scan_interpolated(src, i):
    """src[i] == '$', src[i+1] == '"'. Returns (end, parts, holes)."""
    i += 2
    parts, holes, buf = [], [], []
    while True:
        c = src[i]
        if c == '"':
            parts.append(unescape("".join(buf)))
            return i + 1, parts, holes
        if c == "\\":
            buf.append(src[i:i + 2]); i += 2; continue
        if c == "{" and src[i + 1] == "{":
            buf.append("{{"); i += 2; continue
        if c == "}" and src[i + 1] == "}":
            buf.append("}}"); i += 2; continue
        if c == "{":
            parts.append(unescape("".join(buf))); buf = []
            j, depth, start = i + 1, 0, i + 1
            fmt_at = None
            while True:
                d = src[j]
                if d == '"':
                    j = scan_string(src, j); continue
                if d == "$" and src[j + 1] == '"':
                    j = scan_interpolated(src, j)[0]; continue
                if d == "'":
                    j = src.index("'", j + 2 if src[j + 1] == "\\" else j + 1) + 1; continue
                if d in "([{":
                    depth += 1
                elif d in ")]}":
                    if depth == 0:
                        break
                    depth -= 1
                elif d in ":," and depth == 0 and fmt_at is None:
                    fmt_at = j
                j += 1
            expr = src[start:fmt_at if fmt_at is not None else j]
            suffix = src[fmt_at:j] if fmt_at is not None else ""
            holes.append((expr, suffix))
            i = j + 1
            continue
        buf.append(c)
        i += 1


def literals(src, lo=0, hi=None):
    """Top-level string literals in src[lo:hi], skipping comments and chars."""
    hi = len(src) if hi is None else hi
    i, found = lo, []
    while i < hi:
        c = src[i]
        if src.startswith("//", i):
            i = src.find("\n", i); i = hi if i < 0 else i; continue
        if src.startswith("/*", i):
            i = src.index("*/", i) + 2; continue
        if c == "'":
            i = src.index("'", i + 2 if src[i + 1] == "\\" else i + 1) + 1; continue
        if c == "@" and src[i + 1] == '"':
            i = src.index('"', i + 2) + 1; continue
        if c == "$" and src[i + 1] == '"':
            end, parts, holes = scan_interpolated(src, i)
            found.append(Literal(i, end, True, parts, holes)); i = end; continue
        if c == '"':
            end = scan_string(src, i)
            found.append(Literal(i, end, False, [unescape(src[i + 1:end - 1])], [])); i = end; continue
        i += 1
    return found


def constant_context(src, pos):
    line_start = src.rfind("\n", 0, pos) + 1
    before = src[line_start:pos]
    return bool(re.search(r"\bconst\b", before) or re.match(r"\s*\[", before) or re.search(r"\bcase\s*$", before))


def template(lit):
    out = []
    for k, part in enumerate(lit.parts):
        out.append(part if lit.interpolated else part.replace("{", "{{").replace("}", "}}"))
        if k < len(lit.holes):
            out.append("{" + str(k) + lit.holes[k][1] + "}")
    return "".join(out)


class Converter:
    def __init__(self, table):
        self.table = table
        self.by_text = {}
        self.skipped = []

    def key_for(self, prefix, text):
        for k, v in self.table.items():
            if v == text and k.startswith(prefix + "."):
                return k
        n = 1 + max([int(k.rsplit(".", 1)[1]) for k in self.table if k.startswith(prefix + ".") and k.rsplit(".", 1)[1].isdigit()] or [0])
        key = f"{prefix}.{n:02d}"
        self.table[key] = text
        return key

    def convert(self, src, prefix, path):
        out, last = [], 0
        for lit in literals(src):
            text = "".join(lit.parts)
            if not CYR.search(text):
                # still convert Cyrillic literals nested inside the holes
                if lit.interpolated and any(CYR.search(h[0]) for h in lit.holes):
                    out.append(src[last:lit.start])
                    out.append(self.rebuild_holes(src[lit.start:lit.end], prefix, path))
                    last = lit.end
                continue
            if constant_context(src, lit.start):
                self.skipped.append(f"{path}: {src[lit.start:lit.end][:70]}")
                continue
            args = [self.convert(h[0], prefix, path).strip() for h in lit.holes]
            key = self.key_for(prefix, template(lit))
            call = f'Loc.T("{key}"' + "".join(", " + a for a in args) + ")"
            out.append(src[last:lit.start])
            out.append(call)
            last = lit.end
        out.append(src[last:])
        return "".join(out)

    def rebuild_holes(self, literal_src, prefix, path):
        # literal_src is $"...": convert inside each hole, keep the rest verbatim
        lit = literals(literal_src)[0]
        res, i = [], 2
        body = literal_src
        out = ['$"']
        j = 2
        for expr, suffix in lit.holes:
            k = body.index("{" + expr, j)
            out.append(body[j:k + 1])
            out.append(self.convert(expr, prefix, path))
            out.append(suffix)
            j = k + 1 + len(expr) + len(suffix)
        out.append(body[j:])
        return "".join(out)


def main():
    dry = "--dry-run" in sys.argv
    table = json.load(open(TABLE, encoding="utf-8")) if os.path.exists(TABLE) else {}
    conv = Converter(table)
    changed = 0
    for path in sorted(glob.glob(f"{ROOT}/**/*.cs", recursive=True)):
        if os.path.basename(path) in SKIP_FILES or set(path.split(os.sep)) & SKIP_DIRS:
            continue
        src = open(path, encoding="utf-8").read()
        prefix = snake(os.path.splitext(os.path.basename(path))[0])
        new = conv.convert(src, prefix, path)
        if new != src:
            changed += 1
            if not dry:
                open(path, "w", encoding="utf-8").write(new)
    if not dry:
        os.makedirs(os.path.dirname(TABLE), exist_ok=True)
        with open(TABLE, "w", encoding="utf-8") as f:
            json.dump(dict(sorted(table.items())), f, ensure_ascii=False, indent=1)
    print(f"{changed} files, {len(table)} strings")
    for s in conv.skipped:
        print("kept constant:", s)


if __name__ == "__main__":
    main()
