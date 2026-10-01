# Malibu Club: Vice / Miami sunset art direction for the Steam set

*Scope: the look of every store and library asset. Layout and logo placement are only touched on as hooks (§12). Numbers marked "measured" were computed in this session from the repo's own files.*

---

## 0. The brief in eight lines

1. **One world, one moment, one light direction.** Every asset is a different camera crop of the same Malibu Club at **19:17–19:21 ("the sun on the towers")**. That hour comes from the game's own `Assets/Resources/Data/sky_cycle.json` (key `t=0.16`), so the store sky is literally the game's window.
2. **Inside, the bar looks west over the bay** at a sinking sun behind a downtown skyline. **Outside, the Deco front faces the sunset** and catches its gold, under the pastel east sky with its pink band. Both views are physically true for Miami Beach, and together they give the two Miami looks: fire and pastel.
3. **It reads as a cocktail bar first.** Every frame with Roxy carries at least 3 of: counter edge with brass rail, lit back-bar bottles, a coupe in hand, an ID card crossing the counter, a shaker.
4. **Synthwave is seasoning, not the dish.** No perspective grid, no chrome car, no VHS/CRT effects. The sun is the game's own plain disc, and any "stripes" come only from 2–3 real cloud streaks.
5. **Two neon hues, never more:** Magenta and Cyan, the two the logo is made of. No green or yellow neon.
6. **Warm light, cool shadow.** SUN and KEY are warm (Amber, ViceRed, Cream). Every shadow plane leans to ClubBlue or Night.
7. **Roxy: head in the violet, shoulders in the fire.** Her hair and suit are night-value and her skin is horizon-value (measured, §9). So the face goes over a dark zone, the hair silhouette over the hot bands, and an Amber[4] rim on the sun side with a Cyan[4] rim on the room side.
8. **Real pixel art, checked by machine:** one palette (the 55 UITheme colours plus the logo's glow blends), integer scale only, and an asset that survives a NEAREST down/up round trip. The current set fails this: 170,304 distinct colours in the main capsule (§13).

---

## 1. What we are, and what we are not

The game's own art bible defines the house: *"Brass & marble, shift light … an expensive, calm cocktail room … a vice sunset burning through the windows during service … never gritty, never cute-retro, never a showroom"* (`Docs/GDD/14_art_bible.md` §1). Its Miami *"survives as the view, not the room"* (§7). The store art should sell **that** room with the sunset pouring into it. A generic pink neon club is not the game.

### 1a. Genre signal budget (per asset that shows the bar)

| Tier | Elements | Rule |
|---|---|---|
| **Must, at least 3** | counter front edge + brass rail · back-bar bottles lit from behind · a coupe (hero glass) · an ID card mid-hand-off · shaker | They must read at the asset's display size (§7d) |
| **Should** | guests on stools · garnish tray (lime, cherry, mint) · ice bucket · the one "house" neon sign (pictogram) | Rhythm and depth. Never the focal point |
| **Atmosphere (allowed)** | the sunset sky · bay and skyline · palms · Deco trims · birds (M-shapes, as in game) | Lives in the window and the background plane only |
| **Banned** | perspective laser grid · chrome or 3D text · sports car · VHS tracking bars, scanlines, chromatic aberration, CRT curvature · triangles and geometric neon frames · green/yellow/orange neon · a sun with more than 3 stripes · any readable words other than the logo | Each one pulls the read toward "synthwave music video" or "racing game" |

**Why the bans matter:** the OutRun / synthwave code is *"yellow, orange and pink neon sunsets (often with horizontal cuts) … palm tree silhouettes … digital grids and fast cars"* ([TV Tropes: Synthwave](https://tvtropes.org/pmwiki/pmwiki.php/Main/Synthwave), [newretro.net](https://newretro.net/blogs/main/what-is-outrun-the-brief-history-of-outrun-genre)). We keep the sunset and the palms (they are Miami) and drop the cuts, grid and car (they are the genre we must not be mistaken for). VHS and CRT shaders would also break the pixel grid the user asked for.

### 1b. The game-tied twist on the 80s look

- **Unread guests are silhouettes.** Backlit by the window, the guests on the stools are dark shapes. One guest is half-lit and holding out an ID card. This is the game's hidden-information rule ("the order lives behind the card") turned into lighting.
- **The sunset is the clock of the shift.** The game's light model makes SERVICE the sunset state (art bible §4). The key art freezes the moment the doors open, which is also why event art can move along the clock (§2c).

---

## 2. One world, one moment, one light direction

### 2a. The moment
Take the store canon from `sky_cycle.json` key `t=0.16` "19:17 the sun on the towers". Its sky stops are `ClubBlue1, Magenta1, Magenta3, ViceRed3, Amber2`, the room ambient is `Night4` (cool), and the window glow is `ViceRed4`. The game's own renderer was run for this pass (`Tools/window_sky.py preview`). The 19:21 frame shows exactly the wanted state: the sun just above the skyline, a full magenta band, a coral-gold horizon, birds, and thin dark cloud streaks. **The game's sun is a plain disc with a banded halo and no stripes.** The striped synthwave sun in the current capsules contradicts the game.

### 2b. Geography (it makes the art feel real)
- Ocean Drive faces the Atlantic, which is **east**. Miami Beach sunsets are watched **west across Biscayne Bay** toward the downtown skyline ([miamiandbeaches.com: sunset spots](https://www.miamiandbeaches.com/travel-interests/romantic-trip/best-places-to-watch-the-sunset-in-miami)). The game's json already says "noon over the bay".
- **Interior master (the store canon):** the bar's left-wall shopfront looks **west**: sun, skyline, bay. This matches the as-built room, where the window is a full-height shopfront down the LEFT wall (art bible §5b "AS BUILT"). **The sun is always screen-left in every asset.**
- **Exterior master (events, page background):** the club's Deco front faces west into the sunset, so it is lit gold. The sky above it is the **east** sky: a blue Earth's-shadow band at the horizon with the pink **Belt of Venus** above ([EarthSky](https://earthsky.org/tonight/earth-shadow-belt-of-venus-in-east-after-sunset/), [Wikipedia: Earth's shadow](https://en.wikipedia.org/wiki/Twilight_wedge)). This is the pastel, teal-and-pink Miami Vice postcard without a single neon tube.

### 2c. The clock as the only variable (event assets)

| Event mood | `sky_cycle.json` key | Use |
|---|---|---|
| Launch / summer | 18:00 golden hour | warmest, most "Vice City loading screen" |
| **Store canon** | **19:17 the sun on the towers** | every store and library capsule |
| Festival / Next Fest | 20:24 the pink band | sun gone, magenta peak, neon fully on |
| Patch notes | 21:31 blue hour | calm, ClubBlue-dominant |
| Story / LAST CALL / Halloween | 22:48 night | palms on dark, one amber pool (art bible §4) |

Camera, room, cast and palette stay fixed. Only the hour moves, so the set stays one composition.

---

## 3. The sky: gradient structure and ramp mapping

### 3a. Interior (west) sky, top to horizon

The game's five stops, extended upward into Night so the logo always has a dark bed, and made **monotonic in value** (the json's ViceRed[3], L 0.198, dips below Magenta[3], L 0.252, which reads as a dirty band at key-art scale).

| Band | Share of sky height | Token | Hex | Rel. luminance (measured) | Job |
|---|---|---|---|---|---|
| S0 zenith | 0–14 % | Night[1] | `#1A1023` | 0.007 | logo bed (vertical capsules) |
| S1 | 14–26 % | Night[2] | `#241830` | 0.012 | logo bed, stars allowed (≤ 0.22 luma, as in game) |
| S2 | 26–38 % | ClubBlue[1] | `#1F2E66` | 0.032 | **the cool**: makes the warm read as light, not paint |
| S3 | 38–48 % | Night[4] | `#4A3160` | 0.045 | violet bridge (blue to magenta without mud) |
| S4 | 48–58 % | Magenta[1] | `#8F2464` | 0.080 | Roxy's face zone (§9) |
| S5 | 58–66 % | Magenta[2] | `#C23283` | 0.154 | narrow seam band |
| S6 | 66–76 % | Magenta[3] | `#E84DA6` | 0.252 | **the pink band** |
| S7 | 76–86 % | ViceRed[4] | `#F27D8A` | 0.354 | coral bridge (hue 326° to 36° through 353°) |
| S8 | 86–95 % | Amber[3] | `#E8A33D` | 0.437 | the gold layer |
| S9 | 95–100 % | Amber[4] | `#F5C97B` | 0.626 | horizon strip behind the skyline, **only within about 4 sun-radii of the sun** (falls back to S8 elsewhere) |

**Structure rules**
- **About 55 % cool, 25 % magenta, 20 % warm.** Real sunsets compress the hot colours into a thin layer at the horizon. This also keeps saturated pixels within budget (§13) and gives the logo a big dark field.
- **Bands get thinner toward the horizon** (S7–S9 are each ≤ 10 %). Slynyrd's landscape method starts from solid horizontal bars and conveys depth "primarily by color choice" ([Pixelblog 62](https://www.slynyrd.com/blog/2026/5/27/pixelblog-62-landscape-backgrounds), via search summary).
- **Dither only at seams:** 4×4 Bayer, the same matrix the in-game `WindowSky` uses, across a seam about ⅓ of the band height. Flat runs everywhere else, and never dither a material (art bible §2). In the very tall vertical and library capsules, S0–S2 may stretch. The warm bands keep their absolute pixel height so the horizon stays a thin hot line.
- **ViceRed is required in the sky** even though it is not one of the brief's "headline" ramps. Without it, magenta into amber goes brown. ViceRed[3] `#D9455C` is kept for **cloud undersides** (§3c), not for a band.

### 3b. The sun
Use the game's recipe (`sky_cycle.json → sun`) at key-art scale:
- Core Cream[4] `#F2E8D5` up high, sinking to Amber[3] near the horizon. Rim Amber[4] to ViceRed[3].
- Halo: banded Amber[4] at about 3.7× the sun radius (json `haloRadius 26` / `radius 7`), drawn as **flat alpha bands 150/78/34/12**, the same four bands as the logo's glow, so the sun and the sign light the same way.
- The disc sits **about 1 diameter above the skyline** and is partly behind one tower ("on the towers").
- **No palm across the sun disc.** It is the cliché, and it kills the disc's silhouette. Palms frame the sun from the side.

### 3c. Clouds, birds, stars
- **2–3 thin altostratus streaks**, 1–2 art-px tall and 20–60 px long, crossing the upper half of the sun and the magenta band. Undersides (sun side) are ViceRed[3]/Amber[4]; bodies Magenta[1]/Night[4]. Where a streak crosses the disc it cuts it: this is the only "striped sun" allowed, and it is physically real.
- Birds: the game's M-shaped flocks in Night[1], 5–9 birds, only in the magenta and violet bands.
- Stars: S0–S1 only, sparse, single-pixel Cream[3]. None in the store canon's warm half.

### 3d. Exterior (east) sky, for the Deco front

| Band (top to horizon) | Token | Note |
|---|---|---|
| zenith | ClubBlue[1] → ClubBlue[2] | cooler and lighter than the west zenith |
| upper | ClubBlue[3] `#4467CC` | |
| **Belt of Venus** | Magenta[4] `#FF7DC6` → ViceRed[4] `#F27D8A` | the pink pastel band, 10–15 % of sky |
| **Earth's shadow** | ClubBlue[2] `#2E4699` (lifted with ClubBlue[3] seam) | blue-grey band sitting on the sea |
| sea (Atlantic) | ClubBlue[1]–[2] with Magenta[4] glints | **not Cyan** (§4) |

---

## 4. Sea, bay and horizon

- **The bay is the sky mirrored, one ramp step darker and compressed:** far water ViceRed[2]/Magenta[2], middle Magenta[1], near Night[3] to Night[2].
- **Glitter path under the sun:** horizontal dashes 1 art-px tall and 2–8 px long, Cream[4] at the core and Amber[4] then Amber[3] at the edges. They widen and break up toward the viewer. Never a solid column.
- **Skyline:** Night[1] silhouettes (the game's own `window_city`), with sparse single-pixel Amber[3] lit windows and one or two ViceRed[4] aviation dots. **Height ≤ 18 % of the window**, so it never competes with Roxy's head. Its reflection is short vertical Night[1] smears with the windows repeated as 1×2 Amber[3] dashes.
- **Waterline:** one Night[1] row (the city's base), then a single Amber[4] row only beneath the sun.
- **Liberty flagged:** the game's window shows rooftops below the skyline, not water. Adding a bay strip between the skyline and the near ground is consistent with the json's "over the bay", but it is a store-art addition. Keep it ≤ 15 % of the window height.
- **Cyan is light, not paint.** Water is ClubBlue and Magenta. Cyan is reserved for neon, the logo's coupe and the "Bubbly" drink coding, so it stays a signal (art bible §3: Cyan owns selection and information).

---

## 5. Palms

- Pure silhouette: Night[1] against the sky, Night[0] for the nearest palm (two depth layers with no outlines needed). Use the game's `Scene/window_palm_l/r` (+ `_crown`) sprites.
- **2 near palms** frame the window edges, with crowns cropped by the frame top and trunks leaning out of the frame. **1–2 far palms** are small, on the skyline.
- A 1-px Magenta[2] edge appears on frond tips **only** where a frond overlaps the S6–S8 hot bands (light wrap). Elsewhere: no edge.
- **Never a green or neon palm.** The current exterior panel and main capsule both have a green neon palm, which breaks the 2-neon-hue rule.

---

## 6. Art Deco and neon signage

### 6a. Tropical Deco motifs, as pixel recipes
Hallmarks: *"symmetry, ziggurat (stepped) rooflines, decorative friezes, eyebrow window overhangs, relief facades, porthole windows and neon … terrazzo … glass block … nautical motifs reminiscent of ocean liners"* ([miamiandbeaches.com](https://www.miamiandbeaches.com/hotels/art-deco-architecture-boutique-hotels), [FrontRowSociety](https://frontrowsociety.com/discoveries/art-and-culture/100-years-of-art-deco-an-architecture-tour-in-miami-beach/)).

| Motif | Pixel recipe | Where it appears |
|---|---|---|
| **Eyebrows** (window shades) | 2-px ledge of the lit facade colour plus a **3-px Night[3] shadow band** under it. The eyebrow reads by its shadow | exterior master |
| **Speed lines** (triple stripe) | three 1-px bands, 1 px apart | exterior parapet · **in brass (Amber[3]) on the counter front** · ID-card header |
| **Ziggurat / stepped top** | 3–4 steps, symmetric | exterior tower · **top of the back-bar shelf frame** |
| **Vertical fin (spire)** | a tall blade above the entrance carrying the neon | exterior. **Carries the logo's cyan coupe pictogram, no words** |
| **Porthole** | a round window with a 1-px ClubBlue[2] frame | exterior · the cellar door, optional |
| **Glass block** | 4×4 tile grid, ClubBlue[3]/[4], each tile with one Cream[4] pixel | exterior, side of entrance |
| **Rounded corners / bullnose** | streamline curve | the **counter end** Roxy leans on |
| Terrazzo, chrome rails | sparse 1-px chips; Graphite rail with one Cream[4] glint | exterior porch only. The interior floor stays the game's espresso planks |

### 6b. The pastels are light, not loud paint
Leonard Horowitz built the Miami Beach pastel palette *"on the basis of sunset, sunrise, the summer and winter oceans and the sand"* ([MDPL: A Pastel Paradise](https://mdpl.org/news/2020/10/a-pastel-paradise/), [WLRN](https://www.wlrn.org/culture/2013-11-16/meet-the-man-behind-all-those-south-beach-pastels)). That is our ramps' **[4] steps**:

| Horowitz source | Our pastel | Facade role |
|---|---|---|
| sand | Cream[4] `#F2E8D5` / Amber[4] `#F5C97B` | wall field (lit side at 19:17 = Amber[4]) |
| sunset | Magenta[4] `#FF7DC6` / ViceRed[4] `#F27D8A` | eyebrows, speed lines, trim |
| summer ocean | Cyan[4] `#7DF0E3` | one accent per building at most |
| winter ocean | ClubBlue[4] `#6E93F0` | window glass, glass block |
| shadow side | ClubBlue[2] / Night[4] | every plane facing away from the sun |

Keep it to **one building, two accent colours.** A row of hotels (Colony, Breakwater, Carlyle) is postcard material ([Miami New Times: best neon signs](https://www.miaminewtimes.com/arts-culture/the-ten-best-neon-signs-in-miami-8320246/), [Wikipedia: Ocean Drive](https://en.wikipedia.org/wiki/Ocean_Drive_(South_Beach))), but real hotel names and real sign shapes stay out (art bible §10: no real brands). The *grammar* (vertical blade sign, a neon outline along the parapet) is free to use.

### 6c. Neon construction (identical to the logo, so the logo belongs)
- Tube = **1-px Cream[4] core + Magenta[4] or Cyan[4] tube + [3] edge**, then the **four flat alpha glow bands 150/78/34/12**. No soft halo, no blur. A real tube's core over-exposes to white and the halo carries the colour ([neonsignsnow.com](https://www.neonsignsnow.com/guides/how-to-draw-a-neon-sign-lights-on-paper-marker)).
- **Two hues only: Magenta and Cyan** (art bible §10: "more than 2 neon hues in one composition" is a DON'T).
- **Vocabulary:** pictograms only (coupe glass, flamingo, wave, palm outline, crescent moon, shaker, a heart or star from the game's own icons). **No words except the logo.**
- **Neon count per frame:** interior = the counter's magenta tube plus **one** pictogram sign. Exterior = the fin pictogram plus one parapet outline. The logo is the loudest neon in every asset, and nothing else may be brighter or bigger.

---

## 7. The bar: the genre kit

### 7a. Counter (foreground, every interior crop)
- Use **Tier 3 "navy marble & brass"**, the hero look the player upgrades toward (art bible §6). Top field ClubBlue[1], mottling ClubBlue[0]/[2], veins Cream[3] with sparse Amber[3] gold, polished edge ClubBlue[2].
- **Brass rail** in Amber[2]/[3] with **one** Cream[4] highlight, and the brass **speed lines** on the front.
- **The as-built magenta tube** along the top edge: NEON token, the one magenta that is allowed near Roxy, and **below** her torso, never behind it.
- **Key-art liberty:** a 1–2 px Amber[4]/Magenta[3] band along the polished top edge (the sunset caught in the marble). Room plates must not paint reflections (art bible §7b), but the key art *is* the lit frame. Keep it to the edge.

### 7b. Back bar (right-hand framing wall)
- Shelves are Oak (Amber ramp) or the as-built Magenta[3]-framed cellar. **Bottles are the game's v4 sandwich sprites.**
- **"Glowing" = backlit stained glass:** one KEY strip (Amber[4] to Cream[4]) under each shelf. Each liquid shows its transmission colour at [3]/[4], with the glass body at [1]/[2]. Three shelves, about 6–9 bottles per shelf in the main capsule.
- **Never place the back bar directly behind Roxy's torso** (hue collision, §9). It sits to her right, cropped by the frame edge, as the stage's "wing".

### 7c. Props
| Prop | Spec |
|---|---|
| **Coupe (hero glass, in Roxy's hand)** | Magenta[3] body / Magenta[4] surface, dotted Cream[4] sugar rim, one garnish (ViceRed[3] cherry or Lime[3] wheel). Held at shoulder height, **on the sun side**, so the glass rim catches an Amber[4] line |
| **Highball (secondary)** | Cyan bubbles. "Bubbly = Cyan" is the game's own type coding (art bible §3) |
| **Shaker** | Graphite ramp, one Cream[4] glint on the room side, one Amber[4] line on the sun side. On the counter near Roxy, or mid-shake in the GIFs |
| **ID card (the mechanic)** | ISO ID-1 proportion 1.586:1. Cream[4] body, ClubBlue[2] header with speed lines, a photo square, glyph-lines in Cream[2] (**nothing readable**). Pushed across the counter by the one half-lit guest, angled ≤ 15°. Optional story nod: a small flag on the card (the H6 altered card) |
| **Guests** | 2–4 on stools, backlit silhouettes in Night[1]/[2] with a 1-px ViceRed[4]/Amber[4] rim from the window. **One** guest half-lit (KEY from above) holding the card |

### 7d. Minimum on-screen sizes (proposal, check at each asset's display size)
| Element | Main 1232×706 | Header 920×430 | Small 462×174 |
|---|---|---|---|
| Roxy's head (crown to chin) | ≥ 90 px | ≥ 70 px | ≥ 48 px, head and shoulders only |
| Coupe | ≥ 48 px tall | ≥ 36 px | optional |
| ID card | ≥ 56 px wide | ≥ 44 px | omit (illegible) |
| At 120×45 (Steam's auto thumb) | — | — | only the logo plus a warm-bottom/dark-top value split survive |

Capsule guidance: *"one mood, one character, one focal point"*, logo legible at 120×45, value contrast over saturation, test in grayscale ([presskit.gg](https://presskit.gg/field-guides/steam-capsule-art-guide), [steampageanalyzer.com](https://www.steampageanalyzer.com/blog/steam-capsule-design-guide)).

---

## 8. Lighting

Mapped onto the game's light language (art bible §4b: KEY / FILL / NEON / SCREEN / SUN), so the store art is lit by the same rules as the game.

| Light | Token / colour | Direction | Hits | Rule |
|---|---|---|---|---|
| **SUN** (key light from the window) | core Cream[4], body Amber[4], falloff ViceRed[4] → ViceRed[3] | **screen-left**, low, behind-side | rims on hair, shoulders, glass rims, guests' silhouettes; a warm shaft across the counter | the strongest light. It **backlights**, it does not front-light |
| **KEY** (house downlights) | Amber[4] → Cream[4] (game KEY `#F8D9A2` snapped onto the ramps) | above, slightly in front | Roxy's face and hands, the hero glass, the card, the counter pool | the face's light. **Only one amber pool** (art bible §10: no second amber key) |
| **NEON fill** | Cyan[4] (counter tube underlight) · Magenta[4] (one sign) | below / room side (screen-right) | Roxy's room-side edge, underside of glasses, bottle bottoms | 1-px rims and underlights only, never a wash |
| **FILL** (ambient) | ClubBlue[2] / Night[4] (json 19:17 ambient = Night4) | everywhere | every shadow plane | shadows lean cool, **never** black or grey |
| SCREEN | — | — | — | not used in key art |

**The Vice formula:** warm light, cool shadow. Every lit plane moves toward hues 350°–38° (ViceRed, Amber); every shadow plane toward 227°–272° (ClubBlue, Night). This complementary split is what makes it read as "Miami at dusk" rather than "purple room".

**Value architecture, top to bottom:** dark top (logo), mid-dark middle (Roxy's face over Magenta[1], room in FILL), **bright horizon line** (S8–S9 plus the sun), dark foreground (counter). The brightest non-logo pixel in the frame is the sun core. The second brightest is the glint on the hero glass.

---

## 9. Placing Roxy against the sunset (measured)

### 9a. Her values (sampled from `Patron/hostess/idle/idle_00.png`)
| Part | Colours | Rel. luminance |
|---|---|---|
| Hair | `#1B0512` `#5F101D` `#851A22` | 0.004 – 0.058 |
| Jumpsuit | `#791139` `#510A27` `#420A23` | 0.015 – 0.048 |
| Lapels | `#302C37` `#11080C` | 0.003 – 0.027 |
| Skin | `#F8A368` `#CF6B42` `#AB4A2D` | 0.14 – 0.47 |
| Gold (hoops, belt) | `#E9A023` `#FCED5F` | 0.43 – 0.82 |

### 9b. Contrast against the sky bands (WCAG-style ratio, measured)
| | Night[1] | Night[4] | ClubBlue[1] | Magenta[1] | Magenta[2] | Magenta[3] | ViceRed[4] | Amber[3] | Amber[4] |
|---|---|---|---|---|---|---|---|---|---|
| hair (mid) | 1.38 | 1.20 | 1.04 | 1.65 | 2.58 | 3.83 | 5.12 | 6.17 | 8.57 |
| suit | 1.71 | 1.03 | 1.19 | 1.33 | 2.09 | 3.10 | ~4.2 | 4.99 | 6.93 |
| skin | **9.13** | **5.49** | **6.36** | **4.00** | 2.56 | 1.73 | 1.29 | **1.07** | 1.30 |

**Reading:** her hair and suit **vanish** against the dark upper sky and the room (about 1.0–1.7). Her face **vanishes** against the hot horizon and the sun (about 1.1–1.7). That gives a split rule:

### 9c. Placement rules
1. **The horizon at her collarbone or shoulder line.** Face and crown sit in S3–S4 (Night[4]/Magenta[1], skin contrast 4.0–5.5). The hair mass falling over her shoulders drops into S6–S9 (3.8–8.6), so the silhouette pops where the hair is biggest.
2. **The sun beside her head, never behind her face:** on the window side, 1–2 head-widths away, at chin height. Its banded halo laps the hair edge and becomes a motivated rim.
3. **Rims:**
   - Sun side: **1 art-px Amber[4]** (8.6:1 vs hair) on hair crown, shoulder and arm.
   - Room side: **1 art-px Cyan[4]** (9.8:1) from the counter tube, on the jaw, arm and hip edge.
   - Never both rims on one edge.
   - **Magenta[4] is the weakest rim on her** (5.7:1, and the same hue family as her hair and suit). The art bible already prefers *"a warm brass rim (Amber[4]) where neon fights the material"* (§8), and her burgundy suit is that case.
4. **The suit's hue (337°) equals Magenta's (326°).** Keep Magenta[3] shelf frames, magenta neon and magenta sky out from behind her torso edge. Put marble (ClubBlue[1]), backlit amber bottles or Cream plaster behind her (suit vs Cream[3] ≈ 8.5:1).
5. **Face lit by KEY** (from above and in front) so it is the most readable warm-mid area. Eyes to camera. The head turns slightly toward the window and the logo, so the gaze and the light share a direction.
6. **Two or three Cream[4] glints only:** one hoop, the belt buckle, the glass rim. They echo the sun.
7. **Pose is glamour, not pin-up:** a welcoming host presenting the coupe or handing back the ID. Patrick Nagel's flat, high-contrast, Deco-descended portraiture ([Wikipedia](https://en.wikipedia.org/wiki/Patrick_Nagel)) is a good reference for elegance with few values.
8. Her skin keeps her own sprite colours (identity). Light is added only through the rim tokens and the KEY step.

General rim-light practice behind this: the rim must be brighter than anything near it to read, and the background is kept recessive ([CLIP STUDIO TIPS: backlit characters](https://tips.clip-studio.com/en-us/articles/10930), [2D Will Never Die](https://2dwillneverdie.com/tutorial/picking-the-best-colors-for-your-sprite/)).

### 9d. What this fixes in the current main capsule (observed)
- The shelves and wall behind Roxy are magenta, the same hue as her suit, so the torso edge melts.
- The sunset is far left and never touches her: no rim, no shared light.
- The striped synthwave sun and the green neon palm push the read toward synthwave.
- The room is a generic pink bar, not the game's brass-and-marble room with a navy counter.
- Roxy herself (face, gaze, the coupe at shoulder height, sitting on the right third) is already right. Keep her.

---

## 10. Ramp → role map (the master table)

| Element | Ramp / steps | Notes |
|---|---|---|
| Zenith, logo bed | Night[1], Night[2] | logo magenta 5.3:1, logo cyan 8.9:1 on Night[1] (measured) |
| Upper sky (the cool) | ClubBlue[1], Night[4] | |
| Pink band | Magenta[1] → [2] → [3] | Magenta[1] is Roxy's face zone |
| Coral bridge | ViceRed[4] (bands), ViceRed[3] (cloud undersides) | |
| Horizon gold | Amber[3], Amber[4] near the sun | |
| Sun | Cream[4] core / Amber[3] low core / Amber[4] rim / banded Amber[4] halo | same 150/78/34/12 bands as the logo |
| Bay water | ViceRed[2], Magenta[2] → Magenta[1] → Night[3] → Night[2] | glitter Cream[4]/Amber[4]/Amber[3] |
| Skyline | Night[1], windows Amber[3] (1 px), beacons ViceRed[4] | ≤ 18 % of the window height |
| Palms | Night[1] far, Night[0] near; Magenta[2] tip edge on hot bands only | never green |
| Deco facade, lit | Cream[4] / Amber[4] | |
| Deco facade, shadow | ClubBlue[2] / Night[4]; eyebrow shadow Night[3] | |
| Deco pastel trims | Magenta[4], ViceRed[4], Cyan[4] (one), ClubBlue[4] | two per building |
| East sky (exterior) | ClubBlue[1→3], Belt of Venus Magenta[4]/ViceRed[4], shadow ClubBlue[2] | |
| Neon tubes | Cream[4] core + Magenta[4]/[3] or Cyan[4]/[3] + 4 alpha bands | two hues max |
| Counter (Tier 3) | ClubBlue[0–2], veins Cream[3] + Amber[3] | |
| Brass rail, speed lines | Amber[2]/[3] + one Cream[4] | |
| Counter tube | Magenta[3]/[4] | below Roxy's torso only |
| Back bar | Oak = Amber[0–3]; KEY strip Amber[4]/Cream[4]; liquids at [3]/[4] | v4 bottle sprites |
| Hero coupe | Magenta[3]/[4], Cream[4] sugar rim | |
| Bubbly drink | Cyan bubbles | game's type coding |
| Shaker | Graphite[1–4] + Cream[4] glint | |
| ID card | Cream[4] body, ClubBlue[2] header, Cream[2] glyph lines | |
| Guests | Night[1]/[2] silhouettes, ViceRed[4]/Amber[4] window rim | one half-lit by KEY |
| **Roxy rim, sun side** | **Amber[4]** | |
| **Roxy rim, room side** | **Cyan[4]** | |
| All shadows | ClubBlue[1]/[2], Night[3]/[4] | never black, never grey |

---

## 11. Mood references: take and leave

| Reference | Take | Leave |
|---|---|---|
| **GTA: Vice City** loading screens and key art (Stephen Bliss painted the Vice City artwork; *"my favorite gta art style, location and era"*: [X/@iamstephenbliss](https://x.com/iamstephenbliss/status/1762859072871632927), [IBTimes](https://www.ibtimes.co.uk/grand-theft-auto-artist-stephen-bliss-discusses-his-15-years-rockstar-games-1599910)) | one confident character, three-quarter, against a pastel sunset waterfront with palms ([gtabase artworks](https://www.gtabase.com/gta-vice-city/artworks/)); flat cel-shaded value economy | the **comic-panel grid box art**, the GTA lettering, purple suits and guns (*from memory: the grid is Rockstar's trade dress*) |
| **Miami Vice** titles | the **teal + flamingo-pink + linen-white + near-black** pairing ([designbycurio](https://designbycurio.com/learn/miami-vice-pastel-teal-1984)) = our Cyan + Magenta + Cream + Night; flamingos, Deco, water ([Miami Vice wiki: opening](https://miamivice.fandom.com/wiki/Opening_Sequence)) | speedboats, cop iconography |
| **Hotline Miami** | two-neon discipline, hard flat colour; its cyan `#2EFFFF` / pink `#FE6DBC` ([COLOURlovers](https://www.colourlovers.com/palette/3173336/Hotline_Miami)) sit near our Cyan[4]/Magenta[4], ours being softer, which keeps us distinct | violence, psychedelic wobble, the top-down view |
| **Far Cry 3: Blood Dragon** | the VHS-cover poster hierarchy (one hero, a huge sky, the title on top) ([Signalnoise](https://blog.signalnoise.com/far-cry-3-blood-dragon-art/), [Game Informer](https://gameinformer.com/b/features/archive/2013/07/26/the-80s-strike-back-the-complete-story-behind-blood-dragon.aspx)) | chrome letters, VHS filter, lasers, the grid |
| **Katana ZERO** | pixel-art neon noir: a dark world where neon is the accent, with a tightly controlled palette ([80.lv](https://80.lv/articles/katana-zero-pixel-art-platformerslasher)) | chromatic aberration and VHS shaders (they break the grid) |
| **OutRun** | low horizon, palms in rhythm, sunset as the destination ([Wikipedia: Synthwave](https://en.wikipedia.org/wiki/Synthwave)) | striped sun, grid, car |
| *Genre pros (from memory):* **VA-11 Hall-A**, **Coffee Talk** | the bar seen across the counter, a warm interior, the bartender waist-up as host | cyberpunk grime (VA-11), cosy-café softness (Coffee Talk) |

---

## 12. Hooks for the layout pass (not the layout itself)

- **Logo always over S0–S2 (Night/ClubBlue).** Measured, the logo's Magenta[3] tube on Magenta[2] is 1.48:1 and on Amber[2] is 1.11:1, so it disappears. Never over the warm half.
- **Sun left, Roxy right, back bar at the far right, counter across the bottom** in every interior crop. The main and header logo goes top-left over the sky. The vertical and library logo goes top-centre over S0–S1.
- **Eye path:** logo → Roxy's face → hero coupe → ID card → sun.
- **Pixel density:** one art-pixel ≈ 2 screen px as Steam displays each asset: ×2 for the capsules and page background, ×4 for the 3840×1240 hero. A 960×310 hero master at ×2 is also exactly the 1920×622 event header (one master, two assets).
- The library hero carries no text or logo (*Steamworks library-asset rule, from memory*).

---

## 13. Pixel discipline and QA gates (proposed, with today's measured values)

| Gate | Target | Main 1232×706 now | Header now | Library 600×900 now | Small now |
|---|---|---|---|---|---|
| Distinct colours | ≤ 64 + logo glow blends | **170,304** (PNG) | 88,058* | 92,364* | 32,085* |
| Pixels with S > 0.4 (V > 0.25) | ≤ 40 % | 61.8 % | 56.9 % | **89.5 %** | 41.8 % |
| Largest single hue | ≤ 20 % | magenta 22.8 % | 19.0 % | **55.7 %** | 15.0 % |
| Neon hues | 2 (Magenta, Cyan) | 3 (green palm) | 3 | — | — |

\*JPG inflates the colour count, but the PNG main capsule alone shows the set is not palette-locked.

Further gates:
- **Integer round-trip:** downscaling by the asset's factor with NEAREST and scaling back is byte-identical. This proves one pixel grid.
- **No anti-aliasing, binary alpha,** outlines in the darkest step of the object's own ramp, no pure black or white (art bible §2–3).
- **Grayscale at 120×45:** the logo and Roxy's face are still separable.
- Generated (Nano Banana / PixelLab) output is **palette-snapped and grid-snapped** before it is judged (`Tools/steam_page/pixelsnap.py`). The logo is never generated.

---

## 14. Risks (not legal advice)

- **"Malibu Club" is a nightclub property in GTA: Vice City** (Vice Point, bought for $120,000) ([gtabase](https://www.gtabase.com/gta-vice-city/properties/malibu-club), [GTA Wiki](https://gta.fandom.com/wiki/Malibu_Club_(3D_Universe))). With a Vice City theme on top, any echo of Rockstar's trade dress (comic grid, Pricedown, the pink "Vice City" script, Tommy-style hero) raises the derivative/confusion risk. Keep Vice City as **mood only**.
- **Malibu is also Pernod Ricard's coconut rum:** a white bottle with palm trees and sunset ([The Spirits Business](https://www.thespiritsbusiness.com/2019/06/malibu-goes-global-with-contemporary-redesign/), [DesignBro](https://designbro.com/blog/inspiration/malibu-logo-sun-beach-palm-trees-fun/)). **No white bottle with a palm/sunset label** anywhere on the back bar, and no rum bottle given hero position.
- Real hotel names and signs (Colony, Breakwater, Carlyle) only as grammar, never copied.

---

### Sources

- GTA / Bliss: [Dexerto](https://www.dexerto.com/gta/ex-rockstar-artist-adds-to-gta-6-hype-with-vice-city-tease-1328800/) · [X/@iamstephenbliss](https://x.com/iamstephenbliss/status/1762859072871632927) · [IBTimes](https://www.ibtimes.co.uk/grand-theft-auto-artist-stephen-bliss-discusses-his-15-years-rockstar-games-1599910) · [gtabase artworks](https://www.gtabase.com/gta-vice-city/artworks/) · [gtabase Malibu Club](https://www.gtabase.com/gta-vice-city/properties/malibu-club) · [GTA Wiki Malibu Club](https://gta.fandom.com/wiki/Malibu_Club_(3D_Universe)) · [ResetEra on GTA III box art](https://www.resetera.com/threads/the-story-of-how-grand-theft-auto-iiis-iconic-box-art-came-to-be-is-so-fascinating.153792/)
- Miami / Deco: [MDPL: A Pastel Paradise](https://mdpl.org/news/2020/10/a-pastel-paradise/) · [WLRN: Horowitz](https://www.wlrn.org/culture/2013-11-16/meet-the-man-behind-all-those-south-beach-pastels) · [miamiandbeaches: Art Deco hotels](https://www.miamiandbeaches.com/hotels/art-deco-architecture-boutique-hotels) · [FrontRowSociety](https://frontrowsociety.com/discoveries/art-and-culture/100-years-of-art-deco-an-architecture-tour-in-miami-beach/) · [Miami New Times: neon signs](https://www.miaminewtimes.com/arts-culture/the-ten-best-neon-signs-in-miami-8320246/) · [Wikipedia: Ocean Drive](https://en.wikipedia.org/wiki/Ocean_Drive_(South_Beach)) · [miamiandbeaches: sunset spots](https://www.miamiandbeaches.com/travel-interests/romantic-trip/best-places-to-watch-the-sunset-in-miami)
- Sky: [EarthSky: Belt of Venus](https://earthsky.org/tonight/earth-shadow-belt-of-venus-in-east-after-sunset/) · [Wikipedia: Earth's shadow](https://en.wikipedia.org/wiki/Twilight_wedge) · [Slynyrd Pixelblog 62](https://www.slynyrd.com/blog/2026/5/27/pixelblog-62-landscape-backgrounds) (fetch blocked; via search summary) · [Slynyrd Pixelblog 11](https://www.slynyrd.com/blog/2018/11/16/pixelblog-11-landscape-pixeling)
- Mood: [Miami Vice opening](https://miamivice.fandom.com/wiki/Opening_Sequence) · [designbycurio: Miami Vice teal](https://designbycurio.com/learn/miami-vice-pastel-teal-1984) · [Wikipedia: Hotline Miami](https://en.wikipedia.org/wiki/Hotline_Miami) · [COLOURlovers: Hotline Miami](https://www.colourlovers.com/palette/3173336/Hotline_Miami) · [Signalnoise: Blood Dragon](https://blog.signalnoise.com/far-cry-3-blood-dragon-art/) · [Game Informer: Blood Dragon](https://gameinformer.com/b/features/archive/2013/07/26/the-80s-strike-back-the-complete-story-behind-blood-dragon.aspx) · [80.lv: Katana ZERO](https://80.lv/articles/katana-zero-pixel-art-platformerslasher) · [foro3d: Katana ZERO](https://foro3d.com/en/2026/mayo/katana-zero-pixel-art-neon-y-distorsion-vhs-en-gamemaker-studio-2.html) · [Wikipedia: Synthwave](https://en.wikipedia.org/wiki/Synthwave) · [TV Tropes: Synthwave](https://tvtropes.org/pmwiki/pmwiki.php/Main/Synthwave) · [newretro.net: OutRun](https://newretro.net/blogs/main/what-is-outrun-the-brief-history-of-outrun-genre) · [Wikipedia: Patrick Nagel](https://en.wikipedia.org/wiki/Patrick_Nagel) · [Wikipedia: VA-11 Hall-A](https://en.wikipedia.org/wiki/VA-11_Hall-A)
- Craft: [presskit.gg capsule guide](https://presskit.gg/field-guides/steam-capsule-art-guide) · [steampageanalyzer capsule rules](https://www.steampageanalyzer.com/blog/steam-capsule-design-guide) · [CLIP STUDIO: backlit characters](https://tips.clip-studio.com/en-us/articles/10930) · [2D Will Never Die: sprite colours](https://2dwillneverdie.com/tutorial/picking-the-best-colors-for-your-sprite/) · [neonsignsnow: drawing neon](https://www.neonsignsnow.com/guides/how-to-draw-a-neon-sign-lights-on-paper-marker)
- Brand risk: [The Spirits Business: Malibu redesign](https://www.thespiritsbusiness.com/2019/06/malibu-goes-global-with-contemporary-redesign/) · [DesignBro: Malibu logo](https://designbro.com/blog/inspiration/malibu-logo-sun-beach-palm-trees-fun/)
- From the project:
  - `/home/user/FABLE5/Docs/GDD/14_art_bible.md` (§1, §2, §3, §4b, §5b, §6, §7, §7b, §8, §10)
  - `/home/user/FABLE5/Assets/Resources/Data/sky_cycle.json` (sky keys, sun, room light)
  - `/home/user/FABLE5/Tools/window_sky.py` (game sky renderer; preview at `/tmp/claude-0/-home-user-FABLE5/4011248d-b9dc-5209-9e44-5c8a7d0af4c5/scratchpad/sky_preview.png`)
  - `/home/user/FABLE5/Assets/Resources/Patron/hostess/idle/idle_00.png` (Roxy colours)
  - `/home/user/FABLE5/Tools/steam_page/out/capsules/*` (measured gates)