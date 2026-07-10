# LAST CALL — GDD Module: Art Bible (Visual Identity Spec)

> Required before any final asset production. All AI-generation prompt templates and any commissioned art must comply with this document.

## 1. Identity in one line
"Cozy noir": a warm, dim, slightly mysterious late-night bar — inviting, never sleazy, never gritty-realistic. Painterly illustration, soft edges, no photorealism, no pixel art.

## 2. Palette (locked hex values)
- Background base: deep plum `#1A1023`, panel surface `#241830`
- Warm core: amber `#E8A33D`, candle glow `#F5C97B`, wood brown `#6B4226`
- Cool accents: teal shadow `#1E4D4A`, neon cyan `#4DD9D0` (sparingly), neon magenta `#D94D8F` (VIP/danger only)
- Text: cream `#F2E8D5` on dark; never pure white, never pure black
- Ingredient type bands: Spirit `#E8A33D`, Sour `#8FBF4D`, Sweet `#E87DA4`, Bitter `#C74B3C`, Bubbly `#5BC8D9`, Garnish `#D9B84D`
Rule: any screen is ≥60% warm-dark neutrals, ≤10% neon accent.

## 3. Light rule (consistency anchor)
Single warm key light from the upper-left at ~45°, as if from a bar lamp; teal fill in shadows. EVERY portrait, prop and card illustration follows this. This is the #1 check on generated assets.

## 4. Card anatomy (authoring spec)
- Card canvas 284×380 px (2× of 142×190 display). Corner radius 12 px (at 1×).
- Art window: upper 62%; type color band: 8 px strip under art; Flavor value: top-left circle 34 px, display font; name plate: bottom 20%.
- Quality overlays are additive layers (Top Shelf sheen, Barrel-Aged rainbow specular, Signature animated gradient, Bootleg inverted palette) — art underneath never changes.
- Portrait framing (Patrons/VIPs): bust, 3/4 view facing screen-left, eyes at 40% height, flat dark-teal vignette background. No hands, no props crossing the frame border.

## 5. Typography
- Display (logo, scores, card values): a chunky rounded slab (e.g., "Fredoka"-class). Numbers must render tabular.
- UI/body: high-legibility humanist sans (e.g., "Inter"-class), min 16 px at 1080p.
- Accessibility alternative: OpenDyslexic swap affects UI/body only, never the logo.

## 6. Motion & FX language
Cards: ±3° idle wobble on hover, 90 ms snap on select. Scoring: per-card pop + rising number, pitch-linked. Big ×Mult: 4-frame screen shake (disable-able) + foam particle burst. Everything eases with back-out curves; nothing linear.

## 7. Do / Don't
DO: visible brush texture, warm rim light, exaggerated silhouettes, diverse patron cast, drinks as craft objects.
DON'T: real brands or bottle labels, drunkenness depiction, photoreal glass renders, harsh pure-black outlines, horror imagery, more than one neon accent per composition.

## 8. AI-generation compliance hooks
Every prompt template must embed: style block (Section 1 wording), palette words (amber/teal/plum), light rule (Section 3), framing spec (Section 4). Every generated batch passes a manual review against Sections 3–4 before import; rejects are regenerated, never hand-fixed into inconsistency.
