# PLAN — the recipes, their characters, and what a bad ending costs

**Status:** R0–R4 and D1 shipped 2026-09-22. Written from the author's ninth list, in three parts:

> *"Alkol tariflerini incele ve yeni yaptığımız güncellemelere göre isimlerini/tarzlarını/
> bardaklarını/içeriklerini düzenle ve değiştir. Bununla beraber menü sayfalarını profesyonel bir
> artist gibi tekrardan tasarla, görsel, icon ve göze çarpıcı metinler kullanılsın."*
>
> *"Tariflere/kokteyllere buff/nerf özellikler eklenmeli ve bu dengeli olmalı, çeşitlilik çok ve
> etkisi az ile orta seviye arasında olmalı ... o kokteyller hiçbir özelliği olmayanlardan bir tık
> daha uygun fiyatlı olabilir."*
>
> *"Müşteri kovmak hem para cezası hem de puan cezası olmamalı. Eğer siparişini yetiştiremediysen
> gün sonu faturasına ceza gelmeli ama puanı daha az düşürmeli. Kovmak puanı daha çok düşürmeli
> ama para kaybetmemelisin. Bu sahte kimlikle kovulması gereken müşteriler için geçerli değil, o
> hem + puan sağlamalı hem de + para getirmeli."*

The ECONOMY pass the author queued behind this (per-star price bands, a daily earnings ceiling,
rent) is **not** in here. Where a line disagrees with `Docs/GDD_MEVCUT.md` after a phase ships,
GDD_MEVCUT wins.

---

## R1 — the recipe review (shipped)

Nothing about an ID moved, and nothing ever should: `RatioRecipeMatcher.PerfectPour` hashes
`recipe.Id`, so a renamed id is a different hidden perfect pour and a different drink to learn.

### Glasses

| page | was | is | why |
|---|---|---|---|
| kamikaze | rocks | martini | shaken equal thirds with nothing long in it is served up; every sibling of that shape already was |
| margarita | coupe | rocks | a salt crust reads along a wide mouth and vanishes on a stem |
| mai_tai | highball | rocks | a double old-fashioned over crushed ice, and the book's only five-band drink in the 1.0 glass |
| matador | coupe | highball | half the glass is pineapple juice; it is a long drink |
| mint_julep | rocks | highball | a julep is tall and packed, and the sprig has to stand in something |
| vesper | martini | coupe | Fleming's "deep champagne goblet", which the page's own lore quotes |

Confirmed and deliberately NOT changed: the sours in rocks, the gimlet and manhattan in coupe, the
negroni family in rocks, the martinis in martini, the draught in the pint.

### Signature extras (`RecipeDefinition.Garnish`)

Four added: **margarita → salt_rim**, **lemon_drop → sugar_rim**, **dry_martini → lemon_twist**,
**martinez → lemon_twist**. The first two were promised by their lore lines and never happened.
The second two close a **real matcher hole**: the rule at `RatioRecipe.cs:174` lets a SIGNED page
beat a plain one whatever its rank, so a perfect Dry Martini (rank 22, $14) served to somebody who
had asked for an olive as an ordinary extra was graded and paid as an Olive Martini (rank 14, $10).
Martinez satisfied both as well, its curaçao being under the 15% unnamed allowance. Signed, each
page can only ever be itself.

The rung check: twists open at 0.5★, rims at 1.0★; the four pages gate at 3.0★ and 4.0★, so none
is ever unorderable. **Deliberately not given signatures:** old_fashioned, cosmopolitan, negroni —
each signature narrows the order pool and makes the page match nothing when the extra is missed,
and four at once is enough to measure.

### Bands

- **long_island**: the five spirits and the curaçao 0.08–0.18 → 0.06–0.12, lemon → 0.07–0.15,
  cola 0.15–0.30 → **0.34–0.52**. It poured 78% neat spirit into a full highball, which is not the
  drink and is not drinkable.
- **rum_punch**: rum 0.35–0.55 → 0.25–0.40, orange 0.20–0.40 → 0.30–0.50 — "one of sour, two of
  sweet, three of strong, four of weak", which is the page's own lore line.

