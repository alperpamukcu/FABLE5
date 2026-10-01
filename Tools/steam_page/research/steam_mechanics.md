# How Steam shows and crops each store and library image (for Malibu Club: Cocktail Bar Simulator)

**How these facts were gathered.** The egress proxy blocks partner.steamgames.com, steamcommunity.com, presskit.gg, immutable.com, framedrop.work, gamingonlinux.com, pcgamer.com and the other guide sites, so I could not open any Steamworks page directly. Every Valve fact below comes from WebSearch excerpts of the Steamworks pages. Each claim carries a tag:

- **[V]** Valve text, quoted or closely paraphrased in search excerpts of partner.steamgames.com.
- **[3P]** A third-party guide or a news report.
- **[M]** From my memory. Not verified this session.
- **[C]** My own arithmetic or inference.

Before shipping, check the items marked uncertain against the live Steamworks upload and preview screens.

---

## 0. Global mechanics that apply to every asset

| Topic | Fact | Tag |
|---|---|---|
| 2024 size doubling | In August 2024 Steam started accepting double-size capsules: header 920×430, small 462×174, main 1232×706, vertical 748×896, page background 1438×810. The old half sizes (460×215, 231×87, 616×353, 374×448) are what many places still draw at. | [V]/[3P] |
| Half-size serving | Steam makes half-size copies of the library images itself: library capsule 300×450, hero 1920×620, and a half-size logo. Third-party guides say most store spots draw the header at 460×215 and the small capsule at 184×69 and 120×45. | [V]/[3P] |
| CDN derivatives | Files Steam generates from the uploads include `header_292x136`, `capsule_231x87`, `capsule_184x69`, `capsule_sm_120`, `capsule_616x353`, `hero_capsule` (vertical at 374×448), `library_600x900`, `library_hero`, `logo`, `page_bg_raw` and `page_bg_generated_v6b`. | [M], except page_bg_* which is [3P] |
| Store page width | Since September–November 2025 the store page column is **1200 px** wide. It used to be 940. Search rows are wider and taller, so the art in them is drawn larger. | [3P] news |
| New store home | The refreshed home page went into beta on 2026-04-01 and reached everyone in June 2026 (one source gives 2026-06-04). Changes: higher-resolution art, a wider responsive layout, **a game's micro-trailer plays when the pointer is on its cover art**, and the edges of the neighbouring carousel slides now show. Users can switch off hover trailers and animated assets. | [3P] news |
| Pixel art and scaling | Browsers and the client scale these images smoothly, with no nearest-neighbour filtering. Every upload size is even. If one art pixel is drawn as 2×2 output pixels, the 50% copies stay crisp. A 4×4 art pixel also divides most sizes, but not 430, 706, 450 or 622, where a partial last row is acceptable. At the odd scales (header to 292 = 31.7%; small to 184 = 39.8%; small to 120 = 26%), nothing stays crisp. Shapes and value contrast have to carry those sizes. | [C] |
| Aspect ratios | The logo is 2.64:1 and the small capsule is 2.655:1, so the small capsule is nearly "logo plus a thin margin". The hero is 3.097:1 and the event header is 3.087:1, so one composition can serve both. Header capsule = library header = 2.14:1. Page background (1.775) and event cover (1.78) are about 16:9. Main capsule is 1.745. Vertical capsule is 0.835. Library capsule is 0.667. | [C] |

## 1. Valve content rules for base capsules

| Rule | Detail | Tag |
|---|---|---|
| What is allowed | "Content on base graphical asset capsules … is limited to **game artwork, the game name, and any official subtitle**." In force since 2022-09-01. | [V] |
| Banned on base capsules | Review scores of any kind. Award names, symbols or laurels. Discount copy such as "On Sale Now" or "Up to 90% off". Text or imagery promoting another product, sequels included. Any other text, which by my reading covers slogans, "Out Now", "Wishlist" and quotes. | [V] (the last item is my reading of "no other misc text") |
| Store-capsule guidance | "Do not include quotes or other strings of text beyond the title of your game. The game's logotype should be easily legible against the background." "Use the key art and logo … graphically-centric … sense of the game-play." "Generally this artwork should not change." | [V] |
| Artwork Overrides | Text about a major update, seasonal event, battle pass, DLC or similar is allowed only as a temporary override. It runs **at most one month** and then expires. The text must be **localized into at least every language the game supports**. | [V] |
| Library images | Library hero: **no text at all, and no logo**. Library capsule: no quotes or text beyond the title, and the logo must be legible. Library logo: the logotype only. | [V] |
| What this means here | "COCKTAIL BAR SIMULATOR" is part of the official Steam name ("Malibu Club: Cocktail Bar Simulator"), so the full logo may go on every base capsule. Taglines, "Demo", "Next Fest", "Coming Soon" and review or award marks may not. | [C] |

