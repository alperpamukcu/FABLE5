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
| Strong progress (≥ 20 toward the intent) | 2 |
| Any progress | 1 |
| No movement, a wrong-way slip, or a bust | 0 |

Deliberately coarse. The player should feel "I got that one" or "I didn't", not compute a
decimal. A customer with 4 Mixes can bank several serves, so the practical ceiling per
customer is well above 3; measured average is **2.10**.

## 3. The quota curve

| Week | Required | Bot pass rate |
|---|---|---|
| 1 | 7 | 93% |
| 2 | 11 | 80% |
| 3 | 12 | 76% |
| 4 | 14 | 52% |

Beyond week 4 (endless): `14 + 3 × (week − 4)`.

**How this was chosen.** The first pass was 6/9/12/14, which measured 97/94/75/53. The
end-to-end difficulty was defensible but the *shape* was wrong: half the run was a formality
and every decision that mattered lived in weeks 3–4. The current curve moves pressure earlier.

**The trade, stated honestly:** this cost real difficulty, not just shape. Bot win rate went
from 36.3% to 29.0%. The intent was a neutral redistribution; it isn't one. If playtests say
the run is too punishing, week 2 is the dial — dropping it 11 → 10 buys back roughly 4 points
and is the least damaging place to give ground, because weeks 3–4 are where the game is
supposed to bite.

**How much to trust these numbers.** They come from a greedy one-ply bot that reads only the
ID and never shops. The *shape* comparison is sound — same bot, same seeds, one variable. The
absolute win rate is a **floor**: real players buy patrons and tools and think more than one
mix ahead. Do not quote 29% as a predicted player win rate.

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

| Id | Name | Leads with | Weight |
|---|---|---|---|
| `after_shift` | Off the Late Shift | Fatigue | 4 |
| `wound_tight` | Wound Tight | Anxiety | 4 |
| `recently_ended` | Something Recently Ended | Heartbreak | 3 |
| `celebrating` | Celebrating Something | Excitement | 3 |
| `slow_burn` | Still Chewing On It | Anger | 3 |
| `nothing_much` | Nothing Much, Honestly | flat | 3 |
| `long_way_from_home` | A Long Way From Home | Sadness | 2 |
| `deadline` | Something Due Tomorrow | Anxiety + Fatigue | 2 |

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
