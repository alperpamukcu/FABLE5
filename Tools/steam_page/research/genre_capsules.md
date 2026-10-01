# Steam capsule teardown: pixel-art, bar/cafe and neon games, and the rules for MALIBU CLUB

## 0. Limits on this research

- In this environment the egress proxy blocks the Steam CDN (`*.steamstatic.com`), `store.steampowered.com`, `partner.steamgames.com` and every third-party guide I tried, through both curl and WebFetch. WebSearch works, so the facts about Valve's rules come from search-result excerpts of the official pages and of the guides.
- I could not open any capsule image. Each per-game description carries a tag:
  - **[src]**: a cited search result supports it.
  - **[mem-hi]**: from memory, confident.
  - **[mem-lo]**: from memory, uncertain. Check these before relying on them.
- To check quickly from a machine that can reach the CDN, use this pattern with the app IDs listed below: `https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appid}/{header.jpg | capsule_616x353.jpg | capsule_231x87.jpg | hero_capsule.jpg | library_600x900.jpg | library_hero.jpg | logo.png}`. The pattern itself is from memory.

## 1. Platform rules that limit every composition

| Rule | Source |
|---|---|
| Base capsules may carry only game art, the game's name and an official subtitle. No review scores, awards, discount text or other text. In force since 2022-09-01. | [src] gamedeveloper.com, pcgamesn |
| Small capsule (462×174): the logo should **nearly fill it** and must be **readable at 120×45**. The art may differ from the other capsules (a different crop or a separate small version is allowed). | [src] Steamworks store assets, via search |
| Header and main capsules: "graphically-centric", "use the key art and logo used for retail/marketing", and the logo must be "easily legible against the background". The library header should "focus on branding" and reuse the header or library-capsule art. | [src] Steamworks, via search |
| Library capsule 600×900: Steam auto-generates a **300×450** version. Test at that size. | [src] |
| Library hero 3840×1240: **no words at all**. The centred **860×380 safe area** is never cropped, so the main character's face must sit fully inside it. The developer chooses where the logo goes: **bottom-left, top-centre, centre, or bottom-centre**. | [src] |
| Library logo: PNG with transparency, **1280 wide and/or 720 tall**, only the logotype plus an optional logomark. Steam scales it and places it over the hero. | [src] |
| Page background 1438×810: must be "ambient", with no bright focal detail. Steam applies its own template over it. | [src] |
| Event cover 800×450: art and branding for the event. Steam always shows the game's name and icon next to it, so the logo is optional. Event header 1920×622 has a template that marks where text goes. | [src] Steamworks event assets |

Market data:
- Of 98 top-seller capsules (July 2026):
  - 20 have a readable face.
  - 40 have any character.
  - 44 sit on a dark background.
  - 24 are pure logotypes.
  - 18 fail legibility at 120 px.
- The guides say the title should take **15–25% of the capsule's area**. [src] steampageanalyzer, gamosy
- A glance lasts about **0.3 s**. Click-through is 2–4% on average, 4–7% is good, and above 7% is top tier. [src] gamosy
- Zukowski (2020):
  - The **text-left / character-right** layout with one well-rendered character on a simple background is the trend.
  - A **character looking out at the viewer** is the most reliable thumbnail device.
  - **Red and blue** two-tone schemes are hot.
  - The pixel-art games that reached top sellers showed **cartoon-proportioned characters**, not their in-game sprites.
  - Be "in dialogue with" the best-seller of your genre. [src] howtomarketagame
- Changing only the capsule took Imagine Earth from 0–3 to 40–60 sales a day. [src] howtomarketagame

## 2. Per-game teardown

Abbreviations: **H** header 920×430, **M** main 1232×706, **S** small 462×174, **V** vertical 748×896, **L** library 600×900, **Hero** library hero.

### Bar and cafe games (closest to us)

**VA-11 Hall-A** (447530)
- **Style:** the capsule is an anime illustration by the game's artist, not the in-game pixel art. [mem-hi]
- **Layout:** Jill, the bartender protagonist, is the only face, framed chest-up. The logo is a bar-sign wordmark whose official subtitle names the genre ("Cyberpunk Bartender Action"). [mem-hi for the subtitle; mem-lo for left/right placement]
- **Colour:** magenta and violet night tones.
- **At thumbnail:** what reads is the purple hair silhouette on pink.
- **Lesson:** the bartender is the face, and the genre lives in the logo's subtitle line, as "COCKTAIL BAR SIMULATOR" does for us.

