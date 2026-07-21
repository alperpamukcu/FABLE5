# LAST CALL — GDD Module 23: The Tycoon Loop (v4 pivot, 2026-07-22)

> **This module owns the game loop.** It supersedes the quota/score loop of modules 19–21
> wherever they disagree; module 24 owns the service flow and presentation. The player runs
> a bar like a business now — *Dealer's Life* pacing, *Dave the Diver* energy: customers
> stream in, order drinks, pay, and leave; the till is the score and the ledger is the
> only judge.

## 0. What changes, what stays

| System | Fate |
|---|---|
| Weekly satisfaction quota, score targets, drinks-per-customer | **Gone.** Money is the only win/lose axis. |
| Single customer per round | **Gone.** Up to `Seats` customers sit at the bar simultaneously. |
| Emotions, tiered reads, intent, the licence ID | **Stay** — they move from "the score" to **tips and extra orders** (§5). |
| Regulars, drift, relationship, archetypes | **Stay.** The faces at the bar are still persistent people. |
| Shelf, bottles, brands, refills, the market | **Stay**, expanded (§7). |
| Recipes as ratio bands, house pour | **Stay** — recipes are now *what customers ask for by name*. |
| ScoringEngine (Flavor × Mult), score-based patrons/tools/packs/vouchers | **Retire** on the demolition schedule (PLAN_tycoon_pivot §Demolition). Flavor survives as a price input. |
| VIPs | **Parked.** Return later as special guests with wallet rules, not rule cards. |

## 1. The bar floor

- The bar has **`Seats` stools** (start **4**, upgrade to **6**). Each seat holds one
  `CustomerVisit`.
- Customers **arrive over time** while the day runs: next arrival after
  `ArrivalGap = max(6, 12 − 0.5×Day)` seconds ± 30% jitter (stream `"arrivals"`), if a
  stool is free and the day still has customers left to send. *(v1, 2026-07-22 — v0's
  gentler pacing let a floor bot bank $5k with zero bankruptcies.)*
- A visit's life: **Arrive → Order → Wait → (Served → maybe order again) → Pay → Leave**,
  or **Storm off** when patience runs out.
- Every visit shows two gauges (module 24 §5): the **satisfaction bar** and the
  **patience clock** — a clock icon counting down beside the bar.

## 2. Patience

- `Patience = max(22, 50 − 2.5×Day)` seconds ± 20% jitter (stream `"patience"`), ticking
  only while the customer waits for a drink. *(v1 — tightened with the same sim pass.)*
- Patience hitting zero = **storm-off**: no payment, satisfaction 0 for the day average,
  the stool frees up.
- Serving resets nothing retroactively — the *wait fraction* used by tipping (§4) is
  locked at the moment of serving.
- An extra order (§5) refills patience to **80%** of the original roll.

## 3. Orders and the menu

- A customer orders a **named drink** from what the bar can actually make: the pourable
  recipes (ratio bands) plus the straight pours. The order is visible on the seat
  (speech bubble, module 24).
- **Menu price** (v0): `price = $4 + $1×rank` for recipes; straight pours `$3 + Flavor/2`.
  Quality/tier-2 brands raise the price of drinks that use them (**+$1 per tier step** of
  the most expensive bottle involved) — buying better bottles is buying higher menu prices.
  That is the tycoon engine: earn → upgrade → charge more → earn more.
- **Order difficulty scales with the day** (stream `"orders"`): the roll pool is the
  `3 + Day` lowest-rank pourable recipes, so day 1 asks for Neat Pours and Spritzes,
  day 10 asks for Negronis and Tikis.

## 4. The service verdict

When a glass is served to a seat, `ServiceJudge` compares it to the order:

| Verdict | Condition | Base pay | Anger |
|---|---|---|---|
| **Exact** | served recipe == ordered recipe | full price | none |
| **Close** | wrong drink, but its dominant type matches the order's dominant band type | full price | mild ("not what I asked, but fine") |
| **Wrong** | anything else | **half price** | real |

Tips stack on top of base pay:

| Tip | Amount | Condition |
|---|---|---|
| Mood tip | **+$3…5** | the drink's charges moved the customer's intent stat the right way by ≥ 8 (scales with movement) |
| Speed tip | **+$1** | served within the first 35% of their patience |

Satisfaction (0–1, feeds the day bar §6): `Exact 0.9 / Close 0.6 / Wrong 0.2`, minus
`0.3 × waitFraction`, plus `0.1` if the mood tip landed. Storm-off = 0.

## 5. The extra order (the emotion layer's new job)

A **perfect serve** — Exact match **and** mood tip landed **and** served before 90% of
patience (widened from 75% 2026-07-22 — the read is the skill, timing is the speed tip's
job) — makes the customer **order another drink** (patience refreshed to 80%, new roll,
new full payment). Capped at **2 extra orders** per visit. This is deliberately reachable
("düşünüldüğü kadar zor olmamalı"): reading the ID and serving the right named drink is
the skill, not pixel-perfect ratios. The read still matters — you cannot earn the mood tip
or the extra order without knowing *who* you are serving.

## 6. Days, the ledger, and losing

- A day sends `CustomersPerDay = 8 + Day/2` customers (cap 14). The day ends when the last
  one has left.
- **Day end** shows the invoice (module 24 §7): income (payments + tips) vs expenses
  (refills at **$3 per capacity** — stock is a real cost of goods — market purchases,
  upgrades, and **rent = $15 + $5×Day**). Rent is what makes debt possible. *(v1 numbers;
  v0 was $8+$2×Day rent and $1 stock, which the sim showed was no pressure at all.)*
- **Losing:** close **3 consecutive days with the till below zero** and the bar closes —
  full run reset, roguelite style. In debt means in debt: a rich bar can eat a losing day
  without the clock starting; one close back above water wipes the strikes. *(Clarified
  2026-07-22 — the first draft struck on net-negative days, which killed bars holding
  $700 cash. The user's rule is about debt, and now so is the code.)*
- Day end is also when the **market** opens (§7).

## 7. Reputation and the crowd

- The **daily satisfaction bar** (average of every visit's satisfaction, storm-offs
  included) lives at the top of the screen all day.
- It decides tomorrow's crowd: avg ≥ 0.75 → **High rollers** (prices ×1.25, mood tips
  +$2); 0.4–0.75 → **Regulars**; < 0.4 → **Broke crowd** (prices ×0.75, no speed tips).
  A good bar attracts customers worth serving well — reputation compounds like the shelf.

## 8. The market and upgrades (day-end shop)

Rotating random offers (stream `"shop"`), a few per night:
- **Better bottles** (tier 2/3 brands — existing Market, §3 price effect makes them earn).
- **Bar upgrades with visual counterparts** (module 24 §6): stools 4→5→6, glassware
  (capacity/looks), the counter, the back wall, **the musician** (background performer;
  +satisfaction ambience bonus). Every purchase changes the main scene — progress you can
  see.

## 9. Difficulty & the forever game

No final day. Days scale: more customers, shorter patience, higher-rank orders, higher
rent. The run ends only by bankruptcy (§6) — the game is "how long and how rich", with
the ledger history as the score.

## 10. Balance v0 (all numbers above are starting stakes)

Tuned by the sim (PLAN P3, 2026-07-22, two iterations): a 9s-per-drink floor bot now runs
$132 income against $125 expenses, day 1 always green, red days climbing from day 11 to
35% by day 15 — an unimproved bar slowly sinks, which is the whole tycoon argument.
Storm-offs 22% (floor bot; players triage by the clock), extra orders 14% of serves.
Numbers live in `TycoonConfig` (code) and this module — change both.
