# LAST CALL — GDD Module: Art Bible v3 — "BRASS & MARBLE, SHIFT LIGHT" (supersedes v2 "Vice Pixel")

> **STATUS 2026-08-17 — CURRENT. This doc owns the look.**
>
> v3 keeps v2's discipline (locked ramps, integer scale, chunky readable pixels) and replaces
> its world. The bar is no longer a Miami vice club frozen at 2 AM — it is a brass-and-marble
> cocktail room that LIVES THROUGH THE SHIFT: neutral daylight at open, a vice sunset burning
> through the windows during service, amber pools and scarce neon at last call. The v2 palette
> survives whole and grows by two material ramps. Decided with the author 2026-08-17 against
> the approved scene mockups (the room and the blue-marble counter); amended the same day when
> the second batch drifted (§5) and the venue went layered.
>
> Ownership: **14 owns the look and the scene's layer order (§5), 15 owns the pipeline
> process (patched 2026-08-17 to follow v3), 16 v3 owns the UI chrome and is LAW for it,
> 18 owns choreography and motion only** — its layer-stack content is superseded by §5–7.

## 1. Identity in one line

"Brass & marble, shift light": an expensive, calm cocktail bar rendered in chunky readable
pixel art — cream plaster and bordeaux brick walls, espresso plank floor, a navy-marble
counter with a brass rail — whose light tells the time: neutral day at prep, vice sunset in
the windows at service, amber pools in the dark at last call. Confident and warm; never
gritty, never cute-retro, never a showroom.

## 2. Hard technical rules

- **UI field 1280×720, fixed** (16 v3 §0). **Stage art authored at 640×360** and drawn ×2 —
  one scale factor, both axes (`DiegeticStage` scales the world sprites; `StageArtFit` does
  the same for rect-hosted art). Counter strip 640×150. Runtime upscale only by integers.
- Sprites import as point filtering, no compression, mipmaps off, PPU 1 — `LastCallImporter`
  enforces it for everything under `Assets/Art/`; `Resources/Items` has its own
  postprocessor. New art goes under `Assets/Art/` so the importer owns it.
- No runtime rotation except 90° steps; no non-integer scaling — a drawing is used at the
  size it was drawn or a whole multiple (16 v3 §3). Motion = translation + frame swaps.
- Transparency is binary; no alpha gradients. Glow = banded falloff (16 v3 §5), never a
  soft halo.
- Dithering: 2×2 Bayer only, and only in the window sky. Never on UI, never on a material.
- Type 8/16/24 only; spacing grid 4 (16 v3 §0).

## 3. THE PALETTE (55 colours: 11 ramps × 5 — every drawn pixel comes from here)

The v2 40 + Malt (GDD 21 §10) + two v3 **material ramps**. Values are `UITheme.cs` verbatim;
the code is the truth and this table must match it.

**Index convention: brackets are UITheme's 0-based array indices — `[0]` is the darkest
step, `[4]` the lightest.** Every colour reference in this doc uses that convention and no
other.

| Ramp | [0] (darkest) | [1] | [2] | [3] | [4] | Owns |
|---|---|---|---|---|---|---|
| Night | `#0D0813` | `#1A1023` | `#241830` | `#362447` | `#4A3160` | night air, the espresso floor, scrims |
| Magenta | `#5C1B45` | `#8F2464` | `#C23283` | `#E84DA6` | `#FF7DC6` | the story, the sunset, the one neon sign |
| Cyan | `#123B45` | `#1B5F66` | `#26918F` | `#3BC8BE` | `#7DF0E3` | selection, information, the clock, fridge light |
| Amber | `#4A2E14` | `#8F5A1E` | `#C9822B` | `#E8A33D` | `#F5C97B` | money, primary action, brass, oak, bar light |
| ViceRed | `#3D1220` | `#6E1B32` | `#A62B44` | `#D9455C` | `#F27D8A` | refusal, liquor reds, VIP heat |
| ClubBlue | `#131B3D` | `#1F2E66` | `#2E4699` | `#4467CC` | `#6E93F0` | **the marble**, the window frame, deep light, glass shadow |
| Lime | `#16331B` | `#2A5926` | `#479938` | `#6FCC4B` | `#A8F077` | gain, sour greens |
| Cream | `#453E38` | `#6E6459` | `#9C8F80` | `#C9BCA8` | `#F2E8D5` | text, plaster walls, white marble, smoke |
| Malt | `#3A2410` | `#6B4416` | `#9E6A1D` | `#C98F2B` | `#E6B959` | beer, and only beer (21 §10) |
| **Graphite** (v3) | `#14161A` | `#24272D` | `#383D45` | `#545A64` | `#808893` | cabinet bodies, metalwork, appliance chrome |
| **Brick** (v3) | `#38161A` | `#5C2226` | `#7E3130` | `#9C4740` | `#B96253` | the bordeaux masonry wall, and nothing that signals |

