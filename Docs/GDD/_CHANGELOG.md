# GDD Changelog

## v4.0 (current) — THE TYCOON PIVOT (in progress)

- **27's fourth ladder — the wall (2026-09-06):** the author's four room plates (the cracked
  plaster the bar opens in, fresh plaster, the panelled wall, the harlequin paper) sell as a
  ladder in a `backdrop` slot whose rung REPLACES the room's plate; the market shows a 64×48
  `swatch` of each wall. The door's sign reads +20 ONLY on all four. GDD_MEVCUT §9.26.

- **26's host speaks — S5 (2026-09-05):** the seven written lessons are said. Core observes
  each `StoryCue` where it is true (`TycoonRun.Lessons.cs`), `StoryProgress.Learn` spends it
  once a run, `LessonDue`/`HeardLesson` hand it to whoever draws the bar: the dialogue plate on
  an open night, a 98 message box on the market at the close. The shelf warning looks along the
  arc for this week's guest. The book's title page carries the OPEN TAB (§5) off
  `StoryProgress.CurrentAsked`. GDD_MEVCUT §9.25.

- **28's second tell — H6, the code half (2026-09-05):** the ALTERED card. Half the forged
  cards are the minor's own with the year bumped to 21–24 and a flag that is not their
  country's (`WrongFlagFor`, stable per person); the log and the slip name it. The picture
  rungs 2–3 and the marble sink still wait on drawings the author picks.

- **27 on the boards — H5 (2026-09-05):** the two ratings are seen: two strips of five
  (heart = tonight's service, medallion = the room's comfort this minute) left of the
  standing in the top bar, no numbers; the slip's house row under the score; SERVICE and
  COMFORT rows with their own symbols over TONIGHT on the standing board (both boards
  420 → 460); the upgrade tiles say "Mark n of N · +comfort". `ItemArt.Medal` joins Star and
  Heart. GDD_MEVCUT §9.23.

- **27 on the counter — H4 (2026-09-05):** the scene pays for its marks now
  (`ForTheScene` → smudges on). The glass is picked up and carried (washed over the sink, kept
  in the hand otherwise), the mark is a drawn smudge until the cloth on the counter's left end
  is passed over it, the sink's click washes what the hand holds and the tap runs as a frame
  sheet drawn off the sink's own silhouette (`Tools/sink_water_gen.py`) for exactly Core's
  `WashSecondsFor`, with a synthesised `tap_water` loop; sheets state their own cell in
  `fixtures.json` (`cellW`/`cellH`), the television included. GDD_MEVCUT §9.23.

- **28 wired and on the card (2026-09-05, H2b `6c40b5cb` + H3):** every person carries papers
  from their first arrival (`"papers"` stream, Core-only truth, `CustomerVisit.Papers` throws until
  the card is read); from the second night some arrivals are under twenty, half on a borrowed card
  that prints the LENDER's face and name; `TycoonRun.Kick` with its five guards — a right kick off
  the books, the face barred, $5 thanks at close; a wrong kick a walk-out at zero; a served minor
  fined `$20 + $20/whole star` when they get up, after the tab, with no extra round; the KICK key
  in the licence's header band, the slip's THANKS and FINES rows, the register's line; the bot
  reads every card (`KickIfDue`, `Hands.MisreadId`). 200 runs: 5.4% of seats are minors, the floor
  bot shows every one the door, thanks 2.1% of income, bankruptcies 1.5% → 0.5%. GDD_MEVCUT §9.24.

- **2026-09-05 — one session again; nine commits consolidate what four left half-done**
  (`6cbe1f7b` … `d468216d`): the ONE patience clock (`6cbe1f7b`, GDD_MEVCUT §9.22 — taking the
  order pays one of the gauge's three boxes back, never a reset); the tap locked while a cocktail
  is on the go (`b136a9c8`); **module 27 wired — H1b** (`6673cb47`, GDD_MEVCUT §9.23): the night
  files `min(service, comfort)`, the counter keeps its mess (collect / wipe / wash, ten-second
  grace, `DirtPenalty` 0.75, nothing clears itself), tables are three ladders, the sim bot keeps the
  counter and buys the room by comfort per dollar — the scene's MARKS are gated off
  (`TycoonConfig.ForTheScene`) until the cloth is drawn (H4); the counter's one foot line and the
  coaster's body (`ae732239`); the slip's drawn dollar and star and the week's take (`3d48d683`);
  the room's art as it stands (`7ba2f53a`); the tin bench rebuilt — slate, the shaker as its own
  gauge, the napkin, the ÇÖP key, the cap-off pour (`2378c708`, bench baseline re-blessed); and
  the sweep (`d468216d`): every plate nothing loads, the archetype portraits, the dead fallbacks —
  `ItemArt.Bottle` is v4 → the garnish's dish → null. GDD 23 §7/§8 lose their stale lines; the
  README and GDD_MEVCUT §3 stop describing two clocks. Still to build: H2b/H3 (the door), H4 (the
  cloth and the sink), H5 (the two symbols on the boards), H6 (content).

- `27 / 28 / PLAN_house_and_law` **the house and the door are designed (2026-09-04, design
  only — nothing wired):** the author's brief — *"oyuncular hem alkolü puanlar hem mekanı, 2
  ayrı metrik olacak … ortak yıldızlar olacak"* — becomes two rulebooks. **Module 27, the
  house:** SERVICE (the night's mean satisfaction under the menu ceiling) and COMFORT (what the
  room is worth: `FreeBase` 2.0 + a `comfort` number on every fixture in `fixtures.json` + half
  the old glass-step cap + the stools, drained by the counter's mess) are two ratings and the
  night files the LOWER — `UpgradeStarCap` retires into comfort's base, so the invisible fittings
  ceiling becomes a visible reading with its own symbol (a medallion; the heart is service, the
  star is the standing). The counter has a night of its own: a served leaver leaves a glass AND
  a smudge, the glass holds the stool until collected and nothing clears itself any more; four
  verbs — collect, wipe (never under a glass), carry to the sink, wash (the tap runs
  `1.5 + 0.5/glass` s) — and dirt past a six-second grace costs seat-seconds that read as
  `Cleanliness`, at most one star off the room. Tables become three per-slot ladders; the
  UPGRADES tab says "mark n of N · +comfort". **Module 28, the door:** drinking age 20; from the
  second night some arrivals are minors (`MinorChance` 3% + 1%/day, plateau 12%), half of them
  on a BORROWED card whose tell is the photo; papers are per PERSON, rolled on a new `"papers"`
  stream, hidden behind the card exactly like the order; `Kick(visit)` needs the card read; a
  served minor pays and tips and then, as they get up, the bar pays `$20 + $20/whole star` (may go
  below zero, like rent); a right kick earns the state's `$5` thanks (a well drink; the author's ten was an
  example and priced above what a served customer nets) paid with the rent so the slip adds up,
  and the bounced never return; a wrong kick is a walk-out. Reviewed the same night by five
  adversarial passes; the reversed calls are listed in each module's decisions. **Shipped with the design:** the pure Core halves as new files
  (`VenueComfort`, `Housekeeping`/`CounterMess`, `IdPapers`, `RegularState.Papers`) with
  `HouseTests` and `DoorTests`; the wiring (H1b/H2b) and every screen (H3–H5) wait on the
  shared tree. Conflict ledger and decisions in the PLAN.

- `GDD_MEVCUT §9.9 / 24 §2-3 / 14 §3` **the bench becomes a composition, and the drink
  becomes a thing (2026-08-26, thirteenth round):**
  **One rule untied the layout:** props are DIEGETIC, chrome is not. The tin, bottle, spoon
  and glass stand ON the counter and may rise past the rail into the room — that is
  perspective; everything the player READS stays in the band. Three columns (instruments
  left, work centre, measures right), every prop's foot on one line (BenchFootY), both step
  cards' tops on one line (CardSeat). The cap rests on the counter instead of hiding behind
  the card.
  **The glass arrives by itself.** The TO THE GLASS key retired: capping the tin is the
  player saying the build is done, so once the tin is closed AND pourable (CanPourOut — an
  unmixed two-spirit tin still waits at the door, saying so) the glass slides in after one
  0.45s beat. Never out from under a working hand.
  **The instrument plate is drawn** (ChromeArt.Instrument): the generated board_plate's
  frame was three different rails on one rectangle and nine-slicing stretched noise. Chrome
  is procedural — the liked LOOK (navy face, teal cap, brass hairline, four rivets) redrawn
  on a 48×48 grid; the boards and the step card wear it; the PNG is deleted.
  **The drink became a thing:** ice FLOATS (per-cube bob and roll, phase from the cube's
  index — no dice — settling as the glass empties; mint and olive nod at half strength);
  the lemon is a rim-straddling wedge (glass_lemon_rim, slit over the edge, half inside,
  and it moves with the glass because the decor is the glass's child); the crust sits ON
  the mouth at the mouth's width, twice as deep, three pieces (dark seat, speckle, lit
  lip); the rims carry a PINCH (carry_salt/sugar) that sheds grains by distance travelled
  as it is dragged; the lap's ring became an instrument (dim seat ring, crust marks that
  GROW as they take, a burning head under the cursor, the percentage in the mouth); and
  the coaster is a drawn ellipse (BackBarArt.Coaster) after two generated takes failed on
  proportion.

- `GDD_MEVCUT §9.8 / 22 §5` **the rail shows what the bar owns, and the drink gets a
  place (2026-08-26, twelfth round):**
  **The gate was already in the economy and the rail was not reading it.** Ice, the twist
  and the two rims are house basics and always out; the olive and the mint are STOCK, and
  base_bar.json has priced them behind 3.0 and 4.0 stars since it was written. A jar the
  bar has not bought — or has emptied tonight — is off the counter, and buying one puts it
  there. The row lays out by VISIBLE index, so an unbought garnish leaves no hole and the
  rail still ends where the coaster begins.
  **The hand carries what comes OUT of the dish**, not the dish: a cube off the bucket, a
  wedge off the bowl, a spear off the jar — and it is the same sprite that ends up floating
  in the drink, so the pick, the carry and the float are one object. The two rims are the
  written exception: the verb is turning the glass IN the salt, so the dish is what the
  hand holds.
  **The rail stops vanishing when the cellar opens.** It was switched off with the drawer;
  the dishes stand on the bar, the bar rises with the room, and a tray that disappears the
  moment you reach behind it reads as a bug. It rides CounterLift up instead and only stops
  ANSWERING the pointer, because the cellar's own doors are under it and a click meant for
  a bottle must reach the bottle.
  **The drink has a place.** It stands between the last garnish (stage 380) and the drip mat
  (480), at 430, and its height went 116 → 92 — at 116 it was the tallest thing on the bar.
  A coaster is drawn under it ALWAYS, drink or no drink: an empty coaster is what says where
  the next one lands. Both are placed from one constant.

