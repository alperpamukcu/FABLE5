# LAST CALL — GDD Module: Art Bible v2 — "VICE PIXEL" (supersedes v1 cozy-noir painterly)

> v2 REPLACES the painterly direction entirely. Any asset, shader tint, or UI color not complying with this document is `legacy` (see 17_ui_inventory.md) and must be migrated. This doc owns the look; 16 owns UI systems; 18 owns the nightclub scene.

## 1. Identity in one line
"Vice pixel": a neon-soaked nightclub bar at 2 AM — Miami-sunset magentas and cyans over deep night purples, warm amber pooling on the bar counter. Chunky, readable pixel art. Confident, stylish, never gritty, never cute-retro.

## 2. Hard technical rules
- **Base resolution 640×360.** Everything authored at 1×. Runtime upscale ONLY by integers (×2, ×3=1080p) via Unity Pixel Perfect Camera.
- All sprites: point filtering, no compression, mipmaps off, single PPU (see pipeline patch).
- No runtime rotation of pixel sprites except 90° steps; no non-integer scaling (motion = translation + frame swaps + palette flashes).
- No alpha gradients; transparency is binary. Glow = hand-placed halo pixels in the accent ramp.
- Dithering allowed for large gradient areas (sky/sunset) — 2×2 Bayer only, never on UI.

## 3. THE PALETTE (locked, 40 colors — every pixel in the game comes from here)
Night/Plum (backgrounds, surfaces):  #0D0813 #1A1023 #241830 #362447 #4A3160
Magenta Neon (vice accent, danger):  #5C1B45 #8F2464 #C23283 #E84DA6 #FF7DC6
Cyan Neon (selection, info, cool light): #123B45 #1B5F66 #26918F #3BC8BE #7DF0E3
Amber/Gold (bar light, primary action, money): #4A2E14 #8F5A1E #C9822B #E8A33D #F5C97B
Vice Red (liquor, hearts, VIP heat):  #3D1220 #6E1B32 #A62B44 #D9455C #F27D8A
Club Blue (deep lights, glass shadow): #131B3D #1F2E66 #2E4699 #4467CC #6E93F0
Lime/Sour (green ramp):               #16331B #2A5926 #479938 #6FCC4B #A8F077
Cream/Neutral (text, chrome, smoke):  #453E38 #6E6459 #9C8F80 #C9BCA8 #F2E8D5
Rules: shading = move along a ramp, NEVER darken/lighten off-ramp. Outlines use the darkest step of the object's own ramp (no pure black). Text = Cream 5 (#F2E8D5) on dark, Night 3 (#241830) on amber. AI-generated sprites are quantized to this palette in post — no exceptions.

## 4. Light rule (v2)
Two-source model: (a) warm amber key from ABOVE the bar counter (bottles/counter/hands lit amber on top), (b) cool magenta-or-cyan rim from BEHIND (club lights) on left/right silhouette edges. Every foreground sprite carries 1-2 px of neon rim. This rim is the signature of the game's look.

## 5. Ingredient type color coding (remapped to palette ramps)
Spirit=Amber ramp · Sour=Lime ramp · Sweet=Magenta ramp · Bitter=Vice Red ramp · Bubbly=Cyan ramp · Garnish=Cream ramp. Type is ALWAYS triple-coded: ramp color + icon + label(tooltip only).

## 6. Sprite size standards (at 1×)
Bottles (rail): 24w × 40h in a 32×48 slot · Glass/shaker: 32×32 · Patron portrait card: 48×64 (face readable at arm's length) · VIP large portrait: 96×128 · Tool icons: 16×16 · Type icons: 8×8 (grid-snapped) · Coin: 8×8 · Bar counter tile: 32×32 · Background layers: 640×360 each.

## 7. Do / Don't
DO: bold silhouettes readable at 1×, neon rim light, animated 2-frame flickers on signage, chrome highlights (Cream ramp), diverse patron cast, palm/skyline silhouettes through windows.
DON'T: real brands, drunkenness depiction, outline-less mush, more than 2 neon hues in one composition, mixed pixel densities (NEVER scale a sprite to non-integer size to "fit"), photoreal or painterly elements anywhere, pure black or pure white pixels.

## 8. AI-generation compliance hooks (v2)
Primary generator: PixelLab (or Retro Diffusion) — see 15 pipeline patch. Every prompt embeds: "pixel art", exact sprite size, palette description ("deep purple night, neon magenta and cyan rim light, amber bar glow"), "clean silhouette, 1px outline in darkest ramp step". Every output is post-quantized to the 40-color palette and human/agent-reviewed at BOTH 1× and 3× zoom. LoRA training is DEPRECATED for v2 (palette+grid discipline replaces it).