---

## 2. Each asset in turn

### Header capsule, 920×430

| Item | Detail | Tag |
|---|---|---|
| Where it appears | Top of the store page, above the short description and review summary. "Recommended For You". Big Picture browse. Daily Deals. More places than any other capsule. Also the "More like this" rails. | [V]/[3P] |
| Drawn at | Usually 460×215. On the old 940-px store page the right column showed it at about 324×151. On the 1200-px page it is drawn larger than that; measure it live. `header_292x136` is also generated. | [3P] / [M] / ⚠ |
| Overlays | None on the art on the store page. In Daily Deal and Special Offer tiles the price and discount block sits **below** or beside the image, not on it. | [M] ⚠ |
| Cropping | None. It is scaled uniformly. | [M] |
| Safe zone | Valve publishes none. Make the logo read at 292×136, about 32% scale. | [C] |
| Text | Base rules apply. | [V] |

### Small capsule, 462×174

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "All the lists throughout Steam": search results, top sellers, new releases, wishlists, curator lists, browse pages, the recommendation queue. | [V]/[3P] |
| Drawn at | 231×87, **184×69** and **120×45**, the last being the smallest. Since the 2025 change, search rows are taller, so the image is somewhat larger than the old 120×45 there. | [3P] / ⚠ |
| Overlays | None on the art. Name, price, discount and review sit beside it in the row. | [M] |
| Cropping | None. | [M] |
| Safe zone / design | Valve: "focused on making the logo clearly legible, even at the smallest size." At 120×45 the logo fills about 119×45. The subtitle line becomes roughly 4 px tall and **will not read**, so "MALIBU CLUB" has to carry it. Keep the background calm and low-detail behind the logo. | [V] + [C] |

### Main capsule, 1232×706

| Item | Detail | Tag |
|---|---|---|
| Where it appears | The front-page Featured & Recommended carousel. Sale spotlights. New & Trending. It only appears when Steam features the game. | [V]/[3P] |
| Drawn at | 616×353 at standard density; the full size on HiDPI and on the wider 2026 home page. | [M]/[3P] |
| Overlays | None on the art. Title, screenshots, the reason it is recommended, the review summary and the price sit in the panel beside it. **Since 2026, the micro-trailer plays in place of the art on hover.** | [3P]/[M] |
| Cropping | The full image is not cropped. In the 2026 carousel the neighbouring slides peek in at the sides, so a sliver of the capsule's edge appears in someone else's slide. | [3P] ⚠ |
| Safe zone | Valve publishes none. Keep the logo and focal point clear of the outer ~5% on the left and right, which is the edge seen in a neighbour's peek. | [C] |

### Vertical capsule, 748×896

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "Can appear at the top of the front page during seasonal sales, and on other new sale pages." Also vertical promo rows. | [V] |
| Drawn at | About 374×448 (the `hero_capsule` derivative), sometimes smaller in sale grids. | [M] |
| Overlays | Price and discount appear in a strip **below** the image. Hover opens a tooltip with screenshots and tags. There is no overlay on the art itself. | [M]/[3P] ⚠ |
| Cropping | None known. | [M] ⚠ |
| Design | Treat it like a movie poster. The logo must be legible. Keep the bottom ~8% free of essential detail in case a discount badge ever overlaps it. | [3P] + [C] |

### Page background, 1438×810 (optional)

| Item | Detail | Tag |
|---|---|---|
| Where it appears | Behind the whole store page, anchored top-centre. Without an upload, Steam builds one from a screenshot. | [V] |
| Treatment | "A template will automatically be applied to your uploaded file." Steam keeps `page_bg_raw` and serves `page_bg_generated_v6b`, a darkened, tinted copy that fades into the page colour at the edges and bottom. Since the 2025 redesign more of the game's own colour comes through, and Valve mentions support for "larger background art". | [V] / [3P] / ⚠ no new size found |
| Visible area | If it is drawn at native size, centred behind the 1200-px column, about **119 px on each side** shows (it was 249 px with the 940 column), plus the band at the top above the media block. Elsewhere it shows dimly through panels. | [C] ⚠ |
| Design | Valve: "ambient so as not to compete with the content." No focal subject, logo or text. Place colour and light at the far left and right and along the top. | [V] + [C] |

