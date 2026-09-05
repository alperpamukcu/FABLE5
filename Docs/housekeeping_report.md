# The room's four shapes — GDD 27 §7

100 runs a shape, 30-day horizon, the SAME seeds in every row.
The counter's mess is on for every row (the sim measures the whole rule);
what differs is how the bot keeps it and whether it buys the room's dressing.

| Shape | bankrupt | till p50 | cust/night | service | comfort | clean | comfort-bound | broke nights | standing | 2.5★ reached | 3.0★ reached |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 · instant, no dressing | 0.0% | $202 | 10.0 | 3.00 | 2.88 | 100% | 66.6% | 0.0% | 2.69 | 100.0% | 5.0% |
| 2 · instant, buys dressing | 0.0% | $77 | 10.0 | 2.98 | 3.14 | 100% | 39.3% | 0.0% | 2.74 | 100.0% | 19.0% |
| 3 · never wipes or washes | 2.0% | $69 | 9.9 | 2.96 | 2.77 | 54% | 66.9% | 0.0% | 2.52 | 94.0% | 3.0% |
| 4a · 10 s to the mess | 0.0% | $75 | 10.0 | 2.99 | 3.14 | 100% | 39.4% | 0.0% | 2.74 | 100.0% | 22.0% |
| 4b · 20 s to the mess | 1.0% | $77 | 9.8 | 3.00 | 3.05 | 91% | 49.9% | 0.0% | 2.70 | 100.0% | 21.0% |
| 4c · 30 s to the mess | 3.0% | $71 | 9.2 | 3.01 | 2.81 | 82% | 69.2% | 0.0% | 2.58 | 92.0% | 19.0% |

Read across: shape 1 against the checked-in floor is the cost of halving the
glass share; shape 2 is the new floor; shape 3 is the rot (comfort near the
free base less the penalty, standing stalled, broke nights NOT up, because
the crowd reads the service side); shape 4 is the human, and DirtPenalty is
picked so that 10–20 s hands lose a tenth of a star, not a whole one.