### Names

`dark_stormy` → **Black Squall** (Dark 'n' Stormy is a Gosling's trademark and the lore line
joked about the lawyers); `vodka_bull` → **Night Shift**; `sea_breeze` → **Bay Breeze** (the data
pours pineapple, which IS a Bay Breeze); `sex_on_beach` → **Madras** (no peach on the shelf;
vodka + cranberry + orange is a Madras, and it makes a family with the Cape Codder and the Bay
Breeze); `paloma` → **Ranch Water** (no grapefruit soda); `sidecar` → **Whiskey Sidecar**;
`dirty_martini` → **Olive Martini** (there is no brine, so the bar cannot make a dirty one);
`long_island` → **Long Island Iced Tea**; and one ampersand rule for the spirit-and-mixer
highballs: **Vodka & Soda**, **Vodka & Tonic**, **Whiskey & Ginger**.

Twenty-one lore lines were rewritten with them, `Tools/loc/data_keys.py` + `merge_fragments.py`
regenerated `en.json`, and the 28 other tables were re-translated through
`Tools/loc/translations/<code>/data_recipe_renames.json`.

### Known, and left for the economy pass

- **black_russian is Stirred, opens at 1.0★, and the bar spoon opens at 2.0★** (`BarRank`), so the
  page loses 40% of its craft score for a whole star band. Three fixes are all defensible (move
  the page, move the rung, or make it Built) and all of them move ranks — which the economy pass
  owns. `RecipeCatalog`'s comment at that entry is stale and says the page brings the spoon.
- **mint_julep opens on the rung that opens the mint jar, but the jar must also be bought**, so
  the page can be owned and never ordered until it is.
- **No star band carries all three difficulties**: 1★ is four Easy pages and nothing else, 3★ has
  no Easy at all. The author's per-star easy/medium/hard price bands need the ranks re-cut first.
- The measured difficulty split is **22 Easy / 23 Medium / 9 Hard**; `RecipeDifficulty`'s own
  doc-comment still claims 20/22/12.

---

## R2 — the drink icon (shipped)

`DrinkIcon.GarnishFor` read the recipe's BANDS, and the 2026-09-21 ruling deleted every mint and
olive band — so the two pages whose whole identity is the garnish drew none at all, and the Dry
Martini and the Olive Martini were byte-identical pictures on every menu surface. It reads
`recipe.Garnish` first now, and the mark has four shapes instead of one three-pixel blob: a wedge,
an olive on a pick, a sprig, and a crust run along the mouth.

---

## R3 — the characters (shipped)

**19 traits over 41 pages; 13 pages carry none on purpose.** A book where every page is special
has no special pages.

`Assets/Scripts/Core/Recipes/DrinkTraits.cs` is the catalogue — pure Core, every field a SCALE or
a COUNT on a house constant that already exists at the call site, and `DrinkTrait.None` is every
identity, so a page without a character computes bit-for-bit what it computed before the file
existed. The ASSIGNMENT is content (`recipes.json` ↔ `RecipeCatalog`, under the parity test);
`RecipeDefinition.Trait` carries only the string and `DataLoader` refuses a name nobody wrote.

**One character moves exactly one number** — pinned by `DrinkTraitTests`, so nobody can quietly
turn the set into nineteen flavours of "+10% tip".

| lever | buff | nerf |
|---|---|---|
| the clock | nursed (+40% of the asking box), same_again (+19% refill), keeps_well (−20% lateness), still_good_late (+20% off-clock tip) | drink_it_hot (+20% lateness), dies_warm (−20% off-clock tip) |
| another round | knocked_back (2 sip cycles), never_just_one (22% extra round) | holds_the_stool (4 cycles), one_is_plenty (−20% of its rounds) |
| the pour | made_by_feel (pay floor ×2.2), easy_to_learn (+20% perfect window), reads_full (−20% refusal line) | no_place_to_hide (pay floor ×0.4) |
| the counter | leaves_no_ring (−19% marks) | wrecks_the_bar (+21% marks) |
| the room | the_room_looks_up (+0.04 satisfaction to every other serve for 20 s) | — |
| the till | they_tip_for_this (+20% tip ceiling) | pays_the_bill (−13% tip ceiling) |