**Coffee Talk** (914800) and **Episode 2: Hibiscus & Butterfly** (1663220)
- **Layout:** the camera stands where the barista stands. Customers sit at the counter facing the viewer and the barista is never shown. The logo, with a steaming-cup mark, sits above the cast. [mem-lo]
- **Colour:** rainy-night brown and purple, in PC-98 chunky pixel illustration. [src for palette and style]
- **Episode 2:** keeps the same frame and logo lockup and shifts to a warmer hibiscus pink, with the cover drawn by the series team. [src] rpgfan
- **Lesson:** the counter is the stage. A sequel or series keeps the frame and changes only palette and cast.

**The Red Strings Club** (589780)
- **Style:** painted key art (Pablo Gómez) of one person lighting another's cigarette. [src]
- **Colour:** red and magenta.
- **Lesson:** one intimate gesture across the bar, between bartender and guest, signals the genre better than a crowd does. For us that gesture is handing over a drink or checking an ID.

**Tavern Talk** (2076140)
- **Style:** a visual-novel illustration of the adventurer cast gathered at the counter in warm amber and violet, with a mug glyph in the logo. [mem-lo]
- **Weakness:** a group shot weakens at 120 px.

**Travellers Rest** (1139980)
- **Style:** true pixel art: a warm amber tavern against a cool exterior, with the logo as a wooden signboard. [mem-lo]
- **Lesson:** warm interior light against a cool outside is the whole colour story.

**Coffee Talk Tokyo** (3161220, 2025)
- **Style:** the same series system with a new city palette. [mem-lo]

### Management and cozy sims

**Papers, Please** (239030)
- **Layout:** led by type. A huge condensed title, an Arstotzkan emblem, and a three-colour red, grey and black palette on a flat field. Little or no face. [mem-hi on the type-led style; mem-lo on the details]
- **At thumbnail:** it reads at 120 px because two words in block type sit on a flat field.
- **Lesson:** a flat field behind the logo plus a hard-limited palette wins at thumbnail size.

**Dave the Diver** (1868140)
- **Layout:** Dave front and centre, round and comic, facing out. A bold yellow-on-dark logo at the top over saturated aqua water. [mem-lo]
- **Style:** illustrated 2D/3D, not pixel.
- **Lesson:** a funny, unmistakable silhouette on a one-hue field.

**Stardew Valley** (413150)
- **Layout:** the pixel-lettered gold logo takes the middle of H at roughly 60–70% of the width, on a calm blue sky, with the pixel valley below. Characters are tiny or absent. [mem-hi; logo described in src]
- **Library capsule:** logo stacked on top, scene below. [mem-lo]
- **Lesson:** a pixel-art logo can *be* the key art. The sky behind it stays calm.

**Moonlighter** (606150)
- **Layout:** Will, the shop and a moon logo glyph, with the day-shop / night-dungeon duality in one image. [mem-lo]
- **Lesson:** show both halves of the loop. For us that is the door (ID) and the counter (pour).

**Potion Craft** (1210320)
- **Style:** medieval-manuscript illustration on parchment, with a calligraphic logo. [mem-hi on style]
- **Lesson:** one strict art language across every asset makes the game recognisable at any size, and the calm parchment gives the logo its contrast.

**Unpacking** (1135690)
- **Layout:** no character. A cardboard box, the objects, and a friendly lowercase logo in a pastel isometric pixel style. [mem-med]
- **Lesson:** the mechanic's object can be the hero. For us that is the ID card plus the coupe.

**Spiritfarer** (972660)
- **Layout:** hand-drawn Stella (and the cat) small against a large teal sunset sky, with an elegant logo set in open sky. [mem-lo]
- **Lesson:** a soft sky gradient makes the best field for a logo.

### Neon / Miami / action

**Hotline Miami** (219150)
- **Style:** an El Huervo illustration, not pixel art. [src]
- **Layout:** Jacket in the white rooster mask, holding a weapon, on a hot pink field. The logo, in 80s neon script, sits **bottom-right**. [src]
- **At thumbnail:** "a white mask on pink".
- **Lesson:** one hot field plus one bright iconic shape plus a script logo. That is the Miami formula in three parts.

**Hotline Miami 2** (274170)
- **Layout:** the same brand system (illustration, flat hot colours, script logo) with a new masked cast. [mem-lo]
- **Lesson:** a franchise keeps its system intact.

