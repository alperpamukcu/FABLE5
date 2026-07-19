# LAST CALL — Game Design Document (v2.0)

> **v2.0 — the emotion pivot.** The core loop is no longer "recognise a pattern, score points".
> It is **read the person in front of you and serve what they actually need**. Recipes were not
> deleted; they were demoted to the craft layer. `19_emotion_mechanic.md` and
> `20_regulars_and_week.md` are the source of truth for the new loop, and they win wherever an
> older module disagrees.

**Genre:** Deckbuilder / social-deduction hybrid, with a roguelike run structure
**Structural reference:** *Papers, Please* — a case to read, a discrepancy to spot, a quota to hit.
Mechanically and tonally, not thematically.
**Platform:** PC (Windows/Linux/macOS), keyboard + mouse, full controller support
**Perspective:** 2D, fixed single-screen "behind the bar" view (no 3D camera, no player character model — the player IS the bartender, seen implicitly through hands/UI only)
**Target session length:** 30–60 minutes per run
**Team size:** Solo developer
**Engine:** Unity 6000.3 (C#, URP), data-driven — all cards/patrons/VIPs/archetypes defined in JSON

---

## 1. HIGH CONCEPT

You are the bartender of a dim, neon-soaked late-night cocktail bar. Each run is one "Opening
Week" of 8 Nights, grouped into 4 weeks.

Every customer walks in carrying six **emotions** — Anger, Sadness, Fatigue, Excitement,
Heartbreak, Anxiety — rated 0–100. You cannot see most of them. Asking for ID gets you a
licence showing **one exact value, three rough ranges and two blanks**, plus the one thing
never hidden: what they came in wanting done about it, and which way.

Every ingredient is printed with what it does to a person. You draw from your **Cabinet**
(deck), combine 1–5 bottles, and the mix moves their stats. Land the target emotion exactly on
0 or 100 and you get a **Clean Serve**; push past it and you **bust**. Recipes still matter —
a well-made drink carries what you put in it further — but the Multiplier now comes from how
well you read the room.

Survive by hitting a **weekly satisfaction quota**. One customer you misjudge does not end the
run; a week of them does.

Core fantasy: the bartender who listens. Not alcohol as medicine — attention as the gift.

> **Tone guardrail, non-negotiable:** frame this as the bartender *listening* to people. Never
> as alcohol curing emotions. No glorification of drunkenness (this also matters for the age
> rating). Customer reactions stay stylized and short.

---

## 2. CORE GAME LOOP

```
RUN START → Week 1, Night 1
  ├── Customer 1  → read → serve → satisfaction + Tips
  ├── BACK ROOM (shop: power OR knowledge)
  ├── Customer 2  → read → serve → satisfaction + Tips
  ├── BACK ROOM
  └── Customer 3 (VIP)  → read → serve → satisfaction + Tips
        └── Night 2 … then the WEEK GATE: quota met? → Week 2, else RUN OVER
RUN END: clear week 4 → win. Only a missed weekly quota ends a run.
```

**Per-customer loop (one "round"):**
1. A customer sits down. Click them to see their ID: six readings of varying clarity, plus
   their intent (e.g. *WANTS TO SETTLE FATIGUE*).
2. The **Rail** (hand) is filled to 8 bottles drawn from the Cabinet.
3. Select 1–5 ingredients; the projected movement is previewed on the ID before committing.
4. Then either:
   - **MIX** — serve it. Scores points *and* moves their emotions.
   - **RESTOCK** — dump the selection, draw replacements.
   - **CHAT** — spend a Restock to sharpen one reading instead of pouring.
5. **4 Mixes** and **3 Restocks** per customer (modifiable); Chat costs a Restock but does not
   count as one for patron conditions.
6. Each serve earns 0–3 satisfaction toward the week. Hitting the score target additionally
   pays Tips.
7. Running out of Mixes just ends the visit. The customer leaves; the run continues.

---