**Price.** A character is worth **one notch off the sheet** — 7%, rounded, which is exactly $1
across the whole live range — and only from a sheet of $8 up, so the opening menu is priced
exactly as it was. It is the LAST step of `DrinkOrder.MenuPrice`, which is the one function whose
only argument is the recipe: one edit, six consistent readers, and the book prints the discounted
figure with no UI change. The page also prints the undiscounted sheet beside it.

**Determinism.** One new named stream, **"round"**, drawn UNCONDITIONALLY on every resolved serve
whatever the page carries — so which pages carry `never_just_one` is content, and re-tuning it
cannot reseed anybody's night. `TycoonRun.RoomAura` is the only new per-night state and is zeroed
at BOTH reset points.

**Killed on purpose:** anything touching `ServingSpec.Roll`'s plain gate (it changes how many
draws an order consumes and would re-baseline every seed); a nerf on the refusal line (a step, not
a curve — one page could turn a good night into a hole); a nerf on the perfect window (nobody can
feel a window they have not hit); and anything weighting `DrinkOrder.Roll` (the biggest
distribution lever in the economy, and this set has to be measured against it standing still).

**Not yet measured.** `DrinkTraits.Enabled` is the A/B gate for `LastCall → Simulate Tycoon 200
Runs`: the same 200 seeds, twice, is the only honest read. The bot never shops and no run has
reached 2.0★, so it meets five of the nineteen — a second batch with the menu force-unlocked is
needed or the report will say nothing and look like it said something.

---

## R4 — the menu page (shipped)

`FillRecipePage`'s top half was three stacked centred lines and then a picture: a page with no
subject (GDD 16 §6.3). It is now

1. **the plate** — the drink on a drawn plinth at exactly 2× its own 32 px (it was 2.375×), the
   price beside it as the biggest figure on the page, with the author's **coin** instead of a
   typed dollar, and the undiscounted sheet under it when a character has moved it;
2. **three chips** — the way, the glass and the work, each a drawn mark against one fact, in a
   third of the height the three sentences took. The glass comes back as a picture and a word;
3. **the character strip** — the lever's mark, the name in the heavy face, the lever's word, and
   the host's line, washed in the sign's own ink (Lime for a buff, ViceRed for a nerf);
4. the legend at the tag size, then the pours as before.

**The page counts its pours before it draws anything.** Seventeen dated bugs in that file are one
bug — a cursor growing down meeting a foot that is pinned — so a page with six or more pours makes
its character strip give back the host's line and its legend give back the caption, and the row
pitch floor drops from 32 to 30. The seven-pour page (Long Island Iced Tea) fits with two units to
spare, arithmetically; **it still wants a look in play.**

Also in this pass: the difficulty pips are `ChromeArt.Bulb` — a struck disc with a rim — instead
of bare squares of colour (§6.8, "a dot standing in for an object"); four new 16×16 marks (clock,
round, room, counter); the hardcoded `"20% 40% …"` scale became a key; `GarnishWord` reads the
`prep.<id>.name` that already exists in all 29 tables instead of printing Core's raw English; and
the braceless `if (r.Id != "draught")` in `RecipeSpecRows` is braced.

**NOT done, on purpose:** the market's aisle tile (`TycoonHud.DayEnd.cs`) and its hover card are a
fourth and fifth renderer over the same data and still speak the old visual language. The hover
card sits inside the blessed `basket.png` look-test crop, so it wants a deliberate re-bless and
two suite runs. `TycoonHud.Market.cs` was being edited by the other session throughout this pass.

---

## D1 — the three bad endings (shipped)

Before this, a storm-off and a wrong kick were the same event to the books: both filed a flat zero
and neither cost a penny. They are told apart now by WHICH currency each is paid in.

