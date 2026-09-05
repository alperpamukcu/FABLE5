# 25 — Bottle Art v3: one hand, one camera, liquid inside

**Status: PARTLY SUPERSEDED by events (2026-08-07).** What the game actually ships is the
FLAT ERA: each bottle is ONE composed sprite (`v3_{id}_flat` / `_flat_open`, the author's
picked raw takes), the runtime liquid layer was removed whole in the 2026-08-07 sweep, and
a properly layered set is an **external artist's brief** (the author, 2026-08-07: "onu
ücretli olarak birisine yaptırtabilirim"). Deviations from the text below that are already
law: canvas is **80×160** (not 120×280); the no-text rule is **cancelled** (labels carry
their parody wordmarks); the three-plate sandwich is not on the load path. The camera,
palette, parody-dress and identity sections remain the binding brief for that artist.
**Tool citations below are historical (2026-09-05):** `Tools/v3_brief.py`, `Tools/bottle_open_states.py`
and `Tools/uniform_outline.py` were deleted in the single-session sweep; they describe how the flat era
was made, not a script that exists. The live bottle pipeline is `Tools/v4_bottles/` (`Docs/PLAN_bottle_art_v4.md`).

**Original status: LIVE spec (2026-08-04).** Owns the full rebuild of the 27 alcohol bottles that
followed the brand-parody renaming (`base_bar.json`, commit `8fb13e7`). Supersedes the
bottle sections of 15 §2 and 22 §1 where they conflict; the liquid *colour* tables stay
in `UITheme` (see module 14 §3 and the measured `LiquidColors` rewrite of 2026-08-04).
The author's brief, translated and pinned: every bottle redrawn from scratch, one at a
time, all of them from the same designer's hand; the camera never moves; the pixel
language and resolution are written down before the first bottle; each look parodies the
real brand its parody name points at; and **the liquid must sit inside the glass** —
neither behind the sprite nor in front of it reads as real.

## 1. The camera (the author's reference, 2026-08-04)

The author supplied a box and a cylinder as the law. What they encode:

- **Eye height:** slightly above the object — about **17° of pitch**. Every circular
  cross-section shows its top as an ellipse whose height is **30% of its width**
  (a 40px cap draws a 12px-tall ellipse). Bottom edges bow DOWN by half that ellipse
  (15% of width) — never a flat baseline.
- **No yaw for round things:** bottles are straight-on and left-right symmetric.
  Silhouette does the identity work, not rotation.
- **Boxes and cartons** (the one non-round family): front face is a true rectangle,
  no convergence; the top face shows as a shallow band; the RIGHT side shows as a
  narrow band about **12% of the front width**. Same pitch as the cylinders.
- **Sizes vary, the angle never does.** A taller bottle is more rows, not a new camera.
- **The camera comes from the TOOL, not the prompt** (settled 2026-08-04, three
  rounds deep): prompt-begged pitch on `create_1_direction_object` produced flat
  cut-outs twice. Bottles generate on **`create_map_object`, view "high top-down",
  canvas 120×280** — the author judged that view against "low top-down" and both
  sidescroller rounds and picked it. The ellipse and bow numbers above stay as the
  ACCEPTANCE test (base bow ≈ 15% of body width; cap-top ellipse visible), measured
  per take, since the tool's pitch still varies a little between takes.
- At this size the API returns ONE take per call — queue 2–3 calls per bottle for
  choice (~$0.25–0.35 a bottle).

## 2. Pixel language

- **Canvas:** masters generate on a **120×280** canvas and trim to roughly 70×260;
  they draw in the SAME shelf slot the old 162-band art used (~130 units tall), so
  the change is density, not size — about 2 art pixels per screen unit, the hi-bit
  register precedent. The 162 band survives only as the legacy sprites' grain.
  Body proportion is still asked for as a number in the brief.
- **Density:** hi-bit pixel art, PPU 1, point filtering; on the 1280-unit UI canvas a
  shelf bottle draws at ~120–135 units, so one art pixel ≈ 1.5–2 screen pixels.
  Whole-pixel snapping; no runtime rotation or non-uniform scale, ever.
- **Palette:** raw generations are off-palette by definition; the module 15 §2 chain is
  mandatory — quantize to the v2 40-colour palette (nearest ramp) → binary alpha →
  exact-size verify. Liquid colours are NOT painted into the art; they come from
  `UITheme.LiquidColors` at runtime.
- **Outline:** uniform 1px ink ring via `Tools/uniform_outline.py` (peel then one ring;
  idempotent, MARK-chunk stamped — memory `sprite-pipeline-idempotence`).
- **Light:** one key light, upper-left. One vertical specular streak on the left glass
  wall per bottle; caps and shoulders catch a 1px top rim.
- **No dither, no texture noise:** flat fills and ramp steps. The label is a clean plate.
- **The bottle wears its brand** (the author, 2026-08-04): the label carries the
  parody identity — a LOGO and the parody wordmark — never the real mark. Division of
  labour, because the generator cannot write (it produces mangled glyphs): the **logo
  is generated** as pure geometry (an emblem, a crest, a silhouette — asked for with
  "no text, no letters"), and the **wordmark is stamped by the pipeline** in our own
  3×5 pixel capitals, so every letter is ours and always crisp. Where the plate is too
  small for the full wordmark, the logo alone is the brand and the shelf tag says the
  name, as it already does.
