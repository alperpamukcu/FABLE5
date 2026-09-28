"""The Steamworks half of the achievements (2026-09-28): everything the author types or uploads on the
partner site, written from the game's own data so the two can never disagree.

    py -3 -X utf8 Tools/steamworks/achievements_kit.py                    write Tools/steamworks/out/
    py -3 -X utf8 Tools/steamworks/achievements_kit.py --from export.vdf  fill Steam's own exported file

Out (Tools/steamworks/out/, regenerated, not kept in git):
  SHEET.md                      the stats and achievements to create, in order, with every field
  achievements_loc.vdf          names and descriptions in all 29 languages, for Stats & Achievements ->
                                Achievement Localization -> Import. Its tokens (NEW_ACHIEVEMENT_<block>_<bit>)
                                assume the achievements were created in SHEET.md's order: Steam numbers them
                                32 to a block, in the order they are added.
  rich_presence/<lang>.vdf      the status friends see (steam.presence.* in the string tables), one file per
                                language, for Community -> Rich Presence Localization.

--from: Steam's own export (Achievement Localization -> Export) names the tokens it actually assigned; this
fills every language into THAT file by matching each token's English name to the game's, so the order the
achievements were created in stops mattering.

Sources: Assets/Resources/Data/achievements.json (the book), Assets/Resources/Data/loc/<code>.json (the
translations; a language with a gap falls back to English there, as the game does), and
Assets/Scripts/Core/Text/Languages.cs (the Steam API name of every language the game speaks).
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BOOK = ROOT / "Assets" / "Resources" / "Data" / "achievements.json"
LOC = ROOT / "Assets" / "Resources" / "Data" / "loc"
LANGS = ROOT / "Assets" / "Scripts" / "Core" / "Text" / "Languages.cs"
STATS = ROOT / "Assets" / "Scripts" / "Core" / "Achievements" / "Stats.cs"
OUT = ROOT / "Tools" / "steamworks" / "out"

PRESENCE = [("#Status_Menu", "steam.presence.menu"), ("#Status_Night", "steam.presence.night"),
            ("#Status_Books", "steam.presence.books")]


def languages():
    """(code, steam api name) for every language the game speaks, English first."""
    text = LANGS.read_text(encoding="utf-8")
    pairs = re.findall(r'new Info\("([^"]+)",\s*"([^"]+)"', text)
    if not pairs:
        sys.exit("no languages found in Languages.cs")
    return pairs


def stats():
    """(name, kind) for every stat, in Stats.All's order: the summed ones, then the bests."""
    text = STATS.read_text(encoding="utf-8")
    consts = dict(re.findall(r'public const string (\w+) = "([a-z_]+)";', text))

    def block(name):
        m = re.search(r'private static readonly string\[\] ' + name + r' =\s*\{(.*?)\};', text, re.S)
        return [consts[w] for w in re.findall(r'\b([A-Z]\w+)\b', m.group(1))]
    return [(s, "sum") for s in block("Summed")] + [(s, "best") for s in block("Bests")]


def table(code):
    path = LOC / f"{code}.json"
    if not path.exists():
        return {}
    return {e["key"]: e["text"] for e in json.loads(path.read_text(encoding="utf-8"))["strings"]}


def vdf_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")


def texts(book, langs):
    """{steam language: {achievement id: (name, description)}}, English filling any gap."""
    en = table("en")
    out = {}
    for code, api in langs:
        t = table(code)
        per = {}
        for a in book:
            key = "achievement." + a["id"].lower()
            name = t.get(key + ".name") or en.get(key + ".name") or a["name"]
            desc = t.get(key + ".description") or en.get(key + ".description") or a["description"]
            per[a["id"]] = (name, desc)
        out[api] = per
    return out


def write_loc_vdf(book, langs, path, tokens=None):
    """tokens: {achievement id: token stem}; by default the stem Steam gives the k-th one created."""
    if tokens is None:
        tokens = {a["id"]: f"NEW_ACHIEVEMENT_{k // 32 + 1}_{k % 32}" for k, a in enumerate(book)}
    all_texts = texts(book, langs)
    lines = ['"lang"', "{"]
    for _, api in langs:
        lines += [f'\t"{api}"', "\t{", '\t\t"Tokens"', "\t\t{"]
        for a in book:
            if a["id"] not in tokens:
                continue
            name, desc = all_texts[api][a["id"]]
            stem = tokens[a["id"]]
            lines.append(f'\t\t\t"{stem}_NAME"\t"{vdf_escape(name)}"')
            lines.append(f'\t\t\t"{stem}_DESC"\t"{vdf_escape(desc)}"')
        lines += ["\t\t}", "\t}"]
    lines += ["}", ""]
    path.write_text("\n".join(lines), encoding="utf-8")