| ending | money | standing |
|---|---|---|
| the drink never came (`StormedOff`) | **a line on the night's bill**: `TycoonConfig.WalkOutPenalty` = $8 sheet, scaled by `StarEconomy.PriceAt`, counted as they get up and charged once with the rent | `CustomerVisit.StormOffSatisfaction` = **0.10**, up from 0 |
| a wrong kick | **nothing** | 0, and it weighs `WrongKickWeight` = **2 seats** in the night's mean |
| a right kick | **+ the state's thanks**, now `StarEconomy.PriceAt(KickBonus, stars)` instead of a frozen $5 | **+1.0, filed** — it used to file nothing at all |

0.10 is not a taste call: it has to stay under `BarRating.BrokeStars / MaxStars` (0.125) or a
night where nobody was served stops drawing tomorrow's broke crowd, and under `Declined()`'s 0.15
or an honest "we cannot make that" becomes worse than letting them walk. **0.25 breaks both.**

A wrong kick is already at the floor of what one review can say, so the only Core-pure way to make
the door cost MORE than a slow drink is a weight, and `BarDay.AverageSatisfaction` is a weighted
mean now.

**This reverses GDD 28 D10** ("no review, no seat in the mean"), which is quoted in a code comment
and in a test message. What D10 was really protecting — that a right kick is neither SERVED nor
WALKED on the slip — is done where the slip is written, off `OffTheBooks`, which they still carry.
**GDD 28 §4 and §7 and GDD 23 §6's "only rent can push the till below zero" have not been rewritten
yet.**

**Charged at closing, not in `SettleDepartures`.** The author asked for a line on the *gün sonu
faturası*, and a hand in the till mid-service is a different thing — it would also dip the till
under the bin fee's own clamp. The counter rides `SettleTab`'s idempotency; the money moves once.

---

## What is left

1. **The economy pass** — per-star price bands with easy/medium/hard inside each, a daily earnings
   ceiling (day 1 ≈ $60 average, $80 best), rent that does not outrun the player, and the rank
   re-cut every one of those needs. Start from the measured numbers in
   `Docs/tycoon_sim_report.md` (200/200 bankrupt, median day 21, $102.3/day income against
   $113.5/day expenses, 9.6 customers a night, $5.46 base + $4.31 tip a serve) and from the
   per-star sheet ranges 0★ $4–7, 1★ $8–9, 2★ $9–10, 3★ $11–14, 4★ $14–18, 5★ $18.
2. **The sim** — per-trait rows keyed on `visit.Served?.Trait`, a walk-out-fees row, thanks by
   star, and the `FinishedCounted` meaning change (right kicks are in the list now, so
   `CustomersFinished` and "minors met of seats" both need the subtraction).
3. **The rulebook** — GDD 28 §4/§7, GDD 23 §6, GDD 21's recipe tables, GDD_MEVCUT, and
   `Docs/BALANCE.md`, which was already wrong in five measured ways before any of this.
4. **In play** — nothing in this pass has been seen running. The EditMode suite is green offline
   (454/454 through a scratch NUnitLite runner, because the editor belonged to the other session);
   the PlayMode suite and a look at the book page have not been run.

---

## R5 — how a drink is usually taken (shipped 2026-09-23)

> *"Bazı kokteyllerde bazı garnishler şarttır, örneğin gin fizz'de şeker gerdanlık — yani gin fizz
> söyleyen biri yüksek ihtimalle şeker gerdanlıklı söylemeli. Bunun aynısı diğer garnishler için de
> geçerli."*

`RecipeDefinition.Likes` — preparation ids a page is nearly always dressed with. **Not the same
thing as `Garnish`, and the difference is why both exist:** a signature is what MAKES the page (the
matcher demands it, every order asks for it, and a bar that cannot give it does not take the
order); a habit is only what the customer is very likely to want. A drink without its habit is
still that drink.

- `ServingSpec.UsualChance = 75`. Three in four and not four in four on purpose — a habit that
  never failed would be a second signature.
- **When the habit fires it IS the ask:** the random extras stand down, so the fussiest pages are
  not the ones with the most character. A page is never asked for more than its signature plus its
  own dressing.
