# Pixel-art key art for Steam: how to make it look premium (Malibu Club)

Sources are listed at the end. Most pages were read as search snippets only, because the egress proxy blocked direct fetches of partner.steamgames.com, saint11.art, dev.to and indiedb. Points marked **[memory]** come from my own knowledge and were not checked against a source. Points marked **[computed]** I worked out myself, either from the asset sizes or from the repo (`Tools/steam_page/*.py`, `Tools/title_sign/build_logo.py`, `Assets/Resources/Logo/*`).

---

## 0. Twelve rules to follow everywhere

1. **One grid per image.** Every layer except the logo uses the same art-pixel size. Within an asset you never resample, rotate or blur pixel-art layers.
2. **Use 4x everywhere** (1 art pixel = 4×4 asset pixels) for the whole set. Steam shows most assets at half size on 1x screens, so 4x becomes 2 screen pixels and stays crisp. 2x becomes 1 pixel and turns to mush. Odd scales like 5x become 2.5 pixels and blur.
3. **When a size doesn't divide by 4**, render a native canvas one art pixel larger, scale it ×4 with nearest neighbour, and cut the extra **from the right and bottom edges only**. That keeps the grid lined up at (0,0) and with Steam's 2:1 downscale.
4. **Draw Roxy once** at one native resolution: a head about 32 art px tall, drawn down to mid-thigh, about 170 art px. Each asset picks a crop of her (bust, waist-up or 3/4). Never pick a different scale for her.
5. **Every asset is a different camera crop of the same world:** the same sunset, the same sun position behind Roxy, the same light direction, the same palette.
6. **One palette for the whole project, about 50–56 inks, never more than 64.** That is the game's 35 UITheme inks plus three hue-shifted ramps the key art needs: the sunset sky, skin and red hair. Never build a palette per image.
7. **Save the strongest contrast for two places:** Roxy's face and rim, and the logo's cream core. The logo always sits on a dark field (L\* ≤ 25), never on the bright horizon.
8. **Rim light is 1 art px** of sunset gold, only on edges that face the sun. Shadows hue-shift toward violet or magenta. Fill light comes from cool neon.
9. **Outlines are 1 art px everywhere, using selective outlining** (no pure black). Outer silhouette edges on reusable layers stay hard. Do no anti-aliasing (AA) toward a background you don't know yet.
10. **Dither only in large sky transitions**, using ordered 2×2 or Bayer 4×4 seams 1–2 rows tall. Never dither on a face, on the logo, or anywhere in the small capsule.
11. **Glow and other effects are stepped alpha bands on the grid**, at most 4 bands. The logo's halo already works this way. Never use a Gaussian bloom.
12. **Upload PNG only.** Steam re-encodes store capsules to JPG, so give it a lossless source. Indexed PNG-8 is lossless when you have 64 or fewer inks.

---

## 1. Cheap vs. premium pixel key art

| Looks cheap | Looks premium |
|---|---|
| Character and background at different pixel sizes (mixels). A sprite rotated or scaled by a non-integer factor. | One art-pixel size across all layers in an image |
| Bilinear or Lanczos upscale: soft, uneven "pixels" | Nearest-neighbour upscale at a whole-number factor, done last |
| Hundreds of colours, airbrushed gradients over pixels | 32–64 inks, hue-shifted ramps, flat clusters |
| Random or error-diffusion noise ("dirt") | Ordered dithering, used only for gradients |
| Black outlines everywhere, pillow shading, banding, jaggies, orphan pixels | Selective outlines, form-aware shading from one light, clean line steps |
| Soft bloom over crisp art (a mixel made of light) | Stepped glow bands on the grid |
| JPEG artifacts on edges | PNG sources, even scale, grid lined up for the encoder |

