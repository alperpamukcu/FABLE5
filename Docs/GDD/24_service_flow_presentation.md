# LAST CALL — GDD Module 24: Service Flow & Presentation (v4, 2026-07-22)

> Companion to module 23 (which owns the rules). This module owns **how making a drink
> feels**: the menu, the shaker, the pour, the seats, and the art bar the whole game is
> being raised to. Animation quality is a design pillar now, not a polish item.

## 1. The bottles leave the screen

The back-bar shelves stop being buttons. Bottles live in a **menu**: a menu prop sits on
the counter; clicking it opens the **drink menu UI** — the player's current bottles as a
readable list (name, style colour, price, remaining volume). This declutters the stage for
the seats and makes the shelf feel like *stock*, not UI.

## 2. Building a drink: the shaker flow

1. Click a bottle in the menu → the screen **dims**; a focus vignette shows the **shaker
   and the chosen bottle**, big.
2. **Hold to pour** into the shaker — pour animation, liquid stream, shaker fill readout
   (the ratios UI carries over from GDD 21 §3.1).
3. Closing the focus returns to the menu, so stacking a second bottle is two clicks.
4. Before shaking, **preparations** go in: ice, lemon, salt, sugar (GDD 22 §5 plumbing
   finally gets its UI).
5. **Shake:** press the shake button, then hold and *move the mouse* — the shaker follows
   with a weighty animation; shake duration/energy is a craft input (reserved for future
   effect hooks).

## 3. Serving: the glass and the pour

1. Serve stage dims the screen again: a **glass** (auto-selected by drink family, later)
   and the shaker.
2. **Hold to pour from the shaker** — a real, physical-feeling stream: tilt animation,
   liquid arc, and if the player pours off-target **it spills** (spilled volume is lost;
   this is where spilling lives now — the *aiming* game, not the filling game; GDD 21 §3's
   brim rule still holds inside the glass).
3. **[SERVE]** → click the seat/customer to deliver. `ServiceJudge` (23 §4) resolves.

## 4. The seats

Up to 6 customers visible at the bar, each an animated character with states:
**walk in → sit → order (speech bubble with the drink's name/icon) → idle/talk → drink →
react (happy / annoyed / angry) → pay → leave**, plus **storm-off**. Two gauges per seat:
satisfaction bar and the **patience clock icon** counting down. Reactions must read at a
glance — anger is animation first, numbers second.

## 5. The ID card, v2 — SHIPPED (P6, 2026-07-22)

Readability pass (explicit request): tap any seated customer to open their licence — a
large cream card with **photo, big NAME, AGE, FROM (city), relationship + demand**, the
**ORDER**, an amber WANTS band (intent · glass length), and the **six emotion stats as
full-width rows** — a coloured word tag, a 0–100 track showing the reading (Exact tick /
Range span / Unknown empty), and a big value (number / "40–60" / "??"). The intent stat
wears a ★. Reading is the empty-handed seat click; serving is the drink-in-hand click.

## 6. Upgrades you can see

Every purchase changes the scene (23 §8): new stools appear, the counter/wall art swaps,
better glassware shows in the serve stage, and **the musician** takes the corner stage
with an ambient playing loop. The scene is the save file, visually.

## 7. Day end screens

- **Invoice UI**: a printed bill — income lines (drinks, tips), expense lines (refills,
  rent, purchases), net in big type, debt-strike warning stamps (1/3, 2/3, CLOSED).
- **Market**: rotating offers as shelf cards (existing market visual language).

## 8. Art direction v3 (the new bar)

- **Reference: Dave the Diver-level pixel density and motion** — that is the floor, not
  the ceiling. Current 640×360 stage logic is retired for art; new authoring reference is
  **1280×720 logical**, sprites drawn at final display size, no upscaled placeholders in
  the final set.
- **Animation is critical**: characters 8–12 frame cycles for idle/walk/react; liquid
  pours are animated streams, not rectangles; UI transitions ease, never snap
  (Motion.Reduced still collapses everything to instant).
- **Consistency rule**: every object on screen shares one style and one texel density —
  nothing may "sırıtmak". All current sprites (bottles, patron, register, glass, bg) are
  **placeholders from today**; they stay until the v3 pass (PLAN §P8) replaces the whole
  set together, not piecemeal.
- Background: animated and alive (crowd, neon, musician when bought) but low-contrast and
  slow — attention belongs to the seats and the counter.

## 9. Tutorial (the opening shift)

Scripted first shift with **fixed teaching customers**: one per starting drink — each
teaches "how to build it" (menu → shaker → serve) and "who it is for" (its emotion
identity), one concept at a time. The last teacher introduces the ID/read. Then Day 1
begins unscripted. Skippable for returning players.
