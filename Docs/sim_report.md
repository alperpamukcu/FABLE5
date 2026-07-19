# Sim report — emotion pivot balance

Runs: **300** of 300 requested

Greedy one-ply bot reading only the ID, buying nothing in the Back
Room. Every survival figure is therefore a floor.

| Metric | Value |
|---|---|
| Runs won | 75 (25,0%) |
| Runs lost to quota | 225 (75,0%) |
| Avg night reached | 6,63 |
| Customers served | 5970 |
| Orders filled (score target) | 1593 (26,7%) |
| Satisfaction per customer | 1,95 / 3 |
| Mixes | 20543 |
| Bust rate | 5285 (25,7%) |
| Clean Serve rate | 770 (3,7%) |
| Blind-read mixes | 5026 (24,5%) |

## Weekly quota gate

| Week | Required | Attempts | Passed |
|---|---|---|---|
| 1 | 7 | 300 | 282 (94,0%) |
| 2 | 10 | 282 | 240 (85,1%) |
| 3 | 11 | 240 | 173 (72,1%) |
| 4 | 12 | 173 | 75 (43,4%) |

## How hard customers were to please

| Demand | Customers | Satisfaction each |
|---|---|---|
| Easygoing | 1340 (22,4%) | 1,90 |
| Particular | 2424 (40,6%) | 2,00 |
| Demanding | 2206 (37,0%) | 1,94 |

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
| 1 | 8 / 10 / 12 | 97% | 94% | 84% | 68% | 56% | 41% | 28% | 18% | 12% | 6% | 3% | 2% | 1% |
| 2 | 10 / 12 / 15 | 99% | 97% | 95% | 92% | 85% | 73% | 62% | 49% | 38% | 28% | 19% | 12% | 8% |
| 3 | 10 / 12 / 14 | 98% | 96% | 94% | 88% | 79% | 72% | 60% | 49% | 34% | 24% | 19% | 13% | 11% |
| 4 | 9 / 11 / 14 | 97% | 94% | 89% | 80% | 69% | 55% | 43% | 30% | 25% | 20% | 16% | 9% | 8% |
