# LAST CALL — GDD Module: Chrome Language v3 (supersedes v2)

> **STATUS 2026-08-14 — CURRENT. This is LAW for everything the player looks at.**
>
> v2 described a different game: authored at 640×360, for the card era, and it banned engine
> primitives in favour of hand-authored 9-slice sprites. None of that is true now. The chrome
> is DRAWN IN CODE — `Image` rects and procedurally generated masks (`ChromeArt`) — inside a
> fixed 1280×720 field, and there are no prefabs. What survives from v2 is the part that was
> right: there is ONE button, and no screen ships without passing a checklist.
>
> This module exists because of a verdict (2026-08-14, the author): *"bu tasarımın ai olduğu
> çok belli oluyor ... kutu kutu ... üst barı tamamen yenile"*. §6 is that verdict written
> down as rules, so it does not have to be given again.

## 0. The field

1280×720, fixed, forever. `DesignFrame` windowboxes it and the camera matches, so a HUD unit
is a stage unit at every window shape — see CLAUDE.md. Positions are ABSOLUTE and that is
deliberate: the props stand ON the room (the shelf, the bin, the till, the stools), and an
anchor-and-layout-group HUD would slide off the thing it belongs to. Do not "fix" this.

**Palette:** `UITheme` five-step ramps only — `Night`, `Magenta`, `Cyan`, `Amber`, `ViceRed`,
`ClubBlue`, `Lime`, `Cream`, `Malt`. A literal `new Color(...)` in UI code is a bug unless it
is a tint or an alpha of a token, and it must say why on the line above it.

**Type:** the pixel faces rasterise cleanly only at whole multiples of their 8px design size.
**8, 16 or 24. Nothing else, ever.** `resizeTextForBestFit` stays off.

**Grid:** `UITheme.Grid` is 4. Rects sit on whole units; sizes and positions are integers.

## 1. The vocabulary

The chrome is made of NAMED OBJECTS, not of rectangles. A new surface picks from this list; if
nothing fits, the list grows by one and the new thing gets a name and a reason here. This is
the whole defence against a screen that looks assembled.

| Object | What it is | Drawn by | Where |
|---|---|---|---|
| **BEAM** | A structural run across the screen with a lit top face, a front that falls away, and a light along it. The board over the back counter is one. | `Band` ×3 + a neon tube | top bar |
| **CASE** | A body that holds an instrument: bevelled, lit top and left, shadowed right and bottom. It says *there is a machine in here*. | `Case` | the clock |
| **GLASS** | The dark inset a readout sits behind. Never pure black — a display's dark is the panel's colour through a tint. | an `Image` inside a CASE | the clock |
| **KEY** | The ONE pressable object. Chamfered corners, a real throw along its bottom, tinted by state. Everything the player can press is this. | `ChromeArt.Key()` | §2 |
| **PLATE** | A card a thing stands on. Chamfered, one hairline rule, two shaded rows at its foot so it sits ON the page. | `ChromeArt.Card()` | market listings |
| **LAMP** | A round bulb, with its light falling off in bands when lit. Signage, never a status dot. | `ChromeArt.Lamp` / `LampGlow` | the week marquee |
| **RULE / HAIRLINE** | One unit of edge. A bevel is four of them: lit top and left, shadowed right and bottom. | `Hairline` / `HairlineV` | everywhere |
| **MARK** | A 16×16 drawn glyph, white, for the caller to tint. Never a font glyph — no pixel face carries ⚙. | `ChromeArt.Mark` | keys, steps |

**Two rules carry the top bar:** `CapY` (what a reading IS) and `ReadY` (what it SAYS).
Everything on the beam is placed against one of them, left to right. A new board gets its own
pair and every item on it obeys them.

## 2. The ONE key

Every pressable thing in this game is the same object. A player who has learned the market's
button has learned the settings menu and the HUD.

- **Body:** `ChromeArt.Key()` — chamfered corners, 1-unit edge, a 3-unit throw along the
  bottom, a lit face row. Tinted by state, so the same drawing is the amber primary, the
  grey refusal and the picked green.
- **Press:** the face sinks (`PressSink`, depth 3, lift 2) — the throw is what makes a sink
  read as a press rather than as a colour change.
- **Label:** body face, 8 or 16, UPPERCASE. A key too small for its word takes a **MARK**
  instead, inlaid so the drawing lands 1:1 (see §3).
- **One amber key per screen.** Amber is the primary action. Two amber keys means neither is.

**Known debt (2026-08-14):** `NewButton` draws a flat fill and `SettingsRow` draws a bare
`Image` with no throw at all, so the game currently speaks three button dialects. They both
become the KEY. Until they do, do not add a fourth.

