# Development Plan — Emotion-Stat Pivot ("Read the Customer")

Working document for the pivot specified in `Docs/GDD/19_emotion_mechanic.md`.
Structural fork **B** is locked: **weekly satisfaction quota is the only loss condition**;
a single unhappy customer never ends the run.

Status legend: ☐ todo · ◐ in progress · ☑ done

| Phase | Status |
|---|---|
| 1 — Core model | ☑ `fef55fe` |
| 2 — Playable slice | ☑ `223dca5` (card charges + archetypes pulled forward from phase 4; the slice is not playable without data) |
| 3 — Information economy | ☑ Chat, 4 reading patrons, Eavesdrop, 3 read-rule VIPs. Shop integration came free — they are ordinary patron/tool/VIP entries, so the existing offer roll already sells them. |
| 4 — Content & balance | ☐ recipe `chargeMultiplier` authoring, VIP redesign, quota tuning with a sim harness |
| 5 — Documentation | ☐ |

---

## 0. Decisions locked before writing code

| # | Question | Ruling |
|---|---|---|
| D1 | Do recipes survive? | **Yes.** Demoted to the craft layer; they supply `BaseFlavor`/`BaseMult` and a `ChargeMultiplier`. Nothing is deleted. |
| D2 | Per-customer loss? | **No** (fork B). `RoundPhase.Lost` becomes `RoundPhase.Closed` — the visit ends, the customer leaves at whatever satisfaction was earned. |
| D3 | What ends a run? | Missing the **week's satisfaction quota**. Checked once, at week end. |
| D4 | Do emotion stats persist? | **Yes**, per named regular, across the whole run, with between-visit drift. |
| D5 | No-recipe mix | Still scores 0 points, but **charges still pour at ×0.5**. Supersedes the CLAUDE.md open question and `02 §"no high card"`. |
| D6 | Charge rounding | `applied = round(rawSum × chargeMultiplier)` — integer, so exact landings on 0/100 are reachable. |
| D7 | Name collision | The recipe *Perfect Serve* keeps its name; the emotional burst is **Clean Serve**. |
| D8 | Chat cost | Chat consumes a Restock **charge** but does **not** increment `RestocksUsed` — otherwise it silently feeds Off-Duty Cop and every `restocks_used` patron condition. |
| D9 | Determinism | Three new `RunRng` streams: `"read"` (tier assignment), `"customer"` (archetype + stat roll), `"drift"` (between-visit movement). Never reuse `"deck"`/`"shop"`. |
| D10 | Migration safety | The emotion layer is **opt-in**: a `RunController` built without `archetypes` has no `RegularsRegistry`, no readings, and — critically — **no quota gate**. Without this, every pre-pivot run would earn 0 satisfaction and fail week 1. Phase 4 supplies the data that turns it on for real play. |

---

## Phase 1 — Core model (pure C#, zero UnityEngine)

Everything lands under `Assets/Scripts/Core/Emotion/` unless noted. `LastCall.Core` has
`noEngineReferences: true`; nothing here may reference UnityEngine.

### 1.1 New files

| File | Contents |
|---|---|
| `Core/Emotion/Emotion.cs` | `enum Emotion { Anger, Sadness, Fatigue, Excitement, Heartbreak, Anxiety }`. Declaration order **is** the tie-break order for dominant-emotion (19 §2). |
| `Core/Emotion/EmotionStats.cs` | Immutable-ish 6-slot int store clamped 0–100. `this[Emotion]`, `Dominant`, `With(Emotion, int)`, `Apply(EmotionDelta)`, `Equals`. |
| `Core/Emotion/EmotionCharge.cs` | `readonly struct { Emotion Emotion; int Amount; }` — signed; negative = extinguish. A card carries `IReadOnlyList<EmotionCharge>`. |
| `Core/Emotion/VisibilityTier.cs` | `enum { Exact, Range, Unknown }` + `readonly struct StatReading { VisibilityTier Tier; int Low; int High; }`. |
| `Core/Emotion/CustomerRead.cs` | Per-visit view: `StatReading this[Emotion]`, `Emotion Intent`, `IntentDirection Direction`. Built by `CustomerReadFactory` from a `SeededRng`. |
| `Core/Emotion/CustomerReadFactory.cs` | 1 Exact / 3 Range / 2 Unknown assignment; range half-width by night (±8 / ±12 / ±16 per 19 §3); narrows one step per `Relationship` rank. |
| `Core/Emotion/EmotionResolver.cs` | Pure: `(selection, RecipeMatch, level) → EmotionDelta`. Sums card charges, applies `chargeMultiplier` (or ×0.5 when `match == null`), rounds once at the end. |
| `Core/Emotion/ResonanceResult.cs` | Output of judging a served drink: `double ResonanceMult`, `bool Bust`, `BustKind`, `bool CleanServe`, `bool BlindHit`, `int Satisfaction`. |
| `Core/Emotion/ResonanceJudge.cs` | Pure: `(before, after, intent, direction, read) → ResonanceResult`. Owns the bust table and the Clean Serve / lucky-read rules (19 §6). |
| `Core/Regulars/Relationship.cs` | `enum { Stranger, Familiar, Regular, Confidant }`. |
| `Core/Regulars/RegularState.cs` | `string Id, Name, ArchetypeId; EmotionStats Stats; EmotionStats Baseline; int Visits, SatisfiedCount; Relationship Relationship;` + `RecordVisit(int satisfaction)`. |
| `Core/Regulars/RegularsRegistry.cs` | Owns every `RegularState` for the run. `Get(id)`, `RollNext(SeededRng, night)`, `DriftAll(SeededRng)`. |
| `Core/Regulars/ArchetypeDefinition.cs` | Data-loaded: id, name, per-emotion `(min,max)` baseline range, weight, intent bias. |
| `Core/Run/WeekQuota.cs` | `int Week, Required, Earned; bool Met;` + the quota curve provider (injectable, like `TargetProvider`). |

