# LAST CALL — GDD Module: Recipes & Scoring Engine Rules

> Source of truth for the scoring formula. The scoring engine must be a pure, unit-testable function.
>
> **v2.0 emotion pivot — recipes are demoted, not deleted.** They still supply base Flavor and
> base Mult exactly as described here, and every pattern below is unchanged. What changed:
>
> 1. **Recipes are now the craft layer.** Each recipe carries a `chargeMultiplier` — derived as
>    `1 + 0.2 × (baseMult − 1)`, capped ×3 — deciding how far an ingredient's *emotional
>    charges* carry. A well-made drink says more with the same bottles.
> 2. **A mix that matches no recipe still scores 0** — there is still no "high card" fallback —
>    **but its charges now pour at ×0.5.** The drink still reached the customer and still says
>    something, just at half volume. This closes what was previously an open design question.
> 3. **Mult gains a resonance block**, applied *after* patron hand effects: `+progress÷10` for
>    movement toward what the customer asked for, `+3` for landing a stat you could not see,
>    `×2` (or `×3` blind) for a Clean Serve, and `−2` on a bust with Mult floored at 1.
>
> `19_emotion_mechanic.md` §5–§6 owns those rules.

## 4. RECIPES (THE HAND RANKINGS)

A Mix of 1–5 ingredients is pattern-matched against this table (v1.1, 14 recipes — adds value-based & mono-Type recipes). Priority = detection order; higher priority wins. **Detection rule: evaluate ALL patterns the selection satisfies, then apply the single highest-priority match. Deterministic — no table-order ambiguity.** Base values are Level 1; **Recipe Books** level them up (see 7.2) and exist for all 14 recipes.

| Priority | Recipe | Pattern | Base Flavor | Base Mult | Level-up per Book |
|---|---|---|---|---|---|
| 1 | **Neat Pour** | 1 Spirit alone | 5 | 1 | +10 / +1 |
| 2 | **Spritz** | Spirit + Bubbly | 10 | 2 | +15 / +1 |
| 3 | **Old Fashioned** | Spirit + Sweet + Bitter | 20 | 2 | +20 / +1 |
| 4 | **Highball** | Spirit + Bubbly + Garnish | 25 | 3 | +20 / +1 |
| 5 | **House Special** | any 3 cards with equal Flavor value | 30 | 3 | +25 / +2 |
| 6 | **Sour** | Spirit + Sour + Sweet | 30 | 3 | +25 / +2 |
| 7 | **Martini** | 2 Spirits + (Bitter or Garnish) | 35 | 4 | +25 / +2 |
| 8 | **Layered Pour** | 4 cards with strictly ascending Flavor values (any types, order-independent) | 40 | 4 | +30 / +2 |
| 9 | **Fizz** | Spirit + Sour + Sweet + Bubbly | 45 | 4 | +30 / +2 |
| 10 | **Straight Booze** | 4+ cards of the same Type | 50 | 5 | +30 / +3 |
| 11 | **Negroni** | 2 Spirits + Bitter + Garnish | 55 | 5 | +30 / +3 |
| 12 | **Tiki** | Spirit + Sour + Sweet + Garnish + any 5th | 70 | 6 | +35 / +3 |
| 13 | **Perfect Serve** | Exactly one of 5 different Types incl. Spirit | 100 | 8 | +40 / +4 |
| 14 | **Double Perfect** (hidden, discoverable) | Perfect Serve where all 5 Flavor values are equal | 160 | 14 | +50 / +5 |

**Design intent of the value/mono-Type recipes (v1.1):** House Special and Layered Pour make Flavor VALUES a build axis (value manipulation, deck thinning) — the Muddling Stick Tool (+1 value to up to 2 cards) supports it. Straight Booze is the mono-Type ("flush") recipe the Patron archetype list assumes; type-conversion Tools (Citrus Press et al.) now have a home. For House Special / Layered Pour / Straight Booze, the "pattern" is exactly the qualifying group; other selected cards are non-pattern.

**Scoring formula (identical order of operations to Balatro):**
```
1. Base Flavor & Mult from Recipe level.
2. Add each scored card's Flavor value (left to right), apply per-card effects
   (quality, seals, enhancements, Patron "on card scored" triggers).
3. Apply "on hand scored" Patron effects in Patron slot order (left to right):
   +Flavor first as encountered, +Mult and ×Mult in slot order.
4. FINAL SCORE = Flavor × Mult. Add to customer progress.
```
Ingredients in the Mix that are not part of the matched pattern still get played but do **not** add Flavor (unless a Patron says otherwise — design space, like Splash Joker).

---
