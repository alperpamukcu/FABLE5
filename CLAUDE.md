# CLAUDE.md — LAST CALL

Unity 6000.3.10f1 (URP) bar-tycoon about reading customers and running the till.
**On a machine that has not run this project before, read `Docs/HANDOFF.md` FIRST** —
it lists what git deliberately does not carry (the PixelLab token above all) and why
the PlayMode look tests will fail until the baselines are re-blessed on that GPU.
**`Docs/GDD_MEVCUT.md` is the as-built rulebook** — the game as it actually runs, extracted
from code 2026-08-07; read it first for any live rule. `Docs/GDD/` carries the design specs:
**modules 23 and 24 own the loop**, 21 owns the pour system (21 §10 draught beer), 22 owns
bottles/brands/market, 14 §5 / 15 §4 / 16 §1 / 18 carry the palette, camera, font and stage
specs the UI is built on, 25 is the (partly superseded) bottle-art brief now aimed at an
external artist, and **26 owns the last customer** — the scripted closing beat, its dialogue
and its data (staged in `Docs/PLAN_last_call.md`, which is the phase log: S0–S3 shipped, so
the beat plays in the scene now; S4's light and S5's rewards are not built). The historical modules (00–13, 17, 19, 20 — card era and the demolished
emotion layer) were DELETED in the 2026-08-07 sweep; recipe truth is `recipes.json` ↔
`RecipeCatalog` under the parity test, ingredient types are the `IngredientType` enum.
**The staging document is `Docs/PLAN_service_depth.md` (v5)** — phase order and conflict
ledger; where it disagrees with GDD_MEVCUT, GDD_MEVCUT wins (its header lists the known
overtaken lines). `Docs/GELISTIRME_RAPORU.md` is the standing audit + prioritized backlog.

## Architecture (enforced by asmdefs)

```
LastCall.Core   (Assets/Scripts/Core)   pure C#, noEngineReferences: true — ALL game rules
LastCall.Game   (Assets/Scripts/Game)   Unity glue: DataLoader (JSON→Core), GameBootstrap
LastCall.UI     (Assets/Scripts/UI)     the whole game UI, built in code — no prefabs
LastCall.Editor (Assets/Scripts/Editor) editor tooling (LastCall menu)
LastCall.Tests  (Assets/Tests/EditMode) EditMode tests, references Core + Game
LastCall.PlayTests (Assets/Tests/PlayMode) PlayMode smoke tests: a virtual mouse plays the
                                        real scene (Core + Game + uGUI, never UI internals)
```

`TycoonServiceFlow` is one partial class split by stage — `.Menu`, `.Shaker`, `.Serve`,
`.Tap` — so a stage can be read whole. Shared state stays in `TycoonServiceFlow.cs`.

**The back bar is the ONLY place a drink is picked up** (2026-08-13). `.Menu`'s wall carries
every bottle except garnish and routes each one to its station already in hand; the three
service benches carry no stock of their own. A shelf, rail or fridge on a bench has now been
built and cut twice — don't build a third.

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
  ONE written exception (GDD 26 §3, 2026-08-13): the story's guest of the house — Core itself
  calls `InspectId()` when it seats them, because they introduce themselves and the ask lives
  in the dialogue. That exception never extends to the crowd, and cannot be cited to.

## Verifying changes

Unity is normally open alongside the IDE; drive it via the UnityMCP HTTP server
(see memory: unity-mcp-setup) — `refresh_unity` (compile) → `read_console` (errors) →
`run_tests` with `assembly_names: "LastCall.Tests"`. All tests must pass before a push.
The scene can be rebuilt with the **LastCall → Create Debug Scene** menu item.

**The UI has a floor now** (2026-08-12): `run_tests` with `assembly_names: "LastCall.PlayTests",
mode: "PlayMode", init_timeout: 180000` plays the real scene with a virtual mouse — the bar
opens, a stool is clicked and gives up its licence, a bottle is clicked and lands on the
bench, and the bench pours. It is a floor and not coverage: 17.5k lines of UI still ride on
four tests, and the rest is still caught by entering play mode and LOOKING. Measure the thing
you changed in play (`execute_code` reads live rects and fields) rather than trusting that it
compiles. Run both suites before a push; PlayMode needs the editor OUT of play mode first.

**A killed PlayMode run poisons the editor** (2026-08-13): the suite drives a VIRTUAL mouse and
`InputTestFixture` only takes it away in teardown, so a cancelled or wedged run leaves that fake
device as the editor's only pointer — the game then ignores the real mouse and appears to play
itself (the bench opening on its own is the usual tell). **LastCall → Clear Ghost Input** drops
the devices so the real one is re-discovered; if the pointer is still dead, restart the editor.
A wedged test job also blocks every later `run_tests` with `tests_running` —
`TestJobManager.ClearStuckJob()` via `execute_code` clears it.

Three things that make PlayMode tests lie, all paid for once (see the suite's own comments):
a test frame is ~1ms, so every wait for an animation must be `WaitForSecondsRealtime`, never
a frame count; the field crops rather than scales, so the tests pin the Game view to
1280×720; and a drinker walks in and then *decides* before the stool answers a click.

**The screens are compared pixel for pixel** (`LookTests`, same assembly): the back bar, the
bench (inside its panel — the room around it is alive) and the market's basket foot are held
against blessed pictures in `Assets/Tests/PlayMode/Baselines~/`, which Unity never imports and
git does keep. A screen is only compared once two captures in a row come back identical. When
a screen is *meant* to change: **LastCall → Re-bless UI Baselines**, then run the suite twice —
the first run draws the new pictures and fails on purpose, so nobody blesses a screen without
looking at it. On a failure the current picture and the diff land in `Temp/UiLooks`
(**LastCall → Show Last UI Look Failures**). The baselines are THIS machine's; a different GPU
may differ by a pixel, which is why they stay out of CI — and why a second machine
re-blesses them once before trusting a red look test (`Docs/HANDOFF.md` §4).

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
  and a relationship — the emotion machinery (stats, charges, reads, `DemandLevel`, mood
  tips, "filled to the top") was demolished 2026-08-02 and its last remnants removed in the
  2026-08-07 sweep ("sadece verilen kokteyle verdiği tepkiler kaldı").
- Bottles are **single flat sprites** (`v3_{id}_flat`) since the 2026-08-05 flat era; the
  runtime liquid-in-bottle layer was removed whole in the 2026-08-07 sweep. What is left in
  a bottle lives on the hover card. A layered bottle set is an external artist's brief
  (GDD 25's header) — rebuild the layer against that art when it lands, don't resurrect it.
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
