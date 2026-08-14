# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 30 days, one drink per 9s of bar time.
Floor bot: serves the named order at band midpoints, pulls a pint
leaned over then straightened, and shops — stock, recipes, stools,
glass steps, and one brand upgrade a night it never once affords.
Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 0 (0.0%) |
| Reached the 30-day horizon | 200 (100.0%) |
| Days survived p25/median/p75 | 30 / 30 / 30 |
| Final till p25/median/p75 | $298 / $351 / $414 |
| Avg income / expenses per day | $177.2 / $165.5 |
| Avg daily satisfaction | 59% |
| Storm-offs | 11281 (18.0%) |
| Customers per night | 10.5 |
| Served per bar-minute | 4.95 |
| Bar standing (avg night) | 2.85 stars |
| Serves Exact / Close / Wrong | 71975 (100.0%) / 0 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 9 |
| Take: base / tip | $559950 / $437024 (437024 (43.8%) of it tip) |
| Avg base / tip per serve | $7.78 / $6.07 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 33027 (100.0%) of 33027 |
| Garnish craft landed | 38697 (53.8%) |
| Extra orders earned (of serves) | 20477 (28.5%) |
| Extra orders earned (of exact) | 20477 (28.5%) |
| Draught share of serves | 5670 (7.9%) |
| Pints in the good head band | 5670 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 24059 (33.4%) · $65980 |
| Glasses bussed | 51707 |
| Recipes bought (of 200 runs) | 3797 |
| Brand upgrades bought | 1000 |
| Tier demands the shelf could not answer | 0 of 16106 (0.0%) |
| Demanded upgrades bought | 402 |
| Demanded upgrades OFFERED | 1467 |

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
| 3.0★ | 102 (51.0%) | 28 / 29 / 30 | 5 |
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
| 2 | 200 | 85 (42.5%) |
| 3 | 200 | 63 (31.5%) |
| 4 | 200 | 102 (51.0%) |
| 5 | 200 | 43 (21.5%) |
| 6 | 200 | 4 (2.0%) |
| 7 | 200 | 4 (2.0%) |
| 8 | 200 | 50 (25.0%) |
| 9 | 200 | 54 (27.0%) |
| 10 | 200 | 24 (12.0%) |
| 11 | 200 | 0 (0.0%) |
| 12 | 200 | 0 (0.0%) |
| 13 | 200 | 2 (1.0%) |
| 14 | 200 | 10 (5.0%) |
| 15 | 200 | 32 (16.0%) |
