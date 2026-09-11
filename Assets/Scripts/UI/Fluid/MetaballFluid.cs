using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A particle-based liquid for the drink stages (GDD 24 §3.5, rewrite 2026-07-23). The
    /// body of liquid is a cloud of fine particles run through a position-based fluid solver
    /// (PBD: incompressibility as a relaxed minimum-distance constraint, neighbour-velocity
    /// viscosity, spatial-hash neighbours) that collide with the vessel's *moving* walls,
    /// so the drink sloshes and lags when the shaker is thrown about and pours as tiny merging
    /// droplets. Everything is rendered as one metaball field by <c>LastCall/MetaballLiquid</c>,
    /// so the particles read as connected liquid, never separate balls.
    ///
    /// Coordinates are the pour surface's local space (centre origin, px), the same the tilt-
    /// pour already uses. Volume stays deterministic (GlassContents drives the fill); the
    /// particle count just tracks that fill so the mass looks right.
    /// </summary>
    public sealed class MetaballFluid
    {
        /// <summary>The shipped template the fluid clones its material from — under Resources so
        /// a player build carries the shader (see the constructor). Pinned by ShipTests.</summary>
        public const string MaterialPath = "Fluid/MetaballLiquid";

        // Render budget — pool particles + free stream/splash drops share the shader's _Drops[].
        // THE STREAM HAS ITS OWN SLOTS (2026-09-11). 110 were shared, and measured: the stream
        // alone filled them into an EMPTY glass (90 of 110 on average, 110 at the peak — a 0.7 s
        // fall at one drop per 6 ms), and into a full one the splashes held 60 and left the
        // stream 34. Splashes can no longer take a stream node's place, and the pool gave up 24
        // slots it never uses (the tallest stacked glass holds ~880), so RenderMax is unchanged.
        private const int StreamMax = 96, SplashMax = 64;
        private const int MaxDrops = StreamMax + SplashMax;
        private const int MaxPool = 2016;
        private const int RenderMax = 2176;   // must match MAX_DROPS in the shader = MaxPool + MaxDrops

        private const float Gravity = 1400f;          // px/s² down — the POOL's; its calibration hangs on it
        /// <summary>The drops' own gravity. A pour falls hard and a stream that drifts down reads as
        /// syrup; but the pool's packing was measured under 1400, so the air gets its own number.</summary>
        private const float StreamGravity = 1800f;
        /// <summary>A stream node's radius at width 1. It was 4, and a thresholded kernel draws an
        /// isolated drop at 0.4 of its radius: the pour was a 3-5 px thread carrying about a tenth
        /// of the area the glass gained. A connected rope of 10s draws ~11 px at a trickle and ~21 at
        /// full flow — a fifth of a highball's mouth, a pour you can read the rate off.</summary>
        private const float StreamRadius = 10f;
        /// <summary>Node spacing as a share of the node's radius. Under ~1.28 the fields of two
        /// neighbours join; at 0.3 the rope stays one column most of the way down.</summary>
        private const float StreamSpacing = 0.30f;
        /// <summary>How much thinner liquid in the air is drawn than liquid at rest (the shader's
        /// _StreamAlpha). 0.55 made the pour the faintest thing on the bench — see SetStreamColor.</summary>
        private const float StreamAlphaShare = 0.85f;

        // Position-based fluid (PBD / position-based dynamics, the real-time SPH-family method).
        // Incompressibility is a hard MINIMUM-DISTANCE constraint relaxed a few passes per frame:
        // particles can never pack closer than Spacing, so the body stacks up to the fill line
        // and never collapses. Neighbour-velocity viscosity makes it flow. The particle COUNT is
        // derived from the fill area at Spacing, so it fills any vessel exactly.
        // Recalibrated 2026-07-28, twice: first because a vessel the rules called FULL drew
        // about three quarters full, then because the fix for that cost 10 ms a frame.
        //
        // On the fill: the estimate assumed an ideal packing (a settled particle really takes
        // about 0.71·Spacing²), and the body was compressed worst in the TALL vessels, where
        // the pressure never reached the top of a 40-row column.
        //
        // On the cost: this solver is O(particles × passes) and nothing else came close — with
        // a full tin the frame went 1.8 ms → 12.5 ms, and turning the metaball SHADER off
        // changed nothing at all, which is where the blame would naturally have fallen. So the
        // particle scale is as coarse as the look allows. Measured on a full tin:
        //   1007 particles, 22 passes, both-way pair sweep   10.1 ms   (12.5 ms frame)
        //   556 particles, 14 passes, forward-only sweep      3.2 ms   (~5 ms frame)
        // and 2.1 ms while it is being shaken, which is the case that was reported. The blob
        // radius scales with the spacing, so the drink looks the same — there are simply fewer,
        // larger units inside a surface that is drawn at the same smoothness.
        private const float H = 12.9f;                  // viscosity/neighbour radius (px)
        private const float Spacing = 5.2f;        // rest spacing (min distance) → many small particles
        private const int   RelaxIters = 14;           // incompressibility relaxation passes
        /// <summary>The cap while the vessel is being thrown about, where the body never settles
        /// and the level is not being read anyway.</summary>
        private const int   ShakeRelaxIters = 8;
        /// <summary>
        /// Area one settled particle really takes, as a share of Spacing² — measured, not
        /// derived. It is well under the √3/2 of an ideal hexagonal packing because a body that
        /// was POURED settles compressed: gravity is re-applied every frame and the relaxation
        /// only pushes back so far, so the pile finds an equilibrium tighter than its rest
        /// spacing. Every vessel in the game is filled by pouring, so that is the state to
        /// calibrate against — a body assembled in one frame instead sits at rest spacing, which
        /// is a different (and unreachable in play) density. The tin and the pint run on this.
        /// </summary>
        private const float PackedArea = 0.71f;
        /// <summary>
        /// The same share for the STACKED regime (below), measured 2026-09-11: 0.858. Relaxed
        /// bottom-up with shock passes, a still drink no longer settles compressed — it stacks at
        /// very nearly the hexagonal packing (0.866). On the 420 highball at half fill the drawn
        /// body stood 185.2 u off the floor against the 153.3 the 0.71 estimate asked for, a
        /// ratio of 1.208, and 0.71 x 1.208 = 0.858. The rocks glass then drew its half-full line
        /// to the hundredth of a unit, with its own SetDensity untouched.
        /// </summary>
        private const float StackedArea = 0.858f;

        // ── THE STACKED REGIME (2026-09-11) ─────────────────────────────────────
        //
        // The serve bench's tall glasses need a still drink to be a STACK: bottom-up shock passes,
        // a lattice seed, a cap at what the cavity holds, depth damping and boundaries that stop
        // motion without throwing it — together they took the full 420 highball from a body
        // boiling at 13 ms a step to one that sleeps. But the pint's head RISES through its beer
        // by buoyancy, and a rigid beer stack held two bubbles under it that the committed solver
        // floated (measured: 2 sunk against 0); and the tin and the pint are calibrated on the
        // compressed packing, which the stack no longer makes. So the regime is the bench's to
        // choose, and only the serve bench chooses it: the tin and the tap run exactly as before.
        private bool _stacked;
        /// <summary>Opt this fluid into the stacked regime — for a still, foam-free vessel
        /// (the serving glass). Re-seeds, because the packing the count assumes changes.</summary>
        public void SetStacked(bool on)
        {
            if (on == _stacked) return;
            _stacked = on;
            _pn = 0; _foamN = 0; _wake = true; _restFrames = 0; _capCache = -1;
        }
        // Render radius is well above the spacing so the fine, tightly-packed particles
        // overlap into ONE smooth connected surface with no gaps between them.
        private const float PoolRadius = 7.5f;
        private const float SideOffset = 0.27f;   // iso-surface reach past a side wall particle
        private const float FaceOffset = 0.53f;   // iso-surface reach past a floor/surface particle
        private const float Viscosity = 0.42f;        // 0..1 neighbour-velocity blend (more flow)
        private const float MaxSpeed = 1300f;
        private const float RestDamping = 0.94f;
        /// <summary>What a DEEP particle in a still vessel keeps of its velocity, per 1/60 s.
        /// See the depth damping in StepPool: the surface is untouched by it.</summary>
        private const float DeepDamping = 0.55f;
        /// <summary>How many of the final relaxation passes use shock propagation in a still
        /// vessel (see StepPool: the lower particle of a stacked pair holds, the upper moves).</summary>
        private const int ShockPasses = 4;
        private const float ShakeDamping = 0.995f;   // barely damped while the tin is moving
        private const float ShakeViscosity = 0.22f;  // freer to move, but still one body         // bleeds off the energy the solver adds
        private const float SleepSpeed = 30f;
        private const int MaxNeighbours = 96;   // generous: a weak constraint is what let the clump form
        private const float MinProfile = 0f;      // the interior is shaped by the profile alone now             // below this a particle is simply at rest

        // ── foam (GDD 21 §10, 2026-07-30) ───────────────────────────────────────
        // The head on a pint used to be a separate rectangular Image laid over the beer, which
        // is why it read as a rectangle and not as a liquid: it had straight sides, square
        // corners, and it refused to rotate with the glass. Foam is now made of the SAME
        // particles as the beer, so the two share one metaball surface and the head is a
        // wobbling, bubbled crown that leans when the glass leans — because it is the same
        // body of fluid, differing only in what it is made of.
        private const byte KindBeer = 0, KindFoam = 1;
        /// <summary>Foam is mostly air, so it barely falls — but it does fall, which is what lets
        /// a glass of pure froth fill from the bottom instead of sticking to the rim.</summary>
        private const float FoamGravity = 0.46f;
        /// <summary>
        /// The share of beer neighbours at which buoyancy is already at full strength. Below it
        /// the lift tapers off, so a bubble merely *resting* on the beer — the whole underside of
        /// the head — is not fired upward, and the head stays a layer instead of a diffuse cloud
        /// (a 13% head drew 3.4× too thick before this tapered off at all).
        ///
        /// It is a RAMP and not a dead zone, though: a dead zone traps bubbles. One pressed
        /// against the glass wall could be too buried to sink and too lightly buried to be lifted,
        /// so it sat there — and the head grew a streak of foam clinging down one side, which is
        /// exactly what "the foam has to stay on top of the beer" rules out (2026-07-30).
        /// </summary>
        private const float FoamFullLiftAt = 0.35f;
        /// <summary>
        /// A light nudge along gravity between an overlapping beer/foam pair — foam up, beer down.
        /// Only a nudge: buoyancy below is what actually stratifies the drink, and this was doing
        /// the job twice. At its old strength it kept pushing after the layers had already parted
        /// and opened a gap between them wide enough for the metaball field to fall under its
        /// threshold — the head and the beer were separated by a ragged black hole (2026-07-30).
        /// </summary>
        private const float FoamSort = 0.5f;
        /// <summary>
        /// Real buoyancy, and the reason the head can be relied on to stay on top of the beer
        /// (2026-07-30): a foam particle surrounded by beer is pushed UP, hard, in proportion to
        /// how much beer is around it. The pairwise sorting above is a local nudge and a bubble
        /// that got buried could have its nudges cancel from all sides; this cannot cancel,
        /// because it is a body force that only points one way. It is also self-cancelling in
        /// the right way — the moment a bubble reaches the surface it has no beer above it, the
        /// force fades out, and it settles instead of being fired out of the glass.
        /// </summary>
        private const float FoamBuoyancy = 0.9f;
        /// <summary>Froth is viscous. It slumps and wobbles where beer sloshes, so it is damped
        /// harder and blended toward its neighbours more — the head moves as one soft mass rather
        /// than a cloud of jittering specks.</summary>
        private const float FoamDamping = 0.86f;
        private const float FoamViscosity = 0.72f;
        /// <summary>Bubbles are coarser than beer, so foam draws with a bigger, varied blob: the
        /// crest breaks into rounds instead of running as a smooth line.</summary>
        private const float FoamRadius = PoolRadius * 1.26f;
        /// <summary>
        /// Foam is drawn with fewer, bigger blobs than the same volume of beer. Given the beer's
        /// particle count the head tiles the glass densely and its top row comes out flat however
        /// it is packed — the bumps are smaller than one particle. Coarser bubbles put the relief
        /// at a size the eye can see. The volume is unchanged: the drawn surface is set by the
        /// blob radius, and radius² rises to cover the count that was dropped.
        ///
        /// Only *slightly* coarser, though. At half the count and 1.5× the radius a thin head had
        /// barely three bubbles to a column, so it clumped instead of covering: the layer opened
        /// 12–21 px gaps that the field could not bridge (measured 2026-07-30). Continuity first
        /// — the relief comes from the per-bubble ceiling and the slack packing, not from being
        /// so sparse the head stops being a layer.
        /// </summary>
        /// Deliberately ABOVE the 0.63 at which count × radius² would come to exactly 1. At that
        /// figure the head draws its true depth (1.07× on a 26% head) but is too sparse to stay
        /// continuous — a partly-full glass opened 22 px holes in two of nine columns. Generous
        /// coverage costs a head drawn about a quarter deep and buys a head with no holes in it,
        /// and of the two only one of them looks like a bug. Measured both ways, 2026-07-30.
        private const float FoamCountScale = 0.8f;
        /// <summary>
        /// How far the head may stand PROUD of the vessel's rim. A real head crowns over the
        /// glass; clamped to the same ceiling as the beer it was planed dead flat instead, which
        /// measured 1.6 px of relief across the whole crest — a rectangle by another name.
        /// </summary>
        private const float FoamCrown = 9f;
        /// <summary>
        /// Foam relaxes only part way. The full-strength minimum-distance constraint settles
        /// particles into a near-crystalline lattice, which is right for a liquid — its surface
        /// really is flat — and wrong for froth, which is a heap of unlike bubbles. Under-relaxing
        /// leaves the packing irregular, so the head is lumpy where the beer is smooth. Free: it
        /// is a multiply inside a branch the sorting already needs.
        /// </summary>
        private const float FoamSlack = 0.7f;

        // Viewport margins: room around the vessel for splashes and the falling stream column.
        private const float StreamMargin = 110f;
        private const float SplashMargin = 40f;

        private readonly RectTransform _rt;
        private readonly RectTransform _surface;
        private readonly RawImage _image;
        private readonly Material _material;
        private Vector2 _size;
        private float _originX, _originY;   // the viewport's centre in surface-local px

        // ── the pooled liquid: particles ────────────────────────────────────────
        private readonly float[] _px = new float[MaxPool];
        private readonly float[] _py = new float[MaxPool];
        private readonly float[] _vx = new float[MaxPool];
        private readonly float[] _vy = new float[MaxPool];
        private readonly float[] _ppx = new float[MaxPool];
        private readonly float[] _ppy = new float[MaxPool];
        private readonly float[] _qx = new float[MaxPool];   // predicted (pre-constraint) position
        private readonly float[] _qy = new float[MaxPool];
        /// <summary>Beer or foam. Both live in the same arrays and the same solver — the head is
        /// not a separate system, it is the same fluid made of lighter stuff.</summary>
        private readonly byte[] _kind = new byte[MaxPool];
        /// <summary>What the final clamp of the frame pressed each particle against: bit 1 the
        /// floor, bit 2 the left wall, bit 4 the right wall, bit 8 the ceiling. See
        /// ClampToVessel(true).</summary>
        private readonly byte[] _contact = new byte[MaxPool];
        // The bottom-up visiting order for a still vessel's relaxation, rebuilt each frame
        // without allocating (Array.Sort on preallocated key/index buffers).
        private readonly int[] _order = new int[MaxPool];
        private readonly float[] _orderKey = new float[MaxPool];

        private float _topKey;                       // the highest particle along gravity's up

        private void SortBottomUp(float upX, float upY)
        {
            float top = float.MinValue;
            for (int i = 0; i < _pn; i++)
            {
                _order[i] = i;
                float k = _px[i] * upX + _py[i] * upY;
                _orderKey[i] = k;
                if (k > top) top = k;
            }
            _topKey = top;
            System.Array.Sort(_orderKey, _order, 0, _pn);
        }

        private int _capCache = -1;
        private float _capW, _capH, _capK, _capArc;
        private float[] _capProfile;

        /// <summary>How many particles the cavity holds on a hexagonal lattice at the rest spacing
        /// — the most that fit without overlap. Cached until the vessel, scale or floor change.</summary>
        private int LatticeCapacity()
        {
            if (_capCache >= 0 && _capW == _halfW && _capH == _halfH && _capK == _k
                && _capArc == _floorArc && ReferenceEquals(_capProfile, _profile))
                return _capCache;
            float ix = Mathf.Max(_halfW - _pr * SideOffset, 2f);
            float iy = Mathf.Max(_halfH - _pr * FaceOffset, 2f);
            float rowH = _sp * 0.8660254f;
            int n = 0;
            for (int r = 0; ; r++)
            {
                float y = -iy + r * rowH;
                if (y > iy) break;
                float w = HalfWidthAt((y + iy) / (2f * iy), ix);
                for (float x = -w + ((r & 1) != 0 ? _sp * 0.5f : 0f); x <= w; x += _sp)
                {
                    if (_floorArc > 0f)
                    {
                        float u = Mathf.Clamp(x / Mathf.Max(ix, 1f), -1f, 1f);
                        if (y < -iy + _floorArc * (1f - Mathf.Sqrt(1f - u * u))) continue;
                    }
                    n++;
                }
            }
            _capCache = Mathf.Min(n, MaxPool);
            _capW = _halfW; _capH = _halfH; _capK = _k; _capArc = _floorArc; _capProfile = _profile;
            return _capCache;
        }

        /// <summary>Lays up to <paramref name="count"/> drink particles on a hexagonal lattice from
        /// the floor up, inside the vessel's own walls and floor arc; returns how many fit.</summary>
        private int SeedLattice(int count)
        {
            if (count <= 0) return 0;
            float ix = Mathf.Max(_halfW - _pr * SideOffset, 2f);
            float iy = Mathf.Max(_halfH - _pr * FaceOffset, 2f);
            float gap = _sp * 1.02f;                 // a hair apart: nothing to push on frame one
            float rowH = gap * 0.8660254f;
            int placed = 0;
            for (int r = 0; placed < count && _pn < MaxPool; r++)
            {
                float y = -iy + r * rowH;
                if (y > iy) break;
                float w = HalfWidthAt((y + iy) / (2f * iy), ix);
                for (float x = -w + ((r & 1) != 0 ? gap * 0.5f : 0f);
                     x <= w && placed < count && _pn < MaxPool; x += gap)
                {
                    if (_floorArc > 0f)
                    {
                        float u = Mathf.Clamp(x / Mathf.Max(ix, 1f), -1f, 1f);
                        if (y < -iy + _floorArc * (1f - Mathf.Sqrt(1f - u * u))) continue;
                    }
                    _px[_pn] = x; _py[_pn] = y; _vx[_pn] = 0f; _vy[_pn] = 0f;
                    _kind[_pn] = KindBeer; _contact[_pn] = 0;
                    _pn++; placed++;
                }
            }
            return placed;
        }
        /// <summary>
        /// 0..1, how much beer is sitting ON TOP of this particle — measured from the neighbours
        /// the viscosity pass already gathers, so buoyancy costs no extra neighbour search.
        ///
        /// Above, not merely around (2026-07-30). "Around" cannot tell a buried bubble from one
        /// resting on the beer's surface, and lifting the resting ones pushed the whole underside
        /// of the head up off the beer — leaving a gap between the layers that the metaball field
        /// could not bridge, which drew as black holes through the drink.
        /// </summary>
        private readonly float[] _submerged = new float[MaxPool];
        private int _pn;                               // live pool particles
        private int _foamN;                            // how many of them are foam

        // The forward half of a 3×3 neighbourhood: this cell, then the four that come after it
        // in scan order. Every neighbouring pair is met exactly once across the whole sweep.
        private static readonly int[] StencilX = { 0, 1, -1, 0, 1 };
        private static readonly int[] StencilY = { 0, 0, 1, 1, 1 };

        // Spatial hash grid → O(N) neighbour queries, so the particle count can go high cheaply.
        // 8192 buckets against the few hundred cells a vessel actually occupies: collisions are
        // rare, and harmless when they happen — a false neighbour just fails the distance test.
        // It was 32768, which is a 128 KB table blanked once per relaxation pass to hold ~550
        // entries.
        private const int GridBuckets = 8192;          // power of two
        private readonly int[] _cellHead = new int[GridBuckets];
        private readonly int[] _next = new int[MaxPool];
        // The cell is the CONSTRAINT distance, not the (larger) viscosity radius: at a fine
        // particle scale a viscosity-sized cell would hold dozens of particles and make the
        // relaxation sweep expensive. Relaxation scans 3×3 cells; viscosity widens its sweep.
        private const float Cell = Spacing;
        private static readonly int ViscCellR = Mathf.CeilToInt(H / Cell);   // a ratio: scale-free

        // ── THE PARTICLE FITS THE VESSEL (2026-09-11) ───────────────────────────
        //
        // Every length above is the particle at SCALE 1, the scale the solver was tuned at — in
        // the tin, a column about 41 rows tall, which measured fully asleep when settled. The
        // 420-tall highball of 2026-09-09 is 59 rows at that scale and it never settles: its
        // "still" body measured a median particle speed of 56 px/s, its drawn level swinging
        // between 68 and 125 over two seconds, ~25 u short of the rim Core had filled it to,
        // at 11.7 ms a step. Fourteen passes cannot carry the pressure up a column that tall,
        // and the overlap they leave comes back as velocity the next frame — the body boils.
        //
        // More passes would cost what the frame does not have. So the PARTICLE grows with the
        // vessel instead: SetPool fits the scale so no column is taller than MaxRows, every
        // length the solver and the renderer use grows with it, and the body is drawn from
        // fewer, larger units inside the same smooth iso-surface. A vessel inside the tuned
        // height keeps scale 1 exactly, so the tin, the pint and every short glass are
        // untouched — and the next glass to change size cannot silently break this again.
        private const float MaxRows = 44f;
        private float _k = 1f;                                  // the fitted particle scale
        private float _sp = Spacing, _h = H, _pr = PoolRadius, _fr = FoamRadius, _cell = Cell;

        /// <summary>The particle scale SetPool fitted to the current vessel (1 = as tuned).</summary>
        public float ParticleScale => _k;

        private void FitParticleScale(float cavityHeight)
        {
            // 5% steps, so a vessel is re-seeded when it really changes size and never because
            // a rim moved by a fraction of a pixel between frames.
            float k = Mathf.Max(1f, cavityHeight / (Spacing * MaxRows));
            k = Mathf.Round(k * 20f) / 20f;
            if (Mathf.Abs(k - _k) < 1e-3f) return;
            _k = k;
            _sp = Spacing * k; _h = H * k; _pr = PoolRadius * k; _fr = FoamRadius * k; _cell = Cell * k;
            // Particles packed at the old spacing would be crushed or blown apart by the new
            // one; the body is re-seeded whole, as it is on any change of vessel.
            _pn = 0; _foamN = 0; _wake = true; _restFrames = 0;
        }

        // Container (vessel interior) this frame: a rect rotated by _angle, narrowed at each
        // height by an optional silhouette profile so the liquid takes the VESSEL's shape
        // (a tapered tin, a tumbler) instead of filling an invisible box (2026-07-24).
        private float _cx, _cy, _halfW, _halfH, _angle;
        // Particle positions are stored in the CONTAINER'S LOCAL frame (origin at its centre,
        // unrotated). The walls are therefore static in the sim, so a shaken vessel can never
        // teleport into the liquid and crush it; the shaking arrives as an inertial force.
        private float _fillTopLocal;
        private float _shakeAx, _shakeAy;      // inertial acceleration from the vessel's motion
        private float _vcx, _vcy, _vesselSpeed;      // the vessel's own velocity, for inertia
        private const float MaxShakeAccel = 5200f;   // ~4g: hard enough to slosh, soft enough to stay incompressible
        private float[] _profile;   // half-width multipliers, bottom → rim; null = plain rect
        private float _fillTopY;                       // current liquid line (for spawns)
        private bool _poolSet;

        // ── free droplets: the pour stream and splashes ─────────────────────────
        private struct Drop
        {
            public Vector2 Pos, Vel;
            public float Radius, Life;
            public bool Merges, Active;
            /// <summary>Went in through the vessel's MOUTH: from then on it is inside the glass and
            /// the walls hold it. One that crossed the rim outside the mouth is falling past.</summary>
            public bool Entered;
        }
        private readonly Drop[] _drops = new Drop[MaxDrops];
        private readonly Vector4[] _dropData = new Vector4[RenderMax];
        private float _emitAccum;
        private float _streamT;                  // the stream's own clock, for its sway
        private float _lastLandLx, _lastLandAge = 99f;   // where the stream last hit the drink

        // THE TIN'S MOUTH (2026-09-11). The tin is opaque, so below the brim it draws no body —
        // and with no body to land on, the stream fell straight through the steel and out under
        // it: the "drips to the floor" the author saw. A sink is the mouth as a hole the stream
        // goes into, for a vessel that shows nothing inside.
        private bool _sinkSet;
        private float _sinkX, _sinkY, _sinkHalf;

        /// <summary>A mouth the stream goes INTO and vanishes, for a vessel whose inside is not
        /// drawn (the steel tin below its brim). Surface-local px.</summary>
        public void SetSink(Vector2 mouthCentre, float halfWidth)
        {
            _sinkSet = true; _sinkX = mouthCentre.x; _sinkY = mouthCentre.y; _sinkHalf = halfWidth;
        }

        public void ClearSink() => _sinkSet = false;

        // What became of every drop that left the air — the evidence for "nothing drips onto
        // the counter". Landed: melted into the drink. Swallowed: went into a sink. Lost: a
        // stream node that fell out of the viewport or ran out of life without landing anywhere,
        // which is exactly the drop the author saw fall past the glass.
        public int StreamLanded { get; private set; }
        public int StreamSwallowed { get; private set; }
        public int StreamLost { get; private set; }
        public int SplashLost { get; private set; }
        public void ResetDropCounts() { StreamLanded = StreamSwallowed = StreamLost = SplashLost = 0; }

        private static readonly int IdSize      = Shader.PropertyToID("_Size");
        private static readonly int IdColor     = Shader.PropertyToID("_Color");
        private static readonly int IdDropCount = Shader.PropertyToID("_DropCount");
        private static readonly int IdDrops     = Shader.PropertyToID("_Drops");
        private static readonly int IdPoolMinX  = Shader.PropertyToID("_PoolMinX");
        private static readonly int IdPoolMaxX  = Shader.PropertyToID("_PoolMaxX");
        private static readonly int IdPoolTopY  = Shader.PropertyToID("_PoolTopY");
        private static readonly int IdPoolBot   = Shader.PropertyToID("_PoolBottomY");
        private static readonly int IdSurfTilt  = Shader.PropertyToID("_SurfTilt");
        private static readonly int IdHeightCnt = Shader.PropertyToID("_HeightCount");
        private static readonly int IdThreshold = Shader.PropertyToID("_Threshold");
        private static readonly int IdEdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int IdFoamColor = Shader.PropertyToID("_FoamColor");
        private static readonly int IdStreamColor = Shader.PropertyToID("_StreamColor");

        public MetaballFluid(RectTransform surface)
        {
            _surface = surface;
            var go = new GameObject("MetaballFluid", typeof(RectTransform));
            go.transform.SetParent(surface, false);
            _rt = (RectTransform)go.transform;
            // A centre-anchored viewport, sized to the vessel each frame (FitViewport) rather
            // than stretched over the whole pour surface: the metaball shader loops every blob
            // per pixel, so painting only the pixels the liquid can occupy is the big GPU win.
            _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = surface.rect.size;
            _rt.anchoredPosition = Vector2.zero;

            _image = go.AddComponent<RawImage>();
            _image.raycastTarget = false;

            // THE SHADER HAS TO SHIP (2026-09-11). It used to be found only by Shader.Find, and a
            // player build strips every shader no asset references — this one was referenced by
            // nothing, so outside the editor the fluid switched itself off and the game's main
            // mechanic drew no liquid at all. A material in Resources IS a reference, which is
            // what carries the shader into a build; each fluid clones it, because the arrays
            // below are written per instance. Shader.Find stays as the editor fallback only.
            var template = Resources.Load<Material>(MaterialPath);
            if (template != null)
                _material = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
            else
            {
                var shader = Shader.Find("LastCall/MetaballLiquid");
                if (shader != null)
                    _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (_material != null) _image.material = _material;
            // An error and not a warning: no liquid is a broken main mechanic, not a cosmetic gap.
            else Debug.LogError("MetaballFluid: no liquid material — Resources/" + MaterialPath
                                + " is missing and the shader was not found. Nothing will pour.");

            RefreshSize();
            SetColor(new Color(0.30f, 0.60f, 1.0f, 0.95f));
            SetStreamColor(new Color(0.30f, 0.60f, 1.0f, 0.95f));
            // The particles carry the whole body now — turn the shader's rectangular pool and
            // its height-field surface off (they stay in the shader for compatibility).
            _material?.SetFloat(IdHeightCnt, 0f);
            _material?.SetFloat(IdSurfTilt, 0f);
            // With the big render radius the field is dense; this threshold keeps it one smooth
            // connected body (no gaps between particles) with a flat surface, not separate blobs.
            _material?.SetFloat(IdThreshold, 0.7f);
            _material?.SetFloat(IdEdgeWidth, 0.10f);
            _material?.SetFloat("_StreamAlpha", StreamAlphaShare);
            _image.enabled = _material != null;
        }

        private void RefreshSize()
        {
            _size = _rt.rect.size;
            if (_size.x < 1f) _size.x = 1f;
            if (_size.y < 1f) _size.y = 1f;
            _material?.SetVector(IdSize, new Vector4(_size.x, _size.y, 0, 0));
        }

        /// <summary>
        /// Shrinks the drawn rect to just the region the liquid can occupy: the vessel, plus a
        /// margin for splashes and the column of falling stream above it. Cuts the shaded pixel
        /// count by roughly an order of magnitude versus covering the whole pour surface.
        /// </summary>
        private void FitViewport()
        {
            var surf = _surface.rect;
            float halfW = Mathf.Min(_halfW + StreamMargin, surf.width * 0.5f);
            float bottom = Mathf.Max(_cy - _halfH - SplashMargin, surf.yMin);
            float top = surf.yMax;                       // the stream falls in from above
            float cx = Mathf.Clamp(_cx, surf.xMin + halfW, surf.xMax - halfW);
            float cy = (bottom + top) * 0.5f;
            float h = Mathf.Max(top - bottom, 8f);

            var want = new Vector2(halfW * 2f, h);
            if ((_rt.sizeDelta - want).sqrMagnitude > 1f) _rt.sizeDelta = want;
            var pos = new Vector2(cx, cy);
            if ((_rt.anchoredPosition - pos).sqrMagnitude > 1f) _rt.anchoredPosition = pos;

            _originX = cx; _originY = cy;
            RefreshSize();
        }

        /// <summary>Surface-local px → the viewport's 0..1 uv (the viewport is offset now).</summary>
        private Vector2 ToUv(float x, float y) =>
            new Vector2((x - _originX) / _size.x + 0.5f, (y - _originY) / _size.y + 0.5f);

        /// <summary>
        /// The colour of the SETTLED drink. Alpha is honoured down to <see cref="AlphaFloor"/>:
        /// the clamp used to start at 0.82, which made every liquid in the game read as matte
        /// paint (the author, 2026-08-03: "sıvı renkleri mat olmamalı hiçbiri, saydam olmalı").
        /// The floor is what stops a near-clear spirit from disappearing altogether, so it is
        /// as low as it can be while a vodka in a lit glass still reads as something.
        /// </summary>
        public void SetColor(Color c)
        {
            if (_material == null) return;
            // The floor lifts a DRINK so a near-clear spirit cannot vanish. It must not lift
            // NOTHING: UITheme.Nothing asks for zero, and clamping that to 0.42 was silently
            // drawing an empty vessel as a pale cream film while the doc comment beside it
            // promised the opposite. Zero means zero; every other value is a drink.
            c.a = c.a <= 0f ? 0f : Mathf.Clamp(c.a, AlphaFloor, AlphaCeiling);
            _bodyAlpha = c.a;
            _material.SetColor(IdColor, c);
            // A stream nobody has named is the drink itself — a splash struck off a settled
            // pool, or the tail of a pour that has stopped. Without this the last thing poured
            // kept the slot, so vodka drops still in the air turned amaro-red the instant a
            // different bottle was tipped, and a splash wore whichever drink was poured last.
            if (!_streamNamed) _material.SetColor(IdStreamColor, c);
        }

        /// <summary>The colour of what is being POURED — the stream in the air, before it
        /// joins the drink. Where the stream owns the metaball field the shader draws this
        /// instead of the body colour, so a cola falling into a vodka is cola all the way
        /// down and becomes the mix where it lands.</summary>
        public void SetStreamColor(Color c)
        {
            if (_material == null) return;
            // THE STREAM IS AS OPAQUE AS THE LIQUID IT IS (2026-09-11). It used to take the
            // BODY's alpha — the drink already in the glass — which at the start of every pour
            // is a near-empty glass, so the stream fell at its faintest (measured: ~0.31 on the
            // first frame) exactly when the player is looking for it, and climbed back only as
            // the glass filled. It keeps its own, floored and capped as the body's is, and the
            // shader's share (StreamAlphaShare) is what makes air thinner than rest.
            c.a = c.a <= 0f ? 0f : Mathf.Clamp(c.a, AlphaFloor, AlphaCeiling);
            _streamNamed = true;
            _material.SetColor(IdStreamColor, c);
        }

        /// <summary>Forgets who was pouring: the stream slot goes back to following the body
        /// (see <see cref="SetColor"/>). Called when a pour ends, so drops still in the air
        /// belong to the drink rather than to the next bottle picked up.</summary>
        public void ClearStreamColor() => _streamNamed = false;

        // The floor lets a near-clear spirit stay near-clear. It was 0.42, chosen when every
        // drink shared one alpha range; the range is per-drink now (UITheme.DrinkAlpha reads
        // the drink's own pigment), so a vodka deliberately asks for 0.30 and the old floor
        // would have quietly made it as solid as a juice.
        // The floor rises with UITheme's own bands (2026-09-09): a fluid clamped at 0.26
        // under a drink table that now starts at 0.55 would be the one surface still
        // washing its colour out.
        private const float AlphaFloor = 0.50f, AlphaCeiling = 1.00f;
        private float _bodyAlpha = AlphaCeiling;
        private bool _streamNamed;

        /// <summary>
        /// The colour of the foam particles (GDD 21 §10). Beer and its head are one surface;
        /// only what they are made of differs, and this is that difference.
        ///
        /// This is the ONE colour setter with no alpha clamp, and that is deliberate: a head
        /// is a mass of bubbles with air in it, not a liquid you see through. It is the only
        /// thing in the game the 2026-08-03 "nothing should be matte" pass is meant to skip.
        /// </summary>
        public void SetFoamColor(Color c)
        {
            if (_material == null) return;
            _material.SetColor(IdFoamColor, c);
        }

        /// <summary>
        /// Sets the vessel interior the liquid lives in (surface-local px) and how full it is.
        /// The container is [minX,maxX]×[bottomY,rimY] rotated by <paramref name="angleRad"/>
        /// around its centre; the particle count tracks <paramref name="fillFrac"/>. Called
        /// every frame so the container follows the vessel and the liquid collides with it.
        /// </summary>
        /// <param name="headFrac">Foam riding on top of the beer, in the same glass-fractions as
        /// <paramref name="fillFrac"/> (GDD 21 §10 — head and beer share the glass). It is drawn
        /// as buoyant particles in this same body, never as a lid laid over it.</param>
        public void SetPool(float minX, float maxX, float bottomY, float rimY,
            float fillFrac, float angleRad = 0f, float headFrac = 0f)
        {
            float pcx = _cx, pcy = _cy;
            bool had = _poolSet;
            _cx = (minX + maxX) * 0.5f;
            _cy = (bottomY + rimY) * 0.5f;
            _halfW = Mathf.Max((maxX - minX) * 0.5f, 4f);
            _halfH = Mathf.Max((rimY - bottomY) * 0.5f, 4f);
            _angle = angleRad;
            fillFrac = Mathf.Clamp01(fillFrac);
            headFrac = Mathf.Clamp(headFrac, 0f, 1f - fillFrac);
            // The stream lands on whatever is on top — foam, if there is any — so the surface the
            // drops merge into is the top of the WHOLE body, not the beer line inside it.
            _fillTopY = bottomY + (rimY - bottomY) * (fillFrac + headFrac);
            FitParticleScale(rimY - bottomY);
            _poolSet = true;
            FitViewport();   // draw only over the vessel + its stream/splash margin

            // The vessel's motion enters the sim as an inertial force, not as walls teleporting
            // into the particles (which crushed the drink and shrank its volume while shaking).
            if (had)
            {
                // Shaking is felt as the vessel's ACCELERATION pushing back on the drink — the
                // same reason it climbs the wall when you snap the tin. Taken from real motion
                // in both axes, and clamped: a hand-shake is many g, and letting all of that
                // through simply crushed the particles together and stalled the frame.
                float h = Mathf.Max(Time.deltaTime, 1e-3f);
                float vxc = (_cx - pcx) / h, vyc = (_cy - pcy) / h;
                float ax = (vxc - _vcx) / h, ay = (vyc - _vcy) / h;
                _vcx = vxc; _vcy = vyc;
                float k = 1f - Mathf.Exp(-18f * h);   // smooth, so one jittery frame is not a kick
                _shakeAx = Mathf.Lerp(_shakeAx, Mathf.Clamp(-ax, -MaxShakeAccel, MaxShakeAccel), k);
                _shakeAy = Mathf.Lerp(_shakeAy, Mathf.Clamp(-ay, -MaxShakeAccel, MaxShakeAccel), k);
                _vesselSpeed = Mathf.Sqrt(vxc * vxc + vyc * vyc);
            }
            else { _shakeAx = _shakeAy = 0f; _vcx = _vcy = 0f; _vesselSpeed = 0f; }

            // Enough particles to fill the liquid AREA at the rest spacing — so they pack up to
            // the line, not into a puddle at the bottom. The area follows the vessel silhouette
            // (a narrow tin holds less), so a profiled vessel is not overfilled.
            // The usable area is the interior minus the render-radius inset on each wall, so the
            // count matches the space the particles are actually allowed to occupy.
            // BOTH insets, not one (2026-07-28): the particle centres stop that far short of the
            // floor AND of the surface, but the drawn iso-surface reaches back out past them at
            // each end. Counting it once left every vessel drawing a constant sliver high, which
            // is a constant no per-vessel multiplier can cancel — it flattered a half-full glass
            // and could not be told apart from a full one running short.
            // Beer fills to its own line; beer and foam together fill to the top of the body. Each
            // kind is given the particles its own slice of the vessel holds, so the head genuinely
            // takes the top of the glass instead of being painted over a full pint.
            int beerTarget = CountUpTo(fillFrac, bottomY, rimY, out float beerLineLocal);
            int totalTarget = CountUpTo(fillFrac + headFrac, bottomY, rimY, out _fillTopLocal);
            // A FULL GLASS HOLDS WHAT FITS (2026-09-11). The count is an area estimate, good to a
            // few units of level; below the rim that error just moves a free surface a little.
            // At the rim there is no free surface — the estimate's excess has nowhere to go but
            // into the particles already there, and a full highball's top fifth, 50 particles
            // over what its lattice holds, churned at 110 px/s while the body under it slept.
            // So no target may exceed the hexagonal lattice the cavity can actually hold.
            // Foam is counted off the ESTIMATE, before the cap: capping the beer first would hand
            // the difference to the head, and a full pint would grow froth it was never poured.
            // Foam is not bound by the cavity anyway — a head crowns over the rim.
            int foamTarget = Mathf.Clamp(
                Mathf.RoundToInt((totalTarget - beerTarget) * FoamCountScale), 0, MaxPool - beerTarget);
            if (_stacked)
            {
                int cap = LatticeCapacity();
                if (beerTarget > cap) beerTarget = cap;
            }

            bool seeding = _pn == 0 && totalTarget > 0;   // a fresh body, not a top-up
            Reconcile(KindBeer, beerTarget, beerLineLocal, seeding);
            Reconcile(KindFoam, foamTarget, _fillTopLocal, seeding);
        }

        /// <summary>Particles the vessel holds up to <paramref name="frac"/> of its interior
        /// height, and the container-local y that line sits at.</summary>
        private int CountUpTo(float frac, float bottomY, float rimY, out float topLocal)
        {
            frac = Mathf.Clamp01(frac);
            float h = Mathf.Max((rimY - bottomY) * frac - 2f * _pr * FaceOffset, 0f);
            topLocal = -_halfH + h;
            return Mathf.Clamp(
                Mathf.RoundToInt((2f * Mathf.Max(_halfW - _pr * SideOffset, 1f) * AverageProfile(frac))
                                 * h / (_sp * _sp * (_stacked ? StackedArea : PackedArea)) * _density),
                0, MaxPool);
        }

        /// <summary>
        /// Brings one kind's particle count to its target. Growth rains in at that kind's own
        /// surface line; shrinkage takes the HIGHEST particle of the kind, so a settling head
        /// comes off the top of the glass rather than tearing a hole through the middle of it.
        /// </summary>
        private void Reconcile(byte kind, int target, float lineLocal, bool seeding)
        {
            int have = kind == KindFoam ? _foamN : _pn - _foamN;
            // A FRESH DRINK IS LAID, NOT THROWN (2026-09-11). Seeding at random positions put
            // hundreds of overlapping pairs into the first frame, and the relaxation then had to
            // blow them apart — with the stack's shock passes that blast went all one way, up,
            // and a half glass popped to the rim. Laid on a hexagonal lattice a hair wider than
            // the rest spacing, a new body starts with nothing to push and simply settles.
            if (seeding && kind == KindBeer && _stacked) have += SeedLattice(target - have);
            // THE LEVEL RISES FROM THE POUR (2026-09-11). Growth used to rain in at random x
            // across the whole surface while the stream's own drops were deleted where they hit —
            // the glass filled from everywhere except the place the drink was going in. While a
            // stream is landing, new drink arrives at ITS column, moving down, which is a plunge.
            bool fromStream = !seeding && _lastLandAge < 0.35f;
            float spread = Mathf.Max(_halfW - _pr * SideOffset, 2f);
            while (have < target && _pn < MaxPool)
            {
                _px[_pn] = fromStream
                    ? Mathf.Clamp(_lastLandLx + Random.Range(-1.5f, 1.5f) * _sp, -spread, spread)
                    : Random.Range(-_halfW * 0.6f, _halfW * 0.6f);   // local frame
                _py[_pn] = seeding
                    ? Random.Range(-_halfH, Mathf.Max(lineLocal, -_halfH + 1f))
                    : lineLocal + Random.Range(-6f, 10f);
                _vx[_pn] = 0f; _vy[_pn] = seeding ? 0f : (fromStream ? -140f : -40f);
                _kind[_pn] = kind;
                _contact[_pn] = 0;
                if (kind == KindFoam) _foamN++;
                _pn++; have++;
            }
            while (have > target)
            {
                int top = -1;
                for (int i = 0; i < _pn; i++)
                    if (_kind[i] == kind && (top < 0 || _py[i] > _py[top])) top = i;
                if (top < 0) break;
                RemoveAt(top);
                have--;
            }
        }

        /// <summary>Drops one particle, filling its slot with the last live one.</summary>
        private void RemoveAt(int i)
        {
            int last = _pn - 1;
            if (_kind[i] == KindFoam) _foamN--;
            if (i != last)
            {
                _px[i] = _px[last]; _py[i] = _py[last];
                _vx[i] = _vx[last]; _vy[i] = _vy[last];
                _ppx[i] = _ppx[last]; _ppy[i] = _ppy[last];
                _qx[i] = _qx[last]; _qy[i] = _qy[last];
                _kind[i] = _kind[last];
                _contact[i] = _contact[last];
                _submerged[i] = _submerged[last];
            }
            _pn = last;
        }

        /// <summary>Mean silhouette width over the filled part of the vessel (0..fillFrac).</summary>
        private float AverageProfile(float fillFrac)
        {
            if (_profile == null || _profile.Length == 0 || fillFrac <= 0f) return 1f;
            const int steps = 8;
            float sum = 0f;
            for (int i = 0; i < steps; i++)
                sum += HalfWidthAt(fillFrac * (i + 0.5f) / steps, 1f);
            return sum / steps;
        }

        public void ClearPool() { _poolSet = false; _pn = 0; _foamN = 0; _wake = true; _restFrames = 0; }

        /// <summary>
        /// Where the drawn liquid actually ends, in surface space — taken from the particles
        /// rather than from the fill line they were aimed at. Anything that has to sit ON the
        /// drink (the head on a pint, GDD 21 §10) needs the surface it can see, not the one the
        /// pool was asked for: the two differ by the packing and the metaball iso-offset, and
        /// trusting the nominal line left a visible gap between the beer and its foam.
        /// </summary>
        public float SurfaceY(float fallback)
        {
            if (!_poolSet || _pn < 8) return fallback;
            float local = SurfaceLocalY(float.NaN);
            if (float.IsNaN(local)) return fallback;
            ToSurface(0f, local, out _, out float sy);
            return sy;
        }

        /// <summary>The same reading in the container's own frame — what a falling drop is
        /// measured against (2026-09-07: the landing used to be tested against the NOMINAL
        /// line, so drops popped their splash a row or two off the drawn crest).</summary>
        private float SurfaceLocalY(float fallbackLocal)
        {
            if (!_poolSet || _pn < 8) return fallbackLocal;

            // Not the highest particle: one droplet still falling through the neck sits well
            // above the body and dragged the reported surface up with it. Bin the particles by
            // height and walk down until a bin holds enough of them to be the drink itself.
            const int Bins = 48;
            for (int i = 0; i < Bins; i++) _surfaceBins[i] = 0;
            float span = _halfH * 2f;
            for (int i = 0; i < _pn; i++)
            {
                int b = (int)((_py[i] + _halfH) / span * Bins);
                if (b < 0) b = 0; else if (b >= Bins) b = Bins - 1;
                _surfaceBins[b]++;
            }

            // A bin is about one particle row deep, so 2% of the body was more than a full row
            // could hold and the surface was reported a row or two low every time.
            int need = Mathf.Max(3, _pn / 90);
            for (int b = Bins - 1; b >= 0; b--)
            {
                if (_surfaceBins[b] < need) continue;
                return (b + 1f) / Bins * span - _halfH;
            }
            return fallbackLocal;
        }

        private readonly int[] _surfaceBins = new int[48];

        /// <summary>Punches the surface near a local x — a pour landing or a knock.</summary>
        public void Ripple(float localX, float velImpulse)
        {
            _wake = true;
            float v = velImpulse * _size.y;
            for (int i = 0; i < _pn; i++)
                if (Mathf.Abs(_px[i] - localX) < _h && _py[i] > _fillTopLocal - _h)
                    _vy[i] -= v;
        }

        /// <param name="width">The stream's girth, 1 = the rest width (2026-09-07: a tin
        /// tipped right over pours a fatter rope than one just past the lip, and the drops
        /// scatter a little more the fatter it is).</param>
        public void EmitStream(Vector2 from, Vector2 vel, float dt, float width = 1f)
        {
            // A ROPE, NOT BEADS (2026-09-11). A node every 6 ms spaced them 0.006·v apart, so a
            // stream broke into beads the moment it sped up; and every drop got its own random
            // sideways kick, which frayed it. Now a node leaves each time the last one has gone a
            // fraction of its own radius, so the column stays joined, and the stream sways as ONE
            // thing on a slow wave — a real pour waves, it does not fray.
            width = Mathf.Clamp(width, 0.5f, 1.8f);
            float r = StreamRadius * width;
            float interval = Mathf.Clamp(StreamSpacing * r / Mathf.Max(vel.magnitude, 60f), 0.004f, 0.05f);
            _emitAccum = Mathf.Min(_emitAccum + dt, interval * 4f);   // a hitch never fires a burst
            _streamT += dt;
            int guard = 0;
            while (_emitAccum >= interval && guard++ < 6)
            {
                _emitAccum -= interval;
                // WHERE IT WOULD BE BY NOW. What is left in the accumulator after this node is
                // the time since it left the lip, so that is how far it has already fallen —
                // at v·τ, with the fall's own curve. This used to be interval − τ, which is the
                // same distance measured from the wrong end: the nodes sat unevenly, a frame that
                // let nothing go doubled the gap behind it, and once the stream sped up that gap
                // passed the width the field can bridge — the rope broke into lengths (measured:
                // six nodes 3-13 px apart, then 19-33 px of nothing).
                float tau = _emitAccum;
                var v = vel + new Vector2(9f * width * Mathf.Sin((_streamT - tau) * 2f * Mathf.PI * 2.6f), 0f);
                var pos = from + v * tau + new Vector2(0f, -0.5f * StreamGravity * tau * tau);
                SpawnDrop(pos, v + new Vector2(0f, -StreamGravity * tau), r, 3f, true);
            }
        }

        public void Splash(Vector2 at, float strength) => Splash(at, strength, 36f);

        /// <summary>
        /// A landing's spatter, kept INSIDE the vessel (2026-09-11). It used to fly ±150 px/s
        /// sideways for up to half a second — wider than the glass — and simply die wherever it
        /// was, which is drops landing outside the drink. It rises no higher than
        /// <paramref name="apex"/> (never over the rim), is held by the walls, and melts back into
        /// the drink when it falls to it.
        /// </summary>
        public void Splash(Vector2 at, float strength, float apex)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(1f + strength * 2f), 1, 4);
            float vyMax = Mathf.Sqrt(2f * StreamGravity * Mathf.Max(apex, 4f));
            for (int i = 0; i < n; i++)
            {
                SpawnDrop(at + new Vector2(Random.Range(-4f, 4f), 2f),
                    new Vector2(Random.Range(-90f, 90f), Random.Range(0.5f, 1f) * vyMax),
                    Random.Range(3f, 5.5f), 0.9f, false);
                int k = _lastSpawned;
                if (k >= 0) _drops[k].Entered = true;
            }
        }

        private int _lastSpawned = -1;

        private void SpawnDrop(Vector2 pos, Vector2 vel, float radius, float life, bool merges)
        {
            // The stream and the splash each search their OWN slots.
            int from = merges ? 0 : StreamMax, to = merges ? StreamMax : MaxDrops;
            int slot = -1;
            for (int i = from; i < to; i++) if (!_drops[i].Active) { slot = i; break; }
            _lastSpawned = slot;
            if (slot < 0) return;   // full: let the new drop go, never cull one mid-fall
            _drops[slot] = new Drop { Pos = pos, Vel = vel, Radius = radius, Life = life, Merges = merges, Active = true };
        }

        public void Step(float dt)
        {
            if (dt <= 0f) dt = 1e-4f;
            if (dt > 1f / 30f) dt = 1f / 30f;   // keep the solver stable on a hitch

            Resting = CanRest();
            if (!Resting)
            {
                using (MarkPool.Auto()) StepPool(dt);
                int awake = 0;
                for (int i = 0; i < _pn; i++)
                    if (_vx[i] != 0f || _vy[i] != 0f) awake++;
                Awake = awake;
                // AT REST IS NOT THE SAME AS ASLEEP. The sleep test zeroes any velocity under
                // SleepSpeed (30 px/s), but one frame of gravity only adds ~23 px/s — so a particle
                // falling freely from rest reports zero velocity EVERY frame while it creeps down.
                // Keyed to "every velocity is zero", the first version of this froze exactly those
                // particles in mid-air. What marks a body at rest is that nothing MOVED: a
                // supported particle's net move over the frame is zero, a falling one's is not.
                //
                // And the threshold follows the frame rate: one frame of free fall from rest is
                // g*dt^2 — 0.39 px at 60 fps but only 0.024 px at 240 — so a fixed number in px
                // would freeze falling particles again on a fast machine. A quarter of one
                // frame's free fall can never be mistaken for a fall.
                float restMove = Mathf.Min(RestMovePx, 0.25f * Gravity * dt * dt);
                _restFrames = _lastMaxMove < restMove ? _restFrames + 1 : 0;
                TakeRestSignature();
                _wake = false;
            }
            using (MarkDrops.Auto()) StepDrops(dt);
            using (MarkUpload.Auto()) Upload();
        }

        // ── REST (2026-09-11) ───────────────────────────────────────────────────
        //
        // A still, full glass was costing the whole solve every frame: 1,640 particles in the
        // 420 highball measured 11.7 ms per step STANDING STILL, 3.6x the 3.2 ms the solver was
        // tuned to. Nothing about a settled drink needs solving, so once every particle has
        // gone to sleep and nothing outside the solver has touched the body, the solve is
        // skipped outright and the particles keep exactly where they are.
        //
        // This is NOT the convergence early-out that was tried and removed above (a full vessel
        // never converges — the wall clamp re-introduces overlap each pass). It asks a different
        // question: not "did the passes settle" but "did every particle end the frame asleep,
        // twice running, with the vessel, the count and the constraints all unchanged". Any of
        // those moving — a pour topping it up, the glass carried, a ripple, a knock, a new
        // profile — wakes it for a full solve. A body that never falls fully asleep simply never
        // rests, which is today's behaviour and so costs nothing to have tried.
        //
        // It also makes a still drink STILL: the re-applied gravity used to nudge the packing a
        // fraction of a pixel every frame, which the look tests' byte-identical capture rule
        // cannot tolerate once a settled drink is on screen.

        /// <summary>Frames of the solve in a row that ended with every particle asleep.</summary>
        private int _restFrames;
        /// <summary>Something outside the solver touched the body since it last slept.</summary>
        private bool _wake = true;
        private int _sigPn = -1, _sigFoam = -1;
        private float _sigCx, _sigCy, _sigAngle, _sigTop;
        private const int RestAfterFrames = 2;
        /// <summary>The largest net move, in px, any particle may make in a frame for the body to
        /// count as at rest — capped further to a quarter of one frame's free fall (see Step).
        /// A free fall moves g*dt^2 a frame even while its velocity reads 0.</summary>
        private const float RestMovePx = 0.05f;
        private float _lastMaxMove = float.MaxValue;
        /// <summary>The largest net move any particle made in the last solve (px).</summary>
        public float LastMaxMove => _lastMaxMove;

        /// <summary>True for a frame the solve was skipped because the body is asleep.</summary>
        public bool Resting { get; private set; }
        /// <summary>Particles still moving after the last solve — 0 means the body slept.</summary>
        public int Awake { get; private set; }
        /// <summary>Live pool particles — for probes and the performance notes.</summary>
        public int PoolCount => _pn;

        private static readonly Unity.Profiling.ProfilerMarker MarkPool =
            new Unity.Profiling.ProfilerMarker("LastCall.Fluid.StepPool");
        private static readonly Unity.Profiling.ProfilerMarker MarkDrops =
            new Unity.Profiling.ProfilerMarker("LastCall.Fluid.StepDrops");
        private static readonly Unity.Profiling.ProfilerMarker MarkUpload =
            new Unity.Profiling.ProfilerMarker("LastCall.Fluid.Upload");

        private bool CanRest()
        {
            if (_wake || _pn == 0 || _restFrames < RestAfterFrames) return false;
            if (_pn != _sigPn || _foamN != _sigFoam) return false;
            if (Mathf.Abs(_cx - _sigCx) > 0.01f || Mathf.Abs(_cy - _sigCy) > 0.01f) return false;
            if (Mathf.Abs(_angle - _sigAngle) > 1e-4f || Mathf.Abs(_fillTopY - _sigTop) > 0.01f)
                return false;
            // The shake force decays by a lerp and never reaches zero exactly; under a pixel per
            // second squared against 1400 of gravity is nothing the drink can feel.
            return Mathf.Abs(_shakeAx) < 1f && Mathf.Abs(_shakeAy) < 1f && _vesselSpeed < 0.5f;
        }

        private void TakeRestSignature()
        {
            _sigPn = _pn; _sigFoam = _foamN;
            _sigCx = _cx; _sigCy = _cy; _sigAngle = _angle; _sigTop = _fillTopY;
        }

        // ── the position-based fluid step ───────────────────────────────────────
        private void StepPool(float dt)
        {
            if (_pn == 0) return;

            // World acceleration (gravity + the vessel's motion) rotated into the container's
            // frame: tilting the tin swings gravity across it, shaking throws the drink about.
            float c = Mathf.Cos(-_angle), sn = Mathf.Sin(-_angle);
            float wax = _shakeAx, way = -Gravity + _shakeAy;
            float accX = wax * c - way * sn;
            float accY = wax * sn + way * c;
            for (int i = 0; i < _pn; i++)
            {
                _ppx[i] = _px[i]; _ppy[i] = _py[i];
                // Foam is mostly air: it feels a fraction of the weight beer does, and a bubble
                // with beer around it is pushed the other way entirely.
                float g = 1f;
                if (_kind[i] == KindFoam)
                {
                    float buried = Mathf.Clamp01(_submerged[i] / FoamFullLiftAt);
                    g = FoamGravity - FoamBuoyancy * buried;
                }
                _vx[i] += accX * g * dt; _vy[i] += accY * g * dt;
                _px[i] += _vx[i] * dt; _py[i] += _vy[i] * dt;
                _qx[i] = _px[i]; _qy[i] = _py[i];   // predicted, before the constraints
            }

            // Gravity's own direction in the container's frame — the axis foam rises along and
            // beer sinks along. Taken from gravity alone, not from accX/accY, so a knock to the
            // glass shakes the head about without turning the stack upside down.
            float upX = -sn, upY = c;

            // Incompressibility: relax a minimum-distance constraint a few passes — no two
            // particles closer than Spacing — so the body packs up to the fill line and never
            // collapses. The vessel walls are re-applied between passes so the liquid stays in.
            // A vessel being thrown about relaxes fewer times (2026-07-28). The passes buy an
            // accurate settled LEVEL, and mid-slosh nobody is reading the level — so the case
            // that used to cost the most now costs the least, which is the way round it should
            // have been. (An early-out on convergence was tried and removed: a full vessel never
            // converges, because the wall clamp re-introduces overlap after every pass, so it
            // never once fired.)
            int maxIters = _vesselSpeed > 40f ? ShakeRelaxIters : RelaxIters;
            float minD = _sp, minD2 = minD * minD;

            // BOTTOM UP, AND THE BOTTOM HOLDS (2026-09-11). A still drink is a STACK, and a stack
            // relaxed by splitting every overlap half-and-half never finishes settling: each
            // pass pushes the lower particle down as far as it pushes the upper one up, so the
            // weight never reaches the floor and the column is re-compressed every frame. On
            // the full 420 highball that left the bottom fifth of the body churning at 155 px/s.
            // Two standard remedies, used together and only in a still vessel:
            //   * the particles are visited from the BOTTOM of the gravity axis up, so a pass
            //     carries support up the column instead of across it;
            //   * the last passes use SHOCK PROPAGATION: between two drink particles one above
            //     the other, only the UPPER one moves, by the whole overlap — the lower one is
            //     ground. The column then resolves from the floor up, and the floor stops moving.
            // A shaken or carried vessel keeps the plain symmetric passes: there the drink is
            // meant to be thrown about, and a rigid stack would fight the slosh.
            bool stillVessel = _stacked && _vesselSpeed <= 40f;
            int shockFrom = stillVessel ? maxIters - ShockPasses : int.MaxValue;
            if (stillVessel) SortBottomUp(upX, upY);
            float shockTop = stillVessel ? _topKey - 2.5f * _sp : float.MinValue;
            for (int iter = 0; iter < maxIters; iter++)
            {
                bool shock = iter >= shockFrom;
                BuildGrid();   // O(N) neighbour lookup — keeps a fine particle scale affordable
                // Only the FORWARD half of the neighbourhood (2026-07-28). Scanning all nine
                // cells and throwing away half the pairs with `j <= i` walked every pair twice
                // to use it once, and this loop is ~80% of the fluid's frame. These four cells
                // plus this one still meet every neighbouring pair exactly once: a pair that
                // straddles two cells is found from the backward one, a pair inside a cell by
                // taking only j > i.
                for (int oi = 0; oi < _pn; oi++)
                {
                    int i = stillVessel ? _order[oi] : oi;
                    int cx = CellOf(_px[i]), cy = CellOf(_py[i]);
                    int seen = 0;
                    float pxi = _px[i], pyi = _py[i];
                    for (int s = 0; s < 5; s++)
                    {
                        int j = _cellHead[HashCell(cx + StencilX[s], cy + StencilY[s])];
                        for (; j >= 0 && seen < MaxNeighbours; j = _next[j])
                        {
                            seen++;
                            if (s == 0 && j <= i) continue;   // own cell: each pair once
                            float dx = _px[j] - pxi, dy = _py[j] - pyi;
                            float r2 = dx * dx + dy * dy;
                            if (r2 >= minD2 || r2 < 1e-4f) continue;
                            float r = Mathf.Sqrt(r2);
                            float push = (minD - r) * 0.5f;   // no over-relaxation: >1 pumped energy into the fluid
                            // Froth is a heap of unlike bubbles, not a lattice: relaxing foam
                            // against foam only part way leaves the packing irregular, which is
                            // what gives the head a lumpy crest instead of a planed one.
                            if (_kind[i] == KindFoam && _kind[j] == KindFoam) push *= FoamSlack;
                            // Only INSIDE the body. The top rows stay soft: a stack held rigid all
                            // the way up had nowhere to put the load but the ceiling of a full
                            // glass, and its surface fifth churned at 230 px/s against it.
                            if (shock && _kind[i] == KindBeer && _kind[j] == KindBeer
                                && pxi * upX + pyi * upY < shockTop
                                && _px[j] * upX + _py[j] * upY < shockTop)
                            {
                                // Ground and load: the pair's whole overlap goes to whichever of
                                // the two sits higher along gravity. A side-by-side pair (less
                                // than ~20 degrees off level) is not a stack, and splits as usual.
                                float along0 = dx * upX + dy * upY;         // >0: j is above i
                                if (along0 > r * 0.35f)
                                {
                                    float full = minD - r;
                                    _px[j] += dx / r * full; _py[j] += dy / r * full;
                                    continue;
                                }
                                if (along0 < -r * 0.35f)
                                {
                                    float full = minD - r;
                                    pxi -= dx / r * full; pyi -= dy / r * full;
                                    continue;
                                }
                            }
                            float nx = dx / r * push, ny = dy / r * push;
                            pxi -= nx; pyi -= ny;
                            _px[j] += nx; _py[j] += ny;

                            // Beer and foam do not merely avoid each other, they SORT. The plain
                            // minimum-distance constraint above is symmetric and cannot tell them
                            // apart, so an overlapping unlike pair also EXCHANGES along gravity
                            // until the foam is the one on top.
                            //
                            // The exchange is driven by how badly the pair is out of order, not by
                            // how much it overlaps, and that distinction is the whole thing. Driven
                            // by overlap it kept pushing after the two had sorted themselves and
                            // levered the layers apart — leaving a gap the metaball field could not
                            // bridge, which drew as black holes through the drink. Driven by the
                            // mis-ordering it falls to nothing the instant the foam is above the
                            // beer, so the layers sort and then stay in contact (2026-07-30).
                            if (_kind[i] != _kind[j])
                            {
                                float along = dx * upX + dy * upY;      // >0: j sits above i
                                float si = _kind[i] == KindFoam ? 1f : -1f;
                                float mis = along * si;                 // >0: the beer is on top
                                if (mis > 0f)
                                {
                                    float ex = mis * 0.5f * FoamSort;
                                    pxi += upX * ex * si; pyi += upY * ex * si;
                                    _px[j] -= upX * ex * si; _py[j] -= upY * ex * si;
                                }
                            }
                        }
                    }
                    _px[i] = pxi; _py[i] = pyi;
                }
                ClampToVessel();
            }

            // Cap how far the constraints may move a particle in one frame. Without this the
            // correction feeds straight back into velocity, which throws particles into the
            // walls, packs them on top of each other (the drink visibly loses volume) and
            // stuffs the grid cells so the neighbour sweep — and the frame — blows up.
            bool moving = _vesselSpeed > 40f;
            float damp = moving ? ShakeDamping : RestDamping;
            // THE BOTTOM OF A STILL GLASS DOES NOT MOVE (2026-09-11). Measured on the full 420
            // highball, settled for six seconds: the bottom fifth of the body averaged 155 px/s
            // and the surface fifth 16. A still drink's floor has no business moving at all —
            // what moved there was the solver: gravity presses the stack every frame, the
            // passes cannot resolve a tall column completely, and the correction left over is
            // largest where the weight is largest, at the bottom, where it turns into velocity
            // and comes back as next frame's overlap. Damping by DEPTH breaks that loop where it
            // lives and nowhere else: the top of the drink keeps every bit of its freedom to
            // ripple, take a stream and slosh, and a vessel being moved or shaken is exempt.
            // Per SECOND, not per frame, so it holds at any frame rate.
            float deepKeep = Mathf.Pow(DeepDamping, 60f * dt);
            float maxMove2 = 0f;
            float depthSpan = Mathf.Max(_fillTopLocal + _halfH, 1f);
            float maxCorr = _sp * 4f, maxCorr2 = maxCorr * maxCorr;
            for (int i = 0; i < _pn; i++)
            {
                float cxd = _px[i] - _qx[i], cyd = _py[i] - _qy[i];
                float m2 = cxd * cxd + cyd * cyd;
                if (m2 > maxCorr2)
                {
                    float sc = maxCorr / Mathf.Sqrt(m2);
                    _px[i] = _qx[i] + cxd * sc; _py[i] = _qy[i] + cyd * sc;
                }
            }
            ClampToVessel(true);

            // Velocity from the net move (this is what carries a moving/tilting vessel into the
            // liquid — the slosh), speed-capped.
            for (int i = 0; i < _pn; i++)
            {
                // Velocity is the net move over the frame. This is the property that lets the
                // drink come to rest at all: a particle held by the floor or its neighbours
                // barely moves, so its velocity falls to zero instead of being re-integrated.
                float mvx = _px[i] - _ppx[i], mvy = _py[i] - _ppy[i];
                float mv2 = mvx * mvx + mvy * mvy;
                if (mv2 > maxMove2) maxMove2 = mv2;
                _vx[i] = mvx / dt;
                _vy[i] = mvy / dt;
                // THE FLOOR STOPS A DROP, IT DOES NOT THROW IT (2026-09-11). Velocity is the net
                // move, and the net move of a particle the floor just pushed back up INCLUDES that
                // push — so the floor launched it. The glass's floor is the near arc of an
                // ellipse, 1 - sqrt(1 - u^2), whose slope runs to infinity at the walls: a
                // particle sliding into a bottom corner was thrown up that near-vertical arc,
                // fell, and was thrown again. Measured on the full 420 highball: the bottom fifth
                // of the body averaged 116 px/s while the surface averaged 16 — the drink was
                // boiling from the floor up. A boundary may stop motion INTO it; it may not add
                // motion OUT of it. Tangential motion is untouched, so the drink still runs down
                // a wall and sloshes along the floor. In the container frame, so it holds tilted.
                if (_stacked && !moving)
                {
                    float depth = (_fillTopLocal - _py[i]) / depthSpan;   // 0 surface, 1 floor
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.62f, depth));
                    if (k > 0f)
                    {
                        float keep = Mathf.Lerp(1f, deepKeep, k);
                        _vx[i] *= keep; _vy[i] *= keep;
                    }
                }
                byte ct = _stacked ? _contact[i] : (byte)0;
                if ((ct & 1) != 0 && _vy[i] > 0f) _vy[i] = 0f;
                if ((ct & 2) != 0 && _vx[i] > 0f) _vx[i] = 0f;
                if ((ct & 4) != 0 && _vx[i] < 0f) _vx[i] = 0f;
                // And the CEILING. A full glass is filled to its pool ceiling by definition, so
                // its top row lives against that clamp — which pushed it back down every pass and
                // threw it into the body below: the top fifth of a full highball churned at
                // 110-440 px/s while the rest of the body slept.
                if ((ct & 8) != 0 && _vy[i] < 0f) _vy[i] = 0f;
                // Damping is what lets the drink go still — but applied while you are shaking it
                // it just swallows the slosh. So it is light in a moving tin, strong in a still
                // one, and strongest of all on foam, which is thick and does not ring.
                float d = moving ? damp : (_kind[i] == KindFoam ? FoamDamping : damp);
                _vx[i] *= d; _vy[i] *= d;
                float sp2 = _vx[i] * _vx[i] + _vy[i] * _vy[i];
                float sleep = _vesselSpeed > 40f ? 0f : SleepSpeed;   // a moving tin never sleeps
                if (sp2 < sleep * sleep) { _vx[i] = 0f; _vy[i] = 0f; }
                else if (sp2 > MaxSpeed * MaxSpeed) { float s = MaxSpeed / Mathf.Sqrt(sp2); _vx[i] *= s; _vy[i] *= s; }
            }
            _lastMaxMove = Mathf.Sqrt(maxMove2);
            BuildGrid();   // positions moved during relaxation — refresh before the neighbour blend
            ApplyViscosity();
        }

        /// <summary>
        /// The vessel's half-width at a height, <paramref name="t"/> running 0 (floor) → 1 (rim).
        /// Without a profile the vessel is a plain box; with one, the liquid follows the real
        /// silhouette — narrow at a shaker's neck, flared at a tumbler's mouth.
        /// </summary>
        private float HalfWidthAt(float t, float ix)
        {
            if (_profile == null || _profile.Length == 0) return ix;
            float f = Mathf.Clamp01(t) * (_profile.Length - 1);
            int i0 = Mathf.FloorToInt(f);
            int i1 = Mathf.Min(i0 + 1, _profile.Length - 1);
            // Never squeeze below MinProfile: the art's rounded base pinches to a slot barely
            // wider than a particle, which both walled the drink out of the bottom corners and
            // fired particles back out of the gap — a permanent source of the jitter.
            float w = Mathf.Max(Mathf.Lerp(_profile[i0], _profile[i1], f - i0), MinProfile);
            return ix * w;
        }

        /// <summary>Sets the vessel silhouette: half-width multipliers sampled bottom → rim.
        /// Pass null for a plain rectangular interior.</summary>
        public void SetProfile(float[] halfWidths)
        {
            if (!ReferenceEquals(_profile, halfWidths)) _wake = true;   // new walls: re-solve
            _profile = halfWidths;
        }

        /// <summary>
        /// Per-vessel correction on how many particles a given fill asks for. The estimate draws
        /// the tin and the pint at their own level across the whole range, so both leave this at
        /// 1; only the tumbler still runs generous in the middle of its range — it is much the
        /// shortest cavity, so what is left of the inset error is a bigger share of it — and it
        /// asks for a tenth fewer. Measured 2026-07-28 at four fills, live in each stage.
        /// </summary>
        public void SetDensity(float multiplier)
        {
            float d = Mathf.Clamp(multiplier, 0.25f, 4f);
            if (d != _density) _wake = true;
            _density = d;
        }
        private float _density = 1f;

        /// <summary>THE FLOOR IS AN ARC (2026-09-07, the author: "tabanı yay şeklinde değil").
        /// How much higher the floor stands at the walls than mid-vessel, in surface px: the
        /// near arc of the floor's ellipse, the same curve the fill mask is cut to. The clamp
        /// used to be one flat line across the whole width at the arc's LOWEST row, so the
        /// drink's corners hung below the glass's floor by the depth of the arc.</summary>
        public void SetFloorArc(float px)
        {
            // Called every frame by the tap; only a CHANGED floor may wake a sleeping body.
            float a = Mathf.Max(0f, px);
            if (a != _floorArc) _wake = true;
            _floorArc = a;
        }
        private float _floorArc;

        /// <summary>Clamps every particle inside the rotated vessel interior (profile-shaped).</summary>
        private void ClampToVessel(bool record = false)
        {
            // Local frame: the walls are axis-aligned here, so this is a straight compare —
            // no rotation per particle per iteration (the old hot path).
            // Inset by however far the drawn iso-surface actually reaches past a particle centre
            // — measured against this kernel and threshold, not guessed: 0.27r out to the side
            // (where the wall cuts a packed column) and 0.53r above a free surface. Holding the
            // centres exactly that far in makes the DRAWN liquid meet the vessel wall, so it
            // covers the whole interior without bleeding out of it.
            float ix = Mathf.Max(_halfW - _pr * SideOffset, 2f);
            float iy = Mathf.Max(_halfH - _pr * FaceOffset, 2f);
            for (int i = 0; i < _pn; i++)
            {
                // Foam may stand proud of the rim — a head crowns over the glass — and each bubble
                // gets its OWN ceiling. A single shared one is a hard clamp applied after every
                // relaxation pass, so every particle that tried to rise was slammed to exactly the
                // same y: the flat top edge was not the packing settling, it was this line drawing
                // a ruler across the head (measured 2026-07-30).
                //
                // Capping foam at the DRINK's surface instead was tried and reverted: it gave beer
                // a higher ceiling than foam, so beer thrown above that line by a hard swing could
                // never be displaced back down — the pint inverted and stayed inverted.
                float ceil = _kind[i] == KindFoam
                    ? iy + FoamCrown * (0.30f + 0.70f * (i * 0.6180339f % 1f))
                    : iy;
                float ly = _py[i];
                float lx = _px[i];
                // The floor at THIS column: deepest in the middle, rising to the arc's height
                // at the walls (the near arc of the floor's ellipse, seen from a little above).
                float floorHere = -iy;
                if (_floorArc > 0f)
                {
                    float u = Mathf.Clamp(lx / Mathf.Max(ix, 1f), -1f, 1f);
                    floorHere += _floorArc * (1f - Mathf.Sqrt(1f - u * u));
                }
                byte hit = 0;
                if (ly < floorHere) { ly = floorHere; hit |= 1; } else if (ly > ceil) { ly = ceil; hit |= 8; }
                float w = HalfWidthAt((ly + iy) / (2f * iy), ix);   // the wall at this height
                if (ly > iy)
                {
                    // The crown DOMES (the author, 2026-08-02: the head poked past the
                    // mouth's corners — "bira bardağı taşıyor"). Above the rim there is
                    // no wall to hold a cylinder of foam: it narrows as it rises, and it
                    // gives back the extra iso reach its fatter render radius has over
                    // the pool particles the wall insets were measured for.
                    w = w * (0.92f - 0.35f * (ly - iy) / FoamCrown)
                        - (_fr - _pr) * SideOffset;
                }
                if (lx < -w) { lx = -w; hit |= 2; } else if (lx > w) { lx = w; hit |= 4; }
                _px[i] = lx; _py[i] = ly;
                if (record) _contact[i] = hit;
            }
        }

        /// <summary>Container-local → surface-local px (for rendering and drop tests).</summary>
        private void ToSurface(float lx, float ly, out float sx, out float sy)
        {
            float c = Mathf.Cos(_angle), s2 = Mathf.Sin(_angle);
            sx = _cx + lx * c - ly * s2;
            sy = _cy + lx * s2 + ly * c;
        }

        /// <summary>Blends each particle's velocity toward its neighbours' — the liquid flows as
        /// one body instead of rattling as loose grains. Grid-accelerated.</summary>
        private void ApplyViscosity()
        {
            float h2 = _h * _h;
            // Gravity's direction in the container's frame, so "above" means above in the world
            // even when the glass is laid over.
            float gc = Mathf.Cos(-_angle), gs = Mathf.Sin(-_angle);
            float upX = -gs, upY = gc;
            for (int i = 0; i < _pn; i++)
            {
                float avx = 0f, avy = 0f; int n = 0, same = 0, beerN = 0;
                byte ki = _kind[i];
                int cx = CellOf(_px[i]), cy = CellOf(_py[i]);
                for (int gy = cy - ViscCellR; gy <= cy + ViscCellR; gy++)
                    for (int gx = cx - ViscCellR; gx <= cx + ViscCellR; gx++)
                        for (int j = _cellHead[HashCell(gx, gy)]; j >= 0; j = _next[j])
                        {
                            if (j == i) continue;
                            float dx = _px[j] - _px[i], dy = _py[j] - _py[i];
                            if (dx * dx + dy * dy < h2)
                            {
                                n++;
                                // Beer, and above me: the only neighbour that has to be escaped.
                                if (_kind[j] == KindBeer && dx * upX + dy * upY > 0f) beerN++;
                                // Only LIKE sticks to like. Blending a bubble's velocity toward
                                // the beer around it erased the very rise buoyancy had just given
                                // it, so a head stirred into the beer could never climb back out
                                // — 22 of 32 bubbles stayed buried after a thrashing (measured
                                // 2026-07-30). Foam coheres with foam and slides past beer, which
                                // is both what froth does and what the head needs to do.
                                if (_kind[j] == ki) { avx += _vx[j]; avy += _vy[j]; same++; }
                            }
                        }
                // How buried this particle is, for next frame's buoyancy. Free: the neighbours
                // were already gathered for the viscosity blend.
                _submerged[i] = ki == KindFoam && n > 0 ? (float)beerN / n : 0f;
                if (same == 0) continue;
                // Averaging neighbour velocities is what holds the drink together — but it is
                // also what erases the slosh, so a moving tin gets much less of it. Foam is
                // stickier than beer: it slumps as one mass instead of scattering.
                float visc = _vesselSpeed > 40f ? ShakeViscosity
                           : ki == KindFoam ? FoamViscosity : Viscosity;
                _vx[i] = Mathf.Lerp(_vx[i], avx / same, visc);
                _vy[i] = Mathf.Lerp(_vy[i], avy / same, visc);
            }
        }

        // ── spatial hash grid ───────────────────────────────────────────────────
        private int CellOf(float v) => Mathf.FloorToInt(v / _cell);

        private static int HashCell(int gx, int gy)
        {
            // A cheap 2D hash folded into the bucket range (power-of-two mask).
            unchecked { return ((gx * 73856093) ^ (gy * 19349663)) & (GridBuckets - 1); }
        }

        /// <summary>Rebuilds the neighbour grid from the current positions (O(N)).</summary>
        private void BuildGrid()
        {
            for (int i = 0; i < GridBuckets; i++) _cellHead[i] = -1;
            for (int i = 0; i < _pn; i++)
            {
                int h = HashCell(CellOf(_px[i]), CellOf(_py[i]));
                _next[i] = _cellHead[h];
                _cellHead[h] = i;
            }
        }

        private void StepDrops(float dt)
        {
            // The viewport is offset from the surface origin now, so the kill line has to be
            // measured from its centre — otherwise drops die (and vanish) at the wrong height.
            float floor = _originY - _size.y * 0.5f - 30f;
            // WHERE THE DRINK ACTUALLY IS (2026-09-07): the drawn surface, read once a step,
            // so a drop melts in at the crest it can be seen hitting — and splashes there.
            float land = _poolSet ? SurfaceLocalY(_fillTopLocal) : _fillTopLocal;
            float wix = Mathf.Max(_halfW - _pr * SideOffset, 2f);
            float wiy = Mathf.Max(_halfH - _pr * FaceOffset, 2f);
            float mouthHalf = _poolSet ? HalfWidthAt(1f, wix) + 2f : 0f;
            // A splash rises no higher than half the air left in the glass, and never over 36 px.
            float apex = Mathf.Min(0.5f * Mathf.Max(_halfH - land, 0f), 36f);
            _lastLandAge += dt;
            for (int i = 0; i < MaxDrops; i++)
            {
                if (!_drops[i].Active) continue;
                ref Drop d = ref _drops[i];
                d.Vel.y -= StreamGravity * dt;
                d.Pos += d.Vel * dt;
                d.Life -= dt;

                // A vessel that draws nothing inside (the steel tin under its brim) swallows the
                // stream at its mouth instead of letting it fall through the metal.
                if (d.Merges && !_poolSet && _sinkSet
                    && d.Pos.y <= _sinkY && Mathf.Abs(d.Pos.x - _sinkX) <= _sinkHalf)
                {
                    d.Active = false;
                    StreamSwallowed++;
                    continue;
                }

                if (_poolSet)
                {
                    // Into the container's frame.
                    float c = Mathf.Cos(-_angle), s2 = Mathf.Sin(-_angle);
                    float ox = d.Pos.x - _cx, oy = d.Pos.y - _cy;
                    float lx = ox * c - oy * s2, ly = ox * s2 + oy * c;

                    // IN THROUGH THE MOUTH, THEN HELD BY THE GLASS (2026-09-11). A drop crossing
                    // the rim inside the mouth is in the drink's vessel from then on, and its walls
                    // hold it: a stream that comes in near a wall runs down it instead of passing
                    // through the glass and out onto the counter.
                    if (!d.Entered && ly < _halfH && ly > _halfH - 40f && Mathf.Abs(lx) <= mouthHalf)
                        d.Entered = true;
                    if (d.Entered && ly < _halfH)
                    {
                        float w = HalfWidthAt((ly + wiy) / (2f * wiy), wix);
                        if (Mathf.Abs(lx) > w)
                        {
                            float side = Mathf.Sign(lx);
                            lx = side * w;
                            // local velocity; the component into the wall goes (a splash bounces a little)
                            float cb = Mathf.Cos(-_angle), sb = Mathf.Sin(-_angle);
                            float lvx = d.Vel.x * cb - d.Vel.y * sb, lvy = d.Vel.x * sb + d.Vel.y * cb;
                            if (lvx * side > 0f) lvx = d.Merges ? 0f : -0.4f * lvx;
                            float ca = Mathf.Cos(_angle), sa = Mathf.Sin(_angle);
                            d.Vel = new Vector2(lvx * ca - lvy * sa, lvx * sa + lvy * ca);
                            ToSurface(lx, ly, out float wx, out float wy);
                            d.Pos = new Vector2(wx, wy);
                        }
                    }

                    // A splash that falls back to the drink goes back into it.
                    if (!d.Merges && d.Entered && ly <= land && d.Vel.y < 0f)
                    {
                        d.Active = false;
                        continue;
                    }
                }

                // A stream drop that reaches the liquid surface inside the vessel melts in.
                if (d.Merges && _poolSet)
                {
                    // Into the container's frame: has it reached the liquid line inside the tin?
                    float c = Mathf.Cos(-_angle), s2 = Mathf.Sin(-_angle);
                    float ox = d.Pos.x - _cx, oy = d.Pos.y - _cy;
                    float lx = ox * c - oy * s2, ly = ox * s2 + oy * c;
                    float wHere = HalfWidthAt(Mathf.Clamp01((land + wiy) / (2f * wiy)), wix) + 2f;
                    if (ly <= land + 6f && Mathf.Abs(lx) < wHere)
                    {
                        _lastLandLx = lx; _lastLandAge = 0f;
                        StreamLanded++;
                        // A drop that has fallen further hits harder: more spatter, a deeper
                        // punch in the surface. What makes a pour READ as a pour (2026-09-07,
                        // the author: "akışkanlığı ve dökülme hissiyatını").
                        float k = Mathf.Clamp01((d.Vel.magnitude - 180f) / 520f);
                        if (Random.value < 0.30f + 0.30f * k)
                        {
                            ToSurface(lx, land, out float hx, out float hy);
                            Splash(new Vector2(hx, hy), 0.3f + 0.6f * k, apex);
                        }
                        Ripple(lx, 0.010f + 0.012f * k);
                        d.Active = false;
                        continue;
                    }
                }
                if (d.Life <= 0f || d.Pos.y < floor)
                {
                    d.Active = false;
                    if (d.Merges) StreamLost++; else SplashLost++;
                }
            }
        }

        private void Upload()
        {
            if (_material == null) return;
            int count = 0;
            // A shaken drink spreads out, and spread particles thin the metaball field between
            // them — which reads as the drink losing volume. Give each one a little more reach
            // while the tin is moving so the body stays as solid as it is when it is still.
            float r = _vesselSpeed > 40f ? _pr * 1.18f : _pr;
            for (int i = 0; i < _pn && count < RenderMax; i++)
            {
                ToSurface(_px[i], _py[i], out float sx, out float sy);
                var uv = ToUv(sx, sy);
                // Foam draws bigger and unevenly, and flags itself in w so the shader can colour
                // it: the golden ratio gives each bubble a stable size that does not march in
                // step with its neighbours, so the crest breaks into rounds.
                bool foam = _kind[i] == KindFoam;
                float rr = foam ? _fr * (0.80f + 0.40f * (i * 0.7548777f % 1f)) : r;
                _dropData[count++] = new Vector4(uv.x, uv.y, rr, foam ? 2f : 1f);
            }
            for (int i = 0; i < MaxDrops && count < RenderMax; i++)
            {
                if (!_drops[i].Active) continue;
                var uv = ToUv(_drops[i].Pos.x, _drops[i].Pos.y);
                // Flagged 3: a drop still in the AIR. The shader draws it thinner than the
                // settled body — a stream has nothing behind it, where the drink in a glass is
                // read through a translucent wall, and drawing both at one alpha made the pour
                // read as paint (the author, 2026-08-03). It fills in to solid by itself as
                // the pool's own particles take over the field where it lands.
                _dropData[count++] = new Vector4(uv.x, uv.y, _drops[i].Radius, 3f);
            }
            for (int i = count; i < RenderMax; i++) _dropData[i] = Vector4.zero;

            _material.SetFloat(IdDropCount, count);
            _material.SetVectorArray(IdDrops, _dropData);
            // The rectangular pool contributes NOTHING to the field — the particles are the
            // body — so its width and floor stay collapsed.
            _material.SetFloat(IdPoolMinX, 0f); _material.SetFloat(IdPoolMaxX, 0f);
            _material.SetFloat(IdPoolBot, 2f);

            // _PoolTopY is a second thing: the shader reads it as the LIQUID LINE for the
            // surface band and the sheen through the body. It was pinned at 2.0 — off the top
            // of a 0..1 uv — which silently killed both terms, so the drink had no light on
            // its surface and no wet gradient at all, while the shader's own comment promised
            // "it reads as wet volume". That deadness is a real part of why a drink read as a
            // flat wash. Fed from the particles' own measured surface, so the band rides the
            // drink rather than a nominal line, and it goes off-screen again only when there
            // is no body to light.
            if (_poolSet && _pn >= 8)
            {
                float surfaceLocal = SurfaceY(float.NaN);
                _material.SetFloat(IdPoolTopY, float.IsNaN(surfaceLocal)
                    ? 2f : ToUv(0f, surfaceLocal).y);
            }
            else _material.SetFloat(IdPoolTopY, 2f);
        }

        public void Clear()
        {
            for (int i = 0; i < MaxDrops; i++) _drops[i].Active = false;
            _pn = 0; _foamN = 0; _emitAccum = 0f;
            _wake = true; _restFrames = 0;
            Upload();
        }

        public void SetActive(bool on)
        {
            if (_image != null && _material != null) _image.enabled = on;
        }
    }
}
