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
`ClubBlue`, `Lime`, `Cream`, `Malt`, plus the v3 material ramps `Graphite` and `Brick`
(14 v3 §3: architecture/furniture only — never a signal, a sacred number or a key face),
plus the `ViceFade` band set (14 v3 §3a: CHROME only, and it is a band set, not a
gradient — see §6.10; twenty-six bands since 2026-08-19, when the author asked the
first take's eight for "daha smooth" — a band set smooths by growing bands, never by
interpolating). A literal `new Color(...)` in UI code is a bug unless it
is a tint or an alpha of a token, and it must say why on the line above it.

**Type:** the pixel faces rasterise cleanly only at whole multiples of their 8px design size.
**8, 16 or 24. Nothing else, ever.** `resizeTextForBestFit` stays off.

**Grid:** `UITheme.Grid` is 4. Rects sit on whole units; sizes and positions are integers.

## 0c. Two shapes, two meanings (2026-09-06)

The author: *"konuşurlarken kafalarının üstündeki baloncuğun şekli değişmeli böylece
oyuncular kafasının üstünde yazanın ne zaman bilgi ne zaman sohbet için olduğunu anlar
... klasik pixel beyaz mavi çerçeveli bulut şeklinde konuşma balonu."*

A **straight plate** is a READOUT — the ticket over a head, up for as long as somebody is
on the stool, and everything on it is a fact about the order. A **lobed cloud** is a
VOICE — it arrives when somebody says something and it goes when they stop. One glance
tells the player which they are looking at, which is the whole point.

`ChromeArt.CloudBubble` draws it: a 24×24 sheet with 8-pixel borders whose edge strips
carry one half-round bump each, drawn TILED so a wide balloon is that bump repeated
rather than one stretched blob. `CloudTail` is three shrinking puffs. Both are drawn in
code, like every other piece of chrome in this game — the cloud is arithmetic, not a
picture somebody found.

The balloon MEASURES its copy (`LayOutSay` asks the text generator for the wrapped
height): three sentences that each wrap on their own defeat any estimate, and a balloon
that guesses hangs half a drinker's verdict over their head.

## 0b. The room answers the pointer (2026-09-06)

The author: *"Ana sahnede, mahzen sahnesinde, kokteyl yapma sahnesinde seçilebilir bir
nesnenin üstüne mouse geldiyse, o nesne yükselir biraz boyutu büyük ve arkasından ışık
çıkar aynı zamanda çok hafif sağa ve sola doğru hareket eder."*

