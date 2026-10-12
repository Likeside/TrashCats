#!/usr/bin/env python3
"""Edits enemy stats in both the game config asset and the test config (they must stay identical).
Usage: Tools/set_enemy_stats.py enemy_id STR AGI TOU [HP_PERMILLE] ..."""
import re, sys

ASSET = "Assets/_Project/Configs/GameConfig.asset"
TESTS = "Assets/_Project/Tests/EditMode/Support/TestConfigs.cs"

def set_asset(text, enemy, s, a, t, hp):
    start = text.index(f"- id: {enemy}\n")
    end = text.find("\n  - id:", start + 1)
    end = len(text) if end < 0 else end
    block = text[start:end]
    for field, value in (("strength", s), ("agility", a), ("toughness", t), ("hpMultiplierPermille", hp)):
        if value is not None:
            block = re.sub(rf"(\n    {field}: )-?\d+", rf"\g<1>{value}", block, count=1)
    return text[:start] + block + text[end:]

def set_tests(text, enemy, s, a, t, hp):
    const = {"enemy_rat": "Rat", "enemy_gull": "Gull", "enemy_shopcat": "ShopCat", "enemy_crow": "Crow", "enemy_bigrat": "BigRat",
             "enemy_crow_boss": "CrowBoss", "enemy_raccoon": "Raccoon", "enemy_pug": "Pug", "enemy_admiral": "Admiral"}[enemy]
    if enemy == "enemy_admiral":
        text = re.sub(r"(Id = TestIds\.Admiral, NameKey = \"enemy\.admiral\", Strength = )\d+(, Agility = )\d+(, Toughness = )\d+",
                      rf"\g<1>{s}\g<2>{a}\g<3>{t}", text)
        if hp is not None:
            text = re.sub(r"HpMultiplierPermille = \d+, ViewKey = \"admiral\"", f"HpMultiplierPermille = {hp}, ViewKey = \"admiral\"", text)
        return text
    return re.sub(rf"(Enemy\(TestIds\.{const}, \"[^\"]+\", )\d+, \d+, \d+", rf"\g<1>{s}, {a}, {t}", text)

args = sys.argv[1:]
asset, tests = open(ASSET).read(), open(TESTS).read()
i = 0
while i < len(args):
    enemy = args[i]; s, a, t = map(int, args[i + 1:i + 4]); i += 4
    hp = None
    if i < len(args) and args[i].isdigit():
        hp = int(args[i]); i += 1
    asset, tests = set_asset(asset, enemy, s, a, t, hp), set_tests(tests, enemy, s, a, t, hp)
    print(f"{enemy}: {s}/{a}/{t}" + (f" hp {hp}" if hp else ""))
open(ASSET, "w").write(asset); open(TESTS, "w").write(tests)
