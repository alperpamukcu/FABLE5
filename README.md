# LAST CALL

> *Last call. What are you having?*

A roguelike deckbuilder / score-attack game (Balatro-like) set in a neon-lit late-night
cocktail bar. You are the bartender: draw Ingredient bottles, mix them into Recipes, satisfy
escalating customer orders across an 8-Night "Opening Week", and stack wild Patron synergies
to turn a humble rail drink into a 50,000-point masterpiece.

**Engine:** Unity 6000.3.10f1 (URP) · **Platform:** PC (Win/Linux/macOS)
**Status:** core + run loop playable end to end; the v2 "Vice Pixel" art pass is in flight.

## Quickstart

1. Open the project in Unity **6000.3.10f1** (or a compatible 6000.3.x editor).
2. Open [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) and press **Play**.
3. Click 1–5 bottles on the bar, watch the live recipe preview, then **MIX** (score) or
   **RESTOCK** (redraw). Reach the customer's target before you run out of Mixes.

Handy while playing:

| Key / control | Effect |
|---|---|
| **F1** | Toggle the debug score log (off by default) |
| **RECIPES** | Recipe book — every pattern drawn with the real bottle art |
| **NEW RUN** | Restart from the seed in the field next to it (seeds are reproducible) |

If the scene is ever broken, rebuild it from the **LastCall → Create Debug Scene** menu item;
it wires the camera, data assets, art and HUD from scratch.

## Running tests

Unity **Test Runner → EditMode** runs the `LastCall.Tests` assembly — **170 tests** covering
recipe matching, scoring goldens, deck/RNG determinism, round and run flow, shop economy, and
data-file parity. All tests must be green before pushing.

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
- **Content is data.** Ingredients, recipes, patrons, tools, VIPs and vouchers live in
  `Assets/Data/*.json` and are parsed with loud validation. New content = new data, not new code.
- **Determinism.** All randomness flows through `RunRng` named streams (custom PCG32), so a
  seed string reproduces an identical run on every platform.

## Content

| Data file | Count |
|---|---|
| `decks/classic_bar.json` | 48 ingredient cards (46 unique) |
| `recipes/recipes.json` | 14 recipes |
| `patrons/patrons.json` | 60 patrons |
| `vips/vips.json` | 20 VIPs |
| `tools/tools.json` | 16 tools |
| `vouchers/vouchers.json` | 7 vouchers |

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
| `Assets/Art/Portraits/`, `VIPs/`, `Tools/`, `Icons/` | v1 painterly banks — still used by the shop, queued for the v2 pass |
| `Tools/AssetPipeline/` | Generation helpers and the audit log (AI-disclosure evidence trail) |

## Design docs

[`Docs/GDD/`](Docs/GDD/) is the source of truth — check the relevant module before changing any
game rule. Modules 14–18 (art bible, asset pipeline, UI style guide, UI inventory, nightclub
scene spec) are on **v2**; the rest describe the design as shipped.

## Roadmap

| Milestone | Scope | Status |
|---|---|---|
| M1 — Core prototype | Rail, mix, recipe detection, scoring, customer loop | ✅ Done |
| M2 — Run loop | Nights, targets, tips, Back Room (patrons / tools / books / vouchers / packs) | ✅ Done |
| M3 — Content pass | 60 Patrons, 20 VIPs, 16 Tools, vouchers, packs, Bars, Stakes | 🔨 Data in, balance pending |
| M4 — Juice & UX | v2 pixel art, animation, score moment, SFX/music, settings, controller, save | 🔨 In progress |
| M5 — Balance & polish | Sim harness, playtests, localization (EN/TR), achievements, Steam demo | — |

See [`Docs/GDD/11_milestones.md`](Docs/GDD/11_milestones.md) for the full breakdown.
