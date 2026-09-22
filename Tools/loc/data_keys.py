"""Writes Tools/loc/fragments/data.json: one string-table entry per shown data field.

Localization L1 (Docs/PLAN_localization_L1.md section 5). The English stays in the data files;
this turns every shown field into a `data.<kind>.<id>.<field>` entry that merge_fragments.py folds
into Assets/Resources/Data/loc/en.json. The UI reads them with
`UIText.Data(kind, id, field, english)`, which builds the key from the object's id AT RUNTIME, so an
id is never altered here: an id the key rules refuse is reported and left out, not "fixed".

Customer voice lines, story dialogue and host lessons are a later dialogue pass and are NOT
generated here.

    py -3 -X utf8 Tools/loc/data_keys.py

Deterministic (entries sorted by key) and idempotent (the file is only rewritten when it changes).
Exit code 1 when an id breaks the key rules, a text breaks the placeholder rules, or a source file is
missing or malformed.
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Tools" / "loc" / "fragments" / "data.json"
RESEARCH = ROOT / "Docs" / "steam" / "research" / "loc_codebase.json"

PLURAL_SUFFIXES = ("zero", "one", "two", "few", "many", "other")
CONVENTION = re.compile(r"^[a-z0-9_]+$")  # plan section 2: lower_snake ASCII segments


def _notes_recipe_name(obj, ctx):
    return ("Cocktail name: menu, recipe book page title, order ticket; often shown in capitals. "
            "Classic names (Negroni, Mojito, Old Fashioned) are usually kept; descriptive ones are "
            "translated. Keep it about as short as the English. Mirrored in RecipeCatalog.cs.")


def _notes_recipe_lore(obj, ctx):
    name = ctx["recipe_names"].get(obj["id"], obj["id"])
    return (f"Recipe book page for \"{name}\": the house's note under the recipe, in the host's "
            "voice (TycoonHud.Book.cs). Wraps over a few lines of the page.")


def _notes_recipe_origin(obj, ctx):
    name = ctx["recipe_names"].get(obj["id"], obj["id"])
    return (f"Recipe book page for \"{name}\": the provenance line (where and when the drink comes "
            "from), authored in capitals (TycoonHud.Book.cs). One short line.")


def _notes_slot_title(obj, ctx):
    return (f"Name of a place in the room that takes one fitting (slot id {obj['id']}); the decor "
            "panel heading, shown in capitals (TycoonHud.Decor.cs). A few words.")


def _notes_slot_place(obj, ctx):
    return (f"Where the slot \"{obj.get('title') or obj['id']}\" sits in the room, the meta line on the "
            "decor panel and the fitting's card (TycoonHud.Decor.cs, TycoonHud.Chrome.cs). A few words.")


def _notes_bottle_name(obj, ctx):
    return ("Bottle name on the market tile and the cellar card: a parody brand plus a generic noun. "
            "Keep the brand exactly as it is; translate only the generic noun (Vodka, Gin, Rum...).")


def _notes_bottle_style(obj, ctx):
    return (f"Style word of the bottle \"{obj.get('name', obj['id'])}\", shown in capitals in the "
            "market meta line and on the cellar card (TycoonHud.Market.cs). Lower-case in the data; "
            "the code capitalises it. One or two words.")


def _notes_fixture_name(obj, ctx):
    slot = ctx["slot_titles"].get(obj.get("slot", ""), obj.get("slot", ""))
    return (f"Upgrade name on the market / decor tile (slot: {slot}). Short title line; "
            "shown in capitals.")


def _notes_fixture_blurb(obj, ctx):
    return (f"Body text of the upgrade tile for \"{obj.get('name', obj['id'])}\" "
            "(TycoonHud.Decor.cs, TycoonHud.DayEnd.cs). Wraps over a few lines.")


def _notes_glass_name(obj, ctx):
    return ("Glass name. Also lower-cased and set inside day-end sentences (TycoonHud.DayEnd.cs), "
            "so keep it a plain noun.")


def _notes_country_name(obj, ctx):
    return ("Country on the customer's licence, CITIZEN OF field, printed in capitals "
            "(TycoonHud.Id.cs). Keyed by the ISO code papers.json uses. Use the language's own "
            "short name for the country.")


def _notes_archetype_name(obj, ctx):
    return ("Customer archetype label, shown only in the developer cast guide (ARCHETYPE column); "
            "not seen in player builds.")


# kind, field, source file, list property, id property, text property, note builder, trim
# `trim` mirrors the loader: RecipeLore.Parse trims the note (not the origin, which it takes as
# `origin ?? ""`); DataLoader passes the rest as-is.
PAIRS = [
    ("recipe", "name", "Assets/Data/recipes/recipes.json", "recipes", "id", "name", _notes_recipe_name, False),
    ("recipe", "lore", "Assets/Resources/Data/recipes_lore.json", "entries", "id", "note", _notes_recipe_lore, True),
    ("recipe", "origin", "Assets/Resources/Data/recipes_lore.json", "entries", "id", "origin", _notes_recipe_origin, False),
    ("slot", "title", "Assets/Data/fixtures/fixtures.json", "slots", "id", "title", _notes_slot_title, False),
    ("slot", "place", "Assets/Data/fixtures/fixtures.json", "slots", "id", "place", _notes_slot_place, False),
    ("bottle", "name", "Assets/Data/bottles/base_bar.json", "cards", "id", "name", _notes_bottle_name, False),
    ("bottle", "style", "Assets/Data/bottles/base_bar.json", "cards", "id", "style", _notes_bottle_style, False),
    ("fixture", "name", "Assets/Data/fixtures/fixtures.json", "fixtures", "id", "name", _notes_fixture_name, False),
    ("fixture", "blurb", "Assets/Data/fixtures/fixtures.json", "fixtures", "id", "flavor", _notes_fixture_blurb, False),
    ("glass", "name", "Assets/Data/glassware/glassware.json", "glasses", "id", "name", _notes_glass_name, False),
    ("country", "name", "Assets/Data/customers/papers.json", "papers", "iso", "country", _notes_country_name, False),
    # the strangers a borrowed licence wears (2026-09-22): their flags carry a country's name on the card's tip too
    ("country", "name", "Assets/Resources/Data/strangers.json", "papers", "iso", "country", _notes_country_name, False),
    ("archetype", "name", "Assets/Data/customers/archetypes.json", "archetypes", "id", "name", _notes_archetype_name, False),
]

# (file, loc_codebase.json field) -> the pair that covers it
COVERED = {
    ("Assets/Data/recipes/recipes.json", ".recipes[].name"): "recipe/name",
    ("Assets/Resources/Data/recipes_lore.json", ".entries[].note"): "recipe/lore",
    ("Assets/Resources/Data/recipes_lore.json", ".entries[].origin"): "recipe/origin",
    ("Assets/Data/fixtures/fixtures.json", ".slots[].title"): "slot/title",
    ("Assets/Data/fixtures/fixtures.json", ".slots[].place"): "slot/place",
    ("Assets/Data/bottles/base_bar.json", ".cards[].name"): "bottle/name",
    ("Assets/Data/bottles/base_bar.json", ".cards[].style"): "bottle/style",
    ("Assets/Data/fixtures/fixtures.json", ".fixtures[].name"): "fixture/name",
    ("Assets/Data/fixtures/fixtures.json", ".fixtures[].flavor"): "fixture/blurb",
    ("Assets/Data/glassware/glassware.json", ".glasses[].name"): "glass/name",
    ("Assets/Data/customers/papers.json", ".papers[].country"): "country/name",
    ("Assets/Resources/Data/strangers.json", ".papers[].country"): "country/name",
    ("Assets/Data/customers/archetypes.json", ".archetypes[].name"): "archetype/name",
}

# Later dialogue pass (plan section 7): rewritten per language, not generated here.
DEFERRED_FILES = {"Assets/Data/story/story.json", "Assets/Resources/Data/voices.json"}

# Shown fields loc_codebase.json does not list, found reading the UI.
UNLISTED_SHOWN = [
    ("Assets/Data/fixtures/fixtures.json", ".slots[].title",
     "slot title, capitalised as the decor panel heading (TycoonHud.Decor.cs)"),
]
UNLISTED_SHOWN = [row for row in UNLISTED_SHOWN if (row[0], row[1]) not in COVERED]


def placeholder_problem(text):
    """Port of StringTable.PlaceholderProblem."""
    depth, start = 0, 0
    for i, c in enumerate(text):
        if c == "{":
            if depth > 0:
                return "a '{' opens inside another placeholder."
            depth, start = 1, i + 1
        elif c == "}":
            if depth == 0:
                return "a '}' closes nothing."
            depth = 0
            name = text[start:i]
            if not name:
                return "an empty placeholder '{}'."
            if any(not (("a" <= n <= "z") or ("0" <= n <= "9") or n == "_") for n in name):
                return f"placeholder '{{{name}}}' is not lower_snake_case."
    return "a '{' never closes." if depth > 0 else None


def id_problem(raw_id):
    """Why an id cannot sit inside a key StringTable accepts, or None."""
    if not isinstance(raw_id, str) or not raw_id.strip():
        return "empty or missing id"
    if any(c.isspace() for c in raw_id):
        return "whitespace in the id"
    if "#" in raw_id:
        return "'#' in the id (read as a plural suffix)"
    if "." in raw_id:
        return "'.' in the id (splits the key's segments)"
    return None


def load(rel, errors, cache):
    if rel in cache:
        return cache[rel]
    path = ROOT / rel
    try:
        with open(path, encoding="utf-8-sig") as f:
            cache[rel] = json.load(f)
    except (OSError, json.JSONDecodeError) as e:
        errors.append(f"{rel}: cannot read ({e})")
        cache[rel] = None
    return cache[rel]


def render(strings):
    lines = ["{", '  "group": "data",', '  "strings": [']
    for i, s in enumerate(strings):
        body = ", ".join(f"{json.dumps(k)}: {json.dumps(s[k], ensure_ascii=False)}"
                         for k in ("key", "text", "note"))
        lines.append("    { " + body + " }" + ("," if i < len(strings) - 1 else ""))
    lines += ["  ]", "}"]
    return "\n".join(lines) + "\n"


# Dialogue as lists (2026-09-13): one entry per line, `data.<kind>.<id>.<field>.<index>`, read by
# UIText.DataLines. Voice lines wait: they are filled in Core after the pick (VoiceBook), a pass of their own.
# kind, source file, list property, id property, {json list property: key field}
LIST_PAIRS = [
    ("lesson", "Assets/Data/story/story.json", "lessons", "id", {"say": "say"}),
    ("story", "Assets/Data/story/story.json", "beats", "id", {
        "ask": "ask", "nudge": "nudge", "servedRight": "served_right", "servedWrong": "served_wrong",
        "declined": "declined", "hostBefore": "host_before", "hostAfter": "host_after",
        "shortOfGate": "short_of_gate"}),   # hostWarning: loaded and checked, never shown
    # Customer voices: the key's middle is the VoiceCue name in lower case, the same string
    # TycoonHud.Seats.cs builds (voice.Id + "." + cue) before VoiceBook.Fill puts the drink in.
    ("voice", "Assets/Resources/Data/voices.json", "voices", "id", {
        "order": "order", "perfect": "perfect", "another": "another", "close": "close", "wrong": "wrong",
        "praise": "praise", "sip": "sip", "leaving": "leaving", "kicked": "kicked"}),
]

LIST_NOTES = {
    "voice": "What a guest with the '{id}' voice says ({field}), line {i} of {n}; one is picked at random. "
             "REWRITE it in the language the way this kind of guest would talk (their accent, slang and foreign "
             "words carry over), do not translate word for word. Keep every {{drink}} / {{advice}} / {{advice_l}} "
             "slot: {{drink}} is a drink name, {{advice}} a sentence of advice, {{advice_l}} the same with a "
             "lower-case first letter. One short spoken line in a speech balloon.",
    "lesson": "Ece, the host, teaching the player one thing (tutorial in her voice), line {i} of {n} on the "
              "talk plate / the market's host note. One sentence; warm, dry, never a manual. Lesson '{id}'.",
    "story": "Story night '{id}', {field} line {i} of {n}, said on the talk plate by the guest or the host. "
             "One sentence; spoken, characterful.",
}


def list_entries(errors, cache, entries, counts):
    for kind, rel, list_prop, id_prop, fields in LIST_PAIRS:
        data = load(rel, errors, cache)
        made = 0
        items = (data or {}).get(list_prop)
        if not isinstance(items, list):
            errors.append(f"{rel}: no '{list_prop}' list")
            counts.append((kind, "*", rel, list_prop, "lines", 0, 0))
            continue
        for obj in items:
            raw_id = obj.get(id_prop)
            problem = id_problem(raw_id)
            if problem:
                errors.append(f"{kind}: {rel} {list_prop} id {raw_id!r}: {problem}; lines left out")
                continue
            for json_prop, field in fields.items():
                lines = obj.get(json_prop) or []
                for i, text in enumerate(lines):
                    if not isinstance(text, str) or not text.strip():
                        continue
                    key = f"data.{kind}.{raw_id}.{field}.{i}"
                    problem = placeholder_problem(text)
                    if problem:
                        errors.append(f"{key}: {problem} (text {text!r}); entry left out")
                        continue
                    note = LIST_NOTES[kind].format(id=raw_id, field=field, i=i + 1, n=len(lines))
                    entries[key] = {"key": key, "text": text, "note": note}
                    made += 1
        counts.append((kind, "lines", rel, list_prop, ",".join(fields), made, 0))


def main():
    errors, warnings, cache = [], [], {}
    ctx = {"recipe_names": {}, "slot_titles": {}}
    recipes = load("Assets/Data/recipes/recipes.json", errors, cache)
    if recipes:
        ctx["recipe_names"] = {r.get("id"): r.get("name") for r in recipes.get("recipes", [])}
    fixtures = load("Assets/Data/fixtures/fixtures.json", errors, cache)
    if fixtures:
        ctx["slot_titles"] = {s.get("id"): s.get("title") or s.get("id") for s in fixtures.get("slots", [])}

    entries = {}
    counts = []
    for kind, field, rel, list_prop, id_prop, text_prop, note_of, trim in PAIRS:
        data = load(rel, errors, cache)
        made = skipped = 0
        if data is None:
            counts.append((kind, field, rel, list_prop, text_prop, 0, 0))
            continue
        items = data.get(list_prop)
        if not isinstance(items, list):
            errors.append(f"{rel}: no '{list_prop}' list")
            counts.append((kind, field, rel, list_prop, text_prop, 0, 0))
            continue
        for index, obj in enumerate(items):
            if not isinstance(obj, dict):
                errors.append(f"{rel}: {list_prop}[{index}] is not an object")
                continue
            text = obj.get(text_prop)
            if not isinstance(text, str) or not text.strip():
                skipped += 1
                continue
            if trim:
                text = text.strip()
            elif text != text.strip():
                warnings.append(f"{rel}: {list_prop}[{index}].{text_prop} has leading/trailing whitespace "
                                "(kept exactly, the loader does not trim it)")
            raw_id = obj.get(id_prop)
            problem = id_problem(raw_id)
            if problem:
                errors.append(f"{kind}/{field}: {rel} {list_prop}[{index}].{id_prop} = {raw_id!r}: {problem}; "
                              "entry left out")
                continue
            key = f"data.{kind}.{raw_id}.{field}"
            bad_segment = next((seg for seg in (kind, raw_id, field) if not CONVENTION.match(seg)), None)
            if bad_segment is not None:
                warnings.append(f"{key}: id '{raw_id}' is not lower_snake ASCII (plan section 2); kept as-is "
                                "because UIText.Data builds the key from the id")
            problem = placeholder_problem(text)
            if problem:
                errors.append(f"{key}: {problem} (text {text!r}); entry left out")
                continue
            if key in entries:
                if entries[key]["text"] != text:
                    errors.append(f"{key}: the same id carries two texts: {entries[key]['text']!r} vs {text!r}")
                continue  # e.g. many papers share one country
            entries[key] = {"key": key, "text": text, "note": note_of(obj, ctx)}
            made += 1
        counts.append((kind, field, rel, list_prop, text_prop, made, skipped))
    list_entries(errors, cache, entries, counts)

    strings = [entries[k] for k in sorted(entries)]
    content = render(strings)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    old = OUT.read_text(encoding="utf-8") if OUT.exists() else None
    if old != content:
        with open(OUT, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
        state = "wrote"
    else:
        state = "unchanged"

    print(f"{state} {OUT.relative_to(ROOT).as_posix()}: {len(strings)} entries")
    print()
    print("kind/field         entries  skipped-empty  source")
    for kind, field, rel, list_prop, text_prop, made, skipped in counts:
        print(f"{kind + '/' + field:<18} {made:>7}  {skipped:>13}  {rel} .{list_prop}[].{text_prop}")

    report_uncovered()

    if warnings:
        print()
        print(f"WARNINGS ({len(warnings)})")
        for w in warnings:
            print("  " + w)
    if errors:
        print()
        print(f"ERRORS ({len(errors)})")
        for e in errors:
            print("  " + e)
        return 1
    return 0


def report_uncovered():
    print()
    try:
        with open(RESEARCH, encoding="utf-8-sig") as f:
            files = json.load(f).get("data_files", [])
    except (OSError, json.JSONDecodeError) as e:
        print(f"(cannot read {RESEARCH.relative_to(ROOT).as_posix()} for the coverage report: {e})")
        return
    shown, other, deferred = [], [], []
    for entry in files:
        rel = entry.get("file", "")
        for fld in entry.get("fields", []):
            visible = fld.get("player_visible", "")
            if visible == "no" or (rel, fld.get("field")) in COVERED:
                continue
            row = (rel, fld.get("field"), visible, fld.get("note", ""))
            if rel in DEFERRED_FILES:
                deferred.append(row)
            elif visible == "yes":
                shown.append(row)
            else:
                other.append(row)
    for rel, field, note in UNLISTED_SHOWN:
        shown.append((rel, field, "yes (not in loc_codebase.json)", note))
    print(f"SHOWN DATA FIELDS NOT COVERED BY THE {len(PAIRS)} PAIRS")
    for rel, field, visible, note in sorted(shown):
        print(f"  {rel} {field}  [{visible}]  {note}")
    print("NOT SHOWN AS DATA TEXT TODAY (indirect / not currently / dev only)")
    for rel, field, visible, note in sorted(other):
        print(f"  {rel} {field}  [{visible}]  {note}")
    print("DEFERRED TO THE DIALOGUE PASS (story, lessons, voices)")
    for rel, field, visible, note in sorted(deferred):
        print(f"  {rel} {field}  [{visible}]")


if __name__ == "__main__":
    sys.exit(main())
