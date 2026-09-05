# LAST CALL — GDD Module 27: The House (venue comfort, cleanliness, the upgrade ladder)

**Status:** design, reviewed, the pure Core half built. Written 2026-09-04 from the author's
brief: *"Oyuncular hem alkolü puanlar hem mekanı, 2 ayrı metrik olacak. 1'si mekandaki
geliştirmelere ve mekanın temizliğine bağlı puanlama, diğeri servis, servis edilen ürünün
puanlaması. Bu ikisi ayrı metrikler olacak fakat ortak yıldızlar olacak. … Bu konfor artmadan ne
kadar iyi servis yaparsan yap genel yıldızın değişmeyecek."* Reviewed the same night by five
adversarial passes against the working tree (balance, Core/determinism, the screen, the tests,
the standing rules); what they found is folded in below and the reversed calls are listed in §8.
Staging lives in `Docs/PLAN_house_and_law.md`; this module is the rulebook for the room. Module
28 is the rulebook for the door (age, papers, the kick) — the same brief, the other half. Where a
line here disagrees with `Docs/GDD_MEVCUT.md` after a phase ships, GDD_MEVCUT wins.

---

## 1. Why this exists

The loop has one axis. Every night is filed as one number — the customers' satisfaction turned into
stars, clipped by two ceilings (`UpgradeStarCap`, `MenuStarCap`) and stepped into an inertial
standing (`BarRating`, GDD 23 §7). The ceilings already say *"a dive cannot be a five-star bar
however well it pours"* — but they say it invisibly: a player who serves a perfect night in a
two-star room sees a 4.9 on the slip, a 2.0 in the books, and a CEILING row they have to read to
find out why. The room itself — the tables, the sink, the lamps, the pictures — is dressing that
touches nothing (fixtures enter neither the cap nor the ambience bonus; `FixtureDefinition.Stars`
is a *gate*, not a contribution).

The author's ruling turns the invisible ceiling into a rating of its own. **Two ratings, one row of
stars:**

- **SERVICE** — what the customers thought of the drink. This is the number the game already
  computes every night.
- **COMFORT** — what the room is worth: its fittings, drained by the night's mess.
- **THE STANDING** — the stars in the corner, stepped each night from the LOWER of the two. Comfort
  that has not risen holds the stars down however good the service was; service that has not risen
  holds them down however fine the room is.

And the room now has a night of its own to lose: every customer leaves a mark on the counter and an
empty glass, and the bar wipes, collects, carries and washes — or the room's rating pays for it.

## 2. Two ratings, one row of stars

### 2.1 Service tonight

Unchanged in substance. `ServiceTonight = min(BarRating.ExactStarsFor(Floor.AverageSatisfaction),
MenuStarCap)` — the room's mean satisfaction in stars, held under the menu ceiling, because what
was *served* includes what the menu allowed to be served. The menu ceiling stays on the service
side on purpose: it is about the drink.

