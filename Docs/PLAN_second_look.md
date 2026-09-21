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
| 1 | Certificate v3: unlocks INSIDE the sheet; one accent; comfort in medals, service in hearts (as the top bar); opening animation (the sheet unrolls); confetti WITH sound (`cheer_sfx`, `level_up`); a preview GIF for the author | `TycoonHud.Ladder` | ☑ |
| 2 | A dev preset for every rung (0.5–5★) with the fittings, stock, book, seats and day of that level; the bench's KOŞU keys | `TycoonRun.DevPresetStars`, `TycoonHud.Chrome` | ☑ |
| 3 | The top bar's lit stars, medals and hearts wave every five seconds | `TycoonHud.Chrome.RefreshStanding` | ☑ |
| 4 | Greenery behind the furniture (the pothos was in front of the left table) | `DiegeticStage.PlaceFixtures` | ☑ |
| 5 | The shutter: the pink arrow gone; `cellar_open` / `cellar_close` re-made softer | `DiegeticStage.BuildShutSign`, `Tools/sfx_bank.py` | ☑ |
| 6 | Character animation at full speed under the slow clock; stopped only by the pause | `TycoonHud.Seats` (`AnimDelta`) | ☑ |
| 7 | The lift's rungs green → amber → red | `TycoonServiceFlow.Shaker` | ☑ |

## 2. Rules and data

| # | Item | Status |
|---|---|---|
| 8 | Mint and olives are GARNISHES asked for as extras (`Preparations.Mint`, `Preparations.Olives`), never recipe bands; the jars in the market carry no fill; the rail's jars are dishes like ice; Core refuses a garnish whose jar is not stocked; recipes.json ↔ RecipeCatalog under the parity test | ☐ (next) |
| 9 | The author's rim FRONT plates (`glass3d_*_rim_*_FRONT`) drawn over the drink: the whole ring in the decor, the near arc over the front crop | ☑ |

## 3. Proposals first (the design rule), then code on the author's pick

| # | Item | Status |
|---|---|---|
| 10 | The beer scene from scratch, with its assets (PixelLab → report → pick) | ☐ report |
| 11 | The shaker bench: panels riding the background; mix bars that name the spirit and its share; SERVE IT again | ☐ report |
| 12 | The cupboard door in the shutter's place (a portcullis over the opening, sized to it) | ☐ report |
| 13 | Kitchen lamps over the counter with their light | ☐ report |
| 14 | The see-through characters: 34 of 54 idle frames have enclosed holes; an import-time fill with a size threshold, shown per face for the author to accept | ☐ report |
