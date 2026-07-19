# GDD Changelog

## v2.0 (current) — The emotion pivot

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
