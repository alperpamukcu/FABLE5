# Sim report — emotion pivot balance

Runs: **300** of 300 requested

Greedy one-ply bot reading only the ID, buying nothing in the Back
Room. Every survival figure is therefore a floor.

| Metric | Value |
|---|---|
| Runs won | 87 (29,0%) |
| Runs lost to quota | 213 (71,0%) |
| Avg night reached | 6,46 |
| Customers served | 5814 |
| Orders filled (score target) | 1590 (27,3%) |
| Satisfaction per customer | 2,10 / 3 |
| Mixes | 19921 |
| Bust rate | 5099 (25,6%) |
| Clean Serve rate | 759 (3,8%) |
| Blind-read mixes | 4868 (24,4%) |

## Weekly quota gate

| Week | Required | Attempts | Passed |
|---|---|---|---|
| 1 | 7 | 300 | 279 (93,0%) |
| 2 | 11 | 279 | 222 (79,6%) |
| 3 | 12 | 222 | 168 (75,7%) |
| 4 | 14 | 168 | 87 (51,8%) |

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
| 1 | 8 / 9 / 12 | 97% | 93% | 83% | 68% | 49% | 37% | 25% | 14% | 8% | 5% | 3% | 2% | 1% |
| 2 | 11 / 13 / 15 | 99% | 98% | 96% | 94% | 91% | 80% | 69% | 59% | 47% | 34% | 24% | 18% | 12% |
| 3 | 12 / 14 / 16 | 99% | 97% | 95% | 94% | 90% | 85% | 76% | 63% | 52% | 41% | 30% | 20% | 16% |
| 4 | 12 / 14 / 16 | 100% | 99% | 98% | 93% | 88% | 84% | 76% | 62% | 52% | 39% | 33% | 23% | 17% |
