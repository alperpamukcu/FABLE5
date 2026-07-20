# Sim report — emotion pivot balance

Runs: **300** of 300 requested

Greedy one-ply bot reading only the ID; it refills the well but buys
nothing else in the Back Room. Every survival figure is therefore a floor.

| Metric | Value |
|---|---|
| Runs won | 45 (15,0%) |
| Runs lost to quota | 255 (85,0%) |
| Avg night reached | 7,42 |
| Customers served | 6678 |
| Orders filled (score target) | 1771 (26,5%) |
| Satisfaction per customer | 2,26 / 3 |
| Mixes | 23574 |
| Bust rate | 1851 (7,9%) |
| Clean Serve rate | 202 (0,9%) |
| Blind-read mixes | 5588 (23,7%) |

## Weekly quota gate

| Week | Required | Attempts | Passed |
|---|---|---|---|
| 1 | 7 | 300 | 300 (100,0%) |
| 2 | 10 | 300 | 299 (99,7%) |
| 3 | 11 | 299 | 214 (71,6%) |
| 4 | 12 | 214 | 45 (21,0%) |

## How hard customers were to please

| Demand | Customers | Satisfaction each |
|---|---|---|
| Easygoing | 1345 (20,1%) | 2,69 |
| Particular | 2683 (40,2%) | 3,15 |
| Demanding | 2650 (39,7%) | 1,15 |

## Quota sweep (what each requirement would have passed)

Measured from the earned-satisfaction distribution. A week that
passes at ~100% is not a gate, it is a formality.

**Survivorship warning:** week N only contains runs that already
cleared weeks 1..N-1, so later rows are drawn from a progressively
stronger population. Raising an early requirement culls weak runs
and *raises* the later rows — these columns are not independent,
and multiplying them together will understate the real win rate.

| Week | earned p25 / median / p75 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 12 / 13 / 14 | 100% | 100% | 100% | 100% | 99% | 95% | 86% | 66% | 42% | 23% | 14% | 8% | 4% |
| 2 | 16 / 19 / 20 | 100% | 100% | 100% | 100% | 100% | 98% | 96% | 92% | 88% | 83% | 76% | 64% | 59% |
| 3 | 10 / 13 / 17 | 93% | 91% | 89% | 82% | 76% | 72% | 67% | 53% | 48% | 42% | 37% | 28% | 23% |
| 4 | 4 / 8 / 11 | 70% | 64% | 55% | 37% | 32% | 26% | 21% | 14% | 11% | 8% | 4% | 2% | 2% |
