# Localization L1 — moving the words into the string table (2026-09-13)

Author: Early Access 5 Nov 2026 with 28 languages in the game. L0 (the table, plurals, capitals,
fallback chain) is in `Assets/Scripts/Core/Text/` and `Assets/Scripts/Game/Localization.cs`. L1 moves
every word the player sees out of the C# into `Assets/Resources/Data/loc/en.json`, **without changing
one English pixel**: every PlayMode caption assert and both look baselines must hold as they are.

## 1 · The API (`Assets/Scripts/UI/Text/UIText.cs`)

| Call | For |
|---|---|
| `UIText.T("market.tab.restock")` | a fixed line |
| `UIText.T("market.more_at", ("noun", noun), ("stars", next))` | a sentence with named slots `{noun}`, `{stars}` |
| `UIText.N("dayend.things_open", opens)` | a counted line: `key#one` + `key#other` in en.json, count is `{n}` |
| `UIText.Caps(s)` | capitals in the player's language; replaces `ToUpperInvariant()` on anything shown |
| `UIText.CapsT(key, ...)` | `Caps(T(...))` |
| `UIText.Data("recipe", recipe.Id, "name", recipe.Name)` | words whose English lives in a data file (key `data.recipe.<id>.name`); falls back to that English |
| `UIText.Refusal(e)` | replaces `Toast(e.Message.ToUpperInvariant())` → `Toast(UIText.Refusal(e))` |

Core sentences return a `Line` (key + args) next to the English they already return. A Core refusal the
player sees keeps its exception type and English message (tests assert both) and attaches its sentence:
`throw Said.With(new InvalidOperationException("…"), Line.Of("rule.…").With(...))`.

## 2 · Keys

`area.element[.variant]`, lower_snake segments joined by dots, ASCII only. Areas by file:

| File | Area prefix |
|---|---|
| TycoonHud.cs | `hud.` |
| TycoonHud.Build.cs | `build.` (top bar, basket foot, keys) |
| TycoonHud.Chrome.cs | `chrome.` (settings, cellar card, prop tips, log, story plate) |
| TycoonHud.DayEnd.cs | `dayend.` (slip, ledger, market tiles drawn there) |
| TycoonHud.Market.cs | `market.` |
| TycoonHud.Decor.cs | `decor.` |
| TycoonHud.Seats.cs | `seats.` |
| TycoonHud.Id.cs | `id.` |
| TycoonHud.Book.cs | `book.` |
| TycoonHud.Recipes.cs | `recipes.` |
| TycoonHud.Curtain.cs | `curtain.` |
| DiegeticStage.cs | `stage.` |
| TycoonServiceFlow*.cs, PourHand.cs | `bench.` (`bench.shaker.`, `bench.serve.`, `bench.tap.`) |
| Core rules and sentences | `rule.` (refusals), `advice.`, `unlock.`, `calendar.`, `job.`, `prep.`, `papers.` |
| data files | `data.<kind>.<id>.<field>` (generated, §5) |

A key is unique across the whole table. Reuse a key only for the *same* word in the *same* role
(`common.close`, `common.total` live under `common.`).

## 3 · What the text looks like in en.json

- **Exactly what the player sees today**, capitals included. A literal the code upper-cased at runtime
  is stored already in capitals and the `ToUpperInvariant` goes away; a *dynamic* value (a name) keeps a
  `UIText.Caps(...)` call.
- **One sentence, one key.** A sentence glued from pieces (`"Fit the " + next + "-line tower first"`)
  becomes one text with slots (`"Fit the {lines}-line tower first"`) — translators must be free to move
  the slot. Never split a sentence over two keys, never glue two keys into a sentence.
- **Plurals** (`n == 1 ? " drink" : " drinks"`) become a counted key: `key#one` / `key#other`.
- Slots are `{lower_snake}`; numbers and money are passed pre-formatted exactly as today (`"$" + x`
  stays a value, not text), so English output does not move by a character.
- Each entry gets a **`note`**: where it shows, what fills the slots, and the room it has
  ("caption on a 96 px key, 8 px face, ~12 characters", "toast line, one line").
- `\n` inside a text is kept where the code had one (tests assert `"PLACE\nORDER"`).

## 4 · What stays in C#

`Debug.Log*`, developer exceptions nobody sees in play, resource and sprite paths, GameObject names
(the first `NewText`/`NewRect` argument), ids, enum names used as keys, tags, animator states, format
strings for numbers (`"0.0"`), single separators and symbols (`·`, `×`, `+`, `$`), and the **DEV TOOLS
bench** (developer-only; it is hidden from player builds instead — `#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

## 5 · Data text

The English stays in the data files; `Tools/loc/data_keys.py` writes a `data.*` entry for every shown
field into en.json (so a data edit re-generates, and the parity test catches drift). Kinds and fields:
`recipe` (name, lore/note), `bottle` (name, style), `fixture` (name, blurb), `glass` (name), `snack`
(name), `country` (name, by ISO code), `archetype` (name), `lesson` (text), `story` (dialogue lines),
`voice` (lines per cue — **rewritten per language**, not translated). Brand parody names inside bottle
names stay as they are; only the generic noun is translated.

## 6 · Tests and the English pin

`LookTests` and `ServiceSmokeTests` call `Localization.UseForSession("en")` in their one-time set-up.
EditMode tests that assert English sentences (PourAdvice, UnlockCondition, story/papers messages) keep
passing because Core keeps producing the same English; the `Line` rides alongside.

## 7 · Working in parallel

One worker per file group; nobody edits a file outside their group. Each worker writes its entries to
`Tools/loc/fragments/<group>.json` (`{"group", "strings":[{"key","text","note"}]}`); the merge into
en.json is one step at the end (`Tools/loc/merge_fragments.py`; duplicates and collisions resolved
there). Compile outside the editor with a private copy of the scratch UI project (never under the
project's `Temp/`, which Unity wipes); errors in another group's files mid-flight are theirs.

Customer voice lines, story dialogue and host lessons are a separate dialogue pass after L1: they are
rewritten per language, and their placeholders are filled in Core after the line is chosen.

## 8 · After L1

L2 overflow audit with the pseudo-locale; L3 per-language fonts; L4 the 28 translations of en.json
against `Docs/marketing/i18n/GLOSSARY.md` and the `glossary_<code>.json` terms the store copy chose.
