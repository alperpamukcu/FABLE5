# PLAN — The Tycoon Pivot (v4)

Source of truth for the v4 transformation (GDD 23/24). Work happens directly on `main`;
every phase lands green (all tests) and playable. The old quota loop stays runnable until
P7 demolition — the same parallel-build/late-delete strategy that carried the pour pivot.

Status legend: ☐ todo · ◐ in progress · ☑ done

## P0 — Paper (this commit) ☑
- ☑ GDD 23 (tycoon loop), GDD 24 (service flow & presentation)
- ☑ Banners on 19/20/21/22 where superseded; changelog v4.0; CLAUDE.md loop pointer

## P1 — Core simulation (pure C#, no Unity) ☑
The tycoon heart, built beside the old loop in `Core/Tycoon/`:
- ☑ `DrinkOrder` — named-drink orders, menu pricing (price = 4 + rank), day-scaled roll pool
- ☑ `CustomerVisit` — seat occupant: patience tick, wait fraction, states (Waiting/Served/StormedOff), extra-order refresh
- ☑ `ServiceJudge` — Exact/Close/Wrong verdicts, base pay, mood/speed tips, satisfaction, orders-again rule (GDD 23 §4–5)
- ☑ `BarDay` — arrival scheduling into limited stools, day completion
- ☑ `DayLedger` — income/expenses/rent, 3-consecutive-red-days bankruptcy, reputation tier for tomorrow's crowd
- ☑ `TycoonCoreTests` — every rule above pinned
- ☑ `TycoonConfig` — all GDD 23 §10 numbers in one place
Gate met: suite green (14 pins), rules mirror GDD 23 tables.

## P2 — Run integration ☑
- ☑ `TycoonRun` controller: day loop over BarDay + shelf/refills + market + ledger; streams `"arrivals" "orders" "patience" "customer" "read"`; regulars/reads attached to visits (registry path wired, first exercised live at P3 bootstrap)
- ☑ Serving path: pour verbs → recipe identification → `ServiceJudge` → visit payment; charges applied to the regular's true stats
- ☑ Wealth tiers modify prices (order roll ×0.75/×1.25) and tips (high-roller mood bonus, broke crowds never speed-tip)
- ☑ Old `RunController` untouched and still green
- Note: market v0 = deterministic brand upgrades; the rotating random market lands in P5
Gate met: `TycoonRunTests` plays a full seeded day headless — arrivals→serves→invoice→strikes→bankruptcy.

## P3 — First playable (debug UI, old input) ◐
Play the tycoon loop before the shaker exists:
- ☑ `TycoonHud`: seat row (6 stools — name, WANTS read line, order + price, patience bar that heats the frame as it drains, locked/empty states), click-a-seat-to-serve
- ☑ Day HUD: top bar (day, arrivals, till, crowd tier, live TONIGHT satisfaction bar), BIN GLASS / NEW RUN
- ☑ Day-end panel: invoice text, RESTOCK / brand offers / STOOL purchase / OPEN TOMORROW; bankruptcy banner
- ☑ Scene boots the tycoon loop (`GameBootstrap.Tycoon`, cloned cards + own RunRng; DebugHud retired from the scene, kept in code until P7)
- ☑ Interim input: shelf-click pouring + garnish pinches against `TycoonRun`; garnish rack moved to the counter top (seat row owns the bottom band)
- ☑ Day-end panel verified in play (invoice, offers, OPEN TOMORROW)
- ☑ `TycoonSimulator` — 200 seeded runs, 9s-per-drink floor bot, Docs/tycoon_sim_report.md
- ☑ Balance v1 (sim-gated, two iterations): rent $15+$5×day, stock $3/capacity, patience
  50−2.5×day (floor 22), arrivals 12−0.5×day (floor 6); bankruptcy strikes watch the
  TILL below zero (in debt means in debt), not the day's net — v0 let a floor bot bank
  $5k with zero bankruptcies; v1 runs the same bot at $132 income vs $125 expenses with
  red days climbing from day 11 (35% by day 15) — upgrade or sink
Gate met: human-playable end-to-end + sim reporting tycoon metrics.

## P4 — The service flow (GDD 24 §1–3) ☑
- ☑ Core shaker model: `Glass` is the shaker, `ServingGlass` receives the pour; `Shake`
  + `AddPreparation` (ice/twist/rims); `PourIntoServingGlass(volume, accuracy)` transfers
  with spill (`GlassContents.TransferInto`/`DrainProportional` — proportional, ratio-
  preserving, brim-capped); `ServeTo` delivers the serving glass and auto-pours the shaker
  perfectly when the aim minigame was skipped (keeps sim/tests/interim-UI on the simple path)
- ☑ Core tests: drain keeps ratios, perfect pour moves the drink whole, a 0.5-accuracy pour
  spills half and under-fills, a sloppy serve pour drops the drink under MinFill and loses
  the recipe (327/327 green)
- ☑ `TycoonServiceFlow`: a MENU button opens the drink menu (bottle list with style-colour
  swatches + remaining %, prep toggles ICE/LEMON/SALT/SUGAR, SHAKE/POUR/EMPTY/CLOSE)
- ☑ Shaker focus stage: dim + hold-to-pour zone into the shaker, live ratio readout, back to menu
- ☑ Serve stage: hold-and-aim pour zone — cursor centred over the glass mouth pours clean,
  drifting off spills; aim bar; ADD MORE / SERVE IT → pick a seat
- ☑ Old direct-pour retired: the back-bar bottles are scenery (no pour callbacks); the seat
  glows cyan when a drink is ready to hand over
- ☑ Verified live: menu/shaker/serve panels render; a 0.5-accuracy serve pour landed 0.35 of
  a 0.7 shaker (half spilled) and delivered a 35%-full glass — spill-by-aim works
