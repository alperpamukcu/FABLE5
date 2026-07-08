# CLAUDE.md — LAST CALL

Unity 6000.3.10f1 (URP) roguelike deckbuilder. `Docs/GDD/` is the design source of truth —
check the relevant GDD module before implementing or changing any game rule.

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
- **Determinism.** All randomness flows through `RunRng` named streams ("deck", "shop", …).
  Never use `System.Random`/`UnityEngine.Random` in game logic; string seeds must reproduce
  identical runs across platforms (custom PCG32).

## Verifying changes

Unity is normally open alongside the IDE; drive it via the UnityMCP HTTP server
(see memory: unity-mcp-setup) — `refresh_unity` (compile) → `read_console` (errors) →
`run_tests` with `assembly_names: "LastCall.Tests"`. All 52+ tests must pass before a PR.
The debug scene can be rebuilt with the **LastCall → Create Debug Scene** menu item.

## Workflow

- Branch from `main`: `feature/<topic>`, `chore/<topic>`, `fix/<topic>`. PRs into `main`
  on GitHub (`alperpamukcu/FABLE5`); no direct pushes to `main` after M1.
- Commit messages: imperative summary line, body explains what/why, in English.
- Scene edits go through code (editor tooling) where possible — scenes are hard to review.

## Gotchas

- Project uses the **new Input System only** (`activeInputHandler: 1`) — runtime-created UI
  needs `InputSystemUIInputModule`, not `StandaloneInputModule`.
- Legacy UGUI `Text` needs `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
- JSON files are parsed with `JsonUtility`: DTOs use public fields, no nullable types —
  use `0`/`false` defaults (see `DataLoader`).
- A mix that matches no recipe scores 0 by design (no "high card" fallback) — open design
  question, revisit after playtests.
