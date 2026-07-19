# LAST CALL

> *Last call. What are you having?*

A deckbuilder about **reading the person in front of you**, set in a neon-lit late-night
cocktail bar. You are the bartender. Every customer walks in carrying six emotions rated
0–100 — and you can only see some of them. Ask for ID and they show you one exact value,
three rough ranges, and two blanks, plus the one thing they never hide: what they came in
wanting done about it.

Every bottle is printed with what it does to a person. Mix 1–5 of them, land the emotion they
asked about exactly on 0 or 100 for a **Clean Serve**, and push too far and you **bust**.
Recipes still matter — a well-made drink carries what you put in it further — but the
Multiplier now comes from how well you read the room.

Survive by hitting a weekly satisfaction quota. One customer you misjudge doesn't end the run.
A week of them does.

Structurally it owes more to *Papers, Please* than to Balatro: a case to read, a discrepancy
to spot, a quota to hit. **The bartender listens — the drink is the gesture, never the cure.**

**Engine:** Unity 6000.3.10f1 (URP) · **Platform:** PC (Win/Linux/macOS)
**Status:** the emotion loop is playable end to end and measured. The Tip target curve and
the tutorial's *implementation* (its design is written) are the known gaps.

## Quickstart

1. Open the project in Unity **6000.3.10f1** (or a compatible 6000.3.x editor).
2. Open [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) and press **Play**.
3. **Click the customer** to see their ID.
4. Click 1–5 bottles — the projected emotional movement is ghosted on the ID before you
   commit — then **MIX** (serve) or **RESTOCK** (redraw).

Handy while playing:

| Key / control | Effect |
|---|---|
| **Click the customer** | Show / hide their ID |
| **F1** | Toggle the debug score log (off by default) |
| **RECIPES** | Recipe book — every pattern drawn with the real bottle art |
| **NEW RUN** | Restart from the seed in the field next to it (seeds are reproducible) |

If the scene is ever broken, rebuild it from the **LastCall → Create Debug Scene** menu item;
it wires the camera, data assets, art and HUD from scratch.

## Running tests

Unity **Test Runner → EditMode** runs the `LastCall.Tests` assembly — **269 tests** covering
recipe matching, scoring goldens, deck/RNG determinism, round and run flow, shop economy,
the emotion model and its boundaries, information leaks, and data-file parity. All tests must
be green before pushing.

## Balance

`LastCall → Simulate 300 Runs` batch-plays seeded runs through the real `RunController` and
writes [`Docs/sim_report.md`](Docs/sim_report.md). The bot reads only the ID, never the hidden
truth, and buys nothing — so its numbers are a **floor**, not a prediction.

## Architecture

Boundaries are enforced by assembly definitions, not convention:

| Path | Assembly | Purpose |
|---|---|---|
| `Assets/Scripts/Core/` | `LastCall.Core` | **Pure C#, no UnityEngine.** All game rules: scoring, recipes, deck, RNG, round/run loop, shop, patrons, tools, VIPs, bars & stakes. |
| `Assets/Scripts/Game/` | `LastCall.Game` | Unity glue: JSON → Core loading, bootstrap, art registry. |
| `Assets/Scripts/DebugUI/` | `LastCall.DebugUI` | The playable screen: diegetic bar stage + HUD overlay, built entirely in code. |
| `Assets/Scripts/Editor/` | `LastCall.Editor` | Editor tooling (scene builder, art library, sprite generators). |
| `Assets/Tests/EditMode/` | `LastCall.Tests` | EditMode suites. |

Three rules hold the project together (details in [`CLAUDE.md`](CLAUDE.md)):

- **Core stays pure.** Scoring is a pure function `(match, level, …) → ScoreBreakdown`; the UI
  only replays the breakdown steps.
- **Content is data.** Ingredients, recipes, patrons, tools, VIPs, vouchers and customer
  archetypes live in `Assets/Data/*.json` and are parsed with loud validation. New content =
  new data, not new code.
- **Determinism.** All randomness flows through `RunRng` named streams (custom PCG32), so a
  seed string reproduces an identical run on every platform.

A fourth rule earned during the pivot: **the player's view is derived only from
`CustomerRead`, never from the hidden stats.** Two UI features were quietly reading the truth
to draw a preview, which would have made every blind read a sure thing;
`ReadIntegrityTests` now pins that boundary.

