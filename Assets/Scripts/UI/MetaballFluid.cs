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
        // Render budget — pool particles + free stream/splash drops share the shader's _Drops[].
        private const int MaxPool = 2040;
        private const int MaxDrops = 110;
        private const int RenderMax = 2176;   // must match MAX_DROPS in the shader

        private const float Gravity = 1400f;          // px/s² down
        private const float StreamRadius = 4f;
        private const float StreamInterval = 0.006f;

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
        /// is a different (and unreachable in play) density.
        /// </summary>
        private const float PackedArea = 0.71f;
        // Render radius is well above the spacing so the fine, tightly-packed particles
        // overlap into ONE smooth connected surface with no gaps between them.
        private const float PoolRadius = 7.5f;
        private const float SideOffset = 0.27f;   // iso-surface reach past a side wall particle
        private const float FaceOffset = 0.53f;   // iso-surface reach past a floor/surface particle
        private const float Viscosity = 0.42f;        // 0..1 neighbour-velocity blend (more flow)
        private const float MaxSpeed = 1300f;
        private const float RestDamping = 0.94f;
        private const float ShakeDamping = 0.995f;   // barely damped while the tin is moving
        private const float ShakeViscosity = 0.22f;  // freer to move, but still one body         // bleeds off the energy the solver adds
        private const float SleepSpeed = 30f;
        private const int MaxNeighbours = 96;   // generous: a weak constraint is what let the clump form
        private const float MinProfile = 0f;      // the interior is shaped by the profile alone now             // below this a particle is simply at rest
        private const float WallFriction = 0.72f;     // (kept for API parity)

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
        private static readonly int ViscCellR = Mathf.CeilToInt(H / Cell);

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
        }
        private readonly Drop[] _drops = new Drop[MaxDrops];
        private readonly Vector4[] _dropData = new Vector4[RenderMax];
        private float _emitAccum;

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

            var shader = Shader.Find("LastCall/MetaballLiquid");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _image.material = _material;
            }
            else Debug.LogWarning("MetaballFluid: shader 'LastCall/MetaballLiquid' not found.");

            RefreshSize();
            SetColor(new Color(0.30f, 0.60f, 1.0f, 0.95f));
            // The particles carry the whole body now — turn the shader's rectangular pool and
            // its height-field surface off (they stay in the shader for compatibility).
            _material?.SetFloat(IdHeightCnt, 0f);
            _material?.SetFloat(IdSurfTilt, 0f);
            // With the big render radius the field is dense; this threshold keeps it one smooth
            // connected body (no gaps between particles) with a flat surface, not separate blobs.
            _material?.SetFloat(IdThreshold, 0.7f);
            _material?.SetFloat(IdEdgeWidth, 0.10f);
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

        public void SetColor(Color c)
        {
            if (_material == null) return;
            c.a = Mathf.Clamp(c.a, 0.82f, 0.97f);
            _material.SetColor(IdColor, c);
        }

        /// <summary>The colour of the foam particles (GDD 21 §10). Beer and its head are one
        /// surface; only what they are made of differs, and this is that difference.</summary>
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
            int foamTarget = Mathf.Clamp(
                Mathf.RoundToInt((totalTarget - beerTarget) * FoamCountScale), 0, MaxPool - beerTarget);

            bool seeding = _pn == 0 && totalTarget > 0;   // a fresh body, not a top-up
            Reconcile(KindBeer, beerTarget, beerLineLocal, seeding);
            Reconcile(KindFoam, foamTarget, _fillTopLocal, seeding);
        }

        /// <summary>Particles the vessel holds up to <paramref name="frac"/> of its interior
        /// height, and the container-local y that line sits at.</summary>
        private int CountUpTo(float frac, float bottomY, float rimY, out float topLocal)
        {
            frac = Mathf.Clamp01(frac);
            float h = Mathf.Max((rimY - bottomY) * frac - 2f * PoolRadius * FaceOffset, 0f);
            topLocal = -_halfH + h;
            return Mathf.Clamp(
                Mathf.RoundToInt((2f * Mathf.Max(_halfW - PoolRadius * SideOffset, 1f) * AverageProfile(frac))
                                 * h / (Spacing * Spacing * PackedArea) * _density),
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
            while (have < target && _pn < MaxPool)
            {
                _px[_pn] = Random.Range(-_halfW * 0.6f, _halfW * 0.6f);   // local frame
                _py[_pn] = seeding
                    ? Random.Range(-_halfH, Mathf.Max(lineLocal, -_halfH + 1f))
                    : lineLocal + Random.Range(-6f, 10f);
                _vx[_pn] = 0f; _vy[_pn] = seeding ? 0f : -40f;
                _kind[_pn] = kind;
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

        public void ClearPool() { _poolSet = false; _pn = 0; _foamN = 0; }

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
                float local = (b + 1f) / Bins * span - _halfH;
                ToSurface(0f, local, out _, out float sy);
                return sy;
            }
            return fallback;
        }

        private readonly int[] _surfaceBins = new int[48];

        /// <summary>A sideways nudge to the whole body — a shove of the glass (uv-ish impulse).</summary>
        public void Disturb(float lateralImpulse)
        {
            float v = lateralImpulse * _size.x * 0.5f;   // uv/s → px/s
            for (int i = 0; i < _pn; i++) _vx[i] += v;
        }

        /// <summary>Punches the surface near a local x — a pour landing or a knock.</summary>
        public void Ripple(float localX, float velImpulse)
        {
            float v = velImpulse * _size.y;
            for (int i = 0; i < _pn; i++)
                if (Mathf.Abs(_px[i] - localX) < H && _py[i] > _fillTopLocal - H)
                    _vy[i] -= v;
        }

        public void EmitStream(Vector2 from, Vector2 vel, float dt)
        {
            _emitAccum += dt;
            int guard = 0;
            while (_emitAccum >= StreamInterval && guard++ < 8)
            {
                _emitAccum -= StreamInterval;
                float f = 1f - _emitAccum / StreamInterval;
                SpawnDrop(from + vel * (StreamInterval * f),
                    vel + new Vector2(Random.Range(-14f, 14f), 0f),
                    StreamRadius * Random.Range(0.85f, 1.1f), 3f, true);
            }
        }

        public void Splash(Vector2 at, float strength)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2f + strength * 3f), 2, 6);
            for (int i = 0; i < n; i++)
                SpawnDrop(at + new Vector2(Random.Range(-5f, 5f), 0f),
                    new Vector2(Random.Range(-150f, 150f), Random.Range(120f, 300f) * strength),
                    Random.Range(6f, 9f), Random.Range(0.28f, 0.5f), false);
        }

        private void SpawnDrop(Vector2 pos, Vector2 vel, float radius, float life, bool merges)
        {
            int slot = -1;
            for (int i = 0; i < MaxDrops; i++) if (!_drops[i].Active) { slot = i; break; }
            if (slot < 0) return;   // full: let the new drop go, never cull one mid-fall
            _drops[slot] = new Drop { Pos = pos, Vel = vel, Radius = radius, Life = life, Merges = merges, Active = true };
        }

        public void Step(float dt)
        {
            if (dt <= 0f) dt = 1e-4f;
            if (dt > 1f / 30f) dt = 1f / 30f;   // keep the solver stable on a hitch

            StepPool(dt);
            StepDrops(dt);
            Upload();
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
            float minD = Spacing, minD2 = minD * minD;
            for (int iter = 0; iter < maxIters; iter++)
            {
                BuildGrid();   // O(N) neighbour lookup — keeps a fine particle scale affordable
                // Only the FORWARD half of the neighbourhood (2026-07-28). Scanning all nine
                // cells and throwing away half the pairs with `j <= i` walked every pair twice
                // to use it once, and this loop is ~80% of the fluid's frame. These four cells
                // plus this one still meet every neighbouring pair exactly once: a pair that
                // straddles two cells is found from the backward one, a pair inside a cell by
                // taking only j > i.
                for (int i = 0; i < _pn; i++)
                {
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
            float maxCorr = Spacing * 4f, maxCorr2 = maxCorr * maxCorr;
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
            ClampToVessel();

            // Velocity from the net move (this is what carries a moving/tilting vessel into the
            // liquid — the slosh), speed-capped.
            for (int i = 0; i < _pn; i++)
            {
                // Velocity is the net move over the frame. This is the property that lets the
                // drink come to rest at all: a particle held by the floor or its neighbours
                // barely moves, so its velocity falls to zero instead of being re-integrated.
                _vx[i] = (_px[i] - _ppx[i]) / dt;
                _vy[i] = (_py[i] - _ppy[i]) / dt;
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
        public void SetProfile(float[] halfWidths) => _profile = halfWidths;

        /// <summary>
        /// Per-vessel correction on how many particles a given fill asks for. The estimate draws
        /// the tin and the pint at their own level across the whole range, so both leave this at
        /// 1; only the tumbler still runs generous in the middle of its range — it is much the
        /// shortest cavity, so what is left of the inset error is a bigger share of it — and it
        /// asks for a tenth fewer. Measured 2026-07-28 at four fills, live in each stage.
        /// </summary>
        public void SetDensity(float multiplier) => _density = Mathf.Clamp(multiplier, 0.25f, 4f);
        private float _density = 1f;

        /// <summary>Clamps every particle inside the rotated vessel interior (profile-shaped).</summary>
        private void ClampToVessel()
        {
            // Local frame: the walls are axis-aligned here, so this is a straight compare —
            // no rotation per particle per iteration (the old hot path).
            // Inset by however far the drawn iso-surface actually reaches past a particle centre
            // — measured against this kernel and threshold, not guessed: 0.27r out to the side
            // (where the wall cuts a packed column) and 0.53r above a free surface. Holding the
            // centres exactly that far in makes the DRAWN liquid meet the vessel wall, so it
            // covers the whole interior without bleeding out of it.
            float ix = Mathf.Max(_halfW - PoolRadius * SideOffset, 2f);
            float iy = Mathf.Max(_halfH - PoolRadius * FaceOffset, 2f);
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
                if (ly < -iy) ly = -iy; else if (ly > ceil) ly = ceil;
                float w = HalfWidthAt((ly + iy) / (2f * iy), ix);   // the wall at this height
                float lx = _px[i];
                if (lx < -w) lx = -w; else if (lx > w) lx = w;
                _px[i] = lx; _py[i] = ly;
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
            float h2 = H * H;
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
        private static int CellOf(float v) => Mathf.FloorToInt(v / Cell);

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
            for (int i = 0; i < MaxDrops; i++)
            {
                if (!_drops[i].Active) continue;
                ref Drop d = ref _drops[i];
                d.Vel.y -= Gravity * dt;
                d.Pos += d.Vel * dt;
                d.Life -= dt;

                // A stream drop that reaches the liquid surface inside the vessel melts in.
                if (d.Merges && _poolSet)
                {
                    // Into the container's frame: has it reached the liquid line inside the tin?
                    float c = Mathf.Cos(-_angle), s2 = Mathf.Sin(-_angle);
                    float ox = d.Pos.x - _cx, oy = d.Pos.y - _cy;
                    float lx = ox * c - oy * s2, ly = ox * s2 + oy * c;
                    if (ly <= _fillTopLocal + 6f && Mathf.Abs(lx) < _halfW)
                    {
                        if (Random.value < 0.5f)
                        {
                            ToSurface(lx, _fillTopLocal, out float hx, out float hy);
                            Splash(new Vector2(hx, hy), 0.4f);
                        }
                        Ripple(lx, 0.012f);
                        d.Active = false;
                        continue;
                    }
                }
                if (d.Life <= 0f || d.Pos.y < floor) d.Active = false;
            }
        }

        private void Upload()
        {
            if (_material == null) return;
            int count = 0;
            // A shaken drink spreads out, and spread particles thin the metaball field between
            // them — which reads as the drink losing volume. Give each one a little more reach
            // while the tin is moving so the body stays as solid as it is when it is still.
            float r = _vesselSpeed > 40f ? PoolRadius * 1.18f : PoolRadius;
            for (int i = 0; i < _pn && count < RenderMax; i++)
            {
                ToSurface(_px[i], _py[i], out float sx, out float sy);
                var uv = ToUv(sx, sy);
                // Foam draws bigger and unevenly, and flags itself in w so the shader can colour
                // it: the golden ratio gives each bubble a stable size that does not march in
                // step with its neighbours, so the crest breaks into rounds.
                bool foam = _kind[i] == KindFoam;
                float rr = foam ? FoamRadius * (0.80f + 0.40f * (i * 0.7548777f % 1f)) : r;
                _dropData[count++] = new Vector4(uv.x, uv.y, rr, foam ? 2f : 1f);
            }
            for (int i = 0; i < MaxDrops && count < RenderMax; i++)
            {
                if (!_drops[i].Active) continue;
                var uv = ToUv(_drops[i].Pos.x, _drops[i].Pos.y);
                _dropData[count++] = new Vector4(uv.x, uv.y, _drops[i].Radius, 1f);
            }
            for (int i = count; i < RenderMax; i++) _dropData[i] = Vector4.zero;

            _material.SetFloat(IdDropCount, count);
            _material.SetVectorArray(IdDrops, _dropData);
            // Ensure the shader's rectangular pool contributes nothing (particles are the body),
            // and push its surface line off the top so the old sheen band leaves no stray mark.
            _material.SetFloat(IdPoolMinX, 0f); _material.SetFloat(IdPoolMaxX, 0f);
            _material.SetFloat(IdPoolTopY, 2f); _material.SetFloat(IdPoolBot, 2f);
        }

        public void Clear()
        {
            for (int i = 0; i < MaxDrops; i++) _drops[i].Active = false;
            _pn = 0; _foamN = 0; _emitAccum = 0f;
            Upload();
        }

        public void SetActive(bool on)
        {
            if (_image != null && _material != null) _image.enabled = on;
        }
    }
}
