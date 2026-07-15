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
| **Diegetic stage — BackgroundLayers** (far club/crowd, mid neon/light, front counter face) | `DiegeticStage` | placeholder | 3 parallax-ready layers, flat v2-palette silhouettes. |
| **Diegetic stage — BarCounter** | `DiegeticStage` | placeholder | Amber-lit counter band across the screen bottom. |
| **Diegetic stage — BottleRail** (8 bottle slots on the counter) | `DiegeticStage` | placeholder | Replaces the UI ingredient rail; bound to the round's rail cards; slide animations. |
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
| Motion / reduced-motion | `Motion` | placeholder | PlayerPrefs-backed; drives every stage animation. |
| Tweening (OutBack coroutine util) | `Tweening` | placeholder | Lightweight stand-in for DOTween; swappable. |
| Art library (id → sprite) | `ArtLibrary` | legacy-cozy-noir | Indexes the v1 painterly assets; repoint at v2 sprites when regenerated. |
| Fonts (Limelight display, Barlow body) | — | legacy-cozy-noir | Replace with a pixel bitmap font in v2. |
| Procedural UI kit (panel/button/bubble/tag/frame/vignette/glow) | `UiSpriteGenerator` | legacy-cozy-noir | SDF rounded kit; replace with pixel 9-slice in v2. |

## 6. Art asset banks (all v1 — regenerate for v2)

| Bank | Count | Status |
|---|---|---|
| Patron portraits | 60 | legacy-cozy-noir |
| VIP portraits | 20 | legacy-cozy-noir |
| Ingredient sprites | 46 | legacy-cozy-noir |
| Tool sprites | 16 | legacy-cozy-noir |
| Icons | 3 | legacy-cozy-noir |
| Scene backgrounds | 3 | legacy-cozy-noir |