`HoverGlow` is that sentence: **Rise** (a few units in the prop's own space), **Grow**
(1.05–1.07), **Sway** — a ROCK of ±2° at ~1.8 Hz, not a drift along x, which the author
corrected the same day (*"sağ sola hareket etmesinden kastım sağ sola sallanması"*) — and
**Halo**, a soft warm bloom behind the prop (`ChromeArt.Halo`, made on the first hover so a
prop nobody points at never pays for one, and never made at all on the way out). The old
brightness (×1.22, the beer font's own number) stays under all of it.

- The movement is **undone and re-applied every frame** in `LateUpdate`, on top of
  wherever the prop's owner put it — the rail re-slots its dishes, the book and the
  coaster ride the counter, the drawer animates the cellar. Nothing fights an owner.
- **The thing under the pointer comes to the front, and its light one step under it**
  (*"mouse önüne gelen hiyerarşide en üste çıkmalı onun bir altında ışıklandırma olmalı"*):
  sibling order on a canvas, sorting order in the room, both put back on the way out. The
  order is left alone while a panel is being disabled — Unity refuses a sibling move made
  during a parent's activation, and a closing bench disables a whole tree of these at once.
- **The light is a RIM, and nothing shines behind the drawing** (2026-09-06, the author:
  "parlarken görselin arkası parlamasın sadece etrafı parlasın"). `ChromeArt.Glow` writes
  nothing where the source is opaque, so what lights up is a halo around the silhouette. It
  matters most for the props you can see through: a glass with a lit disc behind it is a
  glass full of light.
- **The light is the PROP'S OWN SHAPE** (2026-09-06, the author: *"parlama alanı nesnenin
  şekline göre gerçek nesnenin şeklinden daha büyük olmalı, şu an standart bir elips ve bu
  her nesneye uymuyor"*). One ellipse behind everything is a bubble with a thing in it, and
  sizing that ellipse to the drawing — the first answer — only made it a tighter bubble.
  `ChromeArt.Glow` grows the light OUT OF the sprite instead: every texel takes its distance
  to the nearest opaque pixel (a two-pass chamfer transform), and the alpha falls off over
  the reach. A lemon dish glows like a dish, a bar spoon like a spoon, a bottle like a
  bottle. The glow's canvas is the drawing's canvas plus the reach on every side, so drawing
  it centred at the prop's own scale lines it up by construction — no measuring at the call
  site, and a drawing that sits high in its sheet takes its light with it. Cached per sprite
  and re-cut when the prop's drawing changes; `Halo` is now the REACH as a multiple of the
  automatic one (about a sixth of the drawing's short side), and 0 still draws none.
- **A world prop gets a world halo**, three sorting orders under its own drawing: its hit
  plate is on the canvas, and a bloom hung there would sit in front of the bottle.
- **The cellar's bottles do all of it too** (*"bunların aynısı mahzendeki alkoller için de
  olmalı"*). A bottle there is four transforms standing in one place — front plate, back
  plate, the drink and the mask that cuts it — so all four are handed to the glow as
  `Movers`, and every one is turned and grown ABOUT THE BODY'S centre rather than about
  its own. That is the difference between a bottle that rocks and a bottle whose drink
  swings out through its glass.
- **A drinker is not a prop.** People brighten and nothing else — a person who rises and
  grows under the pointer reads as a puppet.
- **The rock is ADDED to whatever else turns the prop.** An absolute angle here held the
  tin upright through a whole pour, because the tin being tipped over a glass is rotated by
  the bench (2026-09-06: "shakerdan bardağa koyma sahnesinde shaker devrilmiyor koyarken").
  Same law as the rise: take off what the glow added last frame, then add this frame's.
- **A glow lets go when its panel stops taking the pointer.** A CanvasGroup that drops
  `blocksRaycasts` — the flow's root while a stage slides, the prop doors while the cellar is
  open — sends no exit event, so a prop the pointer was on when the shutter came down stayed
  lit for as long as the room was open ("musluk seçiliymiş gibi takılı kalabiliyor").
- **The light sits on the drawing's centre, not on the rect's pivot.** The bench's props are
  hung by their feet (the bottle at 0.22, the spoon by its grip), and a halo centred on the
  pivot of a 384-tall bottle sits a hundred units below it — which is what "pour sahnelerinde
  parlamalar aşağı doğru kaymış" was.
- **A lit prop does not carry its light over its neighbours.** The prop comes to the front;
  the light stays where it was, so a hovered dish does not throw its rim across the two
  dishes either side of it ("parlama efekti ana sahnede garnishlerin önünde kalıyor").
- **Everything that can be picked up answers**, and only while it can be: the spoon, the tin
  on both benches, the finished drink and the empties all light now, and the tin on the
  filling bench does NOT until its lid is on, because until then it is a target for the
  bottle rather than something the hand can take.
- **What moves is the PROP, not its hit plate.** The beer font and the sink are drawn in the
  room and clicked through an invisible canvas plate; with no `Riser` named, the glow was
  raising, growing and rocking the plate while the brass and the basin stood still — the
  rise had never once been seen, and a plate that drifts two units off its prop is a drop
  target that moves out from under the hand (the tin's carry test found it by failing to
  reach a basin it was standing on, 2026-09-06).
- **The room can call a prop without a pointer** (`HoverGlow.Beckon`): while a hand is
  carrying something that belongs in the sink, the sink lights and lifts exactly as it would
  under the pointer — same rise, same light, same coming to the front, so the room never
  teaches a second visual language for "here".

## 0d. Notices carry the thing they are about (2026-09-06)

The author: *"aktif görev LOG butonun olduğu yerde bildirim şeklinde kalıyor olması lazım, çok
uzun üstüne bir nesne veya asset geldiğinde şeffaflaşmalı (yok olmamalı sadece biraz
şeffaflaşmalı) mouse ile üstüne gelindiğinde netleşmeli"* and *"bildirimlerde alkollerin
nesnelerin paranın yıldızın ve benzeri nesnelerin kullanım durumunda iconlarından faydalan"*.

- **The week's job lives beside the LOG key**, one row, icon then line: the drink itself for a
  count of one drink, a star for perfect pours, the cloth for clean nights. It counts DOWN —
  "2 MORE PERFECT POURS" is an instruction, "1/3" is a scoreboard.
- **It fades rather than leaves.** 0.85 at rest, 0.35 while the room has something else to say
  in that corner (the cellar up, a bench open), and 1.0 the moment the pointer is on it. A
  notice that disappears is a notice the player has to remember was there.
- **A notice may carry a picture**: `Toast(message, tint, seconds, icon)` puts a 16px mark
  before the line and gives up 22 units of the line's own room for it. The coin for money, the
  star for a rating, a bottle for stock — the picture lands while the eye is still on the
  counter.

## 0e. Five dots are the pour (2026-09-06)

The author: *"tariflerde kullanılan doluluk göstergesinin barını görseldeki tarzda değiştirmek
istiyorum. 5 noktadan oluşuyor her nokta %20lik kısmı ifade ediyor. Her noktanın yine rengi de
olacak."*

The recipe page's sight glass is five dots on a string (`ChromeArt.RatioDots`): one dot a fifth
of the glass, filled up to and including the box a band wants, each in that box's own colour
(`BandBoxColors`, the same five the whole game grades with), the rest left open. A sliding
level asked the reader to MEASURE it; dots are counted. The legend at the top of the page is
the same five dots with the share under each, so nothing is learned twice.

The page is laid out around the drink now: the glass at 76 with the price beside it at display
24 (*"ücreti daha ön plana çıkarılsın"*), bottles at 40 in the rows (*"tarifteki alkol
görsellerini ... büyütelim"*), and every caption at the book's own 16 rather than the 8 the
game keeps for tags over a head (*"menüde daha okunaklı bir font"*). The book itself is the
same size it always was.

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
| **PLATE** | A card a thing stands on. Chamfered, one hairline rule, two shaded rows at its foot so it sits ON the page. | `ChromeArt.Card()` | reading cards, chips |
| **98 KEY** | The market's own pressable: a square raised panel with the era's two-step bevel, greyscale, tinted by state. Pressed it does not travel — it inverts (`Win98Press`), the one press in the game that works that way, because the storefront speaks the desktop's dialect and the bar speaks the bar's. Every button ON the site is this; nothing off the site may be. | `ChromeArt.Win98Key()` | market listings, tabs, checkout, exit, dialogs |
| **ISLE** | The storefront's mark: PALM CARGO's palm on its island. A mark, not chrome — white, tinted by the caller, drawn at its authored 28×24. | `ChromeArt.Isle()` | the market's title bar |
| **LAMP** | A round bulb, with its light falling off in bands when lit. Signage, never a status dot. | `ChromeArt.Lamp` / `LampGlow` | the week panel |
| **WELL** | The recess an instrument's glass sits in, routed INTO the beam: chamfered corners, the top edge dark (light falls from above — a recess shades where a box shines), a lit lip along the bottom, and a floor that IS the display glass. One 9-sliced sprite, any width. (A PixelLab-generated backplate held this row for one build on 2026-08-19 and was withdrawn on the author's next sentence — "oluşturulan takvim görseli bozuk duruyor" — so chrome-is-never-generated stands unbroken.) | `ChromeArt.Well()` | the hour, the week |
| **RULE / HAIRLINE** | One unit of edge. A bevel is four of them: lit top and left, shadowed right and bottom. | `Hairline` / `HairlineV` | everywhere |
| **MARK** | A 16×16 drawn glyph, white, for the caller to tint. Never a font glyph — no pixel face carries ⚙. | `ChromeArt.Mark` | keys, steps |

**Two rules carry the top bar:** `CapY` (what a reading IS) and `ReadY` (what it SAYS).
Everything on the beam is placed against one of them, left to right. A new board gets its own
pair and every item on it obeys them.

**The beam, left to right (2026-09-06, the author: "Saat/Takvim/para/Madalyon/kalp/yıldız/
ayarlar butonu ... uygun bir layouta göre tekrar koyulsun az metin kullanılsın"):** the HOUR
in its well · the TILL in a well of its own (the coin at 2×, the figure in the display's cyan,
red under water — back on the beam after the register left the room) · the WEEK, centred ·
the two house readings, each with ONE word beside it (COMFORT over the medals, SERVICE over
the hearts) · the STANDING's five stars under the crowd's caption · the settings KEY at 42,
its cog at exactly 2×, amber. **Every reading explains itself under the pointer**: the tip
(`ShowPropTip` with an icon and a line) carries the mark it is about, its name, and one line —
"WHAT TONIGHT'S DRINKS ARE WORTH", "WHAT THE ROOM IS WORTH: WALLS, LIGHT, FURNITURE" — and
hangs UNDER a prop that lives at the top of the screen, where a caption raised above it would
be off the picture.

**The settings are a WINDOW, not a list (2026-09-06):** a scrim, a titled plate in the centre
(`ChromeArt.Card`, a band with the cog and the neon under it), the settings as rows — the name
at the left, the control at the right, a note under a name that needs one — grouped AUDIO /
DISPLAY / THE RUN, so the one thing that throws the night away (START OVER → NEW RUN, on a
brick key) sits furthest from the thumb. The volume is a five-block meter with a key at each
end. The developer's bench keeps one small key at the plate's foot; the bench itself is set
in a plain proportional face, in Turkish, in four real columns — it is the author's tool and
not part of the game, so the pixel law does not reach it.

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

**Dressed by `KeyPlate.Dress`, and nowhere else.** It sets the drawn body, slices it, tints
it and wires the press in one call, so a new control cannot accidentally invent a fourth
dialect — there were four (2026-08-14, now closed): the market's drawn key, the service
flow's `plate` sprite out of Resources, the HUD's flat coloured rect, and the settings
menu's bare rect that did not press at all.

Captions on a key are inset along the bottom by `KeyPlate.Throw`, so they ride ON the face
and not on the throw.

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
    *The market's vice fade is the test of this rule, not the exception to it* (2026-08-19).
    It runs blue to pink across a 1040-wide title bar and it is still legal, because it is
    FLAT runs with a hard edge between them and not one interpolated pixel: a one-texel-per-
    band texture drawn with point filtering (twenty-six bands of 40 since 2026-08-19; the
    first take's eight read as stripes, and a band set smooths by growing bands). Swap that
    filter to bilinear and the same object becomes a violation. If a future surface wants a
    fade, it gets bands or it gets nothing. The restock gauge is allowed to WEAR the fade as
    its fill (the author, 2026-08-19) because the reading there is carried by geometry —
    the level's height — and the colour still says only "this is the market's".

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
