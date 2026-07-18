# LAST CALL — GDD Module: Emotion Stats & "Read the Customer" (v1)

> **Source of truth for the core gameplay loop.** This module supersedes the parts of
> `02_recipes_scoring.md` that make recipe patterns the sole engine of Mult. Recipes are
> **not deleted** — they are demoted to the *craft* layer (how much you pour). Emotions are
> the *aim* layer (where you pour it). Score stays `Flavor × Mult`.
>
> **Tone guardrail (non-negotiable):** the fantasy is a bartender who *listens*. Drinks are
> the excuse for the conversation, never a cure. No drunkenness as reward, no "alcohol fixes
> feelings" framing. Reactions are short, stylized and icon-first (16 v2).
>
> **Structural reference:** *Papers, Please* — **mechanically and structurally, not thematically.**
> Each customer is a *case*: a document to read (the ID card), a discrepancy to spot (which stat
> actually rules them), a judgement call made under resource pressure, and a quota at the end of
> the shift. Regulars recur, remember, and carry the story. See §11.

## 1. The six emotions

Every customer carries six stats, each an integer `0–100`.

| Emotion | Ramp (14 v2 §3) | 8×8 icon | Type affinity (soft) |
|---|---|---|---|
| **Anger** | Vice Red | clenched fist | Bitter |
| **Sadness** | Club Blue | falling drop | Bubbly |
| **Fatigue** | Amber | drooping eyelid | Spirit |
| **Excitement** | Cyan | spark / burst | Bubbly |
| **Heartbreak** | Magenta | cracked heart | Sweet |
| **Anxiety** | Lime | jittery zigzag | Sour |

**Ruling — affinity is a heuristic, not a rule.** Charges are printed per *card*, not derived
from its Type. A Spirit may carry Anger; a Garnish may carry Excitement. The affinity column
only says which Type *tends* to carry which emotion, so players build a learnable intuition
without the puzzle collapsing into "read the suit". Five of the six emotion ramps intentionally
echo an ingredient Type ramp (Sadness/Club Blue is emotion-only; Garnish/Cream stays neutral) —
the echo is the teaching aid.

## 2. Intent and the dominant emotion

Each customer arrives with a visible **Intent** on the order ticket:

- **EXTINGUISH** ("wants to forget") — drive the dominant emotion toward **0**.
- **FUEL** ("wants to feel it") — drive the dominant emotion toward **100**.

**Ruling — the dominant emotion is the highest-valued stat**, ties broken by the fixed order
above (Anger → Sadness → Fatigue → Excitement → Heartbreak → Anxiety). This rule is *public*;
only the values are hidden. That is the whole game: the rules are known, the hand is not.

## 3. Tiered visibility

Hovering a customer opens the **ID card**. Each of the six stats sits in one of three tiers:

| Tier | Count | Shown as |
|---|---|---|
| **EXACT** | 1 | precise bar + number (`Anger 62`) |
| **RANGE** | 3 | bar with a dithered uncertain segment (`Sadness 55–75`) |
| **UNKNOWN** | 2 | `?`, empty bar frame |

**Generation (seeded, `RunRng` stream `"read"`):** shuffle the six emotions; index 0 → EXACT,
1–3 → RANGE, 4–5 → UNKNOWN. Uniform — the dominant stat lands in EXACT roughly 1 in 6, and
that gift is fine.

**Range half-width by Night** (wider = harder reads as the run escalates):

| Nights | Half-width | Displayed span |
|---|---|---|
| 1–2 | ±8 | 16 wide |
| 3–5 | ±12 | 24 wide |
| 6–8 | ±16 | 32 wide |

The span is centred on the true value then clamped to `0–100`, so a true 5 with ±12 shows
`0–17` — clamping itself leaks information, which is a fair reward for edge values.

## 4. Ingredient charges

Every ingredient card prints **1–2 emotion charges** as `[icon][±value]`, always visible.
Magnitude is banded by the card's Flavor value, so **high-Flavor cards score more craft but
swing emotions harder** — the central risk/reward tension.

| Flavor | Tier | Primary charge | Secondary charge (≈60% of cards) |
|---|---|---|---|
| 1–3 | Light | ±4 … ±8 | ±2 … ±4 |
| 4–7 | Standard | ±9 … ±15 | ±4 … ±7 |
| 8–11 | Heavy | ±16 … ±24 | ±6 … ±10 |

