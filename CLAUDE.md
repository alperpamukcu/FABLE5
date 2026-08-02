# CLAUDE.md — LAST CALL

Unity 6000.3.10f1 (URP) bar-tycoon about reading customers and running the till.
`Docs/GDD/` is the design source of truth — check the relevant GDD module before implementing
or changing any game rule. **Modules 23 and 24 own the current loop** (tycoon pivot v4,
shipped). **The live staging document is `Docs/PLAN_service_depth.md` (v5)** — it carries the
conflict ledger for the 2026-07-31 revision notes and the phase order; consult it before
starting new feature work. 19–22 survive as subsystem specs where 23/24 reference them, and
21 §10 owns draught beer. Modules 00–13 predate the pivots: treat them as historical **except
the sections live code still cites** — 01 §3 (ingredient types), 02 §4 (the recipe table,
parity-tested), 12 (reduced motion), 13 (determinism & seeding); 14 §5 / 15 §4 / 16 §1 / 18
likewise carry the palette, camera, font and stage specs the UI is built on.

## Architecture (enforced by asmdefs)

```
LastCall.Core   (Assets/Scripts/Core)   pure C#, noEngineReferences: true — ALL game rules
LastCall.Game   (Assets/Scripts/Game)   Unity glue: DataLoader (JSON→Core), GameBootstrap
LastCall.UI     (Assets/Scripts/UI)     the whole game UI, built in code — no prefabs
LastCall.Editor (Assets/Scripts/Editor) editor tooling (LastCall menu)
LastCall.Tests  (Assets/Tests/EditMode) EditMode tests, references Core + Game
```

`TycoonServiceFlow` is one partial class split by stage — `.Menu`, `.Shaker`, `.Serve`,
`.Tap` — so a stage can be read whole. Shared state stays in `TycoonServiceFlow.cs`.

Hard rules:

- **Core stays pure.** No UnityEngine types in `LastCall.Core` — the asmdef enforces it.
  Rules are pure functions the UI only renders: `TapPour`, `ServiceJudge`,
  `RatioRecipeMatcher` all decide without touching a `MonoBehaviour`.
- **The rules layer never trusts the UI.** If a thing must not happen, Core refuses it —
  routing around it in the menu is not enough. The sim bot and the tests use the same verbs
  the player does, so any hole shows up as a wrong balance number, not as a bug report.
- **Content is data.** Ingredients and recipes live in `Assets/Data/*.json` and are parsed by
  `DataLoader` with loud validation. New content = new data, not new code. `RecipeCatalog`
  (code) and `recipes.json` are kept in sync by a parity test — change both.
- **Determinism.** All randomness flows through `RunRng` named streams ("arrivals", "orders",
  "patience", "customer", "read", "decide"). Never use `System.Random`/`UnityEngine.Random`
  in game logic; string seeds must reproduce identical runs across platforms (custom PCG32).
- **Hidden information stays hidden.** The order lives behind the ID card: `CustomerVisit.Order`
  throws until `InspectId()`, and only Core's `OrderTruth` sees past it. Drawing the drink, its
  name or its price before the card is read makes the card decorative and quietly kills the
  mechanic — it has already happened twice. (The emotion layer this rule was written for was
  demolished 2026-08-02; what a customer gives back is their reaction to the cocktail.)

## Verifying changes

Unity is normally open alongside the IDE; drive it via the UnityMCP HTTP server
(see memory: unity-mcp-setup) — `refresh_unity` (compile) → `read_console` (errors) →
`run_tests` with `assembly_names: "LastCall.Tests"`. All tests must pass before a push.
The scene can be rebuilt with the **LastCall → Create Debug Scene** menu item.

The UI has no automated coverage: it is ~6k lines driven entirely by pointer input, and every
UI regression so far was caught by entering play mode and looking. Measure the thing you
changed in play (`execute_code` reads live rects and fields) rather than trusting that it
compiles.

## Workflow

- Work directly on `main` and push to GitHub (`alperpamukcu/FABLE5`); the branch/PR
  flow was retired on 2026-07-09 to keep iteration fast.
- Commit messages: imperative summary line, body explains what/why, in English.
- Scene edits go through code (editor tooling) where possible — scenes are hard to review.

## Gotchas

- Project uses the **new Input System only** (`activeInputHandler: 1`). Read the mouse through
  `Mouse.current`; the legacy `Input` class throws here, which is how the first tap handle
  shipped completely dead.
- Runtime-created UI needs `InputSystemUIInputModule`, not `StandaloneInputModule`.
- Legacy UGUI `Text` needs `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. The
  pixel faces only rasterise cleanly at whole multiples of their 8px design size — pin font
  sizes to 8/16/24 and leave `resizeTextForBestFit` off.
- JSON files are parsed with `JsonUtility`: DTOs use public fields, no nullable types —
  use `0`/`false` defaults (see `DataLoader`).
- The glass **cannot overflow** (GDD 21 §3): pours stop at the brim, `GlassContents.Add`
  returns what was accepted, and `Spills` counts binned drinks, not overflows.
- **Beer is not a cocktail** (GDD 21 §10). It comes from a keg, never enters the shaker, and
  is poured by the angle of the glass; its craft is the head, and `Preparations.Draught` on
  the delivered glass is what tells `ServiceJudge` to grade it.
- Regulars are **opt-in**: a `TycoonRun` built without `archetypes` has no named customers,
  so bench setups and older tests stay valid. They carry a name, an age, a hometown, visits
  and a relationship — the emotion stats, charges and reads were demolished on 2026-08-02
  ("sadece verilen kokteyle verdiği tepkiler kaldı"), so GDD 19/20's mood machinery is
  historical: nothing reads it and nothing writes it.
- `MetaballFluid` fills a vessel from a particle-count estimate that is not exact for every
  silhouette. If a vessel draws short, measure it (`SurfaceY`) and correct that vessel with
  `SetDensity` — do not scale the fill fraction, which just clamps.

## Balance

`LastCall → Simulate Tycoon 200 Runs` batch-plays seeded runs through the real `TycoonRun`
and writes `Docs/tycoon_sim_report.md`. Prefer measuring over guessing — it has already caught
two design bugs and two reporting bugs. The bot reads only the ID and never shops, so its
survival rate is a **floor**, not a prediction; trust the shape comparisons, not the absolute
number.

The bot pulls pints the way the mechanic asks (leaned over, then straightened), so draught is
measured rather than faked. It reports the draught share of serves, how many pints landed in
the good head band, and the average head poured.