## 3. The scaling law

**A drawing is used at the size it was drawn, or at a whole multiple of it. There is no third
option.** `ChromeArt` marks are 16×16, `Key()` is 20×20 9-sliced, `Lamp()` is 16, `LampGlow()`
is 24. A 16-pixel cog inlaid into a 30-unit key comes out 20 wide — 1.25× — and arrives with
its teeth at two different widths. This has now shipped once (the settings key, 2026-08-14).

Corollary: size the CONTAINER to the drawing, never the drawing to the container.

## 4. The fitting law

**Measure the string; the rect must hold it.** `"0.0"` at display 24 is 72 units wide. It was
given a 60-unit rect with `Overflow` on, so it ran left out of its box and sat down on top of
the fifth star (2026-08-14). `Overflow` is for text that is allowed to run — it is never a
substitute for a rect that fits.

Same law for gauges and labels: if a number can reach three digits, the box holds three digits.

## 5. Light says state, colour says kind

- The **state light** is the biggest lit thing available, not the smallest. The board's neon
  tube goes magenta at last call; before that it was a 2-unit rule under one plaque, which
  nobody was ever going to see.
- **Sacred number colours:** money is Amber, the standing is Amber, the story is Magenta,
  the clock and information are Cyan, refusal is ViceRed, gain is Lime. These do not get
  reused for decoration.
- A **glow** is banded falloff, never a bigger rectangle behind a smaller one. Two nested
  squares is a box in a box (§6).

## 6. The tells — what makes a screen look made by nobody

This is the anti-slop list. Every line is something this project actually shipped and the
author actually rejected. Read it before designing a surface, and again before showing one.

1. **A row of equal boxes.** Five bordered slabs side by side, each the same height, each with
   a caption over a value, evenly spaced. This is the single loudest tell. Fix: decide what
   the surface IS (a beam, a shelf, a card), then put things ON it.
2. **A border on everything.** If every element is outlined, no element is grouped. Outline
   the object; let its contents sit inside it without frames of their own.
3. **Everything at one visual weight.** If a screen has no biggest thing, it has no subject.
   The clock is the biggest reading on the board because the night is measured in it.
4. **Captions floating half a line above the thing they caption.** Two rules, and everything
   on one of them. Misalignment reads as carelessness even when nobody can name it.
5. **The same fact printed twice on one screen.** "WEEK 1" on the clock plaque and again over
   the marquee. Each element owns exactly one fact.
6. **A box behind a box, called a glow.** Light falls off; it does not step from one rectangle
   to a bigger rectangle.
7. **A caption where a drawing belongs.** The time set in the body font with a dim copy behind
   it is a caption in costume. A readout has segments; a lamp is round; a cog is drawn.
8. **A dot standing in for an object.** Status dots, coloured squares and 8×8 fills are the
   lazy answer. The bar's own world has lamps, plates, keys, tape and neon — use those.
9. **Decoration that encodes nothing.** A rule, a tick, a pip or a bracket must be true about
   the content. If it is only there to fill the space, delete it and let the space be empty.
10. **Smooth where the game is pixel.** Gradients, anti-aliased arcs, fractional scaling and
    sub-unit positions. Everything here is banded, chamfered and whole.

**The positive form of all ten:** distinctive chrome comes from the SUBJECT'S OWN WORLD. This
is a bar. It has a marquee, a till, enamel plates, bottle labels, tape, a register drawer, a
brass rail, chalk, a neon sign. When a surface needs a new idiom, take it from the room before
inventing a widget.

## 7. The delivery gate

Run this before a screen is shown to the author. It is a PASS/FAIL, not a discussion.
`LastCall → Audit UI` measures items 1–5 on the live screen; the rest are looked at.

- [ ] **Scale** — every sprite drawn at 1× or a whole multiple of its own size
- [ ] **Fit** — no text wider than its rect; no overlapping siblings that were not meant to
- [ ] **Grid** — every rect on whole units, spacing on 4
- [ ] **Type** — every font size is 8, 16 or 24
- [ ] **Palette** — every colour is a `UITheme` token, a tint of one, or has a written reason
- [ ] **Vocabulary** — every object on the screen is in §1, or the list grew and says why
- [ ] **The ONE key** — everything pressable is the KEY; exactly one amber primary
- [ ] **Alignment** — captions on one rule, readings on the other, all the way across
- [ ] **The tells** — §6 read top to bottom against a screenshot at 1×
- [ ] **Looked at** — a capture was taken in play and someone looked at it (this is the house
      rule that catches what no checklist can: the baselines exist for the same reason)
