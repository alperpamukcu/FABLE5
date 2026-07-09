# LAST CALL — GDD Module: Ingredient Cards (Deck)

## 3. INGREDIENT CARDS (THE DECK)

The starting Cabinet contains **48 cards** across **6 Ingredient Types** (the "suits"):

| Type | Icon color | Count in starter deck | Role |
|---|---|---|---|
| **Spirit** | Amber | 12 | Backbone of every recipe |
| **Sour** | Green | 8 | Citrus, acids |
| **Sweet** | Pink | 8 | Syrups, liqueurs |
| **Bitter** | Red | 6 | Bitters, amari |
| **Bubbly** | Cyan | 8 | Soda, tonic, sparkling |
| **Garnish** | Gold | 6 | Fruit, herbs, decoration |

Each card has a **Flavor value** (the "rank"), from 1 to 11 in the starter deck. Distribution per type is a spread (e.g., Spirits: 2,3,4,5,6,7,8,9,10,11 plus two duplicates). Exact starter list lives in `data/decks/classic_bar.json`.

**Card anatomy (UI):** name, type icon + color band, Flavor value (big number, top-left), quality frame, optional Seal gem, flavor text (one line, only in inspect view).

### 3.1 Quality tiers (edition system, like Foil/Holo/Polychrome)
- **House Pour** (default): no bonus.
- **Top Shelf** (foil): +30 Flavor when scored.
- **Barrel-Aged** (holo): +8 Mult when scored.
- **Signature** (polychrome): ×1.5 Mult when scored.
- **Bootleg** (negative): grants +1 Patron slot, card itself scores 0 Flavor.

### 3.2 Seals (trigger modifiers)
- **Wax Seal (red):** retriggers this card's scoring once.
- **Ice Seal (blue):** if held in Rail (not played) at end of round, creates a Recipe Book of the last recipe mixed.
- **Salt Rim (gold):** earn $2 when this card is scored.
- **Smoke Seal (purple):** when restocked (discarded), creates a random Tool.

### 3.3 Ingredient enhancements (like card enhancements)
Applied by Tools: **Infused** (+40 Flavor), **Overproof** (+4 Mult), **Premium** (counts as any one Type — wild), **Frozen** (×2 Mult but 1-in-4 chance to shatter and be destroyed after scoring), **Doubled** (permanent copy stays in deck), **Golden** ($3 if held at end of round).

Rulings (locked 2026-07-09, matching implementation): Premium is wild **for recipe matching only** — it keeps its printed Type for Patron effects and VIP debuffs. Frozen shatters and Doubled copies trigger **only when the card actually scores** (not in voided mixes, not while debuffed). A Doubled card mints its copy **each time it scores**; the copy is a plain card (no enhancement), so copies never re-copy. Golden pays for sitting **unplayed on the rail** at the moment the customer is satisfied.

---
