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

| Hands | ratio σ | Exact | Close | Wrong | Refused | spec | fill | tip/serve | served/night | stars |
|---|---|---|---|---|---|---|---|---|---|---|
| steady (the shipped floor) | 0.00 | 100.0% | 0.0% | 0.0% | 0.0% | 100% | 100% | $6.00 | 10.5 | 2.84 |
| a good night | 0.04 | 100.0% | 0.0% | 0.0% | 0.0% | 99% | 100% | $6.00 | 10.5 | 2.85 |
| an ordinary hand | 0.10 | 100.0% | 0.0% | 0.0% | 0.0% | 96% | 98% | $5.00 | 10.6 | 2.85 |
| busy and rushed | 0.18 | 99.9% | 0.0% | 0.1% | 0.0% | 91% | 95% | $5.00 | 10.7 | 2.87 |
| all thumbs | 0.30 | 90.8% | 7.9% | 1.2% | 0.1% | 84% | 90% | $5.00 | 10.9 | 2.78 |

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

## The middle grade exists now

The first run of this table (2026-08-14, before the rewrite) had no Close
column, because `Compare` only returned Close when the ordered recipe had
a dominant TYPE band and every banded recipe in `recipes.json` is
style-banded — the grade could not be produced by the shipped game at all,
so a pour that drifted out of its bands went straight to Wrong: paid at
the menu price of whatever the glass happened to match against the bar's
UNLOCKED menu, which for an early bar is usually nothing at all.

Close is now the ordered drink poured OUT OF TOLERANCE: everything the
recipe names is in the glass, nothing much else is, and the shares
missed. Same seeds, same bot, judge swapped underneath:

| | old judge | new judge |
|---|---|---|
| steady hands — Exact / stars | 100.0% / 2.84 | 100.0% / 2.84 |
| all thumbs — Close | 0.0% | 7.9% |
| all thumbs — Wrong | 9.0% | 1.2% |
| all thumbs — stars | 2.58 | 2.78 |

A steady hand does not notice, which is the check that it changed nothing
it should not: the grade only ever touches a serve that was not exact.
Eight of the nine points that used to be total losses are now graded
misses — paid for, tipped at half, and a quarter of a satisfaction point
worse — and a clumsy bar's standing goes from 2.58 stars to 2.78. What is
left in the Wrong column is what belongs there: drinks that left an
ingredient out, or that are a third something the recipe never mentions.

## And the tier ladder was being measured by a bot that ignored it

Found on the way, and worth more than the rewrite that found it. The
first table above ran at **92.7% Exact with perfectly steady hands** —
7.3% of every serve in the game missing its band while the day-end
counter insisted the shelf could answer every tier its menu demanded (0
of 15,916). Both numbers were true. `PickByStyle` chose the FULLEST
bottle of a style and never read the band's `MinTier`, so a bar that had
bought the reserve gin poured its well gin into the recipe that asked for
it. The instrument, not the bar — the third time that has been the answer
in this file. With the bottle chosen the way a bartender would choose it,
the floor is 100% Exact again and its standing goes from 2.60 to 2.84.

**And it is a cliff, not a slope.** A band either accepts a ratio or it
does not, so small error does nothing at all until it crosses an edge and
then costs everything. Fill error is one-sided for the same kind of
reason: the glass cannot overflow, and the fill score only counts
shortfalls, so pouring long is free and pouring short is not.

## Why the bands are not the reason

Each recipe's ideal pour, jittered 4000 times a level the way the sweep
jitters it, then read back as proportions. No crowd and no money: this is
what the POUR does, apart from what the ladder happens to serve.

**Budget** is how far the tightest band sits from the ideal, as a share of
its own pour. Read it against the sigma columns and the cancellation is
plain: a drink with an 11% budget survives a hand 30% off nineteen times in
twenty, because pouring everything long is pouring the same drink bigger.

| Recipe | budget | out of band σ=.10 | σ=.18 | σ=.30 |
|---|---|---|---|---|
| negroni | 16% | 0.0% | 7.2% | 45.1% |
| boulevardier | 16% | 0.0% | 6.0% | 44.4% |
| rosita | 16% | 0.0% | 6.9% | 43.4% |
| last_call | 13% | 0.0% | 2.8% | 39.3% |
| espresso_martini | 14% | 0.0% | 0.6% | 20.5% |
| vesper | 13% | 0.0% | 0.0% | 14.6% |
| paloma | 21% | 0.0% | 0.0% | 12.1% |
| tequila_mule | 19% | 0.0% | 0.0% | 11.1% |
| dark_stormy | 19% | 0.0% | 0.0% | 10.9% |
| moscow_mule | 19% | 0.0% | 0.0% | 10.9% |
| screwdriver | 17% | 0.0% | 0.0% | 10.8% |
| gin_sour | 16% | 0.0% | 0.1% | 10.7% |
| **mean of 52** | 18% | 0.0% | 0.5% | 10.8% |

The drinks that DO break are the ones poured in near-equal parts — Negroni,
Boulevardier, Rosita, Last Call. Equal shares leave the differential error
nowhere to hide, which is the same reason those are the drinks a real bar
measures rather than eyeballs.

So narrowing the bands is the wrong lever, or at least a weak one: the
forgiveness is structural, and it comes from grading a drink on its
proportions alone. A pour that had to hit a VOLUME as well as a ratio, or a
fill score that counted long pours the way it counts short ones, would cost
a shaky hand something at every sigma. That is a design decision and it is
the author's — this section exists so it can be made against numbers.
