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
| Bankruptcies | 68 (34.0%) |
| Reached the 30-day horizon | 132 (66.0%) |
| Days survived p25/median/p75 | 25 / 30 / 30 |
| Final till p25/median/p75 | $-106 / $10 / $35 |
| Avg income / expenses per day | $111.1 / $112.6 |
| Avg daily satisfaction | 56% |
| Storm-offs | 7224 (13.1%) |
| Customers per night | 9.8 |
| Served per bar-minute | 4.72 |
| Bar standing (avg night) | 1.82 stars |
| Serves Exact / Close / Wrong | 62507 (99.7%) / 173 (0.3%) / 4 (0.0%) |
| Refused (too little in the glass) / declined | 24 (0.0%) / 2713 |
| Take: base / tip | $305204 / $238427 (238427 (43.9%) of it tip) |
| Avg base / tip per serve | $4.87 / $3.80 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 27961 (100.0%) of 27971 |
| Garnish craft landed | 34420 (54.9%) |
| Extra orders earned (of serves) | 17676 (28.2%) |
| Extra orders earned (of exact) | 17676 (28.3%) |
| Pour accuracy on exact serves (avg) | 77% |
| PERFECT makes (of exact serves) | 50 (0.1%) |
| Recipes revealed by run end (avg) | 0.2 |
| Draught share of serves | 6459 (10.3%) |
| Pints in the good head band | 6459 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 20964 (33.4%) · $57473 |
| Glasses collected / wipes / washes | 40080 / 41440 / 40080 |
| Service (avg night) / comfort (avg night) | 2.80 / 1.90 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 5422 (96.5%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 1.85 / 2.15 / 2.40 |
| Dressing rungs bought (by slot) | counter_end 22 · walls 200 |
| Minors met / shown the door / served (of seats) | 3072 / 3072 / 0 (5.3% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 |
| State's thanks (total · of income) | $23180 · 3.7% |
| Recipes bought (of 200 runs) | 1932 |
| Brand upgrades bought | 303 |
| Tier demands the shelf could not answer | 419 of 5535 (7.6%) |
| Demanded upgrades bought | 303 |
| Demanded upgrades OFFERED | 722 |

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
| 1.5★ | 186 (93.0%) | 17 / 18 / 20 | 3 |
| 2.0★ | 101 (50.5%) | 23 / 25 / 27 | 5 |
| 2.5★ | **none of 200** | — | — |
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
| 9 | 200 | 91 (45.5%) | 0 (0.0%) |
| 10 | 200 | 79 (39.5%) | 3 (1.5%) |
| 11 | 200 | 139 (69.5%) | 3 (1.5%) |
| 12 | 200 | 120 (60.0%) | 8 (4.0%) |
| 13 | 200 | 86 (43.0%) | 15 (7.5%) |
| 14 | 200 | 72 (36.0%) | 20 (10.0%) |
| 15 | 200 | 75 (37.5%) | 40 (20.0%) |
| 16 | 200 | 74 (37.0%) | 40 (20.0%) |
| 17 | 200 | 98 (49.0%) | 54 (27.0%) |
| 18 | 200 | 133 (66.5%) | 73 (36.5%) |
| 19 | 200 | 126 (63.0%) | 75 (37.5%) |
| 20 | 200 | 134 (67.0%) | 91 (45.5%) |
| 21 | 200 | 138 (69.0%) | 122 (61.0%) |
| 22 | 200 | 141 (70.5%) | 125 (62.5%) |
| 23 | 192 | 146 (76.0%) | 131 (68.2%) |
| 24 | 176 | 121 (68.8%) | 118 (67.0%) |
| 25 | 161 | 106 (65.8%) | 102 (63.4%) |
| 26 | 148 | 91 (61.5%) | 91 (61.5%) |
| 27 | 140 | 83 (59.3%) | 80 (57.1%) |
| 28 | 135 | 84 (62.2%) | 82 (60.7%) |
| 29 | 133 | 82 (61.7%) | 82 (61.7%) |
| 30 | 132 | 69 (52.3%) | 68 (51.5%) |
