# LAST CALL — GDD Module 24: Service Flow & Presentation (v4, 2026-07-22)

> **STATUS 2026-07-27 — CURRENT.**
> Owns the service flow and the presentation the player touches.

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
3. **Carry it over (2026-07-22):** the finished drink appears as a **glass on the counter**
   (staged centre-bottom). **Drag it onto a customer** to serve them — a heavy, springy
   carry that leans into the motion (an AAA-feeling 2D drag). Dropping it on a seated
   patron hands it over; `ServiceJudge` (23 §4) resolves and the payment floats up. Clicking
   a customer now only **reads** their licence — serving is the drag.
4. **The glass is the drink (ruling 2026-07-28).** Only what has been poured into the
   serving glass can be carried or handed over. `ServeTo` used to tip an unpoured shaker in
   for you, which meant backing out of the flow served a customer a drink that had skipped
   the aim-and-spill pour entirely — the shaker is a step, not a drink. Now the counter
   stays empty until the pour is made, and closing the flow mid-build says so. The sim and
   the tests pour like everyone else; they simply pour perfectly, which was always their
   standing (they never had to aim).

### 3.5 The feel pass (2026-07-22) — interim physics, still procedural

Player note: *"the falling liquid is boxes that don't touch the vessel; the fill is too
fast; the filled shaker needs liquid physics; a dragged lemon should swing from the end
you hold."* Addressed on the placeholder art, ahead of the P8 re-skin:

- **The pour is a metaball fluid, not separate balls** (`MetaballFluid` +
  `Shaders/MetaballLiquid.shader`): every falling droplet and the pooled liquid feed one
  scalar field that the shader thresholds, so nearby blobs **melt into a single connected
  mass** as they approach each other and the surface. The stream reads as one flowing
  column; where it lands it throws an **organic soft splash** and melts into the pool.
- **The liquid gains volume and takes the glass shape**: the pooled body is a soft-topped
  rectangle clipped to the glass interior, its surface line set from the glass's real
  fill fraction — pour and the level rises like water filling a vessel.
- **The fill is slower** (`PourTimeScale` on the shaker, a gentler serve rate): a pour is
  a held, deliberate motion. Only the drawn volume slows; the floor's patience clock is
  untouched (it runs on its own tick).
- **The surface behaves like water in a glass** (2026-07-22): the pool's top is a live water
  line driven by a **shallow-water height-field** — a row of columns coupled as a wave
  equation, so a disturbance travels, **reflects off the glass walls**, and settles back
  flat, with a bright band of light riding it. Over it sits a **damped lateral slosh** (the
  water lags a moving glass). A landing pour punches a ripple where it hits; throwing the
  shaker slaps waves against the walls, then it calms. The shader samples the height-field
  per-x, so the surface tilts and waves inside the glass instead of being a flat lid.
