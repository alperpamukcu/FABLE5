# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: aims each ingredient at the middle of its lit 20-point box
(the revealed perfect once a page is perfected), pulls a pint
leaned over then straightened, and shops — stock, recipes, stools,
glass steps, and one brand upgrade a night it never once affords.
Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 6 (3.0%) |
| Reached the 30-day horizon | 194 (97.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $136 / $194 / $250 |
| Avg income / expenses per day | $135.1 / $129.3 |
| Avg daily satisfaction | 56% |
| Storm-offs | 10051 (16.4%) |
| Customers per night | 10.3 |
| Served per bar-minute | 4.83 |
| Bar standing (avg night) | 2.67 stars |
| Serves Exact / Close / Wrong | 70724 (100.0%) / 7 (0.0%) / 1 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 571 |
| Take: base / tip | $412778 / $327286 (327286 (44.2%) of it tip) |
| Avg base / tip per serve | $5.84 / $4.63 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 32230 (100.0%) of 32230 |
| Garnish craft landed | 38093 (53.9%) |
| Extra orders earned (of serves) | 20238 (28.6%) |
| Extra orders earned (of exact) | 20238 (28.6%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 1814 (2.6%) |
| Recipes revealed by run end (avg) | 1.3 |
| Draught share of serves | 5863 (8.3%) |
| Pints in the good head band | 5863 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23642 (33.4%) · $64859 |
| Glasses bussed | 51265 |
| Recipes bought (of 200 runs) | 3301 |
| Brand upgrades bought | 729 |
| Tier demands the shelf could not answer | 159 of 12516 (1.3%) |
| Demanded upgrades bought | 727 |
| Demanded upgrades OFFERED | 1046 |

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
| 1.5★ | 200 (100.0%) | 11 / 12 / 12 | 2 |
| 2.0★ | 198 (99.0%) | 16 / 16 / 17 | 3 |
| 2.5★ | 193 (96.5%) | 21 / 22 / 23 | 4 |
| 3.0★ | 24 (12.0%) | 29 / 30 / 30 | 5 |
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
| 2 | 200 | 86 (43.0%) | 0 (0.0%) |
| 3 | 200 | 85 (42.5%) | 0 (0.0%) |
| 4 | 200 | 71 (35.5%) | 2 (1.0%) |
| 5 | 200 | 85 (42.5%) | 0 (0.0%) |
| 6 | 200 | 57 (28.5%) | 0 (0.0%) |
| 7 | 200 | 70 (35.0%) | 0 (0.0%) |
| 8 | 200 | 122 (61.0%) | 1 (0.5%) |
| 9 | 200 | 130 (65.0%) | 1 (0.5%) |
| 10 | 200 | 91 (45.5%) | 0 (0.0%) |
| 11 | 200 | 62 (31.0%) | 1 (0.5%) |
| 12 | 200 | 23 (11.5%) | 3 (1.5%) |
| 13 | 200 | 13 (6.5%) | 1 (0.5%) |
| 14 | 200 | 24 (12.0%) | 3 (1.5%) |
| 15 | 200 | 80 (40.0%) | 5 (2.5%) |
| 16 | 200 | 141 (70.5%) | 6 (3.0%) |
| 17 | 200 | 168 (84.0%) | 8 (4.0%) |
| 18 | 200 | 182 (91.0%) | 13 (6.5%) |
| 19 | 199 | 156 (78.4%) | 10 (5.0%) |
| 20 | 199 | 128 (64.3%) | 11 (5.5%) |
| 21 | 198 | 120 (60.6%) | 14 (7.1%) |
| 22 | 198 | 110 (55.6%) | 25 (12.6%) |
| 23 | 198 | 119 (60.1%) | 18 (9.1%) |
| 24 | 197 | 14 (7.1%) | 11 (5.6%) |
| 25 | 196 | 34 (17.3%) | 11 (5.6%) |
| 26 | 195 | 40 (20.5%) | 7 (3.6%) |
| 27 | 195 | 43 (22.1%) | 8 (4.1%) |
| 28 | 194 | 30 (15.5%) | 9 (4.6%) |
| 29 | 194 | 37 (19.1%) | 12 (6.2%) |
| 30 | 194 | 34 (17.5%) | 11 (5.7%) |
