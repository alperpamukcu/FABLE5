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

## P11 — Orders & grading v2 (Core) ☑ 2026-07-31

The judge learns the notes' payment matrix. This is the phase that rewrites pinned tests.

- ☑ An order = **drink + serving spec** (ice, salt/sugar rim, lemon, extra shaken, filled to
  the top) rolled from the recipe's own sensible options — a pint takes no garnish, a built
  drink is never shaken
- ☑ **Low base price** per drink: `3 + (rank+1)/2`, about half the old `4 + rank`
- ☑ Verdict matrix: perfect doubles the drink; correct-but-spec-missed is paid with the tip
  cut; **wrong drink pays the delivered drink's own base price** (C1) and $0 if the glass is
  no recipe at all; **under 35% full is refused outright**; fill closeness scales the reward
- ☑ **Patience scales the tip continuously** — it used to hit zero at half patience
- ☑ **Out-of-stock requests** (C2): `CanMake` reads the shelf, `DeclineOrder` is the honest
  reply (pays nothing, scores above a storm-off); locked drinks still never roll
- ☑ First-timers order once; the craft gate for an extra round is unchanged otherwise
- ☑ Decide-time spread widened (±35% → ±55%, ≈1.8–6.2s)

**Gate met.** Tests **180/180** (165 → 180; five old pins rewritten, four rent pins re-derived
from `Config.Rent` so P18's tuning cannot break them again). Sim, 200 runs against the
pre-P11 baseline:

| | baseline | P11 |
|---|---|---|
| Bankruptcies | 1.5% | **5.0%** |
| Income / expenses per day | $126.8 / $125.3 | $120.6 / $118.0 |
| Tip share of the take | ~24% | **40.6%** |
| Garnish craft landed | 11.9% | **54.7%** |
| Extra orders (of exact) | 10.7% | 26.2% |
| Storm-offs | 18.5% | **29.1%** |
| Final till median | $56 | $87 |

Three findings worth carrying forward:

1. **A real bug, found by the phase and fixed in it.** `TransferInto` moved liquid and not
   preparations, so every cocktail garnish was ungettable from the day the serve pour became
   compulsory (2026-07-28). The sim had been reporting it all along — "craft landed" was
   *exactly* the draught share, because a pint is the one drink whose preparation is stamped
   on the glass it is pulled into. Documented as GDD 21 §13.
2. **The bot was a strawman, not a floor.** It ignored the serving spec, which the licence
   prints and a player reads, so it forfeited the whole 35% spec weight of the tip: 56% spec
   score, 2.7% of spec'd orders met. Teaching it to read the spec is what makes the floor
   mean anything again — and it is why extra orders more than doubled.
3. **Rent had to move with the price ladder.** Halving base prices and leaving expenses alone
   is not "deferring tuning to P18", it is shipping half a change: the floor bot went bankrupt
   in **43.5%** of runs. Rent came down by roughly the share the take did
   (`15 + 5×day` → `14 + 4.5×day`), restoring a playable shape. **P18 still owns the curve.**

**Flagged for P12:** storm-offs are up from 18.5% to 29.1%, driven by the extra-order rate
more than doubling now that the craft is actually gettable. Not tuned here on purpose — P12
removes the customer cap and puts the night on a clock, which replaces the arrival model that
number comes from. Tuning it against a model about to be deleted would be wasted work. P18's
target is <15%.

## P12 — Rating, clock & open flow (Core + HUD) ☑ 2026-07-31

- ☑ **Star rating**: every finished visit leaves 1–5 stars (storm-offs included); nightly
  average, running average, per-night history (`BarRating`)
- ☑ Top-right display: the average and five fill-lerped stars, drawn procedurally at the
  pixel grain. The TONIGHT bar retires (D3); the crowd keys off the **running** standing
- ☑ Rating drives the **arrival rate** (5★ → 75% of neutral gaps, 1★ → 130%) and the crowd
  wealth tier. *Deferred to P18: tip odds, extra-order odds, shop unlock gates.*
- ☑ **Open night** (C4): the quota is gone; the shift is 95s of bar time, arrivals keep
  coming until closing, and closing does not evict anyone mid-drink
- ☑ **Clock HUD** replaces the day counter (C5) — 18:00–02:00, LAST CALL at closing; the day
  number survives underneath for rent, the ledger and the strikes
- ☑ Sim v2: served per night, served per bar-minute, bar standing; plus a new
  **`LastCall → Measure Service Speed Response`** that runs the same seeds at three service
  speeds — the gate, made measurable rather than asserted
- ☐ **Bussing beat (D2) deferred to P14.** It holds a stool until the player clears a glass,
  which is a throughput change, and P14 is the phase that owns the serve-stage interaction.
  Adding seat-blocking friction in the same phase that opened the night would have made both
  unmeasurable.

**Gate met.** Tests **181/181**. Throughput now answers to speed — 11.6 / 9.3 / 7.8 served per
night at 5s / 9s / 15s per drink, a 32% spread where the quota allowed none. No economy
explosion: income $133.8/day against the pre-P11 baseline's $126.8 (+5.5%).

Two findings:

1. **A tick-order bug, caught by a test.** `BarDay` advanced the clock and *then* asked whether
   the door was shut, so a single tick spanning the whole shift opened and closed the bar with
   nobody walking in. Invisible at a 60th of a second, plain in the sim and in any test that
   ticks in one big step. Arrivals now run on the part of the tick that falls before closing.
2. **An open night needs people to be able to say no.** Uncapped arrivals plus a door that
   admits anyone the moment a stool frees is a machine for generating disappointed customers:
   storm-offs went to 31.4%. Balking at three waiting fixed it (19.0%, against 18.5% under the
   quota), lifted satisfaction 60% → 69%, and roughly doubled how much service speed matters.

**Flagged for P18 — the economy is now too generous.** Bankruptcies are at **0%** and the floor
bot banks a median **$469** by day 30, against $56 at the pre-P11 baseline. Two phases have each
moved one lever for a defensible local reason (P11 eased rent to match the halved price ladder;
P12 opened the night), and the compound result is a bar that cannot fail. P18's rebalance is no
longer optional — but it now has a far better instrument to tune with: an open night, a balk
threshold, a star-driven arrival rate, and a speed-response harness.

## P13 — Menu, shelf & shop presentation ☑

- ☑ **Menu v2**: the index is the bar's **aisles** (v5 P10 categories — VODKA, GIN, JUICES,
  MIXERS, ON TAP — not ingredient types), printed as flat **cream paper keys** with a hairline
  rule and two weights of ink. The coloured plastic plates are gone: the only colour on the
  page is the drink.