**Katana ZERO** (460950)
- **Style:** a painted cover by godsavant. [src]
- **Layout:** a single figure with a katana under magenta and teal neon lighting, with a bold logo. [mem-med]
- **Lesson:** light one figure with a magenta key light and a cyan rim, which is our exact palette pair.

**Balatro** (2379780)
- **Layout:** a pixel logo plus one object, the Joker card, over a red and blue swirling shader backdrop. [src for the swirl and the red/blue scheme]
- **Lesson:** one object and a signature background pattern that nobody else owns. Our equivalent is Bayer-dithered sunset bands.

### Pixel RPGs

**Eastward** (977880)
- **Layout:** true pixel art of John and Sam in a deep green town, logo at the top. [mem-lo]
- **Lesson:** a pixel capsule can have depth if the logo sits on sky.

**Sea of Stars** (1244090)
- **Style:** an illustration by Bryce Kho, not pixel. [src]
- **Layout:** Valere and Zale against a huge blue moon, with an ornate logo. [src]
- **Lesson:** a celestial disc behind the heroes acts as a halo. For us, the sunset sun behind Roxy's head does the same job.

**Cult of the Lamb** (1313140)
- **Layout:** a symmetric, centred mascot facing the viewer with eye contact. One accent colour (the red crown) on dark, and a large logo. [mem-med]
- **Library capsule:** the mascot stacked under the logo. [mem-lo]
- **Lesson:** a central mascot with eye contact plus one accent colour.

### Patterns across the set

1. Most pixel games with top capsules use **non-pixel illustration** (VA-11, HM, Katana ZERO, Sea of Stars, Dave, Red Strings). The ones that stay pixel (Stardew, Papers Please, Balatro, Eastward, Travellers Rest) make up for lower detail in four ways:
   - a bigger logo,
   - flat colour fields,
   - one object or face,
   - hard outlines.
2. The field behind the logo is always calm: sky, a flat colour, or parchment. Detail lives on the character's side.
3. The neon titles (HM, Katana ZERO, Balatro) use **two complementary hues, magenta/red against cyan/blue**, with one bright white or cream shape.
4. Portrait formats are **recomposed**, not cropped: the logo is stacked above or below a portrait-framed hero. [mem-med]
5. Heroes keep the outer areas calm for the library logo and put the face near the centre.

## 3. Composition rules for MALIBU CLUB (20)

The logo is 2.64:1. Its area share converts to width like this: on the 920×430 header, 15–25% of the area means a logo **43–56% of the width**.

1. **Build one master, many layouts.**
   - Build one layered master scene at a native **960×540 art-px** canvas (×4 = 3840×2160). The layers are:
     - sky and sun,
     - Ocean Drive Deco skyline and palms,
     - sea,
     - beach-club counter and back bar,
     - Roxy,
     - props,
     - logo.
   - Every asset re-arranges these layers. None is a crop of a flattened image.
   - V and L are recomposed, and S gets its own logo-first layout.
2. **Use one pixel grid.**
   - Every asset is a native canvas ×4 with nearest-neighbour scaling, cropped to the exact size. Never resample.
   - At Steam's 1× display size that gives the same apparent pixel everywhere: 2 screen px.
   - No mixels: room, Roxy, props and the logo's tube core all sit on the same ×4 grid inside an asset.

   | Asset | Native canvas | Result |
   |---|---|---|
   | Header | 230×108 | crop to 920×430 |
   | Main | 308×177 | crop to 1232×706 |
   | Small | 116×44 | crop to 462×174 |
   | Vertical | 187×224 | 748×896 exact |
   | Library capsule | 150×225 | 600×900 exact |
   | Hero | 960×310 | 3840×1240 exact |
   | Page background | 360×203 | crop to 1438×810 |
   | Event cover | 200×113 | crop to 800×450 |
   | Event header | 480×156 | crop to 1920×622 |
   | Library logo | 320×121 | 1280×484 |
   | Community icon | 46×46 | 184×184 |
   | Client icon | 64×64 | 256×256 |

