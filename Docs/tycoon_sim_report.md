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
| Days survived p25/median/p75 | 17 / 21 / 22 |
| Final till p25/median/p75 | $-159 / $-115 / $-87 |
| Avg income / expenses per day | $75.6 / $84.2 |
| Avg daily satisfaction | 49% |
| Storm-offs | 5150 (14.1%) |
| Customers per night | 9.1 |
| Served per bar-minute | 4.41 |
| Bar standing (avg night) | 0.52 stars |
| Serves Exact / Close / Wrong | 41657 (99.5%) / 144 (0.3%) / 0 (0.0%) |
| Refused (too little in the glass) / declined | 81 (0.2%) / 1315 |
| Take: base / tip | $163917 / $135734 (135734 (45.3%) of it tip) |
| Avg base / tip per serve | $3.91 / $3.24 |
| Avg spec score / fill score | 100% / 93% |
| Orders with a serving spec, fully met | 18617 (99.7%) of 18669 |
| Garnish craft landed | 23200 (55.4%) |
| Extra orders earned (of serves) | 11798 (28.2%) |
| Extra orders earned (of exact) | 11798 (28.3%) |
| Pour accuracy on exact serves (avg) | 76% |
| PERFECT makes (of exact serves) | 12 (0.0%) |
| Recipes revealed by run end (avg) | 0.1 |
| Draught share of serves | 4583 (10.9%) |
| Pints in the good head band | 4583 (100.0%) |
| Average head poured | 18% |
| Glasses collected / wipes / washes | 30404 / 31521 / 30404 |
| Service (avg night) / comfort (avg night) | 2.47 / 0.57 |
| Avg cleanliness | 100% |
| Nights comfort-bound (room under service) | 4016 (100.0%) |
| Broke crowd drawn (of nights) | 0 (0.0%) |
| Comfort base by day 10 / 20 / 30 / 40 / 50 (median) | 0.73 / 0.88 / — / — / — |
| Night the room first reached 5.00, p25/median/p75 | **none of 200** |
| Night the standing first reached 5★, p25/median/p75 | **none of 200** |
| Nights the room held the night, by week | w1 100% · w2 100% · w3 100% · w4 99% |
| Bar-top steps bought | 200 |
| Dressing rungs bought (by slot) | ceiling 200 · floor 157 · floor_rug 200 · wall_center 82 · wall_lamps 200 · walls 198 · walls_right 2 |
| Fitting buffs bought (by kind) | patience 200 · refill 200 · tip 200 · arrivals 198 · grace 157 · service 82 · comfort 2 |
| High rollers drawn / nights at 4★+ (of nights) | 0 (0.0%) / 0 (0.0%) |
| Minors met / shown the door / served (of seats) | 170 / 170 / 0 (0.5% of seats) |
| Wrong kicks / cards misread | 0 / 0 |
| Fines paid (total · per night at 0/1/2/3★) | $0 · 0★ $0.00 · 1★ $0.00 |
| Walk-out compensation (total · per walk-out) | $14467 · $2.81 |
| State's thanks (total · of income) | $1700 · 0.6% |
| Hostess's pay for finished jobs (total · of income) | $2400 · 0.8% |
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
| 1.0★ | 82 (41.0%) | 13 / 14 / 14 | 3 |
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

## The hostess's book (2026-09-27)

The bot never hears her: Core hands each job over itself once she has stood four
floor-seconds unheard, so this is the chain as a headless run plays it. A job is
counted from the hand-over and she comes the night after it is done. The floor bot
pours band midpoints and makes almost no PERFECT pours, so it is expected to stand
on `steady_hands`; the table says where it stands, nothing is tuned around it.

| Measure | Value |
|---|---|
| Jobs handed / done / skipped per run (median) | 2 / 1 / 0 |
| Job on the bar at night 10, p25/median/p75 (row of 18) | 2 / 2 / 2 (200 runs) |
| Job on the bar at night 20, p25/median/p75 (row of 18) | 2 / 2 / 2 (121 runs) |
| Job on the bar at night 30, p25/median/p75 (row of 18) | — |

