using System;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT THE ROOM IS WORTH (GDD 27 §2–§3, 2026-09-04, the author: "Oyuncular hem alkolü
    /// puanlar hem mekanı, 2 ayrı metrik olacak … bu konfor artmadan ne kadar iyi servis
    /// yaparsan yap genel yıldızın değişmeyecek").
    ///
    /// The second of the two ratings. SERVICE is what the customers thought of the drink
    /// (<see cref="BarRating.ExactStarsFor"/> of the night's mean satisfaction); COMFORT is
    /// this — the fittings the room owns, drained by the mess the night left on the counter.
    /// The night's stars are the LOWER of the two, so neither can carry the bar alone.
    ///
    /// Pure functions, no state: the run hands in what it owns and what the floor recorded,
    /// and reads a number back. Every constant is a starting stake for the 200-run sim to
    /// move (GDD 27 §7), which is why they are named rather than inlined.
    /// </summary>
    public static class VenueComfort
    {
        /// <summary>The room as it opens, before anything is bought. Deliberately the number
        /// the old fittings ceiling (UpgradeStarCap) opened with, so a fresh bar still caps
        /// at two stars and every pin that says so stays true.</summary>
        public const double FreeBase = 2.0;

        /// <summary>How much of the glassware ladder's measured cap
        /// (<c>TycoonRun.GlassStepCap</c>) still counts. Half: it was the only route to the
        /// ceiling and must stop being the only one, but five lines of bought glass cannot
        /// be worth nothing to the stars — that is a refund the shop never promised.</summary>
        public const double GlassComfortShare = 0.5;

        /// <summary>Each stool past the four the bar opens with.</summary>
        public const double StoolComfort = 0.25;

        /// <summary>The most a filthy night can take off the room. A bar that wipes and
        /// collects as it goes loses nothing; one that never touches the counter loses all
        /// of this. ONE star, not more, on purpose: a fresh room is worth two, and the
        /// broke-crowd line sits at 0.625 (<see cref="BarRating.BrokeStars"/>) — the mess
        /// may hold the standing down, it may not by itself turn tomorrow's crowd broke
        /// (the crowd reads the SERVICE side, GDD 27 §2.3). The 200-run latency bot moves
        /// it from here.</summary>
        public const double DirtPenalty = 1.0;

        /// <summary>Comfort is read on the star scale: five is the endgame room.</summary>
        public const double MaxComfort = BarRating.MaxStars;

        /// <summary>
        /// What the room is worth with nobody in it. Fittings only; it changes at the market,
        /// never during a night.
        /// </summary>
        /// <param name="fixtureComfort">Σ <c>FixtureDefinition.Comfort</c> over the standing
        /// rung of every ladder slot plus every owned single piece (a fitted-over rung counts
        /// nothing — rungs carry absolute values).</param>
        /// <param name="glassStepCaps">Σ of the glassware ladder's step caps, as the old
        /// ceiling summed them, BEFORE this class halves it.</param>
        /// <param name="extraStools">Stools past the opening four.</param>
        public static double Base(double fixtureComfort, double glassStepCaps, int extraStools)
        {
            if (fixtureComfort < 0) throw new ArgumentOutOfRangeException(nameof(fixtureComfort));
            if (glassStepCaps < 0) throw new ArgumentOutOfRangeException(nameof(glassStepCaps));
            double raw = FreeBase + fixtureComfort
                         + GlassComfortShare * glassStepCaps
                         + StoolComfort * Math.Max(0, extraStools);
            return Clamp(raw);
        }

        /// <summary>
        /// The night's filed comfort: the base, less what the mess cost. Read once, at close,
        /// off the time-weighted <paramref name="cleanliness"/> the floor kept
        /// (<see cref="Housekeeping.Cleanliness"/>), which is already clamped to [0, 1] so
        /// the most this can take off is <see cref="DirtPenalty"/>.
        /// </summary>
        public static double Tonight(double comfortBase, double cleanliness) =>
            Clamp(comfortBase - DirtPenalty * (1.0 - Clamp01(cleanliness)));

        /// <summary>
        /// The shift's live reading — the same rule read off the counter as it stands this
        /// second, so a gauge drops when a glass is left and recovers when it is carried away.
        /// <paramref name="dirtySpots"/> counts what is past its grace, exactly as the night's
        /// exposure does, so the two readings never disagree about what dirt is.
        /// </summary>
        public static double Now(double comfortBase, int dirtySpots, int seats)
        {
            if (seats <= 0) return Clamp(comfortBase);
            double share = Math.Min(1.0, Math.Max(0, dirtySpots) / (double)seats);
            return Clamp(comfortBase - DirtPenalty * share);
        }

        /// <summary>
        /// The night's stars: the LOWER of the two ratings. Service that outran the room is
        /// held to the room; a fine room with poor service is held to the service.
        /// </summary>
        public static double NightStars(double serviceStars, double comfortStars) =>
            Math.Min(Clamp(serviceStars), Clamp(comfortStars));

        private static double Clamp(double v) => Math.Max(0.0, Math.Min(MaxComfort, v));
        private static double Clamp01(double v) => Math.Max(0.0, Math.Min(1.0, v));
    }
}
