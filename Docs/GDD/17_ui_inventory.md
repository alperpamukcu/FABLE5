# LAST CALL — GDD Module 17: UI & Screen Inventory (migration tracker)

> Living index of **every** screen and UI component in the game and its migration
> status against the v2 "Vice Pixel" art direction (see `14_art_bible.md` v2).
> Update the status column as each item is migrated. This is the source of truth
> for what still needs art/UX work.

## Status tags
- **legacy-cozy-noir** — built under the v1 painterly direction (AI-painterly art,
  Limelight/Barlow fonts, procedural rounded UI kit). Functional but off-palette;
  must be regenerated / restyled for v2. **Frozen** — no further polish.
- **placeholder** — art-independent structure built for v2, using flat silhouettes
  from the locked v2 palette. Awaits final pixel-art sprites.
- **migrated-v2** — final Vice Pixel art + palette-compliant, signed off.

## 1. Gameplay screen

| Component | Owner | Status | Notes |
|---|---|---|---|
| Background (animated smoke shader) | `SmokeSwirl.shader` / DebugHud | legacy-cozy-noir | Superseded by the diegetic BackgroundLayers; disabled when the stage is present. |
| **Diegetic stage — BackgroundLayers** (nightclub interior: neon, windows+city, crowd, back-bar) | `DiegeticStage` | migrated-v2 | Real PixelLab club background (`club_bg`, dimmed ~40% so it recedes and the foreground pops — less eye-tiring), palette-quantized; opaque overscanned backdrop behind it. Procedural sky/crowd/neon kept as a fallback. |
| **Diegetic stage — BarCounter** | `DiegeticStage` | migrated-v2 | **Curved (arc) bartender-POV bar** (`counter`): a dome-shaped wood surface + chrome edge with cocktail glasses following the arc, strong FOV. Bottles ride the arc (`SlotArcY` parabola, centre rises `ArcHeight`); register (left) and VIP (right) sit at the tilted arc ends. `CounterSurfaceInset` aligns the chrome rest line to the rail. |
| **Diegetic stage — Customer/VIP** | `DiegeticStage` | migrated-v2 | Pixel patron (`vip_patron`) leaning forearms on the bar top (own desk removed), bottom-right, hands on the wood, drawn on top of the counter + rail. Replaces the legacy painterly card. One generic patron for now — per-VIP pixel art is a follow-up. |
| **Diegetic stage — Cash register + wallet** | `DiegeticStage` | migrated-v2 | Brass till (`register`) bottom-left on the bar; the wallet renders diegetically on its display (`SetMoney`, Money=Amber). |
| **Diegetic stage — BottleRail** (8 bottle slots on the counter) | `DiegeticStage` | placeholder | Fitted to spec-18: 24×40 bottles, slot pitch 56 from x=88, base y=232. Draw/Select/Mix/Refresh choreography per 18 §3 (240ms OutQuad + 2px overshoot, 4px select-rise, 180ms InQuad exit). **All 46 ingredients have a distinct v2 pixel bottle** (colour by type ramp, a recognizable silhouette per drink — keyed by ingredient id, per-type + placeholder fallbacks behind it), 32×48, PixelLab→quantized); sprite bottles carry a baked rim, selection washes them cyan + a 4px rise. |
| Ingredient rail (UI card version) | DebugHud | legacy-cozy-noir | **Removed** — replaced by the diegetic BottleRail. |
| Info panel (night/wallet/target/score/mixes) | DebugHud | legacy-cozy-noir | Text overlay; restyle in v2 UI pass (module 16). |
| Patron shelf | DebugHud | legacy-cozy-noir | Portrait thumbnails + sell; portraits are v1 painterly. |
| Tool belt | DebugHud | legacy-cozy-noir | Tool sprites are v1 painterly. |
| Live recipe preview line | DebugHud | legacy-cozy-noir | Lifted out of the rail; muted while the score card is up. |
| **Score moment** (mix result card) | DebugHud | placeholder | Amber-framed opaque pixel card: recipe + `Flavor cyan × Mult magenta = score`. Rises 10px + fades in (160ms), holds 1.2s, fades out; plain hold under reduced motion. Covers no-recipe and VOIDED. |
| **Ambient life** (neon flicker, patron idle) | `DiegeticStage` | migrated-v2 | 18 §4 budget: neon wall flickers ~1 frame every 3-7s, patron breathes on a 1px 2-frame idle. Skipped under reduced motion; cosmetic only, never touches RunRng. |
| MIX / RESTOCK / SKIP→FAVOR / BOUNCER / RECIPES / New Run buttons | DebugHud | placeholder | Restyled to flat v2 pixel buttons: palette fill + 2px dark border + Silkscreen pixel font (dropped the rounded procedural sprite). Stats block + section headers (PATRONS/TOOLS) still legacy font — pixel pass pending. |
| Customer / VIP portrait card | DebugHud | legacy-cozy-noir | VIP portraits are v1 painterly. |
| RECIPES toggle | DebugHud | legacy-cozy-noir | |
| Win / lose banner | DebugHud | legacy-cozy-noir | Limelight font. |
| Vignette overlay | DebugHud | legacy-cozy-noir | |
| Seed input + New Run (dev) | DebugHud | placeholder | Debug-only; excluded from shipped UI. |
| **Debug score log** | DebugHud | placeholder | Moved off the game view to an **F1** toggle. |

