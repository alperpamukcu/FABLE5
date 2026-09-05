# The room's four shapes — GDD 27 §7

100 runs a shape, 30-day horizon, the SAME seeds in every row.
The counter's mess is on for every row (the sim measures the whole rule);
what differs is how the bot keeps it and whether it buys the room's dressing.

| Shape | bankrupt | till p50 | cust/night | service | comfort | clean | comfort-bound | broke nights | standing | 2.5★ reached | 3.0★ reached |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 · instant, no dressing | 0.0% | $202 | 10.0 | 3.00 | 2.88 | 100% | 66.6% | 0.0% | 2.69 | 100.0% | 5.0% |
| 2 · instant, buys dressing | 0.0% | $75 | 10.0 | 2.98 | 3.13 | 100% | 40.5% | 0.0% | 2.73 | 100.0% | 18.0% |
| 3 · never wipes or washes | 2.0% | $69 | 9.9 | 2.97 | 2.78 | 53% | 67.3% | 0.0% | 2.53 | 94.0% | 5.0% |
| 4a · 10 s to the mess | 1.0% | $78 | 10.0 | 2.98 | 3.14 | 100% | 40.7% | 0.0% | 2.73 | 100.0% | 20.0% |
| 4b · 20 s to the mess | 1.0% | $74 | 9.8 | 2.99 | 3.05 | 91% | 48.9% | 0.0% | 2.70 | 100.0% | 18.0% |
| 4c · 30 s to the mess | 2.0% | $72 | 9.2 | 3.01 | 2.79 | 82% | 70.2% | 0.0% | 2.56 | 91.0% | 18.0% |

Read across: shape 1 against the checked-in floor is the cost of halving the
glass share; shape 2 is the new floor; shape 3 is the rot (comfort near the
free base less the penalty, standing stalled, broke nights NOT up, because
the crowd reads the service side); shape 4 is the human, and DirtPenalty is
picked so that 10–20 s hands lose a tenth of a star, not a whole one.
