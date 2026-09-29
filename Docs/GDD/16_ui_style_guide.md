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
ONE written exception (2026-09-15, the author: "Butonlar için bu dosya yolundaki butonları
kullan"): the menus' keys are the author's own button pack and key caps (`MenuPack`,
`KeyCaps`, GDD_MEVCUT §9.72), and those keep the packs' own faces and inks — the audit
counts them as tokens. Everything drawn AROUND a pack key stays in the ramps.

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

*(The job row below was superseded 2026-09-28 by the hostess's book, GDD_MEVCUT §9.134: the job is a
message at the top left now.)*

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
| **LAMP** | A round bulb, with its light falling off in bands when lit. Signage, never a status dot. | `ChromeArt.Lamp` / `LampGlow` | the week panel (the beam's RED NIGHTS lamps stood for one morning, 2026-09-28) |
| **SIGN** | A plate on the wall with a word bent in neon tube: a rim, a lit top row, four fixings, the tube's glass with its lit line, and its light laid in flat bands that stop at the plate. Signage in the room — it says the same word in every language, like the shutter's painted "Open bar" — so it is drawn, never typeset. | `ChromeArt.ClosedNeon` (drawn at half size, shown at exactly 2×) | the night's call: CLOSED (2026-09-28) |
| **NEON TUBE** | A tube bent along a path with no plate behind it: one texel of glass, a rim either side of it and over its ends, and its light in two flat bands (never a smooth ramp). Four white rings, so it runs its three states by tint alone — DARK glass (Night[3] rim, Night[2] glass, no light), HALF (the hue's [2] round its [3]) and LIT (the hue's [3] round a Cream[4] glass, its [2] bands at .30 and .12). Amber, magenta only on the story's night. A tube that STRIKES stutters dark, half, lit — and goes straight to lit under NO FLASHES. | `ChromeArt.NeonPath` (a chamfered rectangle, optionally broken for a plate) / `NeonBar`, drawn at half size, shown at exactly 2× | the night sign round the curtain's sky window, and one under each night of its week (2026-09-28) |
| **NEON MARK** | A NEON TUBE bent into a key's picture: one line of glass on a 15×15 grid, its rim and two bands of light grown by the same ring (`ChromeArt.NeonRing`), 21×21 shown at exactly 2× (42 units — the glass is the body face's own two-unit stroke). It rests HALF (the hue's [2] round its [3]), strikes LIT (its [3] round Cream[4], its [2] light at .30) while the pointer is on the key — one brightening step, never a blink, the same under NO FLASHES — and is DARK glass on a key that cannot be pressed. On the pack's amber or lime key it lies INKED (the word's ink round the plate's [1]): the ONE amber case — an amber-lit tube on a dark plate reads as the brass the author threw out. MAGENTA for the door's and the pause's verbs, CYAN for the second line (the small row, the tabs, the foot). Inside a key the outer band (.12) is not drawn, so the light stops short of the rim; the free-standing pointer wears both, in the hover's ClubBlue. Carries no words. | `NeonIcons` (the rows) + `SignKey` (the states and the slot: the key's left + 6, on its middle; the first cut's `NeonKeyIcon` / `TycoonHud.NeonGlyph` overlay on the pack's keys was dropped with its last caller) | the front door's keys, small row and pointer; the pause's five verbs; the settings' tabs, RESET and BACK; the credits' BACK (2026-09-29; the brass `mi_*` and the door's 1-bit `ib_m_*` retired) |
| **TITLE SIGN** | The club's lockup as a lit SIGN, drawn per screen scale: seven maps (x1 560×212 … x4 2240×848) sampled from the store logo's masters, the set chosen by the canvas's live factor and shown 1:1, Point, on a whole pixel — a few percent small between the steps, never scaled up. Four flat bands of the tube's own colour at the store art's alphas (150/78/34/12 — the sign's recipe; the menus' typeset titles keep CLOSED's .30/.45/.55/.65 of M0/M0/M1/M2. Two recipes, one each: do not invent a third). Its light is arithmetic on whole 1/30 s frames: the letters strike on in reading order, then a bead runs each line (magenta name, cyan genre line), a letter stutters, the coupe glints, the outer bands breathe. REDUCED: lit and still. NO FLASHES: single-step strikes, the idle held still. It never animates its position. | `TitleSign` + `TitleSignLight` (`Resources/Logo/*_map.bytes` — PNG bytes under a TextAsset name, so no importer resizes them) | the front door's title, centred at +168 so its glow clears a 21:9 crop (2026-09-29; `menu_logo.png` is the store's art only) |
| **CABINET** | A menu's plate as a fitting on the wall (2026-09-29, the author: "diğer sahneleri ürettiğine uygun bir tasarımda sıfırdan"): the SIGN's plate — Night[2] glass, a one-texel Graphite[3] rim, a lit Graphite[4] row under EVERY top edge, a fixing by each of the body's corners — crowned in the middle by a raised header on three stepped shoulders (four texels a step, the cellar's niche), a Magenta NEON TUBE bent round the body 12 in from its edge and broken at the top under the crown, and the screen's NEON TITLE in the crown. The crown is as wide as its title's light plus 24 of air a side (snapped to 8, at least 240); a title that would not fit steps to display 16. Title and tube STRIKE when the screen opens (half .05 s, dark .04 s, lit; the tube .10 s after the title; one step under NO FLASHES, lit at once under REDUCED) — never when it is shown again from its own settings. It replaces a generated picture (`menu_esc_bg`) and a blue skin: chrome-is-never-generated. | `MenuArt.Cabinet` + `MenuArt.FrameTube` + `NeonTube` + `NeonStrike` (`TycoonHud.BuildMenuCabinet`), drawn at half size, shown at exactly 2× | the pause (x 400–880, crown top 112, body 160–600), the credits (x 256–1024, crown top 90, body 130–630), the settings (x 112–1168, crown top 90, body 130–630; the P2 marquee `menu_header` retired) — every cabinet inside a 21:9 window's rows 90..630 (2026-09-29) |
| **RECESS** | A panel let INTO a cabinet, where its keys or its reading stand: Night[1] (a WELL's is Night[0]), its top and left edges in shadow (Night[0]), its right a step up (Night[2]), its sill catching the light (Night[3]) — a cut's bevel runs backwards from a box's. One 9-sliced sprite, one texel of border shown at 2×. The cabinet's plinth carries its instruments in Night[0] recesses: the pause's hour and till. | `MenuArt.Recess(face)` (`TycoonHud.MenuRecess`) | the pause's keys and its two foot wells, the credits' reading (2026-09-29) |
| **SIGN KEY** | The menus' key (2026-09-29) — a small sign, as the 98 KEY is the market's: a plate one Night step proud of its ground, a Graphite rim with the room's light on its top row, the ESC keys' terrazzo in its face, a NEON MARK at its left (centred on the key's middle) and its word dead centre, as wide as the word plus 56 a side (the mark's slot and air), 50 tall in a column, 46 in a row or a foot. POINTED: a ClubBlue NEON TUBE lit round its rim and the mark struck lit. PRESSED: it sinks two units and its lit row goes out. PRIMARY — the one amber key a screen has: Amber[3] enamel, Amber[4] lit row, Amber[1] rim, the word Night[1], the mark inked. DEAD: a Graphite[2] outline on the ground, the word Night[4], the mark dark glass. A key that ASKS FIRST swaps its word for the question (`chrome.pause.new_run_sure`) and a ViceRed tube for 2.6 s; the second press does it. It sits beside a real `Button` (whose `interactable` IS its dead state) and its pivot is its centre. Never the pack's plate, never a second amber key. | `SignKey` + `MenuArt.KeyPlate` (9-sliced) + `ChromeArt.KeySurfaceTile(Terrazzo)` (`TycoonHud.MenuSignKey`) | the front door's column and small row, the pause's five verbs, the credits' BACK; the settings' foot (RESET, BACK, APPLY) and, 34 tall with a SMALL KEY's mark, its row keys OPEN and START OVER (2026-09-29) |
| **NEON TITLE** | A screen's title typeset in the display face and lit as a sign: a Cream[4] glass, a one-face-pixel lining of the ramp's [3], and CLOSED's four flat bands (reach 6/4/3/2 of the FACE'S OWN pixels, [0]/[0]/[1]/[2] at .30/.45/.55/.65) grown four-way — every glyph copied once per band offset, each copy opaque and PRE-BLENDED over the token it really stands on (Night[2] in a crown, Night[1] on a recess). Its light is clipped where its plate stops: under a crown, to the crown's face below the rim. The only typeset thing that glows; it runs HALF/DARK only for the strike. | `NeonWord` (a mesh effect on the `Text`) | PAUSED, CREDITS; the game's name and the thanks on the credits (2026-09-29) |
| **ENAMEL PLATE** | The cellar's cream name plate: a Night[0] frame, its upper half Cream[4], its lower Cream[3] (the light across its top), 24 units tall and as wide as its word plus 16 a side; the word Night[1] in the body face. A heading that is a thing, not a coloured line. | `MenuArt.Enamel` (9-sliced across) | the credits' section heads (2026-09-29) |
| **LEDGE** | A Graphite sill across a column — its lit row, its lip, its shade, two units each — with the cellar's deco FAN (five Magenta rays on a Graphite rim, no light of its own) standing on its middle. Divides the doors from the second line where three coloured rules did. | `FieldFill` ×3 + `MenuArt.Fan` | the front door, between the column and the small row (2026-09-29; the fan is an open pick) |
| **WEEK-ROW TABS** | A window's pages as the curtain's week: one equal cell a page, its NEON MARK and word over a straight NEON TUBE — in a row, or stood on end as a column when the window must fit a 21:9 window's height. The open page lit CYAN (mark and tube, the word Cyan[4]); the one under the pointer its mark lit and its tube HALF ClubBlue; the rest half-lit, the word Cream[3]. Tabs never strike. Never a row of keys. | `NeonIcons.View` + `ChromeArt.NeonBar` (`TycoonHud.BuildSettingsTabs`) | the settings' four pages, a column of 180×54 cells down the cabinet's left (2026-09-29) |
| **SETTINGS ROW** | One option on a page: its 16 mark at 20 (Cream[2]), its name at 48 (body 16, Cream[4]), its control against the right 24 in, a Night[2] rule under it. The pitch is the largest that fits and 2 (mod 4), so a 34-tall control lands on the even grid. POINTED (or a CONTROLS row listening for its key): a ClubBlue[0] fill, a ClubBlue[4] tab at its left, its mark ClubBlue[4]. A name that would reach its control steps to 8. Its NOTE is never on the row — it is the NOTE STRIP's. | `TycoonHud.SettingsRow` | every settings page (2026-09-29) |
| **NOTE STRIP** | A Night[0] well beside a page with body 8 Cream[3], centred and wrapped: the pointed row's note, else the page's own hint — in the face of the language it is written in (a language's name, the picked language's "it switches at once"). Moving the notes here keeps every row one line in all 29 languages. | `MenuArt.Recess(Night[0])` + `TycoonHud.PaintSettingsNote` | the settings, under the tabs, 136–316 × 392–612 (a one-line strip under the page, 596–624, until the 21:9 re-lay) (2026-09-29) |
| **CHOICE** | A setting's ways cut into one Night[0] well with a Graphite[2] rim and Graphite seams, one segment a way; one segment width for a whole page (the widest way + 32, at least 80). CHOSEN: Cyan[3] enamel, a Cyan[4] lit row, the word Night[1]; the others' words Cream[3]; POINTED: a ClubBlue NEON TUBE round the segment. A click on a segment sets it — never a key that steps. | `MenuArt.ChoiceWell` + flat token rects (`TycoonHud.ChoiceControl`) | the settings' ON/OFF and two-way options (2026-09-29) |
| **METER** | A level as a straight NEON TUBE: dark glass end to end, its LIT copy clipped to the level in whole texels (a mask, not a sprite per length), a Graphite[3] tick under every tenth, a Graphite CLAMP (lit top, shaded right and foot, a slot where the tube runs) holding it at the level; SMALL KEYS − and + either side and the level in Cyan[4] after. Dark with the sound off, the level kept in Cream[2]. The seek bar is the same tube without ticks, the clamp riding the song. | `ChromeArt.NeonBar` ×2 + `RectMask2D` + `MenuArt.Clamp` (`TycoonHud.MeterTube`) | the settings' three levels and the track (2026-09-29) |
| **CYCLE** | [<] the value on a Night[0] well (Cyan[4], the widest value + 32, at least 144) [>]. With nothing to choose, its keys are DEAD (dark glass) and the value Cream[2]. | `TycoonHud.CycleControl` | RESOLUTION, FRAME RATE (2026-09-29) |
| **SMALL KEY** | A 34² SIGN KEY with no word: its plate (no terrazzo) and a 9×9 NEON MARK (15 texels with its rings, 30 units, two in from every side), half-lit CYAN at rest and struck lit under the pointer, the rim and first band only. Small controls never light magenta — the house's colour belongs to titles, frames and verbs. | `SignKey` + `NeonIcons.SmallView` (`TycoonHud.SmallMarkKey`) | the meters' − +, the player's transport, the cycles' arrows (2026-09-29) |
| **LANGUAGE LIST** | Every language as a cell (a SIGN KEY's plate without terrazzo, 256×36): its flag 1:1 and its own name in a face that draws it — the house face at 16 for Latin-1, Fusion Pixel 12 for Japanese and Chinese, Galmuri11 12 for everything else — the bracket left to the flag and the strip. THE PICK: a Cyan tube; POINTED: a ClubBlue tube (both under the flag, so their light never lies on it); THE LANGUAGE SPOKEN: a lit Cyan BEAD at the cell's right end. No hover tips. | `LanguageFonts.ListFace` + `MenuArt.Bead` (`TycoonHud.BuildLanguagePage`) | the settings' LANGUAGE page, three columns of ten (2026-09-29; the flag grid with its tips retired) |
| **FLAP BOARD** | A split-flap display: ONE housing — a case, one run of faces, one hinge across them, one lip under them — with a hairline seam where one flap meets the next. Never a row of equal boxes. The letter is cut into three masked bands, and the band across the hinge draws the same glyph one step down its ramp: a hinge that CUT the letter turned a W into a ₩. A turning flap shows its falling leaf over the lower half for half a step. | rects + `Text` in three `RectMask2D` bands (TycoonHud.Curtain `SignFlap`) | the curtain's night name, and the week number in the sign's foot (2026-09-28) |
| **SHUTTER** | A roller shutter: the box it rolls out of, slats in two alternating steps, a bottom bar and its pull — the closed day's fitting, because a bar that does not open has its shutter down, not a dimmer bulb. Drawn long, it rolls down over a whole week; it slides, it does not stretch. | `ChromeArt.RollerShutter` (baked in the night ramp, shown at exactly 2×) | Sunday under the curtain's week; the Sunday the curtain plays between Saturday and Monday (2026-09-28) |
| **WELL** | The recess an instrument's glass sits in, routed INTO the beam: chamfered corners, the top edge dark (light falls from above — a recess shades where a box shines), a lit lip along the bottom, and a floor that IS the display glass. One 9-sliced sprite, any width. (A PixelLab-generated backplate held this row for one build on 2026-08-19 and was withdrawn on the author's next sentence — "oluşturulan takvim görseli bozuk duruyor" — so chrome-is-never-generated stands unbroken.) | `ChromeArt.Well()` | the hour, the till, the standing (the week left its last well, the curtain's day card, on 2026-09-28 — see NEON TUBE) |
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

**The till reads in the hour's own hand (second pass, 2026-09-06):** the coin-and-caption well
was sent back; the figure is `SegmentFigure` — the clock's seven-bar machine, six cells (a sign, a
dollar, four digits), right-aligned, the unused leading cells ghosted — in a matching well. The
readings' tips carry their numbers, re-read every frame the tip is up (`ShowPropTip` with a
`Func<string>`): SERVICE "tonight's drinks: 2.4 of 5", COMFORT "the room now: 1.3 of 5", and the
stars "a step a night toward the lower of — SERVICE 2.4 · COMFORT 1.3 = TONIGHT 1.3". The
comfort medallion is GOLD (Tools/medallion_icon.py, the Amber disc over a Malt rim).

