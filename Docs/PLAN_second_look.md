# PLAN — The second look (the author's round of 2026-09-21, after the certificate)

The author, 2026-09-21, in one message: the designs are becoming "AI slop" and the game needs colour theory and
the rules of game UI applied; his added art is still not in the game; mint and olives must be extras asked for,
not recipe ingredients, and must not fill like a bottle; character animation must not slow with the room's
clock, only the waits; the top bar's lit icons should move every five seconds; the beer scene must be redesigned
from scratch with its assets; on the shaker bench every panel but the shaker and the bottles must ride the
background, the mix bars must say which spirit and how much, the lift's colours must run green to red, and
SERVE IT must be designed again; some characters and garnishes show the background through them; the pothos
must stand behind the tables; kitchen lamps over the counter; a softer cellar sound and a cupboard door in the
shutter's place, and the pink arrow gone regardless; the unlocks inside the certificate; a dev preset for every
star level; and a preview of the first certificate's celebration with sound and an opening animation.

Same rules as every plan here: Core decides, the UI renders, content is data, nothing ships on "it compiles".
The design rule is new and written down in memory (`ui-design-not-ai-slop`): three colour roles a screen, one
accent, one motion, no ornament, and proposals before new screens. Status: ☐ todo · ◐ in progress · ☑ done.

## 1. Quick, certain (ship first)

| # | Item | Where | Status |
|---|---|---|---|
| 1 | Certificate v3: unlocks INSIDE the sheet; one accent; comfort in medals, service in hearts (as the top bar); opening animation (the sheet unrolls); confetti WITH sound (`cheer_sfx`, `level_up`); a preview GIF for the author; the sheet keeps its width, 16:9 at the least and taller only when needed | `TycoonHud.Ladder` | ☑ |
| 2 | A dev preset for every rung (0.5–5★) with the fittings, stock, book, seats and day of that level; the bench's KOŞU keys | `TycoonRun.DevPresetStars`, `TycoonHud.Chrome` | ☑ |
| 3 | The top bar's lit stars, medals and hearts wave every five seconds | `TycoonHud.Chrome.RefreshStanding` | ☑ |
| 4 | Greenery behind the furniture (the pothos was in front of the left table) | `DiegeticStage.PlaceFixtures` | ☑ |
| 5 | The shutter: the pink arrow gone; `cellar_open` / `cellar_close` re-made softer | `DiegeticStage.BuildShutSign`, `Tools/sfx_bank.py` | ☑ |
| 6 | Character animation at full speed under the slow clock; stopped only by the pause | `TycoonHud.Seats` (`AnimDelta`) | ☑ |
| 7 | The lift's rungs green → amber → red | `TycoonServiceFlow.Shaker` | ☑ |

## 2. Rules and data

| # | Item | Status |
|---|---|---|
| 8 | Mint and olives are GARNISHES asked for as extras (`Preparations.Mint`, `Preparations.Olive`), never recipe bands, never poured; the rail's jars are dishes like ice; Core refuses a garnish whose jar is not stocked; recipes.json ↔ RecipeCatalog under the parity test | ☑ (GDD_MEVCUT 9.92) |
| 9 | The author's rim FRONT plates (`glass3d_*_rim_*_FRONT`) drawn over the drink: the whole ring in the decor, the near arc over the front crop | ☑ |

## 3. Proposals first (the design rule), then code on the author's pick

