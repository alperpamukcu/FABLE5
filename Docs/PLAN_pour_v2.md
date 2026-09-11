# PLAN — POUR v2: the main mechanic, rebuilt (2026-09-11)

**The author (2026-09-11):** *"İçecek dökülme fiziği ve dökülme hareketini geliştir. Şu an uzun
bardağa sahnede şişeyi yukarı çıkaramadığımızdan dolayı şişeden sıvı dökemiyoruz. Tüm dökülme
hareketini baştan gözden geçir, daha çok hacim düşsün ve daha çok sıvı gibi hareket etsin.
Dökülen sıvıların sıvı dokusu olması için bir yol düşünelim, dümdüz boyalı alan gibi
gözükmesin; oyundaki ana ve en önemli mekanik bu olacak."* — and, mid-turn: *"Sıvılar ve şişeler
daha hareketli olmalı"*, *"Bardağa koyarken sıvılar yere damlıyor gibi gözüküyor"*.

This is the phase log. Each phase compiles, keeps both suites green (or names the test it
changes and why), and is verified in the editor on its own before the next one starts.

---

## 0 · What was measured (before any change)

A map of the whole pour chain was drawn by seven parallel readers and a completeness critic
(`scratchpad/pour_map.json`, 2026-09-11); the numbers below were then re-derived or measured in
the editor through the fluid's own API.

| Finding | Evidence |
|---|---|
| **The tall glass cannot be poured.** Tilt is derived from lift about a grip at 0.22 of the tin, so d(mouth.y)/dy = 1 − 3.34·sin θ turns NEGATIVE past 17.4°: lifting the tin LOWERS its mouth. The 420 glass (2026-09-09, b791fba6) put the rim above everything the mouth can reach at a pouring angle | pour window 340 glass: pivot y −62..266 = **328 u**, 42–118°; 420 glass: −62..−52 = **10 u**, 42–48°. 29 of 53 cocktails stand in a 420 glass |
| **No liquid draws in a player build.** The shader is found only by `Shader.Find` and referenced by no asset, so a build strips it | `MetaballFluid.cs:276`; GUID d7fa328c… in no .mat/.asset/.unity; not in `m_AlwaysIncludedShaders` |
| **A full 420 glass costs 3.6× the solver budget** — even standing still | 1,640 particles, **11.7 ms/step at rest** (budget 3.2 ms at 556) |
| **The stream alone exhausts its drop slots** | empty glass: 90.5 of 110 slots avg, 110 max |
| **Splashes starve the stream** | full glass: splash 60/110 avg, stream falls to 34 |
| **The stream carries ~1/10 of the area the pool gains**; landing drops are deleted and the pool grows at random x | `MetaballFluid.cs:1005`, `:496-502` |
| **On the shaker bench the stream falls past the tin** (the "drips to the floor" look) | full tin: 139 particles, zero splashes — nothing lands |
| **Flow ignores tilt everywhere** — GDD 24 §2 specs "more tilt = faster pour", never built | `Serve.cs:260`, `Shaker.cs:885` |
| **The body is one flat colour** + an edge lift + a 1/64-uv band | `MetaballLiquid.shader:249-308` |
| **The held vessel has no weight** and is left floating, tilted, on release | `Serve.cs:186`, `Shaker.cs:825` |

## 1 · The design (one of four panel designs finished; adopted with three changes)

* **Hold the NECK, turn about it.** The vessel rotates about a grip point `g` below its drawn
  spout, so `d(spout.y)/d(lift) = 1 − g·sin θ·(dθ/dy) > 0` at every angle — raising the hand can
  never lower the mouth. GDD 24 §2.2 *"the higher it goes, the further it tips"* stays word for
  word; only where the hand holds the vessel changes. Serve tin: grip rest (190, 89), full tilt at
  259, pour band ~116 u (was 10).
* **A pure Core flow law** (`BottlePour`, beside `TapPour`): a fill-dependent onset (a full vessel
  tips at ~24°, the last drops need ~100°) and a rate that grows with the lean. The UI stops owning
  pour rates (`PourTimeScale`, `ServePourRate` move into `TycoonConfig`).
