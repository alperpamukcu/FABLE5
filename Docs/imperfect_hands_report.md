# What getting it nearly right is worth

80 runs a level, 30-day horizon, the SAME seeds at every level.

The bot's dice come off the run's own `RunRng` under a stream named
`hands`, which is seeded independently of arrivals/orders/patience — so a
shaky night is the same NIGHT as a steady one, with the same crowd wanting
the same drinks. Every drink deals a fixed twelve dice in a fixed order,
whatever it needs, so the levels cannot drift apart from each other.

**Read the per-serve columns, not the money.** Tips are rounded to whole
dollars on $4–8 drinks and are paid at ALL only when the crowd is above
Broke, so a money column is partly a census of which nights paid anything.

| Hands | ratio σ | Exact | Wrong | Refused | spec | fill | tip/serve | served/night | stars |
|---|---|---|---|---|---|---|---|---|---|
| steady (the shipped floor) | 0.00 | 100.0% | 0.0% | 0.0% | 100% | 100% | $5.00 | 11.0 | 2.63 |
| a good night | 0.04 | 100.0% | 0.0% | 0.0% | 99% | 100% | $4.00 | 11.0 | 2.63 |
| an ordinary hand | 0.10 | 100.0% | 0.0% | 0.0% | 96% | 98% | $4.00 | 11.1 | 2.65 |
| busy and rushed | 0.18 | 99.9% | 0.1% | 0.0% | 91% | 96% | $4.00 | 11.2 | 2.66 |
| all thumbs | 0.30 | 92.1% | 7.8% | 0.1% | 84% | 90% | $4.00 | 11.4 | 2.38 |

## The headline: the pour barely matters

Measured 2026-08-14. A bartender **eighteen percent off on every single
measure** still gets 99.9% of drinks identified as exactly the right
drink, loses about a dollar of tip a serve, and ends on the same stars as
a machine. The bands are wide enough — typically a tenth of the glass
either side of the ideal — that relative error has to reach roughly 30%
before it crosses one, and only then does anything happen at all.

So the game's central interaction currently has almost no consequence,
and what consequence it does have is nearly all in the GARNISH and the
FILL, which degrade smoothly, rather than in the pour, which does not
degrade at all until it falls off a cliff. That is a balance question and
it is the author's: narrower bands would make aim worth something, and
would also make every existing measurement in this folder harder.

## What the shape of this table means

**A mispour is not a near miss, it is a different drink.** `Compare` only
returns Close when the ordered recipe has a dominant TYPE band, and 51 of
53 recipes are style-banded — so Close is unreachable for almost every
order and a drifted pour goes straight to Wrong. Worse than losing the
tip: a Wrong serve is paid at the menu price of what the glass ACTUALLY
matched, against the bar's unlocked menu only, so an early bar usually
matches nothing and the base goes to zero as well.

**And it is a cliff, not a slope.** A band either accepts a ratio or it
does not, so small error does nothing at all until it crosses an edge and
then costs everything. Fill error is one-sided for the same kind of
reason: the glass cannot overflow, and the fill score only counts
shortfalls, so pouring long is free and pouring short is not.