Rules, unchanged from v2 and still absolute:
- Shading = move along a ramp, NEVER darken/lighten off-ramp.
- Outlines use the darkest step of the object's own ramp — no pure black, no pure white.
- Text: `Cream[4]` on dark, `Night[2]` on amber (`UITheme.TextPrimary` / `TextOnAmber`).
- Generated art is quantized to this palette in post — no exceptions (15 §2, as patched).

Two licensed code exceptions, named so nobody rediscovers them: the drink LIQUID table in
`UITheme` (transmission colours, measured — 21-era law) and the bottle-style tag colours.
The back bar (`BackBarArt`) still draws v2's teal-navy vice hexes off-palette — that is
OPEN WORK (§6), not a licence.

**The material-ramp law (new):** Graphite and Brick are ARCHITECTURE ONLY. They may never
carry a signal — not type coding, not a sacred number, not a state light, not a key face.
The signal colours stay unambiguous precisely because the furniture can't borrow them and
they can't borrow the furniture's.

Ingredient type coding is unchanged (v2 §5): Spirit=Amber · Sour=Lime · Sweet=Magenta ·
Bitter=ViceRed · Bubbly=Cyan · Garnish=Cream · Beer=Malt — always triple-coded (ramp +
icon + label). Sacred number colours live in 16 v3 §5.

## 4. The shift-light model (replaces v2 §4's two-source rule)

The room is authored ONCE, in neutral day light — exactly the light of the approved mockups.
Night is a **relight EDIT of that master** (§11.D), never a fresh generation, so the geometry
cannot drift. The shift has three states; the window plate (§7) is the clock the player
actually reads.

| State | When | Room/counter art | Window plate | Runtime accents |
|---|---|---|---|---|
| **OPEN** | prep, market, before the first seat | day masters | day (low golden sun) | none — honest daylight |
| **SERVICE** | doors open → last call | night variants | vice sunset | amber pool over the counter, fridge glow (Cyan), the one neon sign on |
| **LAST CALL** | the closing beat (GDD 26; S4's light lands here) | night variants + `Night[0]` multiply ≈ 0.35 | night (palms on dark) | one amber pool survives; the beat's six seconds of dark grade from this state |

- Transition is a crossfade on the backdrop layers plus the accents switching — timings and
  wiring are a later runtime phase; this doc fixes what each state LOOKS like.
- The v2 signature ("amber key above, neon rim behind") now describes SERVICE and LAST CALL
  only. Day has no rim and no lit neon — that contrast is the point.

## 5. The room is a SHELL; everything standing in it is a PROP (2026-08-17)

The venue is built in layers, on the author's call: the room master is **EMPTY** — walls,
floor, ceiling, ducts, the window aperture — and every piece of furniture and lighting is
its **own sprite in its own file**, placed on a stand line. This is not tidiness for its own
sake: furniture is the game's upgrade surface (the bar top already sells in tiers —
`TycoonRun.CounterTier`), and a thing the player can improve must be swappable without
touching the room behind it.

**Layer order (back → front):** window plate (§7) → room shell → furniture props → counter
strip (§6, a LAYER of its own, not a prop) → the stage's life (glasses, customers, props on
the counter) → HUD (16/24).

