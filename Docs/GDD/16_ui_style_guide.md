# LAST CALL — GDD Module: UI Style Guide v2 — PIXEL SYSTEM (supersedes v1)

> LAW for all UI. Authored at 640×360; all values in 1× pixels. Palette/ramps from 14_art_bible v2. Engine-drawn primitives are BANNED for visible UI — every panel, button, tab, bar is a hand-authored (or AI-generated+cleaned) pixel sprite, 9-sliced where stretchable.

## 1. Design tokens
- **Spacing grid: 4 px.** Allowed: 2/4/8/12/16/24/32. Everything grid-snapped.
- **Radii:** pixel-corner style — corners cut 2px (small) or 3px (large) with a corner pixel cluster; no smooth arcs.
- **Fonts (pixel, integer scale only):** BODY = m6x11 @1× (11px line) · CAPTION = m5x7 @1× (7px, tooltips/tags only) · HEADINGS = m6x11 @2× · SCORE/NUMBERS = custom chunky 12px numeral set @1×/2×/3× (Display duty). Alternative if licensing blocks: Press Start 2P @1× for headings. NEVER sub-pixel, never anti-aliased.
- **Elevation:** L1 = 1px offset shadow (Night 1 #0D0813) · L2 floating = 2px · L3 modal = 3px + scrim (#0D0813 at 70%, dithered edge).
- **Text colors:** Cream 5 primary · Cream 4 secondary · Night 3 on amber fills. Min text size = 7px caption; UI never renders paragraphs — max 2 short lines, rest goes to tooltip.

## 2. Color roles (unchanged hierarchy, remapped)
Primary action (ONE per screen): Amber 4 fill · Secondary: transparent + 1px Cream 3 border · Destructive/VIP heat: Magenta 4 / Vice Red 4 · Selection/info: Cyan 4 glow pixels · Disabled: ramp step −2 + no shadow. Money = Amber, Flavor = Cyan, Mult = Magenta — these three number colors are sacred and never reused elsewhere.

## 3. Pixel button anatomy (the ONE button, 9-slice)
1px outline (darkest ramp step) → 1px top highlight row (ramp +1) → flat fill (no gradients) → 2px bottom lip (ramp −1) → 1px L1 shadow.
- **Hover:** fill shifts +1 ramp step; 2-frame neon rim blink on left/right edges. **Pressed:** sprite translates down 1px, lip 2px→1px, fill −1 step. **Focus (pad):** 1px Cyan 4 marching-ants border (4-frame loop). **Disabled:** −2 steps, no lip.
- Sizes: L 20px h / M 14 / S 10 (icon button 12×12). Padding 8px horizontal min. Labels: BODY font, UPPERCASE.

## 4. Panels, tabs, tables
- Panel: 9-slice pixel frame, 1px outline + 1px inner highlight top, fill Night 3; 8px inner padding. Nested = fill +1 step.
- Tabs: physical tab shapes overlapping panel top edge; active tab connects (no bottom border), inactive −1 step + 1px lower.
- Tables/lists: zebra rows (Night 2/3), 12px row height, icon-first cells (8×8 type icon + number), text last resort.
- Tooltips: CAPTION font, Night 2 fill, 1px Cream 3 border, max 160px wide.

## 5. Icon-first communication (the "less text" law)
Every recurring concept has an 8×8 icon: 6 ingredient types, coin, Flavor drop, Mult ×, mix shaker, restock arrows, lock, level pip. Rules text pattern: `[icon][number]` chains (e.g. "🍋+15 ⚡×2" style) — full sentences ONLY in tooltips. Recipe preview shows: recipe name + [Flavor icon][n] × [Mult icon][n] — no prose. Numbers count up via tween, Display numerals, color-coded per §2.

## 6. Motion (pixel-adapted)
Durations unchanged: 90/180/300 ms. NO smooth scaling: pops = 2-3 frame sprite swaps (small→over→settle); lifts/slides = pure translation on whole pixels (move in 1× pixel steps, position snapped); flashes = 1-frame palette swap to ramp top. Selected bottle: rises 4px + Cyan rim pixels on. Score slam: screen shake ±2px (3 frames) + amber flash frame. Reduced-motion: swaps become instant, no shake.

## 7. Acceptance checklist (per screen, with 1× and 3× screenshots)
- [ ] Every visible UI element is a pixel sprite (zero engine primitives)
- [ ] All colors from the 40-palette; ramps respected; no off-ramp shading
- [ ] Grid: spacing on 4px, sprites on whole pixels, no non-integer scaling anywhere
- [ ] One amber primary per screen; number colors sacred (Amber/Cyan/Magenta)
- [ ] Icon-first: no prose outside tooltips; fonts integer-scaled pixel fonts only
- [ ] Hover/pressed/focus/disabled states present per §3
- [ ] Motion = translation/frame-swap/flash only; reduced-motion path works
- [ ] Side-by-side hierarchy check vs Balatro at equal zoom
