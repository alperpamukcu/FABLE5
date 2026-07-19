# LAST CALL — GDD Module 20: Regulars & the Week

> Companion to `19_emotion_mechanic.md`. Module 19 defines how you read one person;
> this module covers the cast that keeps coming back and the weekly gate that decides
> whether the run survives. Balance figures here are **measured** — see
> `Docs/sim_report.md` and the `LastCall → Simulate 300 Runs` menu item.

## 1. The week

A week is **2 Nights** (`QuotaTable.NightsPerWeek`), so 6 customers. An 8-Night run is
4 weeks. The quota gate fires when the last Night of a week closes, after the VIP.

**Fork B:** the weekly satisfaction quota is the run's *only* loss condition. Falling short
of one customer's score target costs the tips and nothing else — the customer finishes their
drink and leaves. Only a missed week ends the run. (`Docs/PLAN_emotion_pivot.md` D2/D3.)

## 2. Satisfaction

Each serve is worth 0–3 toward the week, decided by `ResonanceJudge.SatisfactionFor`:

| Serve | Satisfaction |
|---|---|
| Clean Serve (landed exactly on 0 or 100) | 3 |
| Strong progress toward the intent | 2 |
| Any progress | 1 |
| No movement, a wrong-way slip, or a bust | 0 |

"Strong progress" is not a constant — it depends on how hard the customer is to please. See §2.1.

Deliberately coarse. The player should feel "I got that one" or "I didn't", not compute a
decimal. A customer with 4 Mixes can bank several serves, so the practical ceiling per
customer is well above 3; measured average is **1.95**.

## 2.1 How hard they are to please

"Strong progress" is not a fixed number — it scales with the customer. This is the difficulty
axis that scales *the person*, as opposed to the quota, which scales what the week asks of you.

| Demand | Needs for 2 | Ignores below | Clean Serve |
|---|---|---|---|
| Easygoing | 15 | — | 3 |
| Particular | 22 | — | 3 |
| Demanding | 30 | 8 | 3 |

**Demand moves the goalposts, never the ceiling.** A Clean Serve is worth 3 to anyone, because
landing someone exactly where they asked cannot be improved on. What rises is how much
movement *feels* like something — and only the Demanding have a floor beneath which a serve is
worth nothing at all.

`demand = archetypeDisposition + nightStep`, clamped to the scale. Night step is 0 for Nights
1–3, +1 for 4–6, +2 for 7–8. Four archetypes are `Particular` by disposition (people carrying
something heavy) and four are `Easygoing`, so the run opens with a mix and closes with
everyone difficult:

| Night | Easygoing archetype | Particular archetype |
|---|---|---|
| 1–3 | Easygoing | Particular |
| 4–6 | Particular | Demanding |
| 7–8 | Demanding | Demanding |

It is always shown on the ID, colour-coded. Hidden difficulty is unfair; visible difficulty is
tension.

**Tuning history, because the first pass was badly wrong.** Stepping at Nights 3 and 6 with
thresholds 15/25/35 made every customer from Night 6 on Demanding, dropped satisfaction per
customer 2.10 → 1.74 and crushed the bot win rate from 29% to **4%**. Two things went wrong at
once: the demand curve was too steep, *and* the quota was still tuned against the old
satisfaction level, so the run was being escalated twice over. The fix was to soften both —
see §3.

Measured distribution at the current settings: 22% Easygoing, 41% Particular, 37% Demanding,
earning 1.90 / 2.00 / 1.94 satisfaction respectively. That near-flatness is the point: the bot
compensates by committing to bigger moves, so demand punishes *marginal* serves rather than
crushing everything. A player coasting on small nudges gets caught; one who commits gets
through.

## 3. The quota curve

| Week | Required | Bot pass rate |
|---|---|---|
| 1 | 7 | 94% |
| 2 | 10 | 85% |
| 3 | 11 | 72% |
| 4 | 12 | 43% |

Beyond week 4 (endless): `12 + 3 × (week − 4)`.

**Why it is nearly flat.** Because the escalation moved into §2.1. Customers get harder to
please as the run goes on, so later weeks earn *less* satisfaction from the same standard of
play — measured medians run 10 / 11 / 10 / 9, falling at the end even as the player improves.
A steeply rising quota on top of a rising demand curve double-counts the difficulty, and doing
both at once cost 25 points of win rate on its own. The week asks for roughly the same thing
throughout; **the people are what change.**

