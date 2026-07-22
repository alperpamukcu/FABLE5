# GDD Changelog

## v4.0 (current) — THE TYCOON PIVOT (in progress)

- `22 §4 / 23 §8` **inventory economy (2026-07-23):** the menu is your stock now. You open
  with a **bare well** — two spirits and the essential mixers — and **grow the shelf by
  buying new stock** at the end of each night: the market lists both **new bottles** (styles
  you lack, `+`, which are *added* to the shelf so their drinks become makeable) and
  **upgrades** (`↑`, better brands of what you have). A just-bought bottle **flashes ★ NEW**
  on the menu the next day; a depleted bottle reads **OUT** and can't be poured until the
  restock. New-stock prices scale by type and tier (`Market.StockPrice`). *(Still owed from
  the brief: a drink pays a premium for the pricier alcohol used — a small follow-up.)*

- `23 §4–5` **emotion → recipe pivot (2026-07-22):** the emotion layer no longer drives play.
  What you **read off the licence** is now the **drink recipe** (its ingredient bands) and the
  **garnishes the customer wants** (on ice, a twist, a rim) — the ID card shows these where the
  six moods used to be, and the seat tag hints "WANTS EXTRAS". Satisfaction keys off the drink
  match, the garnish craft, and speed; getting every asked-for garnish on an exact, fast serve
  earns another round. Garnishes lift satisfaction (and so reputation and the crowd's wealth)
  rather than paying a direct tip, keeping the till predictable. The emotion data structures
  stay dormant under the hood (customer generation still uses them); `MoodTipLanded` → `CraftLanded`.

- `23 §4` **balance + fixes pass (2026-07-22):** the **wrong drink now pays nothing** (was
  half) and sours the room; the **speed tip scales** up to $4 for the right drink served
  fast, fading to nothing across a wider window (a slow serve just misses the tip). The
  **serve reaction itemises the bill** — the drink's price in amber over the tip in green.
  The finished glass on the counter now shows the drink **as it was actually built** — its
  real fill level and blended colour, not a fixed amount. Ice/lemon/salt/sugar dropped in
  now **fall and dissolve the instant they touch the drink** (they no longer float inside).
  The shaker **stays where you set it down** after a shake (no teleport home). And every
  canvas now **matches height**, so the layout no longer drifts when the window aspect
  changes. *(Still open from the same brief: particle fluid, emotion→recipe pivot, inventory
  economy, real character animations — see PLAN.)*

- `24 §3.5` **feel pass (2026-07-22):** the pour is now a **metaball fluid** (`MetaballFluid`
  + `Shaders/MetaballLiquid.shader`) — droplets melt into one connected mass and into the
  pool instead of reading as separate balls, the liquid gains volume and takes the glass
  shape, and it lands with an organic splash; the fill is slower and more deliberate. The
  pooled **surface is a shallow-water height-field** — waves travel, reflect off the glass
  walls and settle, with a lateral slosh over the top. **Ice and lemon float inside the
  shaker** (`ShakerSolids`), bobbing at the surface and flung about when you shake. **You
  grab the shaker itself and throw it around** to shake (the hold-pad is gone), and the
  meter continues from what's already been shaken. The finished drink now appears as a
  **glass on the counter that you drag onto a customer to serve** (a heavy, springy AAA-feel
  carry; clicking a customer only reads them). The menu drops its ICE/LEMON/SALT/SUGAR and
  SHAKE buttons (those are hands-on now), and the till moves to the counter's edge. Dragged
  preparations have weight — the grip springs after the cursor with overshoot while the
  piece swings from it (`Pendulum`). Still procedural
  placeholder art, still cosmetic-only over the deterministic pour. Also: the pre-menu stage
  dressing (top-left pour-glass HUD, on-counter bottle rail, garnish jars) is retired, and
  the cash register opens a **ledger of past days** (`24 §7`).
- `24 §4` **stable stools & leave animation (2026-07-22):** customers now **keep their stool**
  until they leave — busts no longer shift or morph when the queue compacts (the HUD maps
  visits to fixed stools). When a patron leaves they play an exit: a served one **sinks off
  the stool and fades**, a stormed-off one **shakes then slides out**. Completes the lifecycle:
  walk in → sit → read/serve → react → leave.
- `24 §4` **serve reactions & storm-off notice (2026-07-22):** serving a customer now floats
  a reaction from their seat — green "PERFECT!/THANKS." with the payment, a gold "★ ANOTHER
  ROUND!" when they reorder, red "NOT WHAT I ASKED" on the wrong drink — with a little pop.
  A patron whose patience runs out raises a red "A CUSTOMER STORMED OFF" notice.
- `24 §4` **customers at the counter (2026-07-22):** the bottom seat strip is retired —
  customers now sit at the bar as head-and-shoulders portrait busts with a floating order tag
  above the head, souring red over the last third of patience, sliding in and fading up as
  they arrive. An interim step toward the P8 physical-customer characters, using art that
  already exists.
- NEW `23_tycoon_loop.md` — **the loop is a business now**: up to 6 simultaneous seated
  customers ordering named drinks, patience clocks, price+tip payments (mood tips from the
  emotion read), extra orders on perfect serves, days of 8+ customers, a day-end invoice,
  rent, and **3 consecutive red days = bankruptcy** as the only loss. Reputation bar sets
  tomorrow's crowd wealth. Quota/score loop retired (demolition scheduled, PLAN P7).
- NEW `24_service_flow_presentation.md` — bottles move off-stage into a counter **menu**;
  drinks are built in a **shaker focus stage** (hold-pour → preparations → shake by
  mouse), served by **aimed pour** into a glass (spilling returns as an aiming skill), and
  delivered to a chosen seat. Art direction v3: Dave-the-Diver-level density/animation
  floor, whole-set replacement (P8), scripted tutorial shift.
- NEW `PLAN_tycoon_pivot.md` — P0…P9 phases with gates; old loop remains playable until
  P3, deleted at P7.
- v1–v3 modules stay as subsystem specs where referenced by 23/24; banners mark the rest.

## v3.3 — No spills, the house pour & a legible shelf

- **The glass cannot overflow** (21 §3, supersedes v3.2's "merciful spill"): pours stop at
  the brim, the bottle keeps what the glass cannot take, and the hold auto-releases. A
  heavy hand costs precision, not the drink. `Spills` now counts binned drinks.
- **The house pour** (21 §9): a no-recipe drink pays its volume-weighted Flavor at ×1
  instead of a hard zero; recipes keep paying Flavor × Mult, an order of magnitude more.
  Charges stay at ×0.5 without a recipe — money changed, feelings didn't.
- The recipe book gained a shelf legend: each type word the bands use (SPIRIT, SOUR…)
  mapped to the actual bottles on tonight's shelf in their tag colours — the "which bottle
  is a Spirit?" gap between book and back bar is closed.
- Back-bar rows re-spaced (upper 246 / lower 158) so the lower row's bottle caps stop
  overlapping the upper row's name tags.

## v3.2 — The customer POV, style identity & the merciful spill

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
