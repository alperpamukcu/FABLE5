using System;

namespace LastCall.Core
{
    /// <summary>
    /// What a tipped vessel gives up (2026-09-11) — the bottle over the tin, the tin over the
    /// glass. The keg's sibling (<see cref="TapPour"/>): pure and stateless, the caller owns the
    /// vessel, the hand and the clock, and this only says how much a given lean lets out.
    ///
    /// GDD 24 §2 has said "more tilt = faster pour" since 2026-07 and nothing ever built it: the
    /// bottle poured at one rate the moment it passed 42 degrees, and so did the tin, however far
    /// either was tipped. The rate lived in the UI (a time scale on the shaker bench, a constant
    /// on the serving bench), which is the one place the house says a rule may not live. It lives
    /// here now, and it has two parts, both the shape of a real pour:
    ///
    ///   * THE ONSET follows the level. A full bottle runs the moment it leans — the liquid is
    ///     already at the neck — and an emptying one has to be tipped further and further before
    ///     anything reaches the lip. Full: 24 degrees. Nearly empty: about 100.
    ///   * PAST THE ONSET the flow grows with the lean, from a TRICKLE to the vessel's full rate
    ///     over <see cref="RampDeg"/> — a weir, the flow rising with the head of liquid over the
    ///     lip. The trickle is deliberate: a 5% share is still a second of steady hand near the
    ///     lip, so the ratio boxes (20 points wide, 5% floor) stay something a player can hit.
    /// </summary>
    public static class BottlePour
    {
        /// <summary>The lean, in degrees, at which a FULL vessel starts to run.</summary>
        public const double OnsetFullDeg = 24.0;
        /// <summary>The lean at which the last of an EMPTYING vessel reaches the lip.</summary>
        public const double OnsetEmptyDeg = 102.0;
        /// <summary>How the onset climbs as the vessel empties: over 1 it holds low while the
        /// vessel is well filled and rises fastest near the bottom, the way a neck behaves.</summary>
        public const double OnsetCurve = 1.3;
        /// <summary>Degrees past the onset over which the flow grows from the trickle to full.</summary>
        public const double RampDeg = 30.0;
        public const double RampPower = 1.25;
        /// <summary>The share of full flow the lip gives the instant it runs at all.</summary>
        public const double TrickleShare = 0.08;

        /// <summary>The lean, in degrees, at which a vessel <paramref name="fill"/> full starts to run.</summary>
        public static double OnsetDeg(double fill) =>
            OnsetFullDeg + (OnsetEmptyDeg - OnsetFullDeg) * Math.Pow(1.0 - Clamp01(fill), OnsetCurve);

        /// <summary>The share of full flow (0..1) at a lean of <paramref name="tiltDegrees"/> from a
        /// vessel <paramref name="fill"/> full. Zero under the onset, and zero from an empty vessel.</summary>
        public static double Share(double tiltDegrees, double fill)
        {
            if (fill <= 0) return 0;
            double over = tiltDegrees - OnsetDeg(fill);
            if (over <= 0) return 0;
            double x = Clamp01(over / RampDeg);
            return TrickleShare + (1.0 - TrickleShare) * Math.Pow(x, RampPower);
        }

        /// <summary>The volume that runs in <paramref name="seconds"/> at a lean, from a vessel
        /// whose full flow is <paramref name="fullRate"/> a second.</summary>
        public static double Volume(double tiltDegrees, double fill, double fullRate, double seconds)
        {
            if (seconds <= 0 || fullRate <= 0) return 0;
            return fullRate * Share(tiltDegrees, fill) * seconds;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }
}
