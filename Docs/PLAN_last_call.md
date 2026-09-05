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

## S1c — The beat becomes a trial, and the guest leaves the books ☑ 2026-08-13

**The author's rework:** the last customer is an INSPECTOR now — several drinks, one clock,
one at a time, to the standard of *exactly the drink, made the way the book says*, with the
fill forgiven down to 0.90. And they are a **guest of the house**: no licence (Core inspects
at the seat — the ONE written exception to the hidden-information rule, fenced in CLAUDE.md),
no bill, no rating, no line on the slip. The trial's stake is the arc, nothing else.

- `StoryTrial` (content: asks/seconds/minFill/allowedMistakes) + `StoryTrialRun` (tonight's
  attempt: Talking → Pouring → Passed/Failed). Difficulty is data.
- `CustomerVisit`: `OnTheHouse`, `ClockHeld` (nothing ticks during the dialogue),
  `AskFor` (next demand, same clock — deliberately NOT the extra-round path, which refreshes),
  `GetUp` (leaves owing nothing).
- `BarDay.FinishedCounted()` — every ledger that reads the night reads it here, so "does this
  person count" is answered once. Rating, slip counts and the sim all go through it.
- `TycoonRun`: `Trial`, `BeginLastCallTrial()` (the talking ends, the clock starts — with a
  120s `TalkingGrace` backstop so a held plate can never hold the night), `ServeTo` branches
  to the trial's pass mark for the guest, `DeclineLastCall` fails the trial without marking
  the night's books.
- `story.json` v2: asks/seconds/allowedMistakes per beat; the cast recast — the sister is out,
  the **influencer** (`teal`) and the **gourmet inspector** (`profess`) are in; difficulty
  climbs 1 drink/2 err → 2/1 → 2/1 → 3/0.

**Proof:** 262 EditMode green. The ones that matter: the guest pays nothing and moves no
ledger (the till's only movement on a trial night is the RENT, by name); no rating in either
direction; nothing ticks while they talk; the talking cannot hold the night hostage; one
clock, not one per drink; a short pour of the right drink is a mistake; a brimful glass is
not asked for; mistakes over the allowance end the night; the wrong-drink ask STAYS.

**Deliberately untouched:** the extra-round path (a reward refreshes patience; a demand must
not), the crowd's licence rule, and the weekend economy.

---

## S2a — The story is loaded ☑ 2026-08-13

**Why it needed its own phase:** nothing outside Core knew the arc existed. `story.json` was a
text file — no parser, no field on the bootstrap — so both the sim (S2) and the plate (S3) had
nothing real to play. A bot playing a hand-built arc measures a beat nobody wrote.

- `DataLoader.ParseStory(json, cast, recipes)` — the arc built against the REAL cast and the
  REAL book, with the loud validation every content file signs: a look nobody has papers for,
  an ask that is not a recipe, a night that is not one of the six, a guest on a quiet night, a
  role that is not host/guest, two hosts or none, a lesson naming a condition no code watches,
  a beat leading nowhere. `StoryLesson`/`StoryCue` give the lessons a fixed vocabulary instead
  of a scripting language.
- **A rule the loader now enforces, because it is a rule about writing:** a beat that names a
  `needStyle` must say that word in a `hostWarning` line. The asks come one at a time (GDD 26
  §4), so the host's early warning is the ONLY notice a player gets — and "have something
  brown by Friday" is not a notice, "get a bourbon in before Friday" is. The field is new and
  the three guest beats now carry one.
- `GameBootstrap` parses at boot and exposes `Story`. It does NOT hand it to the run yet
  (`storyInPlay`, off): the guest arrives into a conversation and holds their clock until it
  ends, so a scene that cannot talk would sit a silent stranger on a stool and stall the night
  for the talking grace. Delete the field with S3.

**Proof:** 15 EditMode tests on the shipped file — it parses, the schedule is exactly the one
GDD 26 §11 prints, only the house works a quiet night, every ask exists, the trials get harder
without any of them becoming impossible, and eleven ways to mis-edit the file are refused by
name.

---

## S2 — The bot learns the beat ☑ 2026-08-13 *(balance stays honest)*

- `TycoonSimulator`: on a last customer, pour the ask if the shelf can (the bot already builds
  from `RecipeDefinition`), otherwise `DeclineLastCall()`.
- `tycoon_sim_report.md` gains: story asks, served, declined, and the day each arc stalled on.

**Proof:** 200 runs. On days with no beat the distribution is byte-identical to the last
report; the new lines appear; no run deadlocks at closing time.

**Risk:** a bot that neither serves nor declines hangs the night forever — the report's
"reached the horizon" number is the canary and must stay at 200/200.

**Shipped, and it earned its keep — four findings, none of them guessed.** The bot plays the
arc the sim builds once and shares across all two hundred runs; it starts the trial on arrival
(there is no dialogue to read headlessly), pours to the trial's own standard, and says an
honest no when the shelf cannot answer. The report gained the story block, the beat each run
was still owing at the horizon, and — the line that did the work — **what came back and why**,
with the delivered glass in it.

1. **1600 identical wrong drinks** said `rocks 0.70/0.70 [soda_klara=0.84 vodka_astra=0.16]`.
   A drink DECLARES ITSELF at the glass (`PourAtGlass` re-vessels on every match), so a
   half-built highball is re-housed mid-build and whatever is already in it stays in it: an
   overfilled glass is a permanently wrong ratio. The crowd's build survives this only by
   accident — at 0.85 the clamped ratio lands inside its band and the upgrade rescues it; at
   the fill an inspector wants, it does not. The bot now builds BUILT drinks in the glass, in
   small rounds, always in ratio (`BuildInTheGlass`). **The crowd's path is untouched on
   purpose**; whether it should change is a balance question of its own.
2. Then the mule came back with **no lime in it at all** — the same trap one layer down: the
   glass shrank under a big confident pour and the third ingredient never fitted. Small rounds
   fixed it.
3. Then the manhattan came back wrong every time, and the reason was not the bot: the beat
   says `grantsRecipeOnAsk: "manhattan"` and **nothing was granting it**. The ask now hands
   its page over at the seat (`TycoonRun.GrantRecipe` — no star gate, no price, no slip line;
   the ONE door in the game that opens the book for free) and the last beat became passable.
4. With that, **200/200 arcs finish inside thirty nights**, 800 trials passed, 3 failed. The
   three are all one thing: the gourmet asks for a `gimlet` and the run had not bought the
   page — the influencer's beat is written to REWARD that page, and rewards land in S5.

**The ordinary night did not move.** Measured properly, by running the same 200 seeds with the
arc switched off: standing 2.82 → 2.83, storm-offs 17.9% → 17.9%, customers a night 10.5 →
10.5, serves +0.09%, bankruptcies 0 → 0. What DOES move is the till — median $161 → $208 —
and that is the arc paying out exactly as designed: a page the bar is given is a page it does
not buy, and its quarantined stock comes with it. (The report checked into the repo before
this was stale against several older changes; the storyless run above is the honest baseline.)

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

**Shipped in part, 2026-08-13 — the module is visible.** `storyInPlay` is ON: the beat plays in
the scene, and night one is Ece's.

- **The plate** (layer 7, above the drinkers, below the bench): a face, a name, one line, GO ON
  and SAY NO TONIGHT. It works a SCRIPT — the host's framing, then the guest's ask; then, when
  the trial ends, the outcome and the host's last word. The final GO ON of the ask is what
  starts the clock (`BeginLastCallTrial`), so a slow reader is never punished for reading.
- **The post-it** (layer 14, above the bench and the tap — the note you work FROM): who is
  waiting, the drink in hand, `2 OF 3 · NO MISTAKES`, the clock, and what the shelf is missing.
- **The guest is drawn as themselves**: `LookFor` returns the beat's own face (or the borrowed
  one) instead of hashing a name, and `NameOn` prints their name rather than the borrowed
  face's papers — Ece was being introduced as Serena Fontana.
