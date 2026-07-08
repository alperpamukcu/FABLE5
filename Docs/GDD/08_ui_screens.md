# LAST CALL — GDD Module: Screens, Menus & UI Flow

## 10. GAME SCREENS & UI FLOW

### 10.1 Boot & Main Menu
- Studio splash (skippable) → Title screen: animated bar-interior background (parallax smoke, flickering neon sign with game logo), lo-fi jazz loop.
- Menu buttons (vertical, left-aligned): **PLAY**, **COLLECTION**, **CHALLENGES**, **SETTINGS**, **CREDITS**, **QUIT**. Continue-run banner appears if a save exists ("Resume Night 5 — The Speakeasy").
- PLAY → Run Setup screen: choose Bar (deck), choose Stake, seed input field (optional, for seeded runs), START SHIFT button.

### 10.2 Gameplay screen layout (single fixed screen, 16:9)
- **Top strip:** Patron shelf (5+ slots) — patrons rendered as framed portrait cards.
- **Left panel (fixed):** Customer portrait + speech bubble, required score, current score progress bar, Mixes remaining, Restocks remaining, Tips ($), Night number, Recipe preview panel (live-updates: shows which Recipe the current selection forms, and its Flavor × Mult).
- **Center:** the bar counter. Selected ingredients slide to the counter; on MIX, a shaker animation plays, cards flip/score one by one with rising numbers, glass fills, final score slams into the progress bar.
- **Bottom:** the Rail — 8 ingredient cards, fanned. Sort toggles: by Flavor / by Type. Buttons: **MIX** (big, warm gold) and **RESTOCK** (smaller, cool gray). Cabinet pile (deck viewer on click) bottom-right; Tool slots (2) bottom-left.
- **RECIPES button (book icon, always visible, hotkey R):** toggles the **Recipe Book overlay** — the full recipe table as **color-coded Type patterns** (e.g. ●amber ●green ●pink = Sour), each with its current Level and base Flavor × Mult. Cocktail names are secondary flavor text; the pattern icons are the primary read. Design rule: the game must be fully learnable by color-matching alone — **zero real-world cocktail knowledge is ever required** (players never declare a recipe; the engine auto-matches the best one, and the live preview shows the result before committing).
- **Hover/inspect:** every card and Patron shows a tooltip with full rules text. Right-click = zoom inspect.

### 10.3 Back Room (shop) screen
Wooden shelf backdrop; item cards on shelves with price tags; NEXT CUSTOMER button (door with neon EXIT sign); reroll = cash register bell.

### 10.4 Pause menu (ESC)
Resume / Run Info (current VIP pool, deck list, **Recipe Book with current levels**, stats) / Settings / Abandon Run (confirm) / Quit to Menu. Game auto-saves every state change; pause is safe anywhere.

### 10.5 Game Over / Victory screens
Show: Nights survived, best hand score, most-mixed recipe, money earned, defeat cause (which customer), full Patron lineup, "New discoveries" toasts, buttons: NEW RUN (same setup) / MAIN MENU. Victory adds the winning drink stamped on a polaroid.

### 10.6 Settings menu (tabs)
- **Video:** resolution, window mode (fullscreen/borderless/window), VSync, framerate cap, screen-shake toggle, CRT/scanline shader toggle, background animation intensity (Off/Low/Full), colorblind palettes (deuteranopia/protanopia/tritanopia — retints the 6 Type colors and adds distinct icons).
- **Audio:** master/music/SFX sliders, mute-on-focus-loss.
- **Gameplay:** game speed (1×/2×/4× scoring animations), auto-sort rail, confirm-before-mix toggle, show detailed math breakdown toggle, language (EN/TR at launch).
- **Accessibility:** UI scale (100–150%), dyslexia-friendly font toggle, reduced motion, hold-to-confirm timing.
- **Controls:** rebindable keys; full controller glyph support (D-pad card navigation, A select, X mix, Y restock, LB/RB sort).

---