* **The stream is a rope of linked nodes** with its own budget (splashes can't take it), width
  from Core's actual per-frame volume, a drops-only gravity (the pool keeps its calibrated one),
  and it **lands INSIDE the vessel** and carries its volume into the pool where it hits.
* **A pixel-art liquid**: five value bands that MULTIPLY the drink colour (never toward white),
  ordered dither, dark walls, depth, meniscus, a 2.5D top face in the shader; a striped rope;
  pixel splashes; agitation that is exactly zero at rest.
* **Weight**: spring-followed grip and tilt, a pendulum swing, return to the bench on release with
  an exact snap (LookTests stay byte-identical), slosh in the bottle and the pool.

**Changed from the panel design:** (1) the render-texture/Blit pipeline is NOT the first path —
texel snapping inside the existing canvas shader gives the same look without the URP risk;
(2) a **rest early-out** in the solver is pulled forward into phase 1, because a still full glass
already burns 11.7 ms; (3) phases are re-cut so the two things broken today ship first.

## 2 · Phases

| # | Phase | Done when |
|---|---|---|
| **P1a** ✅ | The shader ships (Resources material + `LogError` + `ShipTests`); the tall glass's solver fixed; rest early-out; profiler markers | **done 2026-09-11** — see §3 |
| **P1b** ✅ | Neck grip + springs + release-home on both benches; drawn spout and drawn rim on the serve bench | **done 2026-09-11** — see §4 |
| **P2** ✅ | `BottlePour` Core law; `PourTick(dt, tilt)`, `PourOutTilted`; rates into `TycoonConfig`; `PourTick` picks the recipe's glass | **done 2026-09-11** — see §5 |
| **P3** ✅ | The rope stream, split budgets, drops-only gravity, landing into the vessel, tin sink, source alpha | **done 2026-09-11** — see §6 |
| **P4** | Pixel-art shading: bands, dither, walls, depth, meniscus, top face, rope stripes, pixel splash, agitation | A/B look report to the author BEFORE locking |
| **P5** | Slosh (bottle + pool, dt-correct damping); frozen solver core if P3 is over budget | pouring full glass ≤ 3.5 ms |
| **P6** | Docs: GDD 24 §2/§3/§3.5/§12, GDD 21 §3, GDD_MEVCUT | — |

**Rules this overturns, for the author to know:** the stream's 55% alpha (it draws at the
source's alpha now); the white-lerped top-face disc (becomes a multiply-only ellipse in the
shader); the fwidth soft edge (hard texel edges); the drawn "miss" stream (no liquid is drawn
that does not pour — GDD 21 already made the aim a gate); the fixed 42° onset (fill-dependent);
UI-owned pour rates (Core owns them).

---

## 3 · P1a — what landed and what it measured (2026-09-11)

**The shader ships.** `Assets/Resources/Fluid/MetaballLiquid.mat` references the shader, so a player
build carries it; `MetaballFluid` clones it (`Resources.Load`) and falls back to `Shader.Find` only
in the editor; a missing material is `LogError`, not a warning. `ShipTests.TheLiquidShaderShipsWithTheGame`
pins it. (Always Included Shaders was not touched: a Resources reference is sufficient.)

**The tall glass was boiling, not just slow.** Measured settled for six seconds, the full 420
highball had a median particle speed of 56 px/s and its drawn level swung between 68 and 125 over
two seconds, ~25 u short of the rim Core had filled it to. Four fixes, each measured:

| Step | Full 420 highball | Why |
|---|---|---|
| before | 1,640 particles, **13.1 ms**, level 68–125 | a 59-row column the passes cannot hold |
| particle fits the vessel (`FitParticleScale`, ≤ 44 rows) | 880, 6.2 ms, **level steady** | fewer, larger units in the same iso-surface; the tin, pint and short glasses keep scale 1 exactly |
| bottom-up + shock passes (interior only) + lattice seed | bottom still, top squeezed | a stack resolved half-and-half never reaches the floor; random seeds popped the body up under shock |
| packing re-measured (0.858) + capacity cap + inelastic boundaries | **707, 4.4 ms, level exact** | the stack sits at hex packing (0.71 × 1.208); a full glass holds what fits |