**Prop laws:** authored at 1× · binary alpha · anchored by bottom-centre on the shell's
floor line or hung on its wall · generated on flat `#00FF00` and cut in post (§11) · a prop
NEVER bakes its own patch of wall or floor into the sprite — the room behind it must be able
to change without the prop noticing.

### 5a. Shell spec — the per-object colour law

| Object | Colours (ramp[step], 0-based per §3) |
|---|---|
| Ceiling | field `Cream[2]`, lifting to `Cream[3]` toward the window; cornice `Cream[1]` |
| Plaster walls (left + back) | field `Cream[3]`, lit patches `Cream[4]`, shade `Cream[2]`, cracks `Cream[1]`, outline `Cream[0]` |
| Baseboards, window sill | `Graphite[2]`, top edge `Graphite[3]` |
| Floor (espresso planks) | plank face `Night[2]`, alternating `Night[1]`, sparse grain `Night[3]`, seams & outline `Night[0]` |
| Brick wall (right) | mortar `Brick[0]`, brick field `Brick[2]`, shadowed course `Brick[1]`, lit faces `Brick[3]`, sparse chips `Brick[4]` |
| Window frame | body `ClubBlue[2]`, lit bevel `ClubBlue[3]`, shadow `ClubBlue[1]`, outline `ClubBlue[0]`, ONE `Cream[4]` highlight |
| Window panes | flat `#00FF00`, keyed in post (§11.B) — never a drawn checkerboard |
| Ceiling ducts (canon as of the second batch) | body `Graphite[2]`, lit top `Graphite[3]`, straps `Graphite[1]`, grille slits `Graphite[1]`, outline `Graphite[0]` |

The second batch (2026-08-17) is what this table exists to prevent, and why it is now a law
rather than a mood: a teal wall, a honey floor, beige fieldstone, a steel frame and a
graphite platform — five materials each individually fine and jointly belonging to no one
room ("renkler hiç uyumlu gelmedi"). Teal is OFF-PALETTE entirely and stays off: it collides
with Cyan's signal job. If an accent wall is ever wanted it is `ClubBlue[1–2]` navy paint —
never teal. Nothing in the room GLOWS Magenta, Cyan or Lime at day (§10 carries the two
written exceptions — an appliance lamp and a dead tube); those hues arrive with the night.

### 5b. Composition anchors (640×360 art px, ±8)

