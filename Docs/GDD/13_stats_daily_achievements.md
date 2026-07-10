# LAST CALL — GDD Module: Stats, Run History, Daily Challenge & Achievements

> Implementation milestone: M5 core (stats, achievements), daily challenge may ship in first post-launch patch.

## 1. Profile stats (persistent, local JSON)
Tracked globally and per-Bar: runs started/won, win rate per Stake, best Night reached, best single mix score (with full breakdown snapshot), most-mixed recipe, favorite Patron (highest presence in wins), total money earned, total mixes.

## 2. Run history
Last 50 runs stored: date, bar, stake, seed, nights survived, final Patron lineup (icons), death cause (customer + shortfall). Tapping a run shows its summary screen again. "Copy seed" button on every entry.

## 3. Daily Shift (daily challenge)
- One global seed per UTC day; everyone plays the same run: same deck order, same shops, same VIPs.
- Fixed setup: The Classic bar, Green Stake, one attempt per day (attempt consumed on first mix).
- Score = furthest Night, tie-break by cumulative score.
- Steam Leaderboard integration: daily board + friends filter. Local history of past daily results.
- UI: MAIN MENU gains a "DAILY SHIFT" button with countdown to next seed.

## 4. Achievements (Steam) — 24 at launch
Progression: first win; win with each of 6 Bars; win on Stakes 2/4/6/8; reach Night 12 endless.
Skill: score 10k / 100k / 1M in a single mix; win without ever restocking; win with 4 or fewer Patrons; beat a VIP using only Neat Pours (hidden).
Collection: discover 30/60 Patrons; discover all VIPs; level any recipe to 10.
Flavor (hidden): mix a Double Perfect; sell the Mysterious Stranger at max value; end a run with $100+.

## 5. Telemetry (local, for balancing — no network)
Append-only JSONL event log per run: run_start(bar,stake,seed), mix(recipe,score,night), purchase(item,price), skip(tag), death(night,customer,shortfall), win. A dev-only importer aggregates logs into the balance dashboard (fail-Night histogram, Patron pick/win rates, recipe usage). Shipped builds keep logging locally; an opt-in "share anonymous stats" toggle may come later — default OFF.
