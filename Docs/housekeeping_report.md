# The room's four shapes — GDD 27 §7

100 runs a shape, 30-day horizon, the SAME seeds in every row.
The counter's mess is on for every row (the sim measures the whole rule);
what differs is how the bot keeps it and whether it buys the room's dressing.

| Shape | bankrupt | till p50 | cust/night | service | comfort | clean | comfort-bound | broke nights | standing | 2.5★ reached | 3.0★ reached |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 · instant, no dressing | 0.0% | $134 | 10.2 | 2.96 | 2.85 | 100% | 64.1% | 0.0% | 2.65 | 100.0% | 2.0% |
| 2 · instant, buys dressing | 1.0% | $76 | 10.2 | 2.94 | 3.05 | 100% | 42.1% | 0.0% | 2.68 | 100.0% | 6.0% |
| 3 · never wipes or washes | 6.0% | $64 | 10.2 | 2.90 | 2.67 | 53% | 71.0% | 0.0% | 2.44 | 84.0% | 0.0% |
| 4a · 10 s to the mess | 1.0% | $73 | 10.2 | 2.94 | 3.05 | 100% | 41.3% | 0.0% | 2.69 | 99.0% | 8.0% |
| 4b · 20 s to the mess | 1.0% | $76 | 10.0 | 2.97 | 2.96 | 91% | 53.5% | 0.0% | 2.66 | 99.0% | 6.0% |
| 4c · 30 s to the mess | 8.0% | $69 | 9.4 | 2.98 | 2.73 | 82% | 72.3% | 0.0% | 2.51 | 83.0% | 7.0% |

Read across: shape 1 against the checked-in floor is the cost of halving the
glass share; shape 2 is the new floor; shape 3 is the rot (comfort near the
free base less the penalty, standing stalled, broke nights NOT up, because
the crowd reads the service side); shape 4 is the human, and DirtPenalty is
picked so that 10–20 s hands lose a tenth of a star, not a whole one.