Gate met: a full drink is built start-to-finish through the new flow only; spilling is real.
- ◐ Polish deferred to P8: shake is a button (hold+move mouse animation), pour streams are
  bars not liquid, the "menu prop" is a button not a counter object

## P5 — Day-end presentation ☑
- ☑ Invoice UI: an itemised receipt on cream card stock — served/walked-out, satisfaction +
  crowd, drink sales / tips / income (green), rent / restock / upgrades (red), NET bold,
  TILL, and a red debt-strike stamp ("one more red day closes the bar") off the run's book
- ☑ The market as a 3-wide card grid: RESTOCK, brand offers, STOOL, GLASSWARE, COUNTER,
  BACK BAR, MUSICIAN — price in green, "(into debt)" in red when unaffordable, greyed when
  owned/maxed
- ☑ Core upgrade economy: glassware/counter/wall (tiers 1–3) + musician are **ambience**
  (each lifts every visit's satisfaction, capped at +0.15 → richer crowd); seats = throughput,
  brands = margin, ambience = reputation. Prices in TycoonConfig; the day's book itemised
  (DaySales/DayTips/DayRent/DayStock/DayUpgrades)
- ☑ Every buyable has a visible scene counterpart (GDD 24 §6): stools unlock, brands land on
  the shelf, glassware sheens the pour glass, the counter warms, the back bar richens, the
  musician takes the corner stage (`DiegeticStage.ApplyBarLook`)
- ☑ Verified live: served a day to $61, the invoice itemised it, bought all upgrades, and the
  next day's scene showed the richer cabinet + the musician + the −$284 debt till
Gate met: every buyable changes the scene.

## P6 — People polish ☑
- ☑ ID card v2 (GDD 24 §5): tap a seated customer → a big cream licence with photo, NAME,
  AGE, FROM, relationship + demand, the ORDER, an amber WANTS band, and the six readings as
  full-width rows (tag · Exact-tick/Range-span/Unknown track · big value), intent starred.
  Reading = empty-handed seat click, serving = drink-in-hand seat click
- ☑ Extra-order feel: a served regular who orders again gets a gold ★ + round count on the
  seat; the extra-order timing window widened 0.75→0.90 so the *read* is the skill (user ask:
  "not as hard as feared")
- ☑ Relationships surface: NEW FACE vs relationship + visit count, and a greeting line
  ("a familiar face…" / "a stranger…") on the licence
- ☑ Sim measured: extra orders **18.7% of exact serves** (>15% gate met; was 14.4% before
  the window widened), mood tips 23.3% — and this is the floor bot, which does not chase tips
- Note for P9: the extra-order income lifts the competent floor bot to 0 bankruptcies over 30
  days (storm-offs 24.8% are its growth ceiling); the debt spiral still bites sloppy play.
  Full skill-range tuning is the P9 balance pass.
Gate met.

## P7 — Demolition ☑
Two stages, each green:
- ☑ Stage 1: shared pour constants moved to PourResolver; GameBootstrap builds only the
  tycoon run; DebugHud (~1400 lines) and the old RunSimulator deleted (−2073 lines).
- ☑ Stage 2: the whole card-era Core cluster gone — RunController, RoundController,
  WeekQuota/StakeTable/TargetTable/ShopState/TipsBreakdown, ScoringEngine/ScoreBreakdown,
  EmotionResolver/ResonanceJudge/ResonanceResult, Patrons, Tools, Vips, Vouchers, Packs,
  BarCatalog, RecipeMatcher (card). RecipeMatch relocated into Pour/RatioRecipe.cs (the
  tycoon loop reads its Recipe). CustomerReadFactory.ApplyVipRules removed; DataLoader
  trimmed to deck/recipes/archetypes; DiegeticStage's legacy CustomerOrder ID methods gone.
- ☑ Tests: 17 old suites deleted; ReadIntegrity/BaseBar-Market/DataLoader/PourTestKit/
  RegularsAndRead trimmed or rewritten against the tycoon loop. 331 → **108 green**.
- Card-era recipe *pattern* fields stay: `RecipeDefinition` still derives its ratio bands
  from them (they are load-bearing, not dead). VIP/patron/tool/voucher JSON stays as cold
  storage, simply unparsed (GDD 23 §0).
Gate met: suite green, `grep` sweep clean (only doc comments mentioned old names, now fixed),
scene boots the tycoon run; build size dropped ~5k lines of gameplay code + assemblies.

## P8 — Art v3 (GDD 24 §8) — the full set, together
- ☐ New authoring reference (1280×720 logical), style guide refresh
- ☐ Characters: 6+ customer bodies × states (walk/sit/order/talk/react/leave), bartender hands, musician
- ☐ Stage: bar, wall, counter, upgrade variants; menu/shaker/glasses; liquid streams
- ☐ Replace whole placeholder set at once; smoothness pass (easings, frame counts)
- ☐ First SFX pass (pour, shake, till, crowd)
Gate: zero pre-v3 sprites on screen; consistency review sheet.

## P9 — Tutorial, balance, run lifecycle
- ☐ Scripted opening shift (GDD 24 §9), skippable
- ☐ Sim v2: tune GDD 23 §10 (day-1 net +$10…20, first red day ~4–5, storm-off <15%)
- ☐ Save/reset on bankruptcy; endless difficulty curve verify
Gate: new-player playtest completes tutorial and survives day 1 unaided.

## Standing rules through the pivot
- Determinism (RunRng streams only), Core purity, data-driven content, hidden-info
  integrity (reads still gate what the player sees) — unchanged and enforced by tests.
- Tone guardrail (GDD 19): serving people, never curing them with alcohol; ABV never
  feeds pricing bonuses or tips.