| Job | Runs that finished it | Night first done, p25/median/p75 | Skipped in |
|---|---|---|---|
| 1. first_wage (serve, rung 0, $12) | 200 (100.0%) | 3 / 3 / 4 | — |
| 2. steady_hands (perfect, rung 0, $20) | 0 (0.0%) | — | — |
| 3. clean_night (clean, rung 0, $24) | 0 (0.0%) | — | — |
| 4. something_on_the_walls (fit, rung 0, $24) | 0 (0.0%) | — | — |
| 5. talk_of_the_street (rank, rung 0, $24) | 0 (0.0%) | — | — |
| 6. cold_and_twisted (garnish, rung 1, $28) | 0 (0.0%) | — | — |
| 7. a_room_worth_sitting_in (comfort, rung 1, $28) | 0 (0.0%) | — | — |
| 8. the_blocks_best (rank, rung 1, $28) | 0 (0.0%) | — | — |
| 9. an_eye_on_the_door (door, rung 2, $64) | 0 (0.0%) | — | — |
| 10. on_the_rim (garnish, rung 2, $64) | 0 (0.0%) | — | — |
| 11. house_proud (comfort, rung 2, $64) | 0 (0.0%) | — | — |
| 12. talk_of_the_town (rank, rung 2, $72) | 0 (0.0%) | — | — |
| 13. stirred_not_shaken (serve, rung 3, $84) | 0 (0.0%) | — | — |
| 14. a_proper_head (pints, rung 3, $108) | 0 (0.0%) | — | — |
| 15. the_citys_best (rank, rung 3, $120) | 0 (0.0%) | — | — |
| 16. the_top_shelf (serve, rung 4, $128) | 0 (0.0%) | — | — |
| 17. the_countrys_best (rank, rung 4, $176) | 0 (0.0%) | — | — |
| 18. the_best_there_is (rank, rung 5, $220) | 0 (0.0%) | — | — |

| Where the runs stood when the nights ran out | Runs |
|---|---|
| steady_hands | 200 (100.0%) |

## Red days by day number

Two columns because there are two ways to end a night behind: the
takings failed to cover rent and stock, or they covered it and the bar
went shopping. Only the second column is trouble.

| Day | Closed | In the red | Red before shopping |
|---|---|---|---|
| 1 | 200 | 109 (54.5%) | 0 (0.0%) |
| 2 | 200 | 63 (31.5%) | 0 (0.0%) |
| 3 | 200 | 60 (30.0%) | 0 (0.0%) |
| 4 | 200 | 94 (47.0%) | 0 (0.0%) |
| 5 | 200 | 65 (32.5%) | 0 (0.0%) |
| 6 | 200 | 87 (43.5%) | 0 (0.0%) |
| 7 | 200 | 99 (49.5%) | 0 (0.0%) |
| 8 | 200 | 54 (27.0%) | 0 (0.0%) |
| 9 | 200 | 63 (31.5%) | 0 (0.0%) |
| 10 | 200 | 86 (43.0%) | 0 (0.0%) |
| 11 | 200 | 101 (50.5%) | 0 (0.0%) |
| 12 | 200 | 86 (43.0%) | 6 (3.0%) |
| 13 | 200 | 127 (63.5%) | 33 (16.5%) |
| 14 | 200 | 143 (71.5%) | 79 (39.5%) |
| 15 | 200 | 146 (73.0%) | 97 (48.5%) |
| 16 | 200 | 167 (83.5%) | 116 (58.0%) |
| 17 | 189 | 180 (95.2%) | 149 (78.8%) |
| 18 | 142 | 139 (97.9%) | 127 (89.4%) |
| 19 | 126 | 125 (99.2%) | 119 (94.4%) |
| 20 | 121 | 121 (100.0%) | 118 (97.5%) |
| 21 | 115 | 115 (100.0%) | 115 (100.0%) |
| 22 | 94 | 94 (100.0%) | 94 (100.0%) |
| 23 | 29 | 29 (100.0%) | 29 (100.0%) |
| 24 | 2 | 2 (100.0%) | 2 (100.0%) |