- **Their seat ticket says TALK TO THEM, not the drink.** Their licence is open from the moment
  they sit, so the ticket would otherwise print the whole ask over their head and hand the
  player a trial that is supposed to arrive one drink at a time.

**Two things play found that no test could have.** The guest used to vanish on the same tick as
the last serve — stool empty, night complete, day-end slip up over a line nobody had read — so
Core now gives them `LastWordSeconds` on the stool to finish the beat out loud (pinned by a
test now that it is known). And a trial that simply ran out of clock was being answered with
the line written for an honest no: `StoryTrialRun.ToldNo` separates the three ways to miss, and
a timed-out guest gets the nudge instead.

**S3 closed out, same day.** The guest takes the stool nearest the till (the last one the bar
actually owns — the row fills from the door end for everyone else); a guest whose clock runs out
walks rather than storms, and the night's log does not book them as a walk-out, because they
were never on its books. And a third thing, found by LOOKING at the first blessed picture: the
story guest kept turning up in a stranger's body while the plate showed the right person. A
stool KEEPS its look by design — a face that changes under the player is worse than a wrong one
— and the guest's stool had been handed a rolled one. Rather than chase which frame won that
race, the written face is now reasserted once a frame: for this one visit the beat is the
authority and the seat is not.

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

**Shipped 2026-08-13.** `DiegeticStage.SetClosingBeat(on, x)` — the HUD says whether a guest is
in and where their stool is, and the stage does the rest over about a second: the ceiling drops
to 0.22 of itself, the wash thins to 0.55, the LAST CALL neon burns at 1.9×, and one lamp is
lit at the guest's own x, hung at the same ceiling height as the others. Nothing is drawn for
it — every number is an intensity on a light that was already hanging there, which is what
keeps it from reading as a different game for thirty seconds. `Motion.Reduced` gets the same
room without the ramp, and the beat reverses itself when the stool empties.