### Library capsule, 600×900

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "The primary way of presenting your game within the Steam Library": library home, the grid and collections, the desktop client, Big Picture, Steam Deck. | [V]/[3P] |
| Drawn at | 300×450 (generated) or smaller in a dense grid. | [V] |
| Overlays | **The game's name is not printed with it**, so the logo has to be in the art. Client status can sit over the bottom: a download or update progress bar, dimming when uninstalled, a hover button. | [M] ⚠ |
| Cropping | None. | [M] |
| Safe zone / design | "Graphically-centric … logo easily legible against the background." No text beyond the title. Valve gives no zone. Keep the logo out of the bottom ~10%, where the status overlays sit; the top third is usual. | [V] + [C] |

### Library hero, 3840×1240 (a 1920×620 copy is generated)

| Item | Detail | Tag |
|---|---|---|
| Where it appears | The top of the game's page in the library: desktop, Big Picture and Deck. The library logo is laid over it, the play controls sit at or over its bottom edge, and the hero and logo move at different speeds when the page scrolls. | [V]/[3P] |
| Text | "This image cannot include any text." No logo. | [V] |
| Cropping | Scaled and cropped with the client window size: the sides go on narrow windows and the top and bottom on wide ones. "Artwork should extend across the entire template." | [V] |
| Safe area | "At the center of the template is a 'safe area' of **860px x 380px**. This area will remain uncropped … a main character's face should be entirely in the safe area or risk being cropped." | [V] |
| ⚠ How to read the safe area | **Literal reading**, on the 3840×1240 master: x 1490–2350, y 430–810, which is 22.4% × 30.6%. **Reading as a 1920×620 template**, doubled on the master: 1720×760 at x 1060–2780, y 240–1000, which is 44.8% × 61.3%. The second is more plausible for how the client actually crops. The third-party guides all assume the first. **Recommendation:** put Roxy's face and the key prop (card or coupe) inside the literal box; make sure nothing important lies outside the 1720×760 box. | [C] |
| Bottom edge | The play bar covers the bottom strip, and the hero fades when the page scrolls. Keep roughly the lowest 15–20% free of essential detail. | [3P]/[M] ⚠ estimate |
| Logo zone | Whichever preset is chosen (next asset), that area of the hero must stay calm, dark and low-detail: no face, no bright sun disc, no magenta or cyan neon that would fight the logo's halo. With BottomLeft, the usual choice, keep about the left 0–40% of width and lower 35–90% of height quiet. CenterCenter would sit over the safe area; avoid it here. | [C] |

### Library logo (transparent PNG, up to 1280 wide and/or 720 tall)

| Item | Detail | Tag |
|---|---|---|
| Size limit | "Either 1280px wide and/or 720px tall." A half-size copy is generated from the logo's own aspect ratio. Transparent background, logotype only. | [V] |
| Placement tool | After upload, a Steamworks preview over the hero offers four positions: **bottom-left corner, top-centre, centre-middle, bottom-centre**. The logo can be scaled; it is stored as `pinned_position` (BottomLeft, UpperCenter, CenterCenter, BottomCenter) with `width_pct` and `height_pct` of the container. | [V] + [3P] schema |
| Default position | Probably BottomLeft. Set it explicitly. | [M] ⚠ |
| Logo file shape | At 2.64:1 the logo fits as about **1280×485**. Crop transparent padding tight, but keep the 4-band glow inside the bounds. Steam scales and anchors the whole bounding box, so empty margin shrinks the logo and moves it off its anchor. | [C] |

### Library header, 920×430

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "Different places within the Steam Client Library, including Recent Games." If none is set, the store header capsule is used. The Deck home's "Recent Games" shelf shows the selected game as a 460×215 wide banner. | [V] / [3P] |
| Design | "Focus on the branding … similar artwork to the Library Capsule or Header Capsule … logo clearly legible." No overlays known. | [V] / [M] |

### Event cover, 800×450

| Item | Detail | Tag |
|---|---|---|
| Where it appears | Recent announcements on the store page, event and announcement lists, the game's library page (What's New / activity), the community hub. | [V] |
| Overlays | "Steam will always include your game's name and icon next to the cover image." The event title is set as text beside or below it, **not on the art**. Event-specific text inside the art is allowed; base-capsule rules do not apply. | [V] / [M] |
| Design | "Concisely represents your announcement." The logo is optional. | [V] |

### Event header, 1920×622

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "On the top of your event … establish your branding and provide color for the detail views." | [V] |
| Overlays | The title and date are page text below the banner; I am not aware of anything drawn over it. | [M] ⚠ |
| Reuse | Same 3.09:1 ratio as the hero. A 1920×620 downscale of the hero plus 2 px fits, and the logo may be included here. | [C] |

### Community / app icon, 184×184 JPG

