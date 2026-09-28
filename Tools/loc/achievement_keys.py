"""Writes Tools/loc/fragments/achievements.json: the achievements' names and descriptions, and the Steam
rich-presence lines, as string-table entries (2026-09-28).

The English stays in Assets/Resources/Data/achievements.json. An achievement's id is its Steamworks API
name and so UPPER_SNAKE, which the data keys' lower_snake rule refuses; its entries are keyed on the id
lowered instead — `achievement.<id>.name` / `.description` — and the UI builds the same key at runtime
(TycoonHud.Achievements: AchievementText). The rich-presence lines are not drawn by the game at all: they
go to Steamworks in its own file (Tools/steamworks/steam_kit.py), but they are translated with
everything else, so they live in the tables too.

    py -3 -X utf8 Tools/loc/achievement_keys.py

Then merge_fragments.py --update --write as for any fragment. Deterministic; rewrites only on change.
"""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets" / "Resources" / "Data" / "achievements.json"
OUT = ROOT / "Tools" / "loc" / "fragments" / "achievements.json"

TIER_NOTE = {
    "opening": "earned on the first night",
    "week": "earned in the first week",
    "early": "earned in the first few weeks",
    "mid": "a mid-game goal",
    "late": "one of the hardest in the game",
    "secret": "a SECRET: hidden until earned, a small joke about a mistake",
}

PRESENCE = [
    ("steam.presence.menu", "At the front door",
     "Steam rich presence: what a friend's list shows while this player is on the game's main menu. Sentence "
     "case, a few words; Steam draws it, not the game."),
    ("steam.presence.night", "Night {night} · {stars}★",
     "Steam rich presence while a night is being worked: {night} is the night number, {stars} the bar's standing "
     "in whole stars (0-5), followed by a star sign. Keep both placeholders and the star sign; a few words."),
    ("steam.presence.books", "Closing the books on night {night}",
     "Steam rich presence at the end of a night (the slip, the market): {night} is the night number. A few words."),
]


def main():
    try:
        data = json.loads(SRC.read_text(encoding="utf-8"))
    except (OSError, ValueError) as e:
        print(f"cannot read {SRC}: {e}")
        return 1
    rows = []
    for a in data.get("achievements", []):
        aid = a["id"]
        key = "achievement." + aid.lower()
        tier = TIER_NOTE.get(a.get("tier", ""), a.get("tier", ""))
        target = a.get("target", 1)
        rows.append({
            "key": key + ".name",
            "text": a["name"],
            "note": (f"Achievement name (Steam API {aid}; {tier}). Shown on Steam and in the game's ACHIEVEMENTS "
                     "list and unlock notice. Steam title case in English; use the language's own convention. "
                     "Short: a few words, like a pub sign or a bartender's saying. Wordplay may be replaced by a "
                     "natural phrase in the language."),
        })
        rows.append({
            "key": key + ".description",
            "text": a["description"],
            "note": (f"What earns the achievement \"{a['name']}\" (target {target}). One sentence, shown under the name "
                     "on Steam and in the game's list. Keep every number and money figure exactly; plain and precise, "
                     "the player must know what to do."),
        })
    for key, text, note in PRESENCE:
        rows.append({"key": key, "text": text, "note": note})
    rows.sort(key=lambda r: r["key"])

    lines = ['{', '  "group": "achievements",', '  "strings": [']
    for i, r in enumerate(rows):
        comma = "," if i < len(rows) - 1 else ""
        lines.append("    " + json.dumps(r, ensure_ascii=False) + comma)
    lines += ['  ]', '}', '']
    text = "\n".join(lines)
    if OUT.exists() and OUT.read_text(encoding="utf-8") == text:
        print(f"{OUT.name}: {len(rows)} entries, unchanged")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"{OUT.name}: {len(rows)} entries written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
