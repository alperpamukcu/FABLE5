using UnityEngine;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// A particle-based liquid for the drink stages (GDD 24 §3.5, rewrite 2026-07-23). The
    /// body of liquid is a cloud of small particles run through a position-based fluid solver
    /// (Clavet et al. double-density relaxation): they attract into one cohesive mass, resist
    /// compression, and — the point of the rewrite — collide with the vessel's *moving* walls,
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
        private const int MaxPool = 84;
        private const int MaxDrops = 40;
        private const int RenderMax = 128;   // must match MAX_DROPS in the shader

        private const float Gravity = 1500f;         // px/s² down
        private const float StreamRadius = 11f;
        private const float StreamInterval = 0.008f;

        // Position-based fluid parameters (tuned for the ~100px glasses here).
        private const float H = 26f;                  // interaction radius (px)
        private const float RestDensity = 4.2f;
        private const float Stiffness = 0.34f;
        private const float NearStiffness = 0.42f;
        private const float PoolRadius = 15f;         // render radius per pool particle
        private const float Viscosity = 0.986f;       // per-frame velocity retention
        private const float MaxSpeed = 1300f;
        private const float WallFriction = 0.72f;     // tangential velocity kept on a wall hit

        private readonly RectTransform _rt;
        private readonly RawImage _image;
        private readonly Material _material;
        private Vector2 _size;

        // ── the pooled liquid: particles ────────────────────────────────────────
        private readonly float[] _px = new float[MaxPool];
        private readonly float[] _py = new float[MaxPool];
        private readonly float[] _vx = new float[MaxPool];
        private readonly float[] _vy = new float[MaxPool];
        private readonly float[] _ppx = new float[MaxPool];
        private readonly float[] _ppy = new float[MaxPool];
        private int _pn;                               // live pool particles

        // Container (vessel interior) this frame: an axis-aligned rect rotated by _angle.
        private float _cx, _cy, _halfW, _halfH, _angle;
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

        public MetaballFluid(RectTransform surface)
        {
            var go = new GameObject("MetaballFluid", typeof(RectTransform));
            go.transform.SetParent(surface, false);
            _rt = (RectTransform)go.transform;
            _rt.anchorMin = Vector2.zero; _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero; _rt.offsetMax = Vector2.zero;

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
            _image.enabled = _material != null;
        }

        private void RefreshSize()
        {
            _size = _rt.rect.size;
            if (_size.x < 1f) _size.x = 1f;
            if (_size.y < 1f) _size.y = 1f;
            _material?.SetVector(IdSize, new Vector4(_size.x, _size.y, 0, 0));
        }

        private Vector2 ToUv(float x, float y) =>
            new Vector2(x / _size.x + 0.5f, y / _size.y + 0.5f);

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
            RefreshSize();
            _cx = (minX + maxX) * 0.5f;
            _cy = (bottomY + rimY) * 0.5f;
            _halfW = Mathf.Max((maxX - minX) * 0.5f, 4f);
            _halfH = Mathf.Max((rimY - bottomY) * 0.5f, 4f);
            _angle = angleRad;
            fillFrac = Mathf.Clamp01(fillFrac);
            _fillTopY = bottomY + (rimY - bottomY) * fillFrac;
            _poolSet = true;

            // Track the fill with the particle count; new particles rain in near the surface.
            int target = Mathf.RoundToInt(fillFrac * MaxPool);
            while (_pn < target && _pn < MaxPool)
            {
                _px[_pn] = _cx + Random.Range(-_halfW * 0.7f, _halfW * 0.7f);
                _py[_pn] = _fillTopY + Random.Range(-6f, 10f);
                _vx[_pn] = 0f; _vy[_pn] = -40f;
                _pn++;
            }
            if (_pn > target) _pn = Mathf.Max(target, 0);   // served/emptied: drop the top ones
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
                if (Mathf.Abs(_px[i] - localX) < H && _py[i] > _fillTopY - H)
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

            for (int i = 0; i < _pn; i++)
            {
                _vy[i] -= Gravity * dt;
                _vx[i] *= Viscosity; _vy[i] *= Viscosity;
                _ppx[i] = _px[i]; _ppy[i] = _py[i];
                _px[i] += _vx[i] * dt; _py[i] += _vy[i] * dt;
            }

            // Double-density relaxation: incompressibility + a little surface tension.
            float h2 = H * H;
            for (int i = 0; i < _pn; i++)
            {
                float rho = 0f, rhoNear = 0f;
                for (int j = 0; j < _pn; j++)
                {
                    if (j == i) continue;
                    float dx = _px[j] - _px[i], dy = _py[j] - _py[i];
                    float r2 = dx * dx + dy * dy;
                    if (r2 >= h2) continue;
                    float q = 1f - Mathf.Sqrt(r2) / H;
                    rho += q * q; rhoNear += q * q * q;
                }
                float pressure = Stiffness * (rho - RestDensity);
                float pressNear = NearStiffness * rhoNear;

                float ddx = 0f, ddy = 0f;
                for (int j = 0; j < _pn; j++)
                {
                    if (j == i) continue;
                    float dx = _px[j] - _px[i], dy = _py[j] - _py[i];
                    float r2 = dx * dx + dy * dy;
                    if (r2 >= h2 || r2 < 1e-4f) continue;
                    float r = Mathf.Sqrt(r2);
                    float q = 1f - r / H;
                    float mag = dt * dt * (pressure * q + pressNear * q * q) * 0.5f;
                    float nx = dx / r, ny = dy / r;
                    _px[j] += nx * mag; _py[j] += ny * mag;
                    ddx -= nx * mag; ddy -= ny * mag;
                }
                _px[i] += ddx; _py[i] += ddy;
            }

            // Collide with the vessel walls (the rotated container) and re-derive velocity so
            // that a moving/tilting vessel carries the liquid — the source of the slosh.
            float cos = Mathf.Cos(-_angle), sin = Mathf.Sin(-_angle);
            float cosB = Mathf.Cos(_angle), sinB = Mathf.Sin(_angle);
            float ix = _halfW - PoolRadius * 0.5f, iy = _halfH - PoolRadius * 0.5f;
            if (ix < 2f) ix = 2f; if (iy < 2f) iy = 2f;
            for (int i = 0; i < _pn; i++)
            {
                float ox = _px[i] - _cx, oy = _py[i] - _cy;
                float lx = ox * cos - oy * sin, ly = ox * sin + oy * cos;   // to container-local
                bool hitX = false, hitY = false;
                if (lx < -ix) { lx = -ix; hitX = true; } else if (lx > ix) { lx = ix; hitX = true; }
                if (ly < -iy) { ly = -iy; hitY = true; } else if (ly > iy) { ly = iy; hitY = true; }
                _px[i] = _cx + (lx * cosB - ly * sinB);
                _py[i] = _cy + (lx * sinB + ly * cosB);

                float vx = (_px[i] - _ppx[i]) / dt, vy = (_py[i] - _ppy[i]) / dt;
                if (hitX || hitY)
                {
                    // Bleed the wall-normal velocity, keep some tangential (friction).
                    float tx = vx * cos - vy * sin, ty = vx * sin + vy * cos;
                    if (hitX) tx *= -0.02f;
                    if (hitY) ty *= -0.02f;
                    tx *= WallFriction; ty *= WallFriction;
                    vx = tx * cosB - ty * sinB; vy = tx * sinB + ty * cosB;
                }
                float sp2 = vx * vx + vy * vy;
                if (sp2 > MaxSpeed * MaxSpeed) { float s = MaxSpeed / Mathf.Sqrt(sp2); vx *= s; vy *= s; }
                _vx[i] = vx; _vy[i] = vy;
            }
        }

        private void StepDrops(float dt)
        {
            float floor = -_size.y * 0.5f - 30f;
            for (int i = 0; i < MaxDrops; i++)
            {
                if (!_drops[i].Active) continue;
                ref Drop d = ref _drops[i];
                d.Vel.y -= Gravity * dt;
                d.Pos += d.Vel * dt;
                d.Life -= dt;

                // A stream drop that reaches the liquid surface inside the vessel melts in.
                if (d.Merges && _poolSet && d.Pos.y <= _fillTopY + 6f &&
                    Mathf.Abs(d.Pos.x - _cx) < _halfW)
                {
                    if (Random.value < 0.5f) Splash(new Vector2(d.Pos.x, _fillTopY), 0.4f);
                    Ripple(d.Pos.x, 0.012f);
                    d.Active = false;
                    continue;
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
                var uv = ToUv(_px[i], _py[i]);
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
