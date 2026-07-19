# LAST CALL — GDD Module: Tutorial & First-Time User Experience

> **v2.0 emotion pivot — this module is stale.** It teaches the pre-pivot loop (patterns and
> score targets) and says nothing about reading a customer, the ID, busting, or the weekly
> quota, which are now the things a new player most needs taught. Needs a rewrite before M4.

> Implementation milestone: M4. Design locked in v1.1.

## 1. Philosophy
No separate tutorial mode. The first run IS the tutorial (Balatro model): a guided, seeded first run with contextual coaching from a diegetic character — **the Bar Owner**, who appears as a speech-bubble portrait in the top-left, replacing generic tooltips with personality.

## 2. Guided first run (seeded)
- Profile flag `ftue_complete = false` → first PLAY starts a fixed-seed run on The Classic bar, Green Stake.
- The seed guarantees: Night 1 rail can form a Sour; first shop contains 1 cheap Common Patron (+Mult type), 1 Recipe Book (Sour); Night 1 VIP is always **The Teetotaler** (teaches "read the rule, adapt").
- Coaching beats (each fires once, advances on player action):
  1. "Select ingredients — watch the Recipe preview panel." (highlights preview panel)
  2. First MIX → slowed-down scoring animation with the math breakdown expanded.
  3. First time stuck with a bad rail → "RESTOCK dumps cards you don't need. You have 3."
  4. First shop → "Patrons sit at your bar and change the math. Buy him."
  5. First Recipe Book → "This permanently levels a recipe this run."
  6. VIP intro → "VIPs have house rules. Read before you pour."
- Skippable at any moment ("Skip coaching" button); skipping sets `ftue_complete = true`.

## 3. Contextual teaching after FTUE
- **Math breakdown panel** (the full Flavor × Mult trace) is ON by default for the first 3 runs, then collapses to on-hover (setting in Gameplay options).
- Every keyword in rules text (Retrigger, Debuffed, Top Shelf…) is underlined; hover/press shows a glossary popup. Glossary also accessible from Pause → Run Info.
- First encounter with any new content type (Seal, Tool, Voucher, quality tier) triggers a one-time toast: short name + one-line effect.

## 4. Loss feedback ("why did I lose?")
Game Over screen must answer the question, not just show stats:
- Shortfall line: "Needed 4,000 — your best mix was 1,860."
- Diagnosis line (rule-based, pick first match): no ×Mult source owned → "Flat Mult only: you had no ×Mult Patron."; recipes all level 1 → "Unleveled recipes: Recipe Books multiply your base."; >40% restocks unused → "Unused restocks: dig for better hands."
- Button: "See winning builds" → opens Collection filtered to Patrons seen this run.

## 5. Acceptance criteria (QA)
- A new player who never reads external material reaches Night 3 within 2 runs in playtests (target: 70% of testers).
- FTUE adds zero cost to experienced players: skip is one click, never re-triggers.
