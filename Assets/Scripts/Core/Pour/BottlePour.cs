using System;

namespace LastCall.Core
{
    /// <summary>
    /// What a tipped vessel gives up (2026-09-11) — the bottle over the tin, the tin over the
    /// glass. The keg's sibling (<see cref="TapPour"/>): pure and stateless, the caller owns the
    /// vessel, the hand and the clock, and this only says how much a given lean lets out.
    ///
    /// THE LEAN PAST LEVEL (2026-09-13, the author: "şişeyi 90 dereceden sonra ne kadar
    /// yatırıyorsa o kadar hızlı dolsun, tam 90 derece şişe ise en hızlı şekilde; eğer yere
    /// paralelleşiyorsa dökülme hızı yavaşlamaya başlasın ve paralelleştiğinde dursun").
    ///
    /// Leans are measured from upright. Nothing runs until the vessel is past LEVEL (90 degrees
    /// — lying parallel to the floor); from there the flow grows in proportion to the lean and
    /// is at its fastest with the neck pointing STRAIGHT DOWN (180, the bottle perpendicular to
    /// the floor, which is the author's "tam 90 derece"). Tipped on past straight down it comes
    /// back towards level on the far side, slows the same way, and stops when it lies level
    /// again (270). The level in the vessel no longer moves the lip: the rule is the angle, so a
    /// hand that learns one bottle has learned every bottle.
    ///
    /// The old law (a lip that followed the level, 24 degrees full to 102 empty, a 30-degree ramp
    /// from an 8% trickle) is what this replaced.
    ///
    /// A TRICKLE FIRST (2026-09-14, the author: "oyuncu %1 koymaya çalışırken zorlanmasın"). The share
    /// is the SQUARE of how far the lean has come from level toward straight down. In proportion, ten
    /// degrees past level ran 11% of full flow — a percent of the tin in a quarter of a second at the
    /// bench's 0.33 tin/s — so a small measure was a flick. Squared, the first fifteen degrees are a
    /// trickle (under 3%, a percent in over a second) and the same full flow is still there neck-down;
    /// the lean that gives 0.5..3% a second is about 2.5 times as wide.
    /// </summary>
    public static class BottlePour
    {
        /// <summary>The lean at which anything runs at all: level with the floor.</summary>
        public const double OnsetDeg = 90.0;

        /// <summary>The lean of full flow: the neck straight down.</summary>
        public const double FullDeg = 180.0;

        /// <summary>
        /// The share of full flow (0..1) at a lean of <paramref name="tiltDegrees"/> from upright.
        /// Zero at or under level, one straight down, zero again lying level the other way — and
        /// zero from an empty vessel however it is held.
        /// </summary>
        public static double Share(double tiltDegrees, double fill)
        {
            if (fill <= 0 || double.IsNaN(tiltDegrees) || double.IsInfinity(tiltDegrees)) return 0;
            double t = tiltDegrees % 360.0;
            if (t < 0) t += 360.0;
            double fromStraightDown = Math.Abs(t - FullDeg);   // 0 neck down .. 90 lying level
            double u = 1.0 - fromStraightDown / (FullDeg - OnsetDeg);   // 0 level .. 1 straight down
            if (u <= 0) return 0;
            return u >= 1 ? 1 : u * u;
        }

        /// <summary>The volume that runs in <paramref name="seconds"/> at a lean, from a vessel
        /// whose full flow is <paramref name="fullRate"/> a second.</summary>
        public static double Volume(double tiltDegrees, double fill, double fullRate, double seconds)
        {
            if (seconds <= 0 || fullRate <= 0) return 0;
            return fullRate * Share(tiltDegrees, fill) * seconds;
        }
    }
}
