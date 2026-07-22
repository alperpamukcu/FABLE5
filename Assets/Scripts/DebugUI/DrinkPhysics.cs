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
}
