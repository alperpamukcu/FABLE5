using UnityEngine;
using UnityEngine.UI;

namespace LastCall.DebugUI
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
        private const int MaxPool = 2100;
        private const int MaxDrops = 40;
        private const int RenderMax = 2176;   // must match MAX_DROPS in the shader

        private const float Gravity = 1400f;          // px/s² down
        private const float StreamRadius = 4f;
        private const float StreamInterval = 0.006f;

        // Position-based fluid (PBD / position-based dynamics, the real-time SPH-family method).
        // Incompressibility is a hard MINIMUM-DISTANCE constraint relaxed a few passes per frame:
        // particles can never pack closer than Spacing, so the body stacks up to the fill line
        // and never collapses. Neighbour-velocity viscosity makes it flow. The particle COUNT is
        // derived from the fill area at Spacing, so it fills any vessel exactly.
        private const float H = 7.5f;                  // viscosity/neighbour radius (px)
        private const float Spacing = 3f;           // rest spacing (min distance) → many small particles
        private const int   RelaxIters = 12;           // incompressibility relaxation passes
        // Render radius is well above the spacing so the fine, tightly-packed particles
        // overlap into ONE smooth connected surface with no gaps between them.
        private const float PoolRadius = 4.3f;
        private const float Viscosity = 0.42f;        // 0..1 neighbour-velocity blend (more flow)
        private const float MaxSpeed = 1300f;
        private const float WallFriction = 0.72f;     // (kept for API parity)

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
        private int _pn;                               // live pool particles

        // Spatial hash grid → O(N) neighbour queries, so the particle count can go high cheaply.
        private const int GridBuckets = 32768;         // power of two
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
        private const float ShakeGain = 110f;  // how hard the vessel's motion throws the drink
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

        /// <summary>
        /// Sets the vessel interior the liquid lives in (surface-local px) and how full it is.
        /// The container is [minX,maxX]×[bottomY,rimY] rotated by <paramref name="angleRad"/>
        /// around its centre; the particle count tracks <paramref name="fillFrac"/>. Called
        /// every frame so the container follows the vessel and the liquid collides with it.
        /// </summary>
        public void SetPool(float minX, float maxX, float bottomY, float rimY,
            float fillFrac, float angleRad = 0f)
        {
            float pcx = _cx, pcy = _cy;
            bool had = _poolSet;
            _cx = (minX + maxX) * 0.5f;
            _cy = (bottomY + rimY) * 0.5f;
            _halfW = Mathf.Max((maxX - minX) * 0.5f, 4f);
            _halfH = Mathf.Max((rimY - bottomY) * 0.5f, 4f);
            _angle = angleRad;
            fillFrac = Mathf.Clamp01(fillFrac);
            _fillTopY = bottomY + (rimY - bottomY) * fillFrac;
            _poolSet = true;
            FitViewport();   // draw only over the vessel + its stream/splash margin

            // The vessel's motion enters the sim as an inertial force, not as walls teleporting
            // into the particles (which crushed the drink and shrank its volume while shaking).
            if (had)
            {
                float dx = _cx - pcx, dy = _cy - pcy;
                _shakeAx = Mathf.Lerp(_shakeAx, -dx * ShakeGain, 0.5f);
                _shakeAy = Mathf.Lerp(_shakeAy, -dy * ShakeGain, 0.5f);
            }
            else { _shakeAx = _shakeAy = 0f; }

            // Enough particles to fill the liquid AREA at the rest spacing — so they pack up to
            // the line, not into a puddle at the bottom. The area follows the vessel silhouette
            // (a narrow tin holds less), so a profiled vessel is not overfilled.
            // The usable area is the interior minus the render-radius inset on each wall, so the
            // count matches the space the particles are actually allowed to occupy.
            const float inset = PoolRadius * 0.75f;
            float fillH = Mathf.Max(_fillTopY - bottomY - inset, 0f);
            float widthScale = AverageProfile(fillFrac);
            int target = Mathf.Clamp(
                Mathf.RoundToInt((2f * Mathf.Max(_halfW - inset, 1f) * widthScale) * fillH
                                 / (Spacing * Spacing) * 1.155f),
                0, MaxPool);
            _fillTopLocal = -_halfH + fillH;
            while (_pn < target && _pn < MaxPool)
            {
                _px[_pn] = Random.Range(-_halfW * 0.6f, _halfW * 0.6f);   // local frame
                _py[_pn] = _fillTopLocal + Random.Range(-6f, 10f);
                _vx[_pn] = 0f; _vy[_pn] = -40f;
                _pn++;
            }
            if (_pn > target) _pn = Mathf.Max(target, 0);   // served/emptied: drop the top ones
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

        public void ClearPool() { _poolSet = false; _pn = 0; }

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
            if (slot < 0)
            {
                float min = float.MaxValue;
                for (int i = 0; i < MaxDrops; i++) if (_drops[i].Life < min) { min = _drops[i].Life; slot = i; }
            }
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
                _vx[i] += accX * dt; _vy[i] += accY * dt;
                _px[i] += _vx[i] * dt; _py[i] += _vy[i] * dt;
                _qx[i] = _px[i]; _qy[i] = _py[i];   // predicted, before the constraints
            }

            // Incompressibility: relax a minimum-distance constraint a few passes — no two
            // particles closer than Spacing — so the body packs up to the fill line and never
            // collapses. The vessel walls are re-applied between passes so the liquid stays in.
            float minD = Spacing, minD2 = minD * minD;
            for (int iter = 0; iter < RelaxIters; iter++)
            {
                BuildGrid();   // O(N) neighbour lookup — keeps a fine particle scale affordable
                for (int i = 0; i < _pn; i++)
                {
                    int cx = CellOf(_px[i]), cy = CellOf(_py[i]);
                    for (int gy = cy - 1; gy <= cy + 1; gy++)
                        for (int gx = cx - 1; gx <= cx + 1; gx++)
                            for (int j = _cellHead[HashCell(gx, gy)]; j >= 0; j = _next[j])
                            {
                                if (j <= i) continue;   // each pair once
                                float dx = _px[j] - _px[i], dy = _py[j] - _py[i];
                                float r2 = dx * dx + dy * dy;
                                if (r2 >= minD2 || r2 < 1e-4f) continue;
                                float r = Mathf.Sqrt(r2);
                                float push = (minD - r) * 0.5f * 1.6f;   // over-relaxed
                                float nx = dx / r, ny = dy / r;
                                _px[i] -= nx * push; _py[i] -= ny * push;
                                _px[j] += nx * push; _py[j] += ny * push;
                            }
                }
                ClampToVessel();
            }

            // Cap how far the constraints may move a particle in one frame. Without this the
            // correction feeds straight back into velocity, which throws particles into the
            // walls, packs them on top of each other (the drink visibly loses volume) and
            // stuffs the grid cells so the neighbour sweep — and the frame — blows up.
            float maxCorr = Spacing * 2.5f, maxCorr2 = maxCorr * maxCorr;
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
                _vx[i] = (_px[i] - _ppx[i]) / dt;
                _vy[i] = (_py[i] - _ppy[i]) / dt;
                float sp2 = _vx[i] * _vx[i] + _vy[i] * _vy[i];
                if (sp2 > MaxSpeed * MaxSpeed) { float s = MaxSpeed / Mathf.Sqrt(sp2); _vx[i] *= s; _vy[i] *= s; }
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
            return ix * Mathf.Lerp(_profile[i0], _profile[i1], f - i0);
        }

        /// <summary>Sets the vessel silhouette: half-width multipliers sampled bottom → rim.
        /// Pass null for a plain rectangular interior.</summary>
        public void SetProfile(float[] halfWidths) => _profile = halfWidths;

        /// <summary>Clamps every particle inside the rotated vessel interior (profile-shaped).</summary>
        private void ClampToVessel()
        {
            // Local frame: the walls are axis-aligned here, so this is a straight compare —
            // no rotation per particle per iteration (the old hot path).
            // Inset by the render radius: the metaball iso-surface reaches ~0.75r past a
            // particle's centre, so holding the centres this far in lands the DRAWN edge of the
            // liquid exactly on the wall, instead of bleeding through the floor and the rim.
            const float edge = PoolRadius * 0.75f;
            float ix = Mathf.Max(_halfW - edge, 2f);
            float iy = Mathf.Max(_halfH - edge, 2f);
            for (int i = 0; i < _pn; i++)
            {
                float ly = _py[i];
                if (ly < -iy) ly = -iy; else if (ly > iy) ly = iy;
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
            for (int i = 0; i < _pn; i++)
            {
                float avx = 0f, avy = 0f; int n = 0;
                int cx = CellOf(_px[i]), cy = CellOf(_py[i]);
                for (int gy = cy - ViscCellR; gy <= cy + ViscCellR; gy++)
                    for (int gx = cx - ViscCellR; gx <= cx + ViscCellR; gx++)
                        for (int j = _cellHead[HashCell(gx, gy)]; j >= 0; j = _next[j])
                        {
                            if (j == i) continue;
                            float dx = _px[j] - _px[i], dy = _py[j] - _py[i];
                            if (dx * dx + dy * dy < h2) { avx += _vx[j]; avy += _vy[j]; n++; }
                        }
                if (n == 0) continue;
                _vx[i] = Mathf.Lerp(_vx[i], avx / n, Viscosity);
                _vy[i] = Mathf.Lerp(_vy[i], avy / n, Viscosity);
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
            for (int i = 0; i < _pn && count < RenderMax; i++)
            {
                ToSurface(_px[i], _py[i], out float sx, out float sy);
                var uv = ToUv(sx, sy);
                _dropData[count++] = new Vector4(uv.x, uv.y, PoolRadius, 1f);
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
            _pn = 0; _emitAccum = 0f;
            Upload();
        }

        public void SetActive(bool on)
        {
            if (_image != null && _material != null) _image.enabled = on;
        }
    }
}