| Item | Detail | Tag |
|---|---|---|
| Where it appears | "Compact layouts where there isn't enough room for a larger capsule": the library list view, chat favourites, notifications in the client, mobile app and Deck. | [V] |
| Drawn at | About 16–32 px in most places. | [M] |
| Design | One silhouette that reads at 32 px, such as Roxy's face or the neon coupe. No wordmark. | [C] |

### Client / shortcut icon, 256×256 or 512×512 ICO or PNG

| Item | Detail | Tag |
|---|---|---|
| Where it appears | Desktop shortcuts. Steam builds the .ico itself from a PNG. Taskbar and Explorer use it at 16, 24, 32 and 48 px. | [V] / [M] |
| Design | Same mark as the community icon. If shipping an .ico, hand-tune the 16, 32 and 48 px frames on the pixel grid. | [C] |

---

## 3. Still to check in the Steamworks preview

1. Which size the hero's 860×380 safe area really refers to (§2, library hero).
2. Whether the 2025 "larger background art" brought a new page-background size, and the exact tint and fade it now gets.
3. How large the header capsule is drawn on the 1200-px store page.
4. The overlays on the vertical capsule in sale grids, and the default logo preset.
5. How the 2026 home page's hover micro-trailer and side peeks show the main capsule.

## Sources

- Steamworks (read only through search excerpts): [Store Graphical Assets](https://partner.steamgames.com/doc/store/assets/standard), [Graphical Assets – Overview](https://partner.steamgames.com/doc/store/assets), [Library Assets](https://partner.steamgames.com/doc/store/assets/libraryassets), [Graphical Asset Rules](https://partner.steamgames.com/doc/store/assets/rules), [Event Graphical Assets](https://partner.steamgames.com/doc/store/assets/eventassets), [Community and Client Icons](https://partner.steamgames.com/doc/store/assets/community), [Now Supporting Larger Store Graphical Assets](https://steamcommunity.com/groups/steamworks/announcements/detail/4354502761457447461), [New asset types for the Steam Library](https://steamcommunity.com/groups/steamworks/eventcomments/1742266164814599669/)
- Third-party guides: [presskit.gg capsule guide](https://presskit.gg/field-guides/steam-capsule-art-guide), [steampageanalyzer asset requirements](https://www.steampageanalyzer.com/blog/steam-page-asset-requirements), [immutable capsule sizes](https://www.immutable.com/guides/steam-capsule-sizes), [immutable library capsule](https://www.immutable.com/insights/steam-library-capsule), [FrameDrop](https://framedrop.work/guides/steam-image-sizes), [myopic.design asset scraper](https://myopic.design/tools/steam-asset-scraper/), [DeadForgeExternalData (logo_position schema)](https://github.com/DeadCodeGames/DeadForgeExternalData), [SteamTinkerLaunch wiki](https://github.com/sonic2kk/steamtinkerlaunch/wiki/Custom-Game-Artwork), [Planet Suj Deck artwork guide](https://www.planetsuj.com/blog/steam-deck-tips-how-to-add-a-non-steam-app-with-all-artwork-a-complete-guide)
- Rules coverage: [Game Developer](https://www.gamedeveloper.com/marketing/valve-to-stop-devs-from-including-accolades-and-marketing-copy-on-steam-graphics), [PCGamesN](https://www.pcgamesn.com/steam/game-art-new-rules-valve), [Destructoid](https://www.destructoid.com/steam-store-cutting-down-cluttered-game-artwork-new-rules-september-2022/)
- Store redesigns 2025–26: [GamingOnLinux 2025-11](https://www.gamingonlinux.com/2025/11/steams-wider-store-page-refresh-is-live-with-plans-to-improve-the-home-page-on-the-way/), [HotHardware](https://hothardware.com/news/steam-store-redesign-with-wider-pages), [TechPowerUp](https://www.techpowerup.com/342781/valve-rolls-out-redesigned-steam-store-pages-for-desktop-gamers), [Steam News: A Refreshed Steam Store Home Page](https://steamcommunity.com/games/593110/announcements/detail/704394876188361009), [PC Gamer](https://www.pcgamer.com/games/instead-of-making-a-joke-valve-celebrates-april-fools-day-by-rolling-out-a-steam-storefront-refresh-that-makes-it-look-much-nicer/), [Shane the Gamer](https://shanethegamer.com/esports-news/steam-store-homepage-refresh-beta), [tech-insider (June 2026 rollout date)](https://tech-insider.org/steam-store-redesign-2026/)

The repo already has a capsule list in `/home/user/FABLE5/Docs/STEAM_SAYFASI.md` §6. Its notes on cropping and safe zones should be updated from this research.