- ☑ **Shelf view**: an aisle opens onto a **shelf** — bottles standing on a plank at their own
  proportions, centred on it, names lettered underneath in engine. Not a grid of keys.
- ☑ **Hover info panel**: what is left in the bottle (with a fill bar that reddens as it
  drains) and what a restock costs, raised beside the bottle and clamped inside the board.
- ☑ **Bottle redesign**: all nineteen reshot against the sheet's own palette, outline and
  shading — the twelve already on the shelf plus the seven v5 P10 added and nobody had drawn.
  Labels are left blank and the brand is lettered in engine, the arrangement the keg settled
  on, and each is trimmed to its content bounds so proportions are true on the plank.
- ☑ **Trash bin** (C7): the `BIN GLASS` button is gone. A drink is thrown away by carrying it
  to the bin on the counter — the same verb that serves it. Tested before the seats, because
  the bin stands among them.
- ☑ **Drink icons**, for every recipe. **Drawn from the recipe rather than generated**, which
  is the decision worth recording: `glassware.json`'s silhouette profile is already the shape
  the fluid solver fills, and a recipe's ratio bands are already what goes in it, so an icon
  composed from those two can never drift from the drink and a recipe added to JSON gets its
  icon the same day. It also keeps the project's own rule — chrome is procedural, generation
  is for illustration — and it costs nothing per recipe, where 27 drawings would have.