- `GDD_MEVCUT §9.5 / 24 / 16` **the bench opens onto the room, and the counter band owns
  every control (2026-08-26, eleventh round):** the author's redesign brief, with their own
  1149×426 working area.
  **The wall lasted one build.** The generated backdrop answered "the bench looks empty" by
  boarding the bar up: the room — and the DRINKERS the night is about — sat fully drawn
  behind a painting of panelling. It is deleted (code and asset); above the counter line the
  live room shows through on every bench, and only the props slide between stages.
  **The band owns the UI.** Everything a bench draws now lives in the author's area — from
  the counter rail down, inside the 1149 margins: the step card stands ON the bar
  (bottom-anchored, left column), the vertical mix gauges dropped fully below the rail and
  inside the right margin, the aim/hint/readout lines moved from under the fascia to a
  measured bottom stack (keys 26..72, readout 84..110, hint 114..128, work meter 134..156),
  and the BACK key moved right of the bar spoon's hanging slot.
  **The invoice boards were fixed, then hired.** board_plate is 9-sliced now
  (ItemArt.BoardPlate — borders measured off the drawing: cap 30, sides 12, foot 14 rows)
  and drawn with pixelsPerUnitMultiplier 0.5, so the frame keeps its 2× grain at any
  height; BoardH went back up to 420 and the content that was falling off the bottom fits;
  the teal caps carry night ink (cyan-on-cyan was the other unreadable); the lit MON row
  came inside the rails. The same plate is the bench step card's now — the style the author
  liked, on the screen they asked it onto. The tin bench's card cap carries the BOTTLE'S
  NAME (RefreshShaker writes it), and the glass bench's card dropped to two honest steps —
  TIP THE TIN, SERVE IT — because the dressing left that bench for the room's rail.
  **One tin on both benches.** The glass bench drew ItemArt.Shaker — a different vessel at
  two-thirds the size — so the object you had just capped came through the slide as
  somebody else's shaker. It is the tin bench's own body with the cap SEATED, at the same
  200×358; the pour maths rode along because the mouth derives from the height.
  **SERVE IT ▶** — one loud display-16 line on a full-height key; what to do next is said
  by the room over the standing drink, not whispered on the key. And SERVE IT closes the
  cellar on the way out (the author: "kapak kapalı bir şekilde oyuna dönmeli") — only that
  door; BACK TO THE BAR keeps it open, because the way back is for another bottle.
  **The roller says nothing and points.** STOCK lasted one build; the chevron alone, at 3×,
  hung where the word sat. The suites aim at the arrow now.
  **The rail slid left** (sink 181 … dishes 195..380 … glass 405 … mat 480), so the counter
  reads the way the night runs: sink, the makings, the drink, the tap.

- `GDD_MEVCUT §9.5-9.6 / 21 §14 / 24` **the bench becomes a room, and the counter grows a
  garnish rail (2026-08-26, ninth and tenth rounds):**
  **The slip was restored.** "Gün sonu fatura ekranı karmaşık ve çok yazılı" was read as a
  note about the receipt; the sentence after it named the two BOARDS beside the receipt.
  TOOK IN / PAID OUT, both subtotals, TIPS and TONIGHT's reading are back exactly as they
  were, and the boards got what was actually asked for — a drawn navy plate with a teal
  capped head, one drawing stood twice, BoardH 384 → 350 so it lands at a whole 2×.
  **One room, not three panels.** The benches had no background: each panel drew a band of
  counter across its own bottom third and left the rest transparent, so the ROOM showed
  through — which is why the screen read empty and why a stage change slid the world. The
  room belongs to the FIELD now (generated wall hung FROM the counter line at a whole 2×,
  the bar top built once instead of three times, a plate behind both); the panels keep only
  what a bench differs by, and those are the only things that slide.
  **The checklist, the name and the keys.** The card came 74 units off the fascia it was
  pinned across and its four 16px marks became STEP NUMBERS; the bottle's name moved from
  the top of the field to a plate cut into the counter's right-hand end; BACK and TO THE
  GLASS came off the wall onto a strip along the bar's front edge, one line each.
  **The work meter is the house's own gauge.** It was a flat 220×14 rectangle with a second
  flat rectangle growing inside it — and on a bench where nothing is being shaken, an empty
  black bar. The day-end standing track's vocabulary (GaugeTube + Solid-sprited Filled +
  GaugeGlass) was finished and unused by every bench; the meter is that, plus three things a
  WORK bar wants: it is not there at rest, its caption is inside the tube, and it carries a
  mark at the point where the work is enough.
  **The garnish rail.** Four stations that appeared beside a finished drink became six props
  that stand on the bar all night and are DRAGGED — the same verb and weight as carrying the
  drink to a stool. Two kinds of prop, each through its own Core verb:
  AddPreparationAtGlass for the volumeless marks (ice, twist, both rims), PourAtGlass for
  the two that are STOCK (olive, mint) — they come off the shelf, they run out, and
  recipes.json grades its "olive" and "mint" bands against them.
  **The rim lap moved with the dishes rather than dying with them.** Taking them off the
  glass bench and applying salt on a single drop would have deleted a skill asked for eight
  days earlier, unasked. The arithmetic came whole: hold the dish over the drink and run a
  full circle round its MOUTH. Same band, same one-third-of-a-lap jump rejection, same
  part-run kept against its dish. A drop never applies a rim.
  **Hover captions, and the sink is dragged to.** One plate told which rect to stand over
  and converting through the screen, so a prop on the stage's canvas works like one on the
  HUD's; the dirty glass, the snack bowls, the sink, the font and all six rail props say
  what they do. Discarding is a release of the carried glass over the basin — the drain
  takes no click at all, because a click made throwing a drink away cheaper than serving it.
  **The room's front has light** (ambient 0.24 → 0.40: everything lighting this room aims at
  its back, and the tables and drinkers stand in front of all of it), **the menu drops half
  its own height**, and **the shutter says STOCK** — OPEN BAR was a sign for the people on
  the other side of that counter.
  **Debt:** the glass bench's old finishing table left `AddGarnishChip`, `AddFinishTub`,
  `TableStand` and its own rim machinery with no callers. They do not crash; they are dead,
  and the next sweep should take them.

- `GDD_MEVCUT §9.3-9.4 / 24 / 16` **the room clears its counter (2026-08-26, eighth
  round):** nine notes from the author, shipped together.
  **(1) The till and the money leave the playing screen.** The register, the gold balance
  over it, the change that floated off its drawer and the ledger it opened all went at
  once. Nothing counts money at you while you serve: the night is read on the slip, and
  the running balance on the market tablet while you spend it. Debt kept its voice — the
  fascia's neon now says three states (shift amber, last call magenta, under water vice
  red) off one cached writer. The ledger's door moved behind the cog.
  **(2) The bin goes; the SINK is the drain.** A stainless well stood half out of frame
  doing a job the room already had a fixture for. The verb is unchanged — a built drink
  is clicked away — only the target: whatever carries `drain: true` in fixtures.json,
  answered by the same hit-plate rule the beer font uses (`BuildPropDoor`).
  **(3) The brass basin waives the write-off.** `drainsFree: true` on the upgraded sink;
  `TycoonRun.WasteIsFree` asks the OWNED catalogue and `WriteOffVessels` skips the fee.
  Rung numbers are not in Core — a third basin is content. The first piece of dressing
  that changes what the bar can afford to do. `DrainTests` pins the boundary, including
  that a fixture-less run still pays.
  **(4) A ladder shows one rung ahead.** The market's DRESSING aisle stood every rung of
  every ladder at once, sealed with LOWER MARK FIRST. It shows what is owned and the one
  rung that may be fitted next; the rest arrives as the ladder is climbed.
  **(5) The menu moves to the sink's left shoulder.** It was standing INSIDE the basin's
  footprint (stage 152 against a sink running 99–181); `BookPropX` −336 → −482.
  **(6) The arrival ease is readable.** The cycle already rode the pace; 0.45 over 260
  units was a slow-down you could measure and not see. 0.30 over 300 lands the last steps
  at 3.5 frames a second. Measured before it was picked: the approach costs 0.45s more,
  and a curved ease was refused at 1.7s — a customer's worth of a 95-second shift.
  **(7) The slip loses half its ink.** Thirteen printed rows became six or seven: the two
  block captions and both subtotals gone, takings one figure, and a bill that was not
  paid is not printed. The stars were already the score, so "TONIGHT 3.5" under them went
  with it.
  **(8) The afro gets its crown back.** Every frame of `afrowoman` was drawn with her
  hair flush against row 0 of the rig canvas and clipped flat. `Tools/afro_crown_fix.py`
  slides all 95 frames down 7 rows and rebuilds the dome on the curve her own hair was
  already drawing (25 px wide at the cut, 36 nine rows down → R ≈ 14.7 and a crown exactly
  7 rows up), sampling the hair for its speckle. Stamped in the PNG so it cannot run
  twice. Her head row and her licence photo were re-measured off the repaired art.
  **(9) Furniture, one take each** (`Tools/room_furniture_gen.py`): three table sets at
  one eye line — two of the three tables had no top at all, only a wire frame — and the
  three draught fonts grown for the bench. `bench_tap_big` matched none of the towers the
  market sells (two faucets facing opposite ways, a red smear where its baked handle was
  rubbed out); the bench stands the tower the bar OWNS now, and every number that hangs
  off a faucet — its drawn size, its lip, its lever's seat and size — hangs off a measured
  per-rung rig instead of a constant.

- `21 §5 / 24 §2-3 / GDD_MEVCUT` **the bench rework (2026-08-25, seventh round):** the
  author's brief in five parts, all shipped together.
  **(1) The rims are a skill.** Salt and sugar stopped being a piece you drop in: press
  the dish and run a full lap round the glass's MOUTH with the cursor — the bar spoon's
  signed-sweep arithmetic turned ninety degrees. A 14-segment ring round the mouth shows
  the lap; leaving the 34–190 band pauses rather than spills; a half-run rim waits on the
  shelf as "SALT 60%". The finished lap applies through the same Core verb the drop used.
  **(2) The ice is counted.** The bucket never turns into a tick: every drag-and-drop
  adds a cube. `GlassContents.IceCubes` counts them (the STEP list stays deduplicated —
  the judge still asks "is there ice", never "how much"), `TransferInto` carries them
  with the drink, and GlassDecor stacks the pile at the liquid line from a fixed lay
  table (7 drawn, so the pile cannot boil between refreshes). `IceCountTests` pins the
  boundary.
  **(3) The bench resets on serve.** SERVE IT — and any exit from the glass bench —
  empties the hand: dish mid-lap, piece mid-drag, ring, part-run rims, all cleared
  (`ResetServeHand`, called from the key and from `GoTo`).
  **(4) The counter is the room's counter, zoomed** ("ekran çok boş görünüyor"): the
  bench band is drawn from colours SAMPLED off `counter.png` — slab, ridge, seam and the
  magenta neon rail at the far edge, six sampled rows at 5 units each — so the bench is
  the same object the room draws, four times closer.
  **(5) The counter's own stations** ("servis et dedikten sonra unutursa diye"): while a
  made drink stands on the room's counter, four counter-scaled stations stand beside it —
  a plain press applies the prep, no lap and no aim; the skill lives on the bench, the
  counter is the safety net. They ride `CounterLift` and hide while the flow is open.
  **Art, one take each** (`Tools/bench_props_gen.py`, quantized): the twisted-stem bar
  spoon (shipped flipped, bowl down), the room's brass single-font grown to 240×480 for
  the tap bench (its baked handle erased at ship — one rig, one handle — the animated
  handle re-mounted at the measured valve, the spout re-measured at (−79,+66)), the two
  rimming dishes, the open ice bucket, the lemon bowl, and the four counter minis.

