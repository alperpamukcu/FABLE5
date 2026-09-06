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
| Bankruptcies | 0 (0.0%) |
| Reached the 30-day horizon | 200 (100.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $69 / $85 / $112 |
| Avg income / expenses per day | $137.6 / $135.0 |
| Avg daily satisfaction | 60% |
| Storm-offs | 9455 (15.7%) |
| Customers per night | 10.0 |
| Served per bar-minute | 4.78 |
| Bar standing (avg night) | 2.76 stars |
| Serves Exact / Close / Wrong | 69837 (100.0%) / 29 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 156 |
| Take: base / tip | $415076 / $315983 (315983 (43.2%) of it tip) |
| Avg base / tip per serve | $5.94 / $4.52 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 32019 (100.0%) of 32019 |
| Garnish craft landed | 37697 (54.0%) |
| Extra orders earned (of serves) | 19261 (27.6%) |
| Extra orders earned (of exact) | 19261 (27.6%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 2062 (3.0%) |
| Recipes revealed by run end (avg) | 1.4 |
| Draught share of serves | 5678 (8.1%) |
| Pints in the good head band | 5678 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23357 (33.4%) · $64038 |
| Glasses collected / wipes / washes | 44865 / 46499 / 44865 |
| Service (avg night) / comfort (avg night) | 2.99 / 3.20 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 2049 (34.2%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 2.55 / 3.58 / 4.48 |
| Dressing rungs bought (by slot) | counter_end 200 · floor_rug 200 · plant_left 198 · sink 1 · table_left 245 · table_mid 173 · table_right 127 |
| Minors met / shown the door / served (of seats) | 3417 / 3417 / 0 (5.4% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 · 3★ $0.00 |
| State's thanks (total · of income) | $30301 · 3.7% |
| Recipes bought (of 200 runs) | 3372 |
| Brand upgrades bought | 767 |
| Tier demands the shelf could not answer | 70 of 13188 (0.5%) |
| Demanded upgrades bought | 767 |
| Demanded upgrades OFFERED | 1309 |

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
| 0.5★ | 200 (100.0%) | 4 / 4 / 4 | 1 |
| 1.0★ | 200 (100.0%) | 7 / 7 / 7 | 2 |
| 1.5★ | 200 (100.0%) | 11 / 11 / 11 | 2 |
| 2.0★ | 200 (100.0%) | 15 / 15 / 16 | 3 |
| 2.5★ | 200 (100.0%) | 20 / 21 / 21 | 4 |
| 3.0★ | 59 (29.5%) | 28 / 29 / 30 | 5 |
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
| 2 | 200 | 61 (30.5%) | 0 (0.0%) |
| 3 | 200 | 72 (36.0%) | 0 (0.0%) |
| 4 | 200 | 69 (34.5%) | 0 (0.0%) |
| 5 | 200 | 90 (45.0%) | 0 (0.0%) |
| 6 | 200 | 61 (30.5%) | 0 (0.0%) |
| 7 | 200 | 61 (30.5%) | 0 (0.0%) |
| 8 | 200 | 105 (52.5%) | 0 (0.0%) |
| 9 | 200 | 106 (53.0%) | 0 (0.0%) |
| 10 | 200 | 80 (40.0%) | 0 (0.0%) |
| 11 | 200 | 44 (22.0%) | 0 (0.0%) |
| 12 | 200 | 73 (36.5%) | 0 (0.0%) |
| 13 | 200 | 85 (42.5%) | 0 (0.0%) |
| 14 | 200 | 106 (53.0%) | 0 (0.0%) |
| 15 | 200 | 127 (63.5%) | 0 (0.0%) |
| 16 | 200 | 123 (61.5%) | 1 (0.5%) |
| 17 | 200 | 134 (67.0%) | 4 (2.0%) |
| 18 | 200 | 108 (54.0%) | 3 (1.5%) |
| 19 | 200 | 97 (48.5%) | 6 (3.0%) |
| 20 | 200 | 105 (52.5%) | 12 (6.0%) |
| 21 | 200 | 113 (56.5%) | 25 (12.5%) |
| 22 | 200 | 91 (45.5%) | 14 (7.0%) |
| 23 | 200 | 101 (50.5%) | 12 (6.0%) |
| 24 | 200 | 25 (12.5%) | 9 (4.5%) |
| 25 | 200 | 73 (36.5%) | 17 (8.5%) |
| 26 | 200 | 85 (42.5%) | 12 (6.0%) |
| 27 | 200 | 89 (44.5%) | 11 (5.5%) |
| 28 | 200 | 85 (42.5%) | 12 (6.0%) |
| 29 | 200 | 87 (43.5%) | 12 (6.0%) |
| 30 | 200 | 83 (41.5%) | 18 (9.0%) |
