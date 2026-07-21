# LAST CALL — GDD Module 22: Bottles, Brands & the Market

> v4 NOTE (2026-07-22): brands/market survive and gain a pricing job — bottle tier now
> raises menu prices (23 §3). The shelf leaves the screen for the menu UI (24 §1).

> The shelf itself becomes progression. Companion to `21_pour_system.md` (which owns pouring)
> and `19/20` (which own the customer). Introduced in v3.1 together with the **hi-bit art
> pass** (§6) and the licence-style patron ID (§3).

## 1. The base bar

The 46-ingredient wall was unreadable — nobody can know that many bottles by silhouette, and
the pour system demands you know your shelf the way a bartender does. It is replaced by a
curated **base bar of 12 branded bottles** (`Assets/Data/bottles/base_bar.json`):

| Bottle | Style | Type | Identity |
|---|---|---|---|
| Astra Vodka | vodka | Spirit | lifts, calms nerves |
| Boothby Gin | gin | Spirit | settles a racing mind, at a melancholy cost |
| Coral Rum | rum | Spirit | wakes people up, warms them |
| Redline Bourbon | bourbon | Spirit | talks anger down, opens sadness |
| Notte Amaro | amaro | Bitter | meets anger head-on |
| Velvet Vermouth | vermouth | Sweet | softens heartbreak |
| House Syrup | syrup | Sweet | comfort by the spoonful |
| Fresh Lemon | lemon | Sour | cuts fatigue, jangles nerves |
| Kicker Ginger | ginger | Bubbly | sparks excitement — and tempers |
| Klara Soda | soda | Bubbly | waters everything down, on purpose |
| Mint | mint | Garnish | quietly restorative |
| Luca Olives | olive | Garnish | takes the edge off |

Design rules, enforced by `BaseBarContentTests`:
- every emotion movable both ways from the starting shelf,
- one bottle per style (so brand upgrades are unambiguous),
- every ingredient type present (so the derived recipe bands are all reachable),
- mixers and garnishes at 0% ABV — the fill/length axis must be reachable without alcohol
  (tone guardrail, GDD 19).

**The old Flavor numbers are off the bottles.** They still feed volume-weighted scoring, but
the at-a-glance information a player needs is *which bottle is which* — the brand name sits
under each bottle like a shelf tag. Flavor moves into the bottle-info popup (§5).

**Style identity is explicit (2026-07-20).** Brand names alone ("Astra") did not say what a
bottle *was*, so two rules now hold everywhere:
- **The display name carries the style word** — "Astra Vodka", "Boothby Gin", "Coral Rum".
  Data rule in `base_bar.json`: the style word is the last word of `name`.
- **Every style owns a signature colour** (`UITheme.StyleColor`): vodka ice-blue, gin green,
  rum coral, bourbon amber, amaro deep blue, vermouth magenta, and so on. The shelf tag, the
  live ratio list, and the liquid poured into the glass all wear it, so "what is vodka"
  is answerable by colour alone before any text is read. Unmapped styles fall back to the
  ingredient-type ramp.

**The stage is seen from the customer's side (2026-07-20).** The bottles stand on two
back-bar wall shelves — spirits on the upper plank, mixers on the lower — behind the
counter, the way a patron sees a bar. Shelf tags are two lines (brand / style word). The
scene layout constants live in `DiegeticStage`; GDD 18's "bottles on the counter, camera
faces the club" note is superseded by this.

## 2. Identity papers (`IngredientInfo`)

Every bottle carries: **style** (what it is a brand of — the market key), **tier** (brand
rung), **price** (for market brands), **origin**, **ABV** and a one-line **blurb**. Origin,
ABV and blurb are display/dialogue material and must never feed scoring — ABV especially,
per the tone guardrail. A bottle-info popup (mirroring the customer ID) reads from here; the
future dialogue system will too.

## 3. The patron licence

The customer ID is now a full licence prop: light card stock, `CITY OF NEW ARDEN — PATRON ID`
header, photo, **NAME / AGE / FROM** fields, a demeanor line styled like a restriction code,
and the six readings as a 3×2 grid of record fields. Age (21–67) and hometown roll from the
`"customer"` stream, per archetype hometown pools — deterministic, and dialogue-ready.

**The happiness gauge**: a bar that fills and turns from red to green as the visit's *earned*
satisfaction grows — once above the customer's head on the stage, once on the licence. It is
driven by earned satisfaction, never the hidden stats; a gauge computed from the truth would
leak exactly what the tier system hides.

## 4. The market

Opens **when the night closes** (in the Back Room after the VIP) — a bar takes deliveries at
closing time, not between customers. It sells **brand upgrades**: better bottles for styles
you already stock. Buying one replaces the shelf bottle in place (muscle memory: the vodka
lives where the vodka lived), and the new brand arrives full. Deterministic and rng-free —
what is on offer is exactly "every catalogue brand strictly better than what you stock".

Launch catalogue: Vor (vodka T2), Juniper Crown (gin T2), Old Harrow (bourbon T2). This is
the third leg of the economy: Back Room = power and knowledge, market = stock.

## 5. Infrastructure laid for later

- **Preparations** (`Preparation.cs`): shaken/stirred (one slot, later choice wins), ice,
  lemon twist, salt rim, sugar rim. Recorded on the glass, cleared with it, **no gameplay
  effect yet** — the plumbing exists so the shaker minigame and rim garnishes are a
  data-and-balance pass, not a systems change.
- **Bottle info popup**: `IngredientInfo` is complete; the popup UI is pending.
- **Dialogue**: name, age, hometown, archetype and demeanor all live on `RegularState`.

## 6. Art: the hi-bit pass (art bible v2.5)

"Too pixel" fixed by **doubling texel density, not resolution**: the scene layout stays at
640×360, but foreground assets are authored at 2× (bottles 72×104 into 32-wide slots, the
pour glass 128×176) so the same objects carry finer pixels. The palette-quantize/binary-alpha
pipeline is unchanged, keeping the new art inside the v2 40. The pour glass renders top-left
at deliberately dominant size, fills with per-type colour bands as you pour, and shows the
fill % inside the glass with live per-ingredient ratios beside it.

Backgrounds, the counter and the customer sprite are still v2-density; they get the same
treatment in a later pass.
