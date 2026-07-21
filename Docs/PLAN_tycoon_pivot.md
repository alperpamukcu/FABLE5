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

## P3 — First playable (debug UI, old input)
Play the tycoon loop before the shaker exists:
- ☐ Seat row on stage: up to 6 patrons with order bubbles, patience clocks, satisfaction bars
- ☐ Serve targeting: SERVE → click a seat
- ☐ Day HUD: top reputation bar, till, day progress; day-end invoice + market panels (functional, plain)
- ☐ Keep shelf-click pouring as interim input
Gate: a human can play day 1→bankruptcy with only new-loop UI; sim bot ported to tycoon metrics (earnings/day, storm-off rate).

## P4 — The service flow (GDD 24 §1–3)
- ☐ Counter menu prop + drink menu UI (bottles leave the stage)
- ☐ Shaker focus stage: dim, hold-to-pour into shaker, preparations before shake, shake input (hold+move)
- ☐ Serve stage: glass + shaker pour with aim/spill, then seat targeting
- ☐ Old direct-pour input retired
Gate: full drink built start-to-finish through the new flow only; spill-by-aim works.

## P5 — Day-end presentation
- ☐ Invoice UI (bill layout, strike stamps), market as shelf cards, upgrade purchases
- ☐ Upgrades change the scene: stools, counter, wall, glassware, musician (GDD 24 §6)
Gate: every buyable has a visible scene counterpart.

## P6 — People polish
- ☐ ID card v2 readability (GDD 24 §5)
- ☐ Extra-order flow feel (bubble refresh, patience refill animation)
- ☐ Regular relationships surface (greetings, remembered orders — dialogue hooks)
Gate: blind-read tip rate and extra-order rate measured by sim; extra orders reachable (>15% of exact serves).

## P7 — Demolition
Only after P3 makes the new loop the played loop:
- ☐ Quota/week system, score targets, ScoringEngine consumers, score patrons/tools/packs/vouchers/favors
- ☐ Card-era recipe pattern fields not needed by band derivation
- ☐ Old round HUD paths, dead assets, dead tests (replaced by tycoon pins)
- ☐ VIP rule cards parked into data cold storage (GDD 23 §0)
Gate: suite green, no orphan references (`grep` sweep), build size drop recorded.

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