3. **Logo slots** (logo box measured without its halo; the halo may overlap the art but never the letters):

   | Asset | Logo width | Position |
   |---|---|---|
   | Header | 46–52% (≈440–480 px) | Left half; box ≈ x 40–500, y 56–230 |
   | Main | 34–40% (≈420–490 px) | Top-left, 5% margins |
   | Small | 80–86% (≈380–400 px) | Centred or left at 4% margin |
   | Vertical | 82–86% (≈610–640 px) | Top, 5–6% top margin, bottom edge at about 32% of height |
   | Library capsule | 83–86% (≈500–516 px) | Top, 5% margin, bottom edge at about 27% |

4. **Roxy slots:**

   | Asset | Placement | Eyes from top | Face width |
   |---|---|---|---|
   | Header | Right half, face centre x ≈ 74–78%, crown at 6–10%, cut at the waist by the counter | 30–36% | ≈10–12% of width |
   | Main | Face centre x ≈ 62–68% (on the right third line), crown to counter ≈ 85–95% of height | ≈30% | — |
   | Vertical | Waist-up under the logo, crown at ≈33% | 43–46% | — |
   | Library capsule | Waist-up under the logo, crown at ≈28% | 37–40% | ≥ 30 px at the 300×450 test size |

5. **Gaze:** eyes to camera, giving eye contact. Body turned three-quarters toward the logo. One hand presents the cocktail or the ID card toward the centre. She never looks out of frame.
6. **Keep a calm field behind the logo.**
   - The logo box plus 6% padding sits on the darkest band of the sky: Night[0–2] or ClubBlue[0–1].
   - The field varies by no more than 2 adjacent ramp steps and has no detail finer than 8 art px.
   - No sun, palm, sign or face crosses that box.
7. **The sun sits behind Roxy, never behind the logo.**
   - A low banded sun disc in Amber[4]/Cream backlights her head and shoulders, the way Sea of Stars uses its moon and Cult of the Lamb its halo.
   - Her dark-red hair then reads as a dark silhouette on a bright disc.
   - Landscape horizon at 52–60% of the height.
