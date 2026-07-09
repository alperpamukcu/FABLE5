# LAST CALL — GDD Module: Bar Themes & Meta Progression

## 9. BAR THEMES (STARTING DECKS) & META PROGRESSION

Unlockable starting configurations, like Balatro decks:

| Bar | Unlock | Modifier |
|---|---|---|
| **The Classic** | default | Standard 48-card cabinet. |
| **The Speakeasy** | Win a run | Start with 1 random Rare Patron; −$2 starting money. |
| **Tiki Hut** | Reach Night 6 | +4 Garnish cards; Tiki starts at level 2. |
| **The Dive** | Win with Speakeasy | +1 Restock, −1 Mix. |
| **Hotel Lobby Bar** | Win 3 runs | Interest cap $10; VIP targets +10%. |
| **The Afterhours** | Win on Amber Stake | Start with a Bootleg card and +1 Patron slot. |

**Collection screen** tracks every Patron/Tool/Book/VIP discovered (silhouette until found). **Challenges** (20 scripted scenario runs) and **unlock toasts** drive long-tail retention. Completion stats: win count per Bar per Stake (sticker system).

Rulings (locked 2026-07-09, matching implementation): M3 ships the first three Bars (Classic / Speakeasy / Tiki Hut), selectable on GameBootstrap; unlock gating is an M4+ meta feature. Tiki Hut's +4 Garnish cards are flavors 4/6/8/10. The Speakeasy's random Rare comes from the run seed ("bar" stream) and skips patrons already seated. Stakes 1–4 are implemented per GDD 5.3 and stack cumulatively (`StakeTable.Apply`).

---
