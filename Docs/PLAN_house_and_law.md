# PLAN — The House and the Law (staging)

The design is `GDD/27_the_house_comfort_and_cleanliness.md` (the room: two ratings, the mess, the
ladder) and `GDD/28_the_door_age_and_papers.md` (the door: minors, borrowed cards, the kick). This
is the order it gets built in, what each phase may touch, and how each one is proved. Same rules as
every other phase in this project: Core decides, the UI renders, content is data, nothing ships on
"it compiles", and money moves only after the 200-run sim has been read.

Status legend: ☐ todo · ◐ in progress · ☑ done. Each phase is a commit. **H1 and H2 are silent
(pure classes with their own tests, nothing reads them). H1b and H2b are NOT silent — each wires a
filed number or money into Core — so each ships in ONE PUSH with the screen that answers it (H1b
with H4's counter, H2b with H3's card): the game never files a comfort the player cannot clean or
a fine the player cannot see coming.** That is the review's first blocker, taken.

> **Working conditions, 2026-09-04/05.** Three other sessions are editing the same tree (the
> weekly job in `TycoonRun`, the bench/shaker art, the wall television; and an unowned batch —
> the one-clock patience, soft-drink pricing — sitting uncommitted in `CustomerVisit`,
> `TycoonConfig`, `DataLoader`, `FixtureDefinition`, `fixtures.json`). The Core phases are
> therefore split into NEW-FILE work (H1, H2: done) and WIRING work (H1b, H2b), which waits for
> those files to land and is staged hunk by hunk, never by whole file. The editor's test runner is
> read (`is_playing`, `HasRunningJob`) before it is queued, and play mode is never stopped from
> here; the new tests were first run through a scratch NUnitLite runner outside the editor.

---

## 0. Conflict ledger — the design vs the live game