Who is in that mean is answered in ONE place, `BarDay.FinishedCounted()` / `AverageSatisfaction`
(the file's own words: *"'does this person count' is answered once, here"*): storm-offs count as
0, the guest of the house is skipped (GDD 26 §3), and — new — a customer rightly shown the door is
skipped too (module 28 §4: a bounced minor is not a customer and does not rate the bar). A wrongly
bounced adult counts, at 0.

### 2.2 Comfort tonight

`ComfortTonight = clamp(ComfortBase − DirtPenalty × (1 − Cleanliness), 0, 5)`

- `ComfortBase` — what the room is worth with nobody in it (§3). Fittings only. It does not change
  during a night; it changes at the market.
- `Cleanliness` ∈ [0, 1] — the share of the night the counter was clean, time-weighted per seat and
  CLAMPED (§4.3). A bar that wipes and collects as it goes scores 1.0 and loses nothing.
- `DirtPenalty` — the most a filthy night can take off the room: **0.75** (balance v1, measured
  — §7). Under a star, on purpose: the room opens at 2.0, so a filthy fresh bar still files
  1.25 — well over the broke line (0.625) even if the crowd read this number (it does not,
  §2.3). The review's first draft said 1.5; that made the mess worth three quarters of a fresh
  bar's whole ceiling. The first measurement said 1.0 with a six-second grace, and the latency
  row (§7 shape 4) showed a hand that reached each mess in twenty seconds losing half a star
  of standing and tripling its bankruptcies — the ordinary pace of a four-stool bar priced
  like never cleaning at all. Three quarters, with the grace at ten seconds, is the number that
  lets that hand lose a tenth of a star while a bar that never wipes still loses the room.

There is also a **live** reading for the shift, `ComfortNow = clamp(ComfortBase − DirtPenalty ×
DirtySpots / Seats, 0, 5)` — the same rule read off the counter as it stands *this second*
(`DirtySpots` counts messes past their grace, capped at `Seats`), so a gauge drops when a glass is
left and recovers when it is carried away. The night's filed number is the time-average, not the
last frame.

### 2.3 The night's stars

`TonightStars = min(ServiceTonight, ComfortTonight)`

Filed exactly where the night is filed today: `BarRating.CloseNight(avg, cap)` with
`StarCeiling = min(ComfortTonight, MenuStarCap)`. `UpgradeStarCap` is **retired** — its two terms
(glass steps, extra stools) move into `ComfortBase` (§3), so the fittings ceiling the loop already
leaned on becomes a visible rating instead of a hidden clamp. `Nights`, `BestNight`, `NightStars`,
the record stamp and the standing's inertial step all keep reading the capped night.

**Tomorrow's crowd reads the SERVICE side.** `CrowdStarsTonight` (which picks HighRollers /
Regulars / the Broke crowd through `BarRating.CrowdFor`) is `ServiceTonight`, not the min. The
crowd is the customers' mood about last night's drinks — a fine room with poor service still draws
a broke crowd, a filthy room with fine service still draws the rollers — and it keeps the mess
from being a price spiral: comfort holds the STANDING down, it does not by itself take the tips
away. (`CrowdStarsTonight` today reads `TonightStars`; this is the one consumer that changes.)

### 2.4 What gates what

`Rating.Average` (the standing) stays the single gate for everything it gates today — brand rungs,
recipe ranks, fixture star gates, story guests, which faces walk in. Comfort gates nothing except
the night's stars. Two axes in the unlock system would drag every "next at n.n stars" hint into a
rung that opens nothing (the trap the code names twice); one axis, fed by the min, needs no new lock
kind and no change to `IUnlockState`.

### 2.5 Three symbols

The author: *"iki farklı sembol bulmalıyız, örneğin madalyon ve kalp vs. gibi."*

| Reading | Symbol | Colour | Art |
|---|---|---|---|
| Service | **heart** | ViceRed | `ItemArt.Heart` (exists — drawn 2026-09-04 by `Tools/heart_icon.py`) |
| Comfort | **medallion** | Cyan ramp, brass rim | new: `Tools/medallion_icon.py` → `Items/medal3d[_socket][_16].png`, `ItemArt.Medal(lit, px)` |
| The standing | **star** | Amber | `ItemArt.Star` (exists) |

Same construction as the star and the heart (32 px, one-pixel ink keyline, three tones lit from
the upper left, one sparkle; socket state in the socket violets; 16 px derived by
`Tools/icon_sizes.py`); same contract (own colour, alpha only, never tinted). The licence's three
bond hearts (how well *this* customer knows you) stay: a heart there and a heart on the slip both
mean *what a drinker feels about the bar*, one person and the whole room. One exception to the
symbol table, owned in §6: the slip's customer rows keep the STAR the author asked onto them on
2026-09-04.

## 3. Comfort base — what the room is worth

