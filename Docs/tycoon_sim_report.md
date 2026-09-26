# Tycoon sim report — GDD 23 balance

Runs: **200** of 200, horizon 90 days, one drink per 9s of bar time.
Floor bot: aims each ingredient at the middle of its lit 20-point box
(the revealed perfect once a page is perfected), pulls a pint
leaned over then straightened, keeps the counter the instant a mess
lands (collect, wipe, wash), and shops — stock, recipes, the night's one
fitting (the cheapest of a stool, a bar-top step or a glass step), the room's
dressing by the comfort it ADDS per dollar while the room is short of the
shop's stars plus half a star, and one brand upgrade a night behind a fat
cushion. Every survival figure is a floor.

| Metric | Value |
|---|---|
| Bankruptcies | 200 (100.0%) |
| Reached the 90-day horizon | 0 (0.0%) |
| Days survived p25/median/p75 | 18 / 22 / 22 |
| Final till p25/median/p75 | $-149 / $-102 / $-75 |
| Avg income / expenses per day | $75.9 / $83.9 |
| Avg daily satisfaction | 49% |
| Storm-offs | 5155 (13.9%) |
| Customers per night | 9.1 |
| Served per bar-minute | 4.42 |
| Bar standing (avg night) | 0.52 stars |
| Serves Exact / Close / Wrong | 42139 (99.5%) / 149 (0.4%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 80 (0.2%) / 1406 |
| Take: base / tip | $165267 / $136638 (136638 (45.3%) of it tip) |
| Avg base / tip per serve | $3.90 / $3.23 |
| Avg spec score / fill score | 100% / 93% |
| Orders with a serving spec, fully met | 18761 (99.7%) of 18812 |
| Garnish craft landed | 23410 (55.3%) |
| Extra orders earned (of serves) | 11879 (28.0%) |
| Extra orders earned (of exact) | 11879 (28.2%) |
| Pour accuracy on exact serves (avg) | 76% |
| PERFECT makes (of exact serves) | 16 (0.0%) |
| Recipes revealed by run end (avg) | 0.1 |
| Draught share of serves | 4649 (11.0%) |
| Pints in the good head band | 4649 (100.0%) |
| Average head poured | 18% |
| Glasses collected / wipes / washes | 30818 / 31955 / 30818 |
| Service (avg night) / comfort (avg night) | 2.47 / 0.56 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 4060 (100.0%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 / 40 / 50 (median) | 0.73 / 0.88 / — / — / — |
| Night the room first reached 5.00, p25/median/p75 | **none of 200** |
| Night the standing first reached 5★, p25/median/p75 | **none of 200** |
| Nights the room held the night, by week | w1 100% · w2 100% · w3 100% · w4 99% |
| Bar-top steps bought | 200 |
| Dressing rungs bought (by slot) | ceiling 200 · floor 157 · floor_rug 200 · wall_center 76 · wall_lamps 200 · walls 198 · walls_right 2 |
| Fitting buffs bought (by kind) | patience 200 · refill 200 · tip 200 · arrivals 198 · grace 157 · service 76 · comfort 2 |
| High rollers drawn / nights at 4★+ (of nights) | 0 (0.0%) / 0 (0.0%) |
| Minors met / shown the door / served (of seats) | 156 / 156 / 0 (0.4% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 |
| Walk-out compensation (total · per walk-out) | $14449 · $2.80 |
| State's thanks (total · of income) | $6184 · 2.0% |
| Recipes bought (of 200 runs) | 800 |
| Brand upgrades bought | 0 |
| Tier demands the shelf could not answer | 0 of 0 (0.0%) |
| Demanded upgrades bought | 0 |
| Demanded upgrades OFFERED | 0 |

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
| 0.5★ | 200 (100.0%) | 10 / 11 / 11 | 2 |
| 1.0★ | 76 (38.0%) | 13 / 14 / 15 | 3 |
| 1.5★ | **none of 200** | — | — |
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
| Arcs finished inside 90 nights | 200 (100.0%) |

## Red days by day number

Two columns because there are two ways to end a night behind: the
takings failed to cover rent and stock, or they covered it and the bar
went shopping. Only the second column is trouble.

| Day | Closed | In the red | Red before shopping |
|---|---|---|---|
| 1 | 200 | 109 (54.5%) | 0 (0.0%) |
| 2 | 200 | 79 (39.5%) | 0 (0.0%) |
| 3 | 200 | 64 (32.0%) | 0 (0.0%) |
| 4 | 200 | 97 (48.5%) | 0 (0.0%) |
| 5 | 200 | 74 (37.0%) | 0 (0.0%) |
| 6 | 200 | 80 (40.0%) | 0 (0.0%) |
| 7 | 200 | 96 (48.0%) | 0 (0.0%) |
| 8 | 200 | 64 (32.0%) | 0 (0.0%) |
| 9 | 200 | 64 (32.0%) | 0 (0.0%) |
| 10 | 200 | 87 (43.5%) | 0 (0.0%) |
| 11 | 200 | 102 (51.0%) | 0 (0.0%) |
| 12 | 200 | 93 (46.5%) | 4 (2.0%) |
| 13 | 200 | 103 (51.5%) | 25 (12.5%) |
| 14 | 200 | 142 (71.0%) | 75 (37.5%) |
| 15 | 200 | 143 (71.5%) | 91 (45.5%) |
| 16 | 200 | 171 (85.5%) | 115 (57.5%) |
| 17 | 191 | 174 (91.1%) | 145 (75.9%) |
| 18 | 150 | 147 (98.0%) | 132 (88.0%) |
| 19 | 134 | 133 (99.3%) | 128 (95.5%) |
| 20 | 128 | 124 (96.9%) | 117 (91.4%) |
| 21 | 122 | 118 (96.7%) | 115 (94.3%) |
| 22 | 101 | 98 (97.0%) | 97 (96.0%) |
| 23 | 29 | 29 (100.0%) | 29 (100.0%) |
| 24 | 6 | 6 (100.0%) | 6 (100.0%) |
