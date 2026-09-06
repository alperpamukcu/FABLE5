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
| Final till p25/median/p75 | $67 / $77 / $86 |
| Avg income / expenses per day | $133.8 / $131.8 |
| Avg daily satisfaction | 60% |
| Storm-offs | 9310 (15.5%) |
| Customers per night | 10.0 |
| Served per bar-minute | 4.78 |
| Bar standing (avg night) | 2.73 stars |
| Serves Exact / Close / Wrong | 69697 (100.0%) / 29 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 1 (0.0%) / 305 |
| Take: base / tip | $409183 / $311775 (311775 (43.2%) of it tip) |
| Avg base / tip per serve | $5.87 / $4.47 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 31800 (100.0%) of 31801 |
| Garnish craft landed | 37525 (53.8%) |
| Extra orders earned (of serves) | 19158 (27.5%) |
| Extra orders earned (of exact) | 19158 (27.5%) |
| Pour accuracy on exact serves (avg) | 78% |
| PERFECT makes (of exact serves) | 1861 (2.7%) |
| Recipes revealed by run end (avg) | 1.3 |
| Draught share of serves | 5725 (8.2%) |
| Pints in the good head band | 5725 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23312 (33.4%) · $63924 |
| Glasses collected / wipes / washes | 44850 / 46337 / 44850 |
| Service (avg night) / comfort (avg night) | 2.98 / 3.11 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 2398 (40.0%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 (median) | 2.50 / 3.50 / 4.08 |
| Dressing rungs bought (by slot) | counter_end 198 · floor_rug 193 · plant_left 192 · table_left 173 · table_mid 102 · table_right 62 |
| Minors met / shown the door / served (of seats) | 3410 / 3410 / 0 (5.4% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 · 2★ $0.00 · 3★ $0.00 |
| State's thanks (total · of income) | $17050 · 2.1% |
| Recipes bought (of 200 runs) | 3253 |
| Brand upgrades bought | 724 |
| Tier demands the shelf could not answer | 83 of 12700 (0.7%) |
| Demanded upgrades bought | 724 |
| Demanded upgrades OFFERED | 1199 |

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
| 1.5★ | 200 (100.0%) | 11 / 11 / 12 | 2 |
| 2.0★ | 199 (99.5%) | 15 / 16 / 16 | 3 |
| 2.5★ | 198 (99.0%) | 20 / 21 / 22 | 4 |
| 3.0★ | 50 (25.0%) | 28 / 29 / 30 | 5 |
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
| 7 | 200 | 63 (31.5%) | 0 (0.0%) |
| 8 | 200 | 103 (51.5%) | 0 (0.0%) |
| 9 | 200 | 107 (53.5%) | 0 (0.0%) |
| 10 | 200 | 87 (43.5%) | 0 (0.0%) |
| 11 | 200 | 55 (27.5%) | 0 (0.0%) |
| 12 | 200 | 54 (27.0%) | 0 (0.0%) |
| 13 | 200 | 83 (41.5%) | 0 (0.0%) |
| 14 | 200 | 104 (52.0%) | 0 (0.0%) |
| 15 | 200 | 128 (64.0%) | 0 (0.0%) |
| 16 | 200 | 132 (66.0%) | 1 (0.5%) |
| 17 | 200 | 130 (65.0%) | 4 (2.0%) |
| 18 | 200 | 116 (58.0%) | 6 (3.0%) |
| 19 | 200 | 98 (49.0%) | 11 (5.5%) |
| 20 | 200 | 92 (46.0%) | 11 (5.5%) |
| 21 | 200 | 104 (52.0%) | 27 (13.5%) |
| 22 | 200 | 111 (55.5%) | 31 (15.5%) |
| 23 | 200 | 96 (48.0%) | 25 (12.5%) |
| 24 | 199 | 29 (14.6%) | 14 (7.0%) |
| 25 | 199 | 68 (34.2%) | 21 (10.6%) |
| 26 | 199 | 93 (46.7%) | 22 (11.1%) |
| 27 | 199 | 83 (41.7%) | 9 (4.5%) |
| 28 | 199 | 81 (40.7%) | 20 (10.1%) |
| 29 | 199 | 94 (47.2%) | 23 (11.6%) |
| 30 | 198 | 82 (41.4%) | 22 (11.1%) |
