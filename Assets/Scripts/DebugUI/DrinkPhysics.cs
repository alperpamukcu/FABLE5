using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// A tiny UI-space physics layer for the drink stages (GDD 24 §2–3): falling liquid
    /// droplets and settling solids, integrated by hand each frame (Unity's Physics2D does
    /// not reach Canvas RectTransforms). Purely cosmetic — the poured *volume* is still
    /// driven by the deterministic tilt-pour logic; this makes it look and feel like a real
    /// pour. Everything lives under one parent RectTransform and is pooled.
    /// </summary>
    public sealed class Splasher
    {
        private const float Gravity = 2600f;   // px/s²

        private readonly RectTransform _parent;
        private readonly List<Body> _bodies = new List<Body>();

        private sealed class Body
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Vel;
            public float Life;
            public float Bounce;     // 0 = liquid (splashes and dies), >0 = solid (bounces)
            public float Floor;      // y it settles/dies at (vessel inner bottom, or off-screen)
            public bool Settle;      // true once a solid has come to rest
            public bool Active;
        }

        public Splasher(RectTransform parent)
        {
            _parent = parent;
        }

        /// <summary>A liquid droplet: falls, and vanishes with a tiny splash at its floor.</summary>
        public void EmitDroplet(Vector2 pos, Vector2 vel, Color color, float floorY)
        {
            var b = GetFree();
            b.Rt.anchoredPosition = pos;
            b.Rt.sizeDelta = new Vector2(Random.Range(4f, 7f), Random.Range(6f, 11f));
            b.Rt.localRotation = Quaternion.identity;
            b.Img.color = color;
            b.Vel = vel + new Vector2(Random.Range(-30f, 30f), 0f);
            b.Life = 2.2f;
            b.Bounce = 0f;
            b.Floor = floorY;
            b.Settle = false;
            b.Active = true;
            b.Rt.gameObject.SetActive(true);
        }

        /// <summary>
        /// The crown thrown up where a pour hits the liquid line: a handful of fine, short-lived
        /// droplets bursting up and out. This is what makes the stream read as *landing in* the
        /// drink instead of passing through it (GDD 24 §2, 2026-07-22).
        /// </summary>
        public void EmitSplash(Vector2 pos, Color color, float floorY, float strength)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2f + strength * 3f), 2, 6);
            for (int i = 0; i < n; i++)
            {
                var b = GetFree();
                b.Rt.anchoredPosition = pos + new Vector2(Random.Range(-6f, 6f), 0f);
                b.Rt.sizeDelta = new Vector2(Random.Range(3f, 5f), Random.Range(3f, 6f));
                b.Rt.localRotation = Quaternion.identity;
                b.Img.color = color;
                // Up and out — a splash, not a fall.
                b.Vel = new Vector2(Random.Range(-140f, 140f), Random.Range(120f, 260f) * strength);
                b.Life = Random.Range(0.25f, 0.5f);
                b.Bounce = 0f;
                b.Floor = floorY - 4f;
                b.Settle = false;
                b.Active = true;
                b.Rt.gameObject.SetActive(true);
            }
        }

        /// <summary>A solid piece (ice/lemon): falls, bounces, and settles at the floor.</summary>
        public void EmitSolid(Vector2 pos, Vector2 vel, Color color, float floorY, float size)
        {
            var b = GetFree();
            b.Rt.anchoredPosition = pos;
            b.Rt.sizeDelta = new Vector2(size, size);
            b.Rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            b.Img.color = color;
            b.Vel = vel;
            b.Life = 6f;
            b.Bounce = 0.45f;
            b.Floor = floorY + size * 0.5f;
            b.Settle = false;
            b.Active = true;
            b.Rt.gameObject.SetActive(true);
        }

        /// <summary>Integrates every live body one frame.</summary>
        public void Step(float dt)
        {
            float bottom = -_parent.rect.height * 0.5f - 40f;
            foreach (var b in _bodies)
            {
                if (!b.Active) continue;
                if (b.Settle) continue;   // a rested solid just sits there

                b.Vel.y -= Gravity * dt;
                var pos = b.Rt.anchoredPosition + b.Vel * dt;
                b.Life -= dt;

                if (b.Bounce > 0f)
                {
                    // Solid: bounce off its floor, losing energy, then settle.
                    if (pos.y <= b.Floor)
                    {
                        pos.y = b.Floor;
                        if (Mathf.Abs(b.Vel.y) < 120f) { b.Settle = true; b.Vel = Vector2.zero; }
                        else { b.Vel.y = -b.Vel.y * b.Bounce; b.Vel.x *= 0.7f; }
                    }
                    b.Rt.anchoredPosition = pos;
                    b.Rt.localRotation *= Quaternion.Euler(0, 0, b.Vel.x * dt * 2f);
                    if (b.Life <= 0f) Kill(b);
                }
                else
                {
                    // Liquid: dies at its floor (a splash) or when it runs off the surface.
                    b.Rt.anchoredPosition = pos;
                    if (pos.y <= b.Floor || pos.y < bottom || b.Life <= 0f) Kill(b);
                }
            }
        }

        /// <summary>Clears everything (stage change).</summary>
        public void Clear()
        {
            foreach (var b in _bodies) Kill(b);
        }

        private static void Kill(Body b)
        {
            b.Active = false;
            b.Settle = false;
            if (b.Rt != null) b.Rt.gameObject.SetActive(false);
        }

        private Body GetFree()
        {
            foreach (var b in _bodies)
                if (!b.Active) return b;

            var go = new GameObject("Body", typeof(RectTransform));
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

    /// <summary>
    /// A continuous pouring stream (GDD 24 §2–3, 2026-07-22): a wavy ribbon of stacked
    /// segments from the bottle mouth down to the liquid line, replacing the old spray of
    /// falling boxes. It reads as one connected fall of liquid; the receiving splash is the
    /// <see cref="Splasher"/>'s job. Pooled and hidden when not pouring.
    /// </summary>
    public sealed class PourStream
    {
        private const int Segments = 12;

        private readonly RectTransform _parent;
        private readonly List<RectTransform> _rts = new List<RectTransform>();
        private readonly List<Image> _imgs = new List<Image>();
        private float _phase;

        public PourStream(RectTransform parent) { _parent = parent; }

        /// <summary>
        /// Draws the stream from <paramref name="from"/> (the mouth) down to <paramref name="toY"/>
        /// (the surface it lands on). The stream falls under gravity, so it narrows and waves a
        /// little more the further it drops.
        /// </summary>
        public void Draw(Vector2 from, float toY, Color color, float widthTop, float dt)
        {
            EnsureSegments();
            _phase += dt * 9f;

            float span = Mathf.Max(2f, from.y - toY);
            float segH = span / Segments + 2f;
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1);   // 0 at mouth, 1 at surface
                float y = Mathf.Lerp(from.y, toY, t);
                // The wave grows as the stream falls and speeds up; the top stays anchored.
                float wobble = Mathf.Sin(_phase - t * 5f) * (t * 6f);
                var rt = _rts[i];
                rt.anchoredPosition = new Vector2(from.x + wobble, y);
                rt.sizeDelta = new Vector2(Mathf.Lerp(widthTop, widthTop * 0.55f, t), segH);
                var c = color; c.a = Mathf.Lerp(0.95f, 0.75f, t);
                _imgs[i].color = c;
                rt.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            foreach (var rt in _rts) rt.gameObject.SetActive(false);
        }

        private void EnsureSegments()
        {
            if (_rts.Count > 0) return;
            for (int i = 0; i < Segments; i++)
            {
                var go = new GameObject("StreamSeg", typeof(RectTransform));
                go.transform.SetParent(_parent, false);
                var rt = (RectTransform)go.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                _rts.Add(rt);
                _imgs.Add(img);
            }
        }
    }

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
}
