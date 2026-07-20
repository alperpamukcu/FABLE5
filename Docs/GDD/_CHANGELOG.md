# GDD Changelog

## v3.2 (current) — The customer POV, style identity & the merciful spill

- **The stage flipped to the customer's side of the bar** (22 §1): bottles on two back-bar
  wall shelves (spirits up, mixers down), the till beside the patron, the counter along the
  bottom. GDD 18's layout section is banner-superseded; `DiegeticStage` holds the truth.
- **Style identity is explicit** (22 §1): display names carry the style word ("Astra
  Vodka"), and every style owns a signature colour (`UITheme.StyleColor`) worn by the shelf
  tag, the ratio list and the liquid itself.
- **A spilled glass can be served** (21 §3): charges cap at one glass's worth, no recipe,
  no fill bonus, still a spill for patron conditions. The hard "bin it first" block is gone.
- The pour glass is a proper stemmed cocktail glass whose fill is clipped by a stencil mask
  baked from the sprite's own bowl — no more square fill floating on the art (21 §3.1).
- **Garnishes go in by the pinch** (21 §3): one tap = a fixed 5% of the glass
  (`GarnishClickFraction`); no more held-jar 1% slivers.
- **Recipe generosity pass** (21 §9): derived bands ±15% → ±20%, unnamed-stray allowance
  10% → 15%. Free-hand pouring should be a judgement call, not a precision test.
- The recipe book rewritten for the pour era: pourable recipes shown as their ratio bands
  (type-coloured, with FILL minimums), unpourable ones counted as "house secrets"; the
  card-era dot patterns and "pick 1–5 bottles" hint are gone.
- Fixed: HUD texts no longer swallow clicks (the win banner sat exactly on the upper shelf
  and made its whole row unclickable); the balance sim loads `base_bar.json` (it silently
  died on the deleted classic bar).
- The sim bot grew up with the shelf: it refills the well (upkeep, not strategy), never
  stalls on a drained or already-landed customer, and **seeks recipes** — staffing each
  pourable recipe's bands with intent-aligned bottles at band midpoints. Measured effect of
  the generosity pass with a recipe-seeking floor bot: orders filled 0.1% → **26.5%**,
  bust rate 9.5% → 7.9%, win floor 0% → **15%** (old 25/26.7% figures were inflated by the
  Double Perfect derive bug and are not comparable).

## v3.1 — Bottles, brands, the market & the hi-bit art pass

- NEW `22_bottles_brands_market.md` — the curated 12-bottle base bar with brand identity
  papers (style/tier/origin/ABV/blurb), the end-of-night brand market, the licence-style
  patron ID (name/age/hometown, happiness gauge), preparation infrastructure
  (shaker/ice/rims, plumbing only), and the v2.5 hi-bit art direction (2x texel density in
  the same 640x360 layout).
- The Flavor numbers came off the bottles; brand-name shelf tags replace them. Flavor still
  feeds volume-weighted scoring and will surface in the bottle-info popup.
- `classic_bar.json` remains as data for tests and packs; the shipped shelf is
  `bottles/base_bar.json`.

## v3.0 — The pour pivot

- NEW `21_pour_system.md` — hold-to-pour, the glass, ratio recipes, bottle volume economy.
  The deck, rail and Restock are deleted; see the audit and casualty list in
  `PLAN_pour_pivot.md`.

## v2.0 — The emotion pivot

The core loop changed from "recognise a pattern, score points" to **read the person and serve
what they need**. Recipes were demoted to the craft layer, not deleted.

**New modules**
- NEW `19_emotion_mechanic.md` — the six emotions, tiered visibility, the ID, charges,
  resonance/Clean Serve/bust, and the information economy.
- NEW `20_regulars_and_week.md` — persistent regulars, drift, relationship, archetypes, and
  the weekly quota with its measured balance figures.
- NEW `../PLAN_emotion_pivot.md` — the phased delivery plan and the rulings behind it.
- NEW `../sim_report.md` — output of `LastCall → Simulate`, regenerated on demand.

**Rule changes**
- **Loss condition replaced.** Failing one order no longer ends the run; only a missed weekly
  satisfaction quota does. `03_run_structure_balance.md` §5.1's table now gates Tips only.
- **No-recipe mixes** still score 0, but their emotional charges pour at ×0.5 — this closes
  the open "high card fallback" question in `02_recipes_scoring.md`.
- **Mult gains a resonance block** applied after patron hand effects.
- **Content counts:** 64 patrons (+4), 17 tools (+1), 23 VIPs (+3), all on the information axis.

**Customer difficulty**
- NEW `DemandLevel` (Easygoing / Particular / Demanding): customers get harder to please as
  the run goes on. Moves the goalposts (how much movement counts as "strong"), never the
  ceiling (a Clean Serve is always worth 3). Shown on the ID.
- The quota curve flattened to 7/10/11/12 in response — the escalation now lives in the
  customers, and stacking both double-counted the difficulty.

**Rewritten**
- `12_tutorial_ftue.md` — rebuilt around what is actually opaque now: asking for ID, reading a
  RANGE, busting, the weekly gate, and demand. Busting is the top teaching priority.

**Stale, flagged in-place rather than rewritten**
- `08_ui_screens.md` is stale on the gameplay screen, current on menus and modals.

**Housekeeping**
- Unused assets, code and packages removed; the build was pointing at `SampleScene` and now
  points at `Main`.

## v1.1 — Design additions during M2
**⚠ M2-BLOCKING (implement before content lock):**
- `02_recipes_scoring.md`: recipe table expanded 11 → 14 (adds value-based and mono-Type recipes), explicit priority numbers, deterministic tie-break rule. **ScoringEngine recipe detection must use this table.**
- `03_run_structure_balance.md`: added §5.4 Regular's Favor tags (skip rewards), §5.5 VIP pool rules (no-repeat, gentle pool, reveal timing).
- `05_shop_economy.md`: starting money defined ($4), Patron duplicate rule, sell values clarified, new voucher "Bouncer" (VIP counterplay).

**Non-blocking (design locked now, implemented later):**
- NEW `12_tutorial_ftue.md` — first-time user experience (build in M4).
- NEW `13_stats_daily_achievements.md` — stats screens, daily challenge, achievements (M5/post-launch).
- NEW `14_art_bible.md` — visual identity spec (required before asset production).

## v1.0 — Initial 12-module GDD
