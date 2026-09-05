# AGENTS.md — LAST CALL

Agent working rules for this repository. Read `CLAUDE.md` first: it carries the
architecture, the verification workflow (UnityMCP → compile → console → EditMode →
PlayMode → look in play) and the gotchas; `Docs/GDD/` is the design source of truth and
`Docs/GDD_MEVCUT.md` the as-built rulebook.

## Art and sound

- Chrome is DRAWN in code (`ChromeArt`, `BackBarArt`, `GlassArt`); never generated.
  Illustrative content (bottles, faces, fixtures) is generated on PixelLab through the
  scripts in `Tools/` (`pixellab.py` is the helper; the token lives in
  `Tools/pixellab_token.txt`, which git does not carry — see `Docs/HANDOFF.md`).
- Every generated asset goes through its pipeline script, is quantised to the 55-colour
  palette (`Tools/v4_bottles/palette.py`, GDD 14 §3), and is REPORTED to the author in an
  HTML contact sheet before anything enters `Assets/` — the author picks, the script ships
  (`Tools/v4_bottles/ship.py` is the model). Nothing is dragged into `Assets/` by hand.
- Open states, cellar copies and pressed states are DERIVED from the approved drawing,
  never generated again (a second take is a different object).
- Sound is synthesised by `Tools/sfx_bank.py`; a new clip is a builder in the bank, not a
  file.
- Icons the game counts in (star, heart, medallion) come from `Tools/*_icon.py` and
  `Tools/icon_sizes.py`, and are used through `ItemArt.Star/Heart/Medal` — never tinted.

## The rules that keep the game honest

- Core decides, in verbs the sim bot can call; the UI only draws.
- Hidden information stays hidden: the order and the papers throw until the card is read.
- Nothing that moves money ships without the 200-run sim (`LastCall → Simulate Tycoon
  200 Runs`) read against the tree before the change.

The Antigravity-era `lastcall-assets` MCP server (`Tools/AssetPipeline/server.py`) was
deleted on 2026-09-05; only the author's source art under `Tools/AssetPipeline/sources/` remains.
