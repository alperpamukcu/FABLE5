# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: serves the named order at band midpoints, pulls a pint
leaned over then straightened, and never buys brands.
Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 0 (0.0%) |
| Reached the 30-day horizon | 200 (100.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $140 / $166 / $196 |
| Avg income / expenses per day | $150.2 / $145.2 |
| Avg daily satisfaction | 58% |
| Storm-offs | 11250 (17.9%) |
| Customers per night | 10.5 |
| Served per bar-minute | 4.94 |
| Bar standing (avg night) | 2.81 stars |
| Serves Exact / Close / Wrong | 72027 (100.0%) / 0 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 0 |
| Take: base / tip | $468599 / $366760 (366760 (43.9%) of it tip) |
| Avg base / tip per serve | $6.51 / $5.09 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 33035 (100.0%) of 33035 |
| Garnish craft landed | 38712 (53.7%) |
| Extra orders earned (of serves) | 20516 (28.5%) |
| Extra orders earned (of exact) | 20516 (28.5%) |
| Draught share of serves | 5677 (7.9%) |
| Pints in the good head band | 5677 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 24009 (33.3%) · $66024 |
| Glasses bussed | 51711 |
| Recipes bought (of 200 runs) | 3797 |

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
| 1.5★ | 200 (100.0%) | 10 / 10 / 11 | 2 |
| 2.0★ | 200 (100.0%) | 13 / 14 / 14 | 3 |
| 2.5★ | 200 (100.0%) | 19 / 19 / 20 | 4 |
| 3.0★ | 30 (15.0%) | 29 / 29 / 29 | 5 |
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

| Day | Closed | In the red |
|---|---|---|
| 1 | 200 | 0 (0.0%) |
| 2 | 200 | 83 (41.5%) |
| 3 | 200 | 80 (40.0%) |
| 4 | 200 | 91 (45.5%) |
| 5 | 200 | 25 (12.5%) |
| 6 | 200 | 3 (1.5%) |
| 7 | 200 | 3 (1.5%) |
| 8 | 200 | 15 (7.5%) |
| 9 | 200 | 15 (7.5%) |
| 10 | 200 | 31 (15.5%) |
| 11 | 200 | 0 (0.0%) |
| 12 | 200 | 1 (0.5%) |
| 13 | 200 | 46 (23.0%) |
| 14 | 200 | 123 (61.5%) |
| 15 | 200 | 111 (55.5%) |
