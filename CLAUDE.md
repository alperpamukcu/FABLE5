# CLAUDE.md — LAST CALL

Unity 6000.3.10f1 (URP) bar-tycoon about reading customers and running the till.
`Docs/GDD/` is the design source of truth — check the relevant GDD module before implementing
or changing any game rule. **Modules 23 and 24 own the current loop** (tycoon pivot v4,
2026-07-22, staged in `Docs/PLAN_tycoon_pivot.md`); 19–22 survive as subsystem specs where
23/24 reference them; modules 00–13 predate the pivots and carry banners where stale.
During the transition the old quota loop remains runnable until PLAN P7 demolition.

## Architecture (enforced by asmdefs)

```
LastCall.Core    (Assets/Scripts/Core)    pure C#, noEngineReferences: true — ALL game rules
LastCall.Game    (Assets/Scripts/Game)    Unity glue: DataLoader (JSON→Core), GameBootstrap
LastCall.DebugUI (Assets/Scripts/DebugUI) M1 debug HUD, built entirely in code
LastCall.Editor  (Assets/Scripts/Editor)  editor tooling (LastCall menu)
LastCall.Tests   (Assets/Tests/EditMode)  EditMode tests, references Core + Game
```

Hard rules:

- **Core stays pure.** No UnityEngine types in `LastCall.Core` — the asmdef enforces it.
  The scoring engine is a pure function `(match, level, …) → ScoreBreakdown`; the UI only
  replays breakdown steps.
- **Content is data.** Ingredients/recipes/patrons/etc. live in `Assets/Data/*.json` and are
  parsed by `DataLoader` with loud validation. New content = new data, not new code.
  `RecipeCatalog` (code) and `recipes.json` are kept in sync by a parity test — change both.
- **Determinism.** All randomness flows through `RunRng` named streams ("deck", "shop",
  "read", "customer", "drift", …). Never use `System.Random`/`UnityEngine.Random` in game
  logic; string seeds must reproduce identical runs across platforms (custom PCG32).
- **Hidden information stays hidden.** Anything the player sees is derived from
  `CustomerRead` — never from `RegularState.Stats`. Reaching past the read to draw a preview
  or a label makes blind reads a sure thing and quietly kills the mechanic. This has already
  happened twice; `ReadIntegrityTests` exists to catch the third time.

## Verifying changes

Unity is normally open alongside the IDE; drive it via the UnityMCP HTTP server
(see memory: unity-mcp-setup) — `refresh_unity` (compile) → `read_console` (errors) →
`run_tests` with `assembly_names: "LastCall.Tests"`. All tests must pass before a push.
The debug scene can be rebuilt with the **LastCall → Create Debug Scene** menu item.

## Workflow

- Work directly on `main` and push to GitHub (`alperpamukcu/FABLE5`); the branch/PR
  flow was retired on 2026-07-09 to keep iteration fast.
- Commit messages: imperative summary line, body explains what/why, in English.
- Scene edits go through code (editor tooling) where possible — scenes are hard to review.

## Gotchas

- Project uses the **new Input System only** (`activeInputHandler: 1`) — runtime-created UI
  needs `InputSystemUIInputModule`, not `StandaloneInputModule`.
- Legacy UGUI `Text` needs `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- JSON files are parsed with `JsonUtility`: DTOs use public fields, no nullable types —
  use `0`/`false` defaults (see `DataLoader`).
- A mix that matches no recipe is a **house pour** (GDD 21 §9, 2026-07-20): it scores its
  volume-weighted Flavor at ×1 (small but never zero), and its emotion charges land at ×0.5.
  Recipes pay Flavor × Mult — the order-of-magnitude gap is the reward ladder.
- The glass **cannot overflow** (GDD 21 §3): pours stop at the brim, `GlassContents.Add`
  returns what was accepted, and `Spills` counts binned drinks, not overflows.
- The emotion layer is **opt-in**: a `RunController` built without `archetypes` has no
  regulars and no quota gate. That is what keeps pre-pivot bench setups and older tests valid.
- `Deck.Draw` takes from the **end** of the draw pile. A test that appends a card to the front
  of the list will never see it on the rail.
- `RestocksUsed` is derived, so anything that spends a Restock silently inflates it. Chat
  tracks its own counter to stay out of patron conditions.

## Balance

`LastCall → Simulate 300 Runs` batch-plays seeded runs through the real `RunController` and
writes `Docs/sim_report.md`. Prefer measuring over guessing — it has already caught two design
bugs and two reporting bugs. The bot reads only the ID and never shops, so its win rate is a
**floor**, not a prediction; trust the shape comparisons, not the absolute number.