- **The rail still has the last word.** At no stars nothing on it is open, so the opening menu is
  asked for exactly as plainly as before; at the first rung the ice comes and the rims do not.
- A pint takes none of it (GDD 21 §10) and neither does the neat pour.

**42 of 54 pages have one; 12 take the drink as it comes.** 36 take ice (every long build and
every rocks drink — nothing served UP takes ice), 13 a lemon twist (the four sours, the stirred
bitter family, the four that go up bare), 3 a sugared rim (Gin Fizz — the author's own example —
the Sidecar and the Pink Lady) and 1 a salted rim (Ranch Water). The table is `RecipeCatalog.Habits`
and it is read down as a list, because a habit is only right in company.

**The determinism, which is the part that would have broken quietly.** Both rolls are taken for
EVERY order whatever page it is, and so is everything after them — a stream whose draw COUNT
depends on the recipe would mean that moving "ice" from one drink to another silently reseeds every
later customer of the night. The old shape already had that fault in miniature (it removed the
signature from the bag, so a signed page drew its index against a smaller number than a plain one);
it does not any more. `ServingHabitTests.TheOrderStream_DrawsTheSameWhicheverPageItIs` is the guard,
across four seeds and two rails.

**The order stream shifts once, globally** — one new draw per order — so the 200-run sim needs
re-baselining before its numbers are read as balance.

The book page prints it: one line, `USUALLY WITH`, each preparation with its own drawn mark, on a
page the bar owns (a page not bought still shows nothing of how it is made).

## R6 — the pendants (shipped 2026-09-23)

> *"Tavan ışıkları abajurun üstünden başlıyor, abajurun üstünde kalan kısmı kes."*

The SPOT's apex is lifted above the shade on purpose — that is what makes its cone the bulb's width
where it leaves the glass instead of a point (`DiegeticStage.ApexLift`, the eighth list). But the
shaft of lit air is the part you can SEE, and a lifted apex drew a wedge of light OVER the lamp,
which is the one thing a shade is for.