- `GDD_MEVCUT §6.2` **the room gets four pieces of the author's own art (2026-08-25):**
  the wall lamps are replaced drawing for drawing (mark 1 is a glass tube, cyan cap into a
  pink body in a steel bracket; mark 2 is a cream panel in a coral frame, which is a wash
  where the old cylinder was a dot; mark 3's palm is unchanged), their light colours are
  re-measured off the new art rather than kept from the old, and the sink becomes the
  SECOND ladder that is not a draught tower — steel at rung 1, brass at rung 2, sharing a
  silhouette to the pixel so the upgrade cannot slide in its hole. The market's rung line
  was hardcoded for lamps ("the back wall · both lamps, one fitting") and the brass sink
  inherited it, telling the player about a wall; `RungPlace` reads the slot now.
  **And a mat is not a prop:** the rug on the boards and the drip mat on the bar lie FLAT
  on surfaces that already carry dressing, so the slot carries a `flat` flag that drops the
  piece one sorting band below its surface's props (34 under the bar's 35, 16 under the
  floor's 20) and takes its contact shadow away — a blob under something touching the floor
  along its whole face is a stain, not a contact. Both come with the room.

- `23 §6 / GDD_MEVCUT` **the day goes past (2026-08-25, sixth round):** the black between
  two nights was six seconds carrying a week and two day-names — a caption for a
  transition. The bar shuts at two in the morning and opens at six in the evening, and
  those sixteen hours are the scene now: the moon finishes its fall into the west, the
  stars go out at first light, the sky walks a seven-key ramp from deep night through
  dawn, morning, noon and afternoon into the room's own golden hour, the sun climbs out of
  the east and comes back down, the city's windows sleep at dawn and wake for the shift,
  and the game's own `SegmentClock` winds from 02:00 round to exactly 18:00. All of it is
  DRAWN in palette tokens (chrome is procedural, 14 §3): the sky is twenty flat bands and
  not a ramp, the sun and moon are the marquee's own bulb and glow at whole multiples, and
  the moon's crescent is bitten out by a second disc wearing the sky's colour at the moon's
  own height. One hour drives every part of it — a sun on its own timer and a clock on
  another would be two animations playing at once. 7.0s: 0.45 in, 3.60 of day, 1.25 held
  on the hour, 1.70 out.
  **Second pass, same day** (the author: the visuals did not look professional —
  generate them if need be): the procedural box-towers, the lamp-disc sun and the
  two-disc moon were replaced by generated art through the standard quantize chain
  (`Tools/day_sky_gen.py` → `Scene/curtain_city.png` 320×96, `curtain_sun.png` 32,
  `curtain_moon.png` 24; six takes judged on a contact sheet). The skyline stands at
  a whole 2× IN FRONT of both bodies, so the sun rises from behind the towers and
  sets back behind them; the silhouette is tinted past white toward the horizon's
  colour at the bright hours, because an Image tint can only multiply and a pitch
  city under a noon sky reads as a hole cut in the picture. **And the room comes back BEFORE the books:** `CloseEverySheet`
  now fires on the phase flip rather than at `ShowDayEnd`, so the last customer's walk to
  the door plays on a bare stage instead of behind whatever was left open.
- `23 §6 / GDD_MEVCUT` **the night's end becomes a report (2026-08-25, fifth round):**
  three things the author asked for in one screen.
  **(1) The day waits for the room.** Core already refused to close while anybody was on
  a stool, but the walk to the door is the HUD's — so the scrim used to come down over the
  last customer's cheer and their exit. The phase flip now only ARMS the books; they open
  when the floor is visually clear (nobody drawn, no tab still counting) or after a 9s
  backstop.
  **(2) The tab is worth watching.** What a customer paid and the stars they leave now
  lift off the stool at display-24 with the till's own two rings, for 3.2s instead of 1.6,
  drifting on a slow sine and leaning into the drift rather than rising in a straight
  line — the phase comes from the stool's index, never from a roll. The stars are the
  `StarRow` ruler, so a 3.5 is three and a half here as everywhere else.
  **(3) The books are a report, not a receipt.** The slip keeps the money and is flanked
  by two instruments in the room's own chrome: **THE WEEK** (six nights and Sunday; a
  played night shows the stars it filed and its net, tonight is lit, the nights ahead are
  five empty sockets, Saturday keeps its VIP star, Sunday its shutter, and the week's own
  subtotal sits at the foot) and **AFTER TONIGHT** (five 40px stars and the figure over a
  0–5 gauge: amber to where the bar stands, a dim band across the slice tonight won or
  lost, a white tick where it stood at opening and a cyan notch at the next rung; beside
  it WAS x.xx and an arrowed step chip; under it TONIGHT, CEILING and TOMORROW). A fourth
  beat was added after the star drop — the standing CLIMBS, and only then does the way out
  appear. Every number is asked of the rules before the books close: `BarRating.Nights` /
  `StandingAfter` (which `CloseNight` now calls, so there is one climb, not two) and
  `TycoonRun.TonightStars` / `StarCeiling` / `StandingAfterTonight` / `CrowdTomorrow`;
  `NightReportTests` pins each by asking, closing, and comparing. The slip dropped its
  "BAR x.x" (the right-hand instrument's whole subject) and its print starts 8 units
  lower, which stops the DISGRACE stamp clipping the date line.
  **Also:** the order bubble's white is 86% opaque (`ChromeArt.BubbleFill`) — only the
  fill; the spout's skirt stays solid, because those three rows exist to erase the plate's
  bottom band and a see-through eraser erases nothing. `ChromeArt.Solid()` was added after
  the gauge came back full at nought: an `Image` with no sprite ignores `Type.Filled`.

- `23 §3 / 16 / GDD_MEVCUT` **a star gate is drawn, and a perfect pour announces itself
  (2026-08-25, fourth round):** every star requirement now draws its stars beside its
  number — the shop's sealed tag (which is the one renderer behind recipe crates, bottle
  crates, fixtures and aisle gates), the cookbook's gate plate (two rows on one ruler:
  what the page wants, where the bar stands) and the index's locked lines. One helper,
  `StarRow`, per-star `Image.Type.Filled`, so a half rung would read as a half star.
  Locks that are not about stars (a tower's rung, a person's beat) keep their sentence.
  **The perfect pour:** `ServeSeat` asks Core whether the ordered recipe was already
  perfected, serves, and asks again — the first time, a platinum notice names the drink
  and points at the book, the BOOK key takes a counted badge, and the book's title page
  carries a pressable line that opens straight at that page and marks it read. The notice
  channel learned a colour and a length (default still the refusal red) and grew to 620
  units, which un-wrapped the long refusals too.
- `23 §3 / GDD_MEVCUT` **the contents becomes a browser (2026-08-25, third round):** a
  search line over the contents (name substring, 15 hits), chapter rows that OPEN into
  their own recipe lists in place — every drink with its folio, each line a jump, LOCKED
  spelled out beside it — and hover glow on every clickable line. A `<<` home key joins
  the page arrows at the foot. The title plate takes its own drink: `menu_cover_drink.png`,
  generated at 64 art px by `Tools/menu_cover_drink_gen.py` (three seeds judged, seed83
  shipped) and quantized onto the 40-colour palette. Index lines print AND for `&` — the
  body face's ampersand at 16 reads as `$`.
