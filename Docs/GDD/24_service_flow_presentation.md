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

**The head is fluid too (2026-07-30).** Player note: *"the foam still looks like a rectangle,
it doesn't look like liquid."* It was one: a tiled `Image` laid over the beer, with straight
sides, square corners, and a rule that it *"never rotates — it only narrows"*. Foam is now made
of the **same particles as the beer** in the same solver (`MetaballFluid`), so beer and head
share one thresholded surface and there is no second object to have edges. What separates them:

- **Foam is light** — it feels about half of gravity, so a glass of pure froth still fills from
  the bottom rather than clinging to the rim.
- **Beer and foam sort by EXCHANGE** — a symmetric minimum-distance constraint cannot separate
  by density, so an overlapping unlike pair also exchanges along gravity until the foam is on
  top. The exchange is driven by *how badly the pair is out of order*, not by how much it
  overlaps, and that distinction is the whole thing: driven by overlap it keeps pushing after
  the layers have sorted and levers them apart, and a gap between the layers is a gap in the
  metaball field — it draws as **black holes through the drink**. Driven by mis-ordering it
  falls to nothing the moment the foam is above the beer, so the two sort and then stay in
  contact. Measured after deliberately thrashing the glass: **0 of 142** bubbles left under the
  beer, worst layer gap 7.5 px against the ~19 px at which a hole opens.
- **Buoyancy is measured from the beer ABOVE a bubble**, not the beer around it. "Around" cannot
  tell a buried bubble from one resting on the surface, and lifting the resting ones pushed the
  whole underside of the head up off the beer.
- **Froth is not a lattice** — foam relaxes only part way against foam, is damped harder, and
  gets its own per-particle ceiling, because a single shared ceiling is a hard clamp that drew a
  **ruler** across the head. That one line was most of the flat top edge (crest relief 1.6 px →
  8.7 px). Foam's viscosity blends it toward *other foam only*: blending a bubble toward the
  beer around it erased the rise buoyancy had just given it, and a stirred-in head could never
  climb back out.
- **Bubbles are coarse, but not so coarse the head stops being a layer** — 0.8× the particles at
  1.26× the radius. Cut to the 0.63 at which the covered area is exactly right, the head draws
  its true depth but opens 22 px holes in a partly-full glass; the generous figure costs a head
  drawn about a quarter deep and buys one with no holes in it. Only one of those looks like a
  bug.

The head therefore leans, wobbles, crowns over the rim and melts into the beer along an
irregular line, because it is liquid — and once poured it goes completely still (0.0 px/s).
Cost went **down**: 1.2–2.3 ms against the 4.6 ms the tap measured before, since foam needs
fewer particles than the beer it displaces.

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

## 5. The ID card — v3 shipped (P15, 2026-07-31)

A **landscape licence** on generated card art (the first UI piece after the no-AI-UI rule
was lifted): guilloche shell, navy header band, portrait window left, stamp watermark —
generated blank at 266×176 and drawn at exactly **3×**, matching the pixel faces' 8px→24px
scale so shell pixels and glyph pixels come out the same size. All lettering is set in
engine (the generator cannot spell). Anchors are measured off the art: window frame
x 15–89 / y 37–132, header rows 3–21.

Reading order, top to bottom: **NAME** big · AGE·CITY / STANDING · RATES THIS BAR (their
own satisfied-visit share as stars — what THEY make of US) / reserved slots (favourite ·
last visit · spent, blank until save arrives, P18) · the **ORDER band** with the drink's
icon · **SERVING PREFERENCES** (garnishes + shaken hard + filled to the top — everything
the tip grades, in one line). **No price anywhere** (C3), and **opening the card is the
`InspectId()` that unlocks the order** — before it, Core refuses and the seat bubble shows
only READY · TAP THE ID. Emotion stat rows left the card per D1; the tell moves to the
reaction animations. The scrim click hands the card back.

> **Player feedback (2026-07-22)** that drove this: v2's rows read cluttered and the
> proportions were wrong; the ask was a real ID-card-proportioned prop with one strong
> reading order, redesigned whole. v2's description kept below for the record.

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

### The tap is a station, not three props (2026-07-30)

Player note: *"the keg and tap assets are low quality and do not fit the scene."* They were:
the tower was a flat yellow post, and the "keg" was the beer **menu icon** (`lager.png`) blown
up to prop size — the stage had no keg art at all. The three objects floated in an empty box
with no surface under them and nothing connecting them.

The stage is now a working bar station, built back to front: the counter with the room's own
brass edge, the under-bar recess divided into bays, the keg standing in one with its line
running up into the foot of the font, the drip tray, the tower bolted to the bar top, and the
pint standing on it. Everything rests on the counter line rather than hovering at a hand-picked
y, and the beer line is what makes the keg and the tap read as one plumbed-in rig.

