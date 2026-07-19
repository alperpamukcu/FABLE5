# Development Plan — The Pour Pivot

Working document for `Docs/GDD/21_pour_system.md`. Bigger than the emotion pivot: that one
added a layer, this one removes the spine (deck → shelf) and rewrites the input model.

Status legend: ☐ todo · ◐ in progress · ☑ done

| Phase | Scope | Status |
|---|---|---|
| 1 | Core pour model, pure C# | ☑ 31 tests |
| 2 | Round/run rewiring: shelf replaces deck | ☑ 298 tests green |
| 3 | UI: the glass readout, shelf tags, licence ID | ☑ hold-to-pour (press/hold/release + drag-off stop), stemmed goblet, garnish rack under the counter, STAFF popup |
| 4 | Economy: bottle volume, refills, brand market | ◐ volume + refills + brand market shipped (GDD 22); glassware/bar tracks pending |
| 5 | Content + balance re-measure | ☐ |
| 6 | Docs and the audit of what broke | ☐ |

---

## 0. Rulings locked before writing code

| # | Question | Ruling |
|---|---|---|
| P1 | Does the deck survive? | **No.** Cabinet, rail and Restock are deleted outright. The shelf is always available. |
| P2 | How do charges scale? | `applied = Σ charge_i × (v_i / capacity) × chargeMultiplier`, rounded once at the end — same single-rounding rule as GDD 19 D6. |
| P3 | Do recipes survive? | **Yes, as ratio bands, as a bonus.** Emotional correctness comes first; matching a recipe pays Flavor/Mult and the charge multiplier on top. Missing every recipe still pours at ×0.5. |
| P4 | What does fill level do? | Its own axis with its own target band, moving a **named second emotion**. Bonus only — never a penalty, because punishing both axes at once makes every serve a coin flip. |
| P5 | Tone | Volume comes from **all** ingredients, mixers included. The fill axis is the drink's length, never its strength. Non-negotiable; it is the only thing keeping GDD 19's guardrail intact under this mechanic. |
| P6 | Overflow | Pouring past capacity spills: no score, volume wasted. Flagged in GDD 21 §10 as possibly one punishment too many. |
| P7 | Running dry mid-pour | Not a spill. You get what was left in the bottle. |
| P8 | Round limiter | Keep "drinks a customer accepts" (inherited from Mixes). Untested under pouring — GDD 21 §10. |

---

## Phase 1 — Core pour model (pure C#, zero UnityEngine)

New, under `Assets/Scripts/Core/Pour/`:

| File | Contents |
|---|---|
| `Pour.cs` | `readonly struct Pour { string IngredientId; double Volume; }` — one contribution. |
| `GlassContents.cs` | Ordered pours + `Capacity`. `Add(ingredient, volume)`, `TotalVolume`, `FillFraction`, `RatioOf(id)`, `IsOverflowing`, `Clear()`. Owns the layering order the UI draws. |
| `GlassDefinition.cs` | Capacity and shape; upgradeable (GDD 21 §7.2). |
| `ShelfBottle.cs` | An `IngredientDefinition` + `Remaining` volume + `PourRate` + tier. Mutable — it drains. |
| `Shelf.cs` | The run's bottles. `Pour(id, requestedVolume) → actualVolume` (short-pours when nearly dry, P7), `Refill(id)`, `IsEmpty(id)`. |
| `FillPreference.cs` | `Long/Regular/Short` + target band + the `Emotion Serves`. |
| `PourResolver.cs` | Pure: `(GlassContents, RecipeMatch) → EmotionDelta`, implementing P2. Replaces the selection-based path in `EmotionResolver`. |
| `RatioRecipeMatcher.cs` | Pure: `(GlassContents, recipes) → RecipeMatch`. Ratio bands, P3. |

Changed:

| File | Change |
|---|---|
| `Recipes/RecipeDefinition.cs` | Add ratio bands (`IngredientId, MinRatio, MaxRatio`) + `MinFill`. The old `PatternRequirement` list stays for now so nothing else breaks; it stops being consulted. |
| `Emotion/CustomerRead.cs` | Add `FillPreference`. |
| `Emotion/ResonanceJudge.cs` | Judge the fill axis alongside the intent axis. |
| `Cards/IngredientCard.cs` | Charges are now **per full glass**; the value scale needs re-reading, not re-authoring. |

