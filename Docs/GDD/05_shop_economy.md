# LAST CALL — GDD Module: Back Room Shop & Economy

## 7. THE BACK ROOM (SHOP)

Opens after every satisfied customer. Layout: **2 card slots** (Patrons/Tools/Ingredient packs randomly), **1 Voucher slot** (permanent run upgrade, refreshes each Night), **2 Booster Pack slots**. **Reroll** button: $5, +$1 per reroll this visit.

### 7.1 Prices (baseline)
- Common Patron $4–5, Uncommon $6–7, Rare $8–9, Legendary $20 (only via special means).
- Tools $3. Recipe Books $4. Ingredient card singles $1–3.
- Booster packs: Cellar Pack (ingredients, $4), Distiller Pack (Recipe Books, $4), Bar Kit (Tools, $4), Regulars Pack (choose 1 of 2/4 Patrons, $6), Speakeasy Pack (rare hybrid, $8).

### 7.2 Recipe Books (Planet card equivalent)
Each Book targets one Recipe and raises its level permanently for the run (values in table §4). Named after famous bar bibles: e.g., *"The Savoy Page"* (Sour), *"Tiki Codex"* (Tiki), etc.

### 7.3 Tools (Tarot equivalent — single-use consumables, max held: 2)
15 at launch. Examples: **Muddler** (Infuse up to 2 cards), **Jigger** (turn a card Overproof), **Ice Pick** (destroy up to 2 cards), **Citrus Press** (convert up to 3 cards to Sour), **Bar Spoon** (copy a card), **Coupe Glass** (turn a card Premium/wild), **Cocktail Umbrella** (add Signature quality to a random Patron), **Bottle Opener** (create the last Tool used), **Tab Ledger** (double your money, max +$20).

Rulings (locked 2026-07-09, matching implementation): **Cocktail Umbrella** targets a chosen rail *card* and makes it Signature quality (×1.5 Mult when scored) — patrons have no quality track, so the original "random Patron" wording was a spec bug. **Bottle Opener** recreates the last Tool *used* this run (the Opener itself never counts as last used; using it with no tool history is an error the UI surfaces). **Tab Ledger** and **Bottle Opener** are run-level tools: they are used during a customer round but ignore card selection. Full launch pool of 15 lives in `Assets/Data/tools/tools.json`.

### 7.4 Vouchers (permanent run upgrades, $10)
Examples: **Happy Hour** (+1 Restock), **Double Shift** (+1 Mix), **Wider Rail** (+1 Rail size), **Loyal Clientele** (Patrons $2 cheaper), **Neon Sign** (rarer Patrons appear more often), **Deep Cellar** (ingredient packs contain +1 card).

Rulings (locked 2026-07-09, matching implementation): every Back Room shows **one dedicated Voucher slot** offering a random voucher the player doesn't own yet; the slot is not affected by rerolls and each voucher can be owned once. Loyal Clientele's discount applies from the *next* shop (prices already on the table don't change) and never drops a patron below $1. **Neon Sign** doubles the Uncommon/Rare shop weights (base weights C60/U30/R10; Legendary weight 0 — "only via special means" = the Speakeasy Pack). **Deep Cellar** adds a 4th card to Cellar Packs. Launch pool of 6 lives in `Assets/Data/vouchers/vouchers.json`.

Pack rulings: the two **Booster Pack slots** roll two *distinct* kinds per shop (among kinds the run's pools can fill) and do not reroll. Buying a pack opens it immediately: the player takes exactly one reward or skips (money stays spent); the Back Room can't be left with a pack open. Cellar Packs offer 3 random cards (any type, flavor 2–10, 1-in-4 quality upgrade); Distiller 1-of-2 Books; Bar Kit 1-of-2 Tools; Regulars 1-of-2 Patrons; Speakeasy one rare-or-legendary Patron (legendary 1-in-4 when available) + a Tool + a Book.

### 7.5 Money (Tips) economy
- Satisfy Customer A: $3. Customer B: $4. VIP: $5.
- +$1 per unused Mix.
- **Interest:** +$1 per $5 held, capped at +$5 (encourages banking to $25).
- Money cap: none. Debt: not allowed (no negative purchases).

---