**Speech (2026-09-06):** the balloon is a real rounded rectangle (`SpeechBox`, a 5.5-pixel radius,
a two-pixel ink rim), set in the body face at 16, REGULAR and ragged-right — never bold, never
centred. Balloons never overlap: `SeparateSays` stands each over its own head every frame, walks
the row and pushes any two that touch apart by half the overlap each, keeps the row inside the
picture, and then puts each TAIL back over its speaker (clamped to the balloon's rim), so a balloon
may be shoved aside but its pointer never is.

**The week's job is a notice on a plate (2026-09-06):** the house card cut to the line, a
magenta pip at its head, the count as a fraction of the week ("ECE · 2/5 · PERFECT POURS"), at
full opacity — and the whole row goes with the job, since an empty plate is a hole in the screen.
*(Superseded 2026-09-28 by the hostess's book, GDD_MEVCUT §9.134: the plate went with the weekly job.)*

**The market's shelf (2026-09-06):** every product fills its box's height at whatever scale that
takes (`PlaceProduct` no longer floors the scale under 3x — a 60-row drawing stood at 1x beside a
48-row one at 2x); a comfort figure on an upgrade tile is led by the medal (`TileSpec.MetaIcon`,
"+1.25 COMFORT"); the restock crate is drawn (Tools/restock_icon.py); THE WALLS (the plaster
ladder) and ON THE WALL (the picture, the screen) are two shelves.

**The market's card, laid out again (2026-09-07, the author: "marketteki ürün kartlarının
düzenini en başından tekrar tasarla"):** five bands, every card the same — the picture in a
recess the card's width; its rung as a row of five small stars along the recess's foot (the
ladder down the side is gone); the name on its dark plate; one line of fact — the stock as a bar
read left to right with its figure, or the meta word with its mark; the state row, then the foot
with the price tag and the key. The aisle keeps its place in PIXELS across a basket rebuild
(`_shopScrollPx`, restored after a forced layout pass) — the normalised figure was restored
against a height the nested grids had not measured yet.

**Balloons stack, they do not drift (2026-09-07, the author: "konuşma balonları gereksiz
kayabiliyor … dikey ve yatay olarak esnek"):** the row of balloons is solved once per set of
balloons and sizes (`_saysSig`) and left alone until that set changes; a balloon may slide up to
48 units along its row to clear a neighbour, and past that it climbs a storey above the balloon it
would have covered, its tail lengthening to keep pointing at its own drinker. Speech types at 12
characters a second (was 20).

**The cellar says its groups (2026-09-07, the author: "içeceklerin hangi grupta olduğu
anlaşılsın, mouse ile üzerine gelmeden"):** the stock stands by family — gin, vodka, rum, whisky,
tequila, liqueurs, then bitters, syrups, juices, soda & tonic, mixers (`CellarGroup` /
`CellarGroupOrder`) — and the family's name is written on the shelf under each run
(`StepCellarLabels`, riding the drawer). The towel fades to a fifth while the cellar is open and
is hit only where its picture is (`alphaHitTestMinimumThreshold`). Liquids are one step louder
everywhere (`UITheme.Vivid`, chroma ×1.35 on the measured table).

**The cellar is a cabinet and its words stand on its own plates (2026-09-28, the author picked the
Art Deco cabinet: "Mahzen tasarımı = C · Art Deco vitrin"; GDD_MEVCUT §9.143):** ten niches, one
family each (`LastCall.Game.CellarCabinet`), and every niche carries a name plate DRAWN into the
cabinet (`Tools/cellar_cabinet`) — the plate is the room's, the WORD is still the HUD's
(`StepCellarLabels`, `chrome.cellar.group.*`, never baked, so every table keeps its own word): the
body face at 16, at 8 when a language's word will not fit the plate's enamel. **An "&" on a plate is
spelled out** (`book.index.ampersand`, the book index's rule of 2026-08-25): the body face draws the
ampersand at 16 as a bar with two nubs, and "SODA & TONIC" read as "SODA $ TONIC" — the money's sign.
The English word then outgrows the plate and is set at 8; legible beats large. The plate's enamel
says the niche's STATE (§5): cream when it holds stock (ink Night[1]), Night when it is empty (ink
Cream[1]) — the player reads there is no rum before reaching for it. **A name plate is never amber:**
amber is the money's colour (§5) and the primary key's (§2), and ten amber plates read as ten
primary keys (the critic's fix). The HUD plate the runs used to wear — a Night card with a magenta lip — and
the staircase that dropped a neighbour's plate a row went with the runs: a niche's plate cannot meet
another's. The cabinet's material follows the counter's tier (plywood, black lacquer with neon, navy
with mirrors and gold); the chrome over it does not.

**The beam reads the night (2026-09-07, the author: "üst bar düzenini en baştan düzenle …
haftayı görmeye artık gerek yok sadece günü görsek yeter, para yazısıyla saat yakın olmamalı, yıldız
konfor kalp kısmını hizalı"):** the week instrument left the beam (since 2026-09-28 the week is shown only on the curtain's night
sign — its tubes under the nights and the flap in its foot; see NEON TUBE / FLAP BOARD);
beside the hour stands a NIGHT well — the count, the night's name, tonight's crowd — with the week in
its hover line. The till moved to the far side of the beam, left of the house's readings, so two
seven-bar readouts never sit together. The star row and the two house strips share the beam's centre
line (`RowY` 0, strips at ±9). The settings key wears a generated 32-pixel gold cog
(`Items/cog3d.png`, the star3d family) at exactly 1x. *(Stale since 2026-09-28: the beam carries no keys. No gold
icon is drawn on any key since 2026-09-29 — the menus wear NEON MARKS, §1.)*

**The cellar's card (2026-09-07, the author: "büyük boy bir kart … kullanıldığı tarifleri, ismini,
fiyatını gibi detaylı küçük kompakt bir tasarım"):** the one-line caption is a card now
(`BuildCellarCard` / `OnCellarHover` / `StepCellarCard`): name, style · tier · price a bottle, what is
left in it (red when nearly out), the house drinks it goes into (four, then "and N more") and the
no-shake row on anything carbonated. Stood over the bottle through the screen like the prop caption,
kept inside the window sideways.

**The cellar's bottle comes up with its card (2026-09-06, the author: "backbarda alkollerin
isimleri gözükmüyor, gözükürse de üst üste binebilir ... bir kart içerisinde olmalı"):** thirteen
names under thirteen bottles print over each other, so the name comes up WITH the bottle — the
hover caption on its card over the bottle (`ShowPropTip` with an icon and a line): the name, the
house drinks that call for it ("IN GIN SOUR · GIN & TONIC +1", `MenuDrinksUsingStyle`), and a
NO-SHAKE mark (`ChromeArt.NoShake`) on anything carbonated. The bottle's drink rocks with it: the
glow's followers are wired after the plates exist (`RefreshCellarMovers`), which they were not.

**A gauge's captions stand outside its mask (2026-09-06):** the standing gauges' "40% VODKA" lines
were children of the bands inside the tin's Mask and were cut off at its silhouette; they live on
the rig's own `Labels` rect now. The tin itself may be drawn by the author: `Items/gauge_tin.png`
(48×106, shown at 2×) replaces the drawn outline and `gauge_tin_solid.png` its silhouette.

**A locked ingredient's tag is one small line (2026-09-06):** "LOCKED · NOT IN THE WELL" at the
body face's 8 under the name, inside the 46 units its row owns — at 16 it wrapped to three rows
and ran over the next ingredient and the gate notice.

**The settings are a WINDOW, not a list (2026-09-06):** a scrim, a titled plate in the centre
(`ChromeArt.Card`, a band with the cog and the neon under it), the settings as rows — the name
at the left, the control at the right, a note under a name that needs one — grouped AUDIO /
DISPLAY / THE RUN, so the one thing that throws the night away (START OVER → NEW RUN, on a
brick key) sits furthest from the thumb. The volume is a five-block meter with a key at each
end. The developer's bench keeps one small key at the plate's foot; the bench itself is set
in a plain proportional face, in Turkish, in four real columns — it is the author's tool and
not part of the game, so the pixel law does not reach it.

**The beam, left to right (2026-09-28, second pass — the author: "sadeleştirelim çok şey var şu an
üst barda. butonları kaldır sadece yıldız gözüksün konfor ve servis yıldızın üstünde hover ile
gözüksün. para göstergesi saatle aynı olmasın"; GDD_MEVCUT §9.107 has the numbers):** the HOUR in its
well · nothing in the middle · the TILL in a well of its own, in the house's figures face at 24
beside the drawn stack of bills — money, not a second seven-bar readout beside the clock · the
STANDING's five stars at the right edge, the only rating on the beam and the ladder's door. **No
keys on the beam**: the music has its own keys and Settings' player, the settings open from
Escape's pause menu and the front door. What the beam no longer carries hangs under it on ONE CARD
while the pointer is on a well — the room's tip plate with rows, not a new surface: under the
stars, the standing's sentence and tonight's reading over the house's two strips (COMFORT over
SERVICE, each beside its word); under the till, the money, tonight's bill, what a red night is and
how many are on the books, with a red row while closing now would add one. It sits over the pinned
note and the bench (its own canvas at 29) and goes down under every sheet that covers the whole
bar. The tube stays the ONE state light, with its fourth state, **OUT** — a dead Night glass, no
light off it — which only the game over sets, when the bar's sign dies. A bill above the till never
reddens the tube; red means money actually under zero. (The first pass that morning put a REGISTER
in the middle — the till in seven cells, a BILL and three red LAMPS — with the licence's strips, a
JUKEBOX key and the neon cog on the right; the author found it still too much.)

**The game over (2026-09-28, GDD_MEVCUT §9.138)** is built from this list and nothing new: the
room under a dark and the beam under a lid, the night tape's own stock printed as the bar's last Z
report, a PLATE for the landlord's notice with the night stamp's construction struck on it, and one
KEY — the pack's orange, the screen's one primary — to the front door. No "GAME OVER" caption: the
beam's tube going OUT says it. (Every night before it now ends under the CLOSED SIGN, which is why
the landlord's stamp says EVICTED and not CLOSED.)

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