### 1.2 Edits to existing files

| File | Change |
|---|---|
| `Core/Cards/IngredientCard.cs` | Add `IReadOnlyList<EmotionCharge> Charges` (ctor param, defaults empty). Carry through `Clone()`. `ConvertType` must **not** rewrite charges — charges are per-card identity, not per-type. |
| `Core/Recipes/RecipeDefinition.cs` | Add `double ChargeMultiplier` (default 1.0), computed `1 + 0.2 × (BaseMult − 1)` capped at 3.0 when not given explicitly. |
| `Core/Recipes/RecipeCatalog.cs` | Mirror the new field so the parity test still passes. |
| `Core/Round/CustomerOrder.cs` | Becomes a **view over a `RegularState`**: keeps `Name`, gains `RegularState Regular`, `CustomerRead Read`, `Emotion Intent`, `IntentDirection Direction`. `TargetScore` stays (per-visit satisfaction bar), `RuleText` stays (VIPs). |
| `Core/Round/RoundController.cs` | ① `Mix()` calls `EmotionResolver` then `ResonanceJudge`, feeds `resonanceMult` into scoring, applies the delta to `Customer.Regular.Stats`. ② `RoundPhase.Lost` → `RoundPhase.Closed`. ③ new `Chat()` action (D8). ④ expose `LastResonance` for the UI. ⑤ `PreviewCharges(selection)` — non-consuming charge preview for the pre-commit UI. |
| `Core/Scoring/ScoringEngine.cs` | New overload taking `ResonanceResult`. Adds steps: `resonance` (+Mult), `lucky_read` (+3), `clean_serve` (×2 / ×3), `bust` (Mult penalty). Order: recipe base → cards → patron hand effects → **resonance block last**. Resonance goes last so the Clean Serve burst is the closing flourish of the score card and is not compounded by patron `MultMult`. Mult floors at 1 after a bust penalty. |
| `Core/Scoring/ScoreBreakdown.cs` | Add `ResonanceResult Resonance` (nullable) so the UI can replay the emotional half. |
| `Core/Run/RunController.cs` | ① Delete the `Phase = RunPhase.RunLost` path in `Mix()`. ② Own a `RegularsRegistry`; `StartCustomer()` pulls a regular via `_rng.GetStream("customer")` and builds the read via `"read"`. ③ Track `WeekQuota`; accumulate satisfaction in `OnCustomerSatisfied()` (rename → `OnCustomerLeaves`). ④ At week end: `DriftAll(_rng.GetStream("drift"))`, then quota gate → `RunLost` or next week. |
| `Core/Run/RunConfig.cs` | Add `Func<int,int> QuotaProvider` (week → required satisfaction), defaulting to a new `QuotaTable`. Keep `TargetProvider` — per-visit score bars still exist, they just no longer kill you. |

### 1.3 Test matrix (`Assets/Tests/EditMode/`)

New suites: `EmotionStatsTests.cs`, `EmotionResolverTests.cs`, `ResonanceJudgeTests.cs`,
`CustomerReadTests.cs`, `RegularsRegistryTests.cs`, `WeekQuotaTests.cs`.