def tokens_from_export(export_path, book):
    """Match the exported file's English _NAME tokens to the book's English names."""
    text = Path(export_path).read_text(encoding="utf-8-sig")
    m = re.search(r'"english"\s*\{\s*"Tokens"\s*\{(.*?)\}', text, re.S | re.I)
    if not m:
        sys.exit("the export has no english Tokens block")
    by_name = {a["name"].strip().lower(): a["id"] for a in book}
    found = {}
    for stem, name in re.findall(r'"(\w+)_NAME"\s*"((?:[^"\\]|\\.)*)"', m.group(1)):
        aid = by_name.get(name.replace('\\"', '"').strip().lower())
        if aid:
            found[aid] = stem
    missing = [a["id"] for a in book if a["id"] not in found]
    if missing:
        print("not found in the export (created under another English name?):", ", ".join(missing))
    return found


def write_presence(langs):
    folder = OUT / "rich_presence"
    folder.mkdir(parents=True, exist_ok=True)
    en = table("en")
    for code, api in langs:
        t = table(code)
        lines = ['"lang"', "{", f'\t"Language"\t"{api}"', '\t"Tokens"', "\t{"]
        for token, key in PRESENCE:
            s = t.get(key) or en.get(key)
            s = s.replace("{night}", "%night%").replace("{stars}", "%stars%")
            lines.append(f'\t\t"{token}"\t"{vdf_escape(s)}"')
        lines += ["\t}", "}", ""]
        (folder / f"{api}.vdf").write_text("\n".join(lines), encoding="utf-8")


def write_sheet(book, stat_list):
    by_stat = {}
    for a in book:
        for s in a.get("stats") or [a["stat"]]:
            by_stat.setdefault(s, []).append(a["id"])
    kinds = dict(stat_list)
    lines = [
        "# Steamworks sheet — stats and achievements",
        "",
        "Written by `Tools/steamworks/achievements_kit.py` from `Assets/Resources/Data/achievements.json`; do not",
        "edit by hand. The steps around it are in `Docs/STEAMWORKS.md`.",
        "",
        "## 1. Stats (Stats & Achievements → Stats) — create these first",
        "",
        "Every stat: type **INT**, *Set By* **Client**, default 0, no min/max. A **sum** stat only ever grows",
        "(tick *Increment Only*); a **best** stat is the highest value ever reached (leave it unticked — the game",
        "never lowers it, but Steam must not refuse the first write).",
        "",
        "| # | API name | kind | read by |",
        "|--:|---|---|---|",
    ]
    for i, (s, kind) in enumerate(stat_list, 1):
        lines.append(f"| {i} | `{s}` | {kind} | {', '.join(by_stat.get(s, [])) or '—'} |")
    lines += [
        "",
        "## 2. Achievements (Stats & Achievements → Achievements) — create them IN THIS ORDER",
        "",
        "Steam numbers achievements 32 to a block in the order they are added; `achievements_loc.vdf` counts on",
        "this order (or fill Steam's own export with `--from`). *Hidden* goes on exactly the secret ones. Where a",
        "row names a progress stat, set it under *Progress Stat* with min 0 and the max shown — that is what",
        "draws the bar on Steam. Icons (64×64 JPG, one lit, one grey) are still to be picked.",
        "",
        "| # | API name | name (English) | description (English) | hidden | progress stat (min–max) |",
        "|--:|---|---|---|:-:|---|",
    ]
    for i, a in enumerate(book, 1):
        stat_names = a.get("stats") or [a["stat"]]
        progress = "—"
        if len(stat_names) == 1 and a["target"] > 1 and kinds.get(stat_names[0]) == "sum":
            progress = f"`{stat_names[0]}` (0–{a['target']})"
        lines.append(f"| {i} | `{a['id']}` | {a['name']} | {a['description']} | {'yes' if a.get('hidden') else ''} | {progress} |")
    lines += ["", f"{len(book)} achievements, {len(stat_list)} stats.", ""]
    (OUT / "SHEET.md").write_text("\n".join(lines), encoding="utf-8")


def main(argv):
    book = json.loads(BOOK.read_text(encoding="utf-8"))["achievements"]
    langs = languages()
    OUT.mkdir(parents=True, exist_ok=True)
    if "--from" in argv:
        export = argv[argv.index("--from") + 1]
        found = tokens_from_export(export, book)
        write_loc_vdf(book, langs, OUT / "achievements_loc_from_export.vdf", found)
        print(f"filled {len(found)} of {len(book)} achievements into out/achievements_loc_from_export.vdf")
        return 0
    stat_list = stats()
    write_sheet(book, stat_list)
    write_loc_vdf(book, langs, OUT / "achievements_loc.vdf")
    write_presence(langs)
    print(f"{len(book)} achievements, {len(stat_list)} stats, {len(langs)} languages -> {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
