# PLAN — Service Depth & Progression (v5)

Source of truth for the v5 expansion, staged from the developer's revision notes of
**2026-07-31**. The v4 tycoon pivot (`PLAN_tycoon_pivot.md`) shipped P0–P7 whole; its open
P8/P9 items are **absorbed here** (mapping at the bottom) and that plan is now a record.

Work happens directly on `main`; every phase lands green (all tests) and playable; every
phase that moves money is **sim-gated** (`LastCall → Simulate Tycoon 200 Runs`, shape
comparison against the previous report). Design lands with the phase: each phase updates its
GDD module(s) in the same commit that changes the rule.

Status legend: ☐ todo · ◐ in progress · ☑ done

---

## 0. Conflict ledger — the notes vs the live game

Every place the revision notes contradict a shipped rule, with the ruling. Rulings follow
the notes unless marked as a decision (D#). **Nothing below is accidental**: where a test
pins the old rule, the phase that changes the rule rewrites the test in the same commit.

| # | Live rule (where) | The notes say | Ruling |
|---|---|---|---|
| C1 | Wrong drink **pays nothing** (`ServiceJudge`, test `TheWrongDrink_PaysNothing_AndSoursTheRoom`) | Wrong drink earns the **delivered** drink's base price + poor rating | Adopt notes — P11 |
| C2 | Orders are always answerable — rolled only from pourable recipes (`DrinkOrder`) | Customers **can** request out-of-stock items; failure = disappointed + poor rating | Adopt notes — P11. Distinction kept: **not-yet-unlocked** drinks are never rolled (notes' own ordering rule); **unlocked-but-dry** ones can be |
| C3 | Seat bubble shows the order **and its price** (`TycoonHud`) | Order hidden until the ID card is inspected; **price never shown** | Adopt notes — P15 |
| C4 | Fixed customer count per day (`TycoonConfig.CustomersOnDay`, cap 14) | No daily limit; throughput is set by service speed | Adopt notes — P12 |
| C5 | Day counter in the top bar | A clock; the shift is a night with a timeline | Adopt notes — P12. The run keeps its internal day identity (rent, ledger, streaks) |
| C6 | TONIGHT satisfaction bar drives tomorrow's crowd (`DayLedger`) | Star rating system drives traffic/wealth/tips/unlocks | **Merge (D3)** — the rating becomes the visible reputation; wealth tiers key off average stars; the bar retires from the top bar |
| C7 | BIN GLASS button | A physical trash bin, drag to discard | Adopt notes — P13 |
| C8 | Everything mixable goes through the shaker | Carbonated drinks **never** enter the shaker; they are added at the serving glass | Adopt notes — P10 Core refusal (the `beer is not a cocktail` precedent, extended) |
| C9 | One serving glass; the pint is the only exception | Multiple glass types, auto-selected per recipe, upgradeable | Adopt notes — P14 |
| C10 | GDD 24 §8 consistency rule: "replace the whole placeholder set at once" | (implied) art lands per surface | Retire the whole-set rule — art has already landed per-stage (tap station); the consistency rule holds **per stage**, enforced by style-reference generation |

Constraints the notes bend but the project does not:

- **UI chrome is never AI-generated** (art-direction rule). "Custom top bar asset", the serve
  button, the tablet shell, the menu paper: all built as drawn-in-engine procedural kit or
  hand-placed pixel art — generation is only for illustrative content (bottles, glasses,
  props, portraits).
- **The generator cannot spell.** "Product names written on the bottles" = blank label areas
  in the art, lettered in engine with the pixel font (the keg precedent).
- **Determinism, Core purity, hidden information, data-driven content** — unchanged. Every
  new refusal (full glass, carbonated-in-shaker, snack-alone) lives in Core, not in menus.

## 0b. Decisions (flagged, reversible)

- **D1 — the emotion layer.** The notes never mention emotions; ID card v3's field list has
  no read rows. Ruling for now: **the emotion engine stays as the hidden satisfaction
  driver** (charges still move regulars, GDD 19/20 stay live under the hood); the *visible*
  reads retire from the card, and the emotional tell moves to the new reaction animations
  (positive/negative after drinking) plus patience. This preserves the "reading customers"
  fantasy through concrete facts + behaviour instead of stat rows. Needs a look in play
  after P15; the layer is opt-in by construction, so either direction stays cheap.
- **D2 — "tables cleared".** Interpreted as: serving faster frees seats faster (already
  true), plus a light bussing beat — a finished customer leaves an empty glass; clicking
  clears it and frees the seat a moment sooner. Lands P12, measured in the sim.
- **D3 — rating replaces the satisfaction bar** as the *visible* reputation currency (C6).
  Internally satisfaction still feeds the stars; nothing about the judge changes shape.
- **D4 — mastery persistence.** Mastery XP is per-run until save/reset lands (P18), then
  becomes cross-run meta. Building it cross-run first would couple it to a system that does
  not exist yet.
- **D5 — one starter body, then variety.** The animation set (idle-at-table, positive and
  negative reactions, constant-speed entrance) is built and proven on the existing Bar
  Patron body first; the 8-archetype body variety follows the proven clip list, every body
  shipping the **same** set (the notes' own rule).

## 0c. Already built — do not rebuild

| The notes ask for | Exists as | Remaining gap |
|---|---|---|
| Patience per customer, visible, decreasing, zero = leaves | Patience rolls ±20% (`RollPatience`), seat clock heats, storm-offs | Gauge on the body (was P8); patience scaling **tips and stars** (P11) |
| Variable order-decide times (2s / 5s / longer) | `RollDecideDelay`: 4s ±35% ≈ 2.6–5.4s | Widen the spread a touch (P11); nothing structural |
| Second/third orders from satisfied returners | Extra-order rule, measured 18.7% of exact serves | Pairing rules: beer-then-beer, drink+snack (P11) |
| Serving preferences vocabulary | `Preparations` (ice/lemon/salt/sugar rims), `FillPreference`, `ShakeEnergy` recorded on every shake | Orders *requesting* them + the judge *grading* them (P11) |
| Liquid fits every glass, never overflows | Core brim-cap law + per-vessel fluid calibration procedure (profile + `SetDensity`, measured) | Run the procedure per new glass (P14) |
| Serve button press feel | `PressSink` | The drawn face (P13) |
| Stock counts, OUT states | Shelf drains; menu keys carry OUT/FULL/BUSY | Out-of-stock *requests* (C2, P11); snack stock (P16) |
| Upgrades with visible scene counterparts | P5 economy: seats/brands/ambience all visible | Per-glass tiers on the under-counter shelf (P14) |

---

## P10 — Content model v2 (Core + data) ☑ 2026-07-31

The vocabulary every later phase speaks. No visible change yet.

- ☑ Ingredient **categories** in data (vodka, gin, rum, whiskey, tequila, liqueurs, juices,
  mixers, garnishes) — a `category` field, loud validation, menu grouping keyed to it
- ☑ **Non-alcoholic ingredients** (juices, sodas, Red Bull, tonic, cola) with a
  `carbonated` flag; **Core refusal**: carbonated never enters the shaker — it is added at
  the serving glass (`BuildAtGlass` verb), same shape as the draught rule
- ☑ **Per-recipe prep method** (shaken / stirred / built) and **per-recipe glass id**;
  recipes that are built skip the shaker entirely
- ☑ **Glassware definitions** in data: id, display name, fluid profile, capacity, sprite
  key, 3 upgrade tiers with prices
- ☑ **Starter cocktail list** as data + `RecipeCatalog` (parity test updated in the same
  commit): Vodka Red Bull, Gin & Tonic, Whiskey & Cola, Cuba Libre, Screwdriver, Vodka
  Soda, Whiskey Sour, Gin Fizz, Martini, Dirty Martini, Margarita, Daiquiri
- ☑ **Snack items** (peanuts, popcorn, chips, mixed nuts): price, stock, no recipe, no glass
- ☑ Icon **slots** per style/recipe (art lands P13; data carries the key now)
Gate met 2026-07-31: loader validates loudly (category/bands/profile/prices), parity
covers all 27 recipes, tests 165/165, and the 200-run sim diffed **byte-identical**
against a fresh same-day baseline. Boot verified live: shelf 6, menu 15, catalogue 27,
glassware 5, snacks 4, locked stock 7.

## P11 — Orders & grading v2 (Core)

The judge learns the notes' payment matrix. This is the phase that rewrites pinned tests.

- ☐ An order = **drink + serving spec** (subset of: ice, salt rim, sugar rim, lemon, extra
  shaken, filled to the top) rolled from the recipe's own sensible options
- ☐ **Low base price** per drink (economy shape: base pay small, tips are the game)
- ☐ Verdict matrix: perfect (right drink + spec + fast) → tips high, stars high;
  correct-but-spec-missed → paid, tip cut per miss; **wrong drink → the delivered drink's
  base price + poor rating** (C1); severe underfill → refusal to pay (extends `MinFill`
  into a gradient); fill closeness scales reward
- ☐ **Patience scales tips and stars** continuously (not only the storm-off cliff)
- ☐ **Out-of-stock requests** (C2): unlocked-but-dry drinks can roll; unfulfillable order →
  disappointed leave + poor rating; **locked drinks never roll**
- ☐ Multi-order sessions: beer-then-beer; drink+snack; never two alcoholic drinks at once;
  first-timers order once (all from the notes, extending the extra-order rule)
- ☐ Decide-time spread widened
Gate: suite green with the rewritten pins; sim runs the new judge; report shows base/tip
split and refusal/disappointment rates.

## P12 — Rating, clock & open flow (Core + HUD)

- ☐ **Star rating**: each serve leaves 1–5 stars (from the same satisfaction the judge
  already computes); floating star feedback beside the money float; nightly average;
  running bar average with history
- ☐ Top-right display: average (e.g. **4.3**) + filled/empty star row (procedural chrome).
  The TONIGHT bar retires (D3); `DayLedger` wealth tiers key off the average
- ☐ Rating drives: arrival rate, crowd wealth odds, tip odds, extra-order odds, **unlock
  gates** for shop content
- ☐ **Open night** (C4): `CustomersOnDay` cap removed; the night runs on a clock
  (18:00–02:00 over the day's real seconds); arrivals keep coming while there is time and
  room, rate set by rating + day; the day ends at closing, not at a quota
- ☐ **Clock HUD** replaces the day counter (C5); day identity stays internal
- ☐ Bussing beat (D2): finished customers leave a glass; click to clear
- ☐ Sim v2: bot plays the open night; report gains served/hour, average stars, clearing
Gate: sim shape comparison — served count now responds to bot speed (faster bot serves
measurably more); no economy explosion (net within ±30% of v1 baseline day 1–5).

## P13 — Menu, shelf & shop presentation

- ☐ **Menu v2**: cream paper / sticky-note flat style (procedural kit); category page →
  **full shelf view** (bottles standing on a drawn shelf, most of the screen); hover → info
  panel (remaining stock, price); click → prep stage; smooth menu↔shelf transition
- ☐ **Icons** for every bottle style and cocktail (generated per style-reference, quantized,
  point-imported); shown beside names everywhere a drink is named
- ☐ **Bottle redesign**: every style re-generated with black outline + blank label; brand
  lettered in engine; one texel density across the set
- ☐ **Trash bin** (C7): drawn bin, bottom-right on the counter; drag the build/glass onto it
  to discard; BIN GLASS button retires
- ☐ **Top bar v2**: procedural chrome redesign carrying day/clock, till, rating stars
- ☐ **Tablet shop**: the market/upgrade flow presented as a tablet — grid of purchases,
  each with preview image + icon + price; restock, brands, glass tiers, recipes
- ☐ **End-of-night receipt v2**: expenses, earnings, customers served, average stars
Gate: measured in play (rects, grain, hover panels); flow regressions checked by playing a
full night through the new menu only.

## P14 — Prep & serve v2 + glassware (the big interaction phase)

- ☐ **Prep stage**: bar-mat backdrop; shaker and bottles scaled up; free drag within screen
  bounds (the confining panel goes); prep tray up the left edge at ~60°, pieces sized to
  match the bottles; drawn SERVE button on `PressSink`
- ☐ **Serve stage in the shaker's interaction style**: left rail garnishes/finishing (ice,
  lemon, rims), right rail **carbonated/mixers added at the glass** (the P10 Core verb)
- ☐ **Glassware system** (C9): the P10 glass set drawn (incl. a dedicated beer glass);
  **auto-selection** — SERVE pours into the recipe's glass; per-glass fluid profile
  calibrated by the measured procedure; drag-to-serve shows the true glass
- ☐ **Decorations persist** on the served drink: salt/sugar visibly on the rim, wedge on
  the glass wall, ice floating in the liquid, garnish on top — the delivered glass shows
  everything that went in
- ☐ **Glass upgrades**: 3 tiers each, bought individually in the tablet shop, standing
  visibly on the under-counter shelf (the tap station's bays extend across the bar);
  upgrade swaps the shelf art
- ☐ Bar mat also appears on the main counter (continuity between stage and scene)
Gate: every starter recipe lands in its correct glass with its decorations; fluid surface
measured (`SurfaceY`) inside every silhouette; tap stage unchanged (beer glass joins it).

## P15 — Customers alive + ID card v3

- ☐ **Animation set** (D5, on the current body first): constant-speed entrance, slightly
  slower; scale up to match the room; idle-at-table (arms on the bar); positive reaction;
  negative reaction — then the same set across all bodies as variety lands
- ☐ **Visible patience bar** attached to the body (absorbs the P8 gauge item)
- ☐ **ID card v3** (C3; absorbs the P8 item; v2 explicitly disliked — full redesign):
  US-licence-inspired card with portrait, first/last name, age, city, previous visits,
  requested drink **with its icon**, serving preferences, customer's bar rating. Later
  slots reserved: favourite drink, last visit, last rating, total spent. **No price,
  anywhere on it**
- ☐ **The order hides** (C3): the bubble stops naming the order; ready-to-order customers
  signal it, the player clicks and reads the card. Enforced in Core (what the HUD can see
  of an uninspected visit), pinned by a hidden-info test the way reads already are
- ☐ Emotion reads leave the card per D1; reactions carry the tell
Gate: hidden-info test green; a full night played by card-reading alone; animation timing
checked in play.

## P16 — Snacks & drink variety

- ☐ **Snack loop**: dragged straight from the menu to a seat (no prep); small income;
  Core refusals — never alone, only alongside an alcoholic order (the P11 pairing rules
  get their content)
- ☐ Drink variety wave 2: more brands per category, more cocktails on the P10 model —
  **new content = new data**, code only if a genuinely new verb appears
Gate: sim shows snack share and pairing legality (zero solo snacks possible); parity test
still green.

## P17 — First audio pass

The project has **zero** audio files; this phase creates the pipeline and the first set.

- ☐ SFX: bottle open, pour, ice drop, shake loop, garnish placement, serve, glass on
  counter, cash/tip, satisfied/disappointed reactions, door
- ☐ Ambient bar bed (low, loopable), volume ducking while stages are open
- ☐ Audio settings (volume, mute) beside the reduced-motion toggle
Gate: every listed event audibly fires in play; nothing clips at overlap.

## P18 — Mastery, economy & run lifecycle

- ☐ **Cocktail mastery** (D4): XP per correct make, thresholds, levels raise that drink's
  base price and tip odds; per-run until save exists, then meta
- ☐ **Upgrade-driven base price**: premium glasses / better alcohol / equipment raise base
  prices; furniture does not (the notes' rule, wired through the existing upgrade economy)
- ☐ **Economy rebalance** (the notes: wealth comes too fast): lower early profits, real
  upgrade costs, longer arc — tuned against sim v3 targets (day-1 net small-positive,
  first red day ~4–5, storm-off <15%, no floor-bot fortune by day 30)
- ☐ Carried from v4 P9: **tutorial shift** (skippable), **arrival pacing feel** (pulses
  and valleys, measured via storm-off clustering), **save/reset on bankruptcy**
Gate: sim v3 targets met; a new player finishes the tutorial and survives night 1 unaided.

---

## Absorption of PLAN_tycoon_pivot P8/P9

| v4 item | Now lives in |
|---|---|
| Seated customers as full scene characters, gauges on the body | P15 |
| ID card v3 | P15 |
| Characters: 6+ bodies, bartender hands | P15 (D5 sequencing) |
| Stage art variants, whole-set replacement | P13/P14 per-stage (C10 retires the whole-set rule) |
| First SFX pass | P17 |
| Tutorial, sim v2 tuning, arrival pacing, save/reset | P12 (sim) + P18 |

## Standing rules through v5

- Determinism (`RunRng` named streams only), Core purity, data-driven content,
  hidden-information integrity — unchanged, enforced by tests. The order joining the
  hidden set (P15) **extends** this rule; it does not bend it.
- The glass cannot overflow; beer is not a cocktail; carbonated is not shaken (P10).
- Tone guardrail (GDD 19): serving people, never curing them; ABV never feeds pricing.
- Art: chrome procedural, illustration generated against a style reference and quantized,
  every word lettered in engine, anchors measured off the art — never guessed.
