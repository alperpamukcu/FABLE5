using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // (The Splasher died here, audit 2026-08-11: a droplet/solid particle system
    // that was built, cleared and ticked every frame — and never once fed. Its two
    // emitters had zero callers; the whole thing was a pump running on an empty well.)


    /// <summary>
    /// A hand-integrated pendulum (GDD 24 §2.4, 2026-07-22): a piece held at its top swings
    /// about the grip as the hand moves — grab a lemon by one end and the other end lags and
    /// sways. Pure state; the caller owns the RectTransform (pivot at the grip) and applies
    /// <see cref="Angle"/> as a Z rotation each frame.
    /// </summary>
    public sealed class Pendulum
    {
        /// <summary>Degrees from straight-down; positive leans the hanging end to the right.</summary>
        public float Angle { get; private set; }
        private float _vel;   // deg/s

        private const float Stiffness = 12f;   // pull back toward hanging
        private const float Damping = 4.0f;
        private const float DriveGain = 0.30f; // how hard the hand's motion throws the end
        private const float MaxAngle = 72f;

        public void Reset() { Angle = 0f; _vel = 0f; }

        /// <summary>One step. <paramref name="pivotVel"/> is the grip's motion this frame (px/s).</summary>
        public void Step(float dt, Vector2 pivotVel)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.05f);   // stay stable on a hitch
            // Moving the grip right throws the free end left (inertia), and back again.
            float drive = -pivotVel.x * DriveGain;
            float accel = -Stiffness * Angle - Damping * _vel + drive;
            _vel += accel * dt;
            Angle += _vel * dt;
            if (Angle > MaxAngle) { Angle = MaxAngle; _vel *= -0.3f; }
            else if (Angle < -MaxAngle) { Angle = -MaxAngle; _vel *= -0.3f; }
        }
    }

    /// <summary>
    /// A piece dropped into the shaker (GDD 24 §2.4, 2026-07-22 revision): ice / lemon /
    /// salt / sugar falls from the mouth and <b>dissolves the instant it touches the drink's
    /// surface</b> — a quick fade-and-shrink — so nothing floats visibly inside the liquid.
    /// Hand-integrated in the shaker's local px; pooled RectTransform bodies.
    /// </summary>
    public sealed class ShakerSolids
    {
        private const float Gravity = 1900f;      // px/s²
        private const float DissolveTime = 0.22f; // s to fade away after touching the drink

        private readonly RectTransform _parent;
        private readonly List<Body> _bodies = new List<Body>();
        private float _minX, _maxX, _floorY, _surfaceY;
        private bool _bounded;

        private sealed class Body
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Size;
            public float Spin;      // deg/s
            public Color Color;
            public bool Dissolving;
            public float DissolveT;
            public bool Active;
        }

        public ShakerSolids(RectTransform parent) { _parent = parent; }

        /// <summary>The tin interior and the current liquid line, in surface-local px.</summary>
        public void SetBounds(float minX, float maxX, float floorY, float surfaceY)
        {
            _minX = minX; _maxX = maxX; _floorY = floorY; _surfaceY = surfaceY;
            _bounded = true;
        }

        public bool Any
        {
            get { foreach (var b in _bodies) if (b.Active) return true; return false; }
        }

        /// <summary>
        /// Drops a piece in above the drink with a little sideways toss.
        ///
        /// <paramref name="sprite"/> is what the piece IS — a cube of ice, a twist of lemon.
        /// Without it every piece landed as an untextured square, and since the drag layer
        /// tints its image white whenever the real art loaded, the colour argument carried no
        /// information either: ice, lemon, salt and sugar all fell into the tin as identical
        /// white blocks differing only in size. The colour stays for the sprite-less fallback,
        /// which is the only case it was ever right for.
        /// </summary>
        public void Add(Vector2 pos, Color color, float size, Sprite sprite = null)
        {
            var b = GetFree();
            b.Pos = pos;
            b.Vel = new Vector2(Random.Range(-30f, 30f), -40f);
            b.Size = size;
            b.Color = color;
            b.Rt.sizeDelta = new Vector2(size, size);
            // Bodies are pooled, so the sprite must be cleared as deliberately as it is set.
            b.Img.sprite = sprite;
            b.Img.preserveAspect = sprite != null;
            b.Img.color = color;
            b.Rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            b.Spin = Random.Range(-140f, 140f);
            b.Dissolving = false;
            b.DissolveT = 0f;
            b.Active = true;
            b.Rt.gameObject.SetActive(true);
        }

        public void Step(float dt)
        {
            if (!_bounded) return;
            dt = Mathf.Min(dt, 1f / 60f);
            foreach (var b in _bodies)
            {
                if (!b.Active) continue;

                if (b.Dissolving)
                {
                    // Melt into the drink: shrink and fade, then vanish.
                    b.DissolveT += dt / DissolveTime;
                    float s = Mathf.Clamp01(1f - b.DissolveT);
                    b.Rt.sizeDelta = new Vector2(b.Size * s, b.Size * s);
                    var c = b.Color; c.a *= s; b.Img.color = c;
                    b.Rt.anchoredPosition = b.Pos;
                    if (b.DissolveT >= 1f) { b.Active = false; b.Rt.gameObject.SetActive(false); }
                    continue;
                }

                b.Vel.y -= Gravity * dt;
                b.Pos += b.Vel * dt;
                // Touch the liquid line (within the glass) → start dissolving right there.
                if (b.Pos.y <= _surfaceY && b.Pos.x > _minX && b.Pos.x < _maxX)
                {
                    b.Pos.y = _surfaceY;
                    b.Dissolving = true;
                }
                else if (b.Pos.y < _floorY)   // missed the drink somehow — gone
                {
                    b.Active = false; b.Rt.gameObject.SetActive(false); continue;
                }

                b.Rt.anchoredPosition = b.Pos;
                b.Rt.localRotation *= Quaternion.Euler(0, 0, b.Spin * dt);
            }
        }

        public void Clear()
        {
            foreach (var b in _bodies)
            {
                b.Active = false;
                if (b.Rt != null) b.Rt.gameObject.SetActive(false);
            }
        }

        private Body GetFree()
        {
            foreach (var b in _bodies)
                if (!b.Active) return b;

            var go = new GameObject("Solid", typeof(RectTransform));
            go.transform.SetParent(_parent, false);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var body = new Body { Rt = rt, Img = img };
            _bodies.Add(body);
            return body;
        }
    }
}