- ☑ **Top bar v2**: three groups anchored to their own edges — clock left, till centre,
  standing right — instead of hand-tuned offsets from the middle, and **opaque**, because at
  0.82 alpha the neon sign behind it showed through the star rating.
- ☑ **Tablet shop**: the market moved into a tablet — bezel, lens, home bar, a status strip
  carrying the till, and the bottle art on the listing that sells it. The shell is the cheap
  half; the **tabs** are the real fix, because restocking the well and buying a musician are
  different errands and thirteen identical cards made the player read all of them to do one.
- ☑ **End-of-night receipt v2**: a till slip, not a summary panel. It **itemises what was
  poured** — `2x GIN FIZZ ... $8` — above the totals, set in monospace columns at 16pt so
  the leader dots land on the pixel grid.

The receipt needed one thing Core did not keep. Lines are taken from what was *poured*
(`CustomerVisit.Served`) and priced at the new `PaidBase`, not from menu prices, because a
wrong drink is paid at the price of the thing in the glass — listing menu prices would leave
a night where the player misread somebody quietly short. Two tests pin it: the itemised bases
sum to `DaySales`, and a storm-off leaves no line at all.

The top bar turned up a real bug rather than a layout one. The rating read **"3,0"**, because
every number in the project was formatted in the machine's own culture; the editor here is
`tr-TR`, which also writes a percent as `%75`, which is why four call sites were patching
`:P0` with a string replace that only ever worked under a culture none of them were running
in. The same setting decides how `0.75` is *read*, so a glass profile or a ratio band could
have come out of a data file meaning seventy-five instead of three quarters on someone else's
desktop. The game now pins one culture at boot — invariant, with the percent pattern amended
to `n%` — and the four hand-patches are gone.

Proportion turned out to be presentation, not content: the profile says a martini is a cone
and a highball is a tube, but not that the highball is the *tall* one, and at 32px the glasses
have to be told apart by silhouette alone. So `DrinkIcon` keys width and height off the glass
id and takes only the taper from the data. The reverse also surfaced — the fifteen pre-pour
shapes had **no glass at all**, so every one of them drew as the same default tube. They now
name one in `recipes.json` *and* `RecipeCatalog`, which the parity test compares, and P14 gets
the serving side of that for free.

Two layout traps found and fixed, both the same shape: `_bottleList` carries a
`VerticalLayoutGroup`, so anything parented to it has its size and position taken over.
Hand-anchored shelf bands came up **blank**, and the hover panel was laid out as another list
row — losing its size, its backing plate and its place above the bottle at once. The shelves
are now stacked *by* the layout (which is what they wanted anyway) and the panel is parented
to the board instead.

## P14 — Prep & serve v2 + glassware (the big interaction phase)

- ☐ **Prep stage**: bar-mat backdrop; shaker and bottles scaled up; free drag within screen
  bounds (the confining panel goes); prep tray up the left edge at ~60°, pieces sized to
  match the bottles; drawn SERVE button on `PressSink`
- ☐ **Serve stage in the shaker's interaction style**: left rail garnishes/finishing (ice,
  lemon, rims), right rail **carbonated/mixers added at the glass** (the P10 Core verb)
- ◐ **Glassware system** (C9). Core half done:
  - ☑ **Capacity per glass** — P10 specced it and shipped without it, so every glass held
    1.0 and the set was cosmetic. A coupe is 0.55 and a pint 1.6, scaled against the old
    single glass, and `minFill` and the ratio bands are shares of *that*.
  - ☑ **Auto-selection**: the glass comes down at the pour out of the shaker — the last
    moment the bar can reach for the right vessel and the first moment it has anything to
    reach for it with — and at the first pull on the tap, which is always a pint. An
    unrecognisable mix lands in the default. Refused once there is liquid in the glass:
    swapping the vessel under a drink is a spill or a free top-up depending on which way the
    capacity moved.
  - ☑ The **simulator plays the glass set** too. It was building runs without one, so it
    was measuring a bar nobody plays.
  - ☐ The glass set **drawn** (incl. a dedicated beer glass); per-glass fluid profile
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