| # | Live rule (where) | The design says | Ruling |
|---|---|---|---|
| C1 | A dirty glass auto-clears after 7 s (`BarDay.BusSeconds`; GDD_MEVCUT §3 "7 sn kilitler") | a glass stays until collected | **Adopt design.** The auto-clear was a stand-in for a verb that did not exist. PLAN_service_depth D2 measured the 7 s block as noise; the persistent glass is measured again (H1b). |
| C2 | `UpgradeStarCap = 2.0 + glass steps + 0.25/stool` caps the night; fixtures enter nothing | comfort caps the night; the cap's terms become comfort's base; fixtures carry `comfort` | **Adopt design** — the cap becomes visible. `AFreshBar_CapsAtExactlyTwoStars` and `TonightStars_AreHeldUnderTheFittingsAndTheMenu` keep their 2.0 because `FreeBase = 2.0` and the test nights clean as they go. |
| C3 | Tables are three single-piece slots (`fixtures.json`) | three per-slot ladders, rungs 1–3 | **Adopt design.** Data only. Existing ids retired; a saved run does not exist, so nothing migrates. |
| C4 | The heart is the licence's bond symbol (Id.cs, 2026-09-04) | the heart is the SERVICE symbol; a medallion is comfort | **Merge.** Both hearts mean *what a drinker feels about the bar*; the licence keeps its three, the boards and the top-bar strip get the rating. |
| C5 | Only rent may take the till below zero (GDD 23 §6; `EnsureAffordable`, the bin fee's clamp) | a fine may | **Adopt design (GDD 28 D3).** Written into GDD_MEVCUT §6 with H2b. |
| C6 | A declined order leaves an invisible Core dirty glass that blocks the stool 7 s (BarDay.Tick, `State != StormedOff`) | nothing is left when nothing was poured (`DrinkServed` is the signal) | **Adopt design** — it was a bug. |
| C7 | The slip prints RAW room stars; the week board and book print the CAPPED night | unchanged; the room gets its own slip row | **Keep** (GDD 27 D7). The standing board explains the min. |
| C8 | GDD 23 §8 still lists the back wall and the musician; §7 says `1 + 4x` | — | **Overtaken.** GDD 27 §3 and GDD_MEVCUT §7 are the ladder truth; the `1 + 4x` prose is stale everywhere (code is 5x, 2026-08-11) and is corrected in H1b's docs pass. |
| C9 | `IUnlockState` grows no more facts (its own comment) | comfort gates nothing | **Keep.** No new lock kind. |
| C10 | Regulars roll ages 21–67 on the `"customer"` stream | papers are rolled on a NEW `"papers"` stream, per person, opt-in with regulars; the bot's misread on a bot-only `"door"` stream | **Adopt design.** No existing stream draws one more number; from the first minor met the night diverges, which the A/B measures. |
| C11 | The top bar's standing block is "a row of stars and NOTHING ELSE" (the author, 2026-08-19; Chrome.cs) | two live readings under the caption | **Keep the ruling** (GDD 27 D9). The live readings are icon strips with no number, left of the block, not under it. |
| C12 | The slip's score row carries the star the author asked onto it (2026-09-04, DayEnd.cs) | the row would carry a heart and a medallion instead | **Keep the ruling** (GDD 27 D7). The score row is unchanged; the room gets its own row under it. |
| C13 | "Does this person count" is answered once, in `BarDay.FinishedCounted()` (its own comment) | a right kick is skipped in `ContinueToNextDay` | **Keep the ruling** (GDD 28 D10). The skip lives in `BarDay`, as `OnTheHouse` does; nothing downstream needs a second opinion. |
| C14 | `ServeTo` moves no money; the till collects when they get up (`SettleDepartures`) | the fine is charged at the serve | **Keep the ruling** (GDD 28 D6). The fine is charged in `SettleDepartures` after the tab. |
| C15 | `CrowdStarsTonight` reads `TonightStars` (the min) | — | **Change** (GDD 27 D8): the crowd reads `ServiceTonight`, so a filthy fresh bar (floor 1.0) can never fall under the broke line (0.625) by the mess alone. |

## 0b. Decisions

GDD 27 §8 (D1–D10) and GDD 28 §10 (D1–D10) are the flagged, reversible calls. Three change what
the suites pin:

- Every helper that plays a night **cleans as it goes** through one shared `TestNight.Clean(run)`
  (collect and wipe every mess after each tick; wash only when the hand holds something and the
  sink is idle): both copies of `PlayDayServingEveryone` (TycoonRunTests, NightReportTests),
  LastCustomerTests' `PlayToClosing` / `CloseTheNight` / `PlayUntilDay`, WeeklyJobTests' loop, and
  `TycoonCoreTests.AnOpenNight_SeatsAsManyAsTheStoolsCanTurnOver`, which drives `BarDay` directly
  and today relies on the 7 s auto-clear to turn its two stools (it collects through the `BarDay`
  verb in its own loop).
- `UpgradeStarCap` is **retired**, not aliased. The pin on its value moves to `ComfortBase`.
- The two pins that read `Floor.Dirty` and call `DirtyGlass.Bus()`
  (`AnEmptyGlass_BlocksTheStool_UntilItIsBussed`, `AnUnmatchedServe_StillLeavesADirtyGlass`) are
  rewritten against `Floor.Messes` and `CollectGlass` in the same commit — they are listed, not
  discovered.

## 0c. Already built — do not rebuild

| The design asks for | Exists as | Remaining gap |
|---|---|---|
| a rung is bought one at a time; rung N+2 hidden | `CanBuyRung`, `LadderLevel`, the aisle's `Level > LadderLevel + 1` skip (2026-08-26) | tables are not rungs; the tile copy does not say "mark n of N" |
| a glass left on the counter, click to take it | `BarDay.DirtyGlass` + `Bus()`, the HUD's `DirtyGlass` prop (D2, 2026-08-11) | it clears itself; nothing carries it anywhere; no smudge; no wash |
| the sink as a place to carry something to | the drain door (`BuildPropDoor`, `PointerOverDrain`, 2026-08-26) | a second carried thing (the dirty glass), and the water |
| a frame-sheet fixture animation | the wall television (`LoadScreenFrames`, `PlayTelevision`, 2026-09-04) | the cutter is hard-coded to the TV's 45×45 cell; the sink wants a water overlay sheet with its cell size in data |
| the card as the one place hidden information is shown | `ShowId`/`BuildIdCard`, `InspectId` gate | no verb on the card; no truth behind the printed age |
| a verb that sends someone away | `DeclineOrder` (bot and story only; books a 0.15 review, leaves the invisible glass) | not the kick: different books, different state |
| a small icon in two states and two sizes | `ItemArt.Star/Heart`, `Tools/heart_icon.py`, `Tools/icon_sizes.py` | the medallion |
| a per-night record every screen reads | `DayDetail`/`DayResult`, ask-then-close tests | seven new fields |
| the bot's private dice on a free stream | `rng.GetStream("hands")` in the simulator | a `"door"` stream for the misread |

---

## H0 — The rulebooks ☑ 2026-09-04

GDD 27, GDD 28, this plan; `_CHANGELOG` entry; CLAUDE.md pointer; GELISTIRME §8.3 rows. Reviewed
the same night by five adversarial passes (balance, Core/determinism/hidden information, the
screen, tests/sim/proofs, the standing rules and the author's words) run against the working tree;
27 findings, none dismissed unread. The ones that changed the design are the reversed decisions in
each module's last-but-one section and the ledger rows C11–C15 above; the ones that changed the
plan are the H1b/H4 and H2b/H3 pairings and the named test list in 0b.

## H1 — Comfort and cleanliness, silent (new Core files) ☑ 2026-09-05

- `Core/Tycoon/VenueComfort.cs` — the pure rules of GDD 27 §2–§3: `Base`, `Tonight`, `Now`,
  `NightStars`, the constants (`DirtPenalty` 1.0 after the review).
- `Core/Tycoon/Housekeeping.cs` — `CounterMess` (the 2026-08-11 `DirtyGlass` grown a smudge) and
  `Housekeeping`: the messes, the hand, the sink's timer, `DirtSpotSeconds`, `Cleanliness(seats,
  elapsed)` clamped, `DirtySpots`, `GlassesOnCounter`, the verbs `LeaveMess` / `CollectGlass(mess)`
  / `Wipe(mess)` / `WashGlasses()` / `Tick` / `CloseNight`, each refusing loudly.
- `Assets/Tests/EditMode/HouseTests.cs` — 21 pins: FreeBase 2.0; the glass share; stools; the
  clamp at five; the penalty floor above the broke line; a clean night loses nothing and a filthy
  one the penalty; the live reading per dirty seat; the min; grace exactness across a tick; one
  filthy stool on four is 0.75; cleanliness floors at zero; no wiping under a glass; a smudge
  left behind still counts; the sink wants a full hand and runs for the stack; glasses in the
  hand drain nothing; closing washes the hand free.

**Proof:** 32/32 (with DoorTests) on a scratch NUnitLite runner built from the same files, outside
the editor, because the editor was the author's for the night; the editor run follows with H1b.

## H2 — The door, silent (new Core files) ☑ 2026-09-05

- `Core/Tycoon/IdPapers.cs` — `DrinkingAge`, `Forgery {None, Borrowed, Altered}`, the value type
  (`TrueAge`, `PrintedAge`, `Forgery`, `LooksYoung`, `IsMinor`, `IsForged`, `IsHonestAdult`,
  `ShouldBeKicked`), `MinorChance(day)`, `Roll(rng, day, registryAge)` (never null), `FineFor`,
  `KickBonus` 5.
- `RegularState.Papers` (INTERNAL getter, set once through an internal door), `LooksYoung`
  (public), `Barred`; `RegularsRegistry.RollNext` passes over the barred without spending an
  extra draw.
- `Assets/Tests/EditMode/DoorTests.cs` — 11 pins: nineteen vs twenty; the borrowed card prints
  an adult; a card cannot lie the wrong way (and a minor always looks young); the odds by day;
  opening night rolls only honest adults; the same seed rolls the same papers; 4,000 draws land
  near 12% minors, half forged, a quarter of adults young; the roll touches no other stream; the
  roll refuses a minor from the registry; the fine curve; a person starts unasked and unbarred.

**Proof:** as H1.

## H1b + H4 — Comfort and cleanliness, wired and drawn ◐ *(H1b shipped 2026-09-05; H4 open)*

**Shipped 2026-09-05, the Core half — with the scene GATED, not paired.** The author said
"devam" while the tree was still shared, so H1b landed before H4 could: everything below under
"Core" is in, plus the four one-line HUD hunks that turn the old click into `CollectGlass`, and
the pairing rule is honoured by a gate instead — `TycoonConfig.CounterSmudges` (the rule is on
for the sim and every test) with `TycoonConfig.ForTheScene` handing the scene the marks OFF until
the cloth is drawn, so the scene's counter costs only what it shows: the glasses. H4 flips it.
Balance moved once on measurement (GDD 27 §7): v0 (penalty 1.0, grace 6 s, half the fixture
values, a cheapest-first bot) had a 20-second hand losing half a star and tripling bankruptcies
and a dressing-buying bot going 0%→4% bankrupt for a LOWER standing; v1 is penalty 0.75, grace
10 s, fixture values doubled, the bot buying by comfort per dollar. The 200-run report against
the tree before the change: bankruptcies 2 (1.0%) → 3 (1.5%), median till $84 / $136 / $199 → $64 / $76 / $87, standing
2.71 stars → 2.66 stars, 2.5★ reached 196 (98.0%) → 196 (98.0%), 3.0★ 24 (12.0%) → 8 (4.0%); service / comfort 2.94 / 2.99, cleanliness
100%, comfort-bound nights 2784 (46.5%), broke nights 0 (0.0%). The four shapes are in
`Docs/housekeeping_report.md`. EditMode 452/452 green.

Core (H1b):
- `FixtureDefinition.Comfort` (+ DTO `comfort`, appended after `screen`); `fixtures.json` values
  per GDD 27 §3.1; the three table ladders; `plant_monstera` from the orphan art.
- `CustomerVisit.DrinkServed` (internal set, only in `ServeTo`); `BarDay` owns a `Housekeeping`
  and exposes `Messes` (`Dirty` and `DirtyGlass` retired — C6, 0b); the departure tick calls
  `LeaveMess` only for `DrinkServed`; the stool gate reads `GlassesOnCounter`; `BusSeconds`
  gone.
- `TycoonRun`: `FixtureComfort` (standing rungs + singles), `ComfortBase`, `ComfortNow`,
  `ComfortTonight`, `ServiceTonight`; `StarCeiling = min(ComfortTonight, MenuStarCap)`;
  `CrowdStarsTonight` reads `ServiceTonight` (C15); `UpgradeStarCap` removed; the verbs
  `CollectGlass(mess)`, `Wipe(mess)`, `WashGlasses()` forwarded with the phase guard;
  `Housekeeping.CloseNight()` in the close block before `ComfortTonight` is read;
  `DayDetail.ServiceStars/ComfortStars` filed; `DevJumpToNight` resets.
- The sim bot: loads `fixtures.json`; in the pre-gate block where it busses today (so `buildTimer`
  is never charged): collect → wipe → wash-when-idle; buys the cheapest open rung at day end under
  the existing cushion, skipping the taps ladder; `Hands.NeverCleans` (collects, never wipes or
  washes) and `Hands.CleanLatencySeconds`; the report rows of GDD 27 §7.
- Tests: `TestNight.Clean` and every helper in 0b; pins moved consciously (`UpgradeStarCap` →
  `ComfortBase`; the two `Dirty` pins); ask-then-close for `ComfortTonight`; a dirty night files
  lower than a clean one on the same seed; a declined order, a storm-off and the guest leave
  nothing; an unmatched serve in a no-glassware run still leaves a glass; a run without fixtures
  still caps at 2.0; a filthy fresh bar still draws tomorrow's crowd off its service.
- Docs: GDD_MEVCUT §3 (7 s line), §6.2 (tables), §7 (the two ratings; the stale `1 + 4x`),
  §9 HUD row, a new `### 9.23`; GDD 23 §7/§8 stale lines struck; BALANCE.md regenerated with a
  comfort sheet.

Screen (H4):
- Smudge prop at the stool's spot (one list per stool; a wipe on the stool wipes all of it); the
  glass prop persists; the cloth prop (free, on the counter — its x measured in play against the
  book at stage 79 and the sink at 99–181); pick-up and travel-wipe over the mark with
  `ShedGrain`-style droplets; the glass carry (the garnish rail's grab/step/drop) released over
  `PointerOverDrain`; the water sheet over the sink and its coroutine (the TV's cutter with the
  cell size read from data); a synthesised tap loop; `Motion.Reduced` stills.
- The two icon strips left of the standing block (GDD 27 §4.4), no numbers.
- `Seats.cs:361/1806` and `TycoonHud.cs` move from `Dirty.Bus()` to `Messes`/`CollectGlass`.

**Proof:** EditMode green in the editor; the four sim shapes of GDD 27 §7 quoted in the commit
body against the baseline regenerated on the parent commit; in play, measured: the smudge appears
where the customer sat; wiping under a glass refuses with the house's nudge; the water runs for
`WashSecondsFor(n)` and stops; the medallion strip falls with a left glass and recovers when it is
washed. The bench and back-bar baselines are not touched by anything here (the counter's room-side
props are outside both crops) — verified by two green PlayMode runs, not assumed.

**Risk:** the star track. Halving the glass share lowers the ceiling a bot reaches by glass
alone, but the floor bot's nights are service-bound (≈ 2.8 stars) and will barely move; the
service-forced bot is what shows the cost, and the fixture-buying bot must climb at least as fast
as the baseline or the numbers move back before the push. Second risk: the persistent glass. The
bot collects instantly; the latency bot (10/20/30 s) is what shows what a human pays, and
`DirtPenalty` is chosen from that row.

## H2b + H3 — The door, wired and on the card (one push) ☐ *(waits for the tree)*

Core (H2b):
- `CustomerVisit.Papers` (throws until inspected), `VisitState.Kicked`, `OffTheBooks`, `Fined`,
  `FineOwed`; `OrdersAgain` refused for a minor with the verdict re-issued.
- `TycoonRun.NextArrival` rolls papers for a person whose `PapersRolled` is false, on `"papers"`;
  `Kick(visit)` with the five guards of GDD 28 §4 (`Bar()` on a right kick); `SettleDepartures`
  charges `FineOwed` after the tab (`DayFines`, may go below zero); the bonus paid at
  `Floor.IsComplete` with the rent (`DayBonus`); `DayIncome/DayExpenses` include them;
  `BarDay.FinishedCounted`/`AverageSatisfaction` skip `OffTheBooks`; the people count reads
  `StormedOff || (Kicked && !OffTheBooks)` as walked; `DayDetail.Fines/Bonus/RightKicks/WrongKicks/
  MinorsServed`; both reset points zeroed.
- The sim bot: after `InspectId`, kicks on `Papers.ShouldBeKicked`; `Hands.MisreadId` drawn once
  per visit from `rng.GetStream("door")`; report rows (minors met from `DayDetail`, right/wrong
  kicks, minors served, fines, fines by whole star, thanks, thanks as a share of net).
- Tests: papers throw before the card is read; a blind kick throws; a kick after a served round
  throws; the guest cannot be kicked; a right kick leaves `Floor.AverageSatisfaction` and
  `DayResult.Served/WalkedOut` exactly as a night without that visit and pays $5 at close, once;
  a wrong kick files 0 and counts as walked; a fined visit lowers `TillAfter` and shows in
  `DayResult.Fines` once they get up; a kicked visit leaves no mess; a barred face does not come
  back; a run without regulars never meets a minor; the same seed meets the same minors.
- Docs: GDD_MEVCUT §3 (the card's second job), §6 (fines, thanks, the second landlord), §9 HUD row.

Screen (H3):
- `papers.json` `"young"` rows; `LookFor` draws from the young pool off `visit.LooksYoung`; the
  lender booked per person beside the face; the honest minor's AGE field prints `PrintedAge`; the
  ticket and the log print the card's name once read.
- The KICK key in the licence's header band (GDD 28 §4), `run.Kick` in a try/catch → toast, the
  local-then-close sequence; the stool's exit; the log line; FINES / STATE'S THANKS rows and
  their `ChromeArt.Masks`; the people line and the critics off the filed record; the register's
  book line.
- PlayMode: the floor test still opens a licence and closes it; a new smoke test calls the public
  `StartNewRun(seed)` with a seed found headless (an EditMode helper builds the same run with
  regulars, `DevJumpToNight(2)` before any arrival, walks `NextArrival` until the second arrival
  is a minor), jumps to night 2 after `OpenTheBar`, seats two, opens the second card and presses
  KICK. Opening night has no minors by rule, so no seed alone can do it.

**Proof:** EditMode green; sim rows quoted in the commit body; in play: an honest minor's card
reads 19; a borrowed card shows a stranger's face and a stranger's name over the head; KICK on
each → `SHOWN THE DOOR`; KICK on an adult → the walk-out; a served minor's slip prints FINES with
the reason; a night with two right kicks prints `STATE'S THANKS · 2 SHOWN THE DOOR +$10`.

**Risk:** the seats minors take. A minor who is kicked was still a customer the door admitted,
and at ~6% of seats by week two that is a seat every other night that pays $5 instead of a drink
and a tip. Measured, and `MinorChance` is the lever.

## H5 — The two ratings (UI: symbols, slip, boards, the upgrade screen) ☐

- `Tools/medallion_icon.py` → `medal3d[_socket][_16].png`; `ItemArt.Medal(lit, px)`; the
  contact sheet reported before the icon enters the game.
- The slip's comfort row (GDD 27 §6); the standing board's SERVICE / COMFORT / TONIGHT / TOMORROW
  rows on a 460-unit board with `StandRow`'s unit sprite; the register's book line; the week
  board unchanged.
- The UPGRADES tab: `MARK n OF N · +0.20 COMFORT` copy, the owned rung as a trail, the crate.

**Proof:** in play, photographed: a clean night and a filthy night on the same seed, both boards
read; the `basket.png` baseline re-blessed only if the basket's foot moved (it should not — the
market's foot is the compared crop), and only after the session that owns it has been told.

## H6 — Content and the second tell ☐

- Painting rungs 2–3 and the marble sink: generated, reported in HTML, shipped through the
  fixture tools; the `comfort` numbers of GDD 27 §3.1 already wait for them in the design.
- The altered card (flag tell): `Forgery.Altered`, the HUD's mismatched flag, a second sim row.
- Balance pass two on the 200-run report with everything on; GDD_MEVCUT as-built.

---

## Standing rules for this arc

1. **Core decides, in verbs the bot can call.** Every rule here is reachable by `TycoonRun` verbs
   the simulator uses — if the bot cannot wipe, the drain is decorative and the survival floor lies.
2. **Hidden information stays hidden.** The truth behind the card is Core-only; papers throw until
   the card is read; the kick refuses a card that was not read; the one fact the room may see
   (`LooksYoung`) is shared with adults so it is never the verdict.
3. **Nothing moves money without the sim.** H1b and H2b quote the shapes in their commit bodies
   against a baseline regenerated on the parent commit; a shape that moved the wrong way moves the
   numbers back before the push.
4. **A filed number and its verb ship together.** H1b with H4, H2b with H3.
5. **The tree is shared.** New files first; wiring by hunk; the index stays empty between commits;
   the editor's test runner is read before it is queued, and play mode is never stopped from here.
6. **As-built follows shipped.** GDD_MEVCUT is updated in the same commit as the rule it describes,
   never ahead of it.