| # | Item | Status |
|---|---|---|
| 10 | The beer scene from scratch, with its assets (PixelLab → report → pick) | ◐ the author picked **A**; the bench is in the family now (GDD_MEVCUT 9.96); the tower waits on the pick (`Docs/reports/third_look/` T1–T4) |
| 11 | The shaker bench: panels riding the background; mix bars that name the spirit and its share; SERVE IT again | ☑ (GDD_MEVCUT 9.94) |
| 12 | The cupboard door in the shutter's place (a portcullis over the opening, sized to it) | ◐ opens DOWNWARD; two takes on `Docs/reports/third_look/` (D1, D2, or in code), waiting on the pick |
| 13 | Kitchen lamps over the counter with their light | ◐ the hook, the light's hang and four takes are in (`Docs/reports/lighting/` L1–L4); the piece waits on the pick |
| 14 | The see-through characters and dishes: the bodies rode the HUD group's fade at a middle alpha and the rail's dishes faded to 55%/40% — both solid now (`TycoonHud.Seats`: a body is whole or gone, a dish darkens instead of fading; the counter glass and the carried glass and tin at full alpha). The enclosed holes in the idle frames (34 faces) are the arm-to-body gaps, real; sheet at `Docs/reports/patron_holes/index.html`. **The waist (2026-09-21, the author's four pictures):** those very pockets, closed at import by `PatronArtPostprocessor` — every one of them, since "katı olması gerekiyor" — 4,384 frames, 0 pockets left, the PNGs untouched | ☑ |

## 4. The proposals (2026-09-21) — the author picks a direction, then the work is measured and built

**10. The beer scene from scratch.** What stands today: `TycoonServiceFlow.Tap` (1210 lines) stands one of three
font drawings (`bench_tap_single/arch/tee`, chosen by `TapLevel`) on a bench panel with the pint at a rest
measured off the spout, the pull by the pointer's height, the head settling on the glass, and a plaque of keys.
The room's tower is the author's `fx_tap_beer` at every count now, so the bench and the room disagree. Three
directions, one asset list each:
- A — *the tower at four times*: the room's own `tap_beer` redrawn at bench scale (one drawing, three handles;
  the second and third handle dead until the ladder opens them), the pint under the spout, the head read off the
  glass with no gauge, one key (SERVE IT). Assets: the tower at bench scale, three handle states, a drip tray.
- B — *the keg room*: kegs on the left carrying the beer's cellar plate (`v4_beer_*_c`), the tower centre, the tray
  right; a keg is tapped by dragging its line to a handle. Assets: three keg drawings, a line/coupler, the tower.
- C — *the counter in close-up*: the camera moves to the room's tap; nothing new is drawn, the room's tower is
  the font. Assets: none; the cost is code (the bench becomes a camera move and a hand).
Whichever it is, the assets come through PixelLab → `Tools/.../report.html` → the author's pick → `ship.py`, as
the bottles did; and the colour rule holds (the counter's finish, one accent on the handle in play).

**11. The shaker bench.** Three separate asks, in order of certainty: (a) *the panels ride the background*: the
lift ladder, the plaque and the keys move with `_pourSurface` (the slide) instead of standing on `_shakerPanel`;
only the shaker and the bottles stay in the hand's plane — a re-parenting, measured against the entrance slide
(`PlayBenchEntrance`). (b) *the mix bars*: today one blended gauge (`_shakerMixBar`); proposed: one bar per
pour in the tin, the bottle's own liquid colour, its NAME and its share as a number, 16 px tall, stacked left to
right in pour order, on the plaque's band. (c) *SERVE IT*: the key drawn again on the plate family in the one
accent, at the plaque's right end, wider than tall, the word alone; the lamps come off it. A mock of (b) and (c)
at 640×360 first, then code.

**12. The cupboard door.** The shutter is a roller over rows 65..241 of the counter's opening (176 art px tall,
`ShutterOpeningTopPx`); the sound is a wooden door now. Proposed: a single wooden panel the opening's exact size,
hung from its top and RISING into the counter to open (a portcullis), the roller's travel and rail kept
(`ShutterTravel`, `ShutterRail`). Assets: one panel drawing in the counter's finish family (PixelLab, three
candidates) — or drawn in code the way `BackBarArt` draws the wall, if the author prefers no generated art here.

