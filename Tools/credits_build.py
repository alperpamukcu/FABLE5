# -*- coding: utf-8 -*-
"""THE CREDITS, BUILT FROM THEIR SOURCES (2026-09-27, the author: "Evet ekle" - a CREDITS screen on the
front door, for the 1-bit icon pack's attribution and the fonts' OFL, which asks for its licence text
to travel with the game).

Writes two Resources files the credits screen reads (TycoonHud.Credits):
  Assets/Resources/Data/credits.json   sections: a heading key (a string-table line) and its lines -
                                        proper nouns and addresses, the same in every language; a line
                                        starting "@" is a string-table key instead
  Assets/Resources/Data/licenses.txt   the full text of every font licence that ships, verbatim

Single sources: the fonts' own OFL files (Assets/Fonts/OFL-*.txt), the sound ledger
(Docs/SES_KAYNAKLARI.md, written by Tools/sfx_ingest.py), and Docs/KREDILER.md's picture rows mirrored
in ART below. Re-run after any of them changes.

  py -3 -X utf8 Tools/credits_build.py
"""
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
FONTS = os.path.join(ROOT, "Assets", "Fonts")
OUT_JSON = os.path.join(ROOT, "Assets", "Resources", "Data", "credits.json")
OUT_LIC = os.path.join(ROOT, "Assets", "Resources", "Data", "licenses.txt")
SOUNDS = os.path.join(ROOT, "Docs", "SES_KAYNAKLARI.md")

STUDIO = "LASGEN INTERACTIVE"
GAME = "MALIBU CLUB: COCKTAIL BAR SIMULATOR"

# third-party pictures (mirrors Docs/KREDILER.md)
ART = [
    ['"1-BIT PIXEL ICONS" BY NIKOICHU', "NIKOICHU.ITCH.IO/PIXEL-ICONS", "CC0 1.0"],
]

# every font the build carries, the face's shown name, and its licence file (all SIL OFL 1.1)
FONT_ROWS = [
    ("SILKSCREEN", "OFL-Silkscreen.txt"),
    ("MALIBU ARCADE, BASED ON PRESS START 2P", "OFL-PressStart2P.txt"),
    ("JERSEY 15", "OFL-Jersey15.txt"),
    ("INDIE FLOWER", "OFL-IndieFlower.txt"),
    ("GALMURI", "OFL-Galmuri.txt"),
    ("FUSION PIXEL 12", "OFL-FusionPixel.txt"),
]


def copyright_of(path):
    for line in io.open(path, encoding="utf-8"):
        if line.lower().startswith("copyright"):
            # the holder, without the trailing reserved-name clause or a URL in brackets
            holder = re.sub(r"^copyright\s*(\(c\))?\s*", "", line.strip(), flags=re.I)
            holder = re.sub(r",?\s*with Reserved Font Name.*$", "", holder)
            holder = re.sub(r"\s*\([^)]*\)\s*", " ", holder).strip().rstrip(".,").strip()
            return holder
    return ""


def sound_owners():
    text = io.open(SOUNDS, encoding="utf-8").read()
    owners = re.findall(r"^\| `[^`]+` \| [^|]+\| ([^|]+) \|", text, flags=re.M)
    return sorted({o.strip() for o in owners}, key=lambda s: s.lower())


def chunk(names, width=44):
    lines, cur = [], ""
    for n in names:
        piece = n if not cur else cur + " · " + n
        if len(piece) > width and cur:
            lines.append(cur)
            cur = n
        else:
            cur = piece
    if cur:
        lines.append(cur)
    return lines


def main():
    sections = [
        {"head": "chrome.credits.by", "lines": [STUDIO]},
        {"head": "chrome.credits.icons", "lines": [l for row in ART for l in row]},
    ]
    # The six licence files are one licence (SIL OFL 1.1 - they differ only in the address they print and
    # a trailing space), so the text travels ONCE, after every font's own copyright notice: the notice
    # plus the licence is what the OFL asks to accompany the font, and one copy keeps the screen's
    # text under UGUI's per-Text vertex ceiling.
    font_lines, notices, body = [], [], None
    for shown, fname in FONT_ROWS:
        path = os.path.join(FONTS, fname)
        text = io.open(path, encoding="utf-8").read().replace("\r\n", "\n")
        holder = copyright_of(path)
        font_lines.append(shown)
        font_lines.append(holder.upper())
        first = next(l for l in text.splitlines() if l.lower().startswith("copyright")).strip()
        notices.append(shown + "\n" + first + "\nLicensed under the SIL Open Font License, Version 1.1.")
        at = text.find("SIL OPEN FONT LICENSE Version 1.1")
        if body is None and at >= 0:
            body = text[text.rfind("\n", 0, at) + 1:].strip()
    assert body, "no OFL body found"
    licences = notices + [body]
    font_lines.append("SIL OPEN FONT LICENSE 1.1")
    sections.append({"head": "chrome.credits.fonts", "lines": font_lines})
    owners = sound_owners()
    sections.append({"head": "chrome.credits.sound",
                     "lines": ["FREESOUND.ORG · KENNEY.NL · CC0 1.0"] + [l.upper() for l in chunk(owners)]})
    sections.append({"head": "chrome.credits.music", "lines": ["@chrome.credits.music_line"]})

    data = {"game": GAME, "sections": sections}
    io.open(OUT_JSON, "w", encoding="utf-8", newline="\n").write(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    io.open(OUT_LIC, "w", encoding="utf-8", newline="\n").write("\n\n".join(licences))
    print("credits.json: %d sections, %d sound owners; licenses.txt: %d notices + 1 licence, %d chars"
          % (len(sections), len(owners), len(notices), sum(len(l) for l in licences)))


if __name__ == "__main__":
    main()
