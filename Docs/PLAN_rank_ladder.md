# PLAN — The Ladder (rank, popularity and what each rung unlocks)

The author, 2026-09-21: *"Her yıldız seviyesi geçildiğinde yani 0.5-1-2-3-4-5 yıldızlara ulaşıldığında barın
popülerliği değişecek (örneğin: sokağın en iyisi, şehrin en iyisi, ülkenin en iyisi) ve her popülerlikte yeni
oynanış özellikleri açılmalı. 1. yıldızdan sonra müşterilerin kimliğini sorgulama, sahte kimlik tespit etme ve kovma
mekanikleri gelecek. 2. yıldızda kaşık ile karıştırma oyuna eklenecek. Yarım yıldıza ulaşıldığında buz ve limon
oyuna eklenecek. 1 yıldıza ulaşıldığında tuz ve şeker eklenecek. Zeytin ve nane 2 yıldızda. Sadece zeytin ve nane
markette kilitli olacak, diğerleri direkt ana sahneye eklenecek. Her yıldız seviyesine ulaşıldığında oyuncuyu
kutlayan ve yeni nelerin açıldığını gösteren, eski yıldız seviyesi ile şimdiki arasındaki geçişi gösteren bir ekran
açılmalı; bu ekran üst bardaki yıldız barına tıklayarak tekrar açılabilmeli."*

Same rules as every plan here: Core decides, the UI renders, content is data, nothing ships on "it compiles", and
the 200-run sim is read before a number is trusted. Status legend: ☐ todo · ◐ in progress · ☑ done.

## 0. What already exists, measured (2026-09-21)

The ladder is not a new system so much as a NAME for gates the game already has, plus three it does not:

