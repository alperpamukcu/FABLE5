# PLAN — Service Depth & Progression (v5)

Source of truth for the v5 expansion, staged from the developer's revision notes of
**2026-07-31**. The v4 tycoon pivot (`PLAN_tycoon_pivot.md`) shipped P0–P7 whole; its open
P8/P9 items are **absorbed here** (mapping at the bottom) and that plan is now a record.

Work happens directly on `main`; every phase lands green (all tests) and playable; every
phase that moves money is **sim-gated** (`LastCall → Simulate Tycoon 200 Runs`, shape
comparison against the previous report). Design lands with the phase: each phase updates its
GDD module(s) in the same commit that changes the rule.

Status legend: ☐ todo · ◐ in progress · ☑ done

> **2026-08-07 — the sweep + the as-built extraction.** The game as it actually runs is
> documented in `Docs/GDD_MEVCUT.md` (single source for live rules) and audited in
> `Docs/GELISTIRME_RAPORU.md`. Where a line below disagrees with those, THEY win. Known
> overtaken lines in this file: D1 (the emotion engine is NOT a hidden driver — demolished
> 2026-08-02, remnants removed 2026-08-07); P14's "glass upgrades: 3 tiers" (shipped as 6,
> see the 08-02 addenda); P16's back-bar "scene build next" (built); the P11/P18 rent
> curves (only the 08-02 addendum's `12 + 2d + d²/9` is live); "filled to the top"
> (retired 2026-08-02, machinery removed 2026-08-07). Historical GDD modules 00–13, 17,
> 19, 20 were deleted in the sweep; 14–16, 18, 21–25 remain the live subsystem specs.

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

- ~~**UI chrome is never AI-generated**~~ — **lifted by the author, 2026-07-31: "UI can be
  generated with PixelLab."** The rule came from the first AI UI set in 2026-07-12, and the
  complaint behind it was *inconsistency*, not generation: every piece invented its own
  texture and the result read as a collage. That is what `style_image_base64` + `style_copy`
  fixed — a generation now takes the project's own art as its style reference instead of
  inventing one. So the ban outlived the problem. What still holds: reference every request
  against existing project art, keep the palette tokens, verify every batch by eye, and leave
  label areas blank because **the generator cannot spell** — text is set in engine with the
  pixel font (the keg precedent).
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
- ☑ **Bussing beat (D2)** *(2026-07-31)*: a drinker leaves the empty glass on the stool
  (`DirtyGlass` in Core), and the stool is blocked until it is cleared — the click does it
  now, the bar's own clock in 7s. Storm-offs leave nothing. The HUD stands the glass where
  the customer sat, clickable with the full press. The floor bot busses on its round, so the
  floor measures a bar that busses (~70k glasses/200 runs) and the ≤1s residual block cost
  moved the shape by noise (16.0%→17.0% bankruptcies, storm-offs byte-level 18.4%): the
  seven-second cost belongs to inattention, not to the floor. It holds a stool until the player clears a glass,
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

- ☑ **Prep stage v2** *(2026-07-31, the serve stage's recipe)*: the panel is the whole
  screen; the bar mat lies under the tin at exactly 2× its pixels (transparent drainage
  channels, so the counter shows through the ribs); tin 200×358, bottle 140×230, drag
  piece 64×72; the four buckets climb the left edge as a tilted column (124×112 each);
  DONE on the drawn plate with the full `PressSink`. Measured in play, sibling order pinned
  (mat under tin). Worth an eye pass: the mat's x-offset and the tray's lean are first
  guesses
- ◐ **Serve stage in the shaker's interaction style**. Right rail done:
  - ☑ **Mixers added at the glass** (the P10 `PourAtGlass` verb). P10 put the rule in Core —
    carbonated never enters the shaker — and then there was no door in the UI to do it
    through, so **the six built cocktails could not be made by playing at all**. The rail
    carries every carbonated ingredient (which has no other way in) plus the juices and
    mixers; a press is 15% of *whatever glass is on the counter*, so a splash into a coupe
    stays a splash.
  - ☑ The SERVE button no longer requires something in the shaker, which was the other half
    of the same bug: a built drink never sees the shaker. The stage is the glass, and you can
    always walk over to the glass. With an empty shaker it hides the shaker and says so.
  - ☑ **Left rail finishing**: ice, salt and sugar rims and a twist, applied to the SERVING
    GLASS through a new Core verb (`AddPreparationAtGlass`). The shaker's verb could not
    reach a built drink at all, so every serving spec asking for ice on one was unmeetable.
    Same refusal as its shaker twin, against the glass it is actually going in: a brimful
    glass takes nothing, because ice needs somewhere to go. Already-applied reads as a tick
    rather than offering itself twice.

Two things surfaced while wiring the rails. The pool on the serve stage was coloured from the
**shaker**, which is the same drink while you are tipping one into the other — which is why it
went unnoticed — but a built drink leaves the shaker empty and a soda was drawing as pale tan.
And both rails were sized for two keys; they carry up to eight now, so they run the height of
the play surface and are labelled above themselves (FINISH / MIXERS) instead of a garnish
caption stranded in the middle of the keys.
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
  - ☑ The glass set **drawn**, and drawn *procedurally*, for a harder reason than the drink
    icons: a serving glass is **hollow** — the drink pools behind it and shows through — so
    with a picture the interior has to be measured off the image by hand. That is what the
    old single tumbler did, which is why the serve stage carried three tuned constants (0.66
    of the half-width, 0.14 up from the floor, 0.6 of the height) that meant nothing except
    "this is where the drink goes in THAT drawing". Five glasses would have been fifteen of
    them. `GlassArt` draws from the same profile the solver fills and **reports** its
    interior, so the fudge factors are gone.
  - ☑ **Per-glass fluid calibration**, by the measured procedure: each vessel filled to a
    quarter / half / three quarters / brim, settled, and read back with `SurfaceY`. Every
    glass drew short, because the one global density was calibrated against the old tumbler.
    The three tumblers now land within 1–2% at a quarter, a half and three quarters, and a
    brimful glass reads 0.94–0.98 of its rim instead of 0.90.
    **Re-benched against the GENERATED art, 2026-08-03** — the IOU below, twice deferred,
    paid. It was real: every vessel drew SHORT at the brim, the rocks and the highball by
    12.5%, a glass filled to the top standing an eighth of an inch under its own rim.
    Densities now pint 1.02, highball 1.02, rocks 0.95 (was 0.97 / 0.88 / 0.87); brimful
    error −12.5% → −2.1%, and the rocks lands every level inside one bin. The instrument's
    floor is ±2.1% (SurfaceY bins the body 48 ways) with about one bin of run-to-run noise
    from the seeding, so that is as measured as it gets.
    **Known and left**: the two stemmed glasses stay pinned at −4.2% at the brim *no matter
    the density* — 0.62 through 1.10 all read the same there, and raising it only spoils
    their middle fills. Their cavity is short and wide (87px of span against 170 of width),
    so the remainder is geometry, not count. That still needs a response curve in
    `MetaballFluid`, not another tuned number. A 0.78 → 0.74 swap on the martini moved it
    inside the noise floor and was reverted.
  - ☑ **Drag-to-serve shows the true glass**: the drink carried to a seat is the same
    drawing, filled to the level it is really at. `GlassArt` emits a second sprite alongside
    the glass — the interior as a solid silhouette, drawn on the same pass so the two cannot
    disagree — and a vertical `fillAmount` clips the liquid to it. That keeps a martini's
    drink inside the cone instead of a rectangle poking through the walls, and it costs one
    Image where the full solver would have been overkill.
- ☑ **Decorations persist** *(2026-07-31, `GlassDecor`)*: salt/sugar as a deterministic
  speckle crust along the measured rim, the wedge perched on the rim's edge, ice floating
  AT the liquid line and riding the fill as the drink pours — on the serve-stage glass and
  on the carried glass both, anchored off `GlassArt.Piece`. Verified in play: all four
  build at the measured rim (crust y=120 on a 260 glass, wedge at the interior edge).
  ~~Mint/olive floats deliberately excluded — their only art is the shelf JARS, and a jar in
  a drink is worse than nothing; they need sprig/spear pieces first.~~ **Landed 2026-08-03**:
  `garnish_mint` (a sprig) and `garnish_olive` (a spear on a pick) were generated, and both
  float at the liquid line exactly as the ice does, on the serve glass and the carried glass
  both. They are INGREDIENTS rather than preparations, so `GlassDecor.Sync` takes the run and
  reads the styles off the shelf cards behind the glass's ingredient ids.
- ☐ **(superseded note)** on the served drink: salt/sugar visibly on the rim, wedge on
  the glass wall, ice floating in the liquid, garnish on top — the delivered glass shows
  everything that went in
### The serve stage as a bar, not a form (2026-07-31, from the author's sketch)

The rails shipped as **button chips** and that is the wrong verb. The brief, with a layout
sketch: *"garnishes must not stay as small buttons on screen — you interact with large props;
it must feel like taking ice out of an ice bucket. The fizzy drinks likewise, on the other
side, at realistic sizes, as if standing in a glass-fronted cabinet whose door you open and
drag from. We want to offer the player the experience of making a drink, not the feeling of
pressing boxes."*

Layout, left to right: **the finishing shelf** (salt, lemon, sugar, ice — open containers you
reach into), **the serving glass** being filled, **the shaker**, and **the fizzy-drinks
cabinet** — a glass-fronted fridge, bottles at their true size, door opens, bottles drag out.

- ☑ **Left: the finishing shelf.** The four chips are gone. Ice, salt, sugar and the twist
  are open containers you drag a piece out of and drop into the glass — the same verb the
  shaker bench already used, aimed at the serving glass. The dropped piece has to land *in*
  the glass or it is not taken.
- ☑ **Right: the fizzy-drinks cabinet.** A lit case with a hinged glass pane; the bottles
  stand inside at their own proportions. The first press opens the door, the next takes a
  bottle out into your hand, and it is poured by being tipped over the glass with the shaker's
  own tilt model and constants — so the measure is **how long you hold it**, not a fixed 15%
  per press, and running a bottle dry mid-pour puts it down and takes it off the shelf.
  *Superseded 2026-08-02 (the door and the scroll came out — every bottle in frame, one
  press away) and again 2026-08-03, when the case became the drawn fridge below.*
- ☐ The confining panel goes; props are free-dragged within the screen. *Half done
  2026-08-03: the panel is the whole screen and the props stand on furniture rather than in
  rails, but a drag is still clamped to the counter region between the table and the fridge.*

**Second pass on the same brief (2026-07-31, after playing it).** The verbs are right; the
staging is not. In the author's words:

- ☑ **Use the whole screen.** *Done 2026-07-31 at prop scale (the measured re-layout); the
  first-person re-staging that followed it is the separate, PAUSED item below.* The serve stage is still a 1120×640 panel floating on the
  scene. The tubs and the cabinet should be placed across the full screen at real size, not
  packed into two 96px columns at its edges.

  *Measured before starting, 2026-07-31, because the obvious reading of this is wrong:* the
  canvas reference is **1280×720**, so that panel already covers 87% × 89% of the screen.
  Stretching it to the edges buys 160×80 px and would change nothing about the complaint.
  What is actually small is the **props**: a 96px rail forces an ice bucket down to 88×74,
  which is why it reads as a button with a picture on it rather than a tub you reach into.
  So this is a re-layout at prop scale, not a panel resize, and it should be done as one
  pass together with the three items below — the shaker size, the room to grow, and the
  cabinet — because they all move the same four columns. The author's sketch gives the
  order, left to right: **finishing shelf · the glass being filled · the shaker · the
  fizzy-drinks cabinet**.
- ☑ **The shaker is too small** on this stage next to the glass. *Done 2026-07-31* in the
  re-layout: the serve stage keeps its own vessel height (`ServeVesselH = 250`) rather than
  borrowing the shaker bench's 180, which left the tin looking like a thimble beside a
  260-tall glass. Measured in play: shaker 146×250, glass 189×260.
- ☑ **BUG — the shaker freezes.** *Fixed 2026-07-31 (`PutTheShakerDown`), verified in play.* It becomes unmovable "when the drink is poured, or when
  what is inside it runs out". Diagnosed: `RefreshServe` hides the shaker the moment the tin
  empties (`SetActive(!run.Glass.IsEmpty)`), but the drain happens inside `UpdateServeTilt`,
  which only calls `RefreshServeText`. So the tin is left standing mid-air, tilted, at
  wherever the cursor dropped it — visible, dead to the pointer on the next stage refresh,
  and never returned to its rest. It has to be put down deliberately when it empties.
- ☑ **The serve stage in first person** *(shipped 2026-08-03)* — the author's direction,
  2026-07-31, after playing the re-layout: *"I want this scene to be a real place more than
  a set of buttons. Think of the perspective as a VR headset POV. On the left an angled
  table with the ice and so on standing on it; on the right an angled fridge asset with the
  mixers inside."*

  **What shipped.** The prep table was reshot to recede toward the upper right (the first
  one was symmetric, which the diagram rules out for a left-wall piece) and the six
  finishing props stand ALONG its top on a line measured off the art, shrinking as they
  recede — ice nearest and largest, the olive jar at the far end. The mixers stand on the
  fridge's own stand lines: its three wire shelves **and its floor**, each followed as it
  falls toward the near end. The scroll columns, the layout groups and the FINISH /
  AT THE GLASS captions are gone.

  Where the two rulings on bottle size conflict, the later one won: 07-31 wanted hand-size
  bottles with the fridge overflowing the screen, but 08-02 tore out exactly that cabinet
  for being unusable, so the fridge keeps every shelf in frame and every bottle one press
  away. The diagram's depth survives as the OVERFLOW rule — stock past twelve stands in a
  second rank, and hovering pulls a bottle in front of whatever it hides behind. Names show
  on hover; twelve captions under twelve bottles is a spreadsheet.

  This is the next pass and it is art-led. What the re-layout already got right is the
  **scale** of the props and the left-to-right order; what it still gets wrong is that both
  columns are flat lists seen straight on, so the props stand in a grid rather than on
  anything. The change is to give them furniture to stand on and a viewpoint to be seen from.

  Two new props, generating as `furn_prep_table` and `furn_mixer_fridge`: a steel prep table
  receding to the upper left, and an upright glass-door fridge angled front-down-left. **Both
  are shot EMPTY** — bare top, bare shelves. This is the same rule the bottles just had to be
  reshot for: anything painted into the furniture is a picture, and the props standing on it
  have to be separate sprites because their positions are hit targets and their levels are
  drawn by the game.

  What has to follow the perspective once the furniture lands:
  1. Props sit on the table's surface line, not in a vertical layout group — so their
     positions come from the art, measured off it the way every anchor in this project is.
  2. Things further back are drawn smaller. That is the whole reason the viewpoint reads as
     depth rather than as a tilted picture.
  3. The scroll shelves survive in spirit, not in form: more finishing touches mean more room
     along the table, and the fridge has three shelves in the art to fill before it needs to
     scroll.

  **The perspective, given as a diagram by the author 2026-07-31** (the second pass on this
  — the first furniture was generated before it arrived and only the fridge survived). It is
  ONE-POINT perspective, a room seen from where the bartender stands, and it fixes three
  regions. Read off the diagram, normalised to the panel (0,0 = top left, 1,1 = bottom right),
  so they scale with the canvas instead of being pinned to 1280x720:

  | region | what lives there | shape |
  |---|---|---|
  | red, upper | back wall — the shaker, the glass, and where they are combined | x 0.26–0.71, y 0.02–0.51 |
  | red, lower | the counter, opening toward the viewer | top edge y 0.51 x 0.26–0.71; bottom edge y 0.93 x 0.05–0.95 |
  | brown | the prep table and everything standing on it | left wall; right edge x≈0.26, bottom slanting (0.26, 0.51) → (0.05, 0.93) |
  | blue | the drinks fridge | right wall; left edge x≈0.71, bottom slanting (0.71, 0.51) → (0.95, 0.93) |

  The horizon sits at y ≈ 0.51, the middle of the screen. That is what makes the counter read
  as coming toward you: its far edge is 45% of the panel wide and its near edge is 90%.

  Consequences for the art, and they are why the first table had to be reshot: a piece on the
  LEFT wall must recede toward the upper RIGHT, and a piece on the RIGHT wall toward the upper
  LEFT. A symmetric prop that recedes straight back belongs in the centre and nowhere else.
  The fridge generated on 2026-07-31 is angled front-down-left and already fits the blue
  region; the table was symmetric and is being reshot to run away to the upper right.

  **Bottle size in the fridge — settled by the author, 2026-07-31.** The question was real:
  the fridge art's interior is 110 of 198 px wide with shelves ~62 px apart, so a fridge kept
  whole on screen tops out around a 125 px shelf gap against a 250 px bottle in hand. The
  ruling is that **the fridge wins and the screen loses**: it fills the right region and its
  back simply runs off the edge. Bottles stay at realistic size. In the author's words —
  *"it's fine if the back of the fridge doesn't fit on screen, let some of it overflow … it
  should feel like there really are bottles in a fridge."*

  What that commits the layout to:
  1. **Bottles at true size**, not shrunk to fit a shelf. The fridge is scaled so its shelf
     gap takes them, and whatever falls outside the panel is clipped.
  2. **Depth on the shelf, not just a row.** Bottles may stand side by side, and at the
     fridge's angle they may also stand one behind another — a shelf with two ranks reads as
     a stocked fridge; a single row reads as a menu.
  3. **Hover pulls a bottle to the front** of anything it is standing behind, and focuses it.
     Without this, rank two is unreachable and the depth becomes a trap. This is the
     `PressSink` hover doing a fourth thing (lift, bloom, warm, and now raise); the sort order
     is what makes the depth playable rather than decorative.
  4. **An info button appears on hover** — what it is and what is left in it, at the bottle,
     rather than on a panel somewhere else.
  5. **Dragging a bottle back into the fridge puts it away.** `PutTheBottleBack` already
     restores the shelf gap on release; this makes the fridge itself a drop target, so putting
     a bottle away is the same gesture as taking it out, run backwards.

  **Paused by the author, 2026-07-31, and reverted to the flat columns** — with a diagnosis
  worth keeping, because it is the thing that has to be fixed BEFORE this is picked up again:

  > *"the generated table's resolution is far too low, look at the mismatch in the image — it
  > should be wider and higher resolution."*

  They are right and the number says so. `prep_table.png` is **298×356** and the staging drew
  it around **915×1100** — roughly a 3× upscale. The props standing on it (`ice_bucket` and the
  rest) are drawn at or near their native size, so the table is visibly three times coarser
  than everything on it. Point filtering makes that worse, not better: it turns the upscale
  into hard 3×3 blocks right next to crisp 1×1 pixels. Nothing about the layout can hide it.

  So the furniture has to be **generated at the size it will be drawn**, not scaled up to fit.
  For a piece that spans half a 1280-wide stage that means roughly 900×1100 — well past the
  400px cap a single `create_image_pro` call takes, so it needs either a different generation
  route or the piece assembled from several tiles. That is the first job when this resumes;
  the layout code that stood props on the surface worked and is in the history at `5d9bef9`.

  **Anchors measured off the installed art, 2026-07-31** — so the layout places props on the
  furniture rather than guessing where its surfaces are. Sprite-local pixels, y down from the
  top of the sprite.

  `prep_table.png`, 298×356 — the steel top is a diagonal band running from the far end at the
  top right to the near end at the bottom left. Sampled every 20 px:

  | y | surface x span | width |
  |---|---|---|
  | 20 | 211–282 | 71 (far end) |
  | 100 | 98–227 | 129 |
  | 160 | 17–188 | 171 (near end) |

  So the surface centreline runs (246, 20) → (102, 160), and depth `t` from 0 (far) to 1
  (near) gives `x = 246 - 144t`, `y = 20 + 140t`. The band is 2.4× wider at the near end than
  the far end, which is the perspective scale a prop standing on it must take: roughly
  `scale = 0.42 + 0.58t`. Props are placed with their BASE on that point, not their centre.

  `mixer_fridge.png`, 198×334 — lit interior spans x 78–188; the wire shelves' standing
  surfaces are at y ≈ 108, 170 and 250, with the floor at ≈ 275, so the shelf gap is ~62 px.
  With the author's ruling that bottles stay at true size (~250 px), the fridge is scaled
  about 3.5× and only two shelves are on screen at once — which is the overflow that was
  agreed to, not an accident to be fixed.

- ☑ **Room to grow.** *Done 2026-07-31.* Both columns are masked scroll shelves
  (`ScrollShelf`), so anything past the visible run scrolls instead of drawing over the
  buttons under it. Sized so the four basic finishing touches fit without scrolling at all
  — content 544 in a 550 viewport, measured — because making the player scroll to reach ice
  would be a worse answer than the rail that overflowed.
- ☑ **Hover, not just press.** *Done 2026-07-31.* `PressSink` now answers the hover as well
  as the press: the face lifts, grows a hair and warms, deliberately the inverse of the
  press so the two read as one gesture — hover brings the object toward you, press pushes it
  away. The hover eases slower than the press, because a press has to feel instant while a
  hover that snaps flickers as the pointer crosses a shelf. One helper, `Pressable()`, puts
  it on every clickable thing, so a button, a bottle and a tub of ice all answer the same
  way. A bottle that is OUT still lifts but does not sink: *finding* the thing and the thing
  *being usable* are different answers, and withholding the first is what made the shelf feel
  dead. Not yet wired to the seated customers — their root is positioned every frame and
  `PressSink` caches a home position, so those two would fight; a static child has to be the
  face there.
- ☑ **BUG — spirit bottles are drawn with their caps off** on the pour stage. *Done
  2026-07-31*, in the same generation pass as the empty bottles below — same nineteen files.
- ☑ **BUG — a bottle's contents are drawn at a fixed level**, so bottles never visibly empty.
  *Done 2026-07-31.* All nineteen reshot empty with the caps seated and a heavier outline, and
  `BottleArt` now measures each bottle's cavity off its own pixels rather than carrying nineteen
  hand-tuned rectangles that would go stale on the next reshoot. Three tests decide what is
  cavity: eroded from the silhouette (drops the wall and the outline), inside the body band
  (drops the neck and the cap, so a full bottle does not fill its own cork), and *not* saturated
  in company (drops labels and crests, which are on the outside of the glass and stay in front
  of the drink). The company clause is the one that took a measurement to find — testing each
  pixel's colour alone speckled the drink where a label's colour reflects through the glass;
  testing its neighbourhood does not, because a label is a block and a reflection is not.
  Two bottles needed a second take for a reason worth keeping: **a can is opaque by definition**
  (energy was specified as a can) and the first syrup came back as smoked glass — neither can
  ever show a level, whatever the code does. The drink is drawn at 70% alpha so the glass's own
  highlights carry through it; at full opacity it read as paint on the outside of the bottle.
  Verified numerically (all nineteen masks build, floors 0.02–0.09, shoulders 0.41–0.76) — but
  **not yet looked at in play**: Unity does not advance frames while the editor is unfocused, so
  a screenshot taken over the MCP link is a stale frame. Worth a look next time the game is up.

- ☐ *(original note, kept for the reasoning)*
  **This one is not a code fix, and that is worth recording before anyone tries.** Looked at
  the art: `soda.png` is clear glass with its liquid *painted in* at a fixed band, and
  `gin.png` is opaque, showing no liquid at all. Nothing in code can lower a level that is
  part of the picture. The bottles have to be reshot **empty** — clear glass, no contents —
  so the game can draw the level itself against `Remaining / Capacity`. That is exactly the
  arrangement the serving glasses already use: `GlassArt` leaves the interior as a hole and
  reports it, and the drink is rendered into that hole. So the fix is a generation pass plus
  a `BottleArt` that reports an interior, not a one-line change — and it should be scheduled
  as such, with the cap fix folded into the same pass since it is the same nineteen bottles.

  **The bottle pass, in full — one job, three requirements:**
  1. **Empty bottles.** Clear glass, no contents drawn, so the level is the game's to render.
  2. **Caps seated** on the neck, not resting loose on top of it.
  3. **A heavier black outline.** The current one-pixel line lets a dark bottle dissolve into
     a dark bar — the author's report: *"they can disappear along with the background."*
     Thicker, and all the way round, so a bottle reads as an object standing in front of the
     room rather than a shape cut out of it. Worth checking the same line weight against the
     keg and the shaker, which were the style reference the bottles were generated from.

  Note for whoever runs it: the previous pass was driven by a `genbottles.py` in the session
  scratchpad, which does not survive the session. The prompt that matters is its `COMMON`
  suffix — "Crisp BLACK OUTLINE all the way round… label area COMPLETELY BLANK" — plus a
  `style_image_base64` of `Assets/Resources/Items/shaker.png` with
  `style_copy: ["shading", "outline", "detail"]`, at 96×160 and `no_background: true`. The
  API caps concurrency at 10 jobs, so it wants a drain loop, and every result must be trimmed
  to its content bounds before install.

- ☐ **Glass upgrades**: 3 tiers each, bought individually in the tablet shop, standing
  visibly on the under-counter shelf (the tap station's bays extend across the bar);
  upgrade swaps the shelf art
- ☐ Bar mat also appears on the main counter (continuity between stage and scene)
Gate: every starter recipe lands in its correct glass with its decorations; fluid surface
measured (`SurfaceY`) inside every silhouette; tap stage unchanged (beer glass joins it).

## P15 — Customers alive + ID card v3

- ◐ **Animation set** (D5, on the current body first). *Reactions landed 2026-07-31*: two
  custom 7-frame south clips on Bar Patron v7 (`cheer` — fist up, head tips back; `upset` —
  shoulders slump, hand drops to the counter), played one-shot on the stool at the departure
  beat, right where the tab settles — the emotional tell D1 moved off the card lives here.
  Chosen by satisfaction (≥0.55 cheers), storm-offs always upset; skipped cleanly on a body
  with no reaction frames, so variety can land body by body. Frame URLs 403 without a
  browser User-Agent now — recorded because the 07-23 fetch worked bare. *Entrance and
  scale landed later the same day*: the walk-in lost its ease-out (the ground slid fast
  under slow feet at the door and slow under fast feet at the stool — a walk cycle only
  reads as walking when the floor moves at one rate), WalkSpeed 405→340, and the body
  340→330/192 window with the same waist crop. Still open: the same set across the other
  bodies as variety lands (that is the P16 variety pass's job by D5)
- ☑ **Visible patience bar** attached to the body *(2026-07-31)*: the gauge left the
  ticket and floats just over the head — a slim bar in the gap between the character
  window and the tag, so reading who is about to walk means looking at the people, not
  at their paperwork. Same colour ramp (cyan calm → lime → amber → red)
- ☑ **ID card v3** *(2026-07-31)*: landscape licence on generated shell art (guilloche,
  navy band, portrait window, stamp), drawn at 3× = the pixel faces' own 24px scale, so no
  coarseness mismatch; anchors measured off the art. Portrait, name, age·city, standing +
  visits, RATES THIS BAR (their satisfied share as stars), order band with the drink icon,
  serving preferences line (garnishes · shaken hard · filled to the top). Reserved slots
  print as blanks until save lands (P18). No price anywhere; the emotion rows left the
  card (D1) and the v2 recipe-band block left with them — GDD 24 §5 rewritten
- ☑ **The order hides** (C3). *Done 2026-07-31.* `CustomerVisit.Order` throws until
  `InspectId()`; the judge and the run read `OrderTruth` internally, so serving blind stays
  a legal gamble. The bubble shows "READY · TAP THE ID" until the card is opened, then the
  name without a price; opening the card IS the inspection, and the card lost its price too
  (C3: no price anywhere). An extra order does not re-hide — spoken across the bar by
  someone whose card was already read. Pinned by two `ReadIntegrityTests`; the sim bot now
  formally inspects (it always read the ID in fiction) and the report is byte-identical
- ☐ Emotion reads leave the card per D1; reactions carry the tell
Gate: hidden-info test green; a full night played by card-reading alone; animation timing
checked in play.

## P16 — Snacks & drink variety

- ☑ **Snack loop** *(2026-07-31)*: bowls stand ON THE COUNTER rather than in the menu —
  a snack has no prep, so routing it through the drink flow would be a stage with nothing
  on it. Click a bowl into hand, click a customer. Core refuses solo snacks (no drink
  order = no bowl) and empty bowls; the price rides the tab; each unit eaten is bought
  back next morning at $1 under menu, filling only as far as the till reaches. Gate run:
  snack share 33.3% of serves, zero solo snacks possible (pinned by test), and the rent
  divisor tightened 6→5.5 in the same commit because the first, free-refill pass wiped
  the P18 squeeze (19.5%→0% bankruptcies) — restored to 16.0% on the same seeds
- ◐ **The back-bar wall + the Book as THE menu** *(the author's direction, 2026-07-31)*:
  the aisle menu retires. Two surfaces replace it: (1) the HOUSE BOOK becomes the recipe
  browser — filter chips shipped (tier · prep · bottle, cycling), scroll shipped; (2) the
  bottle picker becomes a full-screen BAR BACK WALL — the player "turns around" to face
  shelves of every bottle, no category grouping; click a bottle → the prep stage. Assets
  are a KIT at native 1× grain (the prep-table resolution lesson): `backwall_shelf`
  (corbelled plank, 400×72, ends + repeatable middle — installed), `backwall_header`
  (art-deco lit cornice, 400×110 — installed), `backwall_tile` (wall texture — FIRST TAKE
  CAME BACK EMPTY: `no_background: true` deleted a texture that IS background; regenerate
  with `no_background: false`, job may still be pending server-side). Scene build next:
  full-screen wall in the Menu stage, planks tiled/sliced at 1×, `AddShelfBottle` reused
  (hover info + click→prep already work), SERVE→/bin kept. New content shipped with it:
  4 bottles (cranberry, coffee liqueur, pineapple, grenadine — installed, quarantined) and
  5 cocktails (Black Russian 8, Sex on the Beach 13, Cosmopolitan 19, Espresso Martini 23,
  Mai Tai 24) — 35 recipes, 16 shaken.
- ☑ **Menu redesign — style bands whole** *(2026-07-31, the author's direction)*: the 13
  abstract type recipes retired; 26 named cocktails in four rank tiers (see GDD 23 §3), only
  Draught and Neat Pour type-based on purpose. **What the sim gate caught, in order:** the
  brim-truncation bug in `TransferInto` (pouring a big shaker into a 0.7 rocks glass ruined
  the ratio through no fault of aim — every Gin Sour served Wrong); the bot crashing the
  whole batch on its first carbonated style band (C8: fizz never enters the shaker — it now
  builds two-stage like the player); the sim never wiring `lockedStock` (bought menus were
  undrinkable, half of every night silently declined); and two price-ladder overshoots
  measured and walked back — the ORIGINAL MenuPrice curve was never the problem, the
  unplayable menu was. Floor redefined and recorded: the bot buys the menu and its bottles
  (the growth that used to be free-by-day), never brand upgrades/ambience. Landed: 1.5%
  bankruptcies, till median $40 (p25 −$18), 98% exact, 15 recipes bought/run. Residual: 2%
  Wrong at the floor unexplained (worth one diagnostic), knife-edge margins are P18's
  economy pass to finish.
- ☑ Drink variety wave 2 *(2026-07-31)*. The genuinely new verb appeared, twice over:
  nothing ever unlocked P10's twelve locked cocktails, and nothing ever consumed the
  `LockedStock` their styles needed — both were dead content. `UnlockRecipe` buys a recipe
  at day end (rank-priced, star-gated per C6: 15–18 open / 19–22 at 3.5★ / 23+ at 4★) and
  releases the recipe's waiting bottles into the market, re-rolling tonight's offers so the
  drink is never unstockable. Tablet shop sells the book on THE WELL tab. Content: Mojito
  (five bands) + Tequila Sunrise, tier-2 rum and tequila brands. Parity test green; the
  orphan-upgrade test loosened to "reachable via tier-1", with the reasoning in place
Gate: sim shows snack share and pairing legality (zero solo snacks possible); parity test
still green.

## P17 — First audio pass

The project has **zero** audio files; this phase creates the pipeline and the first set.

- ☑ SFX *(2026-07-31)*: all thirteen SYNTHESIZED — square blips, damped sines, filtered
  noise — because no generator on hand makes audio, and lo-fi synthesis lands in the same
  world as the pixel art rather than beside it. Wired: bottle open (menu + cabinet), pour
  (a held LOOP, driven once per stage frame so the tin and the hand bottle cannot silence
  each other), ice drop, garnish, shake loop (while shaking), serve clink, glass down
  (capping the tin, putting a bowl back), cash + cheer/upset at the tab settle, door on
  arrival, stage-change click. Peak held to 0.7 at synthesis and one-shots round-robin six
  voices, so overlap headroom is by construction
- ☑ Ambient bar bed: 5.75s seamless loop, eased toward loud/ducked while a stage or the
  licence is open — menus muffle the room instead of gating it
- ☑ Audio settings (volume cycle, mute) beside the reduced-motion toggle, in a small gear
  sheet on the top bar — the settings surface module 16 deferred, at minimum useful size.
  The mute row clicks on un-mute, which is itself the test
Gate: every listed event audibly fires in play; nothing clips at overlap. *Wiring and
clip-loading verified by measure (13/13 clips load, 8 sources build); the AUDIBLE half of
the gate needs ears — the author's playthrough.*

## P18 — Mastery, economy & run lifecycle

- ☐ **Cocktail mastery** (D4): XP per correct make, thresholds, levels raise that drink's
  base price and tip odds; per-run until save exists, then meta
- ☐ **Upgrade-driven base price**: premium glasses / better alcohol / equipment raise base
  prices; furniture does not (the notes' rule, wired through the existing upgrade economy)
  **Rent curve v2 shipped on the diagnosis, same day.** The compounding-mechanic route was
  considered and rejected with reasons: reputation already compounds (stars → crowd → prices
  and tips — the config's "third leg"), and a money → crowd link has no fiction — customers
  cannot see the books. The measured problem was that red days cluster late and the early-game
  till absorbs them, so the fix is aimed exactly there: rent gains a quadratic term
  (`14 + 2.5d + d²/6`), a shade GENTLER than v1 through day 10, and $239 by day 30 against
  the floor bot's flat ~$133/night.

  Sim, 200 runs, same seeds: bankruptcies **0% → 19.5%**, survivor till median **$419 →
  −$151** — at the floor, month-end now means debt, and the runs that reach the horizon are
  mostly two strikes from the door. Days 1–10 red days unchanged (0–2.5%); day 15 doubles
  (16.5% → 32%). Everything not money is byte-stable across the reports (storm-offs 18.4%,
  satisfaction 68%, exact 96.8%), so the change is isolated. The floor bot cannot buy seats,
  brands or crowds — a player who grows has every tool to outrun the curve, and that demand
  IS the tycoon. If playtest says too harsh, the knob is the quadratic divisor (6 → 8), not
  the linear term.

  **Diagnosed from the sim, 2026-07-31 — the threat exists and never lands.** Re-ran 200 seeded
  runs against the current build. Rent *does* climb (`Rent(day) = 14 + 4.5*day`, so day 30
  costs $149 against day 1's $18), and red days climb with it: 0.0% on day 1, 6.0% on day 13,
  **16.5% by day 15**. And still **0 bankruptcies in 200 runs**, every one reaching the
  horizon. So the problem is not that nothing goes wrong — something goes wrong more and more
  often. It is that a red day never compounds: by the time they arrive the till has grown
  enough to absorb them, and one bad night costs nothing that the next good one does not
  return. Average day: $133.2 in against $119.7 out.

  That points the rebalance at the *shape* rather than the numbers. Making rent steeper was
  already tried and recorded in `TycoonConfig` — it took bankruptcies from 1.5% to 43.5%, a
  cliff rather than a difficulty. What is missing is a way for a bad night to cost the NEXT
  night: the till absorbing everything is what makes the curve flat. Worth trying before any
  price is touched.

  Two caveats on the numbers, both structural. The bot serves at band midpoints and never
  shops, so 96.8% exact / 0.0% wrong is a perfect player's floor, not a prediction. And tips
  are **43.9% of the whole take** ($4.78 tip against $6.10 base per serve) while the bot never
  chases mood tips — so the tip system is carrying nearly half the economy without being
  played at all.

- ☑ **Wave-3 floor bill settled (2026-08-01):** knob 1 measured at both ends — RecipePrice
  5+2(r−2) landed 2.5% bankruptcies (too soft), the midpoint 5+2.5(r−2) landed **14.0%**
  with the till median at −$27: the rent-v2 target band, threat without a cliff. The
  original diagnosis below kept for the record.
- ~~**FIRST for the economy pass (2026-07-31 close-of-day):**~~ wave 3's five buyable
  recipes moved the floor 7.5% → 43.5% bankruptcies (income flat $167, expenses +$4/day of
  purchases). NOT the bin fee — measured twice, fee 3→2 changed nothing (the bot barely
  bins; it is a player-mistake tax, floor-neutral by design). Nor late buying — a day≤23
  guard moved it 1%. Candidate knobs, in order: RecipePrice 6+3(r−2) back to 5+2(r−2);
  or MenuPrice nudged. The knife edge itself is this pass's subject.
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


---

## 2026-08-02 — the star loop v2 (author-directed, shipped)

The author's big ruling: **the stars are the spine now.** Shipped and sim-gated in one
pass; every number below was measured, not guessed.

- **Standing starts at 0 and is inertial** (`BarRating`): a night drags it one step
  (gain 0.10/step, loss 0.20, max +0.25/night). Five stars is the endgame/sandbox line.
- **Nightly stars are capped twice** (`TycoonRun.UpgradeStarCap` / `MenuStarCap`):
  fittings (glassware line + stools) and the best EXACT rank served that night. The recipe
  star gates moved under the caps they unlock (2.0/3.0/4.0) — the first sim found the old
  3.0 mid-gate sat above the 2.5 starter cap and deadlocked all 200 runs at 2.5 stars.
- **Crowd**: tomorrow reacts to TONIGHT (broke below 1.5 night-stars); the high rollers
  come only when the STANDING clears 4.2. Arrival pivot moved 3.0 → 2.5.
- **Rent retuned for the slower climb**: `12 + 2d + d²/9` (old curve = 100% bankruptcies
  at day ~24; soft pass = +$420 median. This one: 0% floor bankruptcies, median till +$69,
  income $133 vs expenses $131 — knife-edge for a PERFECT server, hard for a human).
- **Fill demands retired**: nobody asks SHORT/LONG/to-the-top any more; the only fill rule
  is the judge's house floor (under-pour costs, brim never rewards).
- **Upgrades pruned** to stools + glassware (musician/counter/wall verbs remain in Core,
  off the shop). Upgrades now gate the star ceiling, which is what makes them mandatory.
- **Market v2**: four tabs (restock / bottles / recipes / upgrades), per-bottle refills
  (`RefillBottle`), star-gated recipes are SEALED (identity hidden), buyable recipes show
  their pour on hover, and everything bought at a close is refundable until dawn
  (`TodaysPurchases` / `RefundToday`).
- **Calendar**: six open days (TUE–SUN), Mondays dark, week counter in the top bar.
- **Dev presets** (`TycoonRun.DevPreset`): fresh / midgame / endgame under the gear menu.

Still open from the author's same note: per-glass upgrade tiers with the under-counter
glass shelf (the big one), the serve-stage pour gauge in the new tube frame, and swapping
the generated keg/licence art in for their procedural stand-ins if they read better.


## 2026-08-02 — per-glass upgrade lines (author-directed, shipped)

The last big item from the same note: **every glass type is its own upgrade ladder.**

- Core: `GlassTier(id)` / `BuyGlassTier(id)` / `GlassUpgradeSteps` replace the single
  glassware ladder; refundable same-day like everything else. glassware.json's per-line
  `tierPrices` finally consumed (P14's own IOU). The star cap reads total steps
  (0.30/step — measured: 0.25 with the original prices went 0% → 85% bankruptcies, so the
  json prices were halved and the step raised; the floor now lands at 13.0%, till −$10).
- One glass everywhere: the tap's pint, the serve stage, the carried drink, the dirty
  empty on the counter (DirtyGlass now records its `GlasswareId`) and the shop cards all
  draw the same `GlassArt` at the line's tier — tier 2 wears etched rings, tier 3 a gold
  rim and foot, geometry untouched so every measured interior survives.
- The under-counter rack: the owned lines stand on a walnut strip left of the bin in the
  main scene, redrawn on every purchase (`RefreshGlassRack`).
- The serve stage gets the brass-capped tube gauge (twin of the shaker's), reading the
  serving glass with its headroom as a pale tail.

~~Open: the tap-stage pint's fluid density (0.90) was measured against the OLD pint.png
cavity — re-measure `SurfaceY` against the GlassArt pint next time play is at hand.~~
**Closed 2026-08-03.** The tap stops using its build-time 0.90 the moment the pint art is
set (`SetDensity(tapPiece.Density)`), so it already reads GlassArt's number; that number
was re-benched the same day (P14 above) and the pulled pint checked in play — it fills to
its rim with the head sitting on the beer, because the head rides `SurfaceY` rather than
the nominal line.


## 2026-08-02 (later) — the generated 3D glass set + the six-step ladder

- The five vessels are GENERATED art now (`glass3d_*` + `_fill` masks): volumetric,
  open-mouth, vice-lit. Install pipeline in `install_glasses.py` — hard alpha, trim,
  interior baked to 0.40 translucency (the drink renders behind and shows through),
  geometric fill mask, floors/rims measured and baked into `GlassArt.Gen3DTable`
  (rocks' floor capped onto its inner ledge by eye). The procedural set remains the
  fallback for anything without art.
- **Six visual tiers per line** (the author): 0★ base + 1★–4★ + the 5★ legendary set.
  `MaxGlassTier = 6`, five priced steps per line in glassware.json. The cap is
  FRONT-LOADED per step (0.20/0.15/0.12/0.08/0.05 — measured: a flat 0.12 quadrupled
  the cost of a cap-star and sent the sim 13% → 83% bankrupt; front-loading plus a
  prudent bot lands 4.5% / median till $0). Late steps are prestige, not survival.
- Tier ART for the five upgrade sets is the NEXT session's work, with the author
  reviewing the base set first. ~~Fluid density (`Gen3D.Density`) still carries the
  procedural vessels' measured values — re-read SurfaceY once the art is settled.~~
  **Done 2026-08-03** — see the re-bench under P14. The procedural `Densities` table is
  deliberately untouched: it belongs to the fallback silhouettes and was measured against
  them, so it is right for the glasses it actually draws.

## 2026-08-11 — the loop rework (author-directed, four phases)

The author's clarified loop: fast SLIDE navigation between the flow's stages (edge back
button, no sharp cuts, a new Shaker→Serve edge), a MANDATORY MIX for two-spirit tins,
and CLICK-TO-SERVE replacing the drag (only taken orders are selectable; the glass slides
down the counter). Decisions taken: fast-nav over multi-select; `Stir` verb added (all
ten stirred recipes carry two spirits); the drag deleted outright; the cap stays one-way.

- **Faz 1 (shipped with this entry):** Core `Stir`/`IsMixed`/`MixRequired`/`CanPourOut`
  + the refusal on `PourIntoServingGlass` (GDD 21 §14); the sim mixes by `recipe.Prep`;
  8 new gate tests (196 total). Sim report stayed byte-identical.
- **Faz 2:** the bar spoon on the shaker bench (open tin = stir, capped = shake) and the
  "TO THE GLASS" edge key gated on `CanPourOut`.
- **Faz 3:** the two-slot stage slide stepper in `TycoonServiceFlow.GoTo`, edge back
  buttons, `RectMask2D` on the field, `AdvanceStageOpen` retired.
- **Faz 4:** the drink-glass state machine (Ready→Sliding→served), seat filter
  `Waiting && HasOrdered && IdInspected` (HUD-level — Core's blind serve stays legal),
  bin click, drag deletion.