Targets for generation, not runtime promises: the constants that place things against the
room (`DiegeticStage`'s lamp/shelf/counter numbers) were measured off the shipped art by
hand, offline — **regenerating a background means re-measuring those constants**, not
trusting the tolerance.

| Anchor | Target |
|---|---|
| Back-wall base (wall meets floor) | y ≈ 130 from top; the floor owns the bottom ~230 px |
| Window frame, outer | x 234 → 407 (center ≈ 320), top y ≈ 53, sill y ≈ 131 |
| Window panes | 3 panes ≈ 48×72 each behind the frame's mullions |
| Brick wall | the right ~third of the frame, receding |

### 5c. Prop catalogue (each its own file, own colours)

| Prop | Colours | Nominal size / seat |
|---|---|---|
| Marble platform | top `Cream[4]`, top shade `Cream[3]`, front `Cream[3]→[2]`, skirt `Cream[1]`, veins `Cream[2]` + sparse `Amber[3]` gold, outline `Cream[0]` | ~184×64; stands x ≈ 20→205, top surface y ≈ 165 |
| Oak shelf unit | frame `Amber[2]`, lit edges `Amber[3]`, sparse highlight `Amber[4]`, shadowed interior `Amber[1]`, depth & outline `Amber[0]` | ~132×108; hung on the brick, upper two-thirds |
| Pendant lamp (per §4 states) | OFF: `Graphite[2]` + `Amber[2]` body · ON: adds `Amber[4]` bulb + banded Amber glow (16 v3 §5) | small; hangs over the counter |
| Neon sign "LAST CALL" | OFF: dead tube `Magenta[1]` (§10 exception — unlit glass, not light) · ON: `Magenta[3–4]` + banded glow | on the brick wall; the ONE neon |

New furniture enters by growing this table — name, colours from §3's ramps under the
material law, size, seat — before it enters the game.

## 6. The counter (640×150, its own layer, sold in TIERS)

The counter is composited at the BOTTOM of the scene as its own strip. Its surface line is
the stage's most load-bearing number — today it is the hand-measured constant
`CounterRestY = 128` in `DiegeticStage` (measured off `counter.png` once, offline), and
everything on the bar stands against it. The file contains ONLY the counter: **everything
above the top slab's back edge is transparent — no wall, no brick, no room** (the second
batch baked both in; the room is the shell's job).

The bar top is an upgrade the game already sells (Bar Top, `CounterTier` 1–3), so the counter
ships as **three strips over ONE shared base**, and the surface line sits at the SAME art y
in all three — glasses stand on it, the constant is measured once, and an upgrade must not
move every glass in the bar.

- **Base (all tiers):** Graphite cabinets — faces `Graphite[2]`, panel insets `Graphite[1]`,
  lit rims `Graphite[3]`, outline `Graphite[0]`, handles `Amber[2]`; open shelving interiors
  `Graphite[1]` with shelf edges `Graphite[3]`; fridge doors `Graphite[1]` glass in
  `Graphite[2]` frames, racks `Graphite[3]`, interior light `Cyan[0]` at day (§10 exception),
  `Cyan[2–3]` at SERVICE.
- **Tier 1 — OAK:** top field `Amber[1]`, grain `Amber[0]`/`Amber[2]`, front edge band
  `Amber[0]`. No rail.
- **Tier 2 — WHITE MARBLE & STEEL:** top field `Cream[3]`, veins `Cream[4]` and `Cream[2]`,
  edge `Cream[2]`; rail `Graphite[3]` with a `Graphite[4]` highlight.
- **Tier 3 — NAVY MARBLE & BRASS (the hero look):** top field `ClubBlue[1]`, depth mottling
  `ClubBlue[0]`/`[2]`, veins `Cream[3]` + sparse `Amber[3]` gold, polished edge `ClubBlue[2]`;
  rail `Amber[2]` body, `Amber[3]` lit, ONE `Cream[4]` highlight line; outline `ClubBlue[0]`.

Veins are 1–2 px runs, never fields. The surface line stays dead straight. Runtime today
loads a single `counterSprite`; the tier swap is later wiring, but the art is authored for
it from the start.

**Open work, named honestly:** the code-drawn back bar (`BackBarArt`) still paints v2's
look from its own hard-coded hexes — teal-navy panels and its own brass, the exact teal §5a
bans. Migrating it onto the v3 ramps (Graphite body, Amber brass, ClubBlue accents) is a
design-and-rebless job of its own: the back bar is a pixel-compared LookTests screen, so it
moves in one deliberate sitting, not as a drive-by. The v3 material spec for that sitting
is `backbar.png` in §11.C (2026-08-17): the back bar page is CODE-LAID-OUT — shelf count
grows with the cellar, and bottles, name plates, kegs, the bin and SERVE are all live
sprites — so the art is an EMPTY shell like the room's: graphite panelled wall, three empty
niches on deep navy, brass shelf edges, and a ledge in the T3 counter's own navy marble so
the front and back bar read as one bar. The master art-directs the look and is then sliced
into `BackBarArt`'s strip sprites (wall tile, shelf face, shelf floor, ledge) at migration
time — the page cannot use it whole.

## 7. The windows: the vice sunset clock

Three plates, one bounding box — the union of the three KEYED panes, nominal ~160×72 (three
48×72 panes plus the mullions between them; the frame's stiles and sill are painted
`ClubBlue`, not keyed) — drawn behind the room, final size measured off the keyed master.
This is where v2's Miami survives — as the view, not the room.

- **Day:** soft golden sky (`Amber[3–4]` into `Cream[4]`), palm silhouettes optional, faint.
- **Sunset (SERVICE):** the vice gradient — Magenta into Night, 2×2 Bayer dither, palm and
  skyline silhouettes in `Night[1]`. The loudest the palette ever gets, and it is outside.
- **Night (LAST CALL):** `Night[0–2]` sky, sparse lit windows (`Amber[3]`, single px), palms
  near-black against it.

## 8. Rim light policy (v3 — no longer a law)

The 1–2 px neon rim on foreground sprites was v2's signature and a hard rule. It is now an
**option, judged by results** (the author, 2026-08-17: "daha iyi sonuçlar alınırsa
değiştirilebilir, şart değil"):

- New masters are authored rim-free in neutral light.
- At SERVICE/LAST CALL a rim is *preferred* where it reads — magenta/cyan as before, or a
  warm brass rim (`Amber[4]`) where neon fights the material.
- Existing rim-baked sprites are night-correct and stay until an asset is redone anyway.
  Nothing gets a rework *for* the rim.

## 9. Sprite size standards (at 1×)

Unchanged where they hold, corrected where the game moved on:

- Stage backdrop 640×360 · counter strip 640×150 · window plate ~160×72 (measured, §7).
- **Bottles/vessels: sheet-free.** `VesselArt` measures the alpha and the game uses the
  drawing (15 §8) — no fixed bottle box anymore. Bottles are single flat sprites
  (`v3_{id}_flat`); the layered set is the external brief (GDD 25).
- Patron portrait masters 96×128; card crop 48×64 (face readable at arm's length).
- Tool icons 16×16 · type icons 8×8 grid-snapped · coin 8×8.
- Chrome drawings (16 v3 §3): marks 16×16, key 20×20 9-sliced, lamp 16, lamp glow 24.

## 10. Do / Don't

DO: bold silhouettes readable at 1× · materials from §3's ramp map · one brass rail highlight ·
the sunset in the window, not on the wall · diverse patron cast · day scenes that look like
honest daylight.

DON'T: real brands · drunkenness depiction · pure black or pure white pixels · painterly or
photoreal anything · mixed pixel densities (NEVER scale to fit) · more than 2 neon hues in
one composition, and at day: zero LIT neon · glowing Magenta, Cyan or Lime on anything at
OPEN · marble veining as noise fields · a second amber key (16 v3 §2) · a prop that bakes
its own patch of wall or floor (§5) · off-palette hues on the shell — teal shipped once and
reads as nobody's room (§5a).

**The two day exceptions, written so they stay two:** (1) the fridge interior light is
`Cyan[0]` at OPEN — a fridge runs all day and its light is a lamp, not architecture; it
rises to `Cyan[2–3]` at SERVICE. (2) the neon sign's dead tube hangs on the brick at OPEN in
`Magenta[1]` — unlit glass is a material, not a light; the day ban is on anything that
GLOWS. Neither exception ever spreads.

## 11. Background production spec (Nano Banana — the author generates, the agent posts)

The room and counter backgrounds are generated by the author with Nano Banana; everything
here is what makes those generations land in the game unchanged. The staging/review loop and
logging of 15 §2–5 apply as patched 2026-08-17 (55-colour quantize; rim per §8; the
exact-size REJECT is for sprites — backgrounds are deliberately generated large and
area-downsampled per this section).

**A. Canvas & framing.** Generate 16:9 (2816×1584 or similar is fine — it downsamples to
640×360 cleanly). Compose against §5b's anchor table. No text (the neon sign prop excepted),
no brands, no humans. Chunky pixel look requested in-prompt; the true pixel grid is made in
post, so generation-side pixel size need not be exact.

**B. The green key.** Window panes are flat chroma green `#00FF00`, and that green appears
NOWHERE else in the image. Post keys the panes to transparent holes; the window plate (§7)
draws behind. **Never a drawn checkerboard** — a painted "transparency" pattern is baked
pixels and keys as nothing (the second batch shipped one). Props are generated the same way:
the whole background flat `#00FF00`, cut in post. If the generator can emit a true-alpha
PNG instead, that is also fine; the ban is only on *painted* placeholders.

**C. The asset list (each its own file, each its own generation):**

| Asset | File | Size | Prompt colour words (feed these hexes verbatim) |
|---|---|---|---|
| Room shell (EMPTY) | `club_room.png` | 640×360 | cream plaster walls `#C9BCA8` lit `#F2E8D5`, espresso plank floor `#241830` with seams `#0D0813`, bordeaux brick wall `#7E3130` with mortar `#38161A`, navy-blue window frame `#2E4699`, graphite ducts `#383D45`, three flat green `#00FF00` panels; NO furniture |
| Room shell, night | `club_room_night.png` | 640×360 | shell relight edit (§11.D, first prompt) |
| Counter tier 1 | `counter_t1.png` | 640×150 | oak top `#8F5A1E`, graphite cabinet base `#24272D`/`#383D45`, brass handles `#C9822B`, faint fridge light `#123B45` — no rail; background flat `#00FF00` |
| Counter tier 2 | `counter_t2.png` | 640×150 | white marble top `#C9BCA8` with veins `#F2E8D5`, steel rail `#545A64`, same graphite base, faint fridge light `#123B45` |
| Counter tier 3 | `counter_t3.png` | 640×150 | navy marble top `#1F2E66` with cream `#C9BCA8` and gold `#E8A33D` veins, brass rail `#C9822B`, same graphite base, faint fridge light `#123B45` |
| Counter night variants | `counter_t*_night.png` | 640×150 | counter relight edits (§11.D, second prompt) |
| Window plates | `window_day/sunset/night.png` | ~160×72 | §7 verbatim |
| Back bar shell (EMPTY, §6 open work) | `backbar.png` | 640×360 | graphite panelled wall `#24272D` with bevels `#383D45` and seams `#14161A`, three empty shelf niches on deep navy `#131B3D`, slate shelf floors `#383D45`, brass shelf edges `#C9822B` lit `#E8A33D`, navy marble ledge `#1F2E66` with cream `#C9BCA8` and gold `#E8A33D` veins, espresso floor strip `#241830`; NO bottles, NO kegs, NO text |
| Marble platform | `prop_platform.png` | ~184×64 | white marble `#F2E8D5`/`#C9BCA8`, sparse gold veins `#E8A33D`, on flat green |
| Oak shelf unit | `prop_shelf.png` | ~132×108 | golden oak `#C9822B`/`#E8A33D`, dark interior `#8F5A1E`, on flat green |
| Lamps / neon sign | `prop_lamp_*.png`, `prop_sign_*.png` | small | §5c states, OFF and ON as separate files |

`counter.png` (the current shipping strip) stays until a tier lands; tier 3 is the hero look
and replaces it first.

**D. Night is an EDIT, never a regeneration.** Feed the approved day master back with a
relight instruction only — one per asset kind, because the shell is EMPTY and must stay so:

- *Shell:* "same empty room, night — the room fallen toward deep shadow, walls and floor
  dark, a faint warm glow from the ceiling lamps' positions; keep the green panels flat
  green; do not add any furniture; do not move or redraw any object."
- *Counter strips:* "same counter, night — warm amber light pooling over the top surface,
  glass fridge doors glowing cool blue, the base fallen toward shadow; keep the background
  flat green; do not move or redraw any object."

Geometry must survive; if it visibly drifts, reroll the edit, not the master. **The counter
strips are a scene LAYER, not props — they DO get night variants.** Props do not: they live
under the scene's grade, and lamps carry their own ON state instead.

**E. Post chain (agent side, per 15 §2):** crop to exact 16:9 → area-downscale to target
size → quantize to the 55-colour palette (nearest ramp) → key `#00FF00` → binary alpha
cleanup → `Assets/Art/Backgrounds/` (shell, counter, windows) or `Assets/Art/Props/` →
`LastCallImporter` picks it up.

**F. Review, at 1× and 3× (15 §3):** palette-compliant after quantize · counter surface line
straight and at the SAME y across tiers · panes/props keyed clean (no green fringe) ·
silhouettes read at 1× · no anti-aliased edges survived · day master genuinely neutral (no
baked neon glow) · the shell truly empty · no prop carrying its own wall · when a shipped
background changes, `DiegeticStage`'s measured constants (`CounterRestY`, lamp and shelf
anchors) are re-measured against the new art. Max 4 rerolls, then edit or human call.
LookTests baselines are re-blessed when a shipped screen changes — first run fails on
purpose so someone LOOKS (CLAUDE.md).