New art (`keg`, `drip_tray`, and replacements for `tap` and `tap_handle`) was generated against
the **shaker sprite as the style reference**, which is what puts it in the same world as the
rest of the props rather than beside it — 31–47 colours each, against the shaker's 81 and the
old tap's 170, and no semi-transparent edge pixels. Every sprite is trimmed to its content
bounds; padding is what makes a prop float (the same fault as the till, 2026-07-29).

The keg's label is **blank in the art and lettered in engine**, tinted by the beer's style —
the generator cannot spell, so every word in this game is drawn with the pixel font. The same
rule the neon sign follows.

Anchors are measured off the art, never guessed: the faucet lip is the leftmost opaque pixel of
`tap.png` at (−39, +34.5) from its centre, and the handle mounts on the brass fitting at
(−29, +87.5), both scaled by the size the tower is drawn at.

**The font has to tower (2026-07-30).** Player note: *"the tap is far too small next to the
glass."* It was drawn the same height as the pint, and a bar font is around 450 mm against a
glass's 160 — at 1:1 it reads as a toy. The panel grew to near the full canvas and the counter
dropped so the tower could stand **1.55×** the glass with its handle above that again. The glass
cannot be shrunk to buy the ratio: its size is calibrated to what the fluid solver fills. What
caps the ratio is the keg — every unit the counter drops for the tower comes off the under-bar
recess, which has to stay deep enough to show the keg's label.

The under-bar is a **viewport**: a keg is taller than the hatch it stands in, so the hatch crops
it, and it runs off the bottom of the frame because this is a close-up of the bar top and its
foot is simply not in shot. The mask goes on the recess and *not* on the whole surface — a Mask
over the surface clips the keg beautifully and empties the glass, because Unity hands a masked
Graphic a stencil-modified **copy** of its material while `MetaballFluid` goes on writing its
particle array to the original.

### The stage's two big surfaces (2026-07-29)

Both were remade, and they turned out to be wrong in two different ways.

**The backdrop was the wrong SIZE.** A 592×336 image was being stretched across a 736×456
rect — 1.24× wide, 1.36× tall. Non-uniform, so no pixel of it was square, and the room stood
9% too tall. Redrawn at **640×360**, the stage's own reference, and scaled by a single factor
to cover: at 16:9 that factor is exactly 1, so one art pixel is one canvas unit and lands on
a whole number of screen pixels (measured ×4 at 1440p). Import settings were already right —
point filter, no compression — which is why the loss was purely geometric.

The room is an **ordinary bar, drawn wide**: dark wood panelling running off both edges, a
continuous shelf of plants along it, four hanging lamps in an even row, a couple of tables and
chairs, and a small neon. Nothing exotic — the width is the point, and repetition across the
full frame is what gives it.

**The bands are fixed by the furniture, not by taste.** The counter covers the bottom 40% of
the 360-unit stage, the seated customers sit across 40–60%, and the top 40% is the only part
never covered. So the lamps and the sign go up there, the panelling and the plant shelf sit in
the customers' band where they stay dark and quiet, and the tables go low where the counter
hides them. A backdrop that ignores this puts its good parts where nothing can see them, which
is what happened to every earlier attempt.

Seven other rooms were drawn getting here and each failure is worth keeping: a booth lounge read
as dead; an art-deco ballroom as too lavish and too crowded; a symmetric velvet room as a
*corridor*, because a centred vanishing point turns a bar into a tunnel; a brick room as a
medieval cellar; a rooftop terrace was liked as an idea but its first draft put the railing under
the counter and the city where the customers' heads are. Standing figures in the backdrop always
looked wrong — the customers are the people in this game, and a second set competes with them.

**Things standing on the bar are measured, not eyeballed (2026-07-29).** Three separate
alignment faults, all of them arithmetic once looked at:

- The till floated because its sprite carried **6 transparent rows** under the art on a 58px
  canvas. The stage seats a prop by putting its sprite's bottom on the bar, so any margin under
  the drawing is a gap. Props are trimmed to their own content now.
- The customers floated because `TycoonHud` kept its **own hand-written copy** of the bar's
  line (279) which was right for the previous counter art and 19 units too high for this one.
  It is derived from `DiegeticStage.CounterTopY` now — the HUD is 1280×720 against the stage's
  640×360, so exactly twice — and cannot drift again.
- Cutting the bodies at the counter sprite's **top** still left a sliver, because the art has two
  transparent rows above its brass edge. `CounterTopY` is the brass line itself, which is also
  what it means: the edge a customer leans on.

And a prop resting **on** the bar sits forward of that line, on the surface, not balanced on the
far edge — with a contact shadow, which is what actually sells the contact.

Two structural lessons, both worth more than the art:

- **A UI child draws on top of its parent.** The weather layer was parented under the room art
  and so drew over it — which is how an extra sky layer came to hide the very skyline it was
  meant to sit behind. It is a sibling now, sharing the room's fitter so it scales identically.
- **Add only what the picture cannot do itself.** A painted room already has its own sky, walls
  and lights; layering another set over them hides them. The animation adds rain in front of the
  view and signs that blink, and nothing else.

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
