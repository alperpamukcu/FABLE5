# LAST CALL

> *Last call. What are you having?*

A roguelike deckbuilder / score-attack game (Balatro-like) set in a dim, smoky late-night
cocktail bar. You are the bartender: draw Ingredient cards, mix them into Recipes, satisfy
escalating customer orders across an 8-Night "Opening Week", and stack wild Patron synergies
to turn a humble rail drink into a 50,000-point masterpiece.

**Engine:** Unity 6000.3.10f1 (URP) · **Platform:** PC (Win/Linux/macOS) · **Status:** M1 complete — core loop playable

## Quickstart

1. Open the project in Unity **6000.3.10f1** (or a compatible 6000.3.x editor).
2. Open [`Assets/Scenes/Main.unity`](Assets/Scenes/Main.unity) and press **Play**.
3. Click 1–5 cards on the rail, watch the live recipe preview, then **MIX** (score) or
   **RESTOCK** (redraw). Reach the customer's Satisfaction target before running out of Mixes.

The current UI is the M1 debug HUD; the real presentation layer lands in M4.
If the scene is ever broken, rebuild it via the **LastCall → Create Debug Scene** menu item.

## Running tests

Unity **Test Runner → EditMode** runs the `LastCall.Tests` assembly (recipe matching,
scoring goldens, deck/RNG determinism, round flow, data-file parity). All tests must pass
before a PR is opened.

## Project layout

| Path | Purpose |
|---|---|
| `Assets/Scripts/Core/` | `LastCall.Core` — pure C# game rules (no UnityEngine). Scoring, recipes, deck, RNG, round loop. |
| `Assets/Scripts/Game/` | `LastCall.Game` — Unity glue: JSON loading, bootstrap. |
| `Assets/Scripts/DebugUI/` | `LastCall.DebugUI` — M1 debug HUD (code-built UGUI). |
| `Assets/Scripts/Editor/` | `LastCall.Editor` — editor tooling (scene builder). |
| `Assets/Data/` | All game content as JSON: decks, recipes (patrons, tools… as milestones land). |
| `Assets/Tests/EditMode/` | `LastCall.Tests` — EditMode test suites. |
| `Docs/GDD/` | Game Design Document v1.0 — the source of truth for all design decisions. |

## Roadmap

| Milestone | Scope | Status |
|---|---|---|
| M1 — Core prototype | Rail, mix, recipe detection, scoring, customer loop, debug UI | ✅ Done |
| M2 — Run loop | Nights, targets, tips, Back Room (10 Patrons / 5 Tools / Books) | 🔨 In progress |
| M3 — Content pass | 60 Patrons, 20 VIPs, 15 Tools, vouchers, packs, 3 Bars, Stakes 1–4 | — |
| M4 — Juice & UX | Art, animations, SFX/music, settings, controller, save/continue | — |
| M5 — Balance & polish | Sim harness, playtests, localization (EN/TR), achievements, Steam demo | — |

See [`Docs/GDD/11_milestones.md`](Docs/GDD/11_milestones.md) for details.