**13. Kitchen lamps over the counter.** A new slot (`counter_lamps`, x 320, y at the counter's top edge) with a
fixture that starts in the room: a rail of three shades, each with the fixture light the room already has
(`lightR/G/B`, `lightIntensity`, `lightRadius`, glow at two thirds of the sprite). Assets: the rail drawing
(PixelLab, three candidates at the room's 640×360 scale). Measured question for the author: warm (the bar's
amber) or the counter's magenta tube?

**14. The see-through characters.** The sheet `Docs/reports/patron_holes/index.html` paints every enclosed
transparent hole in the idle frames magenta (34 faces; the largest: eastasianman 387 px, analyst 204, bilbao
175, clubgirl 169, canton 130). Proposed: `PatronArtPostprocessor` fills holes under a threshold (say 40 px)
with the nearest opaque colour at import, so the author's PNGs stay untouched and the big true gaps (an arm off
the hip) stay open; the author names any face whose gap must stay. The garnish dishes and the glass garnishes
have no holes and no soft alpha — if something on the rail shows the background, it is the dimmed dish
(`Seats.cs` alpha 0.85) and that is one number.
*Outcome (2026-09-21):* the author's four pictures showed exactly these pockets over the floor, so there is no
threshold — every enclosed transparent pixel is filled from its opaque neighbours' average at import
(`OnPostprocessTexture`, Resources/Patron only); the census over all 4,384 imported frames finds none left, and
the three seated drinkers' waists were looked at in play (r75).

## 5. The third look (2026-09-21) — the author's asks and picks

Shipped in the same commit as the hole fill:
- **Certificate v4** (`TycoonHud.Ladder`): the sheet alone over the dimmed room with CONTINUE under it — no
  blue plate, so it may stand taller (1024 wide, 576 at the least, 648 at most with two bands); paper grain,
  two rules, corner brackets, a watermark; bigger writing (16/24 px, a shadowed title over an amber rule); this
  rung's tiles and the NEXT rung's (features, bottles, recipes; dimmed) on dark plates, the bottles drawn FULL.
- **LOG** off the main screen (`TycoonHud.Chrome`; the sheet and the toggle stay in code).
- **The spoon's napkin under the tin**, and **no spoon over an empty tin** (the spoon dims) — `Shaker.cs`.

The author's picks on §4 (in the order they will be built, each measured in play before the next):
1. **Beer scene — A.** "Yeni bira musluğunun 4x kaliteli hali; sahne yine içki yapma sahnesine benzeyecek —
   arkaplan, tuşlar, butonlar." PixelLab candidates of the room's `fx_tap_beer` at bench scale (empty,
   label-less, in the pipeline's rules) → `Docs/reports/tap_tower/index.html` → the author picks → `Tap.cs`
   rebuilt around it; the bench keeps the cocktail bench's background, plaque and keys.
2. **Shaker bench — "Uygula."** (a) the plaque, keys and ladder re-parented under the slide; (b) one bar per
   pour, the bottle's colour, its name and its share; (c) SERVE IT on the plate family in the one accent.
3. **Cupboard door — DOWNWARD.** The panel hangs from the opening's top and drops INTO the counter's face to
   open (not a rising portcullis): the roller's travel and rail kept, the panel drawn in the counter's finish.
4. **Lighting language.** "Profesyonel bir oyun sanat tasarımı dokunuşu; belli tonlar, belli filtreler; tüm
   ışıklandırmalar ve yansımalar buna göre." A written token set first (key: warm amber; fill: cool night;
   accents: the neon's magenta and cyan; nothing else emits), every fixture light normalised to it, the
   counter lamps (§4.13) as the key light over the till, and one URP Volume built in code (colour adjustments,
   lift/gamma/gain, a vignette — modest) — a before/after sheet, then the look tests re-blessed after LOOKING.
   `DefaultVolumeProfile.asset` is the author's working file and is not edited; the volume is a scene object.
   **Built (GDD_MEVCUT 9.95):** the tokens, the snap, the grade on `LastCallVolume` (the game's own asset, not the
   author's default profile), the sheet. Open: the grade's strength (one place to turn), the lamp pick.
