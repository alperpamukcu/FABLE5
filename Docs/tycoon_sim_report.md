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
| Bankruptcies | 200 (100.0%) |
| Reached the 30-day horizon | 0 (0.0%) |
| Days survived p25/median/p75 | 21 / 21 / 22 |
| Final till p25/median/p75 | $-280 / $-220 / $-145 |
| Avg income / expenses per day | $102.3 / $113.5 |
| Avg daily satisfaction | 49% |
| Storm-offs | 4187 (10.2%) |
| Customers per night | 9.6 |
| Served per bar-minute | 4.60 |
| Bar standing (avg night) | 1.29 stars |
| Serves Exact / Close / Wrong | 43439 (99.2%) / 200 (0.5%) / 4 (0.0%) |
| Refused (too little in the glass) / declined | 156 (0.4%) / 4476 |
| Take: base / tip | $239157 / $188904 (188904 (44.1%) of it tip) |
| Avg base / tip per serve | $5.46 / $4.31 |
| Avg spec score / fill score | 100% / 92% |
| Orders with a serving spec, fully met | 14990 (99.6%) of 15053 |
| Garnish craft landed | 20926 (47.8%) |
| Extra orders earned (of serves) | 11280 (25.8%) |
| Extra orders earned (of exact) | 11280 (26.0%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 16 (0.0%) |
| Recipes revealed by run end (avg) | 0.1 |
| Draught share of serves | 6091 (13.9%) |
| Pints in the good head band | 6091 (100.0%) |
| Average head poured | 18% |
| Glasses collected / wipes / washes | 33183 / 34434 / 33183 |
| Service (avg night) / comfort (avg night) | 2.45 / 1.36 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 4168 (97.2%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 1.50 / 1.50 / — |
| Dressing rungs bought (by slot) | walls 200 |
| Minors met / shown the door / served (of seats) | 941 / 941 / 0 (2.2% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 |
| State's thanks (total · of income) | $10425 · 2.4% |
| Recipes bought (of 200 runs) | 1280 |
| Brand upgrades bought | 4 |
| Tier demands the shelf could not answer | 26 of 63 (41.3%) |
| Demanded upgrades bought | 4 |
| Demanded upgrades OFFERED | 30 |

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
| 0.5★ | 200 (100.0%) | 6 / 7 / 7 | 2 |
| 1.0★ | 199 (99.5%) | 13 / 15 / 17 | 3 |
| 1.5★ | 6 (3.0%) | 21 / 22 / 22 | 4 |
| 2.0★ | **none of 200** | — | — |
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
| 1 | 200 | 52 (26.0%) | 0 (0.0%) |
| 2 | 200 | 117 (58.5%) | 0 (0.0%) |
| 3 | 200 | 23 (11.5%) | 0 (0.0%) |
| 4 | 200 | 71 (35.5%) | 2 (1.0%) |
| 5 | 200 | 84 (42.0%) | 2 (1.0%) |
| 6 | 200 | 89 (44.5%) | 3 (1.5%) |
| 7 | 200 | 82 (41.0%) | 0 (0.0%) |
| 8 | 200 | 105 (52.5%) | 5 (2.5%) |
| 9 | 200 | 84 (42.0%) | 3 (1.5%) |
| 10 | 200 | 65 (32.5%) | 7 (3.5%) |
| 11 | 200 | 61 (30.5%) | 23 (11.5%) |
| 12 | 200 | 102 (51.0%) | 63 (31.5%) |
| 13 | 200 | 117 (58.5%) | 75 (37.5%) |
| 14 | 200 | 131 (65.5%) | 86 (43.0%) |
| 15 | 200 | 113 (56.5%) | 95 (47.5%) |
| 16 | 200 | 140 (70.0%) | 123 (61.5%) |
| 17 | 200 | 144 (72.0%) | 137 (68.5%) |
| 18 | 200 | 148 (74.0%) | 148 (74.0%) |
| 19 | 199 | 180 (90.5%) | 179 (89.9%) |
| 20 | 195 | 180 (92.3%) | 180 (92.3%) |
| 21 | 167 | 166 (99.4%) | 166 (99.4%) |
| 22 | 95 | 94 (98.9%) | 94 (98.9%) |
| 23 | 21 | 19 (90.5%) | 19 (90.5%) |
| 24 | 5 | 4 (80.0%) | 4 (80.0%) |
| 25 | 3 | 3 (100.0%) | 3 (100.0%) |
| 26 | 2 | 2 (100.0%) | 2 (100.0%) |
| 27 | 1 | 1 (100.0%) | 1 (100.0%) |