**Test matrix.** Ratio maths at the boundaries (a band's exact edges are inclusive); overflow
detection at exactly capacity (not a spill) and one unit past (spill); short-pour when the
bottle has less than requested; charge scaling — full glass of one thing equals its printed
charges, half a glass equals half; single-rounding preserved so exact landings stay reachable;
fill-band edges; and `ReadIntegrityTests` extended — the fill preference is visible, but the
*second emotion's value* must obey the same tier rules as everything else.

**Gate:** all existing non-deck tests still green; deck/rail tests deleted with their systems.

### Phase 1 findings

- **Fill and ratio are different numbers and the UI must say so.** Pouring 0.7 gin then 0.2
  vermouth gives a 90%-full glass whose gin *ratio* is 77.8%, not 70%. This is correct (it is
  how mixing works) but it is a trap: a player thinking "70% gin" will pour 0.7 and be wrong
  once they add anything else. The glass readout has to show live **ratios**, not only fill.
- **Ratio bands can be authored unmatchable.** Ratios always sum to 1, so gin 55–75% plus
  vermouth 10–25% admits exactly one pour and nothing else — the bands only overlap at a
  single point. Caught while writing the tests, against my own first draft of the Martini.
  `BandsMustAdmitAValidDrink` now guards every recipe converted in Phase 5.

---

## Phase 2 — Round and run rewiring

`RoundController` loses `Deck`, `_rail`, `Restock`, `FillRail`, `ResolveScoredEnhancements`
(enhancements were card-instance concepts). It gains a `Shelf`, a `GlassContents`, and:

```
BeginPour(ingredientId)  →  starts a pour
PourTick(dt)             →  moves volume, may overflow or run dry
EndPour()                →  stops
Serve()                  →  scores the glass, applies emotions, clears it
Discard()                →  bins the glass, volume wasted
```

`RunController` swaps the deck for the shelf and adds refills to the Back Room.

### The audit

Done before touching a line of the round layer. The headline is better than feared: **most
things survive by reinterpretation**, because pours are *ordered* and the glass has *contents*
— so "which card, in what position, of what type" all still have honest meanings.

**The load-bearing decision:** the glass's ingredients become the scored cards, weighted by
volume. `RecipeMatch` gains a parallel `ScoredWeights` list; `ScoringEngine` multiplies each
card's Flavor by its weight. Without this, card Flavor values, all five quality tiers and
every enhancement become dead content overnight — a huge, silent loss. With it, they all keep
working, just measured by how much went in rather than by how many cards were played.

| Concept | Card meaning | Pour meaning | Verdict |
|---|---|---|---|
| Card Flavor 1–11 | per card played | × volume poured | **survives, weighted** |
| Quality tiers (5) | per card instance | per bottle | **survives** |
| `Infused` / `Overproof` / `Frozen` | per card instance | per bottle | **survives** |
| `CardTypeIs` (16 uses) | scored card's type | poured ingredient's type | **survives** |
| `CardIndexEquals` (7) | position among scored cards | **layer index** — pours are ordered | **survives** |
| `MixContainsType` (8) | mix holds that type | glass holds that type | **survives** |
| `MixSizeEquals` (6) | N cards played | N distinct ingredients | **survives** |
| `RecipeIdIn` (6) | matched recipe | matched recipe | **survives** |
| `MixesUsedEquals` (5) | mixes spent | drinks served | **survives** |
| Packs → `IngredientCard` | joins the deck | joins the shelf | **survives** |
| `ExtraMix`, `PatronDiscount`, `RarePatronBoost`, `RerollVip` | — | unchanged | **survives** |

**Casualties.** Listed rather than quietly dropped, because some of this is good content:

| Thing | Why it cannot survive |
|---|---|
| `Enhancement.Premium` (wild card) | Wild means "counts as any type in a pattern". Ratios have no pattern to be wild in. |
| `Enhancement.Doubled` | Minted a permanent copy into the deck. There is no deck. |
| `Enhancement.Golden` | Paid per Golden card left on the rail at customer end. There is no rail. |
| Frozen's shatter roll | Destroyed the card instance. Bottles are not instances. Frozen's ×2 Mult survives; only the 1-in-4 destruction goes. |
| `ToolOp.Destroy` (Ice Pick) | Removed a card from the run permanently. Emptying a bottle you can refill is not the same effect. |
| `ToolOp.Copy` (Bar Spoon) | Duplicated a card instance. Shelf bottles are unique by id. |
| `VoucherOp.ExtraRestock` (Happy Hour) | Restock is gone. |
| `VoucherOp.ExtraRail` (Wider Rail) | The rail is gone. |
| Silver Stake's −1 Restock | Needs a new penalty; re-themed to a smaller glass. |

**Rescued rather than deleted.** Three patrons keyed on Restock would have died. Deleting
good content because a resource changed name is the wrong trade, so they re-theme onto the
pour system's own scarcity — spills and refills — keeping all 64 patrons alive:

| Patron | Was | Becomes |
|---|---|---|
| The Quiet Monk | +30 Flavor if no Restocks used | +30 Flavor if nothing was spilled this order |
| Off-Duty Cop | ×2 Mult if no Restocks used | ×2 Mult if nothing was spilled this order |
| The Gossip | +5 Flavor per Restock, permanently | +5 Flavor per bottle refilled, permanently |

That needs one new condition (`NoSpillsThisCustomer`) and one new trigger (`OnRefill`).

---

## Phase 3 — UI

The scene changes shape: the bottle rail becomes a **shelf** you pour from, and the glass
readout goes top-left (GDD 21 §3.1) with layered colour bands. Hold-to-pour on mouse down,
with a tap-to-measure fallback for accessibility (GDD 21 §10).

---

## Phase 4 — Economy

Bottle volume, refill pricing, and the three upgrade tracks (bottles / glassware / bar). The
information economy from GDD 19 §8 relocates onto the bar track.

---

### Phase 2 findings

- **Recipes derive their ratio bands from the old type pattern.** Hand-authoring fourteen
  recipes as bands is fourteen chances to write something unmatchable, so
  `RatioRecipeMatcher.DeriveBands` converts "2 Spirit + 1 Bubbly" into "Spirit ~2/3, Bubbly
  ~1/3" with a ±15% tolerance. Eight of the fourteen convert cleanly.
- **Six recipes deliberately stay unpourable.** Perfect Serve, Double Perfect, House Special,
  Layered Pour, Straight Booze and Martini are ruled by things with no proportional meaning —
  distinct types, equal Flavor values, ascending Flavor, mono-type group size, or a slot that
  accepts either of two types. They need a design pass, not a derivation.
- **A derivation bug worth remembering.** Perfect Serve and Double Perfect *do* list a single
  Spirit slot, so the first version of the derivation gave them "Spirit 85–100%" — and a glass
  of neat whisky scored as Double Perfect, the highest-ranked recipe in the game, off one pour
  of one bottle. `RecipesWhoseRuleIsNotProportional_StayUnpourable` pins it.

## Phase 5 — Balance

`RunSimulator` needs a new bot: it currently enumerates card subsets, which no longer exist.
The new bot solves for a ratio, which is a different and easier problem — it can compute the
ideal mix directly, so it will be a *stronger* bot than the card one. Expect the measured win
rate to jump for that reason alone; the comparison to pre-pivot numbers will not be
apples-to-apples and should not be presented as one.

---

## Art pipeline lessons (hi-bit pass 2)

- **`create_ui_asset` is for panels, not scenery.** Asked for a nightclub interior, it
  returned an ornate UI frame; asked for a counter, a decorated slab. Scenery needs
  `create_map_object` (≤400px) or another route entirely.
- **`include_preview` bakes a transparency checkerboard into the preview PNG.** Quantizing
  that preview with forced alpha turned the checker into literal wall pixels. Always fetch
  the real asset, never the preview, for installation.
- **"transparent background" in a prompt loses to scene-implying descriptions.** "Leaning on
  a bar counter" produced a full bar scene behind the VIP. Characters need "isolated
  character only, no background, no scenery" phrasing — the environment upgrade (bg,
  counter, VIP at 2×) is still owed and should be its own careful pass.

## Risk register

| Risk | Mitigation |
|---|---|
| This deletes more than it adds, and some of it is good | Phase 2 audit is explicit and its casualties get listed, not buried. |
| Tone: "more alcohol = happier" | P5. Volume comes from all ingredients; enforced by making mixers volumetrically dominant in the content pass. |
| Pixel precision at 640×360 | GDD 21 §10 — numeric support next to the glass if the bands do not read. |
| Hold-to-pour is an accessibility problem | Tap-to-measure fallback is required, not optional. |
| Balance numbers become incomparable | Say so plainly rather than implying continuity. |