- **A label never ships blank** (the author, 2026-08-04: "etikette yazı veya amblem
  olacak"). The map-object view tends to leave the plate empty however hard the brief
  pushes, so the pipeline finishes the dress deterministically when it must: the
  band, the medallion disc, the emblem and the wordmark are all drawable in code on
  the detected plate, in the brand's ink. Generated dress is a bonus, never a
  dependency.

## 3. The sandwich — how the liquid gets inside the glass

The author's standing complaint: drawn behind the art the liquid is a tint on paint;
drawn in front it covers the label. The answer is the question they asked — "can we
build the image as two layers and combine them?" — with one correction: the layers are
**derived from one master, never generated twice**. Generation cannot repeat itself
pixel-perfectly (the open-states lesson, three times over — memory
`open-states-derive`); derivation is the only thing that aligns 1:1.

**The master is always an EMPTY bottle** (the author, 2026-08-04, explicit): no liquid
is ever painted into the art — the brief demands "EMPTY, see clean through the glass",
and a take with liquid in it is rejected at the pick. The drink belongs to the game,
which is the only way one sprite can be full, half and dry.

Per bottle, the pipeline bakes from one generated master (capped, EMPTY glass):

1. **`*_back.png`** — the inside of the vessel: the glass cavity filled with the
   bottle's own glass hue at ~45% value (clear bottles get a cool pale interior, dark
   bottles a near-black one). Opaque. Transparent everywhere outside the cavity.
2. **the liquid** — drawn by the game between the plates, clipped to the cavity,
   coloured by `UITheme` (`LiquidColor` × `DrinkAlpha`), level from the baked volume
   curve. Runtime-owned; never painted into the art (2026-07-31 rule).
3. **`*_front.png`** — the master with the cavity turned into a glass FILM: cavity
   pixels dropped to ~30% alpha, except specular streaks kept ≥75% and label plates
   kept 100% (a label is printed on the OUTSIDE of the glass — `BottleArt`'s hard-won
   rule). Everything outside the cavity — outline, neck, closure, base — stays opaque.
   Ships in two states: capped, and **`*_front_open.png` derived** by
   `Tools/bottle_open_states.py`, which also paints the mouth bore as an ellipse per
   the §1 camera (30% ratio) — at this pitch an open bottle SHOWS its bore.

Draw order in game: back → liquid → front. The drink then reads through the front
film with the glass sheen over it and the interior wall behind it — inside, at last.

**Baked metadata replaces runtime guessing.** The pipeline knows the cavity exactly, so
it writes a sidecar (`Assets/Resources/Items/bottle_meta.json`: per id — cavity bounds,
wall insets, a 64-step per-row volume curve, mouth line). `BottleArt` reads the sidecar
for v3 bottles and keeps its tone-measurement only as the legacy fallback. This retires
the whole class of cavity-misread bugs (Thornwood, twice).

## 4. Trade dress: the look parodies the brand the name parodies

Same law as the names (commit `8fb13e7`): the SILHOUETTE FAMILY may echo the famous
bottle — squat green gin, square black-label whiskey, long-neck painted lager — but any
**registered distinctive element shifts**, exactly as the wordmarks did. Known hot
spots, decided at each bottle's step: Maker's wax drip (drip stays, colour and cut
change), Bass's red triangle (different shape), Bacardí's bat (a different winged
mark), Absolut's no-label printed glass (keep the idea, change the geometry), Clase
Azul's fluted ceramic (fluting yes, different proportions and colourway). One line of
intent per bottle is written into its generation brief and noted in `_CHANGELOG.md`.

Each label's LOGO is part of this parody (§2): a mark that rhymes with the famous
emblem without copying it — Smirkoff's crest is a smirk, not an eagle; the White Bat
medallion is a different wingshape; Goodness gets a device that is pointedly not a
harp. The stamped wordmark is always the parody name from `base_bar.json`, never the
real one.

## 5a. The fixed brief (2026-08-05)

Hand-written briefs reopened solved failures three rounds running — liquid pooled
back in, labels went blank, the pitch went flat. The brief is therefore FROZEN in
**`Tools/v3_brief.py`**, and a bottle is generated only through its `build()`: the
per-bottle LOOK sentence and its ratio are the only variables; the EMPTY, CHECKER,
BUILD and NO_TEXT blocks and the tool knobs (`create_map_object`, "high top-down",
120×280, single-colour outline, medium shading, medium detail — the exact settings
the approved take was made with) never change. Every block traces to a real failure
and is commented with it. Editing a frozen block is a spec change and goes through
this document.

## 5. Process — one bottle at a time

1. **Pilot = Smirkoff Vodka** (simplest glass, the shelf's zero point). It sets the
   anchor: generated, layered, staged in play next to the old shelf, and approved by
   the author before anything else is touched.
2. Every later batch anchors on the approved pilot via `style_image_base64` +
   `style_copy` (memory `art-direction-rules`) — the model copies OUR style, not its own.
3. Ladder by ladder (vodka → gin → rum → whiskey → tequila → singles → beers), 4
   candidates per bottle, picked by eye against the §6 checklist — scoring metrics
   stay banned (they flipped rankings twice).
4. Chain per bottle: generate (capped, EMPTY) → pick → quantize → stand to 162 →
   uniform outline → derive back/front/open + meta → import → **look at it in play**.
5. The old sprite survives in git; ids never change, so data, recipes, saves and tests
   never notice.

## 6. Acceptance checklist (every bottle, in play, not in the file)

- [ ] Top ellipses at 30%±5 ratio; bottom edge bows down; no flat cut-out look.
- [ ] Silhouette reads as its brand-parody at shelf size (~130 units tall).
- [ ] One light source, upper-left; streak on the left wall.
- [ ] Palette-clean after quantize; 1px uniform outline; binary alpha.
- [ ] Liquid at 25% / 60% / 95% fill reads INSIDE: sheen over it, interior behind it,
      label never tinted.
- [ ] Open state is the same bottle minus the cap, mouth bore visible.
- [ ] Empty bottle reads as empty glass, not as a dark slab.