- `23 §3 / GDD_MEVCUT` **the cookbook respec (2026-08-24, the author's second round):**
  the booklet's interior is one recipe per page behind a title-and-contents spread
  (chapter lines click straight to their page). Each page: prep and glass, a gauge
  LEGEND (what the bar measures, which colour owns which fifth), full-width pours,
  and provenance at the foot from `Resources/Data/recipes_lore.json` (`RecipeLore`,
  catalogue-pinned by test — 54 entries). Locked recipes stand IN their tier (STILL IN
  THE BOOK retired) behind an OPENS gate plate; an unpourable bottle says LOCKED under
  its name (the overlap bug's grave). A perfected page prints exact shares in place of
  its gauges, binds in platinum and wears the angled PERFECT RECIPE ribbon. Drawn
  `<` `>` paper keys join the corner hotspots; the open book slows the floor to
  `BookTimeScale` 0.05. The card grid, its masonry and `menu_board`-era metrics left
  `TycoonHud` whole.
- `23 §3 / GDD_MEVCUT` **the menu booklet (2026-08-24):** the recipe book's clipboard
  board (`menu_board`, a 2.899× fractional upscale) is replaced by the open booklet
  drawn at exactly 2× (`menu_booklet.png` + `menu_page_frame` + sixteen peel frames,
  all out of `Tools/menu_booklet.py`). Pages are chapters — the tiers, then STILL IN
  THE BOOK — and the page TURN is the drawn peel: front print clipped at the fold,
  back face shifted by integer art pixels, three RectMask2D windows aimed by the
  generator's own fold numbers. The board's search and TIER/PREP/BOTTLE chips retired
  with the board; corners or arrow keys turn, the ribbon bookmarks the spread, and
  `menu_board.png` left the project.

- `14 §5 / 16` **the dead-weight sweep (2026-08-20):** the project shed what it had
  stopped using. **The old rig left the build.** `Resources/Patron` carried 43 character
  folders while `PatronCast` seats 10; Unity packs and indexes *everything* under
  `Resources/`, so 33 old-rig faces and the original slug-less patron — 3,188 files,
  18 MB — were riding into every build to be drawn by nobody. They are deleted (git
  history keeps them; each comes back by being REDRAWN to the 2026-08-19 rig, which was
  always the plan), and `LoadPatronClip`'s empty-slug branch went with them. NOTE: the
  story still NAMES four of those faces — `story.json` gives its guest `ece` the
  placeholder `glam`, and `execman`/`profess`/`teal` are written as looks — none of which
  resolved to art before this sweep either, because `LookNamed` only ever searched the
  cast. **Dead builders.** `NeonBlink` (the last survivor of the demolished animated
  backdrop) and `StageArtFit` (superseded by `DiegeticStage`'s own square-pixel scaling)
  had no caller and no scene reference; `BackBarArt.ShelfFloor/NicheTop/ShelfFace` went
  with the back-bar rebuild, and eight colour constants went with them. `TycoonHud.Case`
  is gone because `ChromeArt.Well` replaced it the same day. `BackBarArt.KegCrown` STAYS
  under its own written ruling (hand-drawn art, not logic) — the rule this sweep followed
  is *remove what has already been replaced; leave what the author is still building*, so
  `TycoonRun.CanUnlock` and `StoryArc.BeatNamed` also stay. **Records, not rules.**
  `PLAN_emotion_pivot.md` (its own GDD 19 was deleted in the 2026-08-07 sweep) and
  `PLAN_pour_pivot.md` (GDD 21 is the spec now) are retired; `PLAN_tycoon_pivot.md` and
  `tycoon_speed_response.md` were NOT, because `PLAN_service_depth` and the simulator
  still reference them. `Docs/previews/` (27 orphan captures) is gone — `Docs/readme/` is
  the set README.md actually uses. 275 raw PixelLab generations under `Tools/*_raw/` left
  the repo, which `.gitignore` had already asked for on 2026-08-11.

- `21 §9a / 23 §3–§5 / GDD_MEVCUT §5–§6` **the perfect pour (2026-08-20, the author's
  respec):** every recipe carries one hidden perfect ratio; the menu lights only the
  20-point box each ingredient's perfect sits in (five discrete boxes, red→dark green)
  until the drink is made PERFECTLY once, after which the exact numbers appear on that
  page for the rest of the run. The box replaced the authored min/max band as the
  matcher's acceptance ("tamamen yanlış" outside it, the ≥5% dash floor kept), Exact base
  pay scales with closeness to the perfect (`× (0.10 + 0.90 × accuracy)`, floored — the
  author: right box always earns something), Close pays nothing and keeps 0.30
  satisfaction, and the tip re-seated to `0.35 speed + 0.25 craft + 0.20 accuracy +
  0.20 fill`. The perfect is DERIVED (settled IdealPour + id-hashed edge-guard nudge —
  eleven ideals sat exactly on grid lines and a 40/60 pair is an unpourable order), the
  reveal gate is Core's `ExactPourFor` (throws until perfected, the InspectId pattern),
  and the sim bot plays the discovery loop. Overturned by name: 2026-08-02's "show the
  player the perfect number", 23 §5's "not pixel-perfect ratios", the generosity-pass
  band rationale, and Close's 2026-08-14 half-tip cushion (the cliff is visible now, so
  it is a target). Conflict rows C11–C14; sim: median 30-day till $351 → $194.
- `21 §10.1 / 24` **beer left the wall, and the tap on the counter is its own door
  (2026-08-15, the author: "backbardan biraları kaldıralım ... bira musluğuna tıklanması
  gereksin, musluğa tıklanınca direkt bira koyma sahnesi gelecek"):** the back bar drew a row
  of keg crowns on its floor whose whole job was to open the draught station, and the room now
  has the real thing — the beer fonts that arrived with the fixture set. Clicking one goes
  straight to the tap. The wall keeps garnish and beer off it now, which leaves it with a
  single answer for everything on it: it all goes in the tin.
  The bar **owns the first font** from its opening night (`taps_one`, `startsInTheRoom` in
  `fixtures.json`) — draught is a rank-1 page every bar starts with, so a bar that had to buy
  its own tap could not pour a pint on night one. Owned rather than free: the market prints
  OURS, `BuyFixture` already refuses a piece the bar has, and a refund only walks back
  tonight's purchases, so it cannot be sold back for money nobody paid. That makes one fixture
  load-bearing where the class is documented as cosmetic, and `FixtureDefinition` says so.
  Nobody names a beer on the way in any more, so the **cellar couples itself** — the first
  stocked keg in shelf order, re-chosen if the remembered one has run dry. Without that the
  station opened uncoupled: title DRAUGHT, blank keg label, `CanPull` never asked, and a
  handle that did nothing. The door is a hit plate the STAGE builds over the font (it owns
  where the font stands) with the HUD owning what the click means — the same split the till
  has. Canvas, not a physics ray, so an open panel blocks it for free. 315 EditMode green;
  the back-bar and market baselines were re-blessed after looking at both.

- `14 §7b` **no light is painted in, and the loud colours are atmosphere (2026-08-15, the
  author: "üretilen görsellerde yansıma ve ışıklandırma olmamalı ... çok fazla cırtlak renk
  var, bu renkler sadece hava katmalı ana renkler olmamalı"):** two rules, one idea — the
  plate carries material and the engine carries light. `scene_v3_gen.py`'s shared style string
  was ASKING for "glass reflections, soft dithered light gradients", which is the opposite of
  a scene lit by URP's shift light and a `Light2D` per fixture; it now asks for flat matte
  local colour, form from the ramp's own steps, and says no specular/reflection/cast-shadow/
  rim/glow/bloom out loud. Measured on the shipped room the same day: **55% of the frame above
  S 0.40, and Magenta[2] alone 21.4% of it** — a sign colour used as the wall. The cause was
  mechanical: the plate had been snapped to a 40-colour palette missing **Graphite, Brick and
  Lime**, so nothing dark and neutral was available to build a room out of. The bible now
  carries the ceiling (≤20% above S 0.40, ≤10% per hue) and says which ramps own architecture.

- `14 v3 §5–6, §11` **the room went empty and the furniture became props (2026-08-17, same
  day, second sitting):** the author's second Nano Banana batch drifted off the material map
  — teal wall, honey floor, beige fieldstone, steel frame, graphite platform, and on the
  counter beige marble over oak cabinets —
  and the author felt it before naming it ("renkler hiç uyumlu gelmedi"). The fix is
  structural, not another mood: §5a is now a PER-OBJECT colour law (every shell surface
  names its ramp steps), and the venue is layered on the author's own call — the room master
  is EMPTY, every piece of furniture is its own keyed sprite on a stand line, because
  furniture is the game's upgrade surface. The counter follows all the way: three strips
  over one shared graphite base (oak → white marble & steel → navy marble & brass), the
  surface line at the same y in all three, matching the Bar Top tiers `TycoonRun` already
  sells. Two DON'Ts landed (no prop bakes its own wall; no off-palette hues on the shell),
  two exceptions were written down (the fridge's `Cyan[0]` interior at day — a fridge is a
  lamp, not architecture — and the neon sign's dead `Magenta[1]` tube, unlit glass being a
  material), and §11 grew the full asset list with per-file prompt hexes and a ban on
  painted checkerboard "transparency" (flat `#00FF00` or true alpha, nothing else).
  A three-agent adversarial pass then swept the amendment against the siblings and the
  code and forced five corrections worth recording: bracket indices are now UITheme's
  0-based array truth everywhere (`[0]` darkest — the doc had silently mixed 0- and
  1-based and cited a `Cream[5]` that exists in neither); 15 got its v3 patch (55 colours,
  rim per 14 §8, the size-REJECT scoped to sprites); §11.D split into shell/counter
  relight prompts so the empty room is never asked to relight a counter it doesn't
  contain; §6 now tells the truth that the surface line is the hand-measured
  `CounterRestY = 128` constant, not runtime art-reading; and `BackBarArt` still drawing
  v2's teal-navy off-palette hexes is named as OPEN WORK rather than claimed migrated.

- `14 v3` **the art bible turned toward the room the author was already building
  (2026-08-17):** two approved mockups — the cream-and-brick room and the navy-marble
  counter — did not match v2's "Vice Pixel" Miami club, and the author settled the four open
  questions in one sitting: the light changes with the SHIFT (neutral day at open, dim amber
  night at service, darkest at last call — the two identities become one bar at two hours);
  the vice sunset survives in the WINDOW plates, as the view instead of the room; the palette
  grows by two ramps rather than bending — `Graphite` for the cabinet metal and `Brick` for
  the masonry, both material-only by law (never a signal, never a key face), 55 colours
  total, in `UITheme` beside the doc; and the stage stays 640×360. The neon rim stopped
  being a law ("daha iyi sonuçlar alınırsa değiştirilebilir, şart değil") — night-preferred,
  judged per asset. Backgrounds will be generated by the author with Nano Banana against 14
  v3 §11's spec: day master first, night as a relight EDIT of that master so geometry cannot
  drift, window panes flat `#00FF00` keyed in post, everything quantized to the 55. 18's
  club-crowd layer content is superseded; its choreography rules stand.

- `23 §4` **the middle grade did not exist, and the tier ladder was measured by a bot that
  ignored it (2026-08-14):** `OrderMatch.Close` was documented, coloured amber in the HUD,
  counted in the simulator — and unreachable. `Compare` asked for the ordered recipe's
  dominant *type* band, and since the style era every one of the 52 banded recipes in
  `recipes.json` is style-banded (a recipe may not mix the two kinds; `RecipeDefinition`
  refuses it), so the answer was null every single time. A pour that drifted out of its bands
  therefore matched no recipe and fell straight to **Wrong**, which pays the menu price of
  whatever the glass happens to be against the bar's UNLOCKED menu — usually nothing. A cliff
  at the edge of a band the player cannot see, with no step in between.
  Close is now **the drink they ordered, poured out of tolerance**: every band the recipe
  names is in the glass, strays inside the matcher's own allowance, and the shares missed. It
  pays the menu price at half tip and 0.5 satisfaction instead of 0.75 — money now, standing
  later, which is the currency the star track counts. The tier is forgiven with the shares (a
  Vesper on the well gin lands here); a *different* drink of the same family stays Wrong.
  Two dead helpers went with the old rule, one of them a determinism hole: `DominantGlassType`
  broke volume ties by walking a `Dictionary`, so a seed could have graded two ways.
  **And the measurement caught something bigger than the rewrite.** The first re-run came back
  92.7% Exact with perfectly steady hands, while the day-end counter insisted the shelf could
  answer every tier its menu demanded (0 of 15,916). Both were true: the simulator's
  `PickByStyle` reached for the FULLEST bottle of a style and never read the band's `MinTier`,
  so a bar that had bought the reserve gin poured its well gin into the recipe asking for it —
  the instrument, not the bar, for the third time in that file. With the bottle chosen the way
  a bartender would choose it the floor is 100% Exact again, its standing goes 2.60 → 2.85,
  and the thirty-night star table goes from **none of 200** runs reaching three stars to
  **102 of 200**. Every star-track number taken between the ladder landing and this fix was of
  a bar that owned good bottles and poured cheap ones. 310 EditMode green.

- `26 §12.3` **the reward IS the unlock, so StoryReward is gone (2026-08-14, the author:
  "Reward olayı aslında kilitli olan geliştirmelerin veya tariflerin kilitlerinin kalkması
  olacak"):** the beat carried money, stars, a recipe id and a bottle id, and nothing read any
  of them. That turned out not to be an unfinished payout but the WRONG MODEL — and the right
  one was already standing beside it. A beat pays nothing; the things it earns NAME IT with
  `Kept(beatId)`, so keeping the night takes their lock off. The push became a pull, and three
  things fall out: one night can open any number of things without listing them; each locked
  thing says its own sentence, which the shop already prints; and there is no invisible flag,
  which is this arc's standing rule 4.
  **Bottles got the lock too.** `IngredientInfo` carries the same `Unlock`/`UnlockBeatId` pair,
  and `Market.OffersFor`/`GatedFor` ask it instead of comparing the standing themselves. A
  named lock with nobody to ask stays CLOSED: a caller that cannot evaluate a condition has not
  proved it open, and a bottle leaking out of its lock because the caller was old would be
  silent and in the player's favour — the kind nobody reports. A bottle waiting on a person
  reports NaN rather than a star, so it cannot drag an aisle's "next at" hint onto a rung that
  opens nothing. 304 EditMode green, 8 PlayMode green.

- `26 §12.2` **a lock can come from data, and the shop prints it (2026-08-14):** the condition
  type existed but nothing could express one — `RecipeDefinition` had no field, `recipes.json`
  no key, `RecipeUnlock` was hardcoded to the rank table, and the shop re-derived the star gate
  in three places and wrote its own sentences. All four closed. A recipe carries `unlockBeat` +
  `unlockStars`; the loader composes them with `All(...)`, which collapses when neither is
  given, so **all 49 locked pages behave byte-identically**. The sealed crate and the book row
  now print the lock's own `Sentence`, so a page earned from a person stops drawing as though
  it were waiting for stars. `ParseStory` refuses a recipe naming a beat the arc does not have
  — recipes load first, so that is the first and cheapest moment both halves are in one room,
  and the failure it prevents is the worst this system has: silent, permanent, and
  indistinguishable from content nobody wrote. 304 EditMode green, 8 PlayMode green.

- `26 §2b` **Saturday is the rule, not a star on the wall (2026-08-14, the author: "her
  cumartesi bir hikaye müşterisi gelecek"):** the marquee had been drawing a star on Saturday
  while the rule still allowed Friday-or-Saturday — a wall advertising a night the arc need
  not use. `StoryBeat` refuses any guest night but `BarCalendar.VipNight`; the house keeps its
  quiet nights, and Friday keeps its place in `IsWeekend`, which is the ECONOMY's busy weekend
  and a different question sharing a word. The two parked guests moved off Friday. The suite
  caught what a rename would not: the "one weekend, both its nights" fixture no longer
  describes anything, so it became two consecutive Saturdays and the arc it builds is two
  weeks long — while the two-beats-on-one-night fixture had to keep both on ONE Saturday or
  stop testing what it is named after.

- `23 §6` **the rent stops climbing where the bar's ceiling is (2026-08-14, the author:
  "kasıtlı değil fiyatları ekonomik dengeyi buna göre ayarla"):** `Rent` keeps its measured
  ramp and then **plateaus at day 21** ($103). The quadratic was aimed at a bar that never
  grows and hit one that *cannot* — six stools is the cap, the menu finishes, and a better
  bottle was a purchase the margin never reached — so the two curves crossed on night 31 and
  every long run died on a date rather than on a decision.
  **Measured, 60 runs × 120 nights, before → after:** bankruptcies **60/60 → 0/60**; nights
  survived **36 → 120**; average standing **2.87 → 3.24**; and the takings, which used to
  freeze at $176 on night 21, now climb to **$236 by night 116** — the bar can finally afford
  the better bottles, so the growth the quadratic was demanding actually becomes possible.
  **3.5★ went from unreachable to 40% of runs.** The first thirty days barely move
  (bankruptcies stay 0%, income identical, expenses $145.2 → $141.4), which is the point: the
  early pressure was measured and is kept.
  What this gives up is late pressure FROM RENT, deliberately — the author's answer to the
  late game is more to spend money ON (`ileride detaylandırılacak`), so the pressure returns
  as ambition rather than as a landlord who outgrows the room he rents. The star-track report
  now asks `TycoonConfig` for the curve instead of restating it, because the paragraph had
  gone on quoting `12 + 2d + d²/9` after the shipped rule changed and was announcing a
  crossing that no longer happens.

- `23` / `26 §12.2` **the takings have a ceiling and the rent has none (2026-08-14):** chasing
  why the star track's top five rungs came back empty led out of the story module entirely.
  The bot was taught to shop — one brand upgrade a night, the thing that makes a drink better
  rather than merely possible — and **bought zero across 200 runs**, at a 250 cushion, at 40,
  and with the style filter removed. Three candidates eliminated; the answer was the fourth:
  it never has the money. Two real defects fell out on the way — the "no purchase in the last
  week" cutoff was written as an absolute `day <= 23` against a thirty-day horizon and had
  silently become "stop shopping in week four, forever" for any longer run; and the upgrade
  filter reused the menu's style set, which is built from style BANDS while most recipes ask
  by ingredient type.
  **Then the economy itself was measured, five nights at a time.** The takings climb to about
  **$176 a night by night 21 and stop** — the room caps them: so many stools, so many minutes,
  so long a price list. Rent does not stop: `12 + 2d + d²/9`. The two curves cross on **night
  31**, and 60 of 60 long runs die at a median day 36 — a date, not a dice roll. To be open on
  night 60 a bar needs $532 a night; on night 90, $1092. The star-track report solves and
  prints that crossing itself, so it cannot quietly stop being true.
  **Whether the wall is intended — a deliberately finite run — or a cost curve that outgrew
  its income curve is a GDD 23 decision, and it is the author's.** Until it is made the story
  has six usable rungs.

- `26 §12.2` **the star track, measured — and where the measurement stops (2026-08-14):**
  the eleven thresholds are the biggest guess in the new progression, so they were not
  guessed. `LastCall → Measure the Star Track` plays 60 runs over 120 nights and writes the
  first day each rung is crossed to `Docs/star_track_report.md`; the 200-run balance report
  carries the same table over its own thirty-day horizon.
  **The bottom six rungs are real and evenly spaced** — 0.0 on day 1, then 4, 7, 10, 14, 19 —
  all inside the first four weeks, which is a usable schedule: a guest a rung, a rung a few
  nights, and the shopping week between them with something to be for.
  **The top five are UNMEASURED, not unreachable,** and the report is built to say so rather
  than let the reader assume: all sixty runs went bankrupt at a median day 36, and the last
  rung anyone reaches (3.0★) lands on day 32. The bot ran out of MONEY, not out of stars — it
  reads only the ID and never shops, so it cannot buy the bottles a better night is made of.
  The report prints that warning itself whenever the median death lands within a fortnight of
  the highest rung, because a table that cannot tell "the climb flattened" from "the bar died
  first" will be read as the former by whoever opens it next. **Nothing above 2.5★ gets
  written until a bot that shops has been down this road.**

- `26 §12` **the withheld night is a scene (2026-08-14):** `SyncLastCall` keyed the whole
  panel off `Trial`, and a gated night has none — so the guest arrived, the room dimmed, one
  lamp found their stool and nobody spoke. The plate now has a "short" part (host framing,
  the guest's `ShortOfGate` lines, the house's last word), the key reads GOOD NIGHT, SAY NO
  TONIGHT is hidden because nothing was asked, and `OnPlateKey` no longer guards on a Trial
  that is deliberately absent. **The rung is DRAWN** — five stars filled to what this guest
  came for, in the story's magenta, with where the bar stands printed beside them — because a
  sentence would be a caption standing where a drawing belongs (16 §6.7). The plate's own
  keys and type moved onto the chrome language while it was open.

- `26 §12` **the star track: the arc IS the progression (2026-08-14, the author: "her hikaye
  müşterisi belli bir yıldıza ulaşmanı isteyecek ... 0-0.5-1-1.5-2-2.5-3-3.5-4-4.5-5"):** the
  bar's standing stops being a score and becomes the spine — eleven rungs half a star apart,
  one written guest on each. `StoryBeat.RequiresStars` is that rung and may only BE a rung
  (1.3 is refused at construction: the next name has to be a place you can see from here).
  **A locked night still happens, and that is the whole decision.** The guest comes anyway,
  takes the stool, and says what the bar is short of — `StoryLines.ShortOfGate`, which the
  loader now REQUIRES on any gated beat, the same shape as the `hostWarning` rule above it.
  A beat that silently fails to occur teaches nothing, looks like a bug, and makes the
  marked Saturday on the new calendar a lie; a guest who turns up to explain the lock is the
  tutorial for the system, which is why the opener sits at 0 and can never be missed.
  The gate is asked at the STOOL and never folded into `IsDueOn`, and it is booked in its own
  column (`TurnedAway`, apart from `Missed`) sharing one return clock — an ending that could
  not tell "got it wrong" from "was not ready yet" would accuse a player who never fumbled a
  serve. On a withheld night `Trial` is null, so there is no post-it for a job nobody offered,
  and `BeginLastCallTrial` is a no-op so the UI ends every dialogue the same way.
  §12.2 writes the ordered path for what is left: the withheld scene in the UI, then
  **calibrating the eleven thresholds with the sim** (the biggest risk in the design, and the
  machine that answers it already exists), then one `UnlockCondition` type to replace the
  three kinds of lock living in three places before a fourth arrives. 294 EditMode green.

- `21 §12` **the last two guards of the overturned rule (2026-08-14, the author: "soda
  shakera dökülmüyor"):** Core took fizz in the tin, the wall routed it to the bench, and
  then the BENCH refused to pour it — a `!fizzy` term in the tilt-pour's `pourNow`, saying
  "FIZZ DIES IN THE TIN — BUILD IT AT THE GLASS". Its own comment had argued for keeping it:
  *"a guard that only lives in the routing is a guard that dies in a refactor."* True, and the
  mirror is truer — a guard kept after the rule it guards is overturned becomes a rule of its
  own that nobody wrote down, and it outlived Core's refusal by exactly one day. The wall
  carried the twin: it judged a fizzy bottle against the SERVING GLASS's fullness, so it
  greyed out for a glass the bottle never touches and stayed lit over a tin with no room.
  Both gone; every non-beer bottle is now judged against the tin, because that is the only
  vessel it can go into.

- `21 §12/§12a` **the loop closes, and a shaken fizz goes off in your hands (2026-08-14, the
  author: "alkolü çöpe attığımızda sıfırlanılması gerekiyor fakat bardak ekranına
  gönderiliyor… kokteyl yapılıp servis edildikten sonra döngü başa dönmüyor"):** `_capped` is
  bench state and the bench only cleared it on the way IN, so binning or serving a capped
  drink left the wall convinced the next bottle belonged at the counter — the loop never came
  back to the start. Decisions made off the bench now ask the derived `Capped`, which reads
  the tin and cannot be stale: **an empty tin is never capped.**
  And shaking fizz **bursts the tin** (`ShakeBlowsTheTin`): the drink is gone, the goods are
  written off through the bin's own body so it is never the cheap way out, `Blowouts` counts
  it, and the bench draws the bang before resetting itself. The recipe still outranks the
  bubbles — a Gin Fizz is shaken WITH its soda, so the rule asks `TinMethod` first.

- `21 §12` **the second building place is gone with the rule that needed it (2026-08-14):**
  the serve counter's bottle-in-hand — the one thing the wall could hand it, because fizz had
  nowhere else to go — is cut, and the sim bot now pours carbonated into the TIN like the
  player does instead of holding it back for the glass. A bot that builds drinks somewhere
  the player cannot is a bot that has stopped measuring the game. Measured: the 200-run
  report came back **byte-identical**, so the rule change costs the economy nothing.
  `PourAtGlass` is now called by nothing but the tests.

- `21 §12/§14` **the tin takes everything, and the recipe names the method (2026-08-14, the
  author: "tüm içecekler shakera koyulacak soda gibi gazlı içecekler karıştırılacak tarife
  göre" + "tariflerin hangilerinin çalkalanması gerektiği hangisinin karıştırması gerektiği
  belirtilsin önemli"):** §12's rule that carbonated never enters the shaker is **overturned**.
  `ShakerIngredient` refuses only the keg now, and what happens to a mix in the tin is its
  recipe's `prepMethod` — `MixRequired` reads `TinMethod` (the matched recipe's method) and
  falls back to the two-spirit rule only when the book cannot name the drink. That is what
  lets the twenty-one **Built** highballs come through the tin instead of being assembled in
  the serving glass, which is what gave the bar two building places and stranded a tin with
  the lid open when a soda was picked up after the spirits.
  **The bench says which:** the step card's third row is SHAKE IT / STIR IT / BUILT, NO
  MIXING, the line under the title names the method, and the recipe book prints BUILT beside
  SHAKEN and STIRRED now that it is an instruction you can get wrong. **The wall routes on
  the lid:** open tin → the bench, closed tin → the counter, closed-but-unworked → back to
  the bench with the reason. **And the lid comes off again** (`UncapTin`) — "karıştırmayı
  unutursa diye".
  Two defaults changed with it, because the method stopped being a grading detail the moment
  it could refuse a pour: `RecipeDefinition`'s constructor defaults to **Built** (a rule that
  conscripts the player must be asked for out loud), and `DataLoader` now **throws** on a
  recipe that names no `prepMethod` instead of silently making it shaken.

- `16 §2` **one key, everywhere (2026-08-14):** the four dialects are closed. `KeyPlate.Dress`
  is the only way a control gets dressed now — the drawn `ChromeArt.Key()` body, sliced,
  tinted by the caller, with the house press wired to a face that carries the caption. It
  replaces the service flow's `plate`/`plate_down` sprites out of Resources, the HUD's flat
  coloured rect and the settings menu's bare rect. MENU, BOOK, every settings row, BACK TO
  BAR, TO THE GLASS, SERVE IT and POUR FIRST are the same object the market's key always was.
  `AddEdgeBack` was still setting its caption at 12pt — the audit only measures the screen
  that is actually on the screen, and the bench had not been on one when it last ran.

- `16` **the chrome has a written language again, and a machine that checks half of it
  (2026-08-14, the author: "butonlar ayarlar menüsü oyunun ekrandaki HUD paneller bunların
  aynı sanat dilinde olması gerekiyor"):** module 16 was describing a game that no longer
  exists — 640×360, the card era, hand-authored 9-slice sprites, engine primitives banned —
  while the chrome it was supposed to govern is drawn in code at 1280×720. It is rewritten as
  the **CHROME LANGUAGE**: the field and its laws (§0), the named objects a surface is built
  from — BEAM, CASE, GLASS, KEY, PLATE, LAMP, RULE, MARK (§1) — the ONE key (§2), the scaling
  and fitting laws (§3, §4), what light and colour are allowed to say (§5), **the ten tells**
  (§6, every one of them something this project shipped and the author rejected), and the
  delivery gate (§7).
  **`LastCall → Audit UI` measures the mechanical half** — scale, fit, grid, type, palette —
  on the live screen and writes `Temp/UiAudit.txt`. It found four defects on its first clean
  run, three of them shipped the same week: the standing's stars drawing a 16-pixel star at
  22 units (1.375×, now 32 = 2×), the bin at 1.105×, **every worded key in the game set at
  12pt** — a size the pixel faces do not have — and the clock's colon lamps at ±5.32. A branch
  that is not chrome opts out through `UiAuditExempt` and has to say why in a sentence the
  report prints; the glass rack and the register are placed by perspective and do.
  **Known debt, written into §2:** the game speaks three button dialects — the market's real
  `ChromeArt.Key()`, the HUD's flat fill, and the settings menu's bare rect. They become the
  KEY. 285 EditMode green, 8 PlayMode green, three baselines re-blessed after looking.

- `18` **the board is a beam, not a row of boxes (2026-08-14, the author: "takvim tasarımı
  kötü, kutu kutu ai tarafından yapıldığı çok belli ... üst barı tamamen yenile"):** the top
  bar was five bordered slabs side by side, each with a caption over a value at its own
  height. It is one object now — a fascia with a lit top face, a front that falls away from
  it, and a neon tube along the bottom that is also THE STATE LIGHT (the whole board burns
  magenta at last call; it used to be a 2px rule under one plaque that nobody was going to
  see). On it: the clock as the only body left up here, the week as a MARQUEE — round lamps
  on a wire over their letters, the wire stopping short of Sunday because the bar does not
  open — and the standing as five stars standing on the beam with no plate under them
  ("yıldızlar hala üst barda kutu içerisinde gösteriliyor"). Everything is placed against
  two rules, `CapY` and `ReadY`, which is what "yazılar hizalanmamış" was pointing at. NEW
  RUN moved inside the settings key, where a button that throws away the night you are
  playing belongs. Three measured corrections on the way: the marquee's parts were all
  pivoted to their left edges and so hung left of their own letters; the standing's number
  is 72 units wide in a 60-unit rect and overflowed onto the fifth star; and the cog came
  out at 20 units, which is 1.25× of a 16-pixel drawing — pixel art scales at whole
  multiples or it does not scale. `ChromeArt.Lamp`/`LampGlow` are the new drawings.

- `18` **the clock is drawn as a clock (2026-08-14, the author: "dijital saat olmamış hiç
  dijital saate benzememiş görsel olarak"):** the first pass at "saat dijital saat görüntüsünde
  olsun" set the hour in the UI's pixel display face and laid a dim `88:88` behind it. That is
  a caption in costume and it read as one. `SegmentClock` draws the real geometry instead —
  four digits of seven bars, the unlit ones sitting just above the glass at 0.085, a colon that
  blinks on its own half-second, one unit of bleed per lit bar — sunk in a bezel with a shadowed
  lip above and a lit lip below. **The gap is the whole trick:** the first cut tiled bar/arm/bar
  to exactly the cell height, so an 8 came out a solid brick; a unit of dark between the rows is
  what makes a numeral read as segments. The week number stopped being printed twice across one
  board (the strip owns it, the plaque owns the night), and `TopPlaque` gained a bevel — lit top
  and left, shadowed right — so the readings read as panels screwed to the board rather than as
  filled rectangles. 285 EditMode green, 8 PlayMode green; no baseline moved, the top bar being
  in none of them.

- `26 §11` **only Ece for now (2026-08-13, the author: "şimdilik sadece ece olsun"):** the live
  arc is one written night — hers, on the opening Tuesday — and the collector, the influencer
  and the gourmet are lifted out of `story.json` into `Docs/story_guests_drafted.json` whole,
  to be put back one at a time once the story around her is built. Nothing loads the drafts,
  so an EditMode test builds them against the real cast and the real recipe book anyway: a beat
  that rots quietly in a drawer is worse than no beat, because it is discovered on the day
  somebody wants it. One night still exercises the entire module — she arrives after the door
  shuts, the room dims to her, the plate talks, the trial runs its own clock, the arc finishes
  — and the sim says so: 200/200 trials walked in, passed, and closed their arc.

- `26 §7` **the room dims to the last customer (2026-08-13, PLAN S4 + the rest of S3):** the
  closing beat is lit. `DiegeticStage.SetClosingBeat` takes the ceiling down to 0.22 of itself,
  thins the wash to 0.55, burns the LAST CALL neon at 1.9×, and lights ONE lamp at the guest's
  own stool — every number an intensity on a light that was already hanging in the room, so the
  beat never reads as a different game. It ramps over a second, reverses when the stool empties,
  and `Motion.Reduced` gets the same room without the ramp. The rest of S3 closed with it: the
  guest takes the stool nearest the till, a guest whose clock runs out walks instead of storming
  (and is not booked as a walk-out, being on no books), and the story face is reasserted once a
  frame — the stool had been handing the guest a rolled body while the plate showed the right
  person, and a stool keeps its look by design, so the beat is made the authority instead.
  **The proof is measured, not photographed, and that was a correction:** a blessed picture
  failed its own second run by 89,684 pixels — the plate's cream sits five units apart between
  runs, and the settings key drew its icon in one and its word in the other. Neither is the
  closing beat. `LookTests` asserts the four numbers that ARE the beat, then declines the guest
  and checks the lamp goes out. 284 EditMode green, 8 PlayMode green.

- `26 §7` **the last customer can be seen and spoken to (2026-08-13, PLAN S3 part one):** the
  arc is IN PLAY — night one is Ece's, and the module stops being invisible. Two surfaces do
  all of it. **The plate** at the counter (a face, a name, one line, GO ON, SAY NO TONIGHT)
  works a script: the host frames the night, the guest asks, and the last GO ON is what starts
  the clock — so reading is never punished. **The post-it**, pinned above the bench and the tap
  because a trial is worked while it runs: who is waiting, the drink in hand, `2 OF 3 · NO
  MISTAKES`, the clock, and what the shelf is missing. The guest is drawn as themselves (the
  beat names the face; hashing their name would have put the rent collector in a different body
  every visit) and named as themselves (Ece was being introduced as the papers of the face she
  borrows). Their seat ticket says TALK TO THEM rather than the drink — their licence is open
  from the moment they sit, and the ticket would have handed over a trial that is meant to
  arrive one drink at a time. Two fixes came out of PLAYING it, not testing it: the guest used
  to vanish on the same tick as the last serve, so the night closed over their last line —
  Core keeps them on the stool for `LastWordSeconds` now — and a trial that ran out of clock
  was answered with the line written for an honest no, which `StoryTrialRun.ToldNo` now tells
  apart. 284 EditMode green, 7 PlayMode green.

- `26 §4/§9/§10` **the story is loaded, and the bot plays it (2026-08-13, PLAN S2a+S2):**
  `DataLoader.ParseStory` builds the arc against the real cast and the real recipe book with
  the loud validation every content file signs, and the bootstrap parses it at boot (it does
  not hand it to the run yet — the guest arrives into a conversation nothing can hold up
  until S3 draws the plate). A new writing rule is enforced at load: a beat that needs a style
  must SAY that style in a `hostWarning` line, because the asks come one at a time and the
  host's early warning is the only notice a player gets. The simulator now plays the arc — one
  immutable arc shared by two hundred runs — and the report gained the written-nights block,
  the beat each run still owed at the horizon, and *what came back and why*, with the delivered
  glass in the line. That diagnostic paid for itself three times over: a fizzy Built drink
  poured in big measures is re-housed mid-build and ends up a permanently wrong ratio (the bot
  builds in small rounds now; **the crowd's path is untouched, and whether it should change is
  its own balance question**), and the last beat was failing because `grantsRecipeOnAsk` was
  written but never wired — the ask now hands its page over at the seat, gate and price waived,
  the one door in the game that opens the book for free. 200/200 arcs finish inside thirty
  nights; the ordinary night is unchanged against a storyless run of the same seeds (standing
  2.82 → 2.83, storm-offs level, customers level), while the till rises $161 → $208 median,
  which is the granted page not being bought.

- `21 §12` + `GDD_MEVCUT §8/§9` **the three service scenes rebuilt, and the fridge retired
  (2026-08-13, the author):** the benches were crowded with furniture that did nothing —
  the shaker's prep table had carried no prop since the four preps moved to the glass on
  2026-08-10, and the serve stage's drinks fridge stocked only the fizz behind a sprite
  that drew across the mix gauge. Both are gone. The first attempt replaced them (a speed
  rail on the bench, an open case at the counter, both carrying the whole cellar) and the
  author cut that too, which is the ruling that stuck: **no drink is chosen on a service
  bench.** The bar has ONE picker, the back bar wall, and it now carries everything except
  garnish — fizz included, where the wall had been hiding it since 2026-08-02. Each bench
  is what your hands are on: the shaker keeps the bottle you walked in with, the tin, the
  lid and the spoon; the serve counter keeps the glass, the tin that fills it and the
  finishing table. **No rule moved** — carbonated still has exactly one door (the serving
  glass, Core's refusal, §12), beer still only the tap, and the wall hands a fizz bottle
  over already in hand. The tap gained the opposite of a cut: its under-bar recess is a
  real cellar now, every stocked keg standing in a bay, and clicking one couples it —
  through `CanPull`, so a refused swap changes nothing and says why. Both benches wear the
  back bar's own wall behind them, so the halves of one drink are shot on one set.

  **And then the furniture went too** (same day, the author: "bardak sahnesindeki masa
  assetini kaldır, zaten mor alan tezgahmış gibi olmalı, aynısı shaker sahnesi için de
  geçerli"). `prep_table` and `bar_mat` are off both benches: a drawn table was a second
  surface inside the first, and the props were standing on a picture of a counter that sat
  on top of the counter. The panel IS the counter now — the wall band, a counter band one
  step up the Night ramp, and a lit edge where they meet — and everything standing on it
  carries a `BackBarArt.BottleShadow` contact shadow, the tin's and the bottle's tracking
  their own foot lines per frame and fading as they are lifted off. The faint slab the two
  play surfaces used to draw went with them: they are coordinate spaces, not scenery.

- `26 §3/§4` **the last customer is an inspector, and a guest of the house (2026-08-13, the
  author):** the beat reworked from "one drink with dialogue" into a TRIAL — several drinks
  against one clock, revealed one at a time on a post-it, to a standard nothing else asks for
  (exact drink, exact craft, exact method; the fill alone forgiven at ≥0.90 of the glass), with
  a written mistake allowance that is data. The named model is Dave the Diver's service nights.
  And the guest left the books entirely: no licence (they introduce themselves — the ONE
  written exception to the hidden-information rule, fenced in CLAUDE.md), no bill, no tip, no
  rating, no line on the slip — `CustomerVisit.OnTheHouse`, read once per ledger via
  `BarDay.FinishedCounted()`, because a trial that can move the standing is a trial the player
  can farm or be robbed by. The conversation holds the clock (`ClockHeld`): nothing ticks while
  they talk, the trial starts it (`BeginLastCallTrial`), and a 120s grace backstop keeps a held
  plate from holding the night. The asks-one-at-a-time reveal moves the shopping warning to the
  HOST's lines days early ("have something brown by Friday") — the quiet nights stay the
  preparation nights. Cast recast to public people with the standing to hold a trial: the
  influencer (`teal`) and the gourmet inspector (`profess`) join Ece and the collector; the
  sister and `ember` went back to the crowd. Difficulty climbs across the arc: 1 drink/2
  mistakes → 2/1 → 2/1 → 3 drinks/none.

- **NEW `26 §2b` + `Core/BarCalendar` — the guests come at the weekend (2026-08-13, the
  author):** a story beat is no longer written for "day 9" but for a WEEK and a NIGHT, and a
  guest's night must be Friday or Saturday. The calendar it hangs on was already on the screen
  and meant nothing — the plaque has printed `WEEK 2 · FRIDAY` for weeks — so this costs no new
  vocabulary and buys three things: the quiet nights become the shopping nights (the ask names
  what is missing, and now the deadline has a name), the beat stays rare enough not to become
  wallpaper, and the fiction stops asking a rent collector to turn up on a Wednesday. The house
  is not a guest: Ece may work a quiet night, and beat zero is still the opening Tuesday. The
  calendar moved out of `TycoonHud` into Core with its words unchanged, because a calendar the
  UI keeps to itself is one that can disagree with the rules. The one real hazard is silent —
  a return clock in DAYS would push a Friday guest onto a Sunday, where the gate never opens
  and the arc stops without ever failing — so returns are counted in weeks on the beat's own
  night, and that is a test rather than a comment. Arc rescheduled: Ece W1·TUE, the collector
  W1·FRI, the sister W2·SAT, the critic W3·FRI. The weekend does NOT yet mean a bigger crowd or
  a different rent; that is the economy's decision, fenced off deliberately.

- `26 §2/§3/§9` + `GDD_MEVCUT §3` **the last customer exists, and says nothing (2026-08-13,
  PLAN_last_call S1):** Core can now play a written night. When the door has shut and the room
  has drained, `BarDay.SeatGuest` sits somebody the night did not roll — outside the arrival
  clock, the seat count and the crowd, ordinary in every way after that — and the day's end
  condition was left exactly as it was, because *the door is shut and the last stool is empty*
  already refuses to be true with a guest on a stool. The beat's drink, price and patience come
  from data and not one die is rolled. Serving what was asked for advances the arc; a wrong
  drink, an honest no (`DeclineLastCall()`) or simply never coming to the stool spends the
  night and the beat returns on its own clock — no dead ends, by construction. Content and
  state are split (`StoryArc` is shared and never changes, `StoryProgress` is per run) so the
  simulator can hand one arc to two hundred bars. Still silent and still unwired to
  `story.json`: the story is opt-in like the regulars, so a run without one is the game that
  shipped. 238 EditMode green (23 new), 7 PlayMode green.

- **NEW `26_last_call_story.md` + `PLAN_last_call.md` (2026-08-12, the author's brief):** the
  last call becomes a SCRIPTED beat instead of a clock running out. When the door has shut and
  the room has drained, one named person comes in and asks for a drink the bar cannot pour yet
  — the ask names what is missing, so it is a door and not a wall — and the arc is data
  (`Assets/Data/story/story.json`, first three beats written). Dialogue is two lines a beat in
  the bar's own terse register; the "cutscene" is the 2D light rig dimming to one lamp and the
  neon igniting, not drawn frames. Nothing is missable: served wrong or declined, the beat
  re-arms on its return clock. Two findings came out of writing the data: the cast's papers are
  30 hardcoded lines in `TycoonHud` and must become data first (S0), and **every stirred recipe
  sits at rank 22+, i.e. the 4★ band**, so the stir verb is invisible for most of a run — until
  the ladder grows an early stirred drink, the critic's beat teaches it by handing over the page.

- `15 §8 / 24 §2` **a vessel is measured, not assumed (2026-08-11, the author's new juice
  art):** sizes, standing and the SPOUT all come off the drawing now (`VesselArt`), because
  a UI Image lays a sprite out on its whole sheet and hand-drawn art does not fill its
  sheet. The pour streams from the cap — found, where a capless shot exists, as what the
  closed and open shots disagree about, since a carton's spout is a stub on a flat roof and
  the top of the silhouette is the roof. Vessels wider than 0.44 of their height are fitted
  by width, so a carton stands beside a spirit bottle at a carton's size. Bench, back bar,
  fridge and the market's product line all read the one law; tightly-trimmed art (every v3
  master) is unmoved by it.

- `23 §3.1/§4 + 21 §12` **the method is the recipe's (2026-08-11, the author):** "extra
  shaken" — a customer whim rolled at 25%, and the only place mixing was ever graded —
  retired; the judge now grades `RecipeDefinition.Prep` against the delivered glass
  (craft = 0.6 garnish + 0.4 method; wrong mix = no mix; Built indifferent; the extra
  round demands the right method too). And §12's own debt paid: Klara Soda and Kicker
  Ginger's `carbonated` flags flipped, the glass-side cabinet stocks carbonated ONLY —
  juices and still mixers build in the tin, the fizz is topped at the glass.

- `21 §14` **the mandatory mix + the Stir verb (2026-08-11):** two spirits (category test,
  ≥3% share each) may not leave the tin unmixed — the refusal lives on
  `PourIntoServingGlass`, the UI reads `CanPourOut`. `Stir` arrives as `Shake`'s mirror
  because every stirred recipe names two spirits. Built-at-glass exempt; bin ungated;
  judge still blind to the method (the reserved craft pass). Sim report byte-identical
  with the bot mixing by `recipe.Prep`.

- `22 §4 / 23 §8` **inventory economy (2026-07-23):** the menu is your stock now. You open
  with a **bare well** — two spirits and the essential mixers — and **grow the shelf by
  buying new stock** at the end of each night: the market lists both **new bottles** (styles
  you lack, `+`, which are *added* to the shelf so their drinks become makeable) and
  **upgrades** (`↑`, better brands of what you have). A just-bought bottle **flashes ★ NEW**
  on the menu the next day; a depleted bottle reads **OUT** and can't be poured until the
  restock. New-stock prices scale by type and tier (`Market.StockPrice`). *(Still owed from
  the brief: a drink pays a premium for the pricier alcohol used — a small follow-up.)*

- `23 §4–5` **emotion → recipe pivot (2026-07-22):** the emotion layer no longer drives play.
  What you **read off the licence** is now the **drink recipe** (its ingredient bands) and the
  **garnishes the customer wants** (on ice, a twist, a rim) — the ID card shows these where the
  six moods used to be, and the seat tag hints "WANTS EXTRAS". Satisfaction keys off the drink
  match, the garnish craft, and speed; getting every asked-for garnish on an exact, fast serve
  earns another round. Garnishes lift satisfaction (and so reputation and the crowd's wealth)
  rather than paying a direct tip, keeping the till predictable. The emotion data structures
  stay dormant under the hood (customer generation still uses them); `MoodTipLanded` → `CraftLanded`.

- `23 §4` **balance + fixes pass (2026-07-22):** the **wrong drink now pays nothing** (was
  half) and sours the room; the **speed tip scales** up to $4 for the right drink served
  fast, fading to nothing across a wider window (a slow serve just misses the tip). The
  **serve reaction itemises the bill** — the drink's price in amber over the tip in green.
  The finished glass on the counter now shows the drink **as it was actually built** — its
  real fill level and blended colour, not a fixed amount. Ice/lemon/salt/sugar dropped in
  now **fall and dissolve the instant they touch the drink** (they no longer float inside).
  The shaker **stays where you set it down** after a shake (no teleport home). And every
  canvas now **matches height**, so the layout no longer drifts when the window aspect
  changes. *(Still open from the same brief: particle fluid, emotion→recipe pivot, inventory
  economy, real character animations — see PLAN.)*

- `24 §3.5` **feel pass (2026-07-22):** the pour is now a **metaball fluid** (`MetaballFluid`
  + `Shaders/MetaballLiquid.shader`) — droplets melt into one connected mass and into the
  pool instead of reading as separate balls, the liquid gains volume and takes the glass
  shape, and it lands with an organic splash; the fill is slower and more deliberate. The
  pooled **surface is a shallow-water height-field** — waves travel, reflect off the glass
  walls and settle, with a lateral slosh over the top. **Ice and lemon float inside the
  shaker** (`ShakerSolids`), bobbing at the surface and flung about when you shake. **You
  grab the shaker itself and throw it around** to shake (the hold-pad is gone), and the
  meter continues from what's already been shaken. The finished drink now appears as a
  **glass on the counter that you drag onto a customer to serve** (a heavy, springy AAA-feel
  carry; clicking a customer only reads them). The menu drops its ICE/LEMON/SALT/SUGAR and
  SHAKE buttons (those are hands-on now), and the till moves to the counter's edge. Dragged
  preparations have weight — the grip springs after the cursor with overshoot while the
  piece swings from it (`Pendulum`). Still procedural
  placeholder art, still cosmetic-only over the deterministic pour. Also: the pre-menu stage
  dressing (top-left pour-glass HUD, on-counter bottle rail, garnish jars) is retired, and
  the cash register opens a **ledger of past days** (`24 §7`).
- `24 §4` **stable stools & leave animation (2026-07-22):** customers now **keep their stool**
  until they leave — busts no longer shift or morph when the queue compacts (the HUD maps
  visits to fixed stools). When a patron leaves they play an exit: a served one **sinks off
  the stool and fades**, a stormed-off one **shakes then slides out**. Completes the lifecycle:
  walk in → sit → read/serve → react → leave.
- `24 §4` **serve reactions & storm-off notice (2026-07-22):** serving a customer now floats
  a reaction from their seat — green "PERFECT!/THANKS." with the payment, a gold "★ ANOTHER
  ROUND!" when they reorder, red "NOT WHAT I ASKED" on the wrong drink — with a little pop.
  A patron whose patience runs out raises a red "A CUSTOMER STORMED OFF" notice.
- `24 §4` **customers at the counter (2026-07-22):** the bottom seat strip is retired —
  customers now sit at the bar as head-and-shoulders portrait busts with a floating order tag
  above the head, souring red over the last third of patience, sliding in and fading up as
  they arrive. An interim step toward the P8 physical-customer characters, using art that
  already exists.
- NEW `23_tycoon_loop.md` — **the loop is a business now**: up to 6 simultaneous seated
  customers ordering named drinks, patience clocks, price+tip payments (mood tips from the
  emotion read), extra orders on perfect serves, days of 8+ customers, a day-end invoice,
  rent, and **3 consecutive red days = bankruptcy** as the only loss. Reputation bar sets
  tomorrow's crowd wealth. Quota/score loop retired (demolition scheduled, PLAN P7).
- NEW `24_service_flow_presentation.md` — bottles move off-stage into a counter **menu**;
  drinks are built in a **shaker focus stage** (hold-pour → preparations → shake by
  mouse), served by **aimed pour** into a glass (spilling returns as an aiming skill), and
  delivered to a chosen seat. Art direction v3: Dave-the-Diver-level density/animation
  floor, whole-set replacement (P8), scripted tutorial shift.
- NEW `PLAN_tycoon_pivot.md` — P0…P9 phases with gates; old loop remains playable until
  P3, deleted at P7.
- v1–v3 modules stay as subsystem specs where referenced by 23/24; banners mark the rest.

## v3.3 — No spills, the house pour & a legible shelf

- **The glass cannot overflow** (21 §3, supersedes v3.2's "merciful spill"): pours stop at
  the brim, the bottle keeps what the glass cannot take, and the hold auto-releases. A
  heavy hand costs precision, not the drink. `Spills` now counts binned drinks.
- **The house pour** (21 §9): a no-recipe drink pays its volume-weighted Flavor at ×1
  instead of a hard zero; recipes keep paying Flavor × Mult, an order of magnitude more.
  Charges stay at ×0.5 without a recipe — money changed, feelings didn't.
- The recipe book gained a shelf legend: each type word the bands use (SPIRIT, SOUR…)
  mapped to the actual bottles on tonight's shelf in their tag colours — the "which bottle
  is a Spirit?" gap between book and back bar is closed.
- Back-bar rows re-spaced (upper 246 / lower 158) so the lower row's bottle caps stop
  overlapping the upper row's name tags.

## v3.2 — The customer POV, style identity & the merciful spill

- **The stage flipped to the customer's side of the bar** (22 §1): bottles on two back-bar
  wall shelves (spirits up, mixers down), the till beside the patron, the counter along the
  bottom. GDD 18's layout section is banner-superseded; `DiegeticStage` holds the truth.
- **Style identity is explicit** (22 §1): display names carry the style word ("Astra
  Vodka"), and every style owns a signature colour (`UITheme.StyleColor`) worn by the shelf
  tag, the ratio list and the liquid itself.
- **A spilled glass can be served** (21 §3): charges cap at one glass's worth, no recipe,
  no fill bonus, still a spill for patron conditions. The hard "bin it first" block is gone.
- The pour glass is a proper stemmed cocktail glass whose fill is clipped by a stencil mask
  baked from the sprite's own bowl — no more square fill floating on the art (21 §3.1).
- **Garnishes go in by the pinch** (21 §3): one tap = a fixed 5% of the glass
  (`GarnishClickFraction`); no more held-jar 1% slivers.
- **Recipe generosity pass** (21 §9): derived bands ±15% → ±20%, unnamed-stray allowance
  10% → 15%. Free-hand pouring should be a judgement call, not a precision test.
- The recipe book rewritten for the pour era: pourable recipes shown as their ratio bands
  (type-coloured, with FILL minimums), unpourable ones counted as "house secrets"; the
  card-era dot patterns and "pick 1–5 bottles" hint are gone.
- Fixed: HUD texts no longer swallow clicks (the win banner sat exactly on the upper shelf
  and made its whole row unclickable); the balance sim loads `base_bar.json` (it silently
  died on the deleted classic bar).
- The sim bot grew up with the shelf: it refills the well (upkeep, not strategy), never
  stalls on a drained or already-landed customer, and **seeks recipes** — staffing each
  pourable recipe's bands with intent-aligned bottles at band midpoints. Measured effect of
  the generosity pass with a recipe-seeking floor bot: orders filled 0.1% → **26.5%**,
  bust rate 9.5% → 7.9%, win floor 0% → **15%** (old 25/26.7% figures were inflated by the
  Double Perfect derive bug and are not comparable).

## v3.1 — Bottles, brands, the market & the hi-bit art pass

- NEW `22_bottles_brands_market.md` — the curated 12-bottle base bar with brand identity
  papers (style/tier/origin/ABV/blurb), the end-of-night brand market, the licence-style
  patron ID (name/age/hometown, happiness gauge), preparation infrastructure
  (shaker/ice/rims, plumbing only), and the v2.5 hi-bit art direction (2x texel density in
  the same 640x360 layout).
- The Flavor numbers came off the bottles; brand-name shelf tags replace them. Flavor still
  feeds volume-weighted scoring and will surface in the bottle-info popup.
- `classic_bar.json` remains as data for tests and packs; the shipped shelf is
  `bottles/base_bar.json`.

## v3.0 — The pour pivot

- NEW `21_pour_system.md` — hold-to-pour, the glass, ratio recipes, bottle volume economy.
  The deck, rail and Restock are deleted; see the audit and casualty list in
  `PLAN_pour_pivot.md`.

## v2.0 — The emotion pivot

The core loop changed from "recognise a pattern, score points" to **read the person and serve
what they need**. Recipes were demoted to the craft layer, not deleted.

**New modules**
- NEW `19_emotion_mechanic.md` — the six emotions, tiered visibility, the ID, charges,
  resonance/Clean Serve/bust, and the information economy.
- NEW `20_regulars_and_week.md` — persistent regulars, drift, relationship, archetypes, and
  the weekly quota with its measured balance figures.
- NEW `../PLAN_emotion_pivot.md` — the phased delivery plan and the rulings behind it.
- NEW `../sim_report.md` — output of `LastCall → Simulate`, regenerated on demand.

**Rule changes**
- **Loss condition replaced.** Failing one order no longer ends the run; only a missed weekly
  satisfaction quota does. `03_run_structure_balance.md` §5.1's table now gates Tips only.
- **No-recipe mixes** still score 0, but their emotional charges pour at ×0.5 — this closes
  the open "high card fallback" question in `02_recipes_scoring.md`.
- **Mult gains a resonance block** applied after patron hand effects.
- **Content counts:** 64 patrons (+4), 17 tools (+1), 23 VIPs (+3), all on the information axis.

**Customer difficulty**
- NEW `DemandLevel` (Easygoing / Particular / Demanding): customers get harder to please as
  the run goes on. Moves the goalposts (how much movement counts as "strong"), never the
  ceiling (a Clean Serve is always worth 3). Shown on the ID.
- The quota curve flattened to 7/10/11/12 in response — the escalation now lives in the
  customers, and stacking both double-counted the difficulty.

**Rewritten**
- `12_tutorial_ftue.md` — rebuilt around what is actually opaque now: asking for ID, reading a
  RANGE, busting, the weekly gate, and demand. Busting is the top teaching priority.

**Stale, flagged in-place rather than rewritten**
- `08_ui_screens.md` is stale on the gameplay screen, current on menus and modals.

**Housekeeping**
- Unused assets, code and packages removed; the build was pointing at `SampleScene` and now
  points at `Main`.

## v1.1 — Design additions during M2
**⚠ M2-BLOCKING (implement before content lock):**
- `02_recipes_scoring.md`: recipe table expanded 11 → 14 (adds value-based and mono-Type recipes), explicit priority numbers, deterministic tie-break rule. **ScoringEngine recipe detection must use this table.**
- `03_run_structure_balance.md`: added §5.4 Regular's Favor tags (skip rewards), §5.5 VIP pool rules (no-repeat, gentle pool, reveal timing).
- `05_shop_economy.md`: starting money defined ($4), Patron duplicate rule, sell values clarified, new voucher "Bouncer" (VIP counterplay).

**Non-blocking (design locked now, implemented later):**
- NEW `12_tutorial_ftue.md` — first-time user experience (build in M4).
- NEW `13_stats_daily_achievements.md` — stats screens, daily challenge, achievements (M5/post-launch).
- NEW `14_art_bible.md` — visual identity spec (required before asset production).

## v1.0 — Initial 12-module GDD
