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
| **Diegetic stage — BackgroundLayers** (nightclub interior: neon, windows+city, crowd, back-bar) | `DiegeticStage` | migrated-v2 | Real PixelLab club background (`club_bg`, 640×360, palette-quantized) fills the scene; opaque overscanned backdrop behind it. Procedural sky/crowd/neon kept as a fallback when unwired. |
| **Diegetic stage — BarCounter** | `DiegeticStage` | migrated-v2 | Real PixelLab bar counter (`counter`, polished amber-lit wood + chrome edge) cropped to its surface line and aligned to the bottle rest line. Flat procedural band kept as fallback. |
| **Diegetic stage — BottleRail** (8 bottle slots on the counter) | `DiegeticStage` | placeholder | Fitted to spec-18: 24×40 bottles, slot pitch 56 from x=88, base y=232. Draw/Select/Mix/Refresh choreography per 18 §3 (240ms OutQuad + 2px overshoot, 4px select-rise, 180ms InQuad exit). **All six ingredient-type bottles are real v2 pixel sprites** (Spirit/Bubbly/Sweet/Sour/Bitter/Garnish, 32×48, PixelLab→quantized); sprite bottles carry a baked rim, selection washes them cyan + a 4px rise. |
| Ingredient rail (UI card version) | DebugHud | legacy-cozy-noir | **Removed** — replaced by the diegetic BottleRail. |
| Info panel (night/wallet/target/score/mixes) | DebugHud | legacy-cozy-noir | Text overlay; restyle in v2 UI pass (module 16). |
| Patron shelf | DebugHud | legacy-cozy-noir | Portrait thumbnails + sell; portraits are v1 painterly. |
| Tool belt | DebugHud | legacy-cozy-noir | Tool sprites are v1 painterly. |
| Live recipe preview line | DebugHud | legacy-cozy-noir | |
| MIX / RESTOCK / SKIP→FAVOR / BOUNCER buttons | DebugHud | legacy-cozy-noir | Procedural rounded kit; retone in v2. |
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
| Ingredient sprites | 46 | legacy-cozy-noir (6 v2 pixel type-bottles live on the rail: spirit/bubbly/sweet/sour/bitter/garnish, 32×48) |
| Tool sprites | 16 | legacy-cozy-noir |
| Icons | 3 | legacy-cozy-noir |
| Scene backgrounds | 3 | legacy-cozy-noir (v2 `club_bg` 640×360 + `counter` live in the diegetic stage) |
