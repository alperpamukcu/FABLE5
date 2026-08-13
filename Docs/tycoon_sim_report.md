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
| Final till p25/median/p75 | $175 / $208 / $228 |
| Avg income / expenses per day | $153.8 / $147.5 |
| Avg daily satisfaction | 59% |
| Storm-offs | 11244 (17.9%) |
| Customers per night | 10.5 |
| Served per bar-minute | 4.91 |
| Bar standing (avg night) | 2.83 stars |
| Serves Exact / Close / Wrong | 72064 (100.0%) / 0 (0.0%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 0 (0.0%) / 2 |
| Take: base / tip | $480636 / $376134 (376134 (43.9%) of it tip) |
| Avg base / tip per serve | $6.67 / $5.22 |
| Avg spec score / fill score | 100% / 100% |
| Orders with a serving spec, fully met | 33048 (100.0%) of 33048 |
| Garnish craft landed | 38686 (53.7%) |
| Extra orders earned (of serves) | 20498 (28.4%) |
| Extra orders earned (of exact) | 20498 (28.4%) |
| Draught share of serves | 5638 (7.8%) |
| Pints in the good head band | 5638 (100.0%) |
| Average head poured | 18% |
| Snack serves (of serves) | 24022 (33.3%) · $66059 |
| Glasses bussed | 52371 |
| Recipes bought (of 200 runs) | 3797 |

## The written nights (GDD 26)

The bot starts the trial the moment it reaches the stool (it has no
dialogue to read), pours every ask to the trial's own fill standard, and
says an honest no when the shelf cannot make one. None of this touches
the numbers above: a guest of the house is not a customer.

| Measure | Value |
|---|---|
| Trials walked in | 803 |
| Drinks poured for them | 1606 |
| Passed / failed / declined | 800 / 3 / 0 |
| Arcs finished inside 30 nights | 200 (100.0%) |

| What came back, and why | Drinks |
|---|---|
| gimlet: not the drink (Wrong) — highball 0.54/1.00 [gin_boothby=0.63 lime_fresh=0.23 syrup_house=0.15] | 3 |

## Red days by day number

| Day | Closed | In the red |
|---|---|---|
| 1 | 200 | 0 (0.0%) |
| 2 | 200 | 85 (42.5%) |
| 3 | 200 | 84 (42.0%) |
| 4 | 200 | 81 (40.5%) |
| 5 | 200 | 33 (16.5%) |
| 6 | 200 | 2 (1.0%) |
| 7 | 200 | 4 (2.0%) |
| 8 | 200 | 19 (9.5%) |
| 9 | 200 | 17 (8.5%) |
| 10 | 200 | 29 (14.5%) |
| 11 | 200 | 0 (0.0%) |
| 12 | 200 | 2 (1.0%) |
| 13 | 200 | 38 (19.0%) |
| 14 | 200 | 125 (62.5%) |
| 15 | 200 | 99 (49.5%) |