| Thing | Where it lives today | Gate today |
|---|---|---|
| The standing (0–5, inertial) | `BarRating.Average`; `PreviousStanding` kept for "what opened last night" | — |
| Market listings | `UnlockCondition.Stars(rung)`, `Market.RequiredStars(tier, price)`, `unlockStars` in `base_bar.json` | per bottle; olives 3.0, mint 4.0 |
| Recipes | `TycoonRun.RecipeStarGate` | per rank |
| The spoon | `TycoonRun.SpoonUnlocked` → `Stir` refuses without it | "the first stirred page on the menu" (the author's 2026-09-16 rule, now superseded) |
| The door: papers, forgeries, the kick | `IdPapers.Roll` on the "papers" stream (`MinorChance(day)`: 0 on day 1, then 3%+1%/day to 12%); `TycoonRun.Kick` (Door.cs); the KICK key on the card (`TycoonHud.Id`) | by DAY, not by stars |
| Ice, lemon twist, salt rim, sugar rim | the counter rail (`TycoonHud.Seats.BuildMiniPreps`), always out; orders ask for them via `ServingSpec.GarnishPool`; `AddPreparationAtGlass` takes any | none |
| Olives, mint | the same rail, shown only when the jar is stocked (bought) | the market's star gate |
| The night's climb | the bill's stand board previews `StandingAfterTonight`; the books close in `ContinueToNextDay` (after the market) | — |
| The top bar's stars | `TycoonHud.Build` star row, fill written every frame in `Chrome.RefreshStanding`; a hover tip, no click | — |

Two facts that shape the design: **the books close AFTER the market** (so a rung reached tonight opens the market's
new stock tomorrow evening — `OpenedLastNight` already says so, by design, 2026-09-08), and **a run does not persist**
(nothing to save; the ladder's memory lives on the run).

## 1. The design

### 1.1 The rungs

Six rungs on the standing's own scale, at the author's thresholds. The rank is a **high-water mark**: the standing
can fall (a bad week drags it), the rank does not — a bar that was once the talk of the town keeps its spoon. The
title the top bar wears is the rank's, so it only ever climbs. (Decision for the author: if the title should FALL with
the standing while the unlocks stay, that is one line in `BarRank`.)

| Rung | Stars | Title (draft — the author renames) | Unlocks |
|---|---|---|---|
| 0 | 0 | NOBODY'S HEARD OF IT | the bar as it opens: pour, shake, cap, the card as the order |
| 1 | 0.5 | TALK OF THE STREET | **ice** and the **lemon twist** on the counter; orders may ask for them |
| 2 | 1.0 | THE BLOCK'S BEST | **the door** — papers behind the card, forgeries, the KICK key, the fine and the thanks; **salt** and **sugar** rims on the counter |
| 3 | 2.0 | TALK OF THE TOWN | **the bar spoon** (stirred drinks); **olives** and **mint** appear in the market |
| 4 | 3.0 | THE CITY'S BEST | (title only — the existing 3-star bottles and recipes are the content) |
| 5 | 4.0 | THE COUNTRY'S BEST | (title only — the 4-star bottles and recipes) |
| 6 | 5.0 | THE BEST THERE IS | (title only — the endgame) |

Rungs 4–6 carry no new verb because the author named none; they still get the celebration, and their "what's new"
list is read off the market's own gates (the recipes and bottles that opened at that rung), so the screen is never
empty. If the author wants a verb on them later, `BarRank.Features` is the one table to add to.

### 1.2 What "unlocked" means, per feature — Core refuses, the UI only hides

- **Ice / lemon / salt / sugar** (`Feature.IceAndLemon`, `Feature.Rims`): the rail does not build the dish; orders
  do not ask for the preparation (`ServingSpec.Roll` takes the allowed pool); `AddPreparationAtGlass` refuses one
  the rank has not opened (the rules layer never trusts the UI).
- **The door** (`Feature.Door`): before it, `IdPapers.Roll` rolls everyone honest (`MinorChance` → 0 while the door
  is shut — the person's papers are rolled ONCE, so a face first met before rung 2 stays honest for life, which is
  right: they were honest when you met them); `Kick` refuses ("the door is not yours yet"); the KICK key is not
  built. The card still opens — it is how the ORDER is read (hidden information, CLAUDE.md) — and still prints the
  age and the flag, which are honest until the door is yours.
- **The spoon** (`Feature.Spoon`): `SpoonUnlocked` is the rank, full stop; the 2026-09-16 "first stirred page"
  rule is retired (the author has moved the spoon from "the first star" to the second rung). `Stir` refuses as it
  does today; the bench draws the spoon off `SpoonUnlocked` as it does today.
- **Olives, mint**: data only — `unlockStars` 3.0/4.0 → **2.0** on both. The market's gate, the rail's "stocked"
  rule and the day-end tablet's "NEW" all read that field already.

### 1.3 The ceremony

**Where:** on the bill, the beat after the standing's climb lands (`StepStandingClimb` reaching `_standTo`), before
CONTINUE takes the player to the market — that is the moment the stars visibly cross the rung, and the market that
follows is unchanged (it opens the rung's stock tomorrow evening, and the screen says so for olives and mint). Core's
truth still flips at `CloseNight`; the bill's preview and the close are the same three lines (`StandingAfter`), so
the ceremony cannot promise a rung the books then refuse.

**What:** a window in the pause menu's family (`NightPlate`, `NightTitle`, the pack's keys in the game's palette):
the old title over a row of stars filled to the old standing, the stars climbing to the new standing, the new title
landing, then the "NEW" list — one line per unlock with its pictogram (PrefArt's ice/lemon/salt/sugar, the mark
set's `step_stir` for the spoon, the card's own art for the door, the garnish jars for olives/mint) — and a
CONTINUE key. Respects `Ceremony.Pace` and `Motion.Reduced`; skippable with the key at any time.

**Again, later:** the top bar's star row becomes a key; it opens the same window for the CURRENT rank (its title,
its stars, everything it has opened so far, and the next rung's threshold), without the climb. A rung reached and not
yet celebrated wears a small "NEW" flag on the star row until the window has been opened once.

## 2. Phases

### L0 — Core: the ladder and its gates ☑ (2026-09-21)
- `BarRank` (new, pure): the rung table, `Of(stars)`, `Feature` and `Grants(feature)`, the title `Line` per rung.
- `BarRating.BestStanding`: the high-water mark, moved in `CloseNight` and `DevSet`.
- `TycoonRun.Rank`, `Has(Feature)`, and the four gates of §1.2; `RankReachedTonight` (the rung the last close
  crossed, or none) and `MarkRankSeen()` for the ceremony's bookkeeping.
- `base_bar.json`: olives and mint at 2.0.
- Tests: `BarRankTests` (rungs, high-water, features); `TycoonRunTests` — the two spoon tests rewritten to the
  rank, a rank-seeded `MixRun`; `DoorWiringTests` seeded to rung 2; a garnish-pool test (an order at rung 0 asks
  for nothing off the rail); `BaseBarAndMarketTests` — olives and mint at 2.0.
- The sim: the "orders" stream's draws change when the garnish bag shrinks, so the 200-run numbers move; read
  the shape (draught share, head band) rather than the absolute, per CLAUDE.md.

### L1 — The room obeys ☑ (2026-09-21)
- The rail builds a dish only when its feature is open (`RefreshRail`'s `stocked` test grows a rank clause).
- The KICK key is not built before rung 2 (`ShowId`).
- Nothing else: the bench already reads `SpoonUnlocked`, the market already reads the data.

### L2 — The ceremony and the star row ☑ (2026-09-21)
- `TycoonHud.Ladder` (new partial): the window, the climb, the NEW list, the CONTINUE key.
- The trigger on the bill; the star row's key and its NEW flag.
- Loc: `Tools/loc/fragments/rank.json` — titles, the window's lines, the refusals.

### L3 — Docs and proof ☑ (2026-09-21; the 200-run sim not yet re-read)
- GDD 23 §7 gets the ladder; GDD 28 notes the door's new gate; GDD_MEVCUT 9.88; changelog.
- Both suites green; the ceremony looked at in play at every rung (the dev presets park the standing at 2.6 and
  5.0 — `DevPreset` must move `BestStanding` with it).

## 3. Open decisions (for the author)

1. The six titles are drafts in English — the Turkish table will carry the author's own words.
2. High-water mark vs. following the standing (§1.1).
3. Rungs 4–6 have no new verb; the screen lists what the market opened there. Add verbs later?
4. The market lag: olives and mint reached at rung 3 show up in the NEXT evening's market (how the market has
   always read the standing). The screen says "in tomorrow's market". If the author wants them the same evening, the
   market must read the night's preview — a separate decision with its own tests.
