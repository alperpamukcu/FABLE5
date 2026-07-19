using System;

namespace LastCall.Core
{
    /// <summary>
    /// Judges one serve against what the customer actually asked for (GDD 19 §6).
    /// Pure and total: no randomness, no state.
    ///
    /// The tension is blackjack's. You are aiming at 0 or at 100, you often cannot see
    /// exactly where they are, and going past is worse than stopping short. The reward for
    /// reading someone right — especially when the card told you nothing — is where the
    /// Mult comes from now.
    /// </summary>
    public static class ResonanceJudge
    {
        /// <summary>Units of progress that buy 1 Mult.</summary>
        public const double ProgressPerMult = 10.0;

        /// <summary>Flat Mult for landing progress on a stat the ID card left blank.</summary>
        public const double LuckyReadBonus = 3.0;

        public const double CleanServeBurst = 2.0;
        public const double BlindCleanServeBurst = 3.0;

        /// <summary>Mult lost for a bust. Applied after the additive block; Mult floors at 1.</summary>
        public const double BustPenalty = 2.0;

        /// <summary>Wrong-way movement up to this much is a slip, not a bust (GDD 19 §6).</summary>
        public const int DriftTolerance = 10;

        // How much progress reads as "a real change, not a nudge" is no longer a constant:
        // it scales with how hard the customer is to please. See Demands.StrongProgress.

        /// <summary>
        /// Judges the intent stat only. <paramref name="delta"/> must be the raw, unclamped
        /// movement — clamping at 0/100 would hide an overshoot, and an overshoot is exactly
        /// what we are looking for.
        /// </summary>
        public static ResonanceResult Judge(EmotionStats before, EmotionDelta delta, CustomerRead read)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (delta == null || delta.IsEmpty) return ResonanceResult.None;

            var intent = read.Intent;
            bool blind = read.IsBlindRead;
            int start = before[intent];
            int movement = delta[intent];
            int rawAfter = start + movement;
            int target = read.TargetValue;
            bool towardZero = read.Direction == IntentDirection.Extinguish;

            // Past the target. Whatever they needed, this was more of it than they could take.
            bool overshot = towardZero ? rawAfter < target : rawAfter > target;
            if (overshot) return Busted(BustKind.Overshoot, blind);

            int progress = towardZero ? start - rawAfter : rawAfter - start;

            if (progress < 0)
            {
                // Moved them away from what they asked for.
                int wrongWay = -progress;
                if (wrongWay > DriftTolerance) return Busted(BustKind.WrongWay, blind);

                // A small slip still lands in the glass — it just earns nothing.
                return new ResonanceResult(0, 0, 1, 0, BustKind.None, false, blind, 0, 0, delta);
            }

            bool cleanServe = rawAfter == target;
            double resonance = progress / ProgressPerMult;
            double lucky = blind && progress > 0 ? LuckyReadBonus : 0;
            double burst = cleanServe ? (blind ? BlindCleanServeBurst : CleanServeBurst) : 1;

            return new ResonanceResult(resonance, lucky, burst, 0, BustKind.None,
                cleanServe, blind, progress,
                SatisfactionFor(progress, cleanServe, read.Demand), delta);
        }

        /// <summary>
        /// What the serve is worth to the week (GDD 19 §10). Deliberately coarse: the player
        /// should feel "I got that one" or "I didn't", not compute a decimal.
        ///
        /// A demanding customer moves the goalposts, not the ceiling: a Clean Serve is always
        /// worth 3, because landing someone exactly where they asked cannot be improved on.
        /// What rises is how much movement counts as *feeling* something — and only the
        /// Demanding have a floor beneath which a serve is worth nothing at all.
        /// </summary>
        public static int SatisfactionFor(int progress, bool cleanServe,
            DemandLevel demand = DemandLevel.Easygoing)
        {
            if (cleanServe) return 3;
            if (progress >= Demands.StrongProgress(demand)) return 2;
            return progress >= Demands.MinProgress(demand) ? 1 : 0;
        }

        /// <summary>A bust scores badly and — critically — never touches their stats.</summary>
        private static ResonanceResult Busted(BustKind kind, bool blind) =>
            new ResonanceResult(0, 0, 1, BustPenalty, kind, false, blind, 0, 0, EmotionDelta.Empty);
    }
}