- **Ice and lemon float inside the shaker** (2026-07-22 note: *"the added ice/lemon don't
  react as if they're inside the shaker while mixing"*): dropped pieces are buoyant bodies
  (`ShakerSolids`) that **bob at the drink's surface**, bounce off the tin's inner walls,
  and get **flung about when you shake** — the bounds move with the tin, so shaking sweeps
  them. Salt and sugar instead scatter and dissolve.
- **You grab the shaker itself and shake it** (2026-07-22 note: *"grabbing the shaker and
  shaking it freely left–right should be fun and lively; it's stiff now"*): the hold-pad is
  gone — grab the shaker and **throw it around**; it springs after the cursor with overshoot
  (loose and whippy), leans into the motion, and the drink (and its ice) sloshes with it.
  Cursor travel builds the shake energy, and the meter now **continues from what's already
  been shaken** instead of resetting to zero each time you grab it.
- The **menu is stripped to essentials** (2026-07-22): the ICE/LEMON/SALT/SUGAR buttons and
  the SHAKE button are gone from it — those are hands-on in the shaker stage now (drag a
  piece in, grab and shake). The menu just picks bottles and moves on.
- **Dragged pieces have weight and swing** (`Pendulum` + a spring grip): the grip
  **springs after the cursor with overshoot** (it lags and jiggles) and the body hangs
  and **swings from that grip** — grab a lemon by one end and the free end sways, then
  settles.
- The spill still lives in the **serve** aim: off-target, the stream drifts wide, misses
  the rim and falls past onto the counter. GDD 21 §3 brim holds inside the glass.

**The drawn level has to equal the stated one (2026-07-28).** Player note: *"the poured
amount doesn't show the vessel as full."* It didn't: a tin the rules called 100% drew to
about **72%** of its cavity. Two separate faults, both measured. The solver's particle-count
estimate assumed an ideal packing (a settled particle really takes ~0.71·spacing² of area),
and the body was genuinely **compressed** — at 14 relaxation passes the pressure never
reached the top of a tall column, so the tin and the pint stopped ~10% short while the
stubby tumbler was fine. On top of that the shaker stage was *deliberately* drawing a ninth
low to hide it. Fixed at the source: an honest estimate, more relaxation passes, and a
coarser particle scale to pay for them — **1007 particles at 22 passes costs 10.2 ms/frame
against the old 1414 at 14 for 10.3 ms**, the same frame drawn truthfully. A full vessel now
stands at its rim, and every fill in between is within a few percent of the number beside it.

**And it has to hold the frame rate (2026-07-28).** Player note: *"shaking with a lot of
liquid drops the FPS a lot — this is a 2D game, it should never fall below the average."*
Correct, and it was the fluid: with a full tin the frame went **1.8 ms → 12.5 ms (80 fps)**.
Not the metaball shader, which is where the blame would naturally fall — turning the drawing
off entirely changed nothing. The solver is O(particles × relaxation passes), so all three
levers were pulled: the pair sweep now walks the **forward half** of each neighbourhood
(every pair was being visited twice to be used once), the particle scale is **coarser**
(556 instead of 1007 — the blob radius scales with it, so the drink looks the same), and a
vessel **being shaken relaxes fewer times**, since the passes buy an accurate settled level
and nobody reads the level mid-slosh. Result: **5.7 ms (175 fps)** in the shaker, **4.6 ms
(215 fps)** at the tap, and the shake case — the one reported — is now the *cheapest* state
rather than the most expensive. The fill accuracy above was re-measured live in all three
stages afterwards and holds.

**A vessel and its contents are one object (2026-07-28).** Player note: *"the glass and
shaker art can come apart from the invisible vessel the liquid is in, while shaking."* They
could: a vessel's drinkable cavity is not centred on the sprite's pivot — the tin's runs from
0.09 to 0.61 of its height — and the sprite turned about its pivot while the liquid turned
about the cavity's own centre. At the 24° a shake reaches, that put the drink **~19 px** out
of an 84 px-wide tin. Wherever a leaning vessel's interior is measured it is now swung about
the pivot the art actually turns on, and the drink is placed **after** every vessel has
finished moving for the frame (it used to be placed before the cap animation slid and grew
the tin, so it trailed a frame behind). Verified against the RectTransform itself rather than
against the same maths: 0.00 px apart at every angle, in both stages, and while moving.

The fluid is a 2D metaball drawn on a UI RawImage — a CPU droplet cloud feeding a threshold
shader, chosen over a Shuriken/RenderTexture rig because it composites cleanly inside the
ScreenSpace-Overlay Canvas and shares the tilt-pour's local coordinates. Solids (ice/lemon
settling) still use the `Splasher`. All hand-integrated in UI space (`DrinkPhysics.cs`,
`MetaballFluid.cs`) — Unity's Physics2D can't reach Canvas RectTransforms. Cosmetic only:
the poured **volume** is still the deterministic tilt-pour.

## 4. The seats

**Commitment (2026-07-22, explicit request): customers are physical characters in the
scene.** The P3 seat panels are interim UI only — the P8 pass replaces them with sprites
who visibly **walk in, sit down at the counter, order, wait, react and leave**, gauges
attached to the person, not to a box. Seeing someone sit down at your bar is the game's
heartbeat.

**Interim step (2026-07-22):** the bottom seat strip is gone — customers now **sit at the
counter** as head-and-shoulders busts (their archetype portrait), bodies cut off by the bar
top, each with a **floating order tag** above the head (name · read · order + price ·
patience) and the face **souring red over the last third of their patience**. A new arrival
**slides in from the left and fades up** into their stool; the tag glows cyan when a drink is
ready to hand over; clicking the bust reads or serves. The room reads as *people at the bar*,
using portrait art that already exists (keyed by archetype, as the licence photo is). Full
per-customer bodies with walk-in/react/leave animation and body-attached gauges are still the
P8 art pass; this is the bridge.

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

### The stage's two big surfaces (2026-07-29)

Both were remade, and they turned out to be wrong in two different ways.

**The backdrop was the wrong SIZE.** A 592×336 image was being stretched across a 736×456
rect — 1.24× wide, 1.36× tall. Non-uniform, so no pixel of it was square, and the room stood
9% too tall. Redrawn at **640×360**, the stage's own reference, and scaled by a single factor
to cover: at 16:9 that factor is exactly 1, so one art pixel is one canvas unit and lands on
a whole number of screen pixels (measured ×4 at 1440p). Import settings were already right —
point filter, no compression — which is why the loss was purely geometric.

The room is an **open-air rooftop terrace**: a railing, a distant night city below it, strings
of small warm bulbs overhead, potted plants. It is deliberately the darkest thing on screen.

**The design rule came out of the layout, not out of taste.** The counter owns y 0–140 of the
360-unit stage and the seated customers roughly y 130–260, so the only band the frame never
covers is the top one. Everything the eye is allowed to notice — the lights, the sky, the sign —
lives up there, above every face the player has to read. Below that the scene is quiet: no
people, no pattern at eye level, nothing that competes.

Six rooms were drawn before this one, and each was rejected for a reason worth keeping:
a booth lounge read as dead; an art-deco ballroom as too lavish and too crowded; a symmetric
velvet room as a *corridor*, because a centred vanishing point turns a bar into a tunnel; a
brick room as a medieval cellar. Standing figures in the backdrop always looked wrong — the
customers are the people in this game, and a second set of them competes.

Being outdoors is also what makes the weather simple: there is no window to see it through, so
the sky and the rain simply *are* the scene, and the mask is skipped entirely.

### The backdrop moves (2026-07-29)

It is a **stack of layers**, not a painting, because everything in it has to animate:
`StageBackdrop` owns sky, city, street, lamp, rain and signs as separate objects.

- **sky** drifts and wraps. It is drawn twice with the second copy **mirrored**, so the join is
  seamless by construction — no seamless-tiling art required.
- **rain** is 90 streaks that fall, lean, and are re-thrown when they land or blow out of frame.
- **wind** is one shared value wandering slowly between −1 and 1. It leans the rain *and* hurries
  the clouds, so the weather reads as one thing happening rather than two effects running.
- **neon** signs blink on their own clocks — long lit, dark for an instant, easing rather than
  snapping. Two signs pulsing in step is what gives a fake away.
- **the street lamp** breathes, with a faint tremor and a rare hard flicker.

Everything outdoors is clipped by a **mask sprite**, not a rectangle: the windows are on side
walls and therefore drawn in perspective, and one mask holds *both* panes, so a single clip
serves them. The mask is the glass cut out of the room art itself, which is why the two agree
exactly. The whole thing is cosmetic, never touches the run, and stops dead under
`Motion.Reduced`.

**Sign lettering is drawn in-engine, never generated.** The art supplies an empty tube frame;
the word is set in the pixel display face with a soft copy behind it for glow, and both blink
with the frame. The generator cannot spell — asked for a bar it produced one called THE REEF —
so any sign that has to *say* something is built, not painted.

**The counter was the wrong THING.** The asset was a back-bar shelf unit — glass racks and
bottle shelves, furniture for the wall *behind* a bartender — installed as the bar we stand at.

It carries **empty shelves**, and that is structure rather than decoration: glassware is a
buyable upgrade (23 §8), so those evenly divided, deliberately empty compartments are where the
bought glasses get drawn. Two brass-railed rows of them under a teal bar top with a brass edge,
sharing the room's own teal and gold.

Measured off the art, in its own 640×150 pixels, for whoever fills them: the **upper row** has
8 compartments about 68px wide centred at x = 37, 113, 193, 280, 357, 444, 524, 601 (row ~70);
the **lower row** has 7, one of them double-width where a divider is missing (row ~125). Read
them again if the bar is ever redrawn — they are a property of the picture, like the surface
line above.

(It was briefly built from the palette in code instead. That version is gone: it answered the
shape problem but not the brief, because a built band has nowhere to put a glass. The generator
does need watching — across four rounds it produced perspective views that ended inside the
frame, near-empty canvases, and shelves with holes of pure transparency in them, which are
filled in post from each column's first opaque row down.)

**Where a glass stands is measured, never assumed.** `CounterSurfaceInset` is the distance from
the sprite's top down to the bar's far edge — the line a glass is set on, pinned to
`CounterRestY`. The two bars drawn for this put that edge 2px and 54px down, so it is a property
of the picture: read it off the art whenever the art changes, or the glassware floats.

**Replacing stage art means keeping its GUID.** Unity binds the scene's sprite fields by GUID,
not by path, so re-adding `counter.png` under a fresh one left `counterSprite` null and the bar
simply did not draw — with no error, because the procedural fallback had been removed in the
same pass. Keep the original GUID in the `.meta` when swapping a file in place, and note that
an already-open scene caches the broken reference: it has to be reloaded before the fix shows.
`DiegeticStage` now logs a warning when the sprite is missing, so the next time this happens it
says so instead of drawing nothing.

## 9. Tutorial (the opening shift)

Scripted first shift with **fixed teaching customers**: one per starting drink — each
teaches "how to build it" (menu → shaker → serve) and "who it is for" (its emotion
identity), one concept at a time. The last teacher introduces the ID/read. Then Day 1
begins unscripted. Skippable for returning players.
