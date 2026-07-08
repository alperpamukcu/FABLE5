# LAST CALL — Game Design Document (v1.0)

**Genre:** Roguelike Deckbuilder / Score-Attack (Balatro-like)
**Platform:** PC (Windows/Linux/macOS), keyboard + mouse, full controller support
**Perspective:** 2D, fixed single-screen "behind the bar" view (no 3D camera, no player character model — the player IS the bartender, seen implicitly through hands/UI only)
**Target session length:** 30–60 minutes per run
**Team size:** Solo developer
**Recommended engine:** Godot 4.x (GDScript), data-driven design (all cards/patrons/bosses defined in JSON/Resource files)

---

---

## 1. HIGH CONCEPT

You are the bartender of a dim, smoky late-night cocktail bar. Each run is one "Opening Week" of 8 escalating Nights. Customers approach the counter and place orders with a required **Satisfaction score**. You draw **Ingredient cards** from your **Cabinet** (your deck) and combine them into cocktails. Each cocktail is recognized as a **Recipe** (the equivalent of a poker hand) which grants base **Flavor** (chips) and a **Multiplier**. Persistent **Patrons** sitting at your bar (the equivalent of Jokers) modify scoring and enable wild synergies. Between customers you spend **Tips** (money) in the **Back Room** (shop) to buy new ingredients, Patrons, Recipe Books, and Tools. Fail to satisfy a customer and the run ends.

Core fantasy: turning a humble rail drink into an absurd 50,000-point masterpiece through stacked synergies, in a cozy, mysterious, neon-soaked bar.

---

---

## 2. CORE GAME LOOP

```
RUN START → Night 1
  ├── Customer 1 (Regular Order)   → score check → Tips
  ├── BACK ROOM (shop)
  ├── Customer 2 (Regular Order)   → score check → Tips
  ├── BACK ROOM (shop)
  └── Customer 3 (VIP / Critic = Boss) → score check → Tips
        └── Night complete → Night 2 (higher targets) ... Night 8
RUN END: Win after Night 8 VIP → Endless Mode unlock, or LOSE on any failed order.
```

**Per-customer loop (one "round"):**
1. Customer appears with a required Satisfaction target and (for VIPs) a special rule.
2. Player's **Rail** (hand) is filled to 8 Ingredient cards drawn from the Cabinet.
3. Player selects 1–5 ingredients and either:
   - **MIX** (play the hand): the selection is scored as a cocktail, or
   - **RESTOCK** (discard): dump selected cards, draw replacements.
4. Player has **4 Mixes** and **3 Restocks** per customer (modifiable).
5. Scores from all Mixes accumulate. Reach the target before running out of Mixes → success.
6. On success: earn Tips, proceed. On failure: run over, show Game Over summary.

---
