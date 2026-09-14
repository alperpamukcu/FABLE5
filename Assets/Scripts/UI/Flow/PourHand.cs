using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The hand that holds a pouring vessel — the bottle on the shaker bench, the tin on the
    /// serving bench (2026-09-11, the author: "şu an uzun bardağa sahnede şişeyi yukarı
    /// çıkaramadığımızdan dolayı şişeden sıvı dökemiyoruz", and "sıvılar ve şişeler daha hareketli
    /// olmalı"). Plain C#, one per bench.
    ///
    /// WHERE YOU TOOK HOLD OF IT STAYS UNDER THE POINTER (2026-09-13, the author: "dökerken
    /// döktüğümüz şişenin kontrolü çok zor ve mousedan çok ayrı bir yerde hareket ediyor"). The
    /// vessel used to turn about a grip under its cap while the pointer's offset from that grip
    /// stayed fixed, so the part you had pressed swung 2·d·sin(θ/2) away from the cursor — nearly
    /// four hundred units at neck-down for a bottle taken by its label — and the tilt was read off
    /// the grip's height inside a lift squeezed to 90 units by the room over the neck. Now the
    /// point pressed is remembered ON THE VESSEL, the vessel turns about THAT point, and the lean
    /// is read off how far the POINTER has risen since the press, over a full lift fitted under
    /// the surface's top. The design's rule is kept word for word — higher still tips further.
    ///
    /// WEIGHT. The hand follows the pointer on a spring rather than snapping to it, the tilt
    /// follows its target on a softer one, and a sharp sideways move swings the body under the
    /// hand like the pendulum it is. Let go and the vessel goes home, and when it gets there it
    /// is put EXACTLY back where it stood — the look tests compare the bench byte for byte.
    /// </summary>
    internal sealed class PourHand
    {
        // ── what this vessel is ───────────────────────────────────────────────
        /// <summary>The vessel pivot's resting anchoredPosition.</summary>
        public Vector2 Rest { get; private set; }
        /// <summary>The DRAWN spout, pivot-relative, in the vessel's unrotated frame.</summary>
        public Vector2 Spout { get; private set; }
        /// <summary>How far below the spout, along the vessel, the grip reference sits.</summary>
        public float GripDepth { get; private set; }
        /// <summary>Pointer lift for a full tilt (the most; a press near the top gets less).</summary>
        public float LiftRange { get; private set; }
        /// <summary>Degrees the vessel leans at full lift (counter-clockwise leans left).</summary>
        public float MaxTilt { get; private set; }

        // ── where it is ───────────────────────────────────────────────────────
        private Vector2 _w, _wv;                 // the grip reference, and its velocity (surface px)
        private float _tilt, _tiltV;              // degrees, and degrees per second
        private Vector2 _p, _pv;                  // the hand: the pointer, followed on a spring
        private Vector2 _hold;                    // the pressed point, grip-relative, in the unrotated frame
        private Vector2 _holdPress;               // …as it was taken; _hold slides from it to the neck by level
        private float _liftBase, _liftRange;      // pointer height where the lift starts, and its travel
        private float _ax;                        // low-passed horizontal acceleration of the hand
        private Vector2 _lastV;

        public bool Held { get; private set; }
        /// <summary>Standing exactly on its rest, upright and still.</summary>
        public bool AtRest { get; private set; } = true;
        public float Tilt => _tilt;
        public Vector2 GripPoint => _w;

        // ── tuning ────────────────────────────────────────────────────────────
        private const float FollowOmega = 30f, FollowZeta = 0.85f;   // close behind the pointer, no overshoot to fight
        private const float TiltOmega = 18f, TiltZeta = 0.7f;        // the lean catches up, and barely rocks
        private const float HomeOmega = 16f, HomeZeta = 0.9f;        // set down, not dropped
        private const float SwingPerAccel = 0.006f;                  // degrees per px/s² of sideways push
        private const float MaxSwing = 8f;
        private const float AccelSmoothing = 14f;                    // per second
        /// <summary>The least lift a full tilt is spread over, however near the top it was taken.</summary>
        public const float MinLiftRange = 110f;
        private const float MaxReleaseSpeed = 1600f;                 // what a let-go carries into the walk home

        /// <summary>The grip reference, vessel-relative: the spout less the grip depth along the vessel.</summary>
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
            float maxG = MaxGripDepth(LiftRange, MaxTilt);
            if (GripDepth > maxG) GripDepth = maxG;
            if (!Held) SnapHome();
        }

        /// <summary>Takes hold of the vessel at <paramref name="pointer"/> (surface space).
        /// <paramref name="ceiling"/> is the highest the pointer will be let go, so the lift a full
        /// tilt takes can be fitted under it.</summary>
        /// <param name="clearY">The top the mouth has to clear before the vessel leans (surface space),
        /// or NaN for a lean that starts at the press.</param>
        public void Press(Vector2 pointer, float ceiling = float.PositiveInfinity, float clearY = float.NaN)
        {
            Held = true;
            AtRest = false;
            _holdPress = Rotate(pointer - _w, -_tilt);
            _hold = _holdPress;
            _p = pointer;
            _pv = Vector2.zero;
            _lastV = Vector2.zero;
            // THE WHOLE ROOM ABOVE THE PRESS (2026-09-13): past level the neck is in the hand, so a
            // tall glass is poured by holding the hand over its rim — the lean is spread over the
            // way from where the vessel was taken up to the surface's top (never less than
            // LiftRange, never more than twice it), so there is still a lean to choose up there.
            _liftRange = RangeUnder(ceiling, pointer.y, squeeze: false);
            // A vessel caught mid-lean keeps its lean: the lift starts where that lean already is.
            _liftBase = pointer.y - Unlean(_tilt, MaxTilt) * _liftRange;

            // UPRIGHT UNTIL IT CLEARS THE TOP (2026-09-14, the author: "şişenin eğilmesi bardağın veya
            // shakerin tepe noktasını geçmeye başladıktan sonra olmalı" — a bottle lifted toward a tall
            // glass or the tin leaned from the moment it left the bench and was over on its side before
            // its mouth reached the rim, so nothing poured). Taken upright, the lean waits for two things:
            // the mouth has risen past clearY, and the hand is high enough that the pouring angle — level —
            // arrives with the mouth AT clearY (from level on the neck is in the hand, so the mouth is where
            // the pointer is). The lean then spreads over the room left under the ceiling, however tall the
            // vessel it is poured into.
            if (!float.IsNaN(clearY) && Mathf.Abs(_tilt) < 1f)
            {
                float start = Mathf.Max(pointer.y, pointer.y + (clearY - SpoutNow.y));
                for (int i = 0; i < 5; i++)   // the start and the room under the ceiling settle together
                {
                    _liftRange = RangeUnder(ceiling, start, squeeze: true);
                    start = Mathf.Max(start, clearY - KneeLift * _liftRange);
                }
                _liftRange = RangeUnder(ceiling, start, squeeze: true);
                _liftBase = start;
            }
        }

        /// <summary>The lift a full tilt is spread over, from <paramref name="from"/> up to the ceiling:
        /// the whole room (never more than twice LiftRange). Without <paramref name="squeeze"/> it is
        /// never less than LiftRange either; with it a start high under the ceiling gets what is left,
        /// down to MinLiftRange, so the full tilt stays within reach.</summary>
        private float RangeUnder(float ceiling, float from, bool squeeze)
        {
            if (float.IsInfinity(ceiling)) return LiftRange;
            float room = ceiling - from;
            float least = Mathf.Min(MinLiftRange, LiftRange);
            if (!squeeze) return Mathf.Max(least, Mathf.Clamp(room, LiftRange, LiftRange * 2f));
            return room >= LiftRange ? Mathf.Min(room, LiftRange * 2f) : Mathf.Max(least, room);
        }

        public void Release() => Held = false;

        /// <summary>Puts the vessel back on its rest this instant, upright and still.</summary>
        public void SnapHome()
        {
            _w = GripRest; _wv = Vector2.zero; _lastV = Vector2.zero; _ax = 0f;
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
        /// null when there is none; <paramref name="bounds"/> is where the hand may go.
        /// </summary>
        public void Step(float dt, Vector2? pointer, Rect bounds)
        {
            if (AtRest && !Held) return;
            if (dt <= 0f) return;
            if (dt > 1f / 30f) dt = 1f / 30f;

            if (Held)
            {
                Vector2 target = pointer.HasValue
                    ? new Vector2(Mathf.Clamp(pointer.Value.x, bounds.xMin, bounds.xMax),
                                  Mathf.Clamp(pointer.Value.y, bounds.yMin, bounds.yMax))
                    : _p;
                if (Motion.Reduced)
                {
                    // No springs for a player who asked for less motion: the vessel is where the
                    // hand is, at the angle the lift says.
                    _p = target; _pv = Vector2.zero;
                    _tilt = LeanAt(_p.y); _tiltV = 0f;
                }
                else
                {
                    _pv += (FollowOmega * FollowOmega * (target - _p) - 2f * FollowZeta * FollowOmega * _pv) * dt;
                    _p += _pv * dt;

                    // The body hangs from the hand, so a push sideways swings it: accelerate left
                    // and the foot lags right, which leans the vessel left — toward the glass.
                    Vector2 acc = (_pv - _lastV) / dt;
                    _lastV = _pv;
                    _ax = Mathf.Lerp(_ax, acc.x, 1f - Mathf.Exp(-AccelSmoothing * dt));
                    float swing = Mathf.Clamp(-SwingPerAccel * _ax, -MaxSwing, MaxSwing);

                    float tt = LeanAt(_p.y) + swing;
                    _tiltV += (TiltOmega * TiltOmega * (tt - _tilt) - 2f * TiltZeta * TiltOmega * _tiltV) * dt;
                    _tilt += _tiltV * dt;
                }
                // CARRIED BY WHERE IT WAS TAKEN, POURED FROM THE HAND (2026-09-13, second measure).
                // Upright, the pressed point stays on the hand. As the vessel tips toward level the
                // hold slides up it to the neck, and from level on the neck is in the hand — so the
                // mouth pours where the pointer is. Holding by the label all the way round put the
                // mouth ~230 units under the hand past level: under the tin's rim and far under a
                // tall glass's, which the pour smoke tests measured as nothing poured at all.
                _hold = _holdPress * (1f - Mathf.Clamp01(_tilt / KneeTilt));
                Vector2 was = _w;
                _w = _p - Rotate(_hold, _tilt);
                _wv = Vector2.ClampMagnitude((_w - was) / dt, MaxReleaseSpeed);
                return;
            }

            // Let go: the grip walks home and the vessel stands back up.
            if (Motion.Reduced)
            {
                _w = GripRest; _wv = Vector2.zero;
                _tilt = 0f; _tiltV = 0f;
            }
            else
            {
                _wv += (HomeOmega * HomeOmega * (GripRest - _w) - 2f * HomeZeta * HomeOmega * _wv) * dt;
                _w += _wv * dt;
                _tiltV += (HomeOmega * HomeOmega * (0f - _tilt) - 2f * HomeZeta * HomeOmega * _tiltV) * dt;
                _tilt += _tiltV * dt;
            }

            if ((_w - GripRest).sqrMagnitude < 0.0025f && _wv.sqrMagnitude < 0.25f
                && Mathf.Abs(_tilt) < 0.05f && Mathf.Abs(_tiltV) < 0.5f)
                SnapHome();
        }

        /// <summary>The lean the pointer's height asks for — GDD 24 §2.2: higher tips further.</summary>
        private float LeanAt(float pointerY) =>
            Lean(Mathf.Clamp01((pointerY - _liftBase) / Mathf.Max(1f, _liftRange)), MaxTilt);

        /// <summary>
        /// THE FIRST PART OF THE LIFT LAYS THE VESSEL LEVEL (2026-09-13). The pour runs only past
        /// level now (BottlePour: nothing until 90 degrees, full flow neck-down at 180), so a lift
        /// that tipped evenly all the way would spend half the hand's travel on angles that pour
        /// nothing. The first <see cref="KneeLift"/> of the lift brings the vessel to level, and
        /// the rest — where every degree changes the flow — is spread over the remaining travel.
        /// The author's "şişeyi yukarı kaydırdıkça şişe dikleşsin": lift more and it stands on
        /// its neck.
        /// </summary>
        // A quarter, not 0.35 (2026-09-14, the author: "şişelerde sıvı dökerken daha ince ayar
        // yapılabilmeli"): the pouring half of the lean gets three quarters of the lift.
        public const float KneeLift = 0.25f, KneeTilt = 90f;

        public static float Lean(float lift01, float maxTilt)
        {
            float f = Mathf.Clamp01(lift01);
            if (maxTilt <= KneeTilt) return maxTilt * f;
            return f <= KneeLift
                ? KneeTilt * f / KneeLift
                : KneeTilt + (maxTilt - KneeTilt) * (f - KneeLift) / (1f - KneeLift);
        }

        /// <summary>The lift <see cref="Lean"/> needs for <paramref name="tilt"/>: its inverse.</summary>
        public static float Unlean(float tilt, float maxTilt)
        {
            if (maxTilt <= 0f) return 0f;
            float t = Mathf.Clamp(tilt, 0f, maxTilt);
            if (maxTilt <= KneeTilt) return t / maxTilt;
            return t <= KneeTilt
                ? KneeLift * t / KneeTilt
                : KneeLift + (1f - KneeLift) * (t - KneeTilt) / (maxTilt - KneeTilt);
        }

        /// <summary>The deepest grip reference that keeps the mouth moving the way the hand does
        /// at every angle of <see cref="Lean"/>: its steepest degrees-per-unit of lift decides it.</summary>
        public static float MaxGripDepth(float liftRange, float maxTilt)
        {
            float lr = Mathf.Max(liftRange, 1f);
            float steepest = maxTilt <= KneeTilt
                ? maxTilt / lr
                : Mathf.Max(KneeTilt / (KneeLift * lr), (maxTilt - KneeTilt) / ((1f - KneeLift) * lr));
            return 0.8f / Mathf.Max(1e-5f, steepest * Mathf.Deg2Rad);
        }

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