`ComfortBase = FreeBase + Σ fixture comfort + GlassComfortShare × Σ GlassStepCap + StoolComfort ×
(Seats − StartingSeats)`, clamped to 5. Built: `VenueComfort.Base(fixtureComfort,
glassStepCaps, extraStools)`.

- `FreeBase = 2.0` — the room as it opens. Deliberately the number `UpgradeStarCap` opened
  with, so a fresh bar still caps at 2.0 and every pin that says so stays true.
- **Fixture comfort is DATA:** a new `comfort` field on every entry in `fixtures.json`
  (`FixtureDefinition.Comfort`, ≥ 0, carried through `DataLoader` like `drainsFree` and `screen`).
  The pieces the room starts with carry **0** — they are the FreeBase. Summed over the STANDING rung
  of each ladder slot plus every owned single piece; a fitted-over rung counts nothing (rungs carry
  absolute values, not increments), which is the same filter the room uses to decide what to draw.
- **Glassware** keeps counting, at half weight (`GlassComfortShare = 0.5` of the measured
  `GlassStepCap {0.20, 0.15, 0.12, 0.08, 0.05}` per line — up to +1.5 over five lines instead of
  +3.0). It was the only route to the ceiling and it must stop being the only route; halving it
  rather than removing it keeps the front-loaded shape the sim tuned against bankruptcies.
