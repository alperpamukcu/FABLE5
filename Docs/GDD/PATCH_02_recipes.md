# PATCH → apply to `02_recipes_scoring.md` (v1.1) — ⚠ M2-BLOCKING

## A. Replace the recipe table with this 14-recipe table (adds value-based & mono-Type recipes)

Priority = detection order. Higher priority wins. **Detection rule: evaluate ALL patterns the selection satisfies, then apply the single highest-priority match. Deterministic — no table-order ambiguity.**

| Priority | Recipe | Pattern | Base Flavor | Base Mult | Book upgrade |
|---|---|---|---|---|---|
| 1 | Neat Pour | 1 Spirit alone | 5 | 1 | +10 / +1 |
| 2 | Spritz | Spirit + Bubbly | 10 | 2 | +15 / +1 |
| 3 | Old Fashioned | Spirit + Sweet + Bitter | 20 | 2 | +20 / +1 |
| 4 | Highball | Spirit + Bubbly + Garnish | 25 | 3 | +20 / +1 |
| 5 | **House Special** *(new)* | any 3 cards with equal Flavor value | 30 | 3 | +25 / +2 |
| 6 | Sour | Spirit + Sour + Sweet | 30 | 3 | +25 / +2 |
| 7 | Martini | 2 Spirits + (Bitter or Garnish) | 35 | 4 | +25 / +2 |
| 8 | **Layered Pour** *(new)* | 4 cards with strictly ascending Flavor values (any types, order-independent) | 40 | 4 | +30 / +2 |
| 9 | Fizz | Spirit + Sour + Sweet + Bubbly | 45 | 4 | +30 / +2 |
| 10 | **Straight Booze** *(new)* | 4+ cards of the same Type | 50 | 5 | +30 / +3 |
| 11 | Negroni | 2 Spirits + Bitter + Garnish | 55 | 5 | +30 / +3 |
| 12 | Tiki | Spirit + Sour + Sweet + Garnish + any 5th | 70 | 6 | +35 / +3 |
| 13 | Perfect Serve | exactly one card of 5 different Types, incl. Spirit | 100 | 8 | +40 / +4 |
| 14 | Double Perfect (hidden) | Perfect Serve where all 5 Flavor values are equal | 160 | 14 | +50 / +5 |

## B. Design intent of the new recipes
- **House Special / Layered Pour** make Flavor VALUES a build axis (value manipulation, deck thinning) — previously dead design space. Tools that shift a card's value (+1/−1) become meaningful; add one such Tool ("Muddling Stick", value +1 to up to 2 cards) to the Tool pool.
- **Straight Booze** is the mono-Type ("flush") recipe the Patron archetype list already assumed. Type-conversion Tools (Citrus Press) now have a home.
- Recipe Books exist for all 14 recipes (3 new Book cards).

## C. Non-pattern cards
Unchanged: cards in the mix outside the matched pattern score no Flavor. For House Special / Layered Pour, the "pattern" is exactly the qualifying value set; other selected cards are non-pattern.
