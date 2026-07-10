# LAST CALL — GDD Module: Art, Audio & Asset Production List

## 12. GAME FEEL, ART & AUDIO DIRECTION

### 12.1 Art
- **Style:** chunky 2D pixel-art-adjacent illustration (or clean vector "cozy noir"), heavy on warm ambers/deep purples/neon cyan-magenta accents; dark vignette; animated shader background (slow swirling smoke, like Balatro's swirl).
- **Resolution target:** native 1920×1080 UI, cards authored at 2× (e.g., 142×190 px per card at 1×).
- **Juice:** card wobble on hover, screen shake on big ×Mult, ascending pitch on chained scoring, confetti/foam burst on target break, glass-clink on customer satisfied.

### 12.2 Asset list (production checklist)
- 48 base ingredient card faces + 6 type frames + 5 quality overlays + 4 seal gems + card back per Bar theme (6).
- 60 Patron portraits (framed card format) + 20 VIP customer portraits (large, with 2 emotion variants: neutral/impressed) + ~10 regular-customer portraits.
- 15 Tool cards, 11 Recipe Book cards, 10 Voucher cards, 5 Booster pack art.
- Screens: title background, bar counter scene, back-room scene, 6 Bar-theme palette variants.
- UI kit: buttons (3 states), panels, progress bar, speech bubble, tooltips, price tags, toasts, polaroid frame.
- SFX (~40): card pick/place/flip, shaker loop, pour, ice clink, cash register, bell, neon buzz, crowd murmur loop, big-score sting set (5 escalating), UI clicks.
- Music: 4 lo-fi jazz loops (menu, gameplay calm, VIP tension variant via layered stem, shop), 1 victory sting, 1 game-over sting.
- Fonts: 1 display (title/scores), 1 readable UI font, dyslexia-friendly alternative.
  - **Chosen (2026-07-10, in `Assets/Fonts/`):** display = **Limelight** (art-deco marquee — the speakeasy sign the game is named after), UI = **Barlow** (signage-inspired grotesque, highly readable at small sizes). Both SIL OFL with Latin-Extended (Turkish ready for M5 localization); licenses committed next to the TTFs. Dyslexia-friendly alternative still open (M4 settings).
- 1 background shader (smoke swirl), 1 CRT optional shader, particle sprites (foam, sparkle, steam).

### 12.3 Audio behavior
Adaptive: gameplay track gains a tension stem during VIP orders; scoring SFX pitch rises with each card in the chain; everything ducks slightly when the final score slams.

---
