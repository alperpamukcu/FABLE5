# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: aims each ingredient at the middle of its lit 20-point box
(the revealed perfect once a page is perfected), pulls a pint
leaned over then straightened, and shops — stock, recipes, stools,
glass steps, and one brand upgrade a night it never once affords.
Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 2 (1.0%) |
| Reached the 30-day horizon | 198 (99.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $84 / $136 / $199 |
| Avg income / expenses per day | $131.7 / $127.5 |
| Avg daily satisfaction | 56% |
| Storm-offs | 10253 (16.7%) |
| Customers per night | 10.3 |
| Served per bar-minute | 4.87 |
| Bar standing (avg night) | 2.71 stars |
| Serves Exact / Close / Wrong | 70443 (100.0%) / 31 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 310 |
| Take: base / tip | $412752 / $311215 (311215 (43.0%) of it tip) |
| Avg base / tip per serve | $5.86 / $4.42 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 32152 (100.0%) of 32152 |
| Garnish craft landed | 37927 (53.8%) |
| Extra orders earned (of serves) | 19598 (27.8%) |
| Extra orders earned (of exact) | 19598 (27.8%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 1817 (2.6%) |
| Recipes revealed by run end (avg) | 1.2 |
| Draught share of serves | 5775 (8.2%) |
| Pints in the good head band | 5775 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23558 (33.4%) · $64596 |
| Glasses bussed | 51386 |
| Recipes bought (of 200 runs) | 3243 |
| Brand upgrades bought | 713 |
| Tier demands the shelf could not answer | 132 of 12530 (1.1%) |
| Demanded upgrades bought | 713 |
| Demanded upgrades OFFERED | 1025 |

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
| 2.5★ | 196 (98.0%) | 20 / 21 / 22 | 4 |
| 3.0★ | 24 (12.0%) | 28 / 29 / 30 | 5 |
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
| 2 | 200 | 72 (36.0%) | 0 (0.0%) |
| 3 | 200 | 72 (36.0%) | 2 (1.0%) |
| 4 | 200 | 64 (32.0%) | 0 (0.0%) |
| 5 | 200 | 74 (37.0%) | 0 (0.0%) |
| 6 | 200 | 71 (35.5%) | 0 (0.0%) |
| 7 | 200 | 70 (35.0%) | 0 (0.0%) |
| 8 | 200 | 118 (59.0%) | 0 (0.0%) |
| 9 | 200 | 115 (57.5%) | 0 (0.0%) |
| 10 | 200 | 98 (49.0%) | 1 (0.5%) |
| 11 | 200 | 54 (27.0%) | 2 (1.0%) |
| 12 | 200 | 18 (9.0%) | 2 (1.0%) |
| 13 | 200 | 10 (5.0%) | 0 (0.0%) |
| 14 | 200 | 41 (20.5%) | 2 (1.0%) |
| 15 | 200 | 101 (50.5%) | 5 (2.5%) |
| 16 | 200 | 149 (74.5%) | 2 (1.0%) |
| 17 | 200 | 178 (89.0%) | 10 (5.0%) |
| 18 | 200 | 159 (79.5%) | 8 (4.0%) |
| 19 | 200 | 132 (66.0%) | 11 (5.5%) |
| 20 | 200 | 121 (60.5%) | 19 (9.5%) |
| 21 | 200 | 102 (51.0%) | 21 (10.5%) |
| 22 | 200 | 95 (47.5%) | 35 (17.5%) |
| 23 | 200 | 100 (50.0%) | 29 (14.5%) |
| 24 | 199 | 32 (16.1%) | 24 (12.1%) |
| 25 | 198 | 47 (23.7%) | 12 (6.1%) |
| 26 | 198 | 51 (25.8%) | 14 (7.1%) |
| 27 | 198 | 62 (31.3%) | 18 (9.1%) |
| 28 | 198 | 66 (33.3%) | 23 (11.6%) |
| 29 | 198 | 45 (22.7%) | 18 (9.1%) |
| 30 | 198 | 44 (22.2%) | 18 (9.1%) |