The light keeps its lifted apex; the AIR is hung back down the whole of the lift
(`PendantAir.DropBelowSpot`, set in the room's own scale where the fixture is laid out) and given
the bulb's radius as its inner core instead. Same "leaves the glass at the glass's width", nothing
above the shade. **Not seen in play.**

---

## E1 — the economy's first half (shipped 2026-09-23)

The author's ruling on the two open questions: **keep the ranks, decouple the price**, and they run
the sim.

### The drink

`DrinkPricing` — a band per (rung × work), and the page's own place inside its band by rank.

| rung | easy | medium | hard |
|---|---|---|---|
| 0★ (ranks 1–8) | **3–6** | **7–10** | **11–12** |
| 1★ (9–11) | 13–17 | 18–22 | 23–27 |
| 2★ (12–14) | 28–35 | 36–45 | 46–55 |
| 3★ (15–21) | 58–72 | 73–92 | 93–112 |
| 4★ (22–29) | 120–150 | 151–190 | 191–235 |
| 5★ (30) | 250–310 | 311–390 | 391–470 |

The zero-star row is the brief's own figures to the dollar. Above it the bands roughly double a rung
at a time, which is the curve the fittings ladder climbs at, so the two sides of the book keep pace.

**`StarEconomy.TierMultiplier` came OFF the drink.** It used to stand in `TycoonRun.PriceOf` as well
— a two-star bar's drinker paid three times the menu — and with the band carrying the stage that
would be the stage counted twice. Two things follow, both of them the point:

- **the book stops lying.** The page printed the sheet while the drinker paid three times it, so
  from two stars up the one number the page is about was wrong. What the page says is what a
  regular crowd pays.
- **the way to earn more is to serve better drinks**, not to have earned more before. A Vodka &
  Soda is a four-dollar drink at five stars exactly as it is at none.

The landlord still reads the stage; that half of `StarEconomy` is untouched.

### The upgrades

The brief: *"0-0.5 arası 20-30, 1-2 arası 100-200, 2-3 arası 400-500, 4-5 arası max 1000 ...
katlanarak gitmesi iyi olur, buna bardak fiyatlarını da dahil et."*

- **The glass line was the one that needed it.** Its five upgrades were charged RAW at 12–105 while
  a three-star fitting already cost about 600 — the ladder the author asked to be included was
  nearly free to climb. Re-cut onto the curve: highball/rocks 110 · 280 · 520 · 760 · 950, pint a
  little over, martini/coupe 130 · 320 · 560 · 800 · **1000**.
- **The fittings are charged `PriceAt(sheet, stars)`,** so the middle rungs already landed on the
  brief's figures (1★ ≈ 100–200, 2★ ≈ 255–495). Only the ends moved: the opening room compressed
  into **20–35** so a bar with nothing can dress itself, and the top lifted to **850–1000**.
  Relative order inside every slot is preserved — what was dearer is still dearer.

### The characters now say what they do

*"Bufflar statlarıyla beraber açıklanmalı. Örneğin (Sabır +%5) gibi."* The first cut printed no
figures at all, on the theory that a menu is not a stat sheet. **That was wrong and is reversed:** a
buff nobody can read is a buff nobody can plan around. Each of the nineteen carries a `StatKey` and
a signed `StatPercent`, and the page prints it in the heavy face at the strip's right-hand end —
`SABIR +%40`, `BAHŞİŞ −%13`. The sign is the NUMBER's, not the character's: a buff that takes a
fifth off the lateness penalty prints −20%, and the green ink beside it is what says the news is
good.

## E2 — the night is planned, and the numbers are measured (2026-09-23)

*"her gün ekonomiye göre nasıl siparişler gelecek oyuncu hangi gün ne kadar kazanabilecek bunların
hesaplamalarını yap, tamamen rastgele değil bir düzen içerisinde rastgele olmalı ... Her yıldız
seviyesinde günler ve ilk 1 hafta için kesinlikle özel sipariş listesi olmalı."*

**`DayPlan`** cuts a whole night up front — covers from the door's own arithmetic, a split across
the rungs the bar has opened (45/30/15/7 counting down from its own), a difficulty mix per rung
(0★ 85/15/0 … 5★ 25/45/30), a cap of 18% of the night on any one page, and the rung's pages turned
a place a night so a big book is seen rather than a quarter of it. Only the ORDER is shuffled, on a
new "plan" stream. A night that outlasts its plan reads the same list again.

**What it replaced was the economy's quietest bug.** `TycoonConfig.OrderPoolSize(day) = 3 + day`
capped the pool at the lowest-ranked `3 + day` pages ON TOP of the star gate, so a bar that reached
three stars on day four owned twenty-one recipes and was asked for seven — all from its opening
menu. Everything bought to climb was unorderable for a fortnight. It is deleted.

**`FirstWeek`** authors the opening seven nights as a lesson: the two-part build, then the keg,
then the neat pour, then the shaker, then the whole menu. The sheet total rises every night (a test
holds it) and the opening night takes about $63. It stands down the moment the bar leaves the
ground floor.

**The opening menu is six pages, not four.** `gin_tonic` and `whiskey_cola` are unlocked in both
`recipes.json` and `RecipeCatalog`: a new bar owned a pint, a neat pour, one highball and one page
it could not yet make, which is not a menu a bench can be taught on.

**A glass of stock is priced by the bottle's tier** ($1–4, `RefillPricePerCapacity(tier)`). The flat
$3 was written against one straight rank line and, against the $3–6 opening band, took 60–100% of a
new bar's takings.

**The till has a ceiling.** `DrinkPricing.CeilingPerDrink` (399) is enforced in `ServiceJudge` after
every multiplier — the tip gives way first. The two house pours (a pint, a neat pour) are priced off
the BAR's rung rather than the page's, or the keg leaves the economy the moment the bar climbs.

**And it is all measured**, not guessed: `EconomyProjection` (Core) computes night-by-night takings
from the constants the game reads, `LastCall → Economy Projection` writes them into
**`Docs/ECONOMY_2026-09-23.md`**, which is the answer to *"hesaplamalarını yap"*. Read that document
for the figures; three of its findings want a decision.

### Not done, and why

- **The sim is still unrun.** Three RNG streams moved and the kick/penalty/pricing rules changed, so
  the standing sim report is void. The projection above is arithmetic, not play — the author runs
  the 200-run sim and the two get read together.
- **Rent.** *"Rent bu kadar artmamalı oyuncu hep fakir kalıyor."* Now measured rather than guessed:
  it is 18–26% of a night's take at four and five stars and about 40% on the ground floor, and the
  ground floor is the only place it bites. Left alone pending the sim.
- **No page below three stars is rated HARD.** The mix table has a hard share from one star up and
  `RecipeDifficulty` gives it nothing to put there. A content gap: a two-star page with three pours
  and a method on it would fill it.
- **The 1★ rung is four pages**, against ten below it and eleven above. The first climb opens the
  smallest shelf in the book.
- **Glass and room upgrades do not explain their buffs yet.** The glass line feeds `Ambience` and
  the fittings feed `ComfortBase`, but neither says so on the upgrade screen. The menu's characters
  now do; these should read the same way.

## E3 — every fitting buffs something, and the buffs can be read (2026-09-23)

*"Tüm upgrade'ler çeşitli bufflar vermeli ... Hangi geliştirme takılıysa o buff aktif olacak, konfor gibi
değil. Bufflar kısa ve net bir şekilde küçük iconuyla markette ürünlerde gözükmeli."*

**Core.** Sixteen kinds (`FittingBuffs`), one per slot, in all 88 rows of `fixtures.json` (`buff` /
`buffPct`, additions only). A buff is live only while its piece is INSTALLED (`TycoonRun.IsActive`);
the room is frozen once the doors open; each hook is a scale on an existing constant, so a bare room
is judged, priced and paced exactly as before, and no kind draws a random number (`HouseBuffs.None`).
Caps per kind; the full room lands on every cap. 38 new tests (`FittingBuffTests`,
`FittingBuffDataTests`); the headless suite is 530/530. `SinkSeconds` no longer ignores the basin you
bought. Measured in dollars in `Docs/ECONOMY_2026-09-23.md` §5b.

**The one decision the author must know about.** The tools (shaker, tap tower, sink) now read the
INSTALLED piece, not the best one owned. That reverses the 2026-09-13 ruling ("a bar that wears the
steel tin still shakes at the gold one's pace"), on the strength of the author's own "hangi geliştirme
takılıysa o buff aktif olacak". `FixtureTests` holds the new rule.

**UI.** One plate for every buff in the game — `ChromeArt.BuffPlate`, framed, corners softened, green
for a buff, red for a nerf, cream for off — with an icon, the figure as the loudest thing on it (white
body, black outline) and a short word. The menu's character strip was rebuilt on it (no two texts share
a line any more, every width measured); thirteen `buff_*` icon masks; the page title is Jersey 15 at
27 for the Latin tables; the licence's recipe hover is rebuilt from the page's own blocks. The market
shows a page's character on its recipe tile, a premium bottle's shelf premium, and every fitting card
its kind, figure and state (on / off until worn / on show / always / the house standard / locked).

**The garnishes** read at their drawn colours: the rail no longer dims the dishes whenever no glass
stands on the coaster, each dish carries 40% of its own light over the room's, and its contents are
lifted in memory (`GarnishArt`, never a PNG; `LiftGamma = 1` turns it off). The menu's garnish icons
are untinted 18px minis instead of a brown-inked 12px squeeze.

**The shaker** names every bottle in the tin: four full rows, two columns of compact cells from five,
"+N" past eight.

### Still waiting

- **The editor.** Nothing above has been seen in play. The PlayMode floor and the look tests have to
  run (the licence and market crops may move), the Jersey title needs checking on Turkish cedillas,
  and `LiftGamma 0.70` / `DishOwnLight 0.40` want the author's eye.
- **The 200-seed A/B** (`LastCall → Simulate Fitting Buffs A-B`) — the clock kinds are the sim's to
  measure; the trims are pre-ordered in the spec if the room runs hot.
- **27 languages** for the new strings (English and Turkish are done).
