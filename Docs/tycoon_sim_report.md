# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: aims each ingredient at the middle of its lit 20-point box
(the revealed perfect once a page is perfected), pulls a pint
leaned over then straightened, keeps the counter the instant a mess
lands (collect, wipe, wash), and shops — stock, recipes, stools, glass
steps, the cheapest open dressing rung, and one brand upgrade a night it
never once affords. Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 18 (9.0%) |
| Reached the 30-day horizon | 182 (91.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $133 / $261 / $365 |
| Avg income / expenses per day | $232.1 / $224.3 |
| Avg daily satisfaction | 58% |
| Storm-offs | 8421 (14.6%) |
| Customers per night | 9.8 |
| Served per bar-minute | 4.70 |
| Bar standing (avg night) | 2.13 stars |
| Serves Exact / Close / Wrong | 67409 (99.9%) / 70 (0.1%) / 1 (0.0%) |
| Refused (too little in the glass) / declined | 4 (0.0%) / 736 |
| Take: base / tip | $731948 / $547710 (547710 (42.8%) of it tip) |
| Avg base / tip per serve | $10.85 / $8.12 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 30350 (100.0%) of 30351 |
| Garnish craft landed | 36891 (54.7%) |
| Extra orders earned (of serves) | 18851 (27.9%) |
| Extra orders earned (of exact) | 18851 (28.0%) |
| Pour accuracy on exact serves (avg) | 77% |
| PERFECT makes (of exact serves) | 434 (0.6%) |
| Recipes revealed by run end (avg) | 0.4 |
| Draught share of serves | 6541 (9.7%) |
| Pints in the good head band | 6541 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 22562 (33.4%) · $61866 |
| Glasses collected / wipes / washes | 43131 / 44551 / 43131 |
| Service (avg night) / comfort (avg night) | 2.90 / 2.44 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 4326 (73.4%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 1.85 / 3.33 / 4.21 |
| Dressing rungs bought (by slot) | counter_end 140 · floor_rug 133 · plant_left 46 · table_left 16 · table_mid 2 · walls 365 |
| Minors met / shown the door / served (of seats) | 3248 / 3248 / 0 (5.3% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 |
| State's thanks (total · of income) | $26580 · 1.9% |
| Recipes bought (of 200 runs) | 2215 |
| Brand upgrades bought | 657 |
| Tier demands the shelf could not answer | 223 of 7687 (2.9%) |
| Demanded upgrades bought | 371 |
| Demanded upgrades OFFERED | 594 |

## The star track — when a bar reaches each rung

Eleven rungs, one written guest on each. This is the table the
thresholds get chosen from, and it is a FLOOR like everything else the
bot measures: it reads only the ID and shops by rule (stock, the cheapest
page, one rung a night, a brand it can rarely afford), never by taste —
so a played bar climbs faster than this. Trust the SHAPE — how far apart
the rungs are — over the absolute weeks. A rung no run reaches is the
most useful line here: it says a guest written for it would never come.

| Rung | Runs that reached it | Day p25/median/p75 | Median week |
|---|---|---|---|
| 0.0★ | 200 (100.0%) | 1 / 1 / 1 | 1 |
| 0.5★ | 200 (100.0%) | 6 / 6 / 6 | 1 |
| 1.0★ | 200 (100.0%) | 11 / 11 / 12 | 2 |
| 1.5★ | 194 (97.0%) | 16 / 18 / 19 | 3 |
| 2.0★ | 178 (89.0%) | 21 / 22 / 25 | 4 |
| 2.5★ | 118 (59.0%) | 25 / 26 / 27 | 5 |
| 3.0★ | **none of 200** | — | — |
| 3.5★ | **none of 200** | — | — |
| 4.0★ | **none of 200** | — | — |
| 4.5★ | **none of 200** | — | — |
| 5.0★ | **none of 200** | — | — |

## The written nights (GDD 26)

The bot starts the trial the moment it reaches the stool (it has no
dialogue to read), pours every ask to the trial's own fill standard, and
says an honest no when the shelf cannot make one. None of this touches
the numbers above: a guest of the house is not a customer.

| Measure | Value |
|---|---|
| Trials walked in | 200 |
| Drinks poured for them | 200 |
| Passed / failed / declined | 200 / 0 / 0 |
| Arcs finished inside 30 nights | 200 (100.0%) |

## Red days by day number

Two columns because there are two ways to end a night behind: the
takings failed to cover rent and stock, or they covered it and the bar
went shopping. Only the second column is trouble.

| Day | Closed | In the red | Red before shopping |
|---|---|---|---|
| 1 | 200 | 95 (47.5%) | 0 (0.0%) |
| 2 | 200 | 21 (10.5%) | 0 (0.0%) |
| 3 | 200 | 53 (26.5%) | 0 (0.0%) |
| 4 | 200 | 67 (33.5%) | 0 (0.0%) |
| 5 | 200 | 62 (31.0%) | 0 (0.0%) |
| 6 | 200 | 93 (46.5%) | 0 (0.0%) |
| 7 | 200 | 76 (38.0%) | 1 (0.5%) |
| 8 | 200 | 82 (41.0%) | 0 (0.0%) |
| 9 | 200 | 90 (45.0%) | 0 (0.0%) |
| 10 | 200 | 74 (37.0%) | 2 (1.0%) |
| 11 | 200 | 117 (58.5%) | 0 (0.0%) |
| 12 | 200 | 123 (61.5%) | 2 (1.0%) |
| 13 | 200 | 58 (29.0%) | 2 (1.0%) |
| 14 | 200 | 47 (23.5%) | 0 (0.0%) |
| 15 | 200 | 38 (19.0%) | 4 (2.0%) |
| 16 | 200 | 35 (17.5%) | 6 (3.0%) |
| 17 | 200 | 49 (24.5%) | 7 (3.5%) |
| 18 | 200 | 69 (34.5%) | 13 (6.5%) |
| 19 | 200 | 83 (41.5%) | 15 (7.5%) |
| 20 | 200 | 109 (54.5%) | 20 (10.0%) |
| 21 | 200 | 126 (63.0%) | 39 (19.5%) |
| 22 | 196 | 120 (61.2%) | 40 (20.4%) |
| 23 | 195 | 99 (50.8%) | 30 (15.4%) |
| 24 | 192 | 48 (25.0%) | 23 (12.0%) |
| 25 | 191 | 45 (23.6%) | 32 (16.8%) |
| 26 | 187 | 40 (21.4%) | 19 (10.2%) |
| 27 | 184 | 54 (29.3%) | 22 (12.0%) |
| 28 | 183 | 44 (24.0%) | 16 (8.7%) |
| 29 | 183 | 48 (26.2%) | 16 (8.7%) |
| 30 | 183 | 54 (29.5%) | 23 (12.6%) |
