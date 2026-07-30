# LAST CALL — GDD Module 23: The Tycoon Loop (v4 pivot, 2026-07-22)

> **STATUS 2026-07-27 — CURRENT.**
> Owns the loop: the day, the till, the orders, rent and bankruptcy.

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

### 3.1 How they want it served (v5 P11, 2026-07-31)

An order is a drink **and a serving spec**: a subset of ice, a lemon twist, a salted or
sugared rim, extra shaken, and filled to the top. It is **stated** — printed on the licence
for the player to read — where the emotion layer's `FillPreference` is *inferred* from a
hidden read. Two different questions, deliberately not merged.

The spec is rolled from what the recipe can actually honour: a pint takes no garnish and
cannot be shaken, and a *built* drink never sees a shaker (21 §12). Asking for something the
recipe forbids would be an order nobody could fill — the one thing an order must never be.

Missing any part of the spec costs **tip, never the payment**: a Gin Fizz served without the
ice is still a Gin Fizz, and it is still paid for.

**Out of stock is not out of the question (C2).** Orders roll from the bar's *unlocked menu*,
not from its stock, so an unlocked drink whose bottle has run dry can still be asked for.
`TycoonRun.CanMake` says whether the shelf can answer; `DeclineOrder` is the honest reply. A
declined order pays nothing and marks the night — but being told straight is not the same as
being ignored, and it scores above a storm-off.

## 4. The service verdict

When a glass is served to a seat, `ServiceJudge` compares it to the order:

**Rewritten v5 P11 (2026-07-31).** The base price is deliberately low and is what a *correct*
drink earns; the tip is the whole reward for doing the job well. Menu price is now
`3 + (rank+1)/2` — about half the old `4 + rank` — because at the old ladder a
correct-but-careless serve earned nearly as much as a perfect one.

| Verdict | Condition | Base pay |
|---|---|---|
| **Exact** | served recipe == ordered recipe | menu price |
| **Close** | wrong drink, but its dominant type matches the order's dominant band type | menu price |
| **Wrong** | anything else | **the delivered drink's own menu price** (C1) — $0 if the glass is no recipe at all |
| **Refused** | the glass is under 35% full | **nothing**, whatever is in it |
| **Declined** | the bar said it could not make it (§3.1) | nothing |

A style-banded recipe (21 §12) names no ingredient *type*, so it has no "family" and there is
no Close for it: a drink specified down to its bottles is either right or wrong.

**The tip** is a share of the base price, at most equal to it — a perfect serve doubles the
drink. It is composed of three continuous scores, none of them a cliff:

```
tip     = basePrice x quality
quality = 0.45 x speed + 0.35 x craft + 0.20 x fill
speed   = 1 - waitFraction          (the whole patience, not a half-time window)
craft   = share of the serving spec delivered   (a pint: its head score, 21 s10.3)
fill    = 1 - shortfall/expected    (expected 0.80, or 0.95 if they asked for it full)
```

Patience now scales the tip **continuously**. It used to hit zero at half patience and stop
mattering there, which made the back half of every customer's wait free.

Satisfaction (0-1, feeds the day bar §6):
`Exact 0.75 / Close 0.5 / Wrong 0.05`, plus `0.20 x (craft - 0.5)`, plus
`0.12 x (fill - 0.5)`, minus `0.3 x waitFraction`, plus ambience. Storm-off = 0.

## 5. The extra order (the emotion layer's new job)

A **perfect serve** — Exact match **and** mood tip landed **and** served before 90% of
patience (widened from 75% 2026-07-22 — the read is the skill, timing is the speed tip's
job) — makes the customer **order another drink** (patience refreshed to 80%, new roll,
new full payment). Capped at **2 extra orders** per visit. This is deliberately reachable
("düşünüldüğü kadar zor olmamalı"): reading the ID and serving the right named drink is
the skill, not pixel-perfect ratios. The read still matters — you cannot earn the mood tip
or the extra order without knowing *who* you are serving.

**Who gets to order twice (v5 P11).** A **first-timer orders once** — the extra round is
what a returning face earns. The gate is otherwise unchanged: the exact drink, every part of
the serving spec they asked for, comfortably inside patience. A *plain* order still earns no
extra round: asking for nothing cannot be got right, and scoring the gate off the raw spec
score handed every plain drink a free round (the sim's refill bill went up half again). An
anonymous crowd — a run built with no archetypes — keeps the old behaviour, since "returning"
has no meaning when nobody is remembered.

## 5b. The night is open (v5 P12, C4/C5, 2026-07-31)

There is **no quota of customers**. The shift runs on a clock — 18:00 to 02:00, 95 seconds of
bar time — and people keep arriving until closing. How many get through the door is decided by
how fast the stools turn over, which is to say by how fast the player works. Closing time stops
new arrivals; it does not throw anyone out mid-drink, so the night ends when the last stool
empties.

The machinery was always here: a full row made the next arrival wait at the door rather than
queueing a backlog. The quota was what hid it. Measured across three service speeds (40 runs,
10 nights each):

| Seconds per drink | Served per night | Storm-offs |
|---|---|---|
| 5 | 11.6 | 0.2% |
| 9 | 9.3 | 7.9% |
| 15 | 7.8 | 26.5% |

**People balk.** Someone who walks in and counts three others still waiting on a drink thinks
better of it and keeps walking. Without that rule an open night hands a struggling bar an
unbounded queue of people to disappoint — the door admitted anyone the instant a stool freed,
however far behind the bar was, and a third of the night stormed off (31.4%, against 18.5%
under the quota). With it, storm-offs sit at 19.0% and the speed spread above roughly doubles:
serving faster does not only earn more, it *keeps the room willing to sit down*.

The day number survives underneath — rent, the ledger and the strike count all still count
days — it simply stops being what the player reads the night by.

## 6. Days, the ledger, and losing

- A day sends `CustomersPerDay = 8 + Day/2` customers (cap 14). The day ends when the last
  one has left.
- **Day end** shows the invoice (module 24 §7): income (payments + tips) vs expenses
  (refills at **$3 per capacity** — stock is a real cost of goods — market purchases,
  upgrades, and **rent = $15 + $5×Day**). Rent is what makes debt possible. *(v1 numbers;
  v0 was $8+$2×Day rent and $1 stock, which the sim showed was no pressure at all.)*
- **Purchases require cash (2026-07-22):** refills, brands, stools and ambience cannot
  be bought on credit — if the till cannot cover it, the buy is refused with a notice.
  Only **rent** can push the till below zero, which keeps debt something that happens
  *to* you, never a button you pressed.
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

**The stars are the reputation (v5 P12 / D3, 2026-07-31).** Every customer leaves **1–5 stars**
on the way out — storm-offs included, because a storm-off is a review too. Satisfaction 0 is one
star, not zero: the scale starts at one. The bar's **running average** across the whole run is
what the top corner shows and what the crowd reads, replacing the TONIGHT satisfaction bar. The
two old bars translate straight across: 0.75 satisfaction is 4.0 stars, 0.40 is 2.6.

Two things now key off the standing rather than off last night's mood:

- **The crowd** (§7 above). A single bad night no longer empties the room of money, and one
  good one no longer buys a rich crowd outright — which is what a reputation should mean.
- **The arrival rate.** A well-reviewed bar is busier: five stars closes the gaps between
  arrivals to 75% of neutral, one star stretches them to 130%. Three stars is exactly 1.0, so
  an unrated bar behaves as it always did.

Still to come (P18): the standing gating shop unlocks, and feeding tip and extra-order odds.

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
