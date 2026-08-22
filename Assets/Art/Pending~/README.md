# Pending art — the 2026-08-20/21 session

> **UPDATE 2026-08-22 — THE ROOM AND THE COUNTER ARE IN.** `background/room_v4.png` is
> now `Assets/Art/Backgrounds/club_room.png`, and the author's counter and shutter
> (`backba-opened-png.png`, `backbar-kapak.png`) are `counter.png` and
> `counter_shutter.png` beside it. Installed under their OLD names on purpose, so every
> scene reference and GUID survived. What changed with them:
>
> - The counter was **cropped to its own ink** — the drawing carries 112 transparent
>   rows on top and the stage hangs the sprite by its top edge, so uncropped it would
>   have hung the slab 112 px low. It is 638×241 now.
> - `CounterRestY` 131 → **120**, read off `mockup_drawer_closed.png` (the slab's
>   near-black band starts at screen row 240 of 360). The same slab is at row 119 in the
>   OPEN frame — a difference of exactly **121**, which is the drawer's whole travel.
> - The nine-slice border re-measured to **(217, 0, 218, 0)**: the front is three bays
>   now, not the old eight, with posts scanning at x 7-32, 209-226, 412-429, 605-630.
> - The two `onCounter` slots followed the surface down 11 (`taps` 131→120,
>   `counter_end` 121→110). Verified in play: the tap tower's foot sits at −60 against
>   a counter top of −58, which is the 2 px surface inset.
>
> STILL NOT IN: the shutter is imported but **not drawn** — the cabinet stands open, so
> the drawer mechanic (shutter up, scene up 121) is the next piece. The taps and the UI
> key below are untouched. EditMode 335/335 and PlayMode 8/8 both green after the swap.

Everything here was made or measured in one sitting and **none of it is in the game yet**.
It sits in a `~` folder on purpose: Unity never imports a directory whose name ends in `~`,
and git does keep it — the same trick `Assets/Tests/PlayMode/Baselines~/` uses. That matters
because the working copies live under `Tools/scene_cast_raw/`, which `.gitignore` line 124
excludes (`Tools/*_raw/`), so anything left only there does not travel to another machine.

The generators are all under `Tools/` and are tracked. Each one carries its own reasoning in
its docstring — this file says **what the pictures are**; the tools say **why they look like
that**.

| Tool | Makes |
|---|---|
| `Tools/scene_cast_gen.py` | the room plates (rounds 1–7) and the five background gates |
| `Tools/backbar_drawer_preview.py` | the two counter mock-up frames |
| `Tools/tap_towers_gen.py` | the beer taps at 64 px |
| `Tools/tap_upscale.py` | the 200 px tap redraws |
| `Tools/ui_buttons_gen.py` | the UI key sheet |
| `Tools/ui_key_clean.py`, `ui_key_pressed.py`, `ui_button_slice.py` | blank the key, derive its pressed state, measure its nine-slice |

Source art the author drew is **not** here — it lives at
`Tools/AssetPipeline/sources/pixellab_user/` (tracked): `backba-opened-png.png` (the counter),
`backbar-kapak.png` (the shutter), `room_ref.jfif` (the room reference).

---

## background/

**`room_v4.png`** — 640×360. The adopted room: cream walls `#EAD1C2`, one continuous indigo
`#4B0082` right wall, floor-to-ceiling grey-framed windows down the left, mahogany plank floor.
Generated as `cast_room7_a`.

Two numbers to know before installing it:

- **The wall meets the floor at y 220.** `club_room.png`, the room in the game today, has that
  line at y 206, and the author's own reference render has it at 182. `DiegeticStage`'s
  constants were hand-measured against one of those, so **installing this plate means
  re-measuring them** (GDD 14 §5b says so in its own words).
- The window panes are keyed `#00FF00`, waiting for the `window_cycle` art behind them.

The §5b table's "y = 181" is not an error — it describes the author's reference render, not
`club_room.png`. That was an open contradiction in the docs until this session measured all
three; the resolution is written into `scene_cast_gen.py` beside `FLOOR_BAND`.

## counter/

**`mockup_drawer_closed.png` / `mockup_drawer_open.png`** — 640×360. Not art: two composites
that show how the backbar drawer is meant to move. The counter is one tall sprite and the
screen is a window onto it, so both frames come out of two numbers:

```
SCENE_Y   :   0  ->  -121
SHUTTER_Y : 305  ->   356      (4 px of the shutter left showing)
```