Half, quarter and three-quarter glasses now sleep completely, so the **rest early-out** skips the
solve (≈ 0 ms still). The rest test is "nothing moved more than a quarter of one frame's free fall"
— not "every velocity is zero", which froze free-falling particles in mid-air (the sleep threshold,
30 px/s, is above one frame of gravity).

Calibration after (serve bench): rocks at half **exact** (−79.99 vs −79.99); highball within one
`SurfaceY` bin (≤ 6.4 u, 2.1%) at 0.25 / 0.5 / 0.75, at the rim at 1.0.

**The stacked regime is the serve bench's only** (`SetStacked(true)`). On the tap it held two foam
bubbles under a rigid beer column that the old solver floats, and the tin and pint are calibrated
on the compressed packing. With the flag off they measure exactly as before: pint beer 197 / foam
34 / 0 sunk (baseline identical), tin 139 particles 0.76 ms.

Still open from P1a: a full glass is calm (p99 23 px/s) but not fully asleep, so it costs 4.4 ms
until it leaves the bench.

Suites: EditMode **505/505**, PlayMode **11/11**.

## 4 · P1b — what landed and what it measured (2026-09-11)

**`PourHand`** (`Assets/Scripts/UI/Flow/PourHand.cs`), one per bench: the vessel turns about a grip
a short way below its DRAWN spout, so `d(spout.y)/d(lift) = 1 − g·sinθ·dθ/dy` stays positive at every
angle (Configure clamps `g` to keep it so). GDD 24 §2.2's "the higher it goes, the further it tips"
is unchanged; only where the hand holds the vessel moved.

| Serve bench, live scene | before | neck grip |
|---|---|---|
| highball (420) pour window | **10 u** of hand travel, 42–48° | **108 u, 43–118°** — the whole tilt range |
| rocks | — | 108 u, 43–118° |

The serve bench now also aims **drawn spout at drawn rim** (`ServeSpoutNow` through the live transform,
because the hover glow grows the held tin 4% about its pivot — 11 u along the axis; `ServeRim` off the
glass art), the pair the shaker bench has measured since 2026-08-11.

**Weight**: the grip follows the pointer on a spring (ω 28, ζ 0.75), the tilt on a softer one (ω 18,
ζ 0.62), a sideways push swings the body under the neck (≤ 12°), and a released vessel walks home
(ω 16, ζ 0.9) and is then put EXACTLY on its rest — the hand writes the pose only while moving and
once on arrival, because the vessel at rest belongs to its additive HoverGlow. `Motion.Reduced`
drops the springs. The tin that runs dry leaves the bench only once it is standing on it; the mix
refusal and a full glass set it down instead of snapping it upright in mid-air.

**Tests**: `Tipping_the_tin_pours_into_the_tall_glass` (new) caps the tin by hand, rides the slide to
the glass, sweeps the tin up the bench in rows and asserts ≥ 4 rows pour into the highball — the
window the old grip left would score 0–1. It first pressed the wrong object: the serve panel carries
a second "Shaker" of its own, which a whole-panel search returns first; the test now looks on the
work surface. `A_released_bottle_goes_back_where_it_stood` (new): exact to 1e-4 u and 0.01°.
The existing bottle sweep passed unchanged under the springs.

Suites: EditMode **505/505**, PlayMode **13/13**.

## 5 · P2 — the pour's own law, in Core (2026-09-11)

`BottlePour` (`Assets/Scripts/Core/Pour/BottlePour.cs`), pure, beside `TapPour`:

| | value | why |
|---|---|---|
| onset, full vessel | **24°** | the liquid is already at the neck |
| onset, emptying | climbs to **102°** (curve 1.3) | the last drops need the vessel nearly upside down |
| past the onset | **8% trickle → 100%** over 30° (power 1.25) | a weir: flow grows with the head over the lip |
| hand pour full flow | bottle `PourRate` × `TycoonConfig.HandPourScale` **0.60** = 0.33 tin/s | was the UI's flat 0.45 × 0.55 = 0.2475 from 42° |
| serve pour full flow | `TycoonConfig.ServePourMax` **0.45** tin/s | was the UI's flat 0.34 from 42° |

