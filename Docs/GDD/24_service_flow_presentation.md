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

**The hands-on rule (2026-07-22, explicit request): the player performs the motions with
the mouse.** Buttons select; the mouse *does*. Every step below is a physical interaction.

1. Click a bottle in the menu → the screen **dims**; a focus vignette shows the **open
   shaker** and the chosen bottle, big.
2. **The tilt-pour:** grab the bottle with the mouse and **lift it — the higher it goes,
   the further it tips** toward the shaker. Liquid streams from the neck only while the
   mouth lines up over the shaker's opening; more tilt = faster pour (and a jumpier
   stream — speed is risk). Lower the bottle to stop.
3. Closing the focus returns to the menu, so stacking a second bottle is two clicks.
4. **Preparations are dragged, not toggled:** the ice bucket / lemon bowl / salt cellar /
   mint sprigs sit beside the shaker; **pick a piece up and drop it into the shaker's
   mouth**. A miss bounces off the counter (no cost, a small clatter).
5. **The shake is real:** grab the shaker and **shake the mouse** — the shaker follows
   with a weighty animation. Shake *energy* (how hard, how long) is measured and recorded
   with the `shaken` preparation as a 0–1 quality; under- and over-shaking are future
   craft hooks. The interim P4 UI approximates all of this with hold-zones and buttons
   until the P8 interaction pass replaces it.

## 3. Serving: the glass and the pour

1. Serve stage dims the screen again: a **glass** (auto-selected by drink family, later)
   and the shaker.
2. **The serve pour is also hands-on:** grab the shaker and **guide/tip it toward the
   glass** — same tilt model as the bottle: raise to tip, line the stream up over the
   glass mouth. Off-target liquid **spills** and is lost (this is where spilling lives
   now — the *aiming* game, not the filling game; GDD 21 §3's brim rule still holds
   inside the glass).
3. **[SERVE]** → click the seat/customer to deliver. `ServiceJudge` (23 §4) resolves.

### 3.5 The feel pass (2026-07-22) — interim physics, still procedural

Player note: *"the falling liquid is boxes that don't touch the vessel; the fill is too
fast; the filled shaker needs liquid physics; a dragged lemon should swing from the end
you hold."* Addressed on the placeholder art, ahead of the P8 re-skin:

- **Pouring is a continuous stream, not a spray of boxes** (`PourStream`): a wavy ribbon
  from the mouth down to the **current liquid line**, narrowing as it falls, and it
  throws a small **crown/splash** where it lands — the pour now visibly *meets the drink*.
- **The fill is slower** (`PourTimeScale` on the shaker, a gentler serve rate): a pour is
  a held, deliberate motion. Only the drawn volume slows; the floor's patience clock is
  untouched (it runs on its own tick).
- **The shaker liquid has a surface that moves** (`_shakerSurface` + `UpdateSlosh`): it
  rocks gently at rest and heaves while shaking or pouring.
- **Dragged pieces swing as a pendulum** (`Pendulum`): the grip rides the cursor, the body
  hangs and lags — yank the hand and the free end sways, then settles.
- The spill still lives in the **serve** aim: off-target, the stream drifts past the rim
  and splashes on the counter (a bigger splash the worse the aim). GDD 21 §3 brim holds.

All hand-integrated in UI space (`DrinkPhysics.cs`) — Unity's Physics2D can't reach Canvas
RectTransforms. Cosmetic only: the poured **volume** is still the deterministic tilt-pour.

## 4. The seats

**Commitment (2026-07-22, explicit request): customers are physical characters in the
scene.** The P3 seat panels are interim UI only — the P8 pass replaces them with sprites
who visibly **walk in, sit down at the counter, order, wait, react and leave**, gauges
attached to the person, not to a box. Seeing someone sit down at your bar is the game's
heartbeat.

Up to 6 customers visible at the bar, each an animated character with states:
**walk in → sit → order (speech bubble with the drink's name/icon) → idle/talk → drink →
react (happy / annoyed / angry) → pay → leave**, plus **storm-off**. Two gauges per seat:
satisfaction bar and the **patience clock icon** counting down. Reactions must read at a
glance — anger is animation first, numbers second.

## 5. The ID card — v2 shipped, v3 owed to the art pass

> **Player feedback (2026-07-22):** the v2 licence works but is not liked — the rows read
> cluttered and the proportions are wrong. **v3 (P8): a real ID-card-proportioned prop**
> (landscape licence ratio), purpose-drawn card art, one strong reading order. Do not
> iterate v2 further; redesign it whole with the art pass.

### v2 as shipped (P6, 2026-07-22)

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
  **Readability rules (2026-07-22): short labels, big type, the bill front-and-centre.**
- **Market**: rotating offers as shelf cards (existing market visual language).
  **You cannot buy what you cannot pay for**: unaffordable cards are visibly disabled and
  a click raises a "NOT ENOUGH MONEY" notice. Only rent can push the till below zero.

## 10. Time and feedback on the floor (2026-07-22)

- **Menus slow the world:** while the service flow or a licence is open, floor time runs
  at **×0.3** (`TycoonConfig.MenuTimeScale`) — building a drink must not cost a storm-off
  by itself, but the clock never fully stops: haste still matters.
- **Money is celebrated:** every payment floats a green **+$N** up from the seat that
  paid it. Costs land on the invoice, never as floaters.
- **Arrival pacing** is a first-class balance knob (P9): gaps that breathe — busy pulses
  with recovery valleys, never a metronome and never a flood.

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