8. **Colour split:** about 60% night and club blue (top, and the logo side), about 30% magenta-amber sunset (behind and below Roxy), and at most about 10% cyan. Cyan appears only on Roxy's shadow-side rim light, the logo's subtitle and coupe, and one prop highlight. That is the HM / Katana ZERO two-tone scheme in our ramps.
9. **Three brightest spots only:** the logo's cream core, the sun, and Roxy's face plus the drink highlight. Downscale to 120 px wide; the three brightest blobs must be exactly those.
10. **Genre in at most 3 props:** the counter edge, one cocktail (a different glass from the logo's coupe, such as a hurricane or highball), and the ID card (the hook). Back-bar bottles appear only as glowing silhouettes. A synthwave grid or chrome must never be the main read, or it looks like a music video.
11. **Silhouette:**
    - A 1-art-px dark outline (4 screen px) on Roxy and the props.
    - Hair, earrings and the halter shape must read as a distinct shape at 120 px.
    - Test sizes: 120×45, 184×69, 231×87, 460×215, 300×450, and V at 25%.
12. **Small capsule is logo first.**
    - The logo nearly fills it, per Steam's guidance.
    - Background: sunset band, a palm or Deco silhouette at the extreme edges, the sun clipped at an edge rather than behind the letters.
    - Roxy is optional, as a head peek in the right 14%, and only if her face can be at least 60 px tall. Otherwise leave her out.
    - Check that the "MALIBU CLUB" script stays legible at 120×45. The subtitle line is allowed to fail at that size.
13. **Header and library header** are the same art and may be the same file; Steam asks for "similar artwork". Use text-left / character-right and keep the counter across the bottom 18–22%.
14. **Main capsule** shows the most environment of any asset: the Deco skyline and sea under and left of the logo, at low contrast. The logo is smaller here because main is the art-forward slot.
15. **Vertical and library capsule:**
    - Stacked layout: logo top, then Roxy, then the counter with the cocktail and ID in the bottom 20%.
    - Keep the bottom 8% free of critical detail.
    - This is a recomposition, never a centre crop of the header.
16. **Library hero:**
    - No text. Set the logo position to bottom-left.
    - Roxy's face goes in the **right half of the safe area** (face centre ≈ x 2150–2250, y 500–640; the safe area is x 1490–2350, y 430–810).
    - The sun sits behind her.
    - Keep x < 1750, y > 560 as a calm night-sea / counter band for the floating logo.
    - Fill the full width with the panorama: Ocean Drive on the left, the club on the right.
17. **Library logo:**
    - Trim the PNG to the logo plus its halo (1280 wide, ≈ 484–485 tall). Transparent padding shifts where Steam places it. [practical; mem]
    - The halo's outer band must reach alpha 0 at least 8 px before the edge.
    - Test it over the hero's calm band and over black and white backgrounds.
18. **Page background:** no logo and no face. A sunset only in the top ≈35%, fading to Night[0] by 60% of the height, with contrast of 2 ramp steps or less. Put the silhouette interest (palms, Deco, neon) in the outer ≈220 px on each side, because the content column covers the middle.
19. **Events:**
    - Event cover: a fixed frame with Roxy in the left 35% and the right 55% kept for event text (text is allowed on events). The logo is optional because Steam shows the name and icon.
    - Event header: the same sky and counter strip, with the middle band kept calm for the title. Follow Steam's template file.
20. **Icons and theme:**
    - Icons use the logo's cyan neon coupe as the logomark on Night[0], with no letters. Design the community icon at 46 px (×4) and the client icon at 32 px first (×8 → 256).
    - For Miami / Vice: Art Deco (MiMo) façades, palms, pastel-to-neon sunset, neon script. Never Rockstar's Vice City logo, font, or GTA's panel-grid box-art layout.

## 4. Where the current kit breaks these rules

Read from `/home/user/FABLE5/Docs/STEAM_SAYFASI.md` §2 and §6.

- **Mixed pixel sizes in one image:** the room is ×2 and Roxy is ×3–7 inside the same image. That breaks rule 2.
- **Resampled logo:** the logo is BOX-downscaled, so its pixels are averaged and fall off the shared grid (rule 2).
- **Small-capsule logo too small:** it is 71% wide; Steam's "nearly fill" points to 80–86% (rule 12).

## Sources
- [Steamworks: Library Assets](https://partner.steamgames.com/doc/store/assets/libraryassets), [Store Graphical Assets](https://partner.steamgames.com/doc/store/assets/standard), [Graphical Asset Rules](https://partner.steamgames.com/doc/store/assets/rules), [Event Graphical Assets](https://partner.steamgames.com/doc/store/assets/eventassets), [Assets overview](https://partner.steamgames.com/doc/store/assets) (all read through search excerpts)
- [Game Developer: Valve bans accolades and marketing copy](https://www.gamedeveloper.com/marketing/valve-to-stop-devs-from-including-accolades-and-marketing-copy-on-steam-graphics), [PCGamesN](https://www.pcgamesn.com/steam/game-art-new-rules-valve)
- [howtomarketagame: Trends for Steam Capsule Design](https://howtomarketagame.com/2020/10/28/trends-for-steam-capsule-design/), [How one new image increased sales 20-fold](https://howtomarketagame.com/2020/04/13/how-one-new-image-increased-sales-by-20x/)
- [steampageanalyzer: data from 98 top sellers](https://www.steampageanalyzer.com/blog/steam-capsule-trends-data), [gamosy: what converts in 2026](https://gamosy.com/blog/steam-capsule-design), [strayspark: the 120-pixel problem](https://www.strayspark.studio/blog/steam-capsule-art-store-page-conversion-guide-2026), [steamcapsule.com guide](https://www.steamcapsule.com/guide), [ModDB: small capsule tips](https://www.moddb.com/tutorials/one-image-to-rule-them-all-tips-for-your-games-small-capsule-on-steam), [presskit.gg capsule guide](https://presskit.gg/field-guides/steam-capsule-art-guide)
- [Wikipedia: Hotline Miami](https://en.wikipedia.org/wiki/Hotline_Miami), [godsavant: Katana ZERO cover](https://godsavant.tumblr.com/post/183405868343/katana-zero-cover-art-i-recently-painted-this), [Nintendo Life: Sea of Stars box art](https://www.nintendolife.com/news/2023/09/poll-box-art-brawl-duel-sea-of-stars), [ArtStation: Red Strings Club key art](https://www.artstation.com/artwork/PomboB), [rpgfan: Coffee Talk Ep. 2 cover](https://www.rpgfan.com/gallery/coffee-talk-2-hibiscus-butterfly-cover-art/), [Wikipedia: Coffee Talk](https://en.wikipedia.org/wiki/Coffee_Talk_(video_game)), [Stardew logo](https://www.designyourway.net/blog/stardew-valley-logo/), [Balatro showcase](https://www.steamcardexchange.net/index.php?gamepage-appid-2379780=)