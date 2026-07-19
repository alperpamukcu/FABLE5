# LAST CALL — GDD Module: Tutorial & First-Time User Experience

> Implementation milestone: M4. Rewritten in v2.0 for the emotion pivot — the pre-pivot
> version taught pattern-matching and score targets, which are no longer what a new player
> struggles with. Modules 19 and 20 own the rules this teaches.

## 1. Philosophy

No separate tutorial mode. The first run IS the tutorial (Balatro model): a guided, seeded
first run with contextual coaching from a diegetic character — **the Bar Owner**, who appears
as a speech-bubble portrait, replacing generic tooltips with personality.

**What needs teaching has changed.** Recipes are learnable by trial: you select bottles, a
preview names the drink, nothing is lost by guessing. The genuinely opaque things now are:

1. the customer's stats are hidden, and you have to *ask*,
2. what a RANGE band means — that you are aiming at a number you cannot see,
3. that overshooting is worse than stopping short,
4. that the run is judged **weekly**, not per customer,
5. that some people are harder to please than others.

Every beat below exists to teach exactly one of those. In priority order **(3) matters most**:
a player who does not understand busting reads the whole game as arbitrary.

## 2. The teaching order

Deliberately not the order the systems were built in. Information first, then commitment,
then consequence.

| # | Beat | Teaches | Fires on |
|---|---|---|---|
| 1 | "Ask to see some ID. Nobody minds." | the ID exists, and clicking the customer opens it | first customer, after ~3s of inactivity |
| 2 | "One number you can trust. Two you can't see at all." | the three tiers | first time the ID is opened |
| 3 | "She wants that one *down*. Pour something that takes it there." | intent and direction | first time the ID is closed |
| 4 | "Watch the bar move before you commit." | the pre-commit preview | first bottle selected |
| 5 | "Past zero is worse than short of it. Ease off." | **busting** | first predicted overshoot, *before* it can be served |
| 6 | "Dead on. That's the good one." | Clean Serve | first Clean Serve, whenever it lands |
| 7 | "You don't have to please everyone. You have to please enough." | the weekly quota | end of Night 1 |
| 8 | "He's had a week of it. He'll take more convincing." | demand levels | first Particular-or-harder customer |

Beat 5 is the only one that **interrupts**. It fires while the selection is still uncommitted
and the projection already crosses the target. That interruption is worth it: letting a new
player discover the bust rule by eating one teaches "the game punished me for trying", which
is the wrong lesson at the worst possible moment.

Beat 7 fires at the end of Night 1 rather than the start, because the quota means nothing to
someone who has not yet earned satisfaction to measure against it.

## 3. The seeded first run

Profile flag `ftue_complete = false` → first PLAY starts a fixed-seed run on The Classic bar,
Green Stake. The seed must guarantee:

- **Night 1 Customer A is `celebrating` or `nothing_much`** — an Easygoing disposition, so the
  opening serves are forgiving and beat 8 has something to contrast against later.
- **Their intent stat is EXACT.** The first customer anyone reads should not be a guess. Blind
  reads are introduced by beat 2 and then learned by living with them.
- **The intent stat sits 25–40 from its target**, so one well-chosen bottle produces visible
  movement without landing — the mechanism is learned before the jackpot.
- **The rail contains a Garnish**, so the fine-adjustment tool is already in hand when beat 5
  fires and the answer to "ease off" is reachable.
- Night 1 VIP is **Open Book** — every reading exact. It reads as a gift, and quietly teaches
  what a fully legible ID looks like before Poker Face ever takes one away.

Skippable at any moment ("Skip coaching"); skipping sets `ftue_complete = true`.

## 4. Contextual teaching after FTUE

- The **score breakdown** (the Flavor × Mult trace, including the resonance block) is expanded
  by default for the first 3 runs, then collapses to on-hover.
- Keywords in rules text (Retrigger, Debuffed, Clean Serve, Bust, Demanding…) are underlined;
  hover shows a glossary popup. Also reachable from Pause → Run Info.
- First encounter with any new content type (Seal, Tool, Voucher, quality tier, a read-rule
  VIP) triggers a one-time toast: short name + one-line effect.
- **The Liar needs its own toast**, once, the first time they appear: *"One thing on that card
  is a lie."* Without it the mechanic is indistinguishable from a bug, and a player who
  concludes the game is buggy stops trusting every reading afterwards.

## 5. Loss feedback ("why did I lose?")

The run ends on a missed weekly quota, so the Game Over screen answers *that* question:

- Shortfall line: "The week needed 10. You brought 7."
- Diagnosis line, rule-based, first match wins:
  - bust rate > 35% → "You kept overshooting. Small pours land; big ones bust."
  - more than half of serves aimed at UNKNOWN intents → "You were pouring blind. Chat, or buy
    someone who talks."
  - median satisfaction ≤ 1 → "Lots of small gestures. Harder customers need a real change."
  - more than 60% of Chats unused → "You never asked. A Chat costs a Restock and buys
    certainty."
- Button: "See who came in" → the run's cast with their relationship ranks, which teaches that
  regulars were quietly accumulating value the player may not have noticed.

## 6. Acceptance criteria (QA)

- A new player who has read nothing external can explain **why a bust scored nothing** by the
  end of their first run (target: 90% of testers, asked directly).
- A new player reaches Week 2 within 2 runs (target: 70%).
- Testers can state unprompted that the run is judged weekly rather than per customer
  (target: 80%). If they cannot, beat 7 is landing too late or too quietly.
- FTUE costs experienced players nothing: skip is one click and never re-triggers.
