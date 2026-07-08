# LAST CALL — GDD Module: Run Structure, Difficulty Curve & Balance

## 5. RUN STRUCTURE, TARGETS & DIFFICULTY CURVE

### 5.1 Nights and targets (Green Stake / default difficulty)

Each Night = Customer A (small), Customer B (big), VIP (boss). Targets:

| Night | Customer A | Customer B | VIP |
|---|---|---|---|
| 1 | 300 | 450 | 600 |
| 2 | 800 | 1,200 | 1,600 |
| 3 | 2,000 | 3,000 | 4,000 |
| 4 | 5,000 | 7,500 | 10,000 |
| 5 | 11,000 | 16,500 | 22,000 |
| 6 | 20,000 | 30,000 | 40,000 |
| 7 | 35,000 | 52,500 | 70,000 |
| 8 | 50,000 | 75,000 | 100,000 |

(Scaling factor ≈ ×2.0–2.5 per Night, tuned so an un-synergized deck dies around Night 3–4 and a well-built deck cruises to Night 8.)

Customer A may be **skipped** (like skipping a blind) for a **Regular's Favor** tag reward (random bonus: free Patron, coupon, doubled next tip, etc.), trading money/XP for tempo.

### 5.2 Endless Mode
After Night 8, targets continue scaling ×2.2/night indefinitely; leaderboard-style personal best tracked ("Latest Last Call: Night 14").

### 5.3 Stakes (unlockable difficulty modifiers, stack cumulatively)
1. **Green:** baseline.
2. **Amber:** VIPs give no tip bonus.
3. **Copper:** targets +25% on Nights 1–2.
4. **Silver:** −1 Restock.
5. **Gold:** shop prices +2$.
6. **Onyx:** Patrons can appear "Tired" (must be paid $ each night or they leave) — analogous to perishable/rental stickers.
7. **Neon:** −1 Mix.
8. **Midnight:** all of the above; leaderboard flex tier.

---

---

## 11. "LEVEL DESIGN" IN A ROGUELIKE CONTEXT

There are no spatial levels; the level design is the **encounter & economy curve**:
- **Night 1–2 = tutorial pressure:** targets beatable with raw recipes; shop teaches one concept at a time (first shop always contains at least 1 Common Patron and 1 Recipe Book).
- **Night 3–4 = commitment point:** targets outpace unsynergized play; the player must pick an archetype.
- **Night 5–6 = scaling check:** ×Mult sources become mandatory; VIP rules attack over-narrow builds.
- **Night 7–8 = mastery exam:** only stacked multiplicative synergies survive; The Critic finale tests flexibility.
- **VIP pool gating:** Nights 1–2 draw from a "gentle" VIP subset; harsher VIPs unlock into the pool from Night 3+.
- **Pity rules:** shop RNG guarantees a Patron offer at least every 2 shops if the player owns <2 Patrons; a Recipe Book for an already-leveled recipe is weighted up (rich-get-richer feel, like Balatro's planet weighting).

---

---

## 14. BALANCING PHILOSOPHY & TEST PLAN

- **Golden ratio:** a "no-Patron" perfect player should die at Night 3; a single-archetype build clears Night 6; a dual-scaling build (flat Mult source + ×Mult source + leveled recipe) clears Night 8. Tune targets/economy to those anchors.
- **Economy anchor:** average income Nights 1–3 ≈ $12/night; a full archetype costs ≈ $35 → assembled around Night 4. Interest teaches banking.
- **Simulation harness:** headless autoplayer bots (greedy-recipe bot, archetype bots) run 10,000 seeded runs per balance patch; log win-rate per Night. Target: Green Stake overall win rate 15–25% for a competent strategy bot.
- **Anti-degenerate rules:** no infinite loops — retrigger effects cannot retrigger retriggers; money interest capped; Patron ×Mult sources capped at 5 simultaneous.
- **Playtest cadence:** weekly builds; track fail-Night histogram; every Patron must appear in ≥1% of winning runs or gets buffed/reworked.

---