**The proof is measured, not photographed — and that was a correction.** A blessed picture was
written first and failed its own second run by 89,684 pixels: the plate's cream sat five units
apart between two runs (a residual fade rounds over bright pixels and not over dark ones) and
the settings key drew its icon in one run and the word SETTINGS in the other. Neither has
anything to do with the closing beat. What the beat IS, is four numbers, so `LookTests` asserts
those instead — the guest's lamp beats the ceiling more than threefold, the ceiling is under
0.3, the wash is under 0.7, the sign is over 1.2 — and then declines the guest and checks the
lamp goes out. It reads the intensities by light NAME through reflection rather than making the
whole test assembly link against the URP 2D runtime for one float.

---

## S5 — The first arc, written ◐ 2026-09-05 (the lessons and the tab; the rewards are the locks)

**Scope cut 2026-08-13 (the author): ONLY ECE FOR NOW.** The live arc is her night alone and
the three guests are parked in `Docs/story_guests_drafted.json` — so S5 is no longer "write the
arc", it is "make one night carry everything it should": her rewards, her lessons, and the book
showing what is owed. The guests come back one at a time, each with the story around them
built, which is what S6 becomes.

**Beat zero is Ece's** (GDD 26 §1b): night one, the door shuts, she crosses the bar and asks
for a neat pour of whatever the well has. It cannot fail for want of stock, it teaches the beat
on a night with nothing at stake, and its second line is the arc's own introduction. Everything
after it arrives already framed by her.

- `Assets/Data/story/story.json`: beats 0–2 of the arc in `GDD 26 §11`, fully written.
- Rewards wired: money, stars, a recipe page, a bottle on the counter.
- The book shows an open ask as a standing line ("Graham wants a whiskey, neat").

**Proof:** play both beats end to end: fail one on purpose, decline one, serve one right, and
watch the reward land in the system it belongs to. EditMode pins each reward kind.

**Shipped 2026-09-05 — the two halves that were still hers.** The reward line above is
superseded by GDD 26 §12.3 (2026-08-14): a beat pays nothing, the things it earns NAME it
(`unlockBeat`), so "rewards wired" is done the day a second beat exists to be named. What
was actually missing was that **nobody ever spoke the lessons** — seven were written, parsed
and read by nothing — and that **the book showed no open tab**.

- **The lessons speak (Core + UI).** `TycoonRun.LessonDue` / `HeardLesson()` and
  `StoryProgress.Learn(cue)`: each `StoryCue` is observed by Core where it is true — the first
  door in the constructor and `ContinueToNextDay`; somebody waiting while no card was ever read,
  and two spirits standing unmixed in the tin, once a tick; the first pull in `BeginPull`; the
  first market and a night under the rent at the close; an extra round in `ServeTo`; and a guest
  THIS WEEK wanting a style the shelf lacks at the door (looked for along the arc, since the
  armed beat may be Ece's quiet Monday and the guest who needs bourbon is Saturday's). Once per
  run, queued in the order the moments came; a card read before the licence lesson lands
  spends it silently; a run without a story has none. On an open night the lesson is the
  dialogue plate with her name and face and GO ON / GOT IT and no SAY NO; at the close it is a
  98 message box of the market's own (`BuildHostNote`), because the plate is a thing of the open
  night and would sit on the slip. Thirteen EditMode tests (`StoryLessonTests`).
- **The open tab (GDD 26 §5).** `StoryProgress.CurrentAsked` — set by a miss or a turn-away,
  cleared when the arc moves on — and the book's title page prints OPEN TAB above the news:
  "<NAME> WANTS <DRINK> · <NIGHT>" once the ask has been heard, and before the first visit
  "GET <STYLE> IN · <NAME> COMES <NIGHT>" while the guest's week is this one or next.
- **Ece's face is being cast** (the author: every face from here on is drawn against heavyset).
  Three rolls are on the casting sheet; until one is adopted the plate speaks with her name and
  an empty well, since `glam` has no frames on this rig either.

Still S5's: nothing. What the plan called "rewards" now waits on S6 — the first `unlockBeat`
is authored the day the collector's night goes back into the file.

---

## S6 — The rest of the cast *(content, repeatable)*

Beats 3–7. No new code unless a beat needs a gate the data cannot express — and if one does,
that is a design question first (GDD 26 §4), not a special case in C#.

**Gated on faces (2026-09-05).** The three drafted guests wear `execman`, `teal` and `profess`,
which are old-rig looks with no frames under `Resources/Patron` — a guest put back today would
sit in a rolled stranger's body with a blank plate. Each comes back the day their face is
drawn to heavyset's rig (`Tools/patron_prompts.py` → roll → judge → the author's pick → clips →
`patron_ship.py`), and with them the first `unlockBeat`: the collector's kept night opening a
bourbon, the influencer's the `gimlet` page (§11). The PixelLab cycle is the pacing: one face
with its eleven clips is ~90 generations.

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
