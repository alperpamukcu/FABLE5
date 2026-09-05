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
| Bankruptcies | 3 (1.5%) |
| Reached the 30-day horizon | 197 (98.5%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $64 / $76 / $87 |
| Avg income / expenses per day | $129.9 / $127.9 |
| Avg daily satisfaction | 59% |
| Storm-offs | 10197 (16.6%) |
| Customers per night | 10.2 |
| Served per bar-minute | 4.87 |
| Bar standing (avg night) | 2.66 stars |
| Serves Exact / Close / Wrong | 70325 (99.9%) / 44 (0.1%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 1 (0.0%) / 385 |
| Take: base / tip | $406408 / $305964 (305964 (43.0%) of it tip) |
| Avg base / tip per serve | $5.78 / $4.35 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 32074 (100.0%) of 32074 |
| Garnish craft landed | 37956 (53.9%) |
| Extra orders earned (of serves) | 19643 (27.9%) |
| Extra orders earned (of exact) | 19643 (27.9%) |
| Pour accuracy on exact serves (avg) | 77% |
| PERFECT makes (of exact serves) | 1738 (2.5%) |
| Recipes revealed by run end (avg) | 1.1 |
| Draught share of serves | 5882 (8.4%) |
| Pints in the good head band | 5882 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23533 (33.4%) · $64533 |
| Glasses collected / wipes / washes | 45043 / 45043 / 45043 |
| Service (avg night) / comfort (avg night) | 2.94 / 2.99 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 2784 (46.5%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 2.50 / 3.35 / 3.83 |
| Dressing rungs bought (by slot) | counter_end 197 · plant_left 130 · table_left 59 · table_mid 20 · table_right 7 · wall_lamps 144 |
| Recipes bought (of 200 runs) | 3014 |
| Brand upgrades bought | 655 |
| Tier demands the shelf could not answer | 118 of 11966 (1.0%) |
| Demanded upgrades bought | 655 |
| Demanded upgrades OFFERED | 818 |

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
| 2.0★ | 199 (99.5%) | 15 / 16 / 17 | 3 |
| 2.5★ | 196 (98.0%) | 21 / 22 / 23 | 4 |
| 3.0★ | 8 (4.0%) | 29 / 30 / 30 | 5 |
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
| 2 | 200 | 75 (37.5%) | 0 (0.0%) |
| 3 | 200 | 76 (38.0%) | 1 (0.5%) |
| 4 | 200 | 65 (32.5%) | 0 (0.0%) |
| 5 | 200 | 77 (38.5%) | 0 (0.0%) |
| 6 | 200 | 69 (34.5%) | 0 (0.0%) |
| 7 | 200 | 66 (33.0%) | 0 (0.0%) |
| 8 | 200 | 126 (63.0%) | 0 (0.0%) |
| 9 | 200 | 119 (59.5%) | 0 (0.0%) |
| 10 | 200 | 97 (48.5%) | 1 (0.5%) |
| 11 | 200 | 52 (26.0%) | 0 (0.0%) |
| 12 | 200 | 53 (26.5%) | 0 (0.0%) |
| 13 | 200 | 62 (31.0%) | 2 (1.0%) |
| 14 | 200 | 94 (47.0%) | 4 (2.0%) |
| 15 | 200 | 121 (60.5%) | 4 (2.0%) |
| 16 | 200 | 139 (69.5%) | 3 (1.5%) |
| 17 | 200 | 129 (64.5%) | 12 (6.0%) |
| 18 | 200 | 125 (62.5%) | 13 (6.5%) |
| 19 | 200 | 90 (45.0%) | 18 (9.0%) |
| 20 | 200 | 101 (50.5%) | 22 (11.0%) |
| 21 | 200 | 97 (48.5%) | 32 (16.0%) |
| 22 | 200 | 109 (54.5%) | 48 (24.0%) |
| 23 | 200 | 92 (46.0%) | 39 (19.5%) |
| 24 | 199 | 41 (20.6%) | 35 (17.6%) |
| 25 | 198 | 62 (31.3%) | 28 (14.1%) |
| 26 | 198 | 89 (44.9%) | 35 (17.7%) |
| 27 | 197 | 87 (44.2%) | 30 (15.2%) |
| 28 | 197 | 86 (43.7%) | 31 (15.7%) |
| 29 | 197 | 86 (43.7%) | 33 (16.8%) |
| 30 | 197 | 83 (42.1%) | 26 (13.2%) |
