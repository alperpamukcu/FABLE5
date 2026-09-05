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
| Bankruptcies | 1 (0.5%) |
| Reached the 30-day horizon | 199 (99.5%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $65 / $77 / $87 |
| Avg income / expenses per day | $133.4 / $131.2 |
| Avg daily satisfaction | 60% |
| Storm-offs | 9342 (15.5%) |
| Customers per night | 10.0 |
| Served per bar-minute | 4.78 |
| Bar standing (avg night) | 2.71 stars |
| Serves Exact / Close / Wrong | 69621 (100.0%) / 33 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 268 |
| Take: base / tip | $408229 / $311004 (311004 (43.2%) of it tip) |
| Avg base / tip per serve | $5.86 / $4.46 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 31674 (100.0%) of 31674 |
| Garnish craft landed | 37441 (53.8%) |
| Extra orders earned (of serves) | 19167 (27.5%) |
| Extra orders earned (of exact) | 19167 (27.5%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 1826 (2.6%) |
| Recipes revealed by run end (avg) | 1.2 |
| Draught share of serves | 5767 (8.3%) |
| Pints in the good head band | 5767 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23281 (33.4%) · $63848 |
| Glasses collected / wipes / washes | 44758 / 44758 / 44758 |
| Service (avg night) / comfort (avg night) | 2.98 / 3.09 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 2521 (42.0%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 2.50 / 3.50 / 4.15 |
| Dressing rungs bought (by slot) | counter_end 199 · plant_left 167 · table_left 136 · table_mid 69 · table_right 33 · wall_lamps 186 |
| Minors met / shown the door / served (of seats) | 3404 / 3404 / 0 (5.4% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 · 3★ $0.00 |
| State's thanks (total · of income) | $17020 · 2.1% |
| Recipes bought (of 200 runs) | 3204 |
| Brand upgrades bought | 708 |
| Tier demands the shelf could not answer | 95 of 12530 (0.8%) |
| Demanded upgrades bought | 708 |
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
| 2.5★ | 198 (99.0%) | 20 / 21 / 22 | 4 |
| 3.0★ | 43 (21.5%) | 29 / 29 / 30 | 5 |
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
| 3 | 200 | 73 (36.5%) | 1 (0.5%) |
| 4 | 200 | 64 (32.0%) | 0 (0.0%) |
| 5 | 200 | 79 (39.5%) | 0 (0.0%) |
| 6 | 200 | 77 (38.5%) | 0 (0.0%) |
| 7 | 200 | 64 (32.0%) | 0 (0.0%) |
| 8 | 200 | 107 (53.5%) | 0 (0.0%) |
| 9 | 200 | 110 (55.0%) | 0 (0.0%) |
| 10 | 200 | 94 (47.0%) | 0 (0.0%) |
| 11 | 200 | 51 (25.5%) | 1 (0.5%) |
| 12 | 200 | 64 (32.0%) | 0 (0.0%) |
| 13 | 200 | 78 (39.0%) | 0 (0.0%) |
| 14 | 200 | 91 (45.5%) | 0 (0.0%) |
| 15 | 200 | 108 (54.0%) | 2 (1.0%) |
| 16 | 200 | 128 (64.0%) | 3 (1.5%) |
| 17 | 200 | 125 (62.5%) | 5 (2.5%) |
| 18 | 200 | 114 (57.0%) | 13 (6.5%) |
| 19 | 200 | 98 (49.0%) | 8 (4.0%) |
| 20 | 200 | 102 (51.0%) | 18 (9.0%) |
| 21 | 200 | 110 (55.0%) | 26 (13.0%) |
| 22 | 200 | 97 (48.5%) | 25 (12.5%) |
| 23 | 200 | 93 (46.5%) | 21 (10.5%) |
| 24 | 200 | 23 (11.5%) | 14 (7.0%) |
| 25 | 200 | 71 (35.5%) | 21 (10.5%) |
| 26 | 200 | 83 (41.5%) | 17 (8.5%) |
| 27 | 200 | 95 (47.5%) | 19 (9.5%) |
| 28 | 199 | 95 (47.7%) | 15 (7.5%) |
| 29 | 199 | 84 (42.2%) | 21 (10.6%) |
| 30 | 199 | 77 (38.7%) | 18 (9.0%) |
