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
| Bankruptcies | 2 (1.0%) |
| Reached the 30-day horizon | 198 (99.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $68 / $80 / $88 |
| Avg income / expenses per day | $133.6 / $131.4 |
| Avg daily satisfaction | 60% |
| Storm-offs | 9276 (15.4%) |
| Customers per night | 10.0 |
| Served per bar-minute | 4.78 |
| Bar standing (avg night) | 2.72 stars |
| Serves Exact / Close / Wrong | 69676 (100.0%) / 29 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 1 (0.0%) / 312 |
| Take: base / tip | $408432 / $310924 (310924 (43.2%) of it tip) |
| Avg base / tip per serve | $5.86 / $4.46 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 31812 (100.0%) of 31813 |
| Garnish craft landed | 37533 (53.8%) |
| Extra orders earned (of serves) | 19156 (27.5%) |
| Extra orders earned (of exact) | 19156 (27.5%) |
| Pour accuracy on exact serves (avg) | 77% |
| PERFECT makes (of exact serves) | 1818 (2.6%) |
| Recipes revealed by run end (avg) | 1.3 |
| Draught share of serves | 5721 (8.2%) |
| Pints in the good head band | 5721 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23300 (33.4%) · $63889 |
| Glasses collected / wipes / washes | 44838 / 44838 / 44838 |
| Service (avg night) / comfort (avg night) | 2.98 / 3.11 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 2433 (40.6%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 2.50 / 3.50 / 4.15 |
| Dressing rungs bought (by slot) | counter_end 198 · plant_left 171 · table_left 124 · table_mid 68 · table_right 32 · wall_lamps 199 |
| Minors met / shown the door / served (of seats) | 3418 / 3418 / 0 (5.4% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 · 3★ $0.00 |
| State's thanks (total · of income) | $17090 · 2.1% |
| Recipes bought (of 200 runs) | 3213 |
| Brand upgrades bought | 702 |
| Tier demands the shelf could not answer | 67 of 12490 (0.5%) |
| Demanded upgrades bought | 702 |
| Demanded upgrades OFFERED | 1097 |

## The star track — when a bar reaches each rung

Eleven rungs, one written guest on each. This is the table the
thresholds get chosen from, and it is a FLOOR like everything else the
bot measures: it reads only the ID, never shops, and never buys a brand,
so a played bar climbs faster than this. Trust the SHAPE — how far apart
the rungs are — over the absolute weeks. A rung no run reaches is the
most useful line here: it says a guest written for it would never come.

| Rung | Runs that reached it | Day p25/median/p75 | Median week |
|---|---|---|---|
| 0.0★ | 200 (100.0%) | 1 / 1 / 1 | 1 |
| 0.5★ | 200 (100.0%) | 4 / 4 / 4 | 1 |
| 1.0★ | 200 (100.0%) | 7 / 7 / 7 | 2 |
| 1.5★ | 200 (100.0%) | 11 / 11 / 12 | 2 |
| 2.0★ | 199 (99.5%) | 15 / 16 / 16 | 3 |
| 2.5★ | 199 (99.5%) | 20 / 21 / 22 | 4 |
| 3.0★ | 42 (21.0%) | 28 / 29 / 30 | 5 |
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
| 1 | 200 | 0 (0.0%) | 0 (0.0%) |
| 2 | 200 | 80 (40.0%) | 0 (0.0%) |
| 3 | 200 | 75 (37.5%) | 1 (0.5%) |
| 4 | 200 | 65 (32.5%) | 0 (0.0%) |
| 5 | 200 | 81 (40.5%) | 0 (0.0%) |
| 6 | 200 | 73 (36.5%) | 0 (0.0%) |
| 7 | 200 | 62 (31.0%) | 0 (0.0%) |
| 8 | 200 | 104 (52.0%) | 0 (0.0%) |
| 9 | 200 | 106 (53.0%) | 0 (0.0%) |
| 10 | 200 | 87 (43.5%) | 0 (0.0%) |
| 11 | 200 | 55 (27.5%) | 0 (0.0%) |
| 12 | 200 | 49 (24.5%) | 0 (0.0%) |
| 13 | 200 | 92 (46.0%) | 0 (0.0%) |
| 14 | 200 | 93 (46.5%) | 1 (0.5%) |
| 15 | 200 | 118 (59.0%) | 0 (0.0%) |
| 16 | 200 | 135 (67.5%) | 1 (0.5%) |
| 17 | 200 | 127 (63.5%) | 5 (2.5%) |
| 18 | 200 | 115 (57.5%) | 6 (3.0%) |
| 19 | 200 | 103 (51.5%) | 12 (6.0%) |
| 20 | 200 | 100 (50.0%) | 13 (6.5%) |
| 21 | 200 | 104 (52.0%) | 33 (16.5%) |
| 22 | 200 | 102 (51.0%) | 29 (14.5%) |
| 23 | 200 | 92 (46.0%) | 26 (13.0%) |
| 24 | 199 | 26 (13.1%) | 14 (7.0%) |
| 25 | 199 | 67 (33.7%) | 25 (12.6%) |
| 26 | 199 | 86 (43.2%) | 18 (9.0%) |
| 27 | 199 | 83 (41.7%) | 15 (7.5%) |
| 28 | 199 | 96 (48.2%) | 16 (8.0%) |
| 29 | 199 | 84 (42.2%) | 17 (8.5%) |
| 30 | 198 | 78 (39.4%) | 16 (8.1%) |
