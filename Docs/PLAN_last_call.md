# PLAN — The Last Customer (staging)

The design is `GDD/26_last_call_story.md`. This is the order it gets built in, what each phase
is allowed to touch, and how each one is proved. Same rules as every other phase in this
project: Core decides, the UI renders, content is data, and nothing ships on "it compiles".

Each phase is a commit. None of them leaves the game in a broken state, and the first three are
invisible to the player on purpose — the beat works before it speaks.

---

## S0 — The cast becomes data *(prerequisite)* ☑ 2026-08-12

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

**Shipped.** 30 people left `TycoonHud` for `Assets/Data/customers/papers.json`; the lookup is
`PatronRoster` in the Game layer (presentation, like `StageSlot`), parsed by
`DataLoader.ParsePapers` and handed over by the bootstrap as `Cast`. The scene was wired in
code, not by hand, the way the debug scene wires every other data file. 11 new EditMode tests:
four faces pinned to the papers they had in C#, the three looks the arc reserves checked for
existence, and the four loud failures (two people on one look, a flag the art cannot draw, a
nameless person, an empty cast). Proved in play as well as in test — three licences read off
the screen against the file: MEREDITH NOLAN / 34 / UNITED STATES, and so on.

---

## S1 — The beat exists (Core only, silent) ☑ 2026-08-13

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

**Risk:** `IsComplete` is the day's end condition, and it is read in exactly ONE place
(`TycoonRun.Tick`) — checked, not assumed. Seating a guest after closing must extend that one
condition rather than grow a second one somewhere else; the HUD reads `IsClosingTime` for the
LAST CALL plaque and must keep meaning the same thing by it.

**Shipped.** The end condition was not extended at all, which is the good news: `IsComplete`
still says *the door is shut and the last stool is empty*, and a guest on a stool already
fails it. `BarDay.SeatGuest` sits somebody the night did not roll — outside the arrival clock,
the seat count and the crowd, ordinary in every way after that — and `TycoonRun.SettleLastCall`
runs inside `Tick`, between the floor's own settling and that one unchanged line.

Content and state are two classes on purpose: **`StoryArc`** is the written nights, built once
and shared by every run that plays them (the simulator will hand the same one to two hundred
bars), and **`StoryProgress`** is where a run has got to — the armed beat, its due day, kept
and missed, plus one `RegularState` per character so a guest who comes back is somebody the
bar has met. `TycoonRun` gained `Story`, `LastCustomer`, `LastCallBeat` and `DeclineLastCall()`,
and prices the scripted ask through the same `PriceOf` line the crowd uses — no die is rolled
anywhere in the beat.

23 EditMode tests (238 green, every older one untouched): the beat does not arm before its
day; the guest sits only after the door has shut and only into an empty room; their ask hides
behind the licence exactly like everyone else's; the night cannot end while they are on the
stool; a right serve advances the arc and a wrong one re-arms it; an honest no takes the
ordinary decline mark and keeps the beat; being left to wait is an answer too; one last call a
night, even after they have gone; and a run built without a story never hears one. The arc
also refuses, loudly, six ways to mis-edit the file it will be built from — a night that leads
nowhere, a circle, an orphan beat, two beats on one night, two beats with one name, and a beat
that comes back the same night it left.

**Two things S1 deliberately left standing, both for S3:** the guest whose patience runs out
is `StormedOff` like anyone else, and §3 says they should *leave a line, not storm* — that is
a presentation difference and it is drawn, not decided, in Core. And the stool is whichever
one the floor hands over; "the stool nearest the till" is the UI's to choose.

---

## S1b — The guests come at the weekend ☑ 2026-08-13

**Why now, between S1 and the bot:** the sim (S2) and the plate (S3) are both built on top of
"when is a beat due". Changing that after they exist would mean changing it in three places.

- `Core/BarCalendar.cs` — the week as a rule instead of a caption: six open nights, Tuesday
  through Sunday, Mondays dark. `NightOf`, `WeekOf`, `DayOf`, `IsWeekend`,
  `NextNightOnOrAfter`, and the `Label` the plaque prints. The HUD's private copy of the
  calendar is gone; it calls this one, and the words on the screen did not change.
- `StoryBeat` is scheduled by `week` + `night`, and computes its own day. A guest written for a
  quiet night is refused at construction, by name and by night; the host is exempt, because Ece
  works the shift rather than coming to the bar.
- `StoryProgress.IsDueOn` gates on the night as well as the clock, and a missed beat returns
  `returnsAfterWeeks` weeks later **on its own night**.
- `story.json` rescheduled: Ece W1·TUE, the collector W1·FRI, the sister W2·SAT, the critic
  W3·FRI.

**Proof:** 255 EditMode green. The calendar's arithmetic is pinned in both directions (day 4 is
a Friday, week 2's Friday is day 10, and `DayOf` inverts `NightOf`/`WeekOf`); the plaque's exact
string is pinned so a rule behind a caption cannot change the caption; nobody comes in on the
Tuesday, the Wednesday or the Thursday; both nights of one weekend can hold a beat; and the
guard against the failure this rule can actually have — *a missed Friday comes back on a
Friday, never on a Wednesday, and never never.*

**Risk:** the return clock is the only place a beat can be pushed onto a night its gate never
opens on, and that failure is SILENT — no exception, no red test, just a story that stops. It
is a test, not a comment.

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

## S3 — The ask is legible, and the people speak (UI)

**Ece speaks here too.** The plate is one surface with two users: the guest on their stool and
the host behind the bar (`hostBefore`/`hostAfter`, and the `lessons` that fire on state). Until
her portrait is drawn, the character's `placeholderLook` names the face the plate borrows — a
field that gets deleted the day the real one lands.

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

**Beat zero is Ece's** (GDD 26 §1b): night one, the door shuts, she crosses the bar and asks
for a neat pour of whatever the well has. It cannot fail for want of stock, it teaches the beat
on a night with nothing at stake, and its second line is the arc's own introduction. Everything
after it arrives already framed by her.

- `Assets/Data/story/story.json`: beats 0–2 of the arc in `GDD 26 §11`, fully written.
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