- **Stools** keep +0.25 each (`StoolComfort`), two at most.
- Five stars of comfort is a LONG purchase: fittings are one a night (`MaxUpgradesPerNight`), so
  the glass and stool part alone is about a month of nights, and dressing (which never spends the
  night's fitting) is what lets a bar get there sooner. That is the endgame shape, on purpose.

### 3.1 The ladders (balance v0)

| Slot | Rung 1 | Rung 2 | Rung 3 | Notes |
|---|---|---|---|---|
| `table_left` / `table_mid` / `table_right` | rustic $40 · +0.2 | brass $85 · +0.4 (1.5★) | steel $120 · +0.6 (3.0★) | **three per-slot ladders** — *"masa eklemek"* is rung 1, *"lvl1 masa varsa önce lvl2"* is the ladder. Art exists (`fx_table_t1/t2/t3`). Today's three single tables become the rung-1/2/3 art of each slot. |
| `wall_lamps` | mark 1 (ours) · 0 | mark 2 $55 · +0.3 | mark 3 $90 · +0.7 | exists |
| `sink` | steel (ours) · 0 | brass $85 · +0.4 (drains free) | marble $140 · +0.8 (3.0★) | rung 3 **needs art** (`Tools/sink_fixture_gen.py`) — ships when drawn |
| `wall_center` (the picture) | triptych (ours) · 0 | canvas $70 · +0.5 (1.5★) | gallery $120 · +0.9 (3.0★) | rungs 2–3 **need art** |
| `plant_left` | palm $20 · +0.1 | fiddle $55 · +0.2 | pothos $95 · +0.4 | exists |
| `plant_right` | snake $25 · +0.1 | agave $70 · +0.2 | monstera $95 · +0.4 (3.0★) | rung 3 uses the orphan `fx_monstera` |
| `taps` | one (ours) · 0 | two · +0.1 | three · +0.2 | the beer ladder is about beer; a token — and the sim's rung-buying bot SKIPS this ladder so kegs stay out of the A/B (§7) |
| `walls` (the back wall) | cracked plaster (ours) · 0 | fresh plaster $70 · +0.3 (1.0★) | panelled $130 · +0.6 (2.0★) | **FOUR rungs, the author's own plates (2026-09-06)** — rung 4 is the harlequin paper, $200 · +1.0 (3.0★). A rung of this ladder is the whole 640×360 room (`backdrop` slot: the sprite REPLACES the plate, nothing stands at a hook), so the market tile shows a 64×48 `swatch` of the wall instead of the room shrunk. The bar opens in the cracked room; the door's sign reads +20 ONLY on every plate. |
| singles: candle, sconce, hanging lantern, paper lantern, neon | +0.2 each | | | lit dressing |
| ours from night one: rug, mat, tv | 0 | | | the FreeBase |

These are the **v1** numbers, twice the first draft's: measured against the glass ladder
(§7), a v0 candle was $30 for a twentieth of a star where a $12 glass step buys a tenth, and a
bot buying the room by price went from 0% to 4% bankruptcies for a standing that went DOWN.
Budget with every rung that has art today: 2.0 + 1.8 + 0.7 + 0.4 + 0.2 + 0.4 + 0.4 + 1.0 + 1.5 +
0.5 + 1.0 (the walls, 2026-09-06) = **9.9 → 5.0**. Five stars of comfort is reachable without the two art-dependent ladders and
without every rung; the player chooses. The sim keeps moving them (§7).

### 3.2 Ladder rules (all existing, restated so the module is whole)

- A rung is bought one at a time (`CanBuyRung`: `Level == LadderLevel(slot) + 1`); the market shows
  the owned rung and the NEXT one only (2026-08-26, *"3. seviye 2. seviyeyi açmadıysan
  gözükmemeli"*); the lower rung stays owned and fitted over; only the tallest is drawn.
- Star gates on rungs stay (`Stars` is the standing the market wants before it will sell).
- Fixtures never spend the night's one fitting.
- NEW: the tile says what it buys — *"MARK 2 OF 3 · +0.20 COMFORT"* — and the aisle is headed
  UPGRADES, not DRESSING (§5).

## 4. Cleanliness — the counter has a night of its own

The author: *"Oyuna tezgah kirliliği eklenecek, her müşteriden sonra tezgahtaki bez ile tezgahı
silmen gerekecek. Tezgahta müşterilerin bıraktığı bardakları toplaman gerekecek. Toplanılan
bardakları lavaboya götürmelisin. Lavaboda su açılma animasyonu devreye girecek ve bardaklar
yıkanıyor hissiyatı verilecek. Bardaklar toplanmadıysa, tezgah silinmediyse bu konfor puanını
düşürecek."*

### 4.1 What a customer leaves

When a customer who was SERVED A DRINK gets up, a mess lands on the counter (`CounterMess`):

- **their empty glass** — it stays until COLLECTED, and while it stands there the stool is taken
  (the rule the bar already has: `_seated + glasses on the counter < Seats`).
- **a smudge** — a ring, a wet patch. It stays until WIPED.

The signal is `CustomerVisit.DrinkServed`, set only by `ServeTo`. Nothing else leaves anything: a
storm-off (nothing was poured), a declined order (today it leaves an invisible Core glass that
blocks the stool for seven seconds — a bug this module fixes), a kicked customer (module 28), the
guest of the house (GDD 26 §3 — outside the books and outside the mess). An unmatched serve in a
run built without glassware still leaves a glass (its `GlasswareId` is null; the pin
`AnUnmatchedServe_StillLeavesADirtyGlass` stands).

**The seven-second auto-clear is retired** (`BarDay.BusSeconds`). A glass nobody collects stays
where it was put down. That is the whole mechanic.

### 4.2 The four verbs (built: `Housekeeping`, owned by `BarDay`, forwarded by `TycoonRun`)

Core has no seat index — the floor counts stools, the room places them, and the HUD claims each
leaver's mess for the stool it drew them on, exactly as it claims the glass today. So the verbs
take the MESS, not a seat.

| Verb | Precondition | Effect |
|---|---|---|
| `CollectGlass(mess)` | the glass is still there; day open | glass leaves the counter → `GlassesInHand++`; the stool is free again |
| `Wipe(mess)` | the spot is smudged and NO glass stands on it | smudge gone; a clean spot leaves the list. *Collect first, then wipe* — you cannot wipe under a glass |
| `WashGlasses()` | `GlassesInHand > 0`; the sink is not already running | the sink runs for `WashSecondsFor(n) = 1.5 + 0.5 × n`; the hand is empty from that moment and the glasses are clean when it stops |
| `DiscardGlass()` | unchanged | pouring a drink away does not wait for a wash — the sink has a drain |

Glasses in the hand are off the counter and drain nothing; there is no hand capacity. The cost of
hoarding is one longer wash and a sink that is busy longer. A stool re-seated under an unwiped
smudge gets a SECOND mess when that customer leaves; the HUD keeps a small list per stool and a
wipe on the stool wipes all of them, and Core caps `DirtySpots` at `Seats` and `Cleanliness` at
1.0 so a stool can never cost more than a stool. (A glass *supply* — running out of clean glasses
— is fenced out, §9.)

### 4.3 Exposure and the two readings

Every tick, for every mess: if it is still dirty and has stood for longer than `DirtGrace`
(**10 s** — time to notice and reach for the cloth; six in the first draft, and at six the bar
was paying for the walk across the counter), it adds `dt` to `DirtSpotSeconds`; a mess that
crosses the line inside a tick pays only for the part past it.

- `Cleanliness = clamp(1 − DirtSpotSeconds / (Seats × Elapsed), 0, 1)` — against the night the
  floor ACTUALLY ran (`Floor.Elapsed` keeps ticking past closing until the last stool empties),
  never the config's 95. One spot dirty past its grace for the whole of a four-stool night reads
  0.75; ten customers' marks each left twenty seconds past the grace read roughly 0.47.
- `ComfortTonight` (§2.2) reads Cleanliness once, at close, and is filed with the night.
- `ComfortNow` reads the spots as they stand — the shift's gauge, and what the sim prints per tick
  when it is asked to.
- **Closing:** `Housekeeping.CloseNight()` runs in `Tick`'s close block (the `Floor.IsComplete`
  branch, with the rent) BEFORE `ComfortTonight` is read: whatever is still in the hand and
  whatever is running in the sink is washed for free, and whatever is still on the counter has
  already been paid for in exposure. Nothing carries over to tomorrow's counter.

### 4.4 What it looks like (module 24 owns the presentation; the beats are named here)

- **The smudge** is a small prop at the stool's spot on the drawn counter; **the glass** is the
  dirty-glass prop the HUD already draws (`GlassArt` of the served glassware, dim).
- **The cloth** is a permanent prop on the counter — free, always there, not a fixture. Click to
  pick it up; wiping is *travel over the mark*, not a drop (the rim lap and the grain trail are the
  house's two "skill by cursor travel" verbs; the author has twice refused a one-drop skill).
- **Collecting** is a click on the glass (as today); the glass joins a small stack in the hand.
  **Carrying** is walking the pointer to the sink and clicking it.
- **Washing** is the sink's own animation: a frame sheet of water running from the tap into the
  bowl, played over whichever sink rung the bar owns (one water sheet, drawn above the sink art;
  the television's frame cutter takes its cell size from data instead of a constant so the sink
  can use it), for `WashSecondsFor(n)`; a tap-running loop under it (synthesised, the bank's own
  way); the stack in the hand shrinks to nothing as it ends. `Motion.Reduced` → a still frame and
  the timer.
- **The gauge:** the top bar's standing block is *"a row of stars and NOTHING ELSE"* (the author,
  2026-08-19 — the decimal was taken off it on purpose) and stays so. The live readings are two
  small ICON STRIPS with no number — five 16 px hearts filled to tonight's service so far, five
  16 px medallions filled to `ComfortNow` — standing LEFT of the star row on its own line, in the
  empty beam between the week strip and the standing block. Measured in play (PLAN H4), not
  assumed.

## 5. The market reads as an upgrade screen

The author: *"Geliştirmeler atlanamayacak … Yani satın alma ekranından daha çok geliştirme ekranı
gibi olmalı."* The ladder machinery is built; what changes is the shop window.

- The UPGRADES tab keeps its order (stool · counter · glass lines · then the room) but every tile
  that is a rung says so: **MARK n OF N**, the comfort it adds, the standing it wants. The owned rung
  shows as a trail under the next one, not as its own full tile.
- Tables become three ladders in data (§3.1); the aisle already hides rung N+2.
- The "n more waiting" crate keeps skipping rungs that are blocked by order, not by stars.

## 6. Where the two ratings show

- **Top bar** (the shift): stars = the standing, as now, nothing else on the block; the two icon
  strips of §4.4 to its left.
- **The slip** (day end): the five big stars stay the CUSTOMERS' rating (raw room stars, as the
  slip prints today) and the score row under them keeps its star, its number and its people
  (`★ 4.6 · 10 SERVED · 1 WALKED` — the author asked that star onto the row on 2026-09-04, and it
  stays). Under it, ONE new row, the room's, in the same language: five 16 px medallions filled
  to `ComfortTonight`, the number, and a word for the night (`SPOTLESS` ≥ 0.95 cleanliness ·
  `TIDY` ≥ 0.75 · `A MESS` ≥ 0.4 · `FILTHY`). It costs 26 units of paper; on the nights that are
  already at the paper's 496-unit limit (two critics and a strike) `FitBillToPaper` shrinks the
  print by about five percent, and that is accepted. This is the author's sentence honoured
  literally — *"konfor puanı fiş ekranında gözükür, müşterilerin o gün verdiği puan gibi"* — and
  it is why the review's first draft (both numbers crammed onto the score row) was reversed: that
  row cannot hold two icon-figure pairs and the people in 384 units.