GDD 24 §2's "more tilt = faster pour" (2026-07) is built at last, and the rates left the UI —
"rules never trust the UI". A 5% dash is ≥ 0.9 s of steady hand at the lip; one 30-fps frame at
full flow is under half the judge's precision window. Verbs: `PourTick(seconds, tilt)` (a lean under
the lip pours nothing, keeps the pour and does NOT un-mix the tin; what runs picks the serving glass
while it is empty — the sim's `PourMeasure` always did, the player's pour never had) and
`PourOutTilted(seconds, tilt)` (through `PourIntoServingGlass`, so the mix gate, the first-drop glass
and one-tin-one-portion all hold). The flat `PourTick(seconds)` stays for the verbs that pour a known time.

Both benches read the lip from `BottlePour.Share` — Core's function, read, never re-derived — and the
fixed 42° gate is gone from both. The stream's girth follows the share until P3 sizes it from Core's
delivered volume.

`BottlePourTests` (13): the onset, monotonicity, saturation, the empty vessel, the 5% dash, frame
quantisation, purity; and the verbs — under the lip, tipped further, the brim, the glass before the
serve, the mix refusal (on a book that cannot name the build: a matched recipe's method decides the
mix, so under the full catalogue gin with a liqueur is a Built drink and needs none), one tin one portion.

The sim pours through `PourMeasure` and `PourIntoServingGlass`, neither of which changed behaviour, so
its report is unchanged by construction. Suites: EditMode **518/518**, PlayMode **13/13**.

## 6 · P3 — the stream carries its drink into the vessel (2026-09-11)

**Why the author saw drinks "dripping onto the floor"**, found in two places:

* **Serve bench:** an EMPTY glass cleared its pool, and a pool that is not there is nothing to land on —
  the first drops of every pour fell straight through the transparent glass onto the counter behind
  it. The empty glass now sets its box with no drink in it, so the stream lands on its floor.
* **Shaker bench:** the steel tin draws no body below its brim, so the stream fell through the metal
  and out under it. The tin's mouth is now a **sink** (`SetSink`): the stream goes in and is gone.
* Also: splashes flew ±150 px/s for up to half a second — wider than the glass — and died wherever
  they were; and an off-aim "miss" stream was drawn falling wide while Core poured nothing. Splashes
  now rise no higher than half the air left in the glass (never over the rim), are held by the walls
  and melt back into the drink; no liquid is drawn that does not pour.

**The rope.** Stream and splash have their own slots (96 + 64; the pool gave up 24 it never uses,
so RenderMax is unchanged). Nodes leave by DISTANCE — every 0.3 of their radius — not every 6 ms,
and sway as one column on a slow wave instead of each drop getting its own random kick. Radius 10
× Core's share (4 before): a trickle is ~11 px, full flow ~21 px. Drops fall at their own 1800 px/s²;
the pool keeps its calibrated 1400. A drop crossing the rim INSIDE the mouth is held by the glass
walls from then on. New drink arrives at the stream's landing column, moving down — the level
rises FROM the pour instead of raining in across the whole surface. The stream keeps its own
alpha (it took the near-empty glass's, and fell at ~0.31 on the first frame) at 0.85 of it.

**A bug the picture found:** the rope still came out in lengths. Every node's height, dumped: six
nodes 3–13 px apart, then 19–33 px of nothing. The emitter placed a node emitted τ ago at
`v·(interval − τ)` — the same distance measured from the wrong end — so the spacing was uneven,
a frame that let nothing go doubled the gap behind it, and once the stream sped up the gap passed
the width the field can bridge (1.28·r). At `v·τ` with the fall's own curve the spacing climbs
smoothly, 6 → 18 px, inside 19 all the way down.

| Measured (whole tin into an empty highball / a bottle into a tin below its brim) | before | after |
|---|---|---|
| stream nodes that fell outside any vessel | the first drops of every pour | **0 / 0** |
| splashes outside | up to ±60 px wide | **0** |
| stream slots in use | 110/110 into an empty glass | **≤ 49 / 96** |
| rope | 3–5 px beads | continuous, 11–21 px, necking only just above the landing |

The pint is unchanged (197 beer / 34 foam / 0 sunk); the tap's stream is width 0.8.
Suites: EditMode **518/518**, PlayMode **13/13**.