- Mixel: a pixel whose size or rotation doesn't match the rest of the artwork. The usual causes are wrong-method upscaling and mixing sprites made at different resolutions. Fix: scale only by 200%, 300% and so on. ([yari-pixels](https://yari-pixels.github.io/Articles/mixels.html), [Wiktionary](https://en.wiktionary.org/wiki/mixel), [Saint11 "Consistency"](https://saint11.art/blog/consistency/))
- Banding, pillow shading, jaggies and orphan pixels are the classic tells. ([Derek Yu](https://www.derekyu.com/makegames/pixelart.html))
- Many big pixel games, such as Katana ZERO, use **painted** key art (Dave Zhang's store illustrations: [ArtStation](https://www.artstation.com/artwork/R3n55e)). That route is legitimate. If you choose true pixel key art, it has to obey every rule above, or it reads as worse than either a painting or real pixel art.

---

## 2. Pixel density and integer scaling

### What Steam actually shows

- In 2024 Steam doubled the store capsule and library header sizes for high-DPI screens and the Steam Deck OLED. The old 1x sizes, such as header 460×215 and main 616×353, are what a 1x display effectively shows. ([Game Developer](https://www.gamedeveloper.com/business/steam-increases-store-image-requirements-details-phase-out-of-old-specs))
- The Terminal Reign developer drew at half the header size (2x pixels). Steam showed the 920×430 capsule at 460×215, so each art pixel became one screen pixel and the definition was lost. Stardew Valley and Noita work because they use pixels of **4×4 or bigger** relative to the upload size. ([Terminal Reign devlog](https://juhrjuhr.itch.io/terminal-reign/devlog/1663656/devlog-pixel-art-steam-capsules))
- The small capsule (462×174) is automatically shrunk to 184×69 and 120×45, which are factors of about 2.51 and 3.85. Those are non-integer, so **the grid will be destroyed whatever you do**. Design that asset as big shapes plus a logo that "should nearly fill the small capsule". ([Steamworks, store assets](https://partner.steamgames.com/doc/store/assets/standard), via search snippet)
- The library hero gets an automatic half-size copy at 1920×620. **[memory]** Users can resize the library grid, so the 600×900 library capsule is resampled at arbitrary scales.
- Steam does not upscale small uploads, so upload each asset at its exact size. ([presskit.gg](https://presskit.gg/field-guides/steam-capsule-art-guide))

### Scale for each asset [computed]

"Exact scales" are the whole-number factors that divide both sides. The 4x plan gives the native canvas, then how many asset pixels to cut from the right and bottom.

| Asset | Size | Exact scales | 4x plan | Logo map to use |
|---|---|---|---|---|
| Header | 920×430 | 1, 2, 5, 10 | 230×108, cut 0 / 2 | x1 (560×212, 61% of width) |
| Small capsule | 462×174 | 1, 2, 3, 6 | 116×44, cut 2 / 2 | **new x0.75 (420×159, 91%)** |
| Main capsule | 1232×706 | 1, 2 | 308×177, cut 0 / 2 | x1.25 (700×265) |
| Vertical capsule | 748×896 | 1, 2, 4 | 187×224, exact | x1 (75% of width) |
| Page background | 1438×810 | 1, 2 | 360×203, cut 2 / 2 | none |
| Library capsule | 600×900 | 1–6, 10, 12 | 150×225, exact | x1 is 93% of width, too tight; **new x0.9 (504×191)** |
| Library hero | 3840×1240 | 1, 2, 4, 5, 8, 10 | 960×310, exact | **none (rule)** |
| Library logo | 1280×720 | 1, 2, 4, 5, 8, 10 | n/a | **new x2.2857 (1280×485)**, transparent |
| Library header | 920×430 | 1, 2, 5, 10 | 230×108, cut 0 / 2 | x1 |
| Event cover | 800×450 | 1, 2, 5, 10 | 200×113, cut 0 / 2 | x1 (70% of width), optional |
| Event header | 1920×622 | 1, 2 | 480×156, cut 0 / 2 | x1.5–x2, inside the central 940 px |
| Community icon | 184×184 | 1, 2, 4, 8 | 46×46, exact | coupe mark or Roxy's face, no wordmark |
| Client icon | 256×256 | 1, 2, 4, 8 | 64×64, exact | coupe mark; hand-draw 32 and 16 for the .ico |

- **Cropping rule:** never centre-crop a 2 px overflow as 1 px top and 1 px bottom. That shifts the whole grid by one asset pixel. When Steam then halves the image, every art-pixel edge lands on a half pixel and the whole image blurs. Cut only from the right and bottom. Losing part of one art pixel at the frame edge is invisible.
- **Optional hero reuse:** at 8x the hero's native canvas is 480×155, almost the same as the event header's 4x canvas (480×156). One native picture could serve both. The cost is that the hero then looks twice as chunky as everything else. I recommend 4x everywhere for a single apparent density.
- **Roxy at 4x fits every asset without changing scale.** With a 32 px head and a 170 px 3/4 figure:
  - Main capsule (177 rows): 3/4 figure.
  - Header (108 rows): waist-up.
  - Vertical (224 rows) and library capsule (225 rows): 3/4 figure under a logo band about 60 rows tall.
  - Hero (310 rows): 3/4 figure inside the safe area.
  - Community icon (46×46): her head crop alone.

  If something doesn't fit, change the crop, never the scale.

### The logo is the one allowed exception [computed]

- `Assets/Resources/Logo/logo_x*_map.bytes` are **pixel-native at screen scale**. Even the x4 map (2240×848) has runs of one pixel, so it is not a ×4 enlargement of x1. The logo therefore lives on a 1-screen-pixel grid, not on the 4x art grid.
- Placing it over 4x art as a title layer at screen resolution is a common convention **[memory]**: titles and wordmarks often sit outside the art grid. It is acceptable on four conditions:
  - Use a map that is exactly the right size. Never resample one.
  - Place it at integer coordinates, preferably multiples of 4 so it lines up with the art grid.
  - Its 4-band halo (alpha 12 / 34 / 78 / 150) is already stepped. Keep it that way.
  - It sits on a dark plate area.
- `build_logo.py` draws a map at any scale (`SETS`). Adding x0.75, x0.9 and x2.2857 produces the **same logo** at new sizes, not a redraw. This still needs the author's OK, because "the logo is fixed".

---

## 3. One shared palette

- **Size:** hi-bit key art usually uses 32–64 inks. With 32–64, every ink has to do double duty: one material's shadow is a darker material's midtone. ([hue-shifting research notes](https://github.com/HaydenReeve/Aseprite-General-Scripts/blob/main/Compress%20Palette%20with%20Hue%20Shifting/research/02-hue-shifting-technique.md))
- **Hue shifting:** about 10–25° per ramp step. Slynyrd's Mondo palette uses +20° per step (8 ramps × 9 swatches). Shadows go cooler (blue or violet), highlights go warmer (yellow or orange). ([Slynyrd Pixelblog 1](https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes))
- **What the game's ramps measure [computed, CIELAB]:**

  | Ramp | L\* by step | Hue across the ramp |
  |---|---|---|
  | Night | 3 / 6 / 11 / 18 / 25 | about 0° |
  | Magenta | 22 / 34 / 46 / 57 / 69 | +5° |
  | Cyan | 23 / 37 / 55 / 74 / 88 | −19° |
  | ClubBlue | 11 / 21 / 32 / 46 / 62 | — |
  | Amber | 22 / 43 / 60 / 72 / 83 | +9° |
  | Cream | 27 → 92 | — |

  Steps of 11–17 L\* are good. **The hue shift is weak**, which is fine for UI but flat for a painted sunset.
- **Ramps to add for the key art:**
  - **Sunset ramp.** It already exists in `plate.py` (steam_kit sky): `#643F89 → #A44B9D → #EA4AA0 → #FF8C56 → #FFB457 → #FEEF7E`, sweeping from about 270° to 52°. That is a textbook hue-shifted sky. Bridging orange to deep blue through magenta or purple avoids a muddy brown-grey. ([synthwave palette notes](https://www.pixel-editor.com/palettes/synthwave))
  - **Skin ramp** (warm tan), 5 steps. Shadows shift toward magenta or violet, highlights toward peach or amber.
  - **Dark-red hair ramp**, 4 steps. It can share its darkest steps with the burgundy jumpsuit, alongside Magenta[0..1].
  - **Gold** (hoops, belt) comes from Amber; nothing new needed.
  - Total: 35 + 6 + 5 + 4, about 50 inks.
- The palette is fixed **once for the whole project**, and every layer of every asset is snapped to it.

---

## 4. Light, silhouette, rim light, focal contrast

- **One light design, used in every asset:** a low sun **behind Roxy**, so she is backlit.
  - Key/rim: warm, `#FEEC7B` or `#FFB457`.
  - Fill or bounce: the club's neon, Cyan[3–4] on the side away from the sun, or Magenta.
  - Shadow masses: Night, ClubBlue or dark Magenta.

  This warm/cool split is the "Miami" look.
- **Rim light:** 1 art px wide (4 asset px), 2 px at most on large forms, and only on edges that face the sun. A rim separates the subject from a background of similar value. ([gamineai](https://gamineai.com/blog/lighting-2d-action-game-silhouettes-rim-ambient-shader-basics-2026))
- **Reading the silhouette:**
  - Against the bright horizon or sun, Roxy reads as a **dark shape**. Keep her body masses at or below the sky's value.
  - Against a dark sky or sea, the rim does the work.
  - Test it: fill the figure with one flat colour. The pose (hand on hip, a coupe raised, hair shape) must still read at 120 px tall.
- **Focal contrast:** the darkest dark next to the lightest light appears in only two places: the face and rim, and the logo.
  - Cream core (L\* about 92) on a plate of L\* ≤ 25 gives ΔL\* of 65 or more.
  - The magenta tube (Magenta[3], L\* 57) needs a background of L\* ≤ 25 (ΔL\* ≥ 30).
  - The WCAG 3:1 large-text contrast ratio is a sensible minimum for the logo against its local background. **[memory]**
- **The logo's field must be dark.** Use the top of the sky, where the gradient is at its night end, or the lower sea bands (`#180C25` / `#12081A`). Never put it over the horizon bands (`#FF8C56` to `#FEEF7E`).

---

## 5. Outlines, selective outlines, AA, clusters

- **Outline colour:** a darker shade of the material it borders, roughly one step below its darkest tone. Pure `#000` looks detached. ([Lospec, outlines part 2](https://lospec.com/articles/pixel-art-outlines-part-2-using-color/), [pixnote](https://pixnote.net/en/learn/outlines/))
- **Selective outline (selout):** wrap the shape in a dark outline. Lighten it to a body colour on the lit side. Keep the dark line wherever the sprite meets the background, to protect the silhouette. ([pixnote](https://pixnote.net/en/learn/outlines/))
- **Line weight:** 1 art px in every layer. Palms and the skyline are filled silhouettes with no outline. Never use 2 px lines on one layer and 1 px on another.
- **Hand AA:** only on curves with staircase steps bigger than 1:1, using intermediate inks from the ramps. ([Lospec AA tutorials](https://lospec.com/pixel-art-tutorials/handmade-antialiasing-by-nemesis42))
- **Layers reused over different plates:** a reusable layer's **outer** edge stays hard (dark selout, no AA). An edge anti-aliased toward orange sky looks wrong over dark sea. Do edge AA, if at all, after composing each asset. At 4x it is rarely needed.
- **Clusters:** shade in readable clumps of colour, not scattered single pixels. ([Lospec, clusters](https://lospec.com/pixel-art-tutorials/beginners-guide-clusters-by-artem-brullov)) Remove orphan pixels and doubles on lines. ([Derek Yu](https://www.derekyu.com/makegames/pixelart.html))

---

## 6. Dithering

- **Use it** to fill a missing midtone in large gradients (sky, water, light falloff). **Avoid it** on small forms, faces and key silhouettes. Ordered patterns blend; random scatter reads as dirt. ([Divoom](https://divoom.com/blogs/setup-ideas/pixel-art-dithering-when-to-use-and-stop), [pixnote](https://pixnote.net/en/learn/dithering/))
- **This project:** the plate's 1–2 row scan-line seams between sky bands already work as structured dithering. Keep them, and add no other dithering.
- **Steam-specific [computed]:** a 1-art-px checkerboard at 4x survives an exact 2:1 downscale (it averages to a flat colour). At non-integer scales (184×69, 120×45, the resizable library grid) it **moirés**. So: no dithering in the small capsule, and none near the logo.

---

## 7. Effects and glow

- Neon glow, sun haze and reflections are drawn as **at most 4 alpha or ink bands on the art grid**, the same scheme as the logo halo (12 / 34 / 78 / 150).
- Sun reflections on the water are short horizontal dashes 1 art px tall (`REFLECT` inks in `plate.py`).
- No Gaussian blur, no soft bloom, no motion blur, no lens flare at screen resolution.

---

## 8. Readability at thumbnail size

- **Design small first:** block in the composition as a 120×45 rectangle of flat colour. If it doesn't read there, it never will. The subject must be dramatically lighter or darker than its background. ([StraySpark](https://www.strayspark.studio/blog/steam-capsule-art-store-page-conversion-guide-2026))
- **Small capsule logo:** it "should nearly fill" the capsule ([Steamworks](https://partner.steamgames.com/doc/store/assets/standard)). In practice, 85–95% of the width.
- **Tests to run on every asset:**
  1. Downscale to Steam's real display sizes: header 460×215, main 616×353, small 184×69 and 120×45, library capsule about 150×225 and 300×450 **[memory]**.
  2. A greyscale copy. There should be three clear value masses: the dark frame, the bright sun or horizon, and the figure and logo.
  3. A 25% shrink, or a blur of about 2 px at display size, to check that the figure still pops.
  4. A real store preview, not a paste-over of a screenshot. ([Terminal Reign devlog](https://juhrjuhr.itch.io/terminal-reign/devlog/1663656/devlog-pixel-art-steam-capsules))
- **Minimum stroke widths:** neon tube strokes stay at 4 px or more at the 600-wide library capsule and 6 px or more at the 462 small capsule, so they still exist at 120×45.

---

## 9. One layered master, many aspect ratios

Practice for key art: start from one master, crop and re-adapt it for each size, and keep the colour, the character rendering and the type consistent. Request backgrounds that are filled out past the frame so elements can move. Re-frame and re-test for each capsule rather than reusing one crop. ([presskit.gg](https://presskit.gg/field-guides/steam-capsule-art-guide), [Immutable](https://www.immutable.com/guides/steam-capsule-best-practices))

### The layer stack, all authored at native 1x and only enlarged ×4 at the end

| Layer | Contents | How each asset gets it |
|---|---|---|
| L0 plate | Sky bands with seams, sun, sea bands | **Procedural, re-drawn per canvas** (`plate.py` already does this). Never cropped or resampled. |
| L1 far midground | Skyline with 1 px windows | Anchored to the horizon line |
| L2 near midground | Palms, pier, club sign | Anchored to the frame edges, used as framing |
| L3 Roxy | The single native drawing | Anchor point plus a minimum-crop box per crop type |
| L4 foreground props | Coupe, lime, bar edge | Optional |
| L5 effects | Glow bands, reflections, sparkles | **Re-drawn per layout** |
| L6 logo | Native map at screen resolution, placed after the ×4 | One map per asset size |

### Layout rules by aspect class

These are proposals built on the Steam rules cited.

- **Ultra-wide (hero 3.10:1, event header 3.09:1)**
  - Hero: no text or logo. Critical content (Roxy's face) stays inside the central **860×380 safe area** ([Steamworks library assets](https://partner.steamgames.com/doc/store/assets/libraryassets), via snippet). Check against the template whether that box is measured in 3840 or 1920 units. Design to the stricter reading.
  - Steam overlays the library logo at bottom-left (the default) or centred top, middle or bottom ([Steam client beta thread](https://steamcommunity.com/groups/SteamClientBeta/discussions/0/803471531442179418/)). Keep **x 0–40%, y 55–100%** dark and low in detail.
  - Event header: critical content inside the central 940 px. ([event assets](https://partner.steamgames.com/doc/store/assets/eventassets), via snippet)
- **Wide (header 2.14, main 1.745, event cover and page background 1.78)**
  - Logo in the left 55–60% of the width, in the upper or middle third, over dark sky.
  - Roxy in the right 35–40%, facing in toward the logo: waist-up on the header, 3/4 on the main capsule.
  - Sun behind her head and shoulders; horizon at about 58–62% of the height.
  - Event cover: Steam adds the game name and icon next to it, so branding inside it is optional. ([event assets](https://partner.steamgames.com/doc/store/assets/eventassets))
  - Page background: no logo and no character, or only a faint one. Keep it low-contrast and dark, because store content covers the middle. ([presskit.gg](https://presskit.gg/field-guides/steam-capsule-art-guide)) Steam fades it into the page colour. **[memory]**
- **Small capsule (2.66)**
  - The logo fills 85–95% of the width.
  - Background is the plate only: horizon, the top of the sun, palms at the edges. No Roxy, or at most a face sliver at the right edge.
- **Tall (vertical 0.835, library capsule 0.667)**
  - Logo band in the top 25–30%, over the night end of the sky.
  - Roxy centred in a 3/4 crop, sun behind her shoulders at about 55% height, horizon at about 60%, palms framing both sides.
- **Square (icons)**
  - Community icon: Roxy's head crop at 4x (46×46), or the cyan coupe mark.
  - Client icon: 64×64 at 4x, plus **hand-pixelled** 32 and 16 versions. Shrinking the large one does not work.
  - The silhouette has to work with two colours.

### Assembly per asset

1. Build the native canvas from the table in section 2.
2. Compose L0–L5 at native size, every placement on integer art pixels.
3. Scale ×4 with nearest neighbour.
4. Cut the overflow from the right and bottom.
5. Place L6 at integer coordinates (multiples of 4).
6. Export PNG.

---

## 10. Turning AI "pixel-style" takes into true-grid pixel art

### Pipeline

The approach used by proper-pixel-art, unfake.js, Pixel-Extractor and Retro Diffusion:

1. **Find the implied grid.**
   - proper-pixel-art: Canny edges, morphological closing, a probabilistic Hough transform keeping only near-vertical and near-horizontal lines, clustering of those lines, then the grid spacing as the **median spacing** with outliers removed. ([proper-pixel-art](https://github.com/KennethJAllen/proper-pixel-art))
   - unfake.js: run-based or edge-aware detection, including **fractional grids** for models that resample their canvas. ([unfake.js](https://github.com/jenissimo/unfake.js/))
   - Expect drift. One apparent pixel can be 14 px and the next 18 px ([SpriteCook](https://www.spritecook.ai/pixel-grid-detector)). Tools fit drift and phase locally ([sindri-pixel PR #13](https://github.com/vardirhq/sindri-pixel/pull/13), [Pixel-Extractor](https://github.com/univeous/Pixel-Extractor)).
   - Retro Diffusion says its fixer recovers the exact native resolution on 77% of images. That is the vendor's own claim. ([Retro Diffusion](https://retrodiffusion.ai/tools/pixel-art-fixer/))
   - **Rule:** if the period drifts more than about 5% across the image, there is no global grid. Use local grid fitting, or treat the take as reference and redraw.
2. **Sample each cell from its centre.** Use the centre about 50% of the cell and take the **mode** (dominant colour) or the **median**. Never average the whole cell and never use bilinear: the borders carry the model's blur. unfake.js offers dominant, median and content-adaptive methods, which introduce no new colours. Your `pixelsnap.py` already samples the centre with a median.
3. **Quantize to the fixed project palette** in a perceptual colour space (CIELAB or OKLab), with **no dithering**. Error diffusion adds noise.
   - proper-pixel-art quantizes first, then takes the most common colour per cell.
   - Use k-means only **once for the whole project**, to find the missing ramps (skin, hair, sunset). Then hand-tune those into hue-shifted ramps.
4. **Automated cleanup:**
   - Despeckle: a lone pixel whose 4 neighbours agree takes their colour.
   - Make alpha binary (0 or 255) for sprite layers.
   - Remove fringes of the generator's background colour. Generate on a flat chroma-key colour that is not in the palette, and key it on the native grid.
5. **Hand pass, which the tools cannot do:**
   - Redraw the eyes, mouth and brows. At a 32 px head these are 1–3 px features, and AI gets them wrong.
   - Fix jaggies on the contour so line steps run 1-2-3, not 1-2-1-3.
   - Redraw selout and rim to the one light direction.
   - Replace AI fake-dithering with flats or ordered seams.
   - Match Roxy to her reference sheet (hoops, lapels, belt).

### Pitfalls

- **Generating every asset separately** gives a different face, palette and pixel size in each. Generate **layers once** (plate, Roxy, props) and recompose them.
- **The character's implied pixel size differs from the background's.** Models usually render the figure finer. If each take is snapped to its own detected grid and then composed, you get mixels. Fix: choose the target native size first, then **set the period from the target**. For example, if Roxy should be 170 art px tall and she is 2040 px tall in the take, the period is 12. Use detection only to find the phase.
- **Non-integer resizing of native art to fit a layout**, such as 0.9× Roxy, breaks every line. Crop, or redraw.
- **Rotated or flipped-and-skewed elements** become mixels. Flipping horizontally is safe.
- **JPEG takes from a generator:** ringing becomes false colours in the quantizer. Always request or keep PNG.
- **Upscaling blur:** scale only at the end, only by whole numbers, only with nearest neighbour, and don't let any later step resize. ([the-pixel.art](https://the-pixel.art/articles/pixel-art-blurry-scaling-exporting/))

---

## 11. Delivery format: PNG vs JPG on Steam

- Steam accepts PNG and JPG ([Steamworks](https://partner.steamgames.com/doc/store/assets/standard)). It converts uploaded artwork to JPG, and users report blocky results on detailed or flat-colour art ([Steam forum](https://steamcommunity.com/discussions/forum/10/3047235828274478892/)). Store capsules are served as `header.jpg`, `capsule_*.jpg` and so on, while the library logo stays `logo.png`. **[memory]**
- **Upload PNG** so Steam's encoder starts from a clean source. A JPG upload is compressed twice.
- With 64 or fewer inks, **indexed PNG-8 is lossless** and small, and its tRNS chunk can carry the logo's 4 alpha levels.
- **Reducing JPEG damage [computed]:**
  - JPEG 4:2:0 averages colour over 2×2 blocks and causes colour bleeding at sharp colour transitions ([Wikipedia: chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling)). A 4x grid lined up at (0,0) keeps each art pixel equal to whole 2×2 colour blocks.
  - Avoid 1-art-px saturated details where magenta meets cyan, because the colour channels average them together.
  - Keep logo strokes thick.

---

## 12. Issues found in the repo [computed]

- `Tools/steam_page/out/capsules/*.jpg`: header, small, vertical, library, hero, event and community are saved as **JPG**. Export them as PNG.
- `pixelsnap.py`, `build_palette()`: when `palette=None` (the CLI default), each image gets **its own** 40 extra k-means colours. Every asset then ends up with a different palette, which breaks rule 6. Build the extras once from all takes together, turn them into 3 hue-shifted ramps of about 15 inks, save them, and pass that palette every time.
- `pixelsnap.py` snaps each take to its **detected** period, so the snapped native size is whatever the take implies. For the Roxy layer, force the period from her target height in art pixels.
- The logo maps are pixel-native at screen scale; see section 2. Three more sizes would fill the gaps: x0.75 for the small capsule, x0.9 for the library capsule, and x2.2857 for the 1280-wide library logo. `build_logo.py` `SETS` can draw them without changing the design, pending the author's OK.

---

### Sources
- Steamworks (read through search snippets; direct fetch was blocked): [Store assets](https://partner.steamgames.com/doc/store/assets/standard) · [Library assets](https://partner.steamgames.com/doc/store/assets/libraryassets) · [Event assets](https://partner.steamgames.com/doc/store/assets/eventassets) · [Asset rules](https://partner.steamgames.com/doc/store/assets/rules)
- [Game Developer: Steam doubles image sizes (2024)](https://www.gamedeveloper.com/business/steam-increases-store-image-requirements-details-phase-out-of-old-specs) · [Terminal Reign: pixel-art capsules](https://juhrjuhr.itch.io/terminal-reign/devlog/1663656/devlog-pixel-art-steam-capsules) · [presskit.gg capsule guide](https://presskit.gg/field-guides/steam-capsule-art-guide) · [StraySpark: the 120-pixel problem](https://www.strayspark.studio/blog/steam-capsule-art-store-page-conversion-guide-2026) · [Immutable: capsule best practices](https://www.immutable.com/guides/steam-capsule-best-practices) · [Steam logo position thread](https://steamcommunity.com/groups/SteamClientBeta/discussions/0/803471531442179418/) · [Steam PNG→JPG thread](https://steamcommunity.com/discussions/forum/10/3047235828274478892/)
- Craft: [Saint11: Consistency](https://saint11.art/blog/consistency/) · [About mixels](https://yari-pixels.github.io/Articles/mixels.html) · [Wiktionary: mixel](https://en.wiktionary.org/wiki/mixel) · [Slynyrd Pixelblog 1: palettes](https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes) · [Hue-shifting notes](https://github.com/HaydenReeve/Aseprite-General-Scripts/blob/main/Compress%20Palette%20with%20Hue%20Shifting/research/02-hue-shifting-technique.md) · [Lospec: outlines with colour](https://lospec.com/articles/pixel-art-outlines-part-2-using-color/) · [pixnote: outlines](https://pixnote.net/en/learn/outlines/) · [pixnote: dithering](https://pixnote.net/en/learn/dithering/) · [Divoom: dithering](https://divoom.com/blogs/setup-ideas/pixel-art-dithering-when-to-use-and-stop) · [Derek Yu: pixel art basics](https://www.derekyu.com/makegames/pixelart.html) · [Lospec: clusters](https://lospec.com/pixel-art-tutorials/beginners-guide-clusters-by-artem-brullov) · [Lospec: handmade AA](https://lospec.com/pixel-art-tutorials/handmade-antialiasing-by-nemesis42) · [gamineai: rim light](https://gamineai.com/blog/lighting-2d-action-game-silhouettes-rim-ambient-shader-basics-2026) · [Synthwave palette](https://www.pixel-editor.com/palettes/synthwave) · [the-pixel.art: scaling and export](https://the-pixel.art/articles/pixel-art-blurry-scaling-exporting/) · [Wikipedia: chroma subsampling](https://en.wikipedia.org/wiki/Chroma_subsampling) · [Katana ZERO key art (Dave Zhang)](https://www.artstation.com/artwork/R3n55e)
- AI cleanup: [proper-pixel-art](https://github.com/KennethJAllen/proper-pixel-art) · [unfake.js](https://github.com/jenissimo/unfake.js/) · [Pixel-Extractor](https://github.com/univeous/Pixel-Extractor) · [Retro Diffusion fixer](https://retrodiffusion.ai/tools/pixel-art-fixer/) · [SpriteCook grid detector](https://www.spritecook.ai/pixel-grid-detector) · [sindri-pixel grid-drift PR](https://github.com/vardirhq/sindri-pixel/pull/13)

Files referenced: `/home/user/FABLE5/Tools/steam_page/pixelsnap.py`, `/home/user/FABLE5/Tools/steam_page/plate.py`, `/home/user/FABLE5/Tools/steam_page/logo_render.py`, `/home/user/FABLE5/Tools/title_sign/build_logo.py`, `/home/user/FABLE5/Assets/Resources/Logo/`, `/home/user/FABLE5/Tools/steam_page/out/capsules/`