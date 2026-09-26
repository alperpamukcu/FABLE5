using LastCall.Game;
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
    ///
    /// INVERTED (2026-09-26, the author: "ters mouse", the settings' INVERT POUR): the vessel still
    /// stays under the pointer — nothing in this game can put the bottle on one side of the screen
    /// and the hand on the other — but the lean is read off how far the hand has come DOWN from the
    /// highest it was lifted. Taken upright it stays upright while it is lifted over the glass; once
    /// the hand has been as high as the whole lean needs, lowering it tips, and raising it again
    /// stands it back up. The mouth-over-the-rim rule is kept the other way round: the hand is not
    /// let below the height where the mouth, at the fullest lean, would drop under the rim.
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
        /// <summary>How far below the spout, along the vessel, the hand holds it from level on: the
        /// grip depth (the neck) for the tin, the middle of the drawing for a bottle (2026-09-14).</summary>
        public float HoldBelowSpout { get; private set; }

        // ── where it is ───────────────────────────────────────────────────────
        private Vector2 _w, _wv;                 // the grip reference, and its velocity (surface px)
        private float _tilt, _tiltV;              // degrees, and degrees per second
        private Vector2 _p, _pv;                  // the hand: the pointer, followed on a spring
        private Vector2 _hold;                    // the pressed point, grip-relative, in the unrotated frame
        private Vector2 _holdPress;               // …as it was taken; _hold slides from it to the neck by level
        private float _liftBase, _liftRange;      // pointer height where the lift starts, and its travel
        private float _ax;                        // low-passed horizontal acceleration of the hand
        private Vector2 _lastV;
        // INVERTED (PlayerOptions.InvertPour, read at the press so a grab never changes its mind):
        private bool _invert;                     // this grab tips as the hand comes down
        private bool _armed;                      // …once the hand has been lifted to _armAt
        private float _armAt;                     // the height that arms it: the top of the lean's window
        private float _peak;                      // the highest the hand has been since it was armed
        private float _floor = float.NegativeInfinity;   // the lowest the hand may go, armed (the mouth over the rim)

        public bool Held { get; private set; }
        /// <summary>Standing exactly on its rest, upright and still.</summary>
        public bool AtRest { get; private set; } = true;
        public float Tilt => _tilt;
        public Vector2 GripPoint => _w;

        /// <summary>
        /// How far the hand has lifted past the pouring angle, 0..1 of the rest of its lift (2026-09-14): what
        /// BottlePour.LiftShare steps. Read off the hand's own height, not the lean, so the swing a sideways
        /// push adds to the lean cannot rock the pour from one step to the next.
        /// </summary>
        public float PourLift01
        {
            get
            {
                if (!Held || _liftRange <= 0f) return 0f;
                float lift01 = Lift01(_p.y);
                if (MaxTilt <= KneeTilt) return Mathf.Clamp01(lift01);
                return Mathf.Clamp01((lift01 - KneeLift) / (1f - KneeLift));
            }
        }

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
        /// <summary>The longest frame the hand catches up in full (2026-09-26): past it a hitch is dropped rather
        /// than flinging the vessel. The frame itself is stepped at most 1/60 s at a time (SpringStep).</summary>
        private const float MaxFrame = 0.1f;
        /// <summary>How close under the ceiling the hand has to come to arm an inverted lean: it follows a pointer
        /// held at the ceiling on a spring, so it only ever nears it.</summary>
        private const float ArmSlack = 2f;

        /// <summary>The grip reference, vessel-relative: the spout less the grip depth along the vessel.</summary>
        private Vector2 G => new Vector2(Spout.x, Spout.y - GripDepth);
        /// <summary>The hold from level on, grip-relative in the unrotated frame.</summary>
        private Vector2 PourHold => new Vector2(0f, GripDepth - HoldBelowSpout);
        public Vector2 GripRest => Rest + G;

        /// <summary>
        /// Sets up the vessel. A hand that is not holding anything is put straight back on the
        /// new rest; one that is mid-pour keeps its grip where it is.
        /// </summary>
        /// <param name="holdBelowSpout">Where the hand holds the vessel from level on, measured down from
        /// the spout; NaN for the neck (the grip depth).</param>
        public void Configure(Vector2 rest, Vector2 spout, float gripDepth, float liftRange, float maxTilt,
            float holdBelowSpout = float.NaN)
        {
            Rest = rest; Spout = spout; GripDepth = gripDepth;
            LiftRange = Mathf.Max(liftRange, 1f); MaxTilt = maxTilt;
            float maxG = MaxGripDepth(LiftRange, MaxTilt);
            if (GripDepth > maxG) GripDepth = maxG;
            HoldBelowSpout = float.IsNaN(holdBelowSpout) ? GripDepth : Mathf.Max(0f, holdBelowSpout);
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
            _invert = PlayerOptions.InvertPour;
            _floor = float.NegativeInfinity;
            if (_invert)
            {
                // Inverted with no rim to clear, or caught mid-lean: armed at once, the lean it kept standing
                // where the hand is, and more of it the lower the hand goes.
                _armed = true;
                _peak = pointer.y + Unlean(_tilt, MaxTilt) * _liftRange;
                _armAt = _peak;
            }

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
                if (_invert) { PressInverted(pointer, ceiling, clearY); return; }
                float start = Mathf.Max(pointer.y, pointer.y + (clearY - SpoutNow.y));
                for (int i = 0; i < 5; i++)   // the start and the room under the ceiling settle together
                {
                    _liftRange = RangeUnder(ceiling, start, squeeze: true);
                    start = Mathf.Max(start, clearY - LowestMouthOverLift(_liftRange));
                }
                _liftRange = RangeUnder(ceiling, start, squeeze: true);
                _liftBase = start;
            }
        }

        /// <summary>
        /// Over every pouring lean (level to MaxTilt), the lowest the mouth stands above the lift's
        /// start: the lift that lean takes plus the mouth's height over the hand at it. Held by the
        /// neck that is level itself (the knee's lift). Held by its middle, a bottle's mouth swings
        /// down under the hand as it tips past level — measured with a 190 lever over a 260 lift, the
        /// lowest point comes near 140 degrees, some 40 under where it stood at level — so the start
        /// is raised until even that point clears the top.
        /// </summary>
        private float LowestMouthOverLift(float range)
        {
            Vector2 lever = new Vector2(0f, GripDepth) - PourHold;   // hold to spout, unrotated
            float lowest = float.PositiveInfinity;
            float top = Mathf.Max(KneeTilt, MaxTilt);
            for (float t = KneeTilt; t <= top + 0.01f; t += 2f)
            {
                float over = range * Unlean(t, MaxTilt) + Rotate(lever, t).y;
                if (over < lowest) lowest = over;
            }
            return float.IsInfinity(lowest) ? KneeLift * range : lowest;
        }

        /// <summary>
        /// INVERTED, TAKEN UPRIGHT (2026-09-26): the lean's window is laid from the top down. Its foot — the
        /// <see cref="_floor"/> — is the lowest the hand may stand once it pours: high enough that the mouth clears
        /// the rim at EVERY pouring lean, whatever height that lean is reached at, so lowering the hand can never
        /// take the mouth under the rim. Its top is a full lift over the foot (never more than LiftRange, so the
        /// climb before the first tip stays short), or just under the ceiling if that is lower. The vessel stays
        /// upright until the hand has been up there.
        /// </summary>
        private void PressInverted(Vector2 pointer, float ceiling, float clearY)
        {
            float floor = clearY - LowestMouthOverHand();
            if (float.IsInfinity(ceiling)) _liftRange = LiftRange;
            else
            {
                float room = ceiling - floor;
                float least = Mathf.Min(MinLiftRange, LiftRange);
                _liftRange = room >= LiftRange ? LiftRange : Mathf.Max(least, room);
            }
            _floor = floor;
            _armAt = Mathf.Min(floor + _liftRange, ceiling - ArmSlack);
            _armed = false;
            _peak = pointer.y;
        }

        /// <summary>Over every pouring lean (level to MaxTilt), the lowest the mouth stands over the HAND: under it
        /// past level, by the whole hold-to-mouth lever at neck-down — half the drawing for a bottle held by its
        /// middle, the neck for the tin.</summary>
        private float LowestMouthOverHand()
        {
            Vector2 lever = new Vector2(0f, GripDepth) - PourHold;   // hold to spout, unrotated
            float lowest = float.PositiveInfinity;
            float top = Mathf.Max(KneeTilt, MaxTilt);
            for (float t = KneeTilt; t <= top + 0.01f; t += 2f)
                lowest = Mathf.Min(lowest, Rotate(lever, t).y);
            return float.IsInfinity(lowest) ? 0f : lowest;
        }

        /// <summary>Inverted: arms the lean once the hand has been up to its top, and keeps the highest it has been
        /// since, which is where the lean is measured down from.</summary>
        private void TrackPeak()
        {
            if (!_invert) return;
            if (!_armed && _p.y >= _armAt) { _armed = true; _peak = _p.y; }
            if (_armed && _p.y > _peak) _peak = _p.y;
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
            // IN STEPS OF A SIXTIETH (2026-09-26, SpringStep): the follow spring's single step went unstable
            // under 32 fps — a vsync halved to 30 flung the grip — so a long frame is walked in steps of at most
            // 1/60 s. At 60 fps and faster that is one step, the same arithmetic as before; the old 1/30 clamp
            // slowed the hand at low rates instead, and only a real hitch is still cut short (MaxFrame).
            if (dt > MaxFrame) dt = MaxFrame;
            int steps = SpringStep.Substeps(dt);
            float h = dt / steps;

            if (Held)
            {
                Vector2 target = pointer.HasValue
                    ? new Vector2(Mathf.Clamp(pointer.Value.x, bounds.xMin, bounds.xMax),
                                  Mathf.Clamp(pointer.Value.y, bounds.yMin, bounds.yMax))
                    : _p;
                // Inverted and pouring: the hand goes no lower than the height that keeps the mouth over the
                // rim (PressInverted), the way the ceiling stops it at the full lean the right way up.
                if (_invert && _armed && target.y < _floor)
                    target.y = Mathf.Min(_floor, bounds.yMax);
                if (Motion.Reduced)
                {
                    // No springs for a player who asked for less motion: the vessel is where the
                    // hand is, at the angle the lift says.
                    _p = target; _pv = Vector2.zero;
                    TrackPeak();
                    _tilt = LeanAt(_p.y); _tiltV = 0f;
                }
                else
                {
                    for (int i = 0; i < steps; i++)
                    {
                        SpringStep.Damped(ref _p, ref _pv, target, FollowOmega, FollowZeta, h);
                        TrackPeak();

                        // The body hangs from the hand, so a push sideways swings it: accelerate left
                        // and the foot lags right, which leans the vessel left — toward the glass.
                        Vector2 acc = (_pv - _lastV) / h;
                        _lastV = _pv;
                        _ax = Mathf.Lerp(_ax, acc.x, 1f - Mathf.Exp(-AccelSmoothing * h));
                        float swing = Mathf.Clamp(-SwingPerAccel * _ax, -MaxSwing, MaxSwing);

                        float tt = LeanAt(_p.y) + swing;
                        SpringStep.Damped(ref _tilt, ref _tiltV, tt, TiltOmega, TiltZeta, h);
                    }
                }
                // CARRIED BY WHERE IT WAS TAKEN, POURED FROM THE HAND (2026-09-13, second measure).
                // Upright, the pressed point stays on the hand. As the vessel tips toward level the
                // hold slides up it to the neck, and from level on the neck is in the hand — so the
                // mouth pours where the pointer is. Holding by the label all the way round put the
                // mouth ~230 units under the hand past level: under the tin's rim and far under a
                // tall glass's, which the pour smoke tests measured as nothing poured at all.
                // BY ITS MIDDLE, FOR A BOTTLE (2026-09-14, the author: "şişenin ucundan değil ortasından
                // tutuyor olmamız gerekiyor böylece oranı daha ince ayarlayabiliriz"): the hold slides to
                // PourHold, which is the neck for the tin and the middle of the drawing for a bottle.
                _hold = Vector2.Lerp(_holdPress, PourHold, Mathf.Clamp01(_tilt / KneeTilt));
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
                for (int i = 0; i < steps; i++)
                {
                    SpringStep.Damped(ref _w, ref _wv, GripRest, HomeOmega, HomeZeta, h);
                    SpringStep.Damped(ref _tilt, ref _tiltV, 0f, HomeOmega, HomeZeta, h);
                }
            }

            if ((_w - GripRest).sqrMagnitude < 0.0025f && _wv.sqrMagnitude < 0.25f
                && Mathf.Abs(_tilt) < 0.05f && Mathf.Abs(_tiltV) < 0.5f)
                SnapHome();
        }

        /// <summary>The lean the pointer's height asks for — GDD 24 §2.2: higher tips further (lower, inverted).</summary>
        private float LeanAt(float pointerY) => Lean(Mathf.Clamp01(Lift01(pointerY)), MaxTilt);

        /// <summary>How far through the lift a hand at <paramref name="y"/> stands, unclamped: up from the lift's
        /// start, or — inverted — down from the highest the hand has been once armed (nothing before).</summary>
        private float Lift01(float y)
        {
            float range = Mathf.Max(1f, _liftRange);
            if (!_invert) return (y - _liftBase) / range;
            return _armed ? (_peak - y) / range : 0f;
        }

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
