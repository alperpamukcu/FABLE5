# LAST CALL — GDD Module: Systems & Technical Design

> NOTE: The engine for this project is Unity (C#), not Godot. Apply the same data-driven principles using ScriptableObjects or JSON + a pure C# scoring core.

## 13. SYSTEMS & TECHNICAL DESIGN

- **Data-driven:** ingredients, patrons, VIPs, tools, books, vouchers, bars, stakes all in JSON/Godot Resources; effects expressed as composable triggers (`on_card_scored`, `on_hand_scored`, `on_restock`, `on_shop_enter`, `on_night_end`) + operations (`add_flavor`, `add_mult`, `mult_mult`, `add_money`, `retrigger`, `create_card`, `destroy_card`, `transform_card`). New content = new data, not new code.
- **Determinism & seeding:** single seeded RNG stream per run domain (deck shuffle / shop / VIP pool) → seeded runs shareable as strings.
- **Save system:** JSON snapshot after every atomic action; profile file for unlocks/stats; cloud-save friendly (single small file).
- **Scoring engine:** pure function `(mix, patrons, state) → score breakdown`, unit-testable; the UI replays the breakdown as animation. (Testable with the same discipline as Mockito-style unit tests: engine has zero rendering dependencies.)
- **Big numbers:** use double + formatted suffixes (1.2K, 3.4M) from Night 6 onward; cap at 1e308 with "∞" display.
- **Performance target:** 60 fps on integrated GPUs; everything is 2D sprites + 2 shaders.
- **Localization:** all strings in CSV keys from day one (EN/TR launch).

---
