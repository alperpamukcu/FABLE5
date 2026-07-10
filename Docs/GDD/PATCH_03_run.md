# PATCH → apply to `03_run_structure_balance.md` (v1.1) — ⚠ M2-BLOCKING

## Add §5.4 — Regular's Favor tags (Customer A skip rewards)
Skipping Customer A grants ONE random tag (weighted; no duplicates held):
| Tag | Effect |
|---|---|
| Loyal Tab | Next shop: one Patron slot is free. |
| On the House | Next shop: booster packs cost $0. |
| Double Tip | Next satisfied customer pays double tip. |
| Investor | Gain $15 after beating the next VIP. |
| Top Shelf Cellar | Next ingredient pack: all cards Top Shelf. |
| Speakeasy Key | Next shop guaranteed to stock a Speakeasy Pack. |
| Word of Mouth | Immediately gain a random Common Patron. |
| Quick Hands | +1 Mix for the next customer only. |
Tags stack in a queue (max 4 held), consumed automatically when their condition occurs.

## Add §5.5 — VIP pool rules
- No VIP repeats within a single run.
- Nights 1–2 draw from the "gentle" subset (Teetotaler, Allergic, Health Inspector, Purist).
- Night 8 is always The Critic (in addition to the no-repeat rule).
- The Night's VIP identity is revealed when the Night begins (before Customer A), so skip/economy decisions can react to it.
- Counterplay: see the "Bouncer" voucher in 05_shop_economy patch.