Required cases:

- exact landing on **0** and on **100** counts as a hit, not a bust
- 1-unit overshoot past the target **busts**
- wrong-direction movement of exactly **10** drifts; **11** busts (boundary)
- rounding boundary: raw −32 lands, raw −33 busts, at the same multiplier
- blind (Unknown-tier) Clean Serve pays **×3**; known-tier pays ×2
- lucky read pays **+3** only on Unknown tiers
- no-recipe mix: `FinalScore == 0` **and** charges applied at ×0.5
- busts do **not** persist to `RegularState.Stats`
- same seed → identical tier assignment, archetype roll and drift (determinism)
- known tiers decay one step per visit; `Relationship` promotion narrows ranges
- quota gate: earned == required passes; required − 1 fails; a single unhappy customer never sets `RunLost`

Plus: **all 170 existing tests stay green.** Fork B intentionally changes
`RunControllerTests` expectations around instant loss — those get rewritten, not deleted.

**Gate:** `run_tests` on `LastCall.Tests` fully green → commit `feat(core): emotion-stat model`.

---

## Phase 2 — Playable slice (DebugUI)

- **Customer ID card** (`DiegeticStage`): 6 stat rows, three visual treatments —
  Exact = number, Range = a bar with a lit band, Unknown = `??` with a scratched plate.
- **Intent ticket**: one icon + `EXTINGUISH` / `FUEL`, always readable.
- **Pre-commit charge preview**: selecting bottles ghosts the projected movement on the ID
  card *before* MIX. Non-binding, uses `PreviewCharges`.
- **Reaction beat**: icon-first, ≤ 3 words, per the tone guardrail — the customer reacts to
  being *understood*, never to being drunk.
- Score card extended with the resonance line: `Flavor × (recipe + resonance) [× burst]`.

**Gate:** a full week playable end to end in the editor → commit `feat(ui): read-the-customer slice`.

---

## Phase 3 — Information economy

- `Chat` action (2 per customer, D8) — reveals or narrows one reading.
- 4 new patrons: Gossip, Confidant, Empath, Regular's Memory (`patrons.json`, no new code
  if the existing effect kinds cover them; otherwise one new `EffectOp`).
- `Eavesdrop` tool (`tools.json`).
- 3 new VIP rule kinds: Poker Face (all tiers Unknown), Open Book (all Exact),
  The Liar (one reading is deliberately wrong).
- Shop integration: information becomes the third purchase axis.

**Gate:** commit `feat(core+data): information economy`.

---

## Phase 4 — Content & balance

- Charges authored for all 48 cards in `decks/classic_bar.json` (bands per 19 §4).
- New `Assets/Data/customers/archetypes.json` + `DataLoader` support + a parity test.
- `ChargeMultiplier` authored for all 14 recipes.
- VIP redesign pass against the new axis.
- Week quota curve tuned with a sim harness (`Assets/Scripts/Editor/`, LastCall menu).

**Gate:** commit `feat(data): emotion content pass`.

---

## Phase 5 — Documentation

| Module | Action |
|---|---|
| `19_emotion_mechanic.md` | **COMMIT** (already written, held for sign-off). |
| `20_regulars_and_week.md` | **NEW** — regulars cast, archetype table, week quota curve, relationship track. |
| `03_run_structure_balance.md` | **REPLACE** §5.1 — the 8-night target table dies with fork B. |
| `02_recipes_scoring.md` | **UPDATE** — recipes demoted; no-recipe now pours charges at ×0.5 (D5). |
| `00, 01, 04, 05, 08, 12, 16, 17, 18` | **UPDATE** — new loop, card charges, VIP axis, shop axis, ID-card UI. |
| `06_patrons.md` | **KEEP + ADD** — all 60 patrons survive unchanged; append the 4 new ones. |
| `README.md`, `CLAUDE.md` | **UPDATE** — pitch, loop, and the retired "no high card" open question. |

---

## Risk register

| Risk | Mitigation |
|---|---|
| Fork B guts `RunControllerTests` | Rewrite those cases in Phase 1 as part of the same commit — never leave the suite red between commits. |
| Charge authoring for 48 cards is a balance minefield | Band-driven (19 §4), not hand-tuned; the sim harness in Phase 4 validates before playtest. |
| Tone drift toward "alcohol fixes feelings" | Every reaction string reviewed against the 19 header guardrail; the verb is always *listening*, never *curing*. |
| Determinism regressions | New streams only; a seeded-reproducibility test per new random source. |
