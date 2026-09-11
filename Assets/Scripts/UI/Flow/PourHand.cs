using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The hand that holds a pouring vessel — the bottle on the shaker bench, the tin on the
    /// serving bench (2026-09-11, the author: "şu an uzun bardağa sahnede şişeyi yukarı
    /// çıkaramadığımızdan dolayı şişeden sıvı dökemiyoruz", and "sıvılar ve şişeler daha hareketli
    /// olmalı"). Plain C#, one per bench.
    ///
    /// HOLD THE NECK, TURN ABOUT IT. The vessel used to be held at 0.22 of its height and tipped
    /// about that point as it was lifted (GDD 24 §2.2: "the higher it goes, the further it tips").
    /// With the mouth L above the grip, lifting by dy raises the mouth by dy·(1 − L·sinθ·dθ/dy) —
    /// and with the tin's L of 324 that turned NEGATIVE past 17°: once pouring, lifting the tin
    /// LOWERED its mouth. The 420 glass of 2026-09-09 put its rim above anything the mouth could
    /// reach at a pouring angle: the pour window shrank from 328 units of hand travel to 10.
    ///
    /// Held a short way below the spout instead, L is small, the bracket stays positive at every
    /// angle, and raising the hand always raises the mouth. The rule the design wrote is kept
    /// word for word — higher still tips further — only where the hand holds the vessel changes.
    ///
    /// WEIGHT. The grip follows the pointer on a spring rather than snapping to it, the tilt
    /// follows its target on a softer one, and a sharp sideways move swings the body under the
    /// neck like the pendulum it is. Let go and the vessel goes home, and when it gets there it
    /// is put EXACTLY back where it stood — the look tests compare the bench byte for byte.
    /// </summary>
    internal sealed class PourHand
    {
        // ── what this vessel is ───────────────────────────────────────────────
        /// <summary>The vessel pivot's resting anchoredPosition.</summary>
        public Vector2 Rest { get; private set; }
        /// <summary>The DRAWN spout, pivot-relative, in the vessel's unrotated frame.</summary>
        public Vector2 Spout { get; private set; }
        /// <summary>How far below the spout, along the vessel, the hand holds it.</summary>
        public float GripDepth { get; private set; }
        /// <summary>Grip lift for a full tilt.</summary>
        public float LiftRange { get; private set; }
        /// <summary>Degrees the vessel leans at full lift (counter-clockwise leans left).</summary>
        public float MaxTilt { get; private set; }

        // ── where it is ───────────────────────────────────────────────────────
        private Vector2 _w, _wv;                 // the grip, and its velocity (surface px)
        private float _tilt, _tiltV;              // degrees, and degrees per second
        private Vector2 _offset;                  // grip minus pointer at the press
        private float _ax;                        // low-passed horizontal acceleration of the grip
        private Vector2 _lastWv;

        public bool Held { get; private set; }
        /// <summary>Standing exactly on its rest, upright and still.</summary>
        public bool AtRest { get; private set; } = true;
        public float Tilt => _tilt;
        public Vector2 GripPoint => _w;

        // ── tuning ────────────────────────────────────────────────────────────
        private const float FollowOmega = 28f, FollowZeta = 0.75f;   // ~40 ms of lag, a hint of overshoot
        private const float TiltOmega = 18f, TiltZeta = 0.62f;       // the lean catches up, and rocks once
        private const float HomeOmega = 16f, HomeZeta = 0.9f;        // set down, not dropped
        private const float SwingPerAccel = 0.010f;                  // degrees per px/s² of sideways push
        private const float MaxSwing = 12f;
        private const float AccelSmoothing = 14f;                    // per second

        /// <summary>The grip, vessel-relative: the spout less the grip depth along the vessel.</summary>
        private Vector2 G => new Vector2(Spout.x, Spout.y - GripDepth);
        public Vector2 GripRest => Rest + G;

        /// <summary>
        /// Sets up the vessel. A hand that is not holding anything is put straight back on the
        /// new rest; one that is mid-pour keeps its grip where it is.
        /// </summary>
        public void Configure(Vector2 rest, Vector2 spout, float gripDepth, float liftRange, float maxTilt)
        {
            Rest = rest; Spout = spout; GripDepth = gripDepth;
            LiftRange = Mathf.Max(liftRange, 1f); MaxTilt = maxTilt;
            // Never let the bracket turn: d(spout.y)/d(lift) = 1 − g·sinθ·(MaxTilt/LiftRange in rad)
            // must stay positive at every angle, or raising the hand lowers the mouth again.
            float maxG = 0.8f * LiftRange / (MaxTilt * Mathf.Deg2Rad);
            if (GripDepth > maxG) GripDepth = maxG;
            if (!Held) SnapHome();
        }

        public void Press(Vector2 pointer)
        {
            Held = true;
            AtRest = false;
            _offset = _w - pointer;
        }

        public void Release() => Held = false;

        /// <summary>Puts the vessel back on its rest this instant, upright and still.</summary>
        public void SnapHome()
        {
            _w = GripRest; _wv = Vector2.zero; _lastWv = Vector2.zero; _ax = 0f;
            _tilt = 0f; _tiltV = 0f;
            AtRest = true;
            _restWritten = false;   // the next Apply puts it there, once
        }

        // The vessel at rest belongs to its HoverGlow, not to the hand. The glow is additive — it
        // takes off last frame's rise and sway and adds this frame's — so a hand writing an
        // absolute pose every frame would cancel the lift it gives a vessel under the pointer.
        // The hand writes while it is moving, and exactly once when it sets the vessel down.
        private bool _restWritten;

        /// <summary>
        /// One frame. <paramref name="pointer"/> is the pointer in the surface's local space, or
        /// null when there is none; <paramref name="bounds"/> is where the grip may go.
        /// </summary>
        public void Step(float dt, Vector2? pointer, Rect bounds)
        {
            if (AtRest && !Held) return;
            if (dt <= 0f) return;
            if (dt > 1f / 30f) dt = 1f / 30f;

            Vector2 target = Held && pointer.HasValue
                ? new Vector2(Mathf.Clamp(pointer.Value.x + _offset.x, bounds.xMin, bounds.xMax),
                              Mathf.Clamp(pointer.Value.y + _offset.y, bounds.yMin, bounds.yMax))
                : Held ? _w : GripRest;

            if (Motion.Reduced)
            {
                // No springs for a player who asked for less motion: the vessel is where the
                // hand is, at the angle the height says, exactly as it was before any of this.
                _w = target; _wv = Vector2.zero;
                _tilt = Held ? TiltFor(_w) : 0f; _tiltV = 0f;
            }
            else
            {
                float om = Held ? FollowOmega : HomeOmega, ze = Held ? FollowZeta : HomeZeta;
                _wv += (om * om * (target - _w) - 2f * ze * om * _wv) * dt;
                _w += _wv * dt;

                // The body hangs under the neck, so a push sideways swings it: accelerate left
                // and the foot lags right, which leans the vessel left — toward the glass.
                Vector2 acc = (_wv - _lastWv) / dt;
                _lastWv = _wv;
                _ax = Mathf.Lerp(_ax, acc.x, 1f - Mathf.Exp(-AccelSmoothing * dt));
                float swing = Held ? Mathf.Clamp(-SwingPerAccel * _ax, -MaxSwing, MaxSwing) : 0f;

                float tt = (Held ? TiltFor(_w) : 0f) + swing;
                float tom = Held ? TiltOmega : HomeOmega, tze = Held ? TiltZeta : HomeZeta;
                _tiltV += (tom * tom * (tt - _tilt) - 2f * tze * tom * _tiltV) * dt;
                _tilt += _tiltV * dt;
            }

            if (!Held && (_w - GripRest).sqrMagnitude < 0.0025f && _wv.sqrMagnitude < 0.25f
                && Mathf.Abs(_tilt) < 0.05f && Mathf.Abs(_tiltV) < 0.5f)
                SnapHome();
        }

        /// <summary>The lean the grip's height asks for — the rule GDD 24 §2.2 wrote.</summary>
        private float TiltFor(Vector2 w) =>
            MaxTilt * Mathf.Clamp01((w.y - GripRest.y) / LiftRange);

        /// <summary>Where the drawn spout is right now, in surface space.</summary>
        public Vector2 SpoutNow => _w + Rotate(Spout - G, _tilt);

        /// <summary>Stands the vessel's RectTransform where the hand has it.</summary>
        public void Apply(RectTransform vessel)
        {
            if (vessel == null) return;
            if (AtRest)
            {
                if (_restWritten) return;
                // Exactly the rest, not Rest + G − G: float rounding would leave the bench a
                // hair off the picture the look test holds it to.
                vessel.anchoredPosition = Rest;
                vessel.localRotation = Quaternion.identity;
                _restWritten = true;
                return;
            }
            vessel.anchoredPosition = _w - Rotate(G, _tilt);
            vessel.localRotation = Quaternion.Euler(0f, 0f, _tilt);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