## Content

| Data file | Count |
|---|---|
| `decks/classic_bar.json` | 48 ingredient cards (46 unique), each with emotion charges |
| `recipes/recipes.json` | 14 recipes |
| `patrons/patrons.json` | 64 patrons (4 on the information axis) |
| `vips/vips.json` | 23 VIPs (3 that rewrite the ID) |
| `tools/tools.json` | 17 tools |
| `vouchers/vouchers.json` | 7 vouchers |
| `customers/archetypes.json` | 8 customer archetypes |

## Art direction & pipeline

The game runs a locked **"Vice Pixel"** direction (v2): a 40-colour palette, a 640×360
reference resolution with integer scaling only, binary transparency and no runtime rotation.

- **Scene** — a diegetic bartender-POV bar: dimmed nightclub backdrop, a wood counter over a
  glass under-shelf, the 8-bottle rail, the patron leaning on the bar, and a till showing the
  wallet. Bottle draws/mixes/restocks are choreographed slides; ambient life is deliberately
  sparse (a neon flicker and a 1px patron idle), and everything collapses to instant snaps
  under reduced motion.
- **Assets** — generated through the **PixelLab MCP** pipeline, then palette-quantized to the
  v2 40, binary-alpha cleaned and size-verified before install. `LastCallImporter` auto-applies
  point filtering, no compression, no mipmaps and PPU 1 to everything under `Assets/Art/`.
- **Tracking** — [`Docs/GDD/17_ui_inventory.md`](Docs/GDD/17_ui_inventory.md) is the live
  migration tracker: every screen and asset bank is tagged `legacy-cozy-noir`, `placeholder`
  or `migrated-v2`.

| Path | Contents |
|---|---|
| `Assets/Art/Bottles/` | 46 per-ingredient pixel bottles — colour by type ramp, a distinct silhouette per drink |
| `Assets/Art/Backgrounds/` | Club backdrop + bar counter |
| `Assets/Art/Characters/`, `Props/` | Patron sprite, cash register |
| `Assets/Art/Portraits/Archetypes/` | 8 customer ID photos, one per archetype (v2) |
| `Assets/Art/Portraits/`, `VIPs/`, `Tools/`, `Icons/` | v1 painterly banks — still used by the shop, queued for the v2 pass |
| `Tools/AssetPipeline/` | Generation helpers and the audit log (AI-disclosure evidence trail) |

## Design docs

[`Docs/GDD/`](Docs/GDD/) is the source of truth — check the relevant module before changing any
game rule.

- **[`19_emotion_mechanic.md`](Docs/GDD/19_emotion_mechanic.md)** and
  **[`20_regulars_and_week.md`](Docs/GDD/20_regulars_and_week.md)** own the current loop and
  win wherever an older module disagrees.
- Modules 14–18 (art bible, asset pipeline, UI style guide, UI inventory, nightclub scene
  spec) are on **v2**.
- Modules 00–13 predate the pivot. The ones it touched carry a banner saying what is stale;
  `12_tutorial_ftue.md` has been rewritten around the new loop.
- [`Docs/PLAN_emotion_pivot.md`](Docs/PLAN_emotion_pivot.md) records the phased plan and the
  rulings behind it.

## Roadmap

| Milestone | Scope | Status |
|---|---|---|
| M1 — Core prototype | Rail, mix, recipe detection, scoring, customer loop | ✅ Done |
| M2 — Run loop | Nights, targets, tips, Back Room (patrons / tools / books / vouchers / packs) | ✅ Done |
| M3 — Content pass | Patrons, VIPs, Tools, vouchers, packs, Bars, Stakes | ✅ Data in |
| **Emotion pivot** | Read-the-customer loop, ID popup, information economy, weekly quota | ✅ Core / UI / content / balance done |
| M4 — Juice & UX | v2 pixel art, animation, score moment, SFX/music, settings, controller, save, **tutorial (designed, not built)** | 🔨 In progress |
| M5 — Balance & polish | Playtests, Tip-target retune, localization (EN/TR), achievements, Steam demo | 🔨 Sim harness in |

See [`Docs/GDD/11_milestones.md`](Docs/GDD/11_milestones.md) for the full breakdown.
