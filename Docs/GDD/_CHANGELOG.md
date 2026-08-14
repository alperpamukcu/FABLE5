# GDD Changelog

## v4.0 (current) — THE TYCOON PIVOT (in progress)

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