The shut bar stands **120 px** tall, which leaves **121 px** of counter below the screen — and
lifting by exactly that 121 lands its bottom edge on the screen's bottom edge, so nothing has
to be invented to fill the gap. Measured off the counter sprite: the shelf opening is rows
177–353, **176 tall, which is the shutter's height exactly** — the two pieces were drawn to
each other. Bottles stand on rows 249 and 339.

One question still open: the shutter is 592 px wide against the counter's 638, so the blue
side posts stay visible when shut. It reads as deliberate and is drawn that way here.

## taps/

Gold beer-tap towers for the bar top. `1mouth` / `2mouth` / `3mouth` is the ladder — the tier
is the **number of spouts**, not the ornament.

| File | Notes |
|---|---|
| `tap_gold_1mouth_64.png` | 64×64. The design all the others copy. |
| `tap_gold_2mouth_64.png` | 64×64. Same column and foot, two spouts on a crossbar. |
| `tap_gold_3mouth_64.png` | 64×64. Three spouts. **Carries a black keyline (31 % of its pixels)** where the others carry none — the odd one out. |
| `tap_gold_1mouth_200.png` | 200×200 redraw, 5× the pixel count, same design. |
| `tap_gold_3mouth_200_detailed.png` | 200×200. Detailed (5 174 px) but still 10 % black. |
| `tap_gold_3mouth_200_natural.png` | 200×200. Clean — **0 % black** — but the tool did not add resolution, so it is the 64 px drawing on a bigger canvas. |

**The three-mouth tap has no version that is both detailed and clean.** Picking one, or
spending one more edit pass from the detailed file, is the open decision. `_sheet_200_compare.png`
shows all three at matched height.

`unused_tier2_fluted_64.png` and `unused_tier3_deco_64.png` are an earlier reading of "tier"
as craft rather than spout count — a fluted column and a deco tower with a crown. They were
not asked for in the end and are kept because they are good and cost nothing to keep.

Colours come from GDD 14 §3's Amber ramp (`#4A2E14 #8F5A1E #C9822B #E8A33D #F5C97B`), which is
why they sit on the counter's near-black slab without arguing with it.

## ui/

Buttons in the style of the author's pink arrow.

**`ui_key.png`** — 132×143, the raised key, blank.
**`ui_key_down.png`** — the same key pressed.
**`ui_icon_beer.png`** — 96×101, the glass that the generator drew inside the key, lifted out.

### The nine-slice border is `18, 18, 18, 24`

Unity's `Vector4` order is left, bottom, right, top. Set it at import in
`PatronArtPostprocessor.cs`, beside the other frames:

```csharp
else if (file == "ui_key" || file == "ui_key_down")
    ti.spriteBorder = new Vector4(18, 18, 18, 24);
```

**This is why there is one button and not a dozen.** A nine-sliced key draws "OK" and
"MENU — MAKE A DRINK" from the same pixels: the corners are pasted at 1:1, the edges repeat
along their own axis, the middle fills. `_sheet_slice_proof.png` shows the one sprite at five
different rectangles; `_sheet_states.png` shows both states at three.

Widths and heights therefore never need new art. **Shapes might**: a key with a large corner
radius squeezed very small keeps its corners and loses its body, so a genuinely different
shape class — a tall tab, a circular key — would be its own drawing.

Two things worth knowing about how these were made:

- **Nothing may be baked into the middle of a nine-slice.** The generator drew a beer glass
  in the key; at 300 px wide the glass stretched to 300 px too. It has been lifted out to
  `ui_icon_beer.png` and must be drawn as a separate sprite on top.
- **The pressed state was computed, not generated.** It is the idle key with its lit lip
  turned to the mid tone and its face darkened — so it can never drift from the idle art, and
  it stays correct if the key is ever redrawn. `ui_key_pressed.py` carries the reasoning,
  including the two attempts that failed first.

`ui_btn_flat.png` is an earlier, flat (un-raised) button from the same style reference, kept
for comparison. Its border is `12, 13, 13, 12`.

---

## Installing any of this

Nothing here is wired up. In rough order of risk:

1. **The UI key** is the cheapest — copy into `Assets/Resources/`, add the border rule above,
   draw with `Image.Type.Sliced`.
2. **The taps** need a decision on the three-mouth file, then `fixtures.json` entries; the
   existing `fx_tap_single/double/triple` are the ones they would replace.
3. **The room** is the most invasive: it moves the horizon, so `DiegeticStage`'s hand-measured
   constants have to be re-measured against it, and the `LookTests` baselines re-blessed.
