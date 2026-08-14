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
| Final till p25/median/p75 | $263 / $286 / $303 |
| Avg income / expenses per day | $157.8 / $148.7 |
| Avg daily satisfaction | 54% |
| Storm-offs | 10407 (16.5%) |
| Customers per night | 10.5 |
| Served per bar-minute | 4.97 |
| Bar standing (avg night) | 2.60 stars |
| Serves Exact / Close / Wrong | 66462 (92.7%) / 0 (0.0%) / 5261 (7.3%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 9 |
| Take: base / tip | $494729 / $386357 (386357 (43.9%) of it tip) |
| Avg base / tip per serve | $6.90 / $5.39 |
| Avg spec score / fill score | 100% / 99% |
| Orders with a serving spec, fully met | 32748 (100.0%) of 32748 |
| Garnish craft landed | 38568 (53.8%) |
| Extra orders earned (of serves) | 19185 (26.7%) |
| Extra orders earned (of exact) | 19185 (28.9%) |
| Draught share of serves | 5820 (8.1%) |
| Pints in the good head band | 5820 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 23976 (33.4%) · $65756 |
| Glasses bussed | 52747 |
| Recipes bought (of 200 runs) | 3728 |
| Brand upgrades bought | 999 |
| Tier demands the shelf could not answer | 0 of 15068 (0.0%) |
| Demanded upgrades bought | 411 |
| Demanded upgrades OFFERED | 411 |

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
| 2.0★ | 200 (100.0%) | 14 / 15 / 16 | 3 |
| 2.5★ | 186 (93.0%) | 22 / 24 / 26 | 4 |
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
| 10 | 200 | 64 (32.0%) |
| 11 | 200 | 1 (0.5%) |
| 12 | 200 | 4 (2.0%) |
| 13 | 200 | 4 (2.0%) |
| 14 | 200 | 31 (15.5%) |
| 15 | 200 | 79 (39.5%) |