- **The standing board** (the right instrument): rows SERVICE (heart unit) · COMFORT (medallion
  unit) · TONIGHT (star, the min; its caption carries the menu ceiling when that is what bound —
  `TONIGHT · menu 2.5`) · TOMORROW. Four rows where there were three (CEILING folds into
  TONIGHT's caption); the board grows to 460 units to hold them and the foot note is held to two
  lines: *"THE ROOM HELD THE NIGHT — CLEAN UP OR BUY THE FITTINGS"* / *"THE SERVICE HELD THE
  NIGHT"* / *"THE MENU HELD THE NIGHT"*. `StandRow` gains a unit sprite (16 px, a whole multiple
  of the icon's 8 px grid — the 13 px it draws today is not).
- **The week board:** unchanged rows; comfort is in the book.
- **The register's book:** the room line gains `· comfort x.x`.
- `DayDetail`/`DayResult` gain `ServiceStars` and `ComfortStars` beside `NightStars`, so every
  reader above reads the same filed numbers (NightReportTests' ask-then-close shape applies).

## 7. Numbers (balance v0 — starting stakes, the sim moves them)

| Constant | Value | Where (as built) |
|---|---|---|
| `FreeBase` | 2.0 | `VenueComfort` |
| `GlassComfortShare` | 0.5 | `VenueComfort` |
| `StoolComfort` | 0.25 per extra stool | `VenueComfort` |
| `DirtPenalty` | 0.75 (v1; 1.0 in v0) | `VenueComfort` |
| `DirtGrace` | 10 s (v1; 6 s in v0) | `Housekeeping` |
| `WashBaseSeconds` / `WashPerGlassSeconds` | 1.5 s / 0.5 s | `Housekeeping.WashSecondsFor` |
| fixture `comfort` | §3.1 | `fixtures.json` |

**What the sim must show before the wiring ships** (`LastCall → Simulate Tycoon 200 Runs`;
seeds TYC-0000..0199, 30 days). The BASELINE is the report regenerated on the phase's parent
commit and quoted beside the new one in the commit body — never a checked-in file, which today
reads 7.0% bankruptcies at HEAD and 1.0% in another session's uncommitted tree. New report rows
the shapes need: `avg ComfortTonight`, `avg Cleanliness`, `nights comfort-bound` (ComfortTonight <
ServiceTonight), `ComfortBase by day p25/p50/p75`, `rungs bought by slot`, `Broke nights per run`.

1. **The floor bot, cleaning instantly, buying no dressing:** customers per night, storm-offs and
   income unchanged; 2.5★/3.0★ reached-share and median day may come later (a cap glass alone can
   no longer give). Run it TWICE — the floor bot (service ≈ 2.8 stars, which the old cap never
   bound past week one, so its rung table will barely move) and a service-forced bot (hands pinned
   to ≈ 0.9 satisfaction) whose nights ARE comfort-bound; the pass criterion is written on the
   comfort-bound row, not on the rung table.
2. **The same bot buying the cheapest open rung each night** (skipping the taps ladder, so kegs
   stay out of the A/B): 2.5★/3.0★ at least as early as the baseline, bankruptcies not worse than
   baseline + 2 points.
3. **`Hands.NeverCleans`** (collects at once so stools still turn; never wipes, never washes):
   `avg ComfortTonight` well under `FreeBase` (v0 expectation ≈ 1.0–1.3: spots dirty only from the
   first departure, ~20 s in, plus the grace), the standing stalls under 1.5, customers per night
   unchanged, Broke nights NOT up (the crowd reads service, §2.3).
4. **`Hands.CleanLatencySeconds` at 10 / 20 / 30 s** — the human case, which the first three do
   not measure: the number `DirtPenalty` is picked from this row, so that a bar that cleans at an
   ordinary human pace loses a tenth of a star, not a whole one.

## 8. Decisions (flagged, reversible)

- **D1 — comfort CAPS the night, it is not a second standing.** `StarCeiling = min(ComfortTonight,
  MenuStarCap)`; the standing stays one inertial number.
- **D2 — glassware keeps half its old weight.** Removing it entirely would make five lines of
  bought glass worth nothing to the stars; keeping it whole would make the room's fittings
  optional. Half, and measure (shape 1, twice).
- **D3 — the glass still takes the stool** while it stands there. Two currencies (throughput,
  comfort), not a double charge in one — and the bar already plays this way.
- **D4 — tables are three per-slot ladders**, not one three-slot ladder. Zero ladder code; *"masa
  eklemek"* is literally rung 1; the room shows tables arriving one by one.
- **D5 — the ambience bonus stays as it is** (`+0.006/glass step`, `+0.03/counter tier`, on the
  service side). Folding it into comfort is a balance move with no author ask behind it.
- **D6 — no glass supply.** Washing is about the room, not about running out of glasses.
- **D7 — the slip's big stars stay the customers' raw rating and the score row keeps its star;
  the room gets its own row.** Three surfaces (slip, week board, book) still print three honest
  numbers (raw, capped, filed) and the standing board explains the min — that was true before
  this module and is not made worse by it.
- **D8 — tomorrow's crowd reads the service side** (§2.3). Reversed from the first draft, which
  let the min pick the crowd and put a filthy fresh bar under the broke line.
- **D9 — the top bar shows icon strips, not decimals** (§4.4). The author's 2026-08-19 ruling on
  that block stands.
- **D10 — `DirtPenalty` is 0.75 and `DirtGrace` 10 s, chosen from the latency row** (shape 4)
  before the wiring shipped; the fixture values doubled from the same measurement (§3.1). The
  numbers the wiring landed with are in PLAN_house_and_law H1b.

## 9. Out of scope (fenced deliberately)

- A glass inventory (running out of clean glasses), floor mopping, toilets, spills on the floor.
- Comfort as a gate on purchases or story guests (§2.4).
- The bartender (Ece, GDD 26 §1b) doing the washing — a later beat, if ever.
- A comfort standing with its own inertia (D1).
- Paintings/sink rung 3 ART: specified here, shipped only when drawn and reported (memory:
  new assets are reported in HTML before entering the game).
