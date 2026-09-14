# Tools/loc — string-table tooling (localization L1)

Rules for keys and text: `Docs/PLAN_localization_L1.md`. The game loads
`Assets/Resources/Data/loc/<code>.json` through `StringTable`, which refuses a bad key, a bad plural
suffix or a bad placeholder. These scripts apply the same rules before the game does.

Standard library only. Run from the project root with `py -3 -X utf8`.

## Scripts

**`data_keys.py`** writes `fragments/data.json`: one `data.<kind>.<id>.<field>` entry per shown
data field, with the English copied from the data file. The thirteen pairs are recipe/name,
recipe/lore, recipe/origin, bottle/name, bottle/style, fixture/name, fixture/blurb, slot/title,
slot/place, glass/name, snack/name, country/name and archetype/name. The UI reads each one with
`UIText.Data(kind, id, field, english)`. Property mapping:

- `recipe/lore` is `recipes_lore.json` `note`, trimmed the way `RecipeLore` trims it.
- `recipe/origin` is `recipes_lore.json` `origin`, kept exactly as written (`RecipeLore` does not
  trim it).
- `fixture/blurb` is `fixtures.json` `flavor`.
- `slot/title` and `slot/place` are `fixtures.json` `slots[].title` and `slots[].place`, keyed by
  the slot's `id`.
- `country/name` is `papers.json` `country`, keyed by `iso`.
- Every other pair uses the property of the same name.

Ids are never altered, because the UI builds the key from the id. An id the key rules refuse is
reported and left out. The script also lists the shown data fields the pairs don't cover. Voice
lines, story dialogue and host lessons belong to a later pass and are not generated here. Output is
sorted, and the file is only rewritten when its content changes.

**`merge_fragments.py`** checks and merges `fragments/*.json` into `en.json`.

- `--check` (the default) reports each group:
  - entries
  - new keys
  - duplicates: same text is merged; different text is an error
  - key-rule violations
  - placeholder errors
  - counted keys missing `#one` or `#other`
  - entries without a note

  A half-written fragment is reported as UNUSABLE; the script doesn't crash on it. Exits 1 on any
  error.
- `--write` runs the check. Only if it passes, it writes `en.json`: every entry sorted by key,
  existing `note`/`src` kept, one entry per line, UTF-8, trailing newline.
- `--update` goes with `--check` or `--write`. It lets a fragment's text replace a different text
  already in `en.json`, for example after a data edit re-ran `data_keys.py`. Two fragments that
  disagree are still an error.
- `--stale <code>` lists the keys in `<code>.json` whose `src` no longer matches the current
  English. It also lists orphaned keys and counts untranslated ones.

A fragment is `{"group": "<name>", "strings": [{"key", "text", "note"}]}`, stored at
`fragments/<name>.json`.

**`assemble_table.py <code>`** joins `translations/<code>/*.json` (`{"strings": [{"key", "text",
"src"}]}`) into `Assets/Resources/Data/loc/<code>.json`. Entries already in the table and absent
from every part are kept, so a delta pass only needs parts for the new keys.

**`check_tables.py [codes] [--list-untranslated]`** checks written tables against `en.json`: keys,
placeholders, CLDR plural forms, key syntax and glyphs in the body face the language ships with
(`Assets/Scripts/UI/Text/LanguageFonts.cs`: Silkscreen for Latin-1, Galmuri7 / Galmuri9 / Galmuri11 /
Fusion Pixel 12 from `Assets/Resources/Fonts/` for the rest). A character English itself shows is not
reported, since it already falls back the same way in English.

**`ps2p_caps.py [--out path]`** builds `Assets/Fonts/MalibuArcade-Regular.ttf`, the game's heading face:
Press Start 2P (the untouched original lives in `Tools/steam_kit/fonts/`) with its accented Latin and
Cyrillic capitals redrawn at full height from `Tools/steam_kit/i18n.ps2p_glyph`, so "RÉGLAGES" does not
read "RéGLAGES". Renamed because the OFL reserves "Press Start 2P". It refuses to write unless plain
letters redrawn from their masks match the original outlines pixel for pixel.

**`check_parts.py <code> [--list-untranslated]`** runs the same check on the table plus its parts
joined in memory, without writing anything. Translators use it while the editor has `Assets/`
(a test run is going); `assemble_table.py` then writes the table when the editor is free.

## Order

1. UI and Core workers write `fragments/<group>.json`, and `data_keys.py` writes `fragments/data.json`.
2. `merge_fragments.py --check` must pass. Fix what it reports, in the group that owns the line.
3. `merge_fragments.py --write` writes `en.json`. After that, run the EditMode and PlayMode suites.
4. For translations, run `merge_fragments.py --stale <code>` to find what to re-translate. Each
   translated entry carries `src` = the English it was translated from.

```
py -3 -X utf8 Tools/loc/data_keys.py
py -3 -X utf8 Tools/loc/merge_fragments.py --check
py -3 -X utf8 Tools/loc/merge_fragments.py --write
py -3 -X utf8 Tools/loc/merge_fragments.py --stale tr
```