## 2. Back Room (shop) modal

| Component | Owner | Status | Notes |
|---|---|---|---|
| Modal panel + scrim | DebugHud | legacy-cozy-noir | Phase-gated modal over a dark scrim. |
| Offer rows (patron / tool / book thumbnails) | DebugHud | legacy-cozy-noir | Thumbnails are v1 painterly. |
| Voucher / pack / reroll / continue buttons | DebugHud | legacy-cozy-noir | |
| Pack-open pick list | DebugHud | legacy-cozy-noir | |

## 3. Recipe Book modal

| Component | Owner | Status | Notes |
|---|---|---|---|
| Modal panel + colour-dot recipe list | DebugHud | legacy-cozy-noir | Type dots use v1 colours; remap to v2 ramps. |

## 4. Screens not yet built (planned)

| Screen | Status | Notes |
|---|---|---|
| Title / main menu | not-built | Will use v2 nightclub scene (module 18). |
| Settings (audio / reduced-motion / dyslexia font) | not-built | Reduced-motion flag exists (`Motion`), no screen yet. |
| Run summary / meta (Bars, Stakes) | not-built | |

## 5. Cross-cutting systems

| System | Owner | Status | Notes |
|---|---|---|---|
| Design tokens (40-palette, ramps, 4px grid, number roles) | `UITheme` | migrated-v2 | Single source for every scene/UI colour; ramps + type→ramp map per 14 v2 §3/§5. |
| Pixel Perfect Camera (640×360 ref, PPU 1, integer upscale) | `DebugSceneCreator` | migrated-v2 | Governs future world-space sprites; UI canvases integer-scale via 640×360 CanvasScaler. |
| Motion / reduced-motion | `Motion` | placeholder | PlayerPrefs-backed; drives every stage animation. |
| Tweening (OutBack/OutCubic/OutQuad/InQuad coroutine util) | `Tweening` | placeholder | Lightweight stand-in for DOTween; carries the 18 §3 easings; swappable. |
| Art library (id → sprite) | `ArtLibrary` | legacy-cozy-noir | Indexes the v1 painterly assets; repoint at v2 sprites when regenerated. |
| Pixel fonts (Press Start 2P display/numbers, Silkscreen body/caption) | — | placeholder | v2 pixel fonts, wired into the diegetic stage. Spec-sanctioned fallbacks for m6x11/m5x7 (itch.io-gated). |
| Legacy fonts (Limelight display, Barlow body) | — | legacy-cozy-noir | Still on the temporary HUD overlay; drop when the HUD is repixeled. |
| Procedural UI kit (panel/button/bubble/tag/frame/vignette/glow) | `UiSpriteGenerator` | legacy-cozy-noir | SDF rounded kit; replace with pixel 9-slice in v2. |

## 6. Art asset banks (all v1 — regenerate for v2)

| Bank | Count | Status |
|---|---|---|
| Patron portraits | 60 | legacy-cozy-noir |
| VIP portraits | 20 | legacy-cozy-noir |
| Ingredient sprites | 46 (v2) | migrated-v2 — v1 painterly `Ingredients/` bank **deleted** (unused; `art.Ingredient()` never called); the 46 per-ingredient pixel bottles in `Bottles/` are the ingredient art now. |
| Tool sprites | 16 | legacy-cozy-noir |
| Icons | 3 | legacy-cozy-noir |
| Scene backgrounds | 3 | legacy-cozy-noir (v2 `club_bg` 640×360 + `counter` live in the diegetic stage) |
