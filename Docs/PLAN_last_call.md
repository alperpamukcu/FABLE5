# PLAN — The Last Customer (staging)

The design is `GDD/26_last_call_story.md`. This is the order it gets built in, what each phase
is allowed to touch, and how each one is proved. Same rules as every other phase in this
project: Core decides, the UI renders, content is data, and nothing ships on "it compiles".

Each phase is a commit. None of them leaves the game in a broken state, and the first three are
invisible to the player on purpose — the beat works before it speaks.

---

## S0 — The cast becomes data *(prerequisite)*

**Why first:** a story character needs the same papers the licence prints, and those papers are
30 hardcoded lines in `TycoonHud` (`PatronPapers`). The story cannot be written on top of C#.

- `Assets/Data/customers/papers.json` — slug → name, age, country, iso. The whole existing
  table, moved verbatim.
- `DataLoader.ParsePapers` with the loud validation every other file gets (unknown iso, empty
  name, duplicate slug → throw at load).
- `TycoonHud` reads the parsed table instead of its dictionary; `PapersFor(look)` keeps its
  signature so nothing else moves.

**Proof:** an EditMode test pinning three slugs to their exact papers (so a careless edit to the
file is caught); the licence screenshot unchanged (`LookTests` needs no re-bless); 203 green.

**Risk:** the papers are read on the licence, the receipt and the guide — one missed call site
prints "Customer". Grep for `PapersFor` before and after.

---

## S1 — The beat exists (Core only, silent)

The last customer arrives, orders, can be served or declined. Nobody says anything yet.

- `Core/Story/StoryBeat.cs`, `StoryCharacter.cs`, `StoryArc.cs` — pure data + the arc's state
  machine (armed / due / served / declined, the return clock).
- `TycoonRun`:
  - holds a `StoryArc` (null when a run has no story, exactly like `archetypes`),
  - in `Tick`, when `Floor.IsClosingTime && Floor.Seated.Count == 0` and a beat is due, seats
    the scripted visit through a new `BarDay.SeatGuest(CustomerVisit)`,
  - `DeclineLastCall()` — the honest exit; the beat re-arms on its return clock,
  - the night completes as it always did, once the guest is gone.
- The scripted visit: `DrinkOrder(recipe, price, spec)` + `RegularState(...)` from the beat,
  patience from data, no RNG draws (a `"story"` stream exists for anything that later needs one).

**Proof (EditMode):** a beat arms on its day and not before; the guest's order is the scripted
recipe; a right serve advances the arc; a wrong serve does not and re-arms; a decline re-arms;
the night cannot end while the guest is seated; a run built without a story behaves exactly as
today (every existing test still green).

**Risk:** `IsComplete` is the day's end condition in three places — the run, the HUD's watcher
and the sim. Seating a guest after closing must not let any of them call the night early.

---

## S2 — The bot learns the beat *(balance stays honest)*

- `TycoonSimulator`: on a last customer, pour the ask if the shelf can (the bot already builds
  from `RecipeDefinition`), otherwise `DeclineLastCall()`.
- `tycoon_sim_report.md` gains: story asks, served, declined, and the day each arc stalled on.

**Proof:** 200 runs. On days with no beat the distribution is byte-identical to the last
report; the new lines appear; no run deadlocks at closing time.

**Risk:** a bot that neither serves nor declines hangs the night forever — the report's
"reached the horizon" number is the canary and must stay at 200/200.

---

## S3 — The ask is legible, and the person speaks (UI)

- The dialogue plate: face, name, one or two lines, a listen key, and a **SAY NO TONIGHT** key.
  Licence paper language (`ChromeArt.Card`), counter-anchored, never over the bar.
- The licence, for a story customer, carries the ask **and what the bar is missing** — the
  market already computes it (`MissingStyles`); the card borrows the same words.
- The seat tag says who is waiting, in their own name.

**Proof (PlayMode):** the smoke suite gains a test that drives a night to closing, sees the
guest arrive, reads the plate's text, and declines; `LookTests` gains a blessed picture of the
plate. Play it once by hand and read every line at 1280×720.

**Risk:** the plate is a new full-width surface at the counter — it must not eat the clicks the
glass and the bin need. The PlayMode suite's serve test is the guard.

---

## S4 — The closing light beat

- On the last call: ceiling lamps down, one lamp over the guest's stool, the LAST CALL sign
  ignites, ambience thins, the HUD's chrome quiets. All of it on the existing 2D rig.
- It reverses on `DayEnd` so the market opens on the ordinary look.

**Proof:** a blessed picture of the closing beat (`LookTests`); the room's own animation means
the picture is taken inside the panel region, the way the bench's is. Measure the light change
in play (`execute_code` reads the light intensities) rather than trusting the eye.

**Risk:** the 2D lights are the one part of the room that is already alive; a beat that fights
the ambient flicker will read as a bug. Change intensities, not colours, first.

---

## S5 — The first arc, written

- `Assets/Data/story/story.json`: beats 1 and 2 of the arc in `GDD 26 §11`, fully written.
- Rewards wired: money, stars, a recipe page, a bottle on the counter.
- The book shows an open ask as a standing line ("Graham wants a whiskey, neat").

**Proof:** play both beats end to end: fail one on purpose, decline one, serve one right, and
watch the reward land in the system it belongs to. EditMode pins each reward kind.

---

## S6 — The rest of the cast *(content, repeatable)*

Beats 3–7. No new code unless a beat needs a gate the data cannot express — and if one does,
that is a design question first (GDD 26 §4), not a special case in C#.

---

## Standing rules for this arc

1. **The ask always names what is missing.** A player must never have to guess why they cannot
   pour it (GDD 26 §4).
2. **No dead ends.** Every beat can be declined, and declining costs a night, not the arc.
3. **Two lines, one sentence each.** If a character needs more, the character is wrong.
4. **Rewards land in systems the player already reads.** No invisible flags.
5. **The crowd is untouched.** This module adds one customer at the end of the night and
   changes nothing about the other ten.
6. **Every phase ships green:** `LastCall.Tests` (EditMode), `LastCall.PlayTests` (PlayMode,
   including the pixel baselines), and — from S2 — a 200-run sim whose ordinary numbers have
   not moved.