**History.** 6/9/12/14 measured 97/94/75/53 — defensible difficulty, wrong shape, half the run
a formality. 7/11/12/14 fixed the shape at a cost of 7 points (36.3% → 29.0%). The current
7/10/11/12 accompanies the demand axis and lands at **25.0%**, which is a little harder than
the pre-demand baseline — appropriate, since a difficulty axis was deliberately added.

If playtests say it is too punishing, week 4 is the dial: it is doing most of the killing at
43%, and the demand curve is already carrying the late-run tension on its own.

**How much to trust these numbers.** They come from a greedy one-ply bot that reads only the
ID and never shops. The *shape* comparison is sound — same bot, same seeds, one variable. The
absolute win rate is a **floor**: real players buy patrons and tools and think more than one
mix ahead. Do not quote 25% as a predicted player win rate.

## 4. Regulars

Customers persist for the whole run (`RegularsRegistry`). Once anyone exists, each new
customer is a returning face **55%** of the time, so the cast becomes familiar fast — which is
the point: persistent stats mean nothing if you never see the same person twice.

### 4.1 Drift

Between weeks, everyone moves (`RegularState.Drift`, stream `"drift"`):

```
newValue = value + round((baseline − value) × 0.35) + jitter(−5..+5)
```

Life keeps happening while they are not at your bar. A stat you dragged to 0 does not stay
at 0; it creeps back toward who that person usually is.

### 4.2 What you remember goes stale

Remembered tiers decay one step per drift: EXACT → RANGE, everything else → UNKNOWN.

**Ruling — memory is a floor, never a ceiling.** Each visit rolls a *fresh* tier assignment
(you are looking at them tonight), and the decayed memory is merged in by taking the clearer
of the two per stat. Knowing someone can only ever help.

> This was a real bug, caught by the simulator. An earlier build used the decayed memory
> *instead of* a fresh roll, and only refreshed it on a first meeting — so every regular got
> strictly blurrier each week until the whole cast was unreadable. Blind-read mixes ran at
> 52.6% and bust rate at 26.5%. Fixing the merge halved blind reads to 24.7% and lifted the
> bot win rate from 17% to 36%. `RegularsAndReadTests` pins it.

### 4.3 Relationship

| Rank | Satisfied visits | Effect |
|---|---|---|
| Stranger | 0 | — |
| Familiar | 1 | RANGE half-width −3 |
| Regular | 3 | RANGE half-width −6 |
| Confidant | 6 | RANGE half-width −9 |

Floored at 2, so a Confidant never gets a free EXACT. Only *satisfied* visits count; serving
someone badly keeps you strangers.

## 5. Archetypes

`Assets/Data/customers/archetypes.json`. An archetype is the shape of a person, not a person:
each regular is one roll inside its baseline bands, so two of the same archetype are
recognisably alike and still different.

| Id | Name | Leads with | Weight | Disposition |
|---|---|---|---|---|
| `after_shift` | Off the Late Shift | Fatigue | 4 | Easygoing |
| `wound_tight` | Wound Tight | Anxiety | 4 | Particular |
| `recently_ended` | Something Recently Ended | Heartbreak | 3 | Particular |
| `celebrating` | Celebrating Something | Excitement | 3 | Easygoing |
| `slow_burn` | Still Chewing On It | Anger | 3 | Particular |
| `nothing_much` | Nothing Much, Honestly | flat | 3 | Easygoing |
| `long_way_from_home` | A Long Way From Home | Sadness | 2 | Easygoing |
| `deadline` | Something Due Tomorrow | Anxiety + Fatigue | 2 | Particular |

Two content invariants are enforced by `EmotionContentTests`, because breaking either makes
intents unplayable rather than merely unbalanced:

- every emotion must be movable **both ways** by some card, and
- every archetype must leave room to work in both directions (no baseline pinned at 0 or 100).

Every emotion must also lead *some* archetype — an emotion no archetype leads never drives an
intent, which would make the cards that move it dead weight.

## 6. Open balance questions

- **The score target is starved.** Orders hit their score target only **27%** of the time,
  so tips are rare and the economy runs dry. `TargetTable.GreenStake` was tuned for the
  pre-pivot game, where mixing for points was the player's only job; now attention is split
  between points and the read. This wants its own pass and is deliberately untouched here —
  it affects the pre-pivot balance too.
- **Bust rate sits at 25.6%.** That is close to blackjack's own ~28%, which is the reference
  the design was aiming at, so it is being left alone. Worth re-checking against real players:
  a bot that never hedges busts differently than a person who does.
- **Charge multipliers are still derived** from each recipe's base Mult
  (`1 + 0.2 × (baseMult − 1)`, capped ×3). Nothing in the data argues for hand-authoring them
  yet, and one number per recipe is easier to balance than two.
