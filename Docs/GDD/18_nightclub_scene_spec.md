# LAST CALL — GDD Module: Nightclub Scene & Bottle Rail Spec (v2)

> ⚠️ **PARTIALLY STALE (2026-07-20).** The camera flipped to the **customer's side of the
> bar**: bottles now live on two back-bar wall shelves (spirits up, mixers down), not on the
> counter, and the "camera faces the club" note in §1 is dead. GDD 22 §1 and the layout
> constants in `DiegeticStage.cs` are the source of truth for the current stage. The layer
> stack, choreography timings and motion rules below still apply.

> The gameplay screen is a DIEGETIC scene: the player IS the bartender of a nightclub. The rail is not an abstract card strip — it is bottles standing on real shelves. This doc is the source of truth for scene layers and the slide choreography. Authored at 640×360.

## 1. Layer stack (back → front, with parallax factors)
| # | Layer | Content | Parallax | Motion |
|---|---|---|---|---|
| 0 | Sky/City | dithered vice sunset→night gradient, skyline+palm silhouettes through tall windows | 0.0 | static |
| 1 | Club Far | dance floor crowd silhouettes (Night 2 ramp), sweeping club lights | 0.1 | crowd 2-frame idle @ 0.8s; light beams 4-frame sweep @ 2s, magenta/cyan alternating |
| 2 | Club Mid | pillars, neon wall sign "LAST CALL" (Magenta ramp), booth glow | 0.2 | neon flicker: random 1-frame off every 3-7s |
| 3 | Back Bar | shelf wall behind player-POV? NO — see note | — | — |
| 4 | Counter | bar counter top+front (amber-lit wood, chrome edge), occupies bottom 96px | 0 | static |
| 5 | Bottles | 8 rail slots ON the counter | 0 | see §3 |
| 6 | Customer | patron/VIP sprite standing across the counter, center-left, 96×128 | 0 | 2-frame idle @ 1s; reaction frames (neutral/impressed/annoyed) |
| 7 | FX | pour particles, glass, score numbers, smoke wisps | 0 | per-event |
| 8 | UI Overlay | HUD, panels, modals per 16 v2 | 0 | per 16 §6 |
Note: camera faces the CLUB (player looks outward from behind the bar) — customer in front, club behind them. No back-bar shelf layer needed; bottles live on the counter.

## 2. Screen layout (640×360)
- Counter surface line at y=264 (bottom 96px = counter front + UI strip).
- 8 bottle slots on the counter: slot pitch 56px, first slot x=88, bottles stand at y=232 (base on counter line).
- Customer anchor: x=200-296, standing behind counter center-left; speech/order ticket panel to their right.
- HUD: top-left compact stats block; Patron shelf top strip (48×64 cards, max 5+); actions bottom-right (MIX primary, RESTOCK secondary); recipe preview bottom-center above slots.

## 3. Bottle rail choreography (all translations, whole pixels, DOTween)
- **Draw (refill):** new bottles enter from RIGHT edge, slide along the counter to their slot; 60ms stagger per bottle, 240ms OutQuad each, tiny 2px overshoot settle (frame swap, not scale). Slide accompanied by glass-clink SFX per landing.
- **Select:** bottle rises 4px, cyan rim pixels on, soft tick.
- **Mix:** selected bottles slide forward+up to the mixing area (center, above counter), pour sequence per 15 §5 (tilt via pre-rotated sprite frames: 0°/-30°/-55°, NOT runtime rotation), stream particles, glass fills; then empties slide LEFT off-screen (180ms, InQuad).
- **Restock:** selected bottles slide left off-screen; replacements enter from right per Draw.
- **Between customers / new night:** entire counter sweeps: remaining bottles slide left out, full new set cascades in from right (the "next shift" wave, 400ms total) — this is the player's turn-transition heartbeat.
- All timings ×0 under reduced-motion (instant placement).

## 4. Ambient life budget (subtle, never noisy)
Max 3 concurrent ambient animations: club light sweep, neon flicker, ONE of (crowd idle / smoke wisp / customer idle). Everything else static. Ambient pauses during score cinematics so the number moment owns the screen.

## 5. Asset checklist for this scene (PixelLab targets)
sky_city 640×360 · club_far 640×360 (+2 crowd frames) · club_mid 640×360 (+neon on/off) · counter 640×96 · 48 bottle sprites 24×40 (per ingredient, 3 tilt frames for spirits used in pours) · glass+shaker 32×32 (+5 fill frames) · customer sprites 96×128 (idle 2f + 3 reactions) · light-beam overlay 4f · smoke wisp 3f.
