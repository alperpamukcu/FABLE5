# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: aims each ingredient at the middle of its lit 20-point box
(the revealed perfect once a page is perfected), pulls a pint
leaned over then straightened, and shops — stock, recipes, stools,
glass steps, and one brand upgrade a night it never once affords.
Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 14 (7.0%) |
| Reached the 30-day horizon | 186 (93.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $87 / $145 / $198 |
| Avg income / expenses per day | $129.7 / $125.5 |
| Avg daily satisfaction | 54% |
| Storm-offs | 9775 (16.1%) |
| Customers per night | 10.2 |
| Served per bar-minute | 4.89 |
| Bar standing (avg night) | 2.59 stars |
| Serves Exact / Close / Wrong | 68612 (100.0%) / 12 (0.0%) / 1 (0.0%) |
| Refused (too little in the glass) / declined | 7 (0.0%) / 1335 |
| Take: base / tip | $392121 / $311833 (311833 (44.3%) of it tip) |
| Avg base / tip per serve | $5.71 / $4.54 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 31093 (100.0%) of 31096 |
| Garnish craft landed | 36945 (53.8%) |
| Extra orders earned (of serves) | 19173 (27.9%) |
| Extra orders earned (of exact) | 19173 (27.9%) |
| Pour accuracy on exact serves (avg) | 77% |
| PERFECT makes (of exact serves) | 1562 (2.3%) |
| Recipes revealed by run end (avg) | 1.1 |
| Draught share of serves | 5852 (8.5%) |
| Pints in the good head band | 5852 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 22936 (33.4%) · $62895 |
| Glasses bussed | 50994 |
| Recipes bought (of 200 runs) | 3024 |
| Brand upgrades bought | 646 |
| Tier demands the shelf could not answer | 202 of 11290 (1.8%) |
| Demanded upgrades bought | 645 |
| Demanded upgrades OFFERED | 945 |

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
| 1.0★ | 200 (100.0%) | 7 / 7 / 8 | 2 |
| 1.5★ | 200 (100.0%) | 11 / 12 / 13 | 2 |
| 2.0★ | 187 (93.5%) | 16 / 17 / 18 | 3 |
| 2.5★ | 182 (91.0%) | 21 / 22 / 24 | 4 |
| 3.0★ | 15 (7.5%) | 28 / 30 / 30 | 5 |
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
| 2 | 200 | 49 (24.5%) | 1 (0.5%) |
| 3 | 200 | 80 (40.0%) | 4 (2.0%) |
| 4 | 200 | 65 (32.5%) | 2 (1.0%) |
| 5 | 200 | 77 (38.5%) | 0 (0.0%) |
| 6 | 200 | 84 (42.0%) | 0 (0.0%) |
| 7 | 200 | 73 (36.5%) | 3 (1.5%) |
| 8 | 200 | 101 (50.5%) | 2 (1.0%) |
| 9 | 200 | 130 (65.0%) | 0 (0.0%) |
| 10 | 200 | 100 (50.0%) | 0 (0.0%) |
| 11 | 200 | 68 (34.0%) | 1 (0.5%) |
| 12 | 200 | 35 (17.5%) | 4 (2.0%) |
| 13 | 200 | 32 (16.0%) | 3 (1.5%) |
| 14 | 200 | 26 (13.0%) | 3 (1.5%) |
| 15 | 200 | 63 (31.5%) | 7 (3.5%) |
| 16 | 200 | 106 (53.0%) | 9 (4.5%) |
| 17 | 200 | 152 (76.0%) | 15 (7.5%) |
| 18 | 200 | 176 (88.0%) | 23 (11.5%) |
| 19 | 200 | 155 (77.5%) | 20 (10.0%) |
| 20 | 200 | 132 (66.0%) | 28 (14.0%) |
| 21 | 200 | 126 (63.0%) | 43 (21.5%) |
| 22 | 200 | 104 (52.0%) | 36 (18.0%) |
| 23 | 194 | 100 (51.5%) | 27 (13.9%) |
| 24 | 191 | 18 (9.4%) | 16 (8.4%) |
| 25 | 189 | 44 (23.3%) | 16 (8.5%) |
| 26 | 188 | 42 (22.3%) | 11 (5.9%) |
| 27 | 188 | 44 (23.4%) | 13 (6.9%) |
| 28 | 188 | 48 (25.5%) | 17 (9.0%) |
| 29 | 188 | 44 (23.4%) | 11 (5.9%) |
| 30 | 188 | 41 (21.8%) | 14 (7.4%) |
