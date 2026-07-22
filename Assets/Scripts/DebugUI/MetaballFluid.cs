using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// The metaball fluid for the drink stages (GDD 24 §3.5, 2026-07-22). It renders one
    /// RawImage stretched over the pour surface, driven by <c>LastCall/MetaballLiquid</c>: a
    /// hand-integrated cloud of droplets and the pooled liquid all feed a single scalar field
    /// that the shader thresholds, so the falling liquid reads as one connected, merging mass
    /// (never separate balls), lands with a soft splash, and fills the glass as a rising body.
    ///
    /// Coordinates: everything the caller passes is in the surface's local space (centre
    /// origin, the same space the tilt-pour geometry already works in). The class converts to
    /// the shader's 0..1 UV internally. Cosmetic only — the poured <b>volume</b> is still the
    /// deterministic tilt-pour; the pool surface is placed from the glass's real fill fraction.
    /// </summary>
    public sealed class MetaballFluid
    {
        private const int MaxDrops = 64;             // must match MAX_DROPS in the shader
        private const float Gravity = 2600f;         // px/s², down
        private const float StreamRadius = 13f;      // px; overlapping drops fuse into a column
        private const float StreamInterval = 0.010f; // s between stream drops (density)

        private readonly RectTransform _rt;
        private readonly RawImage _image;
        private readonly Material _material;
        private Vector2 _size;

        private struct Drop
        {
            public Vector2 Pos;      // surface-local px (centre origin)
            public Vector2 Vel;      // px/s
            public float Radius;     // px
            public float Life;       // s remaining
            public bool Merges;      // true once it has hit the pool (dies with a splash)
            public bool Active;
        }

        private readonly Drop[] _drops = new Drop[MaxDrops];
        private readonly Vector4[] _dropData = new Vector4[MaxDrops];
        private float _emitAccum;

        // Pool (glass interior) in surface-local px.
        private float _poolMinX, _poolMaxX, _poolTopY, _poolBottomY;
        private bool _poolSet;

        // Shader property IDs.
        private static readonly int IdSize        = Shader.PropertyToID("_Size");
        private static readonly int IdColor       = Shader.PropertyToID("_Color");
        private static readonly int IdDropCount   = Shader.PropertyToID("_DropCount");
        private static readonly int IdDrops       = Shader.PropertyToID("_Drops");
        private static readonly int IdPoolMinX    = Shader.PropertyToID("_PoolMinX");
        private static readonly int IdPoolMaxX    = Shader.PropertyToID("_PoolMaxX");
        private static readonly int IdPoolTopY    = Shader.PropertyToID("_PoolTopY");
        private static readonly int IdPoolBottomY = Shader.PropertyToID("_PoolBottomY");
        private static readonly int IdPoolEdge    = Shader.PropertyToID("_PoolEdgeSoft");
        private static readonly int IdPoolStr     = Shader.PropertyToID("_PoolStrength");

        public MetaballFluid(RectTransform surface)
        {
            var go = new GameObject("MetaballFluid", typeof(RectTransform));
            go.transform.SetParent(surface, false);
            _rt = (RectTransform)go.transform;
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;

            _image = go.AddComponent<RawImage>();
            _image.raycastTarget = false;

            var shader = Shader.Find("LastCall/MetaballLiquid");
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _image.material = _material;
            }
            else
            {
                Debug.LogWarning("MetaballFluid: shader 'LastCall/MetaballLiquid' not found.");
            }

            RefreshSize();
            SetColor(new Color(0.30f, 0.60f, 1.0f, 0.95f));
            _image.enabled = _material != null;
        }

        /// <summary>Reads the current surface size (call after layout, before converting coords).</summary>
        private void RefreshSize()
        {
            _size = _rt.rect.size;
            if (_size.x < 1f) _size.x = 1f;
            if (_size.y < 1f) _size.y = 1f;
            _material?.SetVector(IdSize, new Vector4(_size.x, _size.y, 0, 0));
        }

        /// <summary>Surface-local (centre origin) px → shader UV (0..1).</summary>
        private Vector2 ToUv(Vector2 local) =>
            new Vector2(local.x / _size.x + 0.5f, local.y / _size.y + 0.5f);

        public void SetColor(Color c)
        {
            if (_material == null) return;
            // Keep some translucency so the glass reads as liquid, not paint.
            c.a = Mathf.Clamp(c.a, 0.82f, 0.97f);
            _material.SetColor(IdColor, c);
        }

        /// <summary>
        /// Places the pooled body from the glass interior and its live fill (all surface-local
        /// px). <paramref name="topY"/> is the current liquid line; the body fills from
        /// <paramref name="bottomY"/> up to it, clipped to [<paramref name="minX"/>,
        /// <paramref name="maxX"/>].
        /// </summary>
        public void SetPool(float minX, float maxX, float bottomY, float topY)
        {
            RefreshSize();
            _poolMinX = minX; _poolMaxX = maxX; _poolBottomY = bottomY; _poolTopY = topY;
            _poolSet = true;

            if (_material == null) return;
            var minUv = ToUv(new Vector2(minX, bottomY));
            var maxUv = ToUv(new Vector2(maxX, topY));
            _material.SetFloat(IdPoolMinX, minUv.x);
            _material.SetFloat(IdPoolMaxX, maxUv.x);
            _material.SetFloat(IdPoolBottomY, minUv.y);
            _material.SetFloat(IdPoolTopY, maxUv.y);
            // Surface softness scales a touch with rect height so merging looks the same at any size.
            _material.SetFloat(IdPoolEdge, 6f / _size.y);
            _material.SetFloat(IdPoolStr, 1.4f);
        }

        /// <summary>Clears the pool (no liquid body, e.g. an empty glass).</summary>
        public void ClearPool()
        {
            _poolSet = false;
            if (_material == null) return;
            _material.SetFloat(IdPoolMinX, 0f);
            _material.SetFloat(IdPoolMaxX, 0f);
            _material.SetFloat(IdPoolTopY, 0f);
            _material.SetFloat(IdPoolBottomY, 0f);
        }

        /// <summary>
        /// Feeds the stream: emits a run of overlapping droplets from <paramref name="from"/>
        /// with velocity <paramref name="vel"/> (surface-local px/s), paced by <paramref name="dt"/>
        /// so the column density is frame-rate independent.
        /// </summary>
        public void EmitStream(Vector2 from, Vector2 vel, float dt)
        {
            _emitAccum += dt;
            int guard = 0;
            while (_emitAccum >= StreamInterval && guard++ < 8)
            {
                _emitAccum -= StreamInterval;
                // Nudge each drop along the velocity so the column is continuous, not stacked.
                float f = 1f - _emitAccum / StreamInterval;
                Spawn(from + vel * (StreamInterval * f),
                      vel + new Vector2(Random.Range(-14f, 14f), 0f),
                      StreamRadius * Random.Range(0.85f, 1.1f), life: 3f, merges: true);
            }
        }

        /// <summary>An organic splash at a point: a few small droplets thrown up and out.</summary>
        public void Splash(Vector2 at, float strength)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2f + strength * 3f), 2, 6);
            for (int i = 0; i < n; i++)
                Spawn(at + new Vector2(Random.Range(-5f, 5f), 0f),
                      new Vector2(Random.Range(-150f, 150f), Random.Range(120f, 300f) * strength),
                      Random.Range(6f, 9f), life: Random.Range(0.28f, 0.5f), merges: false);
        }

        private void Spawn(Vector2 pos, Vector2 vel, float radius, float life, bool merges)
        {
            int slot = -1;
            for (int i = 0; i < MaxDrops; i++)
                if (!_drops[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                // Full: replace the oldest-looking (shortest life) so the stream stays alive.
                float min = float.MaxValue;
                for (int i = 0; i < MaxDrops; i++)
                    if (_drops[i].Life < min) { min = _drops[i].Life; slot = i; }
            }
            _drops[slot] = new Drop
            {
                Pos = pos, Vel = vel, Radius = radius, Life = life, Merges = merges, Active = true
            };
        }

        /// <summary>Integrates every droplet one frame and uploads the field to the shader.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) dt = 1e-4f;
            float floor = -_size.y * 0.5f - 30f;

            for (int i = 0; i < MaxDrops; i++)
            {
                if (!_drops[i].Active) continue;
                ref Drop d = ref _drops[i];

                d.Vel.y -= Gravity * dt;
                d.Pos += d.Vel * dt;
                d.Life -= dt;

                // A stream drop that reaches the pool surface (within the glass) melts in and
                // kicks up a small splash — the soft, organic landing.
                if (d.Merges && _poolSet &&
                    d.Pos.x > _poolMinX && d.Pos.x < _poolMaxX && d.Pos.y <= _poolTopY + 4f)
                {
                    if (Random.value < 0.5f) Splash(new Vector2(d.Pos.x, _poolTopY), 0.5f);
                    d.Active = false;
                    continue;
                }

                if (d.Life <= 0f || d.Pos.y < floor) d.Active = false;
            }

            Upload();
        }

        private void Upload()
        {
            if (_material == null) return;
            int count = 0;
            for (int i = 0; i < MaxDrops; i++)
            {
                if (!_drops[i].Active) continue;
                var uv = ToUv(_drops[i].Pos);
                _dropData[count] = new Vector4(uv.x, uv.y, _drops[i].Radius, 1f);
                count++;
            }
            for (int i = count; i < MaxDrops; i++)
                _dropData[i] = Vector4.zero;

            _material.SetFloat(IdDropCount, count);
            _material.SetVectorArray(IdDrops, _dropData);
        }

        /// <summary>Kills every droplet (stage change).</summary>
        public void Clear()
        {
            for (int i = 0; i < MaxDrops; i++) _drops[i].Active = false;
            _emitAccum = 0f;
            Upload();
        }

        public void SetActive(bool on)
        {
            if (_image != null && _material != null) _image.enabled = on;
        }
    }
}