Light cards are precision instruments for landing an exact 0/100; Heavy cards are blunt power.
A deck of only Heavies cannot land perfect serves — that is the intended deck-building pressure.

Example print: `Rye Whiskey · Spirit · Flavor 7 · [Anger −14] [Fatigue +6]`.

## 5. Recipes as the charge multiplier

The matched recipe (02 §4, all 14 unchanged) is the **delivery vehicle**: it decides *how much*
of the summed charge actually reaches the customer.

`chargeMultiplier = 1 + 0.2 × (recipeBaseMult − 1)`, capped at **×3.0**, stored per recipe in
data so it is tunable independently.

| Recipe base Mult | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 14 |
|---|---|---|---|---|---|---|---|---|
| Charge ×  | 1.0 | 1.2 | 1.4 | 1.6 | 1.8 | 2.0 | 2.4 | 3.0 (cap) |

**Ruling — applied charge is rounded to the nearest integer** *after* multiplying:
`applied = round(rawSum × chargeMultiplier)`. Without this, exact landings would be arithmetically
impossible and the perfect-serve fantasy dies. This rounding is the player's puzzle surface.

**Ruling — a no-recipe mix still pours, at ×0.5.** It scores **0 Flavor** (02 unchanged) but its
charges still land. A sloppy pour becomes a legitimate *positioning* play — and can still bust.
This resolves the open question flagged in `CLAUDE.md` ("a mix that matches no recipe scores 0 by
design — revisit after playtests"): no-recipe mixes now have a purpose without paying craft score.

## 6. Scoring: resonance, bursts and busts

Score is still `Flavor × Mult`. **Flavor is untouched** (recipe base + card values + quality/
enhancements/patrons). Mult gains a new dominant term:

```
Mult = recipeBaseMult(level)          // craft floor — unchanged
     + resonanceMult                   // NEW, the main engine
     + luckyRead                       // NEW, blind-hit bonus
     + patron/card AddMult              // unchanged
   ×  patron/card MultMult              // unchanged
   ×  serveBurst                        // NEW, the perfect-landing moment
```

**Progress** — movement of the *dominant* emotion in the intended direction, measured after
clamping: `EXTINGUISH → max(0, before − after)`, `FUEL → max(0, after − before)`.

**Resonance:** `resonanceMult = progress ÷ 10` (one decimal). Moving 24 points = **+2.4 Mult**.
Bounded by design — the stat is 0–100, so resonance alone cannot carry Night 8; late-run scaling
still comes from patrons, recipe levels and the burst.

**Clean Serve burst** (landing the dominant *exactly* on its goal — 0 for extinguish, 100 for fuel):

| Situation | Burst |
|---|---|
| Dominant was EXACT or RANGE at commit | **×2 Mult** |
| Dominant was UNKNOWN at commit ("blind") | **×3 Mult** |

> **Naming ruling:** this is the **Clean Serve**, *never* "perfect serve" — `Perfect Serve` is
> already a recipe name (02 §4) and the patron *The Collector* keys off it. Two meanings for one
> phrase would be a live content bug.

**Lucky read:** if the dominant emotion was UNKNOWN at commit and the mix moved it the right way
at all, add **+3 Mult** flat. Gambling stays a real playstyle, not a punished one.

**Bust rules** (the blackjack tension):

| Case | Condition | Result |
|---|---|---|
| **Overshoot bust** | The applied charge would push the dominant past its goal (below 0 / above 100) | Stat clamps; **resonance + burst forfeited for this mix**; negative reaction icon |
| **Wrong-way bust** | Dominant moved *away* from the goal by **more than 10** | Resonance forfeited; negative reaction icon |
| **Drift** (no bust) | Dominant moved wrong by **≤10** | Resonance simply 0, no reaction |

**Ruling — busts do not persist.** A busted emotion can score resonance again on the next mix.
The cost is the forfeited mix, not a locked-out customer; permanent lockout would make a single
misread unrecoverable and punish exactly the exploration the design wants to encourage.

## 7. Worked examples

### 7.1 Safe optimisation (reading an EXACT bar)

*The Closer* — Intent **EXTINGUISH**. Truth: Anger 62, Sadness 30, Fatigue 44, Excitement 12,
Heartbreak 20, Anxiety 35 → dominant **Anger**. Visibility: Anger **EXACT 62**, Sadness/Fatigue/
Anxiety RANGE, Excitement/Heartbreak UNKNOWN. Anger is visibly highest → confident read.

Mix — **Old Fashioned** (Spirit + Sweet + Bitter), charge ×1.2:

| Card | Flavor | Charges |
|---|---|---|
| Rye Whiskey | 7 | Anger −14, Fatigue +6 |
| Demerara Syrup | 9 | Heartbreak −8 |
| Angostura | 3 | Anger −6, Anxiety +4 |

- Flavor = 20 (recipe) + 7 + 9 + 3 = **39**
- Raw Anger = −20 → applied = `round(−20 × 1.2)` = **−24** → Anger 62 → **38**
- Progress 24 → resonance **+2.4**; Mult = 2 (recipe) + 2.4 = **4.4**
- **Score = 39 × 4.4 = 172**

### 7.2 The Clean Serve (exact landing)

Same customer, Anger now **38**. The player wants to land it on 0.
**Spritz** (Spirit + Bubbly), charge ×1.2 → needs raw ≈ 38 ÷ 1.2 = 31.7.

Picking raw **−32**: `round(−32 × 1.2) = round(−38.4) = −38` → Anger **exactly 0**. ✅
(Raw −33 would give `round(−39.6) = −40` → overshoot → **bust**. That one card is the whole game.)

- Flavor = 10 (recipe) + 6 + 8 = **24**
- Progress 38 → resonance **+3.8**; Mult = 2 + 3.8 = 5.8
- Dominant was EXACT → burst **×2** → Mult = **11.6**
- **Score = 24 × 11.6 = 278** — a small-Flavor mix out-scores a big sloppy one. That is the moment.

### 7.3 Misread and bust (gambling on UNKNOWN)

Intent **FUEL**. Truth: Excitement 78 (dominant) but **UNKNOWN**; visible: Anger EXACT 20,
Sadness 30–46, Fatigue 15–31, Heartbreak 40–56, Anxiety ?.

- **Misread:** the player fuels Heartbreak (highest visible). Heartbreak is not dominant → progress
  on the dominant is 0 → resonance 0, no bust. Score = Flavor × recipe Mult only. The craft still
  pays; the engine does not fire.
- **Bust:** fueling Excitement with raw +30 at ×1.4 → `round(42) = +42` → 78 + 42 = 120 → clamps to
  100 but **overshoots** → resonance and burst forfeited.
- **Blind Clean Serve:** raw **+16** at ×1.4 → `round(22.4) = +22` → 78 + 22 = **exactly 100**.
  Resonance +2.2, lucky read +3, burst ×3 → Mult = `(recipeMult + 2.2 + 3) × 3`. With a Highball
  (base 3): `(3 + 2.2 + 3) × 3 = 24.6`. Flavor 25 + cards ≈ 40 → **Score ≈ 984**. The jackpot the
  gambling line exists for.

## 8. Information economy

Money now buys **power or knowledge**. "Unknown" must always be *solvable*, never RNG punishment.

### 8.1 Chat (the free action)

- Up to **2 Chats per customer**; each **costs 1 Restock**.
- Reveals one stat of the player's choice, upgrading it **one tier**: UNKNOWN → RANGE → EXACT.
- Rationale: Restocks (default 3) are the existing tempo resource. Spending redraws on
  information is the cleanest possible expression of "knowledge vs. power" and needs no new
  currency. Two chats + one redraw is a real, tight budget.

### 8.2 Patrons (jokers)

| Patron | Rarity | Effect |
|---|---|---|
| **The Gossip** | Uncommon | All RANGE half-widths narrowed 50% |
| **The Confidant** | Uncommon | +1 Chat per customer |
| **The Empath** | Rare | Overshoot no longer busts — it counts as landing on the boundary (no burst) |
| **The Regular's Memory** | Rare | An archetype seen earlier this run arrives fully revealed |

### 8.3 Tools

| Tool | Effect |
|---|---|
| **Eavesdrop** | Single use: reveal one UNKNOWN stat straight to EXACT |

### 8.4 VIP rules on the information axis (new rule kinds)

| VIP | Rule |
|---|---|
| **Poker Face** | No tiers at all — every stat starts UNKNOWN (Chat still works) |
| **The Open Book** | All six stats EXACT, but target ×1.5 |
| **The Liar** | One RANGE is deliberately offset; which one is not shown |

## 9. Data model (all data-driven)

**Ingredient card** (`decks/classic_bar.json`) gains:
```json
"charges": [ { "emotion": "Anger", "value": -14 }, { "emotion": "Fatigue", "value": 6 } ]
```

**Recipe** (`recipes/recipes.json`) gains `"chargeMultiplier": 1.2` (defaults to the §5 formula).

**Customer archetype** — new `Assets/Data/customers/archetypes.json`. Archetypes store stat
**ranges**, not fixed values; the encounter rolls actual values from the seeded `"customer"`
stream so archetypes stay replayable rather than memorised:
```json
{ "id": "the_closer", "name": "The Closer", "intent": "Extinguish",
  "statRanges": { "Anger": [55, 75], "Sadness": [20, 40], "Fatigue": [35, 55],
                  "Excitement": [5, 20], "Heartbreak": [10, 30], "Anxiety": [25, 45] },
  "weight": 10, "nights": [1, 8] }
```

## 10. Regulars, persistence and the week

### 10.1 Emotion stats persist (locked)

A customer's six stats are **run-level state, not per-visit state**. When *The Closer* comes back
on Night 4, they arrive at the values you left them on Night 1 — plus whatever drift their
archetype applies between visits (see 10.2). This is what turns the deduction puzzle into a
*relationship*: the read you paid for on Night 1 is an asset you still own on Night 4.

**Data model consequence:** encounters need a run-level registry, not a value on `CustomerOrder`.

```
RunController
  └── RegularsRegistry            // run-level, seeded, save-safe
        └── RegularState (per archetype id)
              ├── EmotionStats stats          // persists across nights
              ├── VisibilityTier[] known      // what the player has learned, persists
              ├── int visits, satisfiedCount
              └── Relationship relationship   // see 10.3
```

`CustomerOrder` becomes a *view* over a `RegularState` for one visit (threshold, intent, mixes),
not the owner of the stats.

### 10.2 Between-visit drift

Without drift, a solved regular stays solved forever and the puzzle dies. Between visits each
stat drifts toward its archetype's baseline:

`newValue = value + round((baseline − value) × 0.35)`, plus a seeded jitter of ±5 (stream `"drift"`).

At 0.35 the memory of your last serve clearly survives (a stat you crushed from 80 → 0 comes back
around 28, not 80) while never being perfectly predictable. **Known tiers decay by one step per
visit** (EXACT → RANGE → UNKNOWN) unless refreshed — people change, and information goes stale.
*The Regular's Memory* patron cancels this decay.

### 10.3 Satisfaction is the win condition

`Flavor × Mult` no longer produces an abstract "score" — it produces **Satisfaction** for that
serve. Everything about the maths in §6 is unchanged; only its meaning and where it is gated is.

| Layer | Gate |
|---|---|
| **Per serve** | Satisfaction points = `Flavor × Mult` |
| **Per customer** | Satisfaction ≥ their **threshold** → they leave happy (tip, relationship up). Below → unhappy (relationship down) |
| **Per week (the run)** | Total Satisfaction across the week must clear the **Week Quota** |

Reading a customer correctly is therefore not a scoring bonus — it is *the* way to clear quota,
exactly as the brief states: *serve to their stats and they are more satisfied*.

**Relationship** is a per-regular track (`Stranger → Familiar → Regular → Confidant`) that rises
when you send them away happy. Higher relationship narrows their displayed RANGE tiers for free —
you get to *know* people. This is the narrative engine and the long-term reward for good reads.

## 11. Tuning anchors

- A **confident read + solid recipe** should roughly **double** a mix versus ignoring emotions.
- A **Clean Serve** should be worth ≈ **3–4 ordinary mixes** — memorable, not mandatory.
- A **blind Clean Serve** is the run's best single moment (×3) but should occur in well under
  10% of mixes; if the sim harness shows more, cut the lucky-read bonus first, the burst second.
- Resonance is deliberately **capped** (max +10) so late-Night scaling stays with patrons and
  recipe levels — emotions add *decision quality*, not raw exponential growth.

---